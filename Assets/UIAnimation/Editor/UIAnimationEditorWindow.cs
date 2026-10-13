using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LitMotion;
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

        UIAnimation _target;
        int _state, _track = -1;
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

        UIAnimState CurrentState => _target != null && _state < _target.states.Count ? _target.states[_state] : null;
        UIAnimTrack CurrentTrack => CurrentState != null && _track >= 0 && _track < CurrentState.tracks.Count ? CurrentState.tracks[_track] : null;

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
            _play = IconButton("▶", "재생 / 일시정지 (Play 모드 없이 미리보기)", TogglePlay, accent: true);
            topbar.Add(_play);
            topbar.Add(IconButton("■", "정지하고 원래 값으로 복원", () => _preview.Stop()));
            _loop = IconButton("⟲", "반복 재생", () =>
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

        void SetTarget(UIAnimation target)
        {
            if (target == _target) return;
            _preview.Stop();
            _target = target;
            _state = 0;
            _track = -1;
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
            Rebuild();
            RefreshPreview();
        }

        // ---------- data edits (all undoable, recorded as prefab overrides on instances) ----------

        void Modify(string undoName, Action edit, bool rebuild = true)
        {
            Undo.RecordObject(_target, undoName);
            edit();
            PrefabUtility.RecordPrefabInstancePropertyModifications(_target);
            EditorUtility.SetDirty(_target);
            if (rebuild) Rebuild();
            else _timeline.Refresh(_track);
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
                _track = CurrentState.tracks.FindIndex(t => t.keys.Contains(first));
            }
            _timeline.Refresh(_track);
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
                _track = track;
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
                _track = -1;
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
                    foreach (var t in CurrentState.tracks) t.keys.Sort((a, b) => a.time.CompareTo(b.time));
                });
                if (_dragUndoGroup >= 0) Undo.CollapseUndoOperations(_dragUndoGroup);
                _dragUndoGroup = -1;
            };
            _timeline.DeleteRequested = DeleteSelectedKeys;
            _timeline.AddKeyRequested = (track, time) => Modify("Add Key", () =>
            {
                var t = CurrentState.tracks[track];
                var key = new UIAnimKey { time = time, value = RawValueAt(t, time) };
                var index = t.keys.FindIndex(k => k.time > time);
                t.keys.Insert(index < 0 ? t.keys.Count : index, key);
                _selection.Clear();
                _selection.Add(key);
                _track = track;
            });
            _timeline.RemoveTrackRequested = track => Modify("Remove Track", () =>
            {
                _selection.ExceptWith(CurrentState.tracks[track].keys);
                CurrentState.tracks.RemoveAt(track);
                _track = -1;
            });
            _timeline.AddTrackMenuRequested = rect => ShowPropertyMenu(rect, null);

            _timeline.MarkerClicked = marker =>
            {
                _selection.Clear();
                _track = -1;
                _marker = marker;
                _timeline.SelectedMarker = marker;
                _timeline.Refresh(_track);
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
            _timeline.AddEventRequested = time => Modify("Add Animation Event", () =>
            {
                var name = "Event";
                for (var n = 1; CurrentState.events.Exists(x => x.name == name); n++) name = $"Event {n}";
                var marker = new UIAnimEventMarker { time = time, name = name };
                CurrentState.events.Add(marker);
                SelectNewMarker(marker);
            });
            _timeline.AddCallRequested = ShowChildCallMenu;
        }

        static void SetMarkerTime(object marker, float time)
        {
            time = Mathf.Max(0f, time);
            if (marker is UIAnimEventMarker e) e.time = time;
            else if (marker is UIAnimChildCall call) call.time = time;
        }

        void SortMarkers()
        {
            CurrentState.events.Sort((a, b) => a.time.CompareTo(b.time));
            CurrentState.childCalls.Sort((a, b) => a.time.CompareTo(b.time));
        }

        void SelectNewMarker(object marker)
        {
            SortMarkers();
            _selection.Clear();
            _track = -1;
            _marker = marker;
        }

        // Child UIAnimations under this one, each with its states: pick one to play at `time`.
        void ShowChildCallMenu(Rect at, float time)
        {
            var menu = new GenericMenu();
            var children = _target.GetComponentsInChildren<UIAnimation>(true).Where(c => c != _target).ToArray();
            foreach (var child in children)
            {
                var path = UIAnimTimelineView.TargetPath(_target, child).Replace('/', '\\');
                if (child.states.Count == 0) menu.AddDisabledItem(new GUIContent($"{path}/(상태 없음)"));
                foreach (var state in child.states)
                {
                    var (c, name) = (child, state.name);
                    menu.AddItem(new GUIContent($"{path}/{name}"), false, () => Modify("Add Child Call", () =>
                    {
                        var call = new UIAnimChildCall { time = time, child = c, state = name };
                        CurrentState.childCalls.Add(call);
                        SelectNewMarker(call);
                    }));
                }
            }
            if (children.Length == 0) menu.AddDisabledItem(new GUIContent("이 오브젝트 아래에 UIAnimation이 있는 자식이 없습니다"));
            menu.DropDown(at);
        }

        void DeleteSelectedKeys()
        {
            if (_marker != null)
            {
                var marker = _marker;
                Modify("Delete Marker", () =>
                {
                    if (marker is UIAnimEventMarker e) CurrentState.events.Remove(e);
                    else if (marker is UIAnimChildCall call) CurrentState.childCalls.Remove(call);
                    _marker = null;
                });
                return;
            }
            if (_selection.Count == 0) return;
            Modify("Delete Keys", () =>
            {
                foreach (var t in CurrentState.tracks) t.keys.RemoveAll(_selection.Contains);
                _selection.Clear();
            });
        }

        // ---------- property picker: the animated object's hierarchy, like the Animation window's "Add Property" ----------

        /// <summary>Opens the property picker. <paramref name="retarget"/> changes that track instead of adding one.</summary>
        void ShowPropertyMenu(Rect at, UIAnimTrack retarget)
        {
            var used = new HashSet<(Component, UIAnimProperty)>(CurrentState.tracks.Select(t => (t.target, t.property)));
            (Component, UIAnimProperty)? current = retarget != null ? (retarget.target, retarget.property) : null;
            UIAnimPropertyPicker.Show(this, at, _target, used, current, (component, property) =>
            {
                if (retarget == null) AddTrack(component, property);
                else Retarget(retarget, component, property);
            });
        }

        void AddTrack(Component target, UIAnimProperty property) => Modify("Add Track", () =>
        {
            var time = CurrentState.Duration > 0f ? CurrentState.Duration : 0.2f;
            var key = new UIAnimKey { time = time, value = UIAnimation.Read(target, property) };
            // Text tracks animate absolute values (reveal 0→1, per-character effects), so they never start from the live value.
            var text = IsTextProperty(property);
            CurrentState.tracks.Add(new UIAnimTrack
            {
                target = target, property = property, startFromCurrent = !text,
                stagger = UIAnimTextChars.IsCharProperty(property) ? 0.05f : 0f,
                keys = { key },
            });
            _track = CurrentState.tracks.Count - 1;
            _selection.Clear();
            _selection.Add(key);
        });

        void Retarget(UIAnimTrack track, Component target, UIAnimProperty property) => Modify("Change Track Target", () =>
        {
            track.target = target;
            track.property = property;
        });

        static bool IsTextProperty(UIAnimProperty p) => p == UIAnimProperty.TextReveal || UIAnimTextChars.IsCharProperty(p);

        // The value a new key should get: what the track's keys say at that time, or the live value for an empty track.
        static Vector4 RawValueAt(UIAnimTrack track, float time)
        {
            var keys = track.keys;
            if (keys.Count == 0)
                return track.relativeToRest || track.target == null ? Vector4.zero : UIAnimation.Read(track.target, track.property);
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
            _targetLabel.text = hasTarget ? _target.name : "UIAnimation 없음";

            if (!hasTarget)
            {
                _timeline.SetState(null, null, -1, _selection);
                _inspector.Clear();
                var section = Section("시작하기");
                section.Add(Hint("UIAnimation 컴포넌트가 있는 오브젝트를 선택하세요."));
                var go = Selection.activeGameObject;
                if (go != null && go.transform is RectTransform)
                    section.Add(new Button(() => SetTarget(Undo.AddComponent<UIAnimation>(go))) { text = $"'{go.name}'에 UIAnimation 추가" });
                _inspector.Add(section);
                OnPreviewChanged();
                return;
            }

            _state = Mathf.Clamp(_state, 0, Mathf.Max(0, _target.states.Count - 1));
            for (var i = 0; i < _target.states.Count; i++)
            {
                var index = i;
                var tab = new Button(() =>
                {
                    if (index == _state) return;
                    _preview.Stop();
                    _state = index;
                    _track = -1;
                    _selection.Clear();
                    Rebuild();
                }) { text = _target.states[i].name };
                tab.AddToClassList("uia-tab");
                tab.EnableInClassList("uia-tab--selected", i == _state);
                _tabs.Add(tab);
            }
            var add = new Button(AddState) { text = "+", tooltip = "상태 추가" };
            add.AddToClassList("uia-tab");
            add.AddToClassList("uia-tab--add");
            _tabs.Add(add);

            if (_marker != null && !CurrentState.events.Contains(_marker) && !CurrentState.childCalls.Contains(_marker))
                _marker = null;   // state switched or marker removed
            _timeline.SelectedMarker = _marker;
            _timeline.SetState(_target, CurrentState, _track, _selection);
            RebuildInspector();
        }

        void RebuildInspector()
        {
            _inspector.Clear();
            var state = CurrentState;
            if (state == null)
            {
                var section = Section("상태 없음");
                section.Add(Hint("위의 + 탭으로 상태를 추가하세요."));
                _inspector.Add(section);
            }
            else if (_marker is UIAnimEventMarker marker) BuildEventInspector(marker);
            else if (_marker is UIAnimChildCall call) BuildCallInspector(call);
            else if (_selection.Count == 1 && CurrentTrack != null && CurrentTrack.keys.Contains(_selection.First()))
                BuildKeyInspector(CurrentTrack, _selection.First());
            else if (_selection.Count > 1) BuildMultiKeyInspector();
            else if (CurrentTrack != null) BuildTrackInspector(CurrentTrack);
            else BuildStateInspector(state);

            // Same label column as Unity's Inspector.
            _inspector.Query(className: BaseField<int>.ussClassName).ForEach(f => f.AddToClassList(BaseField<int>.alignedFieldUssClassName));
            OnPreviewChanged();
        }

        void AddState() => Modify("Add State", () =>
        {
            var name = "State";
            for (var n = 1; _target.states.Any(s => s.name == name); n++) name = $"State {n}";
            _target.states.Add(new UIAnimState { name = name });
            _state = _target.states.Count - 1;
            _track = -1;
            _selection.Clear();
        });

        void BuildEventInspector(UIAnimEventMarker marker)
        {
            _inspector.Add(Heading($"▼ {marker.name}", new Color32(255, 183, 77, 255)));
            var section = Section("애니메이션 이벤트");
            var name = new TextField("이름") { value = marker.name, isDelayed = true };
            name.RegisterValueChangedCallback(e => Modify("Rename Animation Event", () => marker.name = e.newValue));
            section.Add(name);
            var time = new FloatField("시간 (s)") { value = marker.time, isDelayed = true };
            time.RegisterValueChangedCallback(e => Modify("Move Marker", () =>
            {
                marker.time = Mathf.Max(0f, e.newValue);
                SortMarkers();
            }));
            section.Add(time);
            _inspector.Add(section);

            _inspector.Add(EventDataSection(marker));

            var usage = Section("받는 방법");
            var dataType = marker.DataType;
            usage.Add(Hint("코드:\n" +
                           "anim.EventRaised += (in UIAnimEventInfo e) => { if (e.Name == \"" + marker.name + "\") ... };\n" +
                           "await anim.WaitEventAsync(\"" + marker.name + "\");" +
                           (dataType != null ? $"\nanim.AddListener<{dataType.Name}>((in UIAnimEventInfo e, in {dataType.Name} data) => ...);" : "") +
                           "\n인스펙터: UI Anim Event Listener 컴포넌트에 이벤트 이름과 UnityEvent를 연결합니다.\n" +
                           "Complete로 건너뛰면 남은 이벤트도 순서대로 발생하고, 취소되면 발생하지 않습니다."));
            var remove = new Button(DeleteSelectedKeys) { text = "이벤트 삭제", style = { marginTop = 8, marginLeft = 4, marginRight = 4 } };
            remove.AddToClassList("uia-danger");
            usage.Add(remove);
            _inspector.Add(usage);
        }

        // Custom data: an IUIAnimEventData struct picked from the project, its fields edited through JsonUtility.
        VisualElement EventDataSection(UIAnimEventMarker marker)
        {
            var section = Section("데이터 (struct)");
            var types = TypeCache.GetTypesDerivedFrom<IUIAnimEventData>()
                .Where(t => t.IsValueType && !t.IsGenericType).OrderBy(t => t.Name).ToList();
            var current = marker.DataType;
            var choices = new List<string> { "(없음)" };
            choices.AddRange(types.Select(t => t.FullName));
            var missing = current == null && !string.IsNullOrEmpty(marker.dataType);
            if (missing) choices.Add(marker.dataType);
            var selected = current != null ? current.FullName : missing ? marker.dataType : "(없음)";

            var type = new PopupField<string>("형식", choices, selected);
            type.RegisterValueChangedCallback(e => Modify("Change Event Data", () =>
            {
                var t = types.FirstOrDefault(x => x.FullName == e.newValue);
                marker.dataType = UIAnimEventMarker.TypeKey(t);
                marker.dataJson = t != null ? JsonUtility.ToJson(Activator.CreateInstance(t)) : "";
                marker.InvalidateData();
            }));
            section.Add(type);

            if (missing) section.Add(Hint($"형식 '{marker.dataType}'을 찾을 수 없습니다. 이름이 바뀌었거나 삭제됐습니다."));
            if (current == null)
            {
                if (types.Count == 0)
                    section.Add(Hint("IUIAnimEventData를 구현한 struct가 없습니다.\n예: public struct HitEvent : IUIAnimEventData { public float damage; }"));
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
                section.Add(editor ?? Hint($"{field.Name}: {field.FieldType.Name} 형식은 편집할 수 없습니다 (숫자·문자열·벡터·색·enum만 지원)."));
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

        void BuildCallInspector(UIAnimChildCall call)
        {
            var childName = call.child != null ? call.child.name : "(없음)";
            _inspector.Add(Heading($"▸ {childName} · {call.state}", new Color32(120, 170, 255, 255)));
            var section = Section("자식 상태 호출");

            var child = new ObjectField("자식") { objectType = typeof(UIAnimation), allowSceneObjects = true, value = call.child };
            child.RegisterValueChangedCallback(e => Modify("Change Child Call", () =>
            {
                call.child = e.newValue as UIAnimation;
                if (call.Target == null) call.state = call.child != null && call.child.states.Count > 0 ? call.child.states[0].name : "";
            }));
            section.Add(child);

            var names = call.child != null ? call.child.states.Select(st => st.name).ToList() : new List<string>();
            if (!names.Contains(call.state)) names.Insert(0, call.state);
            var state = new PopupField<string>("상태", names, call.state);
            state.SetEnabled(call.child != null);
            state.RegisterValueChangedCallback(e => Modify("Change Child Call", () => call.state = e.newValue));
            section.Add(state);

            var time = new FloatField("시간 (s)") { value = call.time, isDelayed = true };
            time.RegisterValueChangedCallback(e => Modify("Move Marker", () =>
            {
                call.time = Mathf.Max(0f, e.newValue);
                SortMarkers();
            }));
            section.Add(time);

            var length = call.Target?.Duration ?? 0f;
            section.Add(Hint(call.Target == null
                ? "자식 또는 상태가 없어서 재생되지 않습니다."
                : $"{call.time:0.00}초에 {childName}의 '{call.state}'({length:0.00}s)를 재생합니다. 부모 길이는 {call.time + length:0.00}초까지 늘어납니다.\n" +
                  "부모를 Complete로 건너뛰면 자식도 끝 값이 되고, 부모가 취소돼도 이미 시작한 자식은 계속 재생됩니다."));

            var remove = new Button(DeleteSelectedKeys) { text = "호출 삭제", style = { marginTop = 8, marginLeft = 4, marginRight = 4 } };
            remove.AddToClassList("uia-danger");
            section.Add(remove);
            _inspector.Add(section);
        }

        void BuildStateInspector(UIAnimState state)
        {
            _inspector.Add(Heading(state.name, null));

            var info = Section("상태");
            var name = new TextField("이름") { value = state.name, isDelayed = true };
            name.RegisterValueChangedCallback(e => Modify("Rename State", () => state.name = e.newValue));
            info.Add(name);
            info.Add(Hint($"길이 {state.Duration:0.00}s · 트랙 {state.tracks.Count}개"));
            _inspector.Add(info);

            var actions = Section("시작 시 동작");
            actions.Add(Hint("상태가 재생을 시작하는 순간 한 번 실행됩니다. 예) 포인터 위치로 이동: 누른 곳에서 원이 퍼지는 연출(Button2)."));
            for (var i = 0; i < state.actions.Count; i++)
            {
                var action = state.actions[i];
                var index = i;
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2 } };
                var type = new EnumField(action.type) { style = { width = 120 } };
                type.RegisterValueChangedCallback(e => Modify("Change Action", () => action.type = (UIAnimActionType)e.newValue));
                var target = new ObjectField { objectType = typeof(RectTransform), allowSceneObjects = true, value = action.target, style = { flexGrow = 1, flexShrink = 1 } };
                target.RegisterValueChangedCallback(e => Modify("Change Action Target", () => action.target = e.newValue as RectTransform));
                row.Add(type);
                row.Add(target);
                row.Add(new Button(() => Modify("Remove Action", () => state.actions.RemoveAt(index))) { text = "×" });
                actions.Add(row);
            }
            actions.Add(new Button(() => Modify("Add Action", () => state.actions.Add(new UIAnimAction()))) { text = "+ 동작 추가" });
            _inspector.Add(actions);

            var remove = new Button(() =>
            {
                if (!EditorUtility.DisplayDialog("상태 삭제", $"'{state.name}' 상태를 삭제할까요?", "삭제", "취소")) return;
                _preview.Stop();
                Modify("Remove State", () => { _target.states.Remove(state); _state = 0; });
            }) { text = "상태 삭제", style = { marginTop = 8, marginLeft = 4, marginRight = 4 } };
            remove.AddToClassList("uia-danger");
            _inspector.Add(remove);
        }

        void BuildTrackInspector(UIAnimTrack track)
        {
            _inspector.Add(Heading(TrackTitle(track), UIAnimTimelineView.PropertyColor(track.property)));

            var section = Section("트랙");
            var change = new Button { text = $"{TrackTitle(track)}   ▾", tooltip = "다른 오브젝트/속성으로 변경", style = { unityTextAlign = TextAnchor.MiddleLeft } };
            change.clicked += () => ShowPropertyMenu(change.worldBound, track);
            section.Add(change);
            if (IsTextProperty(track.property))
            {
                if (UIAnimTextChars.IsCharProperty(track.property))
                {
                    var stagger = new FloatField("글자 간격 (s)") { value = track.stagger, isDelayed = true, tooltip = "각 글자가 앞 글자보다 이만큼 늦게 시작합니다. 0이면 모든 글자가 함께 움직입니다." };
                    stagger.RegisterValueChangedCallback(e => Modify("Edit Track", () => track.stagger = Mathf.Max(0f, e.newValue)));
                    section.Add(stagger);
                    var perChar = new FloatField("글자당 길이 (s)") { value = track.durationPerChar, isDelayed = true, tooltip = "0보다 크면 i번째 글자가 키 전체를 i × 이 값(초) 동안 재생합니다. 뒤 글자일수록 길게 움직입니다. 0이면 키 시간 그대로." };
                    perChar.RegisterValueChangedCallback(e => Modify("Edit Track", () => track.durationPerChar = Mathf.Max(0f, e.newValue)));
                    section.Add(perChar);
                    section.Add(Hint("키는 글자 하나의 움직임입니다. 글자마다 '글자 간격'만큼 늦게 같은 움직임을 재생합니다."));
                }
                else
                {
                    var scramble = new EnumField("스크램블", track.scramble) { tooltip = "아직 나오지 않은 글자를 무작위 문자로 채웁니다." };
                    scramble.RegisterValueChangedCallback(e => Modify("Edit Track", () => track.scramble = (ScrambleMode)e.newValue));
                    section.Add(scramble);
                    section.Add(Hint("0 = 글자 없음, 1 = 전체 표시. 리치 텍스트 태그는 글자 수에 포함되지 않습니다."));
                }
                _inspector.Add(section);
                return;
            }
            var fromCurrent = new Toggle("현재 값에서 시작") { value = track.startFromCurrent, tooltip = "첫 키가 재생 시작 순간의 값이 됩니다. 중간에 끊겨도 값이 튀지 않습니다. 첫 키를 뒤로 옮기면 그만큼 기다렸다가 시작합니다(딜레이). 키가 하나면 현재 값 → 그 키." };
            fromCurrent.RegisterValueChangedCallback(e => Modify("Edit Track", () => track.startFromCurrent = e.newValue));
            var relative = new Toggle("원래 값 기준") { value = track.relativeToRest, tooltip = "키 값을 원래 값에 더합니다. 예: -14 = 원래 크기보다 14 작게." };
            relative.RegisterValueChangedCallback(e => Modify("Edit Track", () => track.relativeToRest = e.newValue));
            section.Add(fromCurrent);
            section.Add(relative);
            _inspector.Add(section);
        }

        void BuildKeyInspector(UIAnimTrack track, UIAnimKey key)
        {
            var index = track.keys.IndexOf(key);
            _inspector.Add(Heading($"◆ {TrackTitle(track)}", UIAnimTimelineView.PropertyColor(track.property)));

            var section = Section("키");
            var time = new FloatField("시간 (s)") { value = key.time, isDelayed = true };
            time.RegisterValueChangedCallback(e => Modify("Edit Key", () =>
            {
                key.time = Mathf.Max(0f, e.newValue);
                track.keys.Sort((a, b) => a.time.CompareTo(b.time));
            }));
            section.Add(time);

            var usesCurrent = index == 0 && track.startFromCurrent && (track.keys.Count > 1 || key.time <= 0f);
            if (usesCurrent)
                section.Add(Hint(key.time > 0f
                    ? "'현재 값에서 시작': 이 키는 재생 시작 순간의 값입니다. 이 시간까지 그 값을 유지합니다(딜레이)."
                    : "'현재 값에서 시작': 이 키는 재생 시작 순간의 값입니다."));
            else
            {
                section.Add(ValueField(key.IsVibration ? "세기" : track.relativeToRest ? "값 (원래 값 +)" : "값", track, key));
                if (!track.relativeToRest && track.target != null && !IsTextProperty(track.property))
                    section.Add(new Button(() =>
                    {
                        _preview.Stop();   // capture the real value, not a previewed one
                        var current = UIAnimation.Read(track.target, track.property);
                        Modify("Capture Value", () => key.value = current);
                    }) { text = "현재 값 캡처", tooltip = "씬에서 오브젝트를 원하는 모양으로 바꾼 뒤 누르세요." });
            }
            _inspector.Add(section);

            // How the track arrives at this key. The first key has no incoming segment unless the track starts from current.
            if (index > 0 || (track.startFromCurrent && track.keys.Count == 1 && key.time > 0f))
                _inspector.Add(ArrivalSection(new[] { key }, UIAnimTimelineView.PropertyColor(track.property)));
        }

        void BuildMultiKeyInspector()
        {
            _inspector.Add(Heading($"◆ 키 {_selection.Count}개 선택", null));
            var keys = _selection.ToArray();
            _inspector.Add(ArrivalSection(keys, new Color(0.42f, 0.70f, 1f)));
            var remove = new Button(DeleteSelectedKeys) { text = "선택한 키 삭제 (Delete)", style = { marginTop = 8, marginLeft = 4, marginRight = 4 } };
            remove.AddToClassList("uia-danger");
            _inspector.Add(remove);
        }

        // Edits arrival easing for one or many keys; shows the first key's value when they differ.
        VisualElement ArrivalSection(UIAnimKey[] keys, Color color)
        {
            var first = keys[0];
            var arrival = Section("도착 방식");
            var type = new EnumField("방식", first.curveType) { showMixedValue = keys.Any(k => k.curveType != first.curveType) };
            type.RegisterValueChangedCallback(e => Modify("Edit Keys", () => { foreach (var k in keys) k.curveType = (UIAnimCurveType)e.newValue; }));
            arrival.Add(type);
            if (keys.All(k => k.curveType != UIAnimCurveType.Curve))
            {
                var ease = new EnumField("Ease", first.ease) { showMixedValue = keys.Any(k => k.ease != first.ease) };
                ease.RegisterValueChangedCallback(e => Modify("Edit Keys", () => { foreach (var k in keys) k.ease = (Ease)e.newValue; }));
                arrival.Add(ease);
            }
            if (keys.All(k => k.IsVibration))
            {
                var frequency = new FloatField("초당 진동 수") { value = first.frequency, isDelayed = true, showMixedValue = keys.Any(k => k.frequency != first.frequency) };
                frequency.RegisterValueChangedCallback(e => Modify("Edit Keys", () => { foreach (var k in keys) k.frequency = Mathf.Max(0f, e.newValue); }));
                var damping = new FloatField("감쇠") { value = first.damping, isDelayed = true, showMixedValue = keys.Any(k => k.damping != first.damping) };
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
                {
                    var f = new Vector2Field(label) { value = v };
                    f.RegisterValueChangedCallback(e => Set(e.newValue));
                    return f;
                }
                case UIAnimProperty.GraphicColor when !relative:
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
                case UIAnimProperty.FillAmount when !relative:
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
            }) { tooltip = linked ? "축별로 편집" : "X/Y/Z 함께 편집 (X 값으로 맞춤)" };
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
            _status.text = _preview.IsActive ? "● 프리뷰 중 — 값은 저장되지 않음" : "";
            _timeline.SetTime(t);
        }
    }
}
