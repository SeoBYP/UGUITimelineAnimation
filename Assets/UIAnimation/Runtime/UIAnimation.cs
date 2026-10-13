using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
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
        TextReveal,         // TMP_Text.maxVisibleCharacters as 0..1 of the text (typewriter)
        CharScale,          // per character, around its center
        CharOffset,         // per character, local position offset
        CharRotation,       // per character, euler angles around its center
        CharColor,          // per character vertex color
        CharAlpha,          // per character vertex alpha
        // appended: values are serialized as ints, so new members go last
        FillAmount,         // Image.fillAmount
        CharacterSpacing,   // TMP_Text.characterSpacing
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
        Punch,      // LMotion.Punch: oscillates around the previous value with this key's value as strength, ends back there
        Shake,      // LMotion.Shake: like Punch but each component jitters randomly every frame
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
        [Tooltip("Punch/Shake: oscillations per second. The count over the segment is (int)(length x this), like LitMotion's frequency.")]
        public float frequency = 10f;
        [Tooltip("Punch/Shake: how fast the oscillation dies out (LitMotion dampingRatio).")]
        public float damping = 1f;

        [NonSerialized] internal Vector4 resolved;

        public bool IsVibration => curveType is UIAnimCurveType.Punch or UIAnimCurveType.Shake;

        /// <summary>Oscillation count over a segment of this many seconds.</summary>
        internal int Oscillations(float seconds) => (int)(seconds * frequency + 1e-4f);

        /// <summary>
        /// Segment progress (0..1) to interpolation weight. Not clamped, so curves may overshoot.
        /// For Punch/Shake this is the (1-second) envelope, used for drawing; sampling uses the real segment length.
        /// </summary>
        public float Weight(float t01) => curveType switch
        {
            UIAnimCurveType.Curve when curve != null => curve.Evaluate(t01),
            UIAnimCurveType.Punch or UIAnimCurveType.Shake => UIAnimVibration.Envelope(Oscillations(1f), damping, EaseUtility.Evaluate(t01, ease)),
            _ => EaseUtility.Evaluate(t01, ease),
        };
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
        [Tooltip("Per-character tracks only: each character starts this many seconds after the previous one.")]
        public float stagger;
        [Tooltip("Per-character tracks only: when above 0, character i plays the keys over i x this many seconds (character 0 stays still).")]
        public float durationPerChar;
        [Tooltip("Text Reveal only: characters not yet revealed are shown as random characters (LitMotion WithScrambleChars).")]
        public ScrambleMode scramble = ScrambleMode.None;
        public List<UIAnimKey> keys = new();      // sorted by time

        [NonSerialized] internal Vector4 rest;          // value captured on Awake, base for relativeToRest
        [NonSerialized] internal Vector4 startValue;    // value at state start, when startFromCurrent
        [NonSerialized] internal bool released;         // another playing state took this property over

        public float End
        {
            get
            {
                if (keys.Count == 0) return 0f;
                var end = keys[keys.Count - 1].time;
                if (UIAnimTextChars.IsCharProperty(property) && target is TMP_Text text && text.textInfo != null)
                {
                    // The last character starts (count - 1) x stagger later and, with durationPerChar, also lasts longest.
                    var last = Mathf.Max(0, text.textInfo.characterCount - 1);
                    end = last * stagger + (durationPerChar > 0f ? last * durationPerChar : end);
                }
                return end;
            }
        }

        /// <summary>
        /// Value at time t. Before the first key it holds (or comes from the start value), after the last it holds.
        /// <paramref name="timeScale"/> stretches the key times (per-character durationPerChar).
        /// </summary>
        internal Vector4 Sample(float t, float timeScale = 1f)
        {
            var first = keys[0];
            var firstTime = first.time * timeScale;
            // A single key is a "go to" track: implicit segment from the live start value to that key.
            if (startFromCurrent && keys.Count == 1 && firstTime > 0f && t < firstTime)
                return Segment(startValue, first, t, firstTime);
            if (t <= firstTime) return first.resolved;   // holds until the first key (a delay when it sits after 0)

            for (var i = 1; i < keys.Count; i++)
            {
                var key = keys[i];
                if (t >= key.time * timeScale) continue;
                var prevTime = keys[i - 1].time * timeScale;
                return Segment(keys[i - 1].resolved, key, t - prevTime, key.time * timeScale - prevTime);
            }
            return keys[keys.Count - 1].resolved;
        }

        // From a value to a key over `span` seconds, at `local` seconds into the segment.
        Vector4 Segment(Vector4 from, UIAnimKey key, float local, float span)
        {
            var p = span > 0f ? Mathf.Clamp01(local / span) : 1f;
            if (!key.IsVibration) return Vector4.LerpUnclamped(from, key.resolved, key.Weight(p));

            // Punch/Shake: key.value is the strength; the motion oscillates around `from` and ends back on it.
            var envelope = UIAnimVibration.Envelope(key.Oscillations(span), key.damping, EaseUtility.Evaluate(p, key.ease));
            var strength = key.value * envelope;
            if (key.curveType == UIAnimCurveType.Shake)
                strength = Vector4.Scale(strength, UIAnimVibration.RandomSigned(0u, local, Dimensions(property)));
            return from + strength;
        }

        static int Dimensions(UIAnimProperty p) => p switch
        {
            UIAnimProperty.SizeDelta or UIAnimProperty.AnchoredPosition => 2,
            UIAnimProperty.Scale or UIAnimProperty.CharScale or UIAnimProperty.CharOffset or UIAnimProperty.CharRotation => 3,
            UIAnimProperty.GraphicColor or UIAnimProperty.CharColor => 4,
            _ => 1,
        };
    }

    [Serializable]
    public sealed class UIAnimAction
    {
        public UIAnimActionType type;
        public RectTransform target;
    }

    /// <summary>Marks a struct as custom data an animation event can carry (e.g. <c>struct HitEvent : IUIAnimEventData { public float damage; }</c>).</summary>
    public interface IUIAnimEventData { }

    /// <summary>
    /// An animation event: a named point in a state's time, raised once when playback passes it (e.g. play a sound on impact).
    /// It can carry one custom <see cref="IUIAnimEventData"/> struct, stored as JSON and converted once on first use.
    /// </summary>
    [Serializable]
    public sealed class UIAnimEventMarker
    {
        public float time;
        public string name = "Event";
        public string dataType = "";   // "Namespace.Type, Assembly" of an IUIAnimEventData struct, or empty
        public string dataJson = "";

        [NonSerialized] object _data;   // boxed once; reading it copies the struct out without allocating
        [NonSerialized] bool _parsed;

        public static string TypeKey(Type type) => type == null ? "" : $"{type.FullName}, {type.Assembly.GetName().Name}";

        public Type DataType => string.IsNullOrEmpty(dataType) ? null : Type.GetType(dataType);

        internal bool TryGetData<T>(out T data) where T : struct, IUIAnimEventData
        {
            if (!_parsed)
            {
                _parsed = true;
                var type = DataType;
                _data = type == null ? null : JsonUtility.FromJson(string.IsNullOrEmpty(dataJson) ? "{}" : dataJson, type);
            }
            if (_data is T value)
            {
                data = value;
                return true;
            }
            data = default;
            return false;
        }

        /// <summary>Call after changing dataType/dataJson (the editor does).</summary>
        public void InvalidateData()
        {
            _parsed = false;
            _data = null;
        }
    }

    /// <summary>What an animation event handler receives (like Unity 6.5's AnimationEventInfo): passed by <c>in</c>, no allocation.</summary>
    public readonly struct UIAnimEventInfo
    {
        public readonly UIAnimation Animation;
        public readonly string State;
        public readonly string Name;
        public readonly float Time;
        readonly UIAnimEventMarker _marker;

        internal UIAnimEventInfo(UIAnimation animation, string state, UIAnimEventMarker marker)
        {
            Animation = animation;
            State = state;
            Name = marker.name;
            Time = marker.time;
            _marker = marker;
        }

        /// <summary>The event's custom data, if it carries a <typeparamref name="T"/>.</summary>
        public bool TryGetData<T>(out T data) where T : struct, IUIAnimEventData
        {
            if (_marker != null) return _marker.TryGetData(out data);
            data = default;
            return false;
        }
    }

    public delegate void UIAnimEventHandler(in UIAnimEventInfo e);
    public delegate void UIAnimEventHandler<T>(in UIAnimEventInfo e, in T data) where T : struct, IUIAnimEventData;

    /// <summary>Plays another UIAnimation's state at a point in this state's time (e.g. a popup showing its buttons).</summary>
    [Serializable]
    public sealed class UIAnimChildCall
    {
        public float time;
        public UIAnimation child;
        public string state = "";

        public UIAnimState Target => child != null ? child.FindState(state) : null;
    }

    [Serializable]
    public sealed class UIAnimState
    {
        internal const int MaxCallDepth = 8;   // a child calling back into its parent is cut here
        [ThreadStatic] static int _durationDepth;

        public string name = "State";
        public List<UIAnimAction> actions = new();
        public List<UIAnimTrack> tracks = new();
        [FormerlySerializedAs("signals")]
        public List<UIAnimEventMarker> events = new();      // animation events, sorted by time
        public List<UIAnimChildCall> childCalls = new();    // sorted by time

        /// <summary>Until the last track, animation event or called child state ends.</summary>
        public float Duration
        {
            get
            {
                var d = 0f;
                foreach (var track in tracks) d = Mathf.Max(d, track.End);
                foreach (var e in events) d = Mathf.Max(d, e.time);
                if (_durationDepth >= MaxCallDepth) return d;
                _durationDepth++;
                try
                {
                    foreach (var call in childCalls) d = Mathf.Max(d, call.time + (call.Target?.Duration ?? 0f));
                }
                finally { _durationDepth--; }
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
            public readonly UIAnimation owner;
            public readonly UIAnimState state;
            public MotionHandle handle;
            public bool active;
            public float lastTime;                                // events in (lastTime, t] are raised on the next step
            public readonly Action onComplete;
            public AutoResetUniTaskCompletionSource completion;   // only while someone awaits PlayAsync
            public CancellationTokenRegistration cancelRegistration;

            public Playback(UIAnimation owner, UIAnimState state)
            {
                this.owner = owner;
                this.state = state;
                onComplete = () => Finish(this);
            }
        }

        /// <summary>
        /// Raised when playback passes an animation event. Skipping to the end (Complete) raises the skipped ones in order;
        /// a canceled play does not. Lambdas need the parameter type: <c>anim.EventRaised += (in UIAnimEventInfo e) =&gt; ...</c>
        /// </summary>
        public event UIAnimEventHandler EventRaised;

        /// <summary>Calls <paramref name="handler"/> for events carrying a <typeparamref name="T"/>; dispose the result to stop.</summary>
        public IDisposable AddListener<T>(UIAnimEventHandler<T> handler) where T : struct, IUIAnimEventData
        {
            void OnEvent(in UIAnimEventInfo e)
            {
                if (e.TryGetData(out T data)) handler(in e, in data);
            }
            EventRaised += OnEvent;
            return new Unsubscriber(this, OnEvent);
        }

        sealed class Unsubscriber : IDisposable
        {
            UIAnimation _owner;
            readonly UIAnimEventHandler _handler;
            public Unsubscriber(UIAnimation owner, UIAnimEventHandler handler) => (_owner, _handler) = (owner, handler);

            public void Dispose()
            {
                if (_owner != null) _owner.EventRaised -= _handler;
                _owner = null;
            }
        }

        /// <summary>Completes with the event the next time any playing state raises <paramref name="eventName"/>.</summary>
        public async UniTask<UIAnimEventInfo> WaitEventAsync(string eventName, CancellationToken cancellationToken = default)
        {
            var source = new UniTaskCompletionSource<UIAnimEventInfo>();
            void OnEvent(in UIAnimEventInfo e)
            {
                if (e.Name == eventName) source.TrySetResult(e);
            }
            EventRaised += OnEvent;
            try { return await source.Task.AttachExternalCancellation(cancellationToken); }
            finally { EventRaised -= OnEvent; }
        }

        readonly List<Playback> _playbacks = new();
        bool _initialized;

        internal UIAnimState FindState(string stateName)
        {
            foreach (var s in states)
                if (s.name == stateName) return s;
            return null;
        }

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
            foreach (var state in states) _playbacks.Add(new Playback(this, state));
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

        /// <summary>For UnityEvents (e.g. a Button's OnClick), which can only pass one argument.</summary>
        public void PlayState(string stateName) => Play(stateName);

        public void Play(string stateName, UIAnimInterrupt interrupt = UIAnimInterrupt.Cancel,
            PointerEventData pointer = null)
        {
            var playback = Prepare(stateName, interrupt, pointer);
            if (playback != null) StartMotion(playback);
        }

        /// <summary>
        /// Plays a state and completes when it reaches its end (or <see cref="Complete"/> skips there).
        /// Canceled (OperationCanceledException) when the state does not finish: another state took all of its
        /// properties, it was played again, the object was disabled, or <paramref name="cancellationToken"/> fired.
        /// e.g. <c>await anim.PlayAsync("Close"); Destroy(gameObject);</c> never destroys a popup that was reopened mid-close.
        /// </summary>
        public UniTask PlayAsync(string stateName, UIAnimInterrupt interrupt = UIAnimInterrupt.Cancel,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return UniTask.FromCanceled(cancellationToken);
            var playback = Prepare(stateName, interrupt, null);
            if (playback == null) return UniTask.CompletedTask;
            var task = Await(playback, cancellationToken);
            StartMotion(playback);
            return task;
        }

        /// <summary>Jumps a playing state to its end; a pending PlayAsync completes normally (e.g. skipping a typewriter).</summary>
        public void Complete(string stateName)
        {
            var playback = FindPlayback(stateName);
            if (playback == null || !playback.active) return;
            playback.handle.TryCancel();
            Evaluate(playback.state, playback.state.Duration);
            Finish(playback);
        }

        static void StartMotion(Playback playback)
        {
            var duration = playback.state.Duration;
            if (duration <= 0f)
            {
                Finish(playback);
                return;
            }

            playback.handle = LMotion.Create(0f, duration, duration)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(playback.onComplete)
                .Bind(playback, static (t, p) => Step(p, t));
        }

        /// <summary>One frame of a playback: values, then the child calls and animation events passed since the last frame. Internal so tests can drive time.</summary>
        internal static void Step(Playback p, float t)
        {
            Evaluate(p.state, t);
            RaiseEvents(p, t);
        }

        [ThreadStatic] static int _callDepth;   // parent and child calling each other at the same time stop here

        static void RaiseEvents(Playback p, float t)
        {
            var from = p.lastTime;
            p.lastTime = t;
            if (_callDepth < UIAnimState.MaxCallDepth)
            {
                _callDepth++;
                try
                {
                    foreach (var call in p.state.childCalls)
                        if (call.time > from && call.time <= t && call.child != null) call.child.Play(call.state);
                }
                finally { _callDepth--; }
            }
            var handler = p.owner.EventRaised;
            if (handler == null) return;
            foreach (var marker in p.state.events)
                if (marker.time > from && marker.time <= t) handler(new UIAnimEventInfo(p.owner, p.state.name, marker));
        }

        /// <summary>The task for this play; split out so tests can drive time.</summary>
        internal static UniTask Await(Playback playback, CancellationToken cancellationToken)
        {
            playback.completion = AutoResetUniTaskCompletionSource.Create();
            if (cancellationToken.CanBeCanceled)
                playback.cancelRegistration = cancellationToken.RegisterWithoutCaptureExecutionContext(
                    static p => Stop((Playback)p), playback);
            return playback.completion.Task;
        }

        // Reached the end: the state stays on its last values.
        static void Finish(Playback p)
        {
            RaiseEvents(p, float.MaxValue);   // skipped to the end: the remaining calls and events still happen, in order
            foreach (var call in p.state.childCalls)
                if (call.child != null && call.child.IsPlaying(call.state)) call.child.Complete(call.state);
            p.active = false;
            Release(p)?.TrySetResult();
        }

        // Did not reach the end.
        static void Stop(Playback p)
        {
            p.handle.TryCancel();
            p.active = false;
            Release(p)?.TrySetCanceled();
        }

        // Hands the completion source out exactly once; the pooled source must not be touched after it is set.
        static AutoResetUniTaskCompletionSource Release(Playback p)
        {
            var completion = p.completion;
            p.completion = null;
            p.cancelRegistration.Dispose();
            p.cancelRegistration = default;
            return completion;
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
            // Show t = 0 right away: e.g. a typewriter after a text change must not show the whole new line for a frame.
            playback.lastTime = -1f;           // an event at 0 fires on this first step
            Step(playback, 0f);

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
                    other.handle.TryCancel();
                    Evaluate(other.state, other.state.Duration);
                    Finish(other);
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
                if (IsText(track.property)) ((TMP_Text)track.target).ForceMeshUpdate();   // character count for duration/stagger
                if (UIAnimTextChars.IsCharProperty(track.property))
                {
                    // Character values are absolute per character (scale 1 = unchanged); there is no live/rest value per character.
                    foreach (var key in track.keys) key.resolved = key.value;
                    HoldThroughVibrations(track);
                    continue;
                }
                if (track.property == UIAnimProperty.TextReveal && track.scramble != ScrambleMode.None)
                    UIAnimTextChars.For((TMP_Text)track.target).CaptureSource();
                track.startValue = Read(track.target, track.property);
                foreach (var key in track.keys)
                    key.resolved = track.relativeToRest ? track.rest + key.value : key.value;
                // The first key becomes the live value, so the track starts where the property is. With 2+ keys it holds
                // until its time (= a delay); a lone key keeps its value and is reached through the implicit segment.
                if (track.startFromCurrent && (track.keys.Count > 1 || track.keys[0].time <= 0f)) track.keys[0].resolved = track.startValue;
                HoldThroughVibrations(track);
            }
        }

        // A punch/shake key's value is a strength, not a destination: after it the track is back where it started.
        static void HoldThroughVibrations(UIAnimTrack track)
        {
            for (var i = 1; i < track.keys.Count; i++)
                if (track.keys[i].IsVibration) track.keys[i].resolved = track.keys[i - 1].resolved;
        }

        /// <summary>Editor preview: resolves a state and every child state it calls.</summary>
        internal static void ResolveTree(UIAnimState state, int depth = 0)
        {
            Resolve(state);
            if (depth >= UIAnimState.MaxCallDepth) return;
            foreach (var call in state.childCalls)
                if (call.Target is { } child) ResolveTree(child, depth + 1);
        }

        /// <summary>Editor preview: the state at t, with each called child state at its own time since the call.</summary>
        internal static void EvaluateTree(UIAnimState state, float t, int depth = 0)
        {
            Evaluate(state, t);
            if (depth >= UIAnimState.MaxCallDepth) return;
            foreach (var call in state.childCalls)
                if (t >= call.time && call.Target is { } child) EvaluateTree(child, t - call.time, depth + 1);
        }

        /// <summary>Writes the state at time t. Shared by runtime playback, tests and the editor preview.</summary>
        internal static void Evaluate(UIAnimState state, float t)
        {
            foreach (var track in state.tracks)
            {
                if (track.released || track.target == null || track.keys.Count == 0) continue;
                if (UIAnimTextChars.IsCharProperty(track.property)) UIAnimTextChars.For((TMP_Text)track.target).Accumulate(track, t);
                else if (track.property == UIAnimProperty.TextReveal && track.scramble != ScrambleMode.None)
                    UIAnimTextChars.For((TMP_Text)track.target).Scramble(track.Sample(t).x, track.scramble, t);
                else Write(track.target, track.property, track.Sample(t));
            }
            // After every plain write (a reveal changes which characters exist), so character effects land on the final mesh.
            UIAnimTextChars.FlushAll();
        }

        static bool IsText(UIAnimProperty p) => p == UIAnimProperty.TextReveal || UIAnimTextChars.IsCharProperty(p);

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
            UIAnimProperty.TextReveal => new Vector4(RevealOf((TMP_Text)target), 0, 0, 0),
            UIAnimProperty.CharScale => Vector3.one,
            UIAnimProperty.CharOffset or UIAnimProperty.CharRotation => Vector4.zero,
            UIAnimProperty.CharColor => ((TMP_Text)target).color,
            UIAnimProperty.CharAlpha => new Vector4(1f, 0, 0, 0),
            UIAnimProperty.FillAmount => new Vector4(((Image)target).fillAmount, 0, 0, 0),
            UIAnimProperty.CharacterSpacing => new Vector4(((TMP_Text)target).characterSpacing, 0, 0, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(property)),
        };

        static float RevealOf(TMP_Text text)
        {
            var count = text.textInfo?.characterCount ?? 0;
            return count == 0 || text.maxVisibleCharacters >= count ? 1f : (float)text.maxVisibleCharacters / count;
        }

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
                case UIAnimProperty.FillAmount: ((Image)target).fillAmount = v.x; break;
                case UIAnimProperty.CharacterSpacing: ((TMP_Text)target).characterSpacing = v.x; break;
                case UIAnimProperty.TextReveal:
                    var text = (TMP_Text)target;
                    // 1 = everything (also covers text that grows later); below that, a share of the current characters.
                    text.maxVisibleCharacters = v.x >= 0.9999f ? 99999 : Mathf.RoundToInt(Mathf.Max(0f, v.x) * text.textInfo.characterCount);
                    break;
                case UIAnimProperty.CharScale:
                case UIAnimProperty.CharOffset:
                case UIAnimProperty.CharRotation:
                case UIAnimProperty.CharColor:
                case UIAnimProperty.CharAlpha:
                    break;   // per-character tracks are applied through UIAnimTextChars, not as a single value
                default: throw new ArgumentOutOfRangeException(nameof(property));
            }
        }
    }
}
