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
        double _lastTick;

        public UIAnimPreview()
        {
            _driver = ScriptableObject.CreateInstance<AnimationModeDriver>();
            _driver.hideFlags = HideFlags.HideAndDontSave;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            PrefabStage.prefabSaving += OnPrefabSaving;
        }

        public void Dispose()
        {
            Stop();
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
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
            foreach (var s in target.states)
            foreach (var track in s.tracks)
                if (track.target != null)
                    _snapshot.Add((track, UIAnimation.Read(track.target, track.property)));

            AnimationMode.StartAnimationMode(_driver);
            AnimationMode.BeginSampling();
            foreach (var (track, _) in _snapshot) Register(target, track);
            AnimationMode.EndSampling();

            // Put the object where this state is normally played from (e.g. Off starts from the end of On).
            var from = PreviewFrom(target, state);
            if (from != null)
            {
                UIAnimation.Resolve(from);
                UIAnimation.Evaluate(from, from.Duration);
            }

            UIAnimation.Resolve(state);
            Seek(t);
        }

        static UIAnimState PreviewFrom(UIAnimation target, UIAnimState state)
        {
            if (string.IsNullOrEmpty(state.previewFrom)) return null;
            foreach (var s in target.states)
                if (s != state && s.name == state.previewFrom) return s;
            return null;
        }

        public void Seek(float t)
        {
            if (!IsActive) return;
            Time = Mathf.Clamp(t, 0f, State.Duration);
            // Written outside Begin/EndSampling on purpose: EndSampling restores registered properties to their
            // recorded originals, which would undo a plain C# write. Registration alone keeps them revert-on-stop and unsaved.
            UIAnimation.Evaluate(State, Time);
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
            _snapshot.Clear();

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
                case UIAnimProperty.FontSize:
                    Add(root, target, "m_fontSize");
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
                _ => null,
            };
            if (value == null) return;

            var binding = EditorCurveBinding.FloatCurve(path, target.GetType(), propertyPath);
            var modification = new PropertyModification { target = target, propertyPath = propertyPath, value = value };
            AnimationMode.AddPropertyModification(binding, modification, true);
        }
    }
}
