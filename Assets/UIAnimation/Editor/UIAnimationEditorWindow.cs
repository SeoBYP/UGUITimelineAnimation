using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Graphic = UnityEngine.UI.Graphic;

namespace SeoBYP.TimelineAnimation.Editor
{
    public sealed class UIAnimationEditorWindow : EditorWindow
    {
        [MenuItem("Window/UI Animation Editor")]
        public static void Open() => GetWindow<UIAnimationEditorWindow>("UI Animation");

        public static void Open(UIAnimation target)
        {
            var window = GetWindow<UIAnimationEditorWindow>("UI Animation");
            window.SetTarget(target);
        }

        /// <summary>Opens the editor on one state of <paramref name="target"/>; with <paramref name="create"/>, adds it when missing.</summary>
        public static void Open(UIAnimation target, string state, bool create = false)
        {
            var window = GetWindow<UIAnimationEditorWindow>("UI Animation");
            window.SetTarget(target);
            window.SelectState(state, create);
        }

        /// <summary>
        /// Opens the editor on a state of <paramref name="owner"/>'s UIAnimation (in Edit or Play mode). A prefab asset is opened in
        /// prefab mode first, so edits are saved to the prefab. With <paramref name="create"/>, a missing UIAnimation / state is added.
        /// </summary>
        public static UIAnimation OpenState(Component owner, string state, bool create = false)
        {
            var go = owner.gameObject;
            if (EditorUtility.IsPersistent(go))
            {
                var stage = UnityEditor.SceneManagement.PrefabStageUtility.OpenPrefab(AssetDatabase.GetAssetPath(go));
                var path = AnimationUtility.CalculateTransformPath(go.transform, go.transform.root);
                go = (string.IsNullOrEmpty(path) ? stage.prefabContentsRoot.transform : stage.prefabContentsRoot.transform.Find(path)).gameObject;
            }
            var anim = go.GetComponent<UIAnimation>();
            if (anim == null)
            {
                if (!create) return null;
                anim = Undo.AddComponent<UIAnimation>(go);
            }
            Selection.activeGameObject = go;
            Open(anim, state, create);
            return anim;
        }

        internal void SelectState(string name, bool create = false)
        {
            if (_target == null || string.IsNullOrEmpty(name)) return;
            var index = _target.states.FindIndex(s => s.name == name);
            if (index < 0 && create)
            {
                Modify("Add State", () => _target.states.Add(new UIAnimState { name = name }), rebuild: false);
                index = _target.states.Count - 1;
            }
            if (index < 0) return;
            _state = index;
            _selTrack = null;
            _selection.Clear();
            Rebuild();
        }

        UIAnimation _target;
        int _state;
        object _selTrack;                // selected track header: UIAnimTrack, UIAnimEventTrack or UIAnimSoundTrack
        readonly HashSet<UIAnimKey> _selection = new();
        readonly Dictionary<UIAnimKey, float> _dragStartTimes = new();
        int _dragUndoGroup = -1;
        object _marker;                  // selected animation event or child call (exclusive with key/track selection)
        int _markerUndoGroup = -1;
        UIAnimPreview _preview;
        readonly HashSet<UIAnimKey> _unlinkedScale = new();   // keys whose Scale is edited per axis

        VisualElement _tabs, _body, _inspector;
        Label _targetLabel, _timeLabel, _status;
        Button _play, _loop;
        UIAnimTimelineView _timeline;

        // Tests drive the window through the same entry points as its UI.
        internal UIAnimTimelineView Timeline => _timeline;
        internal VisualElement Inspector => _inspector;

        UIAnimState CurrentState => _target != null && _state < _target.states.Count ? _target.states[_state] : null;
        UIAnimTrack CurrentTrack => _selTrack as UIAnimTrack;

        // Keyframe tracks editable on this timeline: the state's own, plus those of called child states overridden in this prefab
        // (the override lives on _target, so undo and saving go there and the child's prefab never changes).
        IEnumerable<UIAnimTrack> EditableTracks()
        {
            var state = CurrentState;
            if (state == null) yield break;
            foreach (var track in state.tracks) yield return track;
            foreach (var call in state.childCalls)
            foreach (var track in call.Target?.tracks ?? new List<UIAnimTrack>())
                if (UIAnimTimelineView.LayerOf(call, track).o != null) yield return track;
        }

        void OnEnable()
        {
            _preview = new UIAnimPreview();
            _preview.Changed += OnPreviewChanged;
            Selection.selectionChanged += OnSelectionChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            _preview.Dispose();
        }

        void CreateGUI()
        {
            var root = rootVisualElement;
            root.AddToClassList("uia-root");
            if (!EditorGUIUtility.isProSkin) root.AddToClassList("uia-light");
            var sheet = LoadStyleSheet();
            if (sheet != null) root.styleSheets.Add(sheet);

            var topbar = new VisualElement();
            topbar.AddToClassList("uia-topbar");
            _targetLabel = new Label();
            _targetLabel.AddToClassList("uia-target");
            topbar.Add(_targetLabel);
            _play = IconButton("▶", "Play / Pause (preview without Play Mode)", TogglePlay, accent: true);
            topbar.Add(_play);
            topbar.Add(IconButton("■", "Stop and restore the original values", () => _preview.Stop()));
            _loop = IconButton("⟲", "Loop", () =>
            {
                _preview.Loop = !_preview.Loop;
                _loop.EnableInClassList("uia-iconbtn--accent", _preview.Loop);
            });
            topbar.Add(_loop);
            var speed = new ToolbarMenu { text = "1x", style = { marginLeft = 6 } };
            foreach (var s in new[] { 0.1f, 0.25f, 0.5f, 1f })
                speed.menu.AppendAction($"{s}x", _ => { _preview.Speed = s; speed.text = $"{s}x"; });
            topbar.Add(speed);
            _timeLabel = new Label();
            _timeLabel.AddToClassList("uia-time");
            topbar.Add(_timeLabel);
            topbar.Add(new VisualElement { style = { flexGrow = 1 } });
            _status = new Label();
            _status.AddToClassList("uia-status");
            topbar.Add(_status);
            root.Add(topbar);

            _tabs = new VisualElement();
            _tabs.AddToClassList("uia-tabs");
            root.Add(_tabs);

            _body = new VisualElement { style = { flexGrow = 1 } };
            root.Add(_body);

            var split = new TwoPaneSplitView(1, 330, TwoPaneSplitViewOrientation.Horizontal);
            _timeline = new UIAnimTimelineView();
            WireTimeline();
            _inspector = new ScrollView();
            _inspector.AddToClassList("uia-inspector");
            split.Add(_timeline);
            split.Add(_inspector);
            _body.Add(split);

            if (_target == null) OnSelectionChanged();
            Rebuild();
        }

