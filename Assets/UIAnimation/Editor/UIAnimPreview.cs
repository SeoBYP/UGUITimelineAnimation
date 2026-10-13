using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SeoBYP.TimelineAnimation.Editor
{
    /// <summary>
    /// Plays a UIAnimation state in edit mode without entering Play mode.
    /// Values are driven through AnimationMode, so Unity reverts them on Stop and never saves them;
    /// a snapshot restore and stop-before-save hooks back that up.
    /// </summary>
    internal sealed class UIAnimPreview : IDisposable
    {
        public UIAnimation Target { get; private set; }
        public UIAnimState State { get; private set; }
        public float Time { get; private set; }
        public bool IsActive => Target != null;
        public bool IsPlaying { get; private set; }
        public bool Loop { get; set; }
        public float Speed { get; set; } = 1f;

        public event Action Changed;

        readonly AnimationModeDriver _driver;
        readonly List<(UIAnimTrack track, Vector4 value)> _snapshot = new();
        readonly List<(TMP_Text text, string value)> _texts = new();
        readonly List<(UIAnimTrack track, Vector4 value)> _childRest = new();   // called child states: their rest before the call   // scramble rewrites text; restored on stop
        double _lastTick;
        bool _inPrefabStage;

        public UIAnimPreview()
        {
            _driver = ScriptableObject.CreateInstance<AnimationModeDriver>();
            _driver.hideFlags = HideFlags.HideAndDontSave;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneClosing += OnSceneClosing;
            PrefabStage.prefabSaving += OnPrefabSaving;
        }

        public void Dispose()
        {
            Stop();
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneClosing -= OnSceneClosing;
            PrefabStage.prefabSaving -= OnPrefabSaving;
            UnityEngine.Object.DestroyImmediate(_driver);
        }

        /// <summary>Starts (or restarts from the rest pose) previewing a state, showing time t.</summary>
        public void Begin(UIAnimation target, UIAnimState state, float t = 0f)
        {
            Stop();
            if (target == null || state == null || EditorApplication.isPlayingOrWillChangePlaymode) return;

            Target = target;
            State = state;
            Target.CaptureRest();

            _snapshot.Clear();
            _texts.Clear();
            _childRest.Clear();
            foreach (var s in target.states) SnapshotTracks(s, false);
            // Child UIAnimations called from this one: same snapshot/restore, and their rest is shown until their call.
            foreach (var child in CalledChildren(target))
            {
                child.CaptureRest();
                foreach (var s in child.states) SnapshotTracks(s, true);
            }

            // Prefab Mode only redraws values written inside a sampling block (as the Animation window samples), and
            // there EndSampling would revert registered properties; so nothing is registered and the snapshot restore
            // below (plus stop-before-save) is what keeps the asset untouched.
            _inPrefabStage = PrefabStageUtility.GetPrefabStage(target.gameObject) != null;
            AnimationMode.StartAnimationMode(_driver);
            if (!_inPrefabStage)
            {
                AnimationMode.BeginSampling();
                foreach (var (track, _) in _snapshot) Register(target, track);
                AnimationMode.EndSampling();
            }

            // Put the object where this state is normally played from (e.g. Off starts from the end of On).
            var from = PreviewFrom(target, state);
            if (from != null)
            {
                UIAnimation.Resolve(from);
                UIAnimation.Evaluate(from, from.Duration);
            }

            UIAnimation.ResolveTree(state);
            Seek(t);
        }

        void SnapshotTracks(UIAnimState state, bool child)
        {
            foreach (var track in state.tracks)
            {
                if (track.target == null) continue;
                var value = UIAnimation.Read(track.target, track.property);
                _snapshot.Add((track, value));
                if (child) _childRest.Add((track, value));
                if (track.target is TMP_Text text) _texts.Add((text, text.text));
            }
        }

        static List<UIAnimation> CalledChildren(UIAnimation root)
        {
            var found = new List<UIAnimation>();
            var pending = new Queue<UIAnimation>();
            pending.Enqueue(root);
            while (pending.Count > 0 && found.Count < 64)
            {
                foreach (var s in pending.Dequeue().states)
                foreach (var call in s.childCalls)
                    if (call.child != null && call.child != root && !found.Contains(call.child))
                    {
                        found.Add(call.child);
                        pending.Enqueue(call.child);
                    }
            }
            return found;
        }

        // Children start from their rest pose at every seek, so scrubbing back before a call shows them untouched.
        void EvaluateAt(float t)
        {
            foreach (var (track, value) in _childRest) UIAnimation.Write(track.target, track.property, value);
            UIAnimation.EvaluateTree(State, t);
        }

        // A state bound to the second half of an event pair (Exit, Up, Deselect, ToggleOff) is previewed from the end
        // of the state bound to the first half, so its motion is visible instead of starting at the rest pose.
        static readonly Dictionary<UIAnimEvent, UIAnimEvent> PairedWith = new()
        {
            [UIAnimEvent.PointerExit] = UIAnimEvent.PointerEnter,
            [UIAnimEvent.PointerUp] = UIAnimEvent.PointerDown,
            [UIAnimEvent.Deselect] = UIAnimEvent.Select,
            [UIAnimEvent.ToggleOff] = UIAnimEvent.ToggleOn,
        };

        // States played from code have no trigger; their names pair them instead: "Open" is previewed from the end
        // of "Close" (the popup is spawned closed), "Show" from "Hide", ...
        static readonly (string from, string state)[] NamePairs =
        {
            ("Close", "Open"), ("Open", "Close"), ("Hide", "Show"), ("Show", "Hide"), ("Out", "In"), ("In", "Out"),
        };

        static UIAnimState PreviewFrom(UIAnimation target, UIAnimState state)
        {
            if (target.TryGetComponent<UIAnimTrigger>(out var trigger))
                foreach (var b in trigger.bindings)
                {
                    if (b.state != state.name || !PairedWith.TryGetValue(b.evt, out var first)) continue;
                    foreach (var other in trigger.bindings)
                        if (other.evt == first && Find(target, state, other.state) is { } fromTrigger) return fromTrigger;
                }
            foreach (var (from, name) in NamePairs)
                if (string.Equals(state.name, name, StringComparison.OrdinalIgnoreCase) && Find(target, state, from) is { } fromName)
                    return fromName;
            return null;
        }

        static UIAnimState Find(UIAnimation target, UIAnimState except, string name)
        {
            foreach (var s in target.states)
                if (s != except && string.Equals(s.name, name, StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }

        public void Seek(float t)
        {
            if (!IsActive) return;
            Time = Mathf.Clamp(t, 0f, State.Duration);
            // Scenes: written outside Begin/EndSampling on purpose, since EndSampling restores registered properties to
            // their recorded originals; registration alone keeps them revert-on-stop and unsaved.
            // Prefab Mode: nothing is registered, and only writes inside a sampling block get redrawn.
            if (_inPrefabStage)
            {
                AnimationMode.BeginSampling();
                EvaluateAt(Time);
                AnimationMode.EndSampling();
            }
            else EvaluateAt(Time);
            Repaint();
            Changed?.Invoke();
        }

        public void Play()
        {
            if (!IsActive) return;
            if (Time >= State.Duration) Seek(0f);
            IsPlaying = true;
            _lastTick = EditorApplication.timeSinceStartup;
            Changed?.Invoke();
        }

        public void Pause()
        {
            IsPlaying = false;
            Changed?.Invoke();
        }

        /// <summary>Re-applies the state at the current time, e.g. after a clip was edited.</summary>
        public void Refresh()
        {
            if (!IsActive) return;
            var (target, state, t, playing) = (Target, State, Time, IsPlaying);
            Begin(target, state, t);
            IsPlaying = playing;
        }

        public void Stop()
        {
            if (!IsActive) return;
            IsPlaying = false;
            if (AnimationMode.InAnimationMode(_driver)) AnimationMode.StopAnimationMode(_driver);

            // Fallback: AnimationMode has already reverted registered properties; this covers anything it missed.
            foreach (var (track, value) in _snapshot)
                if (track.target != null) UIAnimation.Write(track.target, track.property, value);
            // Scrambled text is restored first; character effects live only in the generated mesh, so regenerate it.
            foreach (var (text, value) in _texts)
                if (text != null && text.text != value) text.text = value;
            foreach (var (track, _) in _snapshot)
                if (track.target is TMP_Text text) text.ForceMeshUpdate();
            _snapshot.Clear();
            _texts.Clear();
            _childRest.Clear();

            Target = null;
            State = null;
            Time = 0f;
            Repaint();
            Changed?.Invoke();
        }

        void Tick()
        {
            if (!IsPlaying || !IsActive) return;
            if (Target == null) { Stop(); return; }   // destroyed while previewing

            var now = EditorApplication.timeSinceStartup;
            var t = Time + (float)(now - _lastTick) * Speed;
            _lastTick = now;

            var duration = State.Duration;
            if (t >= duration)
            {
                if (Loop && duration > 0f)
                {
                    Begin(Target, State, t % duration);   // restart from the rest pose so Current clips loop cleanly
                    IsPlaying = true;
                    _lastTick = now;
                    return;
                }
                t = duration;
                IsPlaying = false;
            }
            Seek(t);
        }

        void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode) Stop();
        }

        void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path) => Stop();
        // Revert/reopen replaces the previewed objects; stop first so the restore lands on the old ones, not the new scene.
        void OnSceneClosing(UnityEngine.SceneManagement.Scene scene, bool removing) => Stop();
        void OnPrefabSaving(GameObject prefab) => Stop();

        static void Repaint()
        {
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();   // refreshes the Game view and canvases
        }

        // Tells AnimationMode which serialized fields the preview drives, with their original values.
        void Register(UIAnimation root, UIAnimTrack track)
        {
            var target = track.target;
            switch (track.property)
            {
                case UIAnimProperty.Scale:
                    Add(root, target.transform, "m_LocalScale", "x", "y", "z");
                    break;
                case UIAnimProperty.SizeDelta:
                    Add(root, target.transform, "m_SizeDelta", "x", "y");
                    break;
                case UIAnimProperty.AnchoredPosition:
                    Add(root, target.transform, "m_AnchoredPosition", "x", "y");
                    break;
                case UIAnimProperty.GraphicColor:
                case UIAnimProperty.GraphicAlpha:
                    if (target is TMP_Text)
                    {
                        Add(root, target, "m_fontColor", "r", "g", "b", "a");
                        Add(root, target, "m_fontColor32", "rgba");
                    }
                    else if (target is Graphic)
                        Add(root, target, "m_Color", "r", "g", "b", "a");
                    break;
                case UIAnimProperty.TextReveal:
                    Add(root, target, "m_maxVisibleCharacters");
                    Add(root, target, "m_text");   // scramble rewrites the text itself
                    break;
                case UIAnimProperty.FontSize:
                    Add(root, target, "m_fontSize");
                    break;
                case UIAnimProperty.FillAmount:
                    Add(root, target, "m_FillAmount");
                    break;
                case UIAnimProperty.CharacterSpacing:
                    Add(root, target, "m_characterSpacing");
                    break;
            }
        }

        static void Add(UIAnimation root, UnityEngine.Object target, string field, params string[] components)
        {
            var so = new SerializedObject(target);
            var path = AnimationUtility.CalculateTransformPath(((Component)target).transform, root.transform);
            if (components.Length == 0) AddOne(so, target, path, field);
            foreach (var c in components) AddOne(so, target, path, field + "." + c);
        }

        static void AddOne(SerializedObject so, UnityEngine.Object target, string path, string propertyPath)
        {
            var prop = so.FindProperty(propertyPath);
            if (prop == null) return;
            var value = prop.propertyType switch
            {
                SerializedPropertyType.Float => prop.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                SerializedPropertyType.Integer => prop.longValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                SerializedPropertyType.String => prop.stringValue,
                _ => null,
            };
            if (value == null) return;

            var binding = EditorCurveBinding.FloatCurve(path, target.GetType(), propertyPath);
            var modification = new PropertyModification { target = target, propertyPath = propertyPath, value = value };
            AnimationMode.AddPropertyModification(binding, modification, true);
        }
    }
}
