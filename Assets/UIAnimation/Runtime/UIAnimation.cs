using System;
using System.Collections.Generic;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SeoBYP.TimelineAnimation
{
    public enum UIAnimProperty
    {
        Scale,              // Transform.localScale
        SizeDelta,          // RectTransform.sizeDelta
        AnchoredPosition,   // RectTransform.anchoredPosition
        GraphicColor,       // Graphic.color
        GraphicAlpha,       // Graphic.color.a
        FontSize,           // TMP_Text.fontSize
    }

    /// <summary>How a new state treats states that are still playing on the same properties.</summary>
    public enum UIAnimInterrupt
    {
        Cancel,     // stop them where they are; tracks that start from current continue from there
        Complete,   // snap them to their end first
    }

    public enum UIAnimCurveType
    {
        Ease,       // LitMotion ease preset
        Curve,      // AnimationCurve over the segment: 0 = previous key's value, 1 = this key's value
    }

    public enum UIAnimActionType
    {
        MoveToPointer,  // places the target under the pointer that triggered the state
    }

    /// <summary>A value at a time. Ease/curve describe how the track arrives at this key from the previous one.</summary>
    [Serializable]
    public sealed class UIAnimKey
    {
        public float time;
        public Vector4 value;
        public UIAnimCurveType curveType = UIAnimCurveType.Ease;
        public Ease ease = Ease.OutSine;
        public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [NonSerialized] internal Vector4 resolved;

        /// <summary>Segment progress (0..1) → interpolation weight. Not clamped, so curves may overshoot.</summary>
        public float Weight(float t01) => curveType == UIAnimCurveType.Curve && curve != null
            ? curve.Evaluate(t01)
            : EaseUtility.Evaluate(t01, ease);
    }

    [Serializable]
    public sealed class UIAnimTrack
    {
        public Component target;
        public UIAnimProperty property;
        [Tooltip("The first key takes the value the property has when the state starts, so an interrupted animation never jumps. With one key: current → that key.")]
        public bool startFromCurrent = true;
        [Tooltip("Key values are added to the value the property had on Awake (e.g. -14 = 14 smaller than the original size).")]
        public bool relativeToRest;
        public List<UIAnimKey> keys = new();      // sorted by time

        [NonSerialized] internal Vector4 rest;          // value captured on Awake, base for relativeToRest
        [NonSerialized] internal Vector4 startValue;    // value at state start, when startFromCurrent
        [NonSerialized] internal bool released;         // another playing state took this property over

        public float End => keys.Count > 0 ? keys[keys.Count - 1].time : 0f;

        /// <summary>Value at time t. Before the first key it holds (or comes from the start value), after the last it holds.</summary>
        internal Vector4 Sample(float t)
        {
            var first = keys[0];
            // A single key is a "go to" track: implicit segment from the live start value to that key.
            if (startFromCurrent && keys.Count == 1 && first.time > 0f && t < first.time)
                return Vector4.LerpUnclamped(startValue, first.resolved, first.Weight(t / first.time));
            if (t <= first.time) return first.resolved;   // holds until the first key (a delay when it sits after 0)

            for (var i = 1; i < keys.Count; i++)
            {
                var key = keys[i];
                if (t >= key.time) continue;
                var prev = keys[i - 1];
                var span = key.time - prev.time;
                var p = span > 0f ? (t - prev.time) / span : 1f;
                return Vector4.LerpUnclamped(prev.resolved, key.resolved, key.Weight(p));
            }
            return keys[keys.Count - 1].resolved;
        }
    }

    [Serializable]
    public sealed class UIAnimAction
    {
        public UIAnimActionType type;
        public RectTransform target;
    }

    [Serializable]
    public sealed class UIAnimState
    {
        public string name = "State";
        [Tooltip("Editor preview only: this state is applied to its end first, so e.g. 'Off' is previewed starting from 'On'.")]
        public string previewFrom = "";
        public List<UIAnimAction> actions = new();
        public List<UIAnimTrack> tracks = new();

        public float Duration
        {
            get
            {
                var d = 0f;
                foreach (var track in tracks) d = Mathf.Max(d, track.End);
                return d;
            }
        }
    }

    /// <summary>
    /// Holds the animation states of one prefab and plays them.
    /// States can run at the same time; a newly played state takes over only the properties it animates.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIAnimation : MonoBehaviour
    {
        public List<UIAnimState> states = new();

        internal sealed class Playback
        {
            public readonly UIAnimState state;
            public MotionHandle handle;
            public bool active;
            public readonly Action onComplete;

            public Playback(UIAnimState state)
            {
                this.state = state;
                onComplete = () => active = false;
            }
        }

        readonly List<Playback> _playbacks = new();
        bool _initialized;

        public bool IsPlaying(string stateName)
        {
            var p = FindPlayback(stateName);
            return p != null && p.active;
        }

        void Awake() => EnsureInitialized();

        void OnDisable()
        {
            foreach (var p in _playbacks) Stop(p);
        }

        internal void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            _playbacks.Clear();
            foreach (var state in states) _playbacks.Add(new Playback(state));
            CaptureRest();
        }

        /// <summary>Records the current values as the base for relative tracks. The editor preview calls this on every start.</summary>
        internal void CaptureRest()
        {
            foreach (var state in states)
            foreach (var track in state.tracks)
            {
                track.released = false;
                if (track.target != null) track.rest = Read(track.target, track.property);
            }
        }

        public void Play(string stateName, UIAnimInterrupt interrupt = UIAnimInterrupt.Cancel,
            PointerEventData pointer = null)
        {
            var playback = Prepare(stateName, interrupt, pointer);
            if (playback == null) return;

            var duration = playback.state.Duration;
            if (duration <= 0f)
            {
                Evaluate(playback.state, 0f);
                playback.active = false;
                return;
            }

            playback.handle = LMotion.Create(0f, duration, duration)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(playback.onComplete)
                .Bind(playback, static (t, p) => Evaluate(p.state, t));
        }

        /// <summary>Everything Play does except starting the motion. Split out so tests can drive time.</summary>
        internal Playback Prepare(string stateName, UIAnimInterrupt interrupt, PointerEventData pointer)
        {
            EnsureInitialized();
            var playback = FindPlayback(stateName);
            if (playback == null)
            {
                Debug.LogWarning($"[UIAnimation] '{name}' has no state '{stateName}'.", this);
                return null;
            }

            Stop(playback);
            TakeOver(playback.state, interrupt);

            foreach (var track in playback.state.tracks) track.released = false;
            foreach (var action in playback.state.actions) Run(action, pointer);
            Resolve(playback.state);

            playback.active = true;
            return playback;
        }

        // Other playing states give up the properties this state animates.
        void TakeOver(UIAnimState incoming, UIAnimInterrupt interrupt)
        {
            foreach (var other in _playbacks)
            {
                if (!other.active || other.state == incoming || !Overlaps(other.state, incoming)) continue;

                if (interrupt == UIAnimInterrupt.Complete)
                {
                    Evaluate(other.state, other.state.Duration);
                    Stop(other);
                    continue;
                }

                var anyLeft = false;
                foreach (var track in other.state.tracks)
                {
                    if (!track.released && Animates(incoming, track)) track.released = true;
                    anyLeft |= !track.released;
                }
                if (!anyLeft) Stop(other);
            }
        }

        static bool Overlaps(UIAnimState a, UIAnimState b)
        {
            foreach (var track in a.tracks)
                if (Animates(b, track)) return true;
            return false;
        }

        static bool Animates(UIAnimState state, UIAnimTrack other)
        {
            foreach (var track in state.tracks)
                if (track.target == other.target && track.property == other.property) return true;
            return false;
        }

        static void Stop(Playback p)
        {
            p.handle.TryCancel();
            p.active = false;
        }

        Playback FindPlayback(string stateName)
        {
            foreach (var p in _playbacks)
                if (p.state.name == stateName) return p;
            return null;
        }

        /// <summary>Resolves key values (relative/absolute) and the live start value. Call once right before evaluating from t = 0.</summary>
        internal static void Resolve(UIAnimState state)
        {
            foreach (var track in state.tracks)
            {
                if (track.target == null || track.keys.Count == 0) continue;
                track.startValue = Read(track.target, track.property);
                foreach (var key in track.keys)
                    key.resolved = track.relativeToRest ? track.rest + key.value : key.value;
                // The first key becomes the live value, so the track starts where the property is. With 2+ keys it holds
                // until its time (= a delay); a lone key keeps its value and is reached through the implicit segment.
                if (track.startFromCurrent && (track.keys.Count > 1 || track.keys[0].time <= 0f)) track.keys[0].resolved = track.startValue;
            }
        }

        /// <summary>Writes the state at time t. Shared by runtime playback, tests and the editor preview.</summary>
        internal static void Evaluate(UIAnimState state, float t)
        {
            foreach (var track in state.tracks)
            {
                if (track.released || track.target == null || track.keys.Count == 0) continue;
                Write(track.target, track.property, track.Sample(t));
            }
        }

        static void Run(UIAnimAction action, PointerEventData pointer)
        {
            if (action.target == null) return;
            switch (action.type)
            {
                case UIAnimActionType.MoveToPointer:
                    if (pointer == null) return;
                    var space = action.target.parent as RectTransform;
                    if (space != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(
                            space, pointer.position, pointer.pressEventCamera, out var world))
                        action.target.position = world;
                    break;
            }
        }

        public static Vector4 Read(Component target, UIAnimProperty property) => property switch
        {
            UIAnimProperty.Scale => target.transform.localScale,
            UIAnimProperty.SizeDelta => ((RectTransform)target.transform).sizeDelta,
            UIAnimProperty.AnchoredPosition => ((RectTransform)target.transform).anchoredPosition,
            UIAnimProperty.GraphicColor => ((Graphic)target).color,
            UIAnimProperty.GraphicAlpha => new Vector4(((Graphic)target).color.a, 0, 0, 0),
            UIAnimProperty.FontSize => new Vector4(((TMP_Text)target).fontSize, 0, 0, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(property)),
        };

        public static void Write(Component target, UIAnimProperty property, Vector4 v)
        {
            switch (property)
            {
                case UIAnimProperty.Scale: target.transform.localScale = v; break;
                case UIAnimProperty.SizeDelta: ((RectTransform)target.transform).sizeDelta = v; break;
                case UIAnimProperty.AnchoredPosition: ((RectTransform)target.transform).anchoredPosition = v; break;
                case UIAnimProperty.GraphicColor: ((Graphic)target).color = v; break;
                case UIAnimProperty.GraphicAlpha:
                    var g = (Graphic)target;
                    var c = g.color;
                    c.a = v.x;
                    g.color = c;
                    break;
                case UIAnimProperty.FontSize: ((TMP_Text)target).fontSize = v.x; break;
                default: throw new ArgumentOutOfRangeException(nameof(property));
            }
        }
    }
}
