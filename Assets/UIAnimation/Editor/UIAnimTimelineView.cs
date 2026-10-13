using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeoBYP.TimelineAnimation.Editor
{
    /// <summary>
    /// Dopesheet-style timeline: ruler, one row per track, keyframe diamonds.
    /// Selection works like Unity's Animation window: click, Shift/Ctrl+click, box select, drag selected keys, Delete.
    /// The window owns the data, undo and the selection set; this view only reports intent through callbacks.
    /// </summary>
    internal sealed class UIAnimTimelineView : VisualElement
    {
        public const float HeaderWidth = 260f;
        const float Snap = 0.01f;
        const float LaneRight = 16f;
        const float DragThreshold = 3f;

        // Callbacks to the window.
        public Action<float> Scrubbed;
        public Action<int> TrackSelected;                                 // track header clicked
        public Action<UIAnimKey, bool> KeyClicked;                        // key, additive (Shift/Ctrl)
        public Action<List<UIAnimKey>, bool> BoxSelected;                 // keys inside the box, additive
        public Action SelectionCleared;
        public Action<float> SelectedKeysMoved;                           // delta seconds from drag start (live)
        public Action SelectedKeysDragStarted;
        public Action DragFinished;
        public Action DeleteRequested;
        public Action<int, float> AddKeyRequested;                        // track, time
        public Action<int> RemoveTrackRequested;
        public Action<Rect> AddTrackMenuRequested;                        // where to drop the menu
        public Action<UIAnimSignal> SignalClicked;                        // null = clicked the empty signal lane
        public Action<UIAnimSignal, float> SignalMoved;                   // signal, new time (live while dragging)
        public Action SignalDragFinished;
        public Action<float> AddSignalRequested;                          // time

        public UIAnimSignal SelectedSignal { get; set; }

        readonly VisualElement _ruler;
        readonly VisualElement _rows;
        readonly VisualElement _playhead;
        readonly VisualElement _box;
        readonly List<(UIAnimKey key, VisualElement el)> _keyElements = new();
        readonly List<(VisualElement header, int track)> _headers = new();
        readonly List<VisualElement> _lanes = new();
        readonly VisualElement _signalLane;
        readonly List<(UIAnimSignal signal, Label el)> _signalElements = new();
        UIAnimSignal _dragSignal;
        bool _signalDragged;
        UIAnimState _state;
        UIAnimation _root;
        ICollection<UIAnimKey> _selection = Array.Empty<UIAnimKey>();
        int _selTrack = -1;
        float _time;
        float? _frozenRange;

        // Pointer gesture state
        enum Gesture { None, Pending, DragKeys, Box }
        Gesture _gesture;
        Vector2 _downWorld;
        int _downTrack;
        UIAnimKey _downKey;
        bool _downAdditive;

        public float Time => _time;

        public static Color PropertyColor(UIAnimProperty property) => property switch
        {
            UIAnimProperty.Scale => new Color32(74, 144, 226, 255),
            UIAnimProperty.SizeDelta => new Color32(126, 87, 194, 255),
            UIAnimProperty.AnchoredPosition => new Color32(38, 166, 154, 255),
            UIAnimProperty.GraphicColor => new Color32(229, 115, 115, 255),
            UIAnimProperty.GraphicAlpha => new Color32(230, 160, 60, 255),
            UIAnimProperty.FontSize or UIAnimProperty.CharacterSpacing => new Color32(124, 179, 66, 255),
            UIAnimProperty.FillAmount => new Color32(255, 183, 77, 255),
            UIAnimProperty.TextReveal => new Color32(171, 207, 92, 255),
            UIAnimProperty.CharScale or UIAnimProperty.CharOffset or UIAnimProperty.CharRotation => new Color32(77, 182, 172, 255),
            UIAnimProperty.CharColor or UIAnimProperty.CharAlpha => new Color32(240, 98, 146, 255),
            _ => Color.gray,
        };

        public static string PropertyName(UIAnimProperty property) => property switch
        {
            UIAnimProperty.Scale => "Scale",
            UIAnimProperty.SizeDelta => "Size",
            UIAnimProperty.AnchoredPosition => "Position",
            UIAnimProperty.GraphicColor => "Color",
            UIAnimProperty.GraphicAlpha => "Alpha",
            UIAnimProperty.FontSize => "Font Size",
            UIAnimProperty.CharacterSpacing => "Character Spacing",
            UIAnimProperty.FillAmount => "Fill Amount",
            UIAnimProperty.TextReveal => "Reveal (타자기)",
            UIAnimProperty.CharScale => "글자 Scale",
            UIAnimProperty.CharOffset => "글자 Position",
            UIAnimProperty.CharRotation => "글자 Rotation",
            UIAnimProperty.CharColor => "글자 Color",
            UIAnimProperty.CharAlpha => "글자 Alpha",
            _ => property.ToString(),
        };

        public UIAnimTimelineView()
        {
            AddToClassList("uia-timeline");
            focusable = true;   // receives Delete
            RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode is KeyCode.Delete or KeyCode.Backspace) { DeleteRequested?.Invoke(); e.StopPropagation(); }
            });

            var rulerRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var corner = new VisualElement { style = { width = HeaderWidth } };
            corner.AddToClassList("uia-header");
            rulerRow.Add(corner);
            _ruler = new VisualElement { style = { flexGrow = 1, marginRight = LaneRight } };
            _ruler.AddToClassList("uia-ruler");
            _ruler.RegisterCallback<PointerDownEvent>(e => { _ruler.CapturePointer(e.pointerId); Scrubbed?.Invoke(XToTime(e.localPosition.x)); });
            _ruler.RegisterCallback<PointerMoveEvent>(e => { if (_ruler.HasPointerCapture(e.pointerId)) Scrubbed?.Invoke(XToTime(e.localPosition.x)); });
            _ruler.RegisterCallback<PointerUpEvent>(e => _ruler.ReleasePointer(e.pointerId));
            rulerRow.Add(_ruler);
            Add(rulerRow);

            // Signal row: named points in time (double-click the lane or + to add, drag to move, Delete to remove).
            var signalRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            signalRow.AddToClassList("uia-signal-row");
            var signalHeader = new VisualElement { style = { width = HeaderWidth } };
            signalHeader.AddToClassList("uia-header");
            var signalTitle = new Label("시그널") { pickingMode = PickingMode.Ignore, style = { flexGrow = 1 } };
            signalTitle.AddToClassList("uia-track-prop");
            signalHeader.Add(signalTitle);
            signalHeader.Add(SmallButton("+", "재생 위치에 시그널 추가", () => AddSignalRequested?.Invoke(_time)));
            signalRow.Add(signalHeader);
            _signalLane = new VisualElement { style = { flexGrow = 1, marginRight = LaneRight } };
            _signalLane.AddToClassList("uia-signal-lane");
            _signalLane.tooltip = "더블클릭: 시그널 추가 · 드래그: 이동 · Delete: 삭제";
            _signalLane.RegisterCallback<PointerDownEvent>(OnSignalDown);
            _signalLane.RegisterCallback<PointerMoveEvent>(OnSignalMove);
            _signalLane.RegisterCallback<PointerUpEvent>(OnSignalUp);
            signalRow.Add(_signalLane);
            Add(signalRow);

            var scroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            _rows = new VisualElement();
            _rows.RegisterCallback<PointerDownEvent>(OnRowsDown);
            _rows.RegisterCallback<PointerMoveEvent>(OnRowsMove);
            _rows.RegisterCallback<PointerUpEvent>(OnRowsUp);
            scroll.Add(_rows);
            var addTrack = new Button { text = "+  트랙 추가", style = { width = HeaderWidth - 16 } };
            addTrack.clicked += () => AddTrackMenuRequested?.Invoke(addTrack.worldBound);
            addTrack.AddToClassList("uia-addtrack");
            scroll.Add(addTrack);
            Add(scroll);

            _box = new VisualElement { pickingMode = PickingMode.Ignore, style = { display = DisplayStyle.None } };
            _box.AddToClassList("uia-box");
            Add(_box);

            _playhead = new VisualElement { pickingMode = PickingMode.Ignore };
            _playhead.AddToClassList("uia-playhead");
            Add(_playhead);

            RegisterCallback<GeometryChangedEvent>(_ => Layout());
        }

        float LaneWidth => Mathf.Max(50f, layout.width - HeaderWidth - LaneRight);
        // Visible range: the state's length with room to drag keys further out. Frozen while dragging so keys don't jitter.
        float Range => _frozenRange ?? (_state == null ? 1f : Mathf.Max(0.3f, Mathf.Ceil(_state.Duration * 1.4f / 0.05f) * 0.05f));
        float PxPerSecond => LaneWidth / Range;
        float TimeToX(float t) => t * PxPerSecond;
        float XToTime(float x) => Mathf.Max(0f, Mathf.Round(x / PxPerSecond / Snap) * Snap);

        public void SetState(UIAnimation root, UIAnimState state, int selTrack, ICollection<UIAnimKey> selection)
        {
            _root = root;
            _state = state;
            _selTrack = selTrack;
            _selection = selection;
            RebuildRows();
            Layout();
        }

        public void SetTime(float t)
        {
            _time = t;
            _playhead.style.left = HeaderWidth + TimeToX(t);
        }

        /// <summary>Repaints selection and positions without rebuilding (rebuilding would kill an active drag).</summary>
        public void Refresh(int selTrack)
        {
            _selTrack = selTrack;
            foreach (var (key, el) in _keyElements) el.EnableInClassList("uia-key--selected", _selection.Contains(key));
            foreach (var (header, t) in _headers) header.EnableInClassList("uia-header--selected", t == _selTrack);
            foreach (var (signal, el) in _signalElements) el.EnableInClassList("uia-signal--selected", signal == SelectedSignal);
            Layout();
        }

        void RebuildRows()
        {
            _rows.Clear();
            _keyElements.Clear();
            _headers.Clear();
            _lanes.Clear();
            _signalLane.Clear();
            _signalElements.Clear();
            if (_state == null) return;
            for (var i = 0; i < _state.tracks.Count; i++) _rows.Add(MakeRow(i, _state.tracks[i]));
            foreach (var signal in _state.signals)
            {
                var el = new Label("▼ " + signal.name) { userData = signal };
                el.AddToClassList("uia-signal");
                el.EnableInClassList("uia-signal--selected", signal == SelectedSignal);
                _signalElements.Add((signal, el));
                _signalLane.Add(el);
            }
        }

        public static string TargetPath(UIAnimation root, Component target)
        {
            if (target == null) return "(없음)";
            if (root == null || target.transform == root.transform) return target.gameObject.name;
            var path = UnityEditor.AnimationUtility.CalculateTransformPath(target.transform, root.transform);
            return string.IsNullOrEmpty(path) ? target.gameObject.name : path;
        }

        VisualElement MakeRow(int trackIndex, UIAnimTrack track)
        {
            var row = new VisualElement();
            row.AddToClassList("uia-row");
            if (trackIndex % 2 == 1) row.AddToClassList("uia-row--odd");

            var header = new VisualElement { style = { width = HeaderWidth } };
            header.AddToClassList("uia-header");
            header.EnableInClassList("uia-header--selected", trackIndex == _selTrack);
            header.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button == 0 && !(e.target is Button)) { TrackSelected?.Invoke(trackIndex); e.StopPropagation(); }
            });
            _headers.Add((header, trackIndex));

            var stripe = new VisualElement { style = { backgroundColor = PropertyColor(track.property) } };
            stripe.AddToClassList("uia-stripe");
            header.Add(stripe);
            var names = new VisualElement { pickingMode = PickingMode.Ignore, style = { flexGrow = 1, flexShrink = 1, overflow = Overflow.Hidden } };
            var path = new Label(TargetPath(_root, track.target)) { pickingMode = PickingMode.Ignore };
            path.AddToClassList("uia-track-path");
            var prop = new Label(PropertyName(track.property)) { pickingMode = PickingMode.Ignore };
            prop.AddToClassList("uia-track-prop");
            names.Add(path);
            names.Add(prop);
            header.Add(names);
            header.Add(SmallButton("◆", "재생 위치에 키 추가", () => AddKeyRequested?.Invoke(trackIndex, _time)));
            header.Add(SmallButton("×", "트랙 삭제", () => RemoveTrackRequested?.Invoke(trackIndex)));
            row.Add(header);

            var lane = new VisualElement { userData = trackIndex, style = { marginRight = LaneRight } };
            lane.AddToClassList("uia-lane");
            lane.generateVisualContent += ctx => DrawTrackLine(ctx, lane, track);
            lane.RegisterCallback<PointerMoveEvent>(e => lane.tooltip = TailTooltip(track, e.localPosition.x));
            lane.AddManipulator(new ContextualMenuManipulator(e =>
            {
                var t = XToTime(e.localMousePosition.x);
                e.menu.AppendAction("키 추가", _ => AddKeyRequested?.Invoke(trackIndex, t));
                e.menu.AppendAction("선택한 키 삭제", _ => DeleteRequested?.Invoke(),
                    _selection.Count > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }));
            foreach (var key in track.keys) lane.Add(MakeKey(key));
            _lanes.Add(lane);
            row.Add(lane);
            return row;
        }

        // Colored line between keys; a lone start-from-current key gets a thinner lead-in from 0.
        void DrawTrackLine(MeshGenerationContext ctx, VisualElement lane, UIAnimTrack track)
        {
            if (track.keys.Count == 0) return;
            var p = ctx.painter2D;
            var y = lane.contentRect.height * 0.5f;
            var color = PropertyColor(track.property);

            var first = track.keys[0];
            if (track.startFromCurrent && track.keys.Count == 1 && first.time > 0f)
            {
                p.strokeColor = new Color(color.r, color.g, color.b, 0.4f);
                p.lineWidth = 2f;
                p.BeginPath();
                p.MoveTo(new Vector2(0f, y));
                p.LineTo(new Vector2(TimeToX(first.time), y));
                p.Stroke();
            }
            if (track.keys.Count < 2) return;
            var last = track.keys[track.keys.Count - 1].time;
            p.strokeColor = new Color(color.r, color.g, color.b, 0.85f);
            p.lineWidth = 4f;
            p.BeginPath();
            p.MoveTo(new Vector2(TimeToX(first.time), y));
            p.LineTo(new Vector2(TimeToX(last), y));
            p.Stroke();

            // Character tracks: the keys are the first character's motion; later characters finish at track.End.
            if (track.End > last)
            {
                p.strokeColor = new Color(color.r, color.g, color.b, 0.3f);
                p.BeginPath();
                p.MoveTo(new Vector2(TimeToX(last), y));
                p.LineTo(new Vector2(TimeToX(track.End), y));
                p.Stroke();
            }
        }

        // Explains the faded tail while hovering it.
        string TailTooltip(UIAnimTrack track, float x)
        {
            if (track.keys.Count < 2) return "";
            var last = track.keys[track.keys.Count - 1].time;
            var end = track.End;
            if (end <= last || x < TimeToX(last) || x > TimeToX(end)) return "";
            return $"글자 지연 +{end - last:0.00}s (끝 {end:0.00}s)\n" +
                   "키는 첫 글자의 움직임이고, 뒤 글자는 '글자 간격'만큼 늦게 시작해서 여기까지 재생됩니다.\n" +
                   "줄이려면 트랙을 선택하고 '글자 간격' 또는 '글자당 길이'를 줄이세요.";
        }

        static Button SmallButton(string text, string tooltip, Action onClick)
        {
            var b = new Button(onClick) { text = text, tooltip = tooltip };
            b.AddToClassList("uia-smallbtn");
            return b;
        }

        VisualElement MakeKey(UIAnimKey key)
        {
            var el = new VisualElement { userData = key };
            el.AddToClassList("uia-key");
            el.EnableInClassList("uia-key--selected", _selection.Contains(key));
            _keyElements.Add((key, el));
            return el;
        }

        static readonly float[] Steps = { 0.05f, 0.1f, 0.25f, 0.5f, 1f, 2f, 5f, 10f };

        void Layout()
        {
            _ruler.Clear();
            if (_state == null) return;

            // Widest labels ("10.25") need ~44px; pick the finest step that keeps them apart at this zoom.
            var step = Steps[Steps.Length - 1];
            foreach (var s in Steps)
                if (s * PxPerSecond >= 44f) { step = s; break; }
            for (var i = 0; i * step <= Range + 0.0001f; i++)
            {
                var t = i * step;
                var tick = new Label(t.ToString(step < 0.1f ? "0.00" : step < 1f ? "0.0#" : "0")) { pickingMode = PickingMode.Ignore, style = { left = TimeToX(t) } };
                tick.AddToClassList("uia-tick");
                _ruler.Add(tick);
            }

            foreach (var (key, el) in _keyElements)
            {
                el.style.left = TimeToX(key.time) - 6f;
                el.tooltip = $"{key.time:0.00}s";
            }
            foreach (var (signal, el) in _signalElements)
            {
                el.style.left = TimeToX(signal.time) - 5f;   // the ▼ tip sits on the signal time
                el.tooltip = $"{signal.name} · {signal.time:0.00}s";
            }
            foreach (var lane in _lanes) lane.MarkDirtyRepaint();
            SetTime(_time);
        }

        // ---------- signal lane ----------

        void OnSignalDown(PointerDownEvent e)
        {
            if (e.button != 0 || _state == null) return;
            Focus();
            var x = _signalLane.WorldToLocal(e.position).x;
            _dragSignal = (e.target as VisualElement)?.userData as UIAnimSignal;
            _signalDragged = false;
            if (_dragSignal == null)
            {
                if (e.clickCount == 2) AddSignalRequested?.Invoke(XToTime(x));
                else
                {
                    SignalClicked?.Invoke(null);
                    Scrubbed?.Invoke(XToTime(x));
                }
                return;
            }
            SignalClicked?.Invoke(_dragSignal);
            _frozenRange = Range;
            _signalLane.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        void OnSignalMove(PointerMoveEvent e)
        {
            if (_dragSignal == null || !_signalLane.HasPointerCapture(e.pointerId)) return;
            _signalDragged = true;
            SignalMoved?.Invoke(_dragSignal, XToTime(_signalLane.WorldToLocal(e.position).x));
            e.StopPropagation();
        }

        void OnSignalUp(PointerUpEvent e)
        {
            if (_dragSignal == null) return;
            _signalLane.ReleasePointer(e.pointerId);
            _frozenRange = null;
            if (_signalDragged) SignalDragFinished?.Invoke();
            _dragSignal = null;
            e.StopPropagation();
        }

        // ---------- pointer gestures on the lanes: click / Shift+click / box select / drag selected keys ----------

        static bool IsAdditive(IPointerEvent e) => e.shiftKey || e.actionKey;

        void OnRowsDown(PointerDownEvent e)
        {
            if (e.button != 0) return;
            var lane = FindLane(e.target as VisualElement);
            if (lane == null) return;   // headers handle their own clicks
            Focus();

            _downWorld = e.position;
            _downTrack = (int)lane.userData;
            _downKey = (e.target as VisualElement)?.userData as UIAnimKey;
            _downAdditive = IsAdditive(e);

            if (_downKey == null && e.clickCount == 2)
            {
                AddKeyRequested?.Invoke(_downTrack, XToTime(lane.WorldToLocal(e.position).x));
                return;
            }
            // Clicking an unselected key selects it right away so it can be dragged in the same gesture.
            if (_downKey != null && !_selection.Contains(_downKey)) KeyClicked?.Invoke(_downKey, _downAdditive);

            _gesture = Gesture.Pending;
            _rows.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        void OnRowsMove(PointerMoveEvent e)
        {
            if (_gesture == Gesture.None || !_rows.HasPointerCapture(e.pointerId)) return;
            var delta = (Vector2)e.position - _downWorld;

            if (_gesture == Gesture.Pending && delta.magnitude > DragThreshold)
            {
                _gesture = _downKey != null ? Gesture.DragKeys : Gesture.Box;
                if (_gesture == Gesture.DragKeys)
                {
                    _frozenRange = Range;
                    SelectedKeysDragStarted?.Invoke();
                }
            }

            if (_gesture == Gesture.DragKeys)
                SelectedKeysMoved?.Invoke(Mathf.Round(delta.x / PxPerSecond / Snap) * Snap);
            else if (_gesture == Gesture.Box)
            {
                var a = this.WorldToLocal(_downWorld);
                var b = this.WorldToLocal(e.position);
                _box.style.display = DisplayStyle.Flex;
                _box.style.left = Mathf.Min(a.x, b.x);
                _box.style.top = Mathf.Min(a.y, b.y);
                _box.style.width = Mathf.Abs(a.x - b.x);
                _box.style.height = Mathf.Abs(a.y - b.y);
            }
            e.StopPropagation();
        }

        void OnRowsUp(PointerUpEvent e)
        {
            if (_gesture == Gesture.None) return;
            _rows.ReleasePointer(e.pointerId);
            var gesture = _gesture;
            _gesture = Gesture.None;

            switch (gesture)
            {
                case Gesture.Pending:   // a plain click
                    if (_downKey != null) KeyClicked?.Invoke(_downKey, _downAdditive);
                    else
                    {
                        if (!_downAdditive) SelectionCleared?.Invoke();
                        var lane = _lanes[_downTrack];
                        Scrubbed?.Invoke(XToTime(lane.WorldToLocal(_downWorld).x));
                    }
                    break;
                case Gesture.DragKeys:
                    _frozenRange = null;
                    DragFinished?.Invoke();
                    break;
                case Gesture.Box:
                    var box = _box.worldBound;
                    _box.style.display = DisplayStyle.None;
                    var hits = new List<UIAnimKey>();
                    foreach (var (key, el) in _keyElements)
                        if (box.Overlaps(el.worldBound)) hits.Add(key);
                    BoxSelected?.Invoke(hits, _downAdditive);
                    break;
            }
            e.StopPropagation();
        }

        VisualElement FindLane(VisualElement el)
        {
            for (; el != null && el != _rows; el = el.parent)
                if (el.ClassListContains("uia-lane")) return el;
            return null;
        }
    }
}