        StyleSheet LoadStyleSheet()
        {
            // Next to this script, wherever the package is installed (Assets/ or Packages/).
            var dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)));
            return AssetDatabase.LoadAssetAtPath<StyleSheet>(Path.Combine(dir ?? "", "UIAnimationEditor.uss").Replace('\\', '/'));
        }

        static Button IconButton(string text, string tooltip, Action onClick, bool accent = false)
        {
            var b = new Button(onClick) { text = text, tooltip = tooltip };
            b.AddToClassList("uia-iconbtn");
            if (accent) b.AddToClassList("uia-iconbtn--accent");
            return b;
        }

        internal void SetTarget(UIAnimation target)
        {
            if (target == _target) return;
            _preview.Stop();
            _target = target;
            _state = 0;
            _selTrack = null;
            _selection.Clear();
            Rebuild();
        }

        void OnSelectionChanged()
        {
            var go = Selection.activeGameObject;
            var found = go != null ? go.GetComponentInParent<UIAnimation>(true) : null;
            if (found != null) SetTarget(found);
            else if (_target == null) Rebuild();   // refresh the "add UIAnimation" hint
        }

        void OnUndoRedo()
        {
            _selection.Clear();   // undo deserializes new key objects; old references are stale
            _marker = null;
            _selTrack = null;
            Rebuild();
            RefreshPreview();
        }

        // ---------- data edits (all undoable, recorded as prefab overrides on instances) ----------

        void Modify(string undoName, Action edit, bool rebuild = true)
        {
            // An override of a called child may live on this animation or one above it (nested prefabs), so record them all.
            var owners = _target.GetComponentsInParent<UIAnimation>(true);
            Undo.RecordObjects(owners, undoName);
            edit();
            foreach (var owner in owners)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
                EditorUtility.SetDirty(owner);
            }
            if (rebuild) Rebuild();
            else _timeline.Refresh(_selTrack);
            RefreshPreview();
        }

        void RefreshPreview()
        {
            if (!_preview.IsActive || CurrentState == null) return;
            var (t, playing) = (_preview.Time, _preview.IsPlaying);
            _preview.Begin(_target, CurrentState, t);   // re-resolve against the rest pose with the edited data
            if (playing) _preview.Play();
        }

        void SelectionChanged()
        {
            _marker = null;
            _timeline.SelectedMarker = null;
            // A single selected key also selects its track, so the key inspector knows where it lives.
            if (_selection.Count > 0)
            {
                var first = _selection.First();
                _selTrack = EditableTracks().FirstOrDefault(t => t.keys.Contains(first));
            }
            _timeline.Refresh(_selTrack);
            RebuildInspector();
        }

        void WireTimeline()
        {
            _timeline.Scrubbed = t =>
            {
                if (_preview.IsActive && _preview.State == CurrentState) _preview.Seek(t);
                else _preview.Begin(_target, CurrentState, t);
            };
            _timeline.TrackSelected = track =>
            {
                _selection.Clear();
                _selTrack = track;
                SelectionChanged();
            };
            _timeline.KeyClicked = (key, additive) =>
            {
                if (!additive) _selection.Clear();
                if (additive && _selection.Contains(key)) _selection.Remove(key);
                else _selection.Add(key);
                SelectionChanged();
            };
            _timeline.BoxSelected = (keys, additive) =>
            {
                if (!additive) _selection.Clear();
                _selection.UnionWith(keys);
                SelectionChanged();
            };
            _timeline.SelectionCleared = () =>
            {
                _selection.Clear();
                _selTrack = null;
                SelectionChanged();
            };
            _timeline.SelectedKeysDragStarted = () =>
            {
                _dragStartTimes.Clear();
                foreach (var k in _selection) _dragStartTimes[k] = k.time;
                _dragUndoGroup = Undo.GetCurrentGroup();
            };
            _timeline.SelectedKeysMoved = delta =>
            {
                // Clamp so the earliest selected key never goes below 0.
                var minStart = _dragStartTimes.Values.DefaultIfEmpty(0f).Min();
                delta = Mathf.Max(delta, -minStart);
                Modify("Move Keys", () =>
                {
                    foreach (var (k, start) in _dragStartTimes) k.time = Mathf.Round((start + delta) * 100f) / 100f;
                }, rebuild: false);
            };
            _timeline.DragFinished = () =>
            {
                // Keys may have passed their neighbours; restore time order inside each track.
                Modify("Move Keys", () =>
                {
                    foreach (var t in EditableTracks()) t.keys.Sort((a, b) => a.time.CompareTo(b.time));
                });
                if (_dragUndoGroup >= 0) Undo.CollapseUndoOperations(_dragUndoGroup);
                _dragUndoGroup = -1;
            };
            _timeline.DeleteRequested = DeleteSelectedKeys;
            _timeline.AddKeyRequested = (track, time) => Modify("Add Key", () =>
            {
                EnsureStartKey(track);
                if (track.keys.Exists(k => Mathf.Approximately(k.time, time))) return;   // already a key there
                var key = new UIAnimKey { time = time, value = RawValueAt(track, time) };
                var index = track.keys.FindIndex(k => k.time > time);
                track.keys.Insert(index < 0 ? track.keys.Count : index, key);
                _selection.Clear();
                _selection.Add(key);
                _selTrack = track;
            });
            _timeline.RemoveRequested = RemoveItem;
            _timeline.StaggerDragged = (track, stagger) =>
            {
                if (_markerUndoGroup < 0) _markerUndoGroup = Undo.GetCurrentGroup();
                Modify("Edit Stagger", () => track.stagger = stagger, rebuild: false);
                OnPreviewChanged();   // the state's length follows
            };
            _timeline.StaggerDragFinished = () =>
            {
                if (_markerUndoGroup >= 0) Undo.CollapseUndoOperations(_markerUndoGroup);
                _markerUndoGroup = -1;
                Rebuild();
            };
            _timeline.AddTrackMenuRequested = rect => ShowPropertyMenu(rect, null);

            _timeline.MarkerClicked = marker =>
            {
                _selection.Clear();
                _selTrack = null;
                _marker = marker;
                _timeline.SelectedMarker = marker;
                _timeline.Refresh(_selTrack);
                RebuildInspector();
            };
            _timeline.MarkerMoved = (marker, time) =>
            {
                if (_markerUndoGroup < 0) _markerUndoGroup = Undo.GetCurrentGroup();
                Modify("Move Marker", () => SetMarkerTime(marker, time), rebuild: false);
            };
            _timeline.MarkerDragFinished = () =>
            {
                Modify("Move Marker", SortMarkers);
                if (_markerUndoGroup >= 0) Undo.CollapseUndoOperations(_markerUndoGroup);
                _markerUndoGroup = -1;
            };
            _timeline.AddEventRequested = ShowAddEventMenu;
            _timeline.OverrideRequested = OverrideTrack;
            _timeline.RevertOverrideRequested = RevertOverride;
            _timeline.AddSoundRequested = (track, time, clip) => Modify("Add Sound", () =>
            {
                var sound = new UIAnimSound { time = time, clip = clip };
                track.sounds.Add(sound);
                SelectNewMarker(sound);
            });
            _timeline.AddCallRequested = ShowChildCallMenu;
            _timeline.PopulateChildCallMenu = (menu, time) =>
            {
                var choices = ChildCallChoices().ToList();
                foreach (var (path, child, state) in choices)
                {
                    var called = IsCalled(child, state);   // already played by this state: shown checked, not added twice
                    menu.Add($"Add Child Call/{path}/{state}", () => AddCall(child, state, time), enabled: !called, isChecked: called);
                }
                if (choices.Count == 0) menu.Add("Add Child Call", null);
            };
        }

        // ---------- animation events: plain named ones, or a registered [UIAnimEvent] struct with parameters ----------

        // Only types that exist in a player build: an editor-only struct (e.g. in tests) could never be received at runtime.
        static List<Type> EventTypes()
        {
            var player = new HashSet<string>(UnityEditor.Compilation.CompilationPipeline
                .GetAssemblies(UnityEditor.Compilation.AssembliesType.PlayerWithoutTestAssemblies).Select(a => a.name));
            return TypeCache.GetTypesWithAttribute<UIAnimEventAttribute>()
                .Where(t => t.IsValueType && !t.IsGenericType && player.Contains(t.Assembly.GetName().Name))
                .OrderBy(EventDisplayName).ToList();
        }

        static string EventDisplayName(Type type)
        {
            var attribute = (UIAnimEventAttribute)Attribute.GetCustomAttribute(type, typeof(UIAnimEventAttribute));
            return string.IsNullOrEmpty(attribute?.DisplayName) ? type.Name : attribute.DisplayName;
        }

        void ShowAddEventMenu(UIAnimEventTrack track, Rect at, float time)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Name Only"), false, () => AddEvent(track, time, null));
            menu.AddSeparator("");
            var types = EventTypes();
            foreach (var type in types)
            {
                var t = type;
                menu.AddItem(new GUIContent($"{EventDisplayName(t)}  ({t.Name})"), false, () => AddEvent(track, time, t));
            }
            if (types.Count == 0) menu.AddDisabledItem(new GUIContent("Structs marked [UIAnimEvent] appear here"));
            menu.DropDown(at);
        }

        void AddEvent(UIAnimEventTrack track, float time, Type dataType) => Modify("Add Animation Event", () =>
        {
            var baseName = dataType != null ? EventDisplayName(dataType) : "Event";
            var name = baseName;
            for (var n = 1; CurrentState.eventTracks.Any(t => t.events.Exists(x => x.name == name)); n++) name = $"{baseName} {n}";
            var marker = new UIAnimEventMarker
            {
                time = time, name = name,
                dataType = UIAnimEventMarker.TypeKey(dataType),
                dataJson = dataType != null ? JsonUtility.ToJson(Activator.CreateInstance(dataType)) : "",
            };
            track.events.Add(marker);
            SelectNewMarker(marker);
        });

        static void SetMarkerTime(object marker, float time)
        {
            time = Mathf.Max(0f, time);
            if (marker is UIAnimEventMarker e) e.time = time;
            else if (marker is UIAnimChildCall call) call.time = time;
            else if (marker is UIAnimSound sound) sound.time = time;
        }

        void SortMarkers()
        {
            foreach (var track in CurrentState.eventTracks) track.events.Sort((a, b) => a.time.CompareTo(b.time));
            foreach (var track in CurrentState.soundTracks) track.sounds.Sort((a, b) => a.time.CompareTo(b.time));
            CurrentState.childCalls.Sort((a, b) => a.time.CompareTo(b.time));
        }

        bool HasMarker(object marker) => marker switch
        {
            UIAnimEventMarker e => CurrentState.eventTracks.Any(t => t.events.Contains(e)),
            UIAnimSound sound => CurrentState.soundTracks.Any(t => t.sounds.Contains(sound)),
            UIAnimChildCall call => CurrentState.childCalls.Contains(call),
            _ => false,
        };

        void SelectNewMarker(object marker)
        {
            SortMarkers();
            _selection.Clear();
            _selTrack = null;
            _marker = marker;
        }

        // Child UIAnimations under this one with their states (path joined with › so menus don't split it into submenus).
        IEnumerable<(string path, UIAnimation child, string state)> ChildCallChoices()
        {
            foreach (var child in _target.GetComponentsInChildren<UIAnimation>(true))
            {
                if (child == _target) continue;
                var path = UIAnimTimelineView.TargetPath(_target, child).Replace("/", " › ");
                foreach (var state in child.states) yield return (path, child, state.name);
            }
        }

        bool IsCalled(UIAnimation child, string state) => CurrentState.childCalls.Exists(c => c.child == child && c.state == state);

        internal void AddCall(UIAnimation child, string state, float time) => Modify("Add Child Call", () =>
        {
            var call = new UIAnimChildCall { time = time, child = child, state = state };
            CurrentState.childCalls.Add(call);
            SelectNewMarker(call);
        });

        // Pick a child state to play at `time`.
        void ShowChildCallMenu(Rect at, float time)
        {
            var menu = new GenericMenu();
            var choices = ChildCallChoices().ToList();
            foreach (var (path, child, state) in choices)
            {
                if (IsCalled(child, state)) menu.AddDisabledItem(new GUIContent($"{path}/{state}"), true);
                else menu.AddItem(new GUIContent($"{path}/{state}"), false, () => AddCall(child, state, time));
            }
            if (choices.Count == 0) menu.AddDisabledItem(new GUIContent("No child UIAnimation with states under this object"));
            menu.DropDown(at);
        }

        void DeleteSelectedKeys()
        {
            if (_marker != null)
            {
                var marker = _marker;
                Modify("Delete Marker", () =>
                {
                    foreach (var t in CurrentState.eventTracks) t.events.Remove(marker as UIAnimEventMarker);
                    foreach (var t in CurrentState.soundTracks) t.sounds.Remove(marker as UIAnimSound);
                    CurrentState.childCalls.Remove(marker as UIAnimChildCall);
                    _marker = null;
                });
                return;
            }
            if (_selection.Count == 0) return;
            Modify("Delete Keys", () =>
            {
                foreach (var t in EditableTracks()) t.keys.RemoveAll(_selection.Contains);
                _selection.Clear();
            });
        }

        // ---------- property picker: the animated object's hierarchy, like the Animation window's "Add Property" ----------

        /// <summary>Opens the track picker. <paramref name="retarget"/> changes that track instead of adding any.</summary>
        void ShowPropertyMenu(Rect at, UIAnimTrack retarget)
        {
            var used = new HashSet<(Component, UIAnimProperty)>(CurrentState.tracks.Select(t => (t.target, t.property)));
            (Component, UIAnimProperty)? current = retarget != null ? (retarget.target, retarget.property) : null;
            UIAnimPropertyPicker.Show(this, at, _target, used, current, picks =>
            {
                if (retarget == null) AddTracks(picks);
                else Retarget(retarget, picks[0].component, picks[0].property);
            });
        }

        internal void AddTracks(List<(UIAnimPickKind kind, Component component, UIAnimProperty property)> picks) => Modify("Add Track", () =>
        {
            var state = CurrentState;
            var time = state.Duration > 0f ? state.Duration : 0.2f;
            _selection.Clear();
            foreach (var (kind, component, property) in picks)
            {
                switch (kind)
                {
                    case UIAnimPickKind.Event:
                        var events = new UIAnimEventTrack();
                        state.eventTracks.Add(events);
                        _selTrack = events;
                        break;
                    case UIAnimPickKind.Sound:
                        var sounds = new UIAnimSoundTrack { source = component as AudioSource };
                        state.soundTracks.Add(sounds);
                        _selTrack = sounds;
                        break;
                    default:
                        var key = new UIAnimKey { time = time, value = UIAnimation.Read(component, property) };
                        // Text tracks animate absolute values (reveal 0→1, per-character effects), so they never start from the live value.
                        var track = new UIAnimTrack
                        {
                            target = component, property = property, startFromCurrent = !IsTextProperty(property),
                            stagger = UIAnimTextChars.IsCharProperty(property) ? 0.05f : 0f,
                            keys = { new UIAnimKey { time = 0f, value = key.value }, key },   // a key at 0, like the Animation window
                        };
                        state.tracks.Add(track);
                        _selTrack = track;
                        if (picks.Count == 1) _selection.Add(key);
                        break;
                }
            }
        });

        // Removes a track (own, or of an overridden child state), a child call, an animation event or a sound.
        void RemoveItem(object track) => Modify("Remove", () =>
        {
            var state = CurrentState;
            switch (track)
            {
                case UIAnimChildCall call: state.childCalls.Remove(call); break;
                case UIAnimEventMarker e: foreach (var t in state.eventTracks) t.events.Remove(e); break;
                case UIAnimSound sound: foreach (var t in state.soundTracks) t.sounds.Remove(sound); break;
                case UIAnimTrack keys:
                    _selection.ExceptWith(keys.keys);
                    // An overridden child track: removing it from the override = back to the original track.
                    // Like Unity: only the outermost layer goes, so a prefab's own override below it shows again.
                    if (!state.tracks.Remove(keys))
                        foreach (var call in state.childCalls)
                            if (UIAnimTimelineView.LayerOf(call, keys) is { o: { } o } layer)
                            {
                                o.tracks.Remove(keys);
                                if (o.tracks.Count == 0) layer.owner.overrides.Remove(o);
                                break;
                            }
                    break;
                case UIAnimEventTrack events: state.eventTracks.Remove(events); break;
                case UIAnimSoundTrack sounds: state.soundTracks.Remove(sounds); break;
            }
            if (_selTrack == track) _selTrack = null;
            if (_marker == track) _marker = null;
        });

        void Retarget(UIAnimTrack track, Component target, UIAnimProperty property) => Modify("Change Track Target", () =>
        {
            track.target = target;
            track.property = property;
        });

        // Every edited track gets a key at 0 (the Animation window's habit), so a key added before the old first key
        // never silently becomes the start. The playback stays the same:
        // - start from current, one key: the 0 key is the live value, so it's still "current → key".
        // - start from current, a delayed first key: that key held the live value; it now holds the value read right now.
        // - absolute values: the 0 key copies the first key, which the track held anyway before it.
        public static void EnsureStartKey(UIAnimTrack track)
        {
            if (track.keys.Count == 0 || track.keys[0].time <= 0f) return;
            var first = track.keys[0];
            if (first.IsVibration && !track.startFromCurrent) return;   // a strength, not a value to start from
            var start = new UIAnimKey { time = 0f, value = first.value };
            if (track.startFromCurrent && track.target != null)
            {
                var current = IsOffset(track) ? Vector4.zero : UIAnimation.Read(track.target, track.property);
                start.value = current;
                if (track.keys.Count > 1) first.value = current;
            }
            track.keys.Insert(0, start);
        }

        // Key values added to a base instead of absolute: the rest value, or the press point of a Pointer track.
        static bool IsOffset(UIAnimTrack track) => track.relativeToRest || track.start == UIAnimStart.Pointer;

        static bool IsTextProperty(UIAnimProperty p) => p == UIAnimProperty.TextReveal || UIAnimTextChars.IsCharProperty(p);

        // The value a new key should get: what the track's keys say at that time, or the live value for an empty track.
        static Vector4 RawValueAt(UIAnimTrack track, float time)
        {
            var keys = track.keys;
            if (keys.Count == 0)
                return IsOffset(track) || track.target == null ? Vector4.zero : UIAnimation.Read(track.target, track.property);
            if (time <= keys[0].time) return keys[0].value;
            for (var i = 1; i < keys.Count; i++)
            {
                if (time >= keys[i].time) continue;
                var p = (time - keys[i - 1].time) / Mathf.Max(0.0001f, keys[i].time - keys[i - 1].time);
                return Vector4.LerpUnclamped(keys[i - 1].value, keys[i].value, keys[i].Weight(p));
            }
            return keys[keys.Count - 1].value;
        }

        // ---------- view ----------

        void Rebuild()
        {
            if (_tabs == null) return;   // before CreateGUI

            _tabs.Clear();
            var hasTarget = _target != null;
            _body.SetEnabled(hasTarget);
            _targetLabel.text = hasTarget ? _target.name : "No UIAnimation";

            if (!hasTarget)
            {
                _timeline.SetState(null, null, null, _selection);
                _inspector.Clear();
                var section = Section("Getting Started");
                section.Add(Hint("Select an object with a UIAnimation component."));
                var go = Selection.activeGameObject;
                if (go != null && go.transform is RectTransform)
                    section.Add(new Button(() => SetTarget(Undo.AddComponent<UIAnimation>(go))) { text = $"Add UIAnimation to '{go.name}'" });
                _inspector.Add(section);
                OnPreviewChanged();
                return;
            }

            BuildStateTabs();

            if (_marker != null && (CurrentState == null || !HasMarker(_marker))) _marker = null;   // state switched or marker removed
            _timeline.SelectedMarker = _marker;
            _timeline.SetState(_target, CurrentState, _selTrack, _selection);
            RebuildInspector();
        }

        void BuildStateTabs()
        {
            _state = Mathf.Clamp(_state, 0, Mathf.Max(0, _target.states.Count - 1));
            for (var i = 0; i < _target.states.Count; i++)
            {
                var index = i;
                var tab = new Button(() =>
                {
                    if (index == _state) return;
                    _preview.Stop();
                    _state = index;
                    _selTrack = null;
                    _selection.Clear();
                    Rebuild();
                }) { text = _target.states[i].name, tooltip = $"Play On: {PlayOn(_target.states[i].name)?.evt.ToString() ?? "None (your code)"}" };
                tab.AddToClassList("uia-tab");
                tab.EnableInClassList("uia-tab--selected", i == _state);
                _tabs.Add(tab);
            }
            var add = new Button(AddState) { text = "+", tooltip = "Add state" };
            add.AddToClassList("uia-tab");
            add.AddToClassList("uia-tab--add");
            _tabs.Add(add);
        }

        // ---------- current-prefab overrides of called child states ----------

        // Copies one track of the child's state into this prefab so it becomes editable here; the others keep following the
        // child, and the child's prefab stays as is.
        void OverrideTrack(UIAnimChildCall call, UIAnimTrack track)
        {
            if (call.child == null || track == null || UIAnimTimelineView.LayerOf(call, track).o != null) return;   // already overridden
            _preview.Stop();
            Modify("Override Child Track", () =>
            {
                var o = UIAnimTimelineView.OverrideOf(_target, call);
                if (o == null) _target.overrides.Add(o = new UIAnimOverride { child = call.child, state = call.state });
                var copy = o.Find(track);
                if (copy == null) o.tracks.Add(copy = track.Clone());
                _selection.Clear();
                _selTrack = copy;
            });
        }

        void RevertOverride(UIAnimChildCall call)
        {
            if (EditorUtility.DisplayDialog("Revert to Original",
                    $"Revert the outermost overrides of '{call.state}' on {call.child.name}?", "Revert", "Cancel"))
                RemoveOverride(call);
        }

        // The ↺ button after its confirmation (tests call this directly).
        internal void RemoveOverride(UIAnimChildCall call)
        {
            _preview.Stop();
            Modify("Revert Child Override", () =>
            {
                // Like Unity's Revert All: only the outermost layer that overrides this state goes; an inner prefab's own
                // overrides (part of that prefab) show again.
                var owner = call.child.GetComponentsInParent<UIAnimation>(true)
                    .LastOrDefault(a => a != call.child && a.overrides.Exists(o => o.child == call.child && o.state == call.state));
                if (owner == null) return;
                foreach (var o in owner.overrides.FindAll(o => o.child == call.child && o.state == call.state))
                {
                    _selection.ExceptWith(o.tracks.SelectMany(t => t.keys));
                    if (_selTrack is UIAnimTrack t && o.tracks.Contains(t)) _selTrack = null;
                    owner.overrides.Remove(o);
                }
            });
        }

        void RebuildInspector()
        {
            _inspector.Clear();
            var state = CurrentState;
            if (state == null)
            {
                var section = Section("No States");
                section.Add(Hint("Add a state with the + tab above."));
                _inspector.Add(section);
            }
            else if (_marker is UIAnimEventMarker marker) BuildEventInspector(marker);
            else if (_marker is UIAnimChildCall call) BuildCallInspector(call);
            else if (_marker is UIAnimSound sound) BuildSoundInspector(sound);
            else if (_selection.Count == 1 && CurrentTrack != null && CurrentTrack.keys.Contains(_selection.First()))
                BuildKeyInspector(CurrentTrack, _selection.First());
            else if (_selection.Count > 1) BuildMultiKeyInspector();
            else if (CurrentTrack != null) BuildTrackInspector(CurrentTrack);
            else if (_selTrack is UIAnimEventTrack events) BuildEventTrackInspector(events);
            else if (_selTrack is UIAnimSoundTrack sounds) BuildSoundTrackInspector(sounds);
            else BuildStateInspector(state);

            // Same label column as Unity's Inspector.
            _inspector.Query(className: BaseField<int>.ussClassName).ForEach(f => f.AddToClassList(BaseField<int>.alignedFieldUssClassName));
            OnPreviewChanged();
        }

        internal void AddState() => Modify("Add State", () =>
        {
            var name = "State";
            for (var n = 1; _target.states.Any(s => s.name == name); n++) name = $"State {n}";
            _target.states.Add(new UIAnimState { name = name });
            _state = _target.states.Count - 1;
            _selTrack = null;
            _selection.Clear();
        });

        void BuildEventInspector(UIAnimEventMarker marker)
        {
            _inspector.Add(Heading($"▼ {marker.name}", new Color32(255, 183, 77, 255)));
            var section = Section("Animation Event");
            var name = new TextField("Name") { value = marker.name, isDelayed = true };
            name.RegisterValueChangedCallback(e => Modify("Rename Animation Event", () => marker.name = e.newValue));
            section.Add(name);
            var time = new FloatField("Time (s)") { value = marker.time, isDelayed = true };
            time.RegisterValueChangedCallback(e => Modify("Move Marker", () =>
            {
                marker.time = Mathf.Max(0f, e.newValue);
                SortMarkers();
            }));
            section.Add(time);
            _inspector.Add(section);

            _inspector.Add(EventDataSection(marker));

            var usage = Section("How to Receive");
            var dataType = marker.DataType;
            usage.Add(Hint("Code:\n" +
                           "anim.EventRaised += (in UIAnimEventInfo e) => { if (e.Name == \"" + marker.name + "\") ... };\n" +
                           "await anim.WaitEventAsync(\"" + marker.name + "\");" +
                           (dataType != null ? $"\nanim.AddListener<{dataType.Name}>((in UIAnimEventInfo e, in {dataType.Name} data) => ...);" : "") +
                           "\nInspector: map event names to UnityEvents on a UI Anim Event Listener component.\n" +
                           "Complete still raises the remaining events in order; a cancelled playback raises none."));
            var remove = new Button(DeleteSelectedKeys) { text = "Delete Event", style = { marginTop = 8, marginLeft = 4, marginRight = 4 } };
            remove.AddToClassList("uia-danger");
            usage.Add(remove);
            _inspector.Add(usage);
        }

        // Event type: none (name only) or a [UIAnimEvent] struct whose fields are this event's parameters (edited through JsonUtility).
        VisualElement EventDataSection(UIAnimEventMarker marker)
        {
            var section = Section("Event Type");
            var types = EventTypes();
            var current = marker.DataType;
            var choices = new List<string> { "Name Only" };
            choices.AddRange(types.Select(t => t.FullName));
            var missing = current == null && !string.IsNullOrEmpty(marker.dataType);
            if (missing) choices.Add(marker.dataType);
            var selected = current != null ? current.FullName : missing ? marker.dataType : "Name Only";

            string Label(string fullName)
            {
                var t = types.FirstOrDefault(x => x.FullName == fullName);
                return t != null ? $"{EventDisplayName(t)}  ({t.Name})" : fullName;
            }
            var type = new PopupField<string>("Type", choices, selected, Label, Label);
            type.RegisterValueChangedCallback(e => Modify("Change Event Data", () =>
            {
                var t = types.FirstOrDefault(x => x.FullName == e.newValue);
                marker.dataType = UIAnimEventMarker.TypeKey(t);
                marker.dataJson = t != null ? JsonUtility.ToJson(Activator.CreateInstance(t)) : "";
                marker.InvalidateData();
            }));
            section.Add(type);

            if (missing) section.Add(Hint($"Type '{marker.dataType}' was not found. It was renamed or deleted."));
            if (current == null)
            {
                if (types.Count == 0)
                    section.Add(Hint("No event types registered. Mark a struct with the attribute in code and it appears here.\n" +
                                     "e.g. [UIAnimEvent(\"Hit\")] public struct HitEvent { public float damage; }"));
                return section;
            }

            var boxed = JsonUtility.FromJson(string.IsNullOrEmpty(marker.dataJson) ? "{}" : marker.dataJson, current);
            foreach (var field in current.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                var editor = DataField(field, boxed, value => Modify("Edit Event Data", () =>
                {
                    field.SetValue(boxed, value);   // boxed struct: the box itself changes
                    marker.dataJson = JsonUtility.ToJson(boxed);
                    marker.InvalidateData();
                }, rebuild: false));
                section.Add(editor ?? Hint($"{field.Name}: {field.FieldType.Name} can't be edited (numbers, strings, vectors, colors and enums only)."));
            }
            return section;
        }

        static VisualElement DataField(System.Reflection.FieldInfo field, object boxed, Action<object> set)
        {
            var label = ObjectNames.NicifyVariableName(field.Name);
            var value = field.GetValue(boxed);
            VisualElement Bind<T>(BaseField<T> f, T v)
            {
                f.value = v;
                f.RegisterValueChangedCallback(e => set(e.newValue));
                return f;
            }
            var type = field.FieldType;
            if (type == typeof(float)) return Bind(new FloatField(label) { isDelayed = true }, (float)value);
            if (type == typeof(int)) return Bind(new IntegerField(label) { isDelayed = true }, (int)value);
            if (type == typeof(bool)) return Bind(new Toggle(label), (bool)value);
            if (type == typeof(string)) return Bind(new TextField(label) { isDelayed = true }, (string)value ?? "");
            if (type == typeof(Vector2)) return Bind(new Vector2Field(label), (Vector2)value);
            if (type == typeof(Vector3)) return Bind(new Vector3Field(label), (Vector3)value);
            if (type == typeof(Color)) return Bind(new ColorField(label), (Color)value);
            if (type.IsEnum)
            {
                var f = new EnumField(label, (Enum)value);
                f.RegisterValueChangedCallback(e => set(e.newValue));
                return f;
            }
            return null;
        }

        void BuildSoundInspector(UIAnimSound sound)
        {
            _inspector.Add(Heading($"♪ {(sound.clip != null ? sound.clip.name : "Sound")}", new Color32(129, 199, 132, 255)));
            var section = Section("Sound");
            var clip = new ObjectField("Clip") { objectType = typeof(AudioClip), allowSceneObjects = false, value = sound.clip };
            clip.RegisterValueChangedCallback(e => Modify("Change Sound", () => sound.clip = e.newValue as AudioClip));
            section.Add(clip);
            var volume = new Slider("Volume", 0f, 1f) { value = sound.volume, showInputField = true };
            volume.RegisterValueChangedCallback(e => Modify("Change Sound", () => sound.volume = e.newValue, rebuild: false));
            section.Add(volume);
            var time = new FloatField("Time (s)") { value = sound.time, isDelayed = true };
            time.RegisterValueChangedCallback(e => Modify("Move Marker", () =>
            {
                sound.time = Mathf.Max(0f, e.newValue);
                SortMarkers();
            }));
            section.Add(time);
            if (sound.clip != null)
            {
                section.Add(Hint($"Length {sound.clip.length:0.00}s · plays {sound.time:0.00}s ~ {sound.End:0.00}s\n" +
                                 "The state's duration includes the sound's end, so destroying after Close doesn't cut it off.\n" +
                                 "Sounds skipped by Complete don't play."));
                var listen = new Button(() => UIAnimAudioPreview.Play(sound.clip)) { text = "▶  Preview", style = { marginTop = 4, marginLeft = 4, marginRight = 4 } };
                section.Add(listen);
            }
            else section.Add(Hint("Assign a clip, or drop an AudioClip from the Project window onto the sound track."));
            section.Add(Hint("Plays with PlayOneShot on the track's AudioSource; empty uses a shared 2D source, so nothing needs to be added."));
            var remove = new Button(DeleteSelectedKeys) { text = "Delete Sound", style = { marginTop = 8, marginLeft = 4, marginRight = 4 } };
            remove.AddToClassList("uia-danger");
            section.Add(remove);
            _inspector.Add(section);
        }

        void BuildEventTrackInspector(UIAnimEventTrack track)
        {
            _inspector.Add(Heading("Animation Event Track", UIAnimTimelineView.EventColor));
            var section = Section("Track");
            section.Add(Hint($"{track.events.Count} event(s). Double-click the lane or press + to add one at the playhead.\n" +
                             "Tracks are only for organizing: events on any track arrive through the same EventRaised."));
            section.Add(RemoveTrackButton(track));
            _inspector.Add(section);
        }

        void BuildSoundTrackInspector(UIAnimSoundTrack track)
        {
            _inspector.Add(Heading("Sound Track", UIAnimTimelineView.SoundColor));
            var section = Section("Track");
            var source = new ObjectField("Audio Source (optional)") { objectType = typeof(AudioSource), allowSceneObjects = true, value = track.source };
            source.RegisterValueChangedCallback(e => Modify("Change Sound Track", () => track.source = e.newValue as AudioSource));
            section.Add(source);
            section.Add(Hint($"{track.sounds.Count} sound(s). No AudioSource needed: empty plays 2D through a shared source (PlayOneShot).\n" +
                             "Assign one only for a mixer group or 3D sound.\n" +
                             "Drop AudioClips from the Project window onto the lane to add them there."));
            section.Add(RemoveTrackButton(track));
            _inspector.Add(section);
        }

        Button RemoveTrackButton(object track)
        {
            var remove = new Button(() => RemoveItem(track)) { text = "Remove Track", style = { marginTop = 8, marginLeft = 4, marginRight = 4 } };
            remove.AddToClassList("uia-danger");
            return remove;
        }

        void BuildCallInspector(UIAnimChildCall call)
        {
            var childName = call.child != null ? call.child.name : "(None)";
            _inspector.Add(Heading($"▸ {childName} · {call.state}", new Color32(120, 170, 255, 255)));
            var section = Section("Child State Call");

            var child = new ObjectField("Child") { objectType = typeof(UIAnimation), allowSceneObjects = true, value = call.child };
            child.RegisterValueChangedCallback(e => Modify("Change Child Call", () =>
            {
                call.child = e.newValue as UIAnimation;
                if (call.Target == null) call.state = call.child != null && call.child.states.Count > 0 ? call.child.states[0].name : "";
            }));
            section.Add(child);

            var names = call.child != null ? call.child.states.Select(st => st.name).ToList() : new List<string>();
            if (!names.Contains(call.state)) names.Insert(0, call.state);
            var state = new PopupField<string>("State", names, call.state);
            state.SetEnabled(call.child != null);
            state.RegisterValueChangedCallback(e => Modify("Change Child Call", () => call.state = e.newValue));
            section.Add(state);

            var time = new FloatField("Time (s)") { value = call.time, isDelayed = true };
            time.RegisterValueChangedCallback(e => Modify("Move Marker", () =>
            {
                call.time = Mathf.Max(0f, e.newValue);
                SortMarkers();
            }));
            section.Add(time);

            var length = call.Target?.Duration ?? 0f;
            section.Add(Hint(call.Target == null
                ? "Nothing plays: the child or state is missing."
                : $"Plays '{call.state}' of {childName} ({length:0.00}s) at {call.time:0.00}s. The parent lasts until at least {call.time + length:0.00}s.\n" +
                  "Completing the parent completes the child too; cancelling the parent lets an already started child keep playing."));

            var remove = new Button(DeleteSelectedKeys) { text = "Delete Call", style = { marginTop = 8, marginLeft = 4, marginRight = 4 } };
            remove.AddToClassList("uia-danger");
            section.Add(remove);
            _inspector.Add(section);
        }

        // ---------- Play On: one binding per state (UI Anim Trigger), or None = played by your own code ----------

        UIAnimBinding PlayOn(string stateName) => _target.GetComponent<UIAnimTrigger>()?.bindings.Find(b => b.state == stateName);

        VisualElement PlayOnSection(UIAnimState state)
        {
            var section = Section("Play On");
            var binding = PlayOn(state.name);
            const string none = "None";
            var choices = new List<string> { none };
            choices.AddRange(Enum.GetNames(typeof(UIAnimEvent)));
            var field = new PopupField<string>("Play On", choices, binding != null ? binding.evt.ToString() : none)
            {
                tooltip = "When this state plays by itself. None: it never plays automatically; call Play/PlayAsync from your own code.",
            };
            field.RegisterValueChangedCallback(e =>
                SetPlayOn(state.name, e.newValue == none ? null : (UIAnimEvent)Enum.Parse(typeof(UIAnimEvent), e.newValue), binding?.interrupt ?? default));
            section.Add(field);

            if (binding == null)
            {
                section.Add(Hint("Not played automatically. Call it from your own code, or pick an event to play it on."));
                return section;
            }

            var interrupt = new EnumField("Interrupt", binding.interrupt) { tooltip = "What happens to a state already playing on the same properties." };
            interrupt.RegisterValueChangedCallback(e => SetPlayOn(state.name, binding.evt, (UIAnimInterrupt)e.newValue));
            section.Add(interrupt);

            // Two states on events that fire together fight over the same object (e.g. OnEnable and Start both run on Instantiate).
            static bool OnCreate(UIAnimEvent e) => e is UIAnimEvent.OnEnable or UIAnimEvent.Start;
            var clash = _target.GetComponent<UIAnimTrigger>().bindings
                .Where(b => b.state != state.name && (b.evt == binding.evt || OnCreate(b.evt) && OnCreate(binding.evt)))
                .Select(b => $"'{b.state}' ({b.evt})").ToList();
            if (clash.Count > 0)
                section.Add(Hint($"⚠ Also played at the same moment: {string.Join(", ", clash)}. Only one of them should play."));
            return section;
        }

        // One binding per state: replaces whatever the state had (null = None).
        void SetPlayOn(string stateName, UIAnimEvent? evt, UIAnimInterrupt interrupt)
        {
            var trigger = _target.GetComponent<UIAnimTrigger>();
            if (trigger == null)
            {
                if (evt == null) return;
                trigger = Undo.AddComponent<UIAnimTrigger>(_target.gameObject);
            }
            Undo.RecordObject(trigger, "Play On");
            trigger.bindings.RemoveAll(b => b.state == stateName);
            if (evt != null) trigger.bindings.Add(new UIAnimBinding { evt = evt.Value, state = stateName, interrupt = interrupt });
            PrefabUtility.RecordPrefabInstancePropertyModifications(trigger);
            EditorUtility.SetDirty(trigger);
            Rebuild();
        }

        // Keeps bindings pointing at the state when it is renamed (to = null: the state is deleted).
        void RenameBindings(string from, string to)
        {
            var trigger = _target.GetComponent<UIAnimTrigger>();
            if (trigger == null || !trigger.bindings.Exists(b => b.state == from)) return;
            Undo.RecordObject(trigger, "Play On");
            if (to == null) trigger.bindings.RemoveAll(b => b.state == from);
            else foreach (var b in trigger.bindings) if (b.state == from) b.state = to;
            PrefabUtility.RecordPrefabInstancePropertyModifications(trigger);
            EditorUtility.SetDirty(trigger);
        }

        void BuildStateInspector(UIAnimState state)
        {
            _inspector.Add(Heading(state.name, null));

            var info = Section("State");
            var name = new TextField("Name") { value = state.name, isDelayed = true };
            name.RegisterValueChangedCallback(e =>
            {
                RenameBindings(state.name, e.newValue);   // the Play On binding follows the name
                Modify("Rename State", () => state.name = e.newValue);
            });
            info.Add(name);
            info.Add(Hint($"Duration {state.Duration:0.00}s · {state.tracks.Count} track(s)"));
            _inspector.Add(info);
            _inspector.Add(PlayOnSection(state));

            var remove = new Button(() =>
            {
                if (!EditorUtility.DisplayDialog("Delete State", $"Delete the state '{state.name}'?", "Delete", "Cancel")) return;
                _preview.Stop();
                RenameBindings(state.name, null);
                Modify("Remove State", () => { _target.states.Remove(state); _state = 0; });
            }) { text = "Delete State", style = { marginTop = 8, marginLeft = 4, marginRight = 4 } };
            remove.AddToClassList("uia-danger");
            _inspector.Add(remove);
        }

        void BuildTrackInspector(UIAnimTrack track)
        {
            _inspector.Add(Heading(TrackTitle(track), UIAnimTimelineView.PropertyColor(track.property)));

            var section = Section("Track");
            var change = new Button { text = $"{TrackTitle(track)}   ▾", tooltip = "Change to another object/property", style = { unityTextAlign = TextAnchor.MiddleLeft } };
            change.clicked += () => ShowPropertyMenu(change.worldBound, track);
            section.Add(change);
            if (IsTextProperty(track.property))
            {
                if (UIAnimTextChars.IsCharProperty(track.property))
                {
                    var stagger = new FloatField("Stagger (s)") { value = track.stagger, isDelayed = true, tooltip = "Each character starts this much later than the previous one. 0 moves all characters together." };
                    stagger.RegisterValueChangedCallback(e => Modify("Edit Track", () => track.stagger = Mathf.Max(0f, e.newValue)));
                    section.Add(stagger);
                    var perChar = new FloatField("Duration per Char (s)") { value = track.durationPerChar, isDelayed = true, tooltip = "Above 0, the i-th character plays all keys over i × this value (s), so later characters move longer. 0 uses the key times as is." };
                    perChar.RegisterValueChangedCallback(e => Modify("Edit Track", () => track.durationPerChar = Mathf.Max(0f, e.newValue)));
                    section.Add(perChar);
                    section.Add(Hint("The keys are one character's motion; each character plays it 'Stagger' later than the previous."));
                }
                else
                {
                    var scramble = new EnumField("Scramble", track.scramble) { tooltip = "Fills characters not revealed yet with random characters." };
                    scramble.RegisterValueChangedCallback(e => Modify("Edit Track", () => track.scramble = (UIAnimScramble)e.newValue));
                    section.Add(scramble);
                    section.Add(Hint("0 = nothing shown, 1 = fully shown. Rich text tags don't count as characters."));
                }
                _inspector.Add(section);
                return;
            }
            // Pointer only makes sense for a position; other properties offer the first two.
            var starts = new List<UIAnimStart> { UIAnimStart.KeyValue, UIAnimStart.Current };
            if (track.property == UIAnimProperty.AnchoredPosition || track.start == UIAnimStart.Pointer) starts.Add(UIAnimStart.Pointer);
            static string StartLabel(UIAnimStart s) => s switch
            {
                UIAnimStart.KeyValue => "Key Value",
                UIAnimStart.Current => "Current Value",
                _ => "Pointer (press position)",
            };
            var fromCurrent = new PopupField<UIAnimStart>("Start", starts, track.start, StartLabel, StartLabel)
            {
                tooltip = "Where the first key's value comes from when the state starts.\n" +
                          "Key Value: the value written in the key.\n" +
                          "Current Value: the value at that moment, so an interrupted animation never jumps.\n" +
                          "Pointer: where the pointer pressed (a ripple from the touch point). Needs a pointer event in Play On.",
            };
            fromCurrent.RegisterValueChangedCallback(e => Modify("Edit Track", () => track.start = e.newValue));
            var relative = new Toggle("Relative To Rest") { value = track.relativeToRest, tooltip = "Key values are added to the rest value. e.g. -14 = 14 smaller than the original." };
            relative.RegisterValueChangedCallback(e => Modify("Edit Track", () => track.relativeToRest = e.newValue));
            section.Add(fromCurrent);
            section.Add(relative);
            _inspector.Add(section);
        }

        void BuildKeyInspector(UIAnimTrack track, UIAnimKey key)
        {
            var index = track.keys.IndexOf(key);
            _inspector.Add(Heading($"◆ {TrackTitle(track)}", UIAnimTimelineView.PropertyColor(track.property)));

            var section = Section("Key");
            var time = new FloatField("Time (s)") { value = key.time, isDelayed = true };
            time.RegisterValueChangedCallback(e => Modify("Edit Key", () =>
            {
                key.time = Mathf.Max(0f, e.newValue);
                track.keys.Sort((a, b) => a.time.CompareTo(b.time));
            }));
            section.Add(time);

            // Every key shows the same fields; what doesn't apply is disabled with the reason, never hidden.
            var live = index == 0 && track.startFromCurrent && (track.keys.Count > 1 || key.time <= 0f);   // value set at playback start
            var value = ValueField(key.IsVibration ? "Strength"
                : track.start == UIAnimStart.Pointer ? "Value (press point +)"
                : track.relativeToRest ? "Value (rest +)" : "Value", track, key);
            value.SetEnabled(!live);
            section.Add(value);
            var canCapture = !live && !IsOffset(track) && track.target != null && !IsTextProperty(track.property);
            var capture = new Button(() =>
            {
                _preview.Stop();   // capture the real value, not a previewed one
                var current = UIAnimation.Read(track.target, track.property);
                Modify("Capture Value", () => key.value = current);
            })
            {
                text = "Capture Current Value",
                tooltip = canCapture ? "Pose the object in the scene, then press this."
                    : live ? "This key's value is set when playback starts."
                    : "Only for absolute values (not offsets from the rest value or press point, not text tracks).",
            };
            capture.SetEnabled(canCapture);
            section.Add(capture);
            if (live)
                section.Add(Hint((track.start == UIAnimStart.Pointer
                                     ? "Set when playback starts: where the pointer pressed."
                                     : "Set when playback starts: the property's current value.")
                                 + (key.time > 0f ? " Held until this time (delay)." : "")));
            _inspector.Add(section);

            // How the track arrives at this key. The first key has no incoming segment unless a lone key starts from current.
            var arrives = index > 0 || (track.startFromCurrent && track.keys.Count == 1 && key.time > 0f);
            var arrival = ArrivalSection(new[] { key }, UIAnimTimelineView.PropertyColor(track.property));
            if (!arrives)
            {
                foreach (var child in arrival.Children()) child.SetEnabled(false);
                arrival.Add(Hint("First key: nothing arrives before it."));
            }
            _inspector.Add(arrival);
        }

        void BuildMultiKeyInspector()
        {
            _inspector.Add(Heading($"◆ {_selection.Count} Keys Selected", null));
            var keys = _selection.ToArray();
            _inspector.Add(ArrivalSection(keys, new Color(0.42f, 0.70f, 1f)));
            var remove = new Button(DeleteSelectedKeys) { text = "Delete Selected Keys (Delete)", style = { marginTop = 8, marginLeft = 4, marginRight = 4 } };
            remove.AddToClassList("uia-danger");
            _inspector.Add(remove);
        }

        // Edits arrival easing for one or many keys; shows the first key's value when they differ.
        VisualElement ArrivalSection(UIAnimKey[] keys, Color color)
        {
            var first = keys[0];
            var arrival = Section("Arrival");
            var type = new EnumField("Type", first.curveType) { showMixedValue = keys.Any(k => k.curveType != first.curveType) };
            type.RegisterValueChangedCallback(e => Modify("Edit Keys", () => { foreach (var k in keys) k.curveType = (UIAnimCurveType)e.newValue; }));
            arrival.Add(type);
            if (keys.All(k => k.curveType != UIAnimCurveType.Curve))
            {
                var ease = new EnumField("Ease", first.ease) { showMixedValue = keys.Any(k => k.ease != first.ease) };
                ease.RegisterValueChangedCallback(e => Modify("Edit Keys", () => { foreach (var k in keys) k.ease = (UIAnimEase)e.newValue; }));
                arrival.Add(ease);
            }
            if (keys.All(k => k.IsVibration))
            {
                var frequency = new FloatField("Frequency (per s)") { value = first.frequency, isDelayed = true, showMixedValue = keys.Any(k => k.frequency != first.frequency) };
                frequency.RegisterValueChangedCallback(e => Modify("Edit Keys", () => { foreach (var k in keys) k.frequency = Mathf.Max(0f, e.newValue); }));
                var damping = new FloatField("Damping") { value = first.damping, isDelayed = true, showMixedValue = keys.Any(k => k.damping != first.damping) };
                damping.RegisterValueChangedCallback(e => Modify("Edit Keys", () => { foreach (var k in keys) k.damping = Mathf.Max(0f, e.newValue); }));
                arrival.Add(frequency);
                arrival.Add(damping);
            }
            else if (keys.All(k => k.curveType == UIAnimCurveType.Curve))
            {
                var curve = new CurveField("Animation Curve") { value = first.curve };
                curve.RegisterValueChangedCallback(e => Modify("Edit Keys", () => { foreach (var k in keys) k.curve = new AnimationCurve(e.newValue.keys); }));
                arrival.Add(curve);
            }

            // Only when the selected keys share one shape; a mixed selection has nothing single to draw.
            var sameShape = keys.All(k => k.curveType == first.curveType && (k.curveType == UIAnimCurveType.Curve || k.ease == first.ease)
                                          && k.frequency == first.frequency && k.damping == first.damping);
            if (sameShape) arrival.Add(new UIAnimEaseGraph(first.Weight, color));
            return arrival;
        }

        string TrackTitle(UIAnimTrack track) => $"{UIAnimTimelineView.TargetPath(_target, track.target)} · {UIAnimTimelineView.PropertyName(track.property)}";

        VisualElement ValueField(string label, UIAnimTrack track, UIAnimKey key)
        {
            void Set(Vector4 v) => Modify("Edit Key", () => key.value = v, rebuild: false);
            var v = key.value;
            var relative = track.relativeToRest;
            switch (track.property)
            {
                case UIAnimProperty.Scale:
                case UIAnimProperty.CharScale:
                    return ScaleField(label, v, key, relative, Set);
                case UIAnimProperty.Rotation:
                case UIAnimProperty.CharOffset:
                case UIAnimProperty.CharRotation:
                {
                    var f = new Vector3Field(label) { value = v };
                    f.RegisterValueChangedCallback(e => Set(e.newValue));
                    return f;
                }
                case UIAnimProperty.CharColor:
                {
                    var f = new ColorField(label) { value = v };
                    f.RegisterValueChangedCallback(e => Set(e.newValue));
                    return f;
                }
                case UIAnimProperty.CharAlpha:
                case UIAnimProperty.TextReveal:
                {
                    var f = new Slider(label, 0f, 1f) { value = v.x, showInputField = true };
                    f.RegisterValueChangedCallback(e => Set(new Vector4(e.newValue, 0, 0, 0)));
                    return f;
                }
                case UIAnimProperty.SizeDelta:
                case UIAnimProperty.AnchoredPosition:
                case UIAnimProperty.AnchorMin:
                case UIAnimProperty.AnchorMax:
                case UIAnimProperty.Pivot:
                {
                    var f = new Vector2Field(label) { value = v };
                    f.RegisterValueChangedCallback(e => Set(e.newValue));
                    return f;
                }
                case UIAnimProperty.GraphicColor when !relative:
                case UIAnimProperty.CameraBackground when !relative:
                {
                    var f = new ColorField(label) { value = v };
                    f.RegisterValueChangedCallback(e => Set(e.newValue));
                    return f;
                }
                case UIAnimProperty.GraphicColor:
                {
                    var f = new Vector4Field(label + " (r,g,b,a)") { value = v };
                    f.RegisterValueChangedCallback(e => Set(e.newValue));
                    return f;
                }
                case UIAnimProperty.Active:
                {
                    var f = new Toggle(label) { value = v.x >= 0.5f };
                    f.RegisterValueChangedCallback(e => Set(new Vector4(e.newValue ? 1f : 0f, 0, 0, 0)));
                    return f;
                }
                case UIAnimProperty.FillAmount when !relative:
                case UIAnimProperty.CanvasGroupAlpha when !relative:
                case UIAnimProperty.GraphicAlpha when !relative:
                {
                    var f = new Slider(label, 0f, 1f) { value = v.x, showInputField = true };
                    f.RegisterValueChangedCallback(e => Set(new Vector4(e.newValue, 0, 0, 0)));
                    return f;
                }
                default:
                {
                    var f = new FloatField(label) { value = v.x };
                    f.RegisterValueChangedCallback(e => Set(new Vector4(e.newValue, 0, 0, 0)));
                    return f;
                }
            }
        }

        // Linked (default when x == y == z): one slider for uniform scale. Unlinked: per-axis Vector3.
        VisualElement ScaleField(string label, Vector4 v, UIAnimKey key, bool relative, Action<Vector4> set)
        {
            var uniform = Mathf.Approximately(v.x, v.y) && Mathf.Approximately(v.y, v.z);
            var linked = uniform && !_unlinkedScale.Contains(key);

            var row = new VisualElement();
            row.AddToClassList("uia-linkrow");
            if (linked)
            {
                var s = relative
                    ? new Slider(label, -1f, 1f) { value = v.x, showInputField = true, style = { flexGrow = 1 } }
                    : new Slider(label, 0f, 2f) { value = v.x, showInputField = true, style = { flexGrow = 1 } };
                s.RegisterValueChangedCallback(e => set(new Vector4(e.newValue, e.newValue, e.newValue, 0)));
                row.Add(s);
            }
            else
            {
                var f = new Vector3Field(label) { value = v, style = { flexGrow = 1 } };
                f.RegisterValueChangedCallback(e => set(e.newValue));
                row.Add(f);
            }

            var link = new Button(() =>
            {
                if (linked) _unlinkedScale.Add(key);
                else
                {
                    _unlinkedScale.Remove(key);
                    if (!uniform) { Modify("Link Scale", () => key.value = new Vector4(v.x, v.x, v.x, 0)); return; }
                }
                RebuildInspector();
            }) { tooltip = linked ? "Edit per axis" : "Edit X/Y/Z together (matches X)" };
            link.AddToClassList("uia-link");
            link.EnableInClassList("uia-link--on", linked);
            var icon = EditorGUIUtility.IconContent(linked ? "Linked" : "Unlinked")?.image as Texture2D;
            if (icon != null) link.style.backgroundImage = icon;
            else link.text = linked ? "⛓" : "⋯";
            row.Add(link);
            return row;
        }

        // Unity-Inspector-style section: a foldout header bar with the fields indented under it.
        static VisualElement Section(string title)
        {
            var section = new Foldout { text = title, value = true };
            section.AddToClassList("uia-section");
            return section;
        }

        static Label Heading(string text, Color? stripe)
        {
            var l = new Label(text);
            l.AddToClassList("uia-heading");
            if (stripe.HasValue)
            {
                l.style.borderLeftWidth = 4;
                l.style.borderLeftColor = stripe.Value;
            }
            return l;
        }

        static Label Hint(string text)
        {
            var l = new Label(text);
            l.AddToClassList("uia-hint");
            return l;
        }

        // ---------- preview ----------

        void TogglePlay()
        {
            if (CurrentState == null) return;
            if (!_preview.IsActive || _preview.State != CurrentState) _preview.Begin(_target, CurrentState);
            if (_preview.IsPlaying) _preview.Pause();
            else _preview.Play();
        }

        void OnPreviewChanged()
        {
            if (_timeLabel == null) return;
            var state = CurrentState;
            var duration = state?.Duration ?? 0f;
            var t = _preview.IsActive && _preview.State == state ? _preview.Time : _timeline.Time;
            _timeLabel.text = $"{t:0.00} / {duration:0.00}s";
            _play.text = _preview.IsPlaying ? "❚❚" : "▶";
            _status.text = _preview.IsActive ? "● Previewing — values are not saved" : "";
            _timeline.SetTime(t);
        }
    }
}
