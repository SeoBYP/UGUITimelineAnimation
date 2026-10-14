using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SeoBYP.TimelineAnimation
{
    /// <summary>
    /// A virtualized list on a ScrollRect: only the visible items exist, and they are reused as the list scrolls.
    /// Laid out like Unity's Vertical / Horizontal / Grid Layout Group (plus Masonry), filled through callbacks like
    /// UI Toolkit's ListView (bind / unbind by index), and animated with the items' UIAnimation states:
    /// an entry stagger, a state when scrolling brings an item into view, and a state scrubbed by the item's position.
    /// Also: several item prefabs, variable sizes, infinite loop, snapping / one page per swipe, auto scroll,
    /// sticky section headers, sticking to the end (chat), animated item resizing (accordion) and nested scrolling.
    /// </summary>
    [AddComponentMenu("UI Animation/UI Anim Scroll List")]
    [RequireComponent(typeof(ScrollRect))]
    public sealed class UIAnimScrollList : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public enum LayoutType { Vertical, Horizontal, Grid, Masonry }
        public enum GridConstraint { Flexible, FixedColumnCount, FixedRowCount }
        public enum Alignment { Start, Center, End }
        public enum SnapMode { None, Nearest, OnePerSwipe }

        // ---- items ----
        [Tooltip("Instantiated for the visible items and reused while scrolling (prefab 0). Its size is the default item size.")]
        public RectTransform itemPrefab;
        [Tooltip("More item prefabs (1, 2, …), chosen per index with prefabOf (e.g. section headers, chat bubbles of each side).")]
        public List<RectTransform> extraPrefabs = new();
        [Tooltip("Each item's size along the scroll axis is its preferred size from its layout components (e.g. a text that wraps).")]
        public bool measureItems;

        // ---- layout, like the Layout Group inspectors ----
        public LayoutType layout = LayoutType.Vertical;
        public RectOffset padding = new();
        [Tooltip("Vertical/Horizontal: space between items.")]
        public float spacing;
        [Tooltip("Grid: the size of every cell. Masonry: the column width (and the default item height).")]
        public Vector2 cellSize = new(100f, 100f);
        [Tooltip("Grid/Masonry: space between cells (x between columns, y between rows).")]
        public Vector2 gridSpacing;
        public TextAnchor childAlignment = TextAnchor.UpperLeft;
        [Tooltip("Vertical/Horizontal: the last item first.")]
        public bool reverseArrangement;
        [Tooltip("Vertical: items stretch to the viewport width. Horizontal: to its height.")]
        public bool controlChildSize;
        [Tooltip("Grid/Masonry: Flexible fits as many columns as the viewport allows. A fixed row count scrolls horizontally (Grid).")]
        public GridConstraint constraint = GridConstraint.Flexible;
        [Min(1)] public int constraintCount = 2;

        // ---- scrolling ----
        [Tooltip("Vertical/Horizontal: the end continues with the first item again, forever.")]
        public bool loop;
        [Tooltip("After a drag: Nearest settles on the closest item; One Per Swipe moves exactly one item (pages).")]
        public SnapMode snap;
        public Alignment snapAlignment = Alignment.Center;
        [Min(0f)] public float snapDuration = 0.25f;
        [Tooltip("Seconds between automatic moves to the next item (0 = off). Pauses while dragging.")]
        [Min(0f)] public float autoScrollInterval;
        [Tooltip("When items are added while the list is at its end, it stays at the end (chat).")]
        public bool stickToEnd;
        [Tooltip("Items of this prefab are section headers; the current one stays pinned at the top (-1 = off).")]
        public int stickyHeaderPrefab = -1;

        // ---- animation ----
        [Tooltip("Item state played on the visible items one after another by PlayEntry() (and when the list is filled, if On Fill).")]
        public string entryState = "";
        [Min(0f)] public float entryStagger = 0.05f;
        public bool playEntryOnFill = true;
        [Tooltip("Item state played when scrolling brings an item into view.")]
        public string appearState = "";
        [Tooltip("Item state shown at the item's place in the viewport: 0 = start edge, 0.5 = center, 1 = end edge (carousel focus, wheel picker).")]
        public string scrubState = "";

        /// <summary>Which item prefab (0 = itemPrefab, 1… = extraPrefabs) an index uses. Null = all prefab 0.</summary>
        [NonSerialized] public Func<int, int> prefabOf;
        /// <summary>Size of an index along the scroll axis. Null = measured (Measure Items) or the prefab's size.</summary>
        [NonSerialized] public Func<int, float> sizeOf;

        /// <summary>An item came into view (after it was bound).</summary>
        public event Action<int, RectTransform> ItemShown;
        /// <summary>An item left the view (before it is unbound and reused).</summary>
        public event Action<int, RectTransform> ItemHidden;
        /// <summary>The item at the snap alignment changed (pages, carousel, picker selection).</summary>
        public event Action<int> CurrentIndexChanged;

        sealed class Item
        {
            public RectTransform rect;
            public UIAnimation anim;
            public Component view;
            public int index, slot, prefab;   // data index, virtual slot (loops repeat indices), prefab type
            public bool entered;
        }

        ScrollRect _scroll;
        readonly Dictionary<int, Item> _active = new();   // by slot
        readonly List<Stack<Item>> _pools = new();
        readonly List<int> _release = new(), _visible = new();
        readonly HashSet<int> _visibleSet = new();
        readonly List<Rect> _rects = new();               // content space: x right, y down from the content's top-left
        readonly List<float> _measured = new();
        readonly Dictionary<int, float> _sizeOverride = new();
        Action<Item> _bind, _unbind;
        int _count;
        bool _filled;
        float _contentMain, _cycle;
        int _entryFirst;
        UIAnimTimerHandle _entryMotion, _scrollMotion;
        float _scrollFrom, _scrollTarget, _scrollDuration;
        bool _dragging, _routingToParent, _preview, _motionOwnsMovement;
        ScrollRect.MovementType _movementBeforeMotion;
        int _dragStartSlot, _current = -1;
        float _autoTimer;
        ScrollRect _parentScroll;
        Item _stickyHeader;

        public int Count => _count;
        /// <summary>Lowest and highest index in view (inclusive). Empty when last &lt; first.</summary>
        public (int first, int last) VisibleRange { get; private set; } = (0, -1);
        /// <summary>The index at the snap alignment (page, carousel or picker selection).</summary>
        public int CurrentIndex => Mathf.Max(0, _current);

        ScrollRect Scroll => _scroll != null ? _scroll : _scroll = GetComponent<ScrollRect>();
        RectTransform Content => Scroll.content;
        RectTransform Viewport => Scroll.viewport != null ? Scroll.viewport : (RectTransform)transform;

        void OnEnable() => Scroll.onValueChanged.AddListener(OnScrolled);

        void OnDisable()
        {
            Scroll.onValueChanged.RemoveListener(OnScrolled);
            _entryMotion.TryCancel();
            _scrollMotion.TryCancel();
        }

        void OnScrolled(Vector2 _) => UpdateVisible();

        void OnRectTransformDimensionsChange()
        {
            if (_scroll != null && Content != null && _filled) Relayout();
        }

        void Update()
        {
            if (autoScrollInterval <= 0f || _count < 2 || _dragging || _scrollMotion.IsActive())
            {
                if (_dragging) _autoTimer = 0f;
                return;
            }
            _autoTimer += Time.unscaledDeltaTime;
            if (_autoTimer < autoScrollInterval) return;
            _autoTimer = 0f;
            var next = CurrentSlot() + 1;
            if (!loop && next >= _count) next = 0;
            ScrollToSlot(next, snapAlignment, snapDuration > 0f ? snapDuration : 0.3f);
        }

        // ---------- data binding (like UI Toolkit's ListView: bind / unbind by index) ----------

        /// <summary>
        /// Shows <paramref name="count"/> items. <paramref name="bind"/> fills an item when it comes into view;
        /// <paramref name="unbind"/> (optional) clears it before the object is reused for another index.
        /// <typeparamref name="TView"/> is a component on the item prefab (looked up once per item object).
        /// </summary>
        public void SetCount<TView>(int count, Action<TView, int> bind, Action<TView, int> unbind = null) where TView : Component
        {
            ReleaseAll();
            _bind = bind == null ? null : item => bind(View<TView>(item), item.index);
            _unbind = unbind == null ? null : item => unbind(View<TView>(item), item.index);
            Fill(count);
        }

        /// <summary>Shows <paramref name="count"/> items of any prefabs: bind receives the item's RectTransform.</summary>
        public void SetCount(int count, Action<RectTransform, int> bind, Action<RectTransform, int> unbind = null) =>
            SetCount<RectTransform>(count, bind, unbind);

        /// <summary>Shows one item per element of <paramref name="data"/> (kept by reference: call Refresh after changing it).</summary>
        public void SetData<TItem, TView>(IReadOnlyList<TItem> data, Action<TView, TItem, int> bind, Action<TView, int> unbind = null)
            where TView : Component =>
            SetCount<TView>(data.Count, (view, i) => bind(view, data[i], i), unbind);

        /// <summary>
        /// Changes the item count (items were added or removed at the end) and binds the visible items again, keeping the
        /// scroll position (or the end, with Stick To End). With Measure Items only the new items are measured; when the
        /// data of existing items changed (e.g. inserted at the top), call <see cref="Refresh"/>.
        /// </summary>
        public void SetCount(int count, float stickDuration = 0.25f)
        {
            // Still gliding to the end from the last change counts as at the end (messages sent in a row).
            var wasAtEnd = _count > 0 && (AtEnd || _scrollMotion.IsActive() && _scrollTarget >= MaxOffset - 1f);
            var oldCount = _count;
            _count = Mathf.Max(0, count);
            _sizeOverride.Clear();
            // Items still in the list stay as they are (rebound, no Appear again); only new ones come in with Appear.
            _release.Clear();
            foreach (var (slot, item) in _active)
                if (Looping || item.index >= _count || PrefabOf(item.index) != item.prefab) _release.Add(slot);
            foreach (var slot in _release) Release(slot);
            Measure(from: Mathf.Min(oldCount, _count));   // only the new items: the data of the others is the same
            foreach (var item in _active.Values) Rebind(item);
            Relayout();
            if (stickToEnd && wasAtEnd && _count > 0) ScrollToOffset(MaxOffset, stickDuration);
        }

        /// <summary>Binds every visible item again (the data changed). With Measure Items, sizes are measured again.</summary>
        public void Refresh()
        {
            if (measureItems)
            {
                Measure();
                Relayout();
            }
            foreach (var item in _active.Values) Rebind(item);
        }

        /// <summary>Binds one item again if it is in view (and measures it again, with Measure Items).</summary>
        public void RefreshItem(int index)
        {
            if (measureItems && index >= 0 && index < _count)
            {
                Measure(index, index + 1);
                Relayout();
            }
            foreach (var item in _active.Values)
                if (item.index == index) Rebind(item);
        }

        void Rebind(Item item)
        {
            _unbind?.Invoke(item);
            _bind?.Invoke(item);
        }

        static TView View<TView>(Item item) where TView : Component
        {
            if (item.view is not TView view) item.view = view = item.rect.GetComponent<TView>();
            return view;
        }

        void Fill(int count)
        {
            _count = Mathf.Max(0, count);
            _filled = false;
            _current = -1;
            _sizeOverride.Clear();
            Measure();
            Relayout();
            SetOffset(stickToEnd ? MaxOffset : 0f);
            _filled = true;
            if (playEntryOnFill && !_preview) PlayEntry();
        }

        // ---------- getting items ----------

        /// <summary>The item object showing <paramref name="index"/>, or null when it is not in view.</summary>
        public RectTransform GetItem(int index) => FindItem(index)?.rect;

        /// <summary>The item's component of type <typeparamref name="TView"/>, or null when it is not in view.</summary>
        public TView GetItem<TView>(int index) where TView : Component
        {
            var item = FindItem(index);
            return item != null ? View<TView>(item) : null;
        }

        Item FindItem(int index)
        {
            if (!loop) return _active.TryGetValue(index, out var direct) ? direct : null;
            foreach (var item in _active.Values)
                if (item.index == index) return item;
            return null;
        }

        /// <summary>The index of the item under a screen point (e.g. a pointer), or -1.</summary>
        public int IndexAt(Vector2 screenPoint, Camera eventCamera = null)
        {
            foreach (var item in _active.Values)
                if (RectTransformUtility.RectangleContainsScreenPoint(item.rect, screenPoint, eventCamera)) return item.index;
            return -1;
        }

        /// <summary>Scrolls so <paramref name="index"/> sits at the start, center or end of the viewport (the nearest copy, in a loop).</summary>
        public void ScrollTo(int index, Alignment alignment = Alignment.Start, float duration = 0f)
        {
            if (_count == 0) return;
            index = Mathf.Clamp(index, 0, _count - 1);
            ScrollToSlot(loop ? NearestSlotOf(index) : index, alignment, duration);
        }

        void ScrollToSlot(int slot, Alignment alignment, float duration) => ScrollToOffset(OffsetFor(slot, alignment), duration);

        void ScrollToOffset(float target, float duration)
        {
            _scrollMotion.TryCancel();
            Scroll.StopMovement();
            if (!Looping) target = Mathf.Clamp(target, 0f, MaxOffset);
            _scrollTarget = target;
            if (duration <= 0f)
            {
                SetOffset(target);
                return;
            }
            // Released past an end (elastic): glide back from where it is, not from the clamped edge, and keep the
            // ScrollRect's own elastic return from pulling at the same time; its movement type comes back at the end.
            if (!_motionOwnsMovement)
            {
                _movementBeforeMotion = Scroll.movementType;
                _motionOwnsMovement = true;
            }
            Scroll.movementType = ScrollRect.MovementType.Unrestricted;
            _scrollFrom = Offset;
            _scrollDuration = duration;
            _scrollMotion = UIAnimTimer.Start(duration, this,
                static (o, t) =>
                {
                    var list = (UIAnimScrollList)o;
                    var eased = UIAnimEasing.OutCubic(t / list._scrollDuration);
                    list.SetOffset(Mathf.LerpUnclamped(list._scrollFrom, list._scrollTarget, eased), clamp: false);
                },
                static o => ((UIAnimScrollList)o).RestoreMovement(),
                static o => ((UIAnimScrollList)o).RestoreMovement());
        }

        void RestoreMovement()
        {
            if (!_motionOwnsMovement) return;
            _motionOwnsMovement = false;
            Scroll.movementType = _movementBeforeMotion;
        }

        /// <summary>The item closest to the given place in the viewport (start / center / end).</summary>
        public int NearestIndex(Alignment alignment) => _count == 0 ? -1 : Wrap(NearestSlot(alignment));

        int NearestSlot(Alignment alignment)
        {
            var best = 0;
            var bestDistance = float.MaxValue;
            foreach (var slot in _visible)
            {
                var distance = Mathf.Abs(OffsetFor(slot, alignment) - Offset);
                if (distance < bestDistance) (best, bestDistance) = (slot, distance);
            }
            return best;
        }

        int CurrentSlot() => NearestSlot(snapAlignment);

        int NearestSlotOf(int index)
        {
            var here = CurrentSlot();
            var k = Mathf.RoundToInt((here - index) / (float)_count);
            return index + k * _count;
        }

        /// <summary>The list is scrolled to its end (or shows everything).</summary>
        public bool AtEnd => Offset >= MaxOffset - 1f;

        // ---------- item size changes (accordion) ----------

        /// <summary>Changes one item's size along the scroll axis, animated; the items after it move along.</summary>
        public void ResizeItem(int index, float size, float duration = 0.25f)
        {
            if (index < 0 || index >= _count) return;
            var from = SizeOf(index);
            if (duration <= 0f)
            {
                _sizeOverride[index] = size;
                Relayout();
                return;
            }
            UIAnimTimer.Start(duration, new Resize(this, index, from, size, duration), static (o, t) => ((Resize)o).At(t));
        }

        sealed class Resize
        {
            readonly UIAnimScrollList _list;
            readonly int _index;
            readonly float _from, _to, _duration;

            public Resize(UIAnimScrollList list, int index, float from, float to, float duration) =>
                (_list, _index, _from, _to, _duration) = (list, index, from, to, duration);

            public void At(float t)
            {
                if (_list == null) return;   // destroyed
                _list._sizeOverride[_index] = Mathf.LerpUnclamped(_from, _to, UIAnimEasing.OutCubic(t / _duration));
                _list.Relayout();
            }
        }

        /// <summary>The size of an index along the scroll axis.</summary>
        public float SizeOf(int index)
        {
            if (_sizeOverride.TryGetValue(index, out var size)) return size;
            if (sizeOf != null) return sizeOf(index);
            if (measureItems && index < _measured.Count) return _measured[index];
            if (layout == LayoutType.Grid) return MainOf(cellSize);
            if (layout == LayoutType.Masonry) return MainOf(cellSize);
            var prefab = Prefab(PrefabOf(index));
            return prefab != null ? MainOf(prefab.rect.size) : 0f;
        }

        // ---------- animation ----------

        /// <summary>Plays the entry state on the visible items, one after another (entry stagger apart).</summary>
        public void PlayEntry()
        {
            _entryMotion.TryCancel();
            if (string.IsNullOrEmpty(entryState)) return;
            _entryFirst = int.MaxValue;
            foreach (var item in _active.Values)
            {
                _entryFirst = Mathf.Min(_entryFirst, item.slot);
                item.entered = false;
                item.anim?.Sample(entryState, 0f);   // waiting in the start pose
            }
            var span = (_visible.Count - 1) * entryStagger;
            if (span <= 0f)
            {
                FinishEntry();   // one item, or no stagger: all at once
                return;
            }
            _entryMotion = UIAnimTimer.Start(span, this,
                static (o, t) => ((UIAnimScrollList)o).EntryAt(t),
                static o => ((UIAnimScrollList)o).FinishEntry());
            EntryAt(0f);
        }

        internal void EntryAt(float elapsed)
        {
            foreach (var item in _active.Values)
            {
                if (item.entered || Mathf.Max(0, item.slot - _entryFirst) * entryStagger > elapsed + 1e-4f) continue;
                item.entered = true;
                item.anim?.Play(entryState);
            }
        }

        void FinishEntry() => EntryAt(float.MaxValue);

        bool EntryRunning => _entryMotion.IsActive();

        /// <summary>Edit-mode preview of the entry: each visible item's entry state at <paramref name="elapsed"/>.</summary>
        internal void SampleEntry(float elapsed)
        {
            if (string.IsNullOrEmpty(entryState)) return;
            var first = int.MaxValue;
            foreach (var item in _active.Values) first = Mathf.Min(first, item.slot);
            foreach (var item in _active.Values)
                item.anim?.Sample(entryState, elapsed - (item.slot - first) * entryStagger);
        }

        void Shown(Item item, bool scrolledIn)
        {
            item.entered = !EntryRunning;
            if (item.anim != null)
            {
                if (EntryRunning) item.anim.Sample(entryState, 0f);   // its turn comes in EntryAt
                else
                {
                    // A reused object may be mid-animation from its last index: rest it in the entered pose first.
                    if (!string.IsNullOrEmpty(entryState)) item.anim.Sample(entryState, float.MaxValue);
                    if (scrolledIn && !string.IsNullOrEmpty(appearState)) item.anim.Play(appearState);
                }
            }
            ItemShown?.Invoke(item.index, item.rect);
        }

        void Scrub()
        {
            var viewport = ViewportMain;
            if (viewport <= 0f) return;
            foreach (var item in _active.Values)
            {
                if (item.anim == null) continue;
                var center = MainStart(item.slot) + SizeOf(item.index) * 0.5f - Offset;
                item.anim.Sample(scrubState, Mathf.Clamp01(center / viewport) * item.anim.Duration(scrubState));
            }
        }

        // ---------- dragging: snapping, pages, nested scroll views ----------

        public void OnBeginDrag(PointerEventData eventData)
        {
            _scrollMotion.TryCancel();
            _dragging = true;
            _dragStartSlot = CurrentSlot();
            // A drag across this list's axis belongs to an outer scroll view (e.g. a vertical list inside horizontal pages).
            var delta = eventData.position - eventData.pressPosition;
            var across = VerticalScroll ? Mathf.Abs(delta.x) > Mathf.Abs(delta.y) : Mathf.Abs(delta.y) > Mathf.Abs(delta.x);
            if (!across) return;
            if (_parentScroll == null && transform.parent != null) _parentScroll = transform.parent.GetComponentInParent<ScrollRect>();
            if (_parentScroll == null) return;
            _routingToParent = true;
            Scroll.enabled = false;   // this view stops following the pointer for this drag
            ExecuteEvents.Execute(_parentScroll.gameObject, eventData, ExecuteEvents.beginDragHandler);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_routingToParent) ExecuteEvents.Execute(_parentScroll.gameObject, eventData, ExecuteEvents.dragHandler);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
            if (_routingToParent)
            {
                _routingToParent = false;
                Scroll.enabled = true;
                ExecuteEvents.Execute(_parentScroll.gameObject, eventData, ExecuteEvents.endDragHandler);
                return;
            }
            if (_count == 0 || snap == SnapMode.None) return;
            var slot = CurrentSlot();
            if (snap == SnapMode.OnePerSwipe)
            {
                var delta = eventData.position - eventData.pressPosition;
                var along = VerticalScroll ? delta.y : -delta.x;   // positive = towards later items
                slot = Mathf.Abs(along) < 30f ? _dragStartSlot : _dragStartSlot + (along > 0f ? 1 : -1);
            }
            if (!loop) slot = Mathf.Clamp(slot, 0, _count - 1);
            ScrollToSlot(slot, snapAlignment, snapDuration);
        }

        // ---------- layout (content space: x right, y down from the content's top-left) ----------

        bool VerticalScroll => layout != LayoutType.Horizontal && !(layout == LayoutType.Grid && constraint == GridConstraint.FixedRowCount);
        bool Linear => layout is LayoutType.Vertical or LayoutType.Horizontal;
        bool Looping => loop && Linear && _count > 0;
        float MainOf(Vector2 v) => VerticalScroll ? v.y : v.x;
        float CrossOf(Vector2 v) => VerticalScroll ? v.x : v.y;
        float PadStart => VerticalScroll ? padding.top : padding.left;
        float PadEnd => VerticalScroll ? padding.bottom : padding.right;
        float CrossPadStart => VerticalScroll ? padding.left : padding.top;
        float CrossPadEnd => VerticalScroll ? padding.right : padding.bottom;
        float MainSpacing => Linear ? spacing : MainOf(gridSpacing);
        float CrossSpacing => Linear ? 0f : CrossOf(gridSpacing);
        float ViewportMain => MainOf(Viewport.rect.size);
        float ViewportCross => CrossOf(Viewport.rect.size);

        int PrefabOf(int index) => prefabOf == null ? 0 : Mathf.Clamp(prefabOf(index), 0, extraPrefabs.Count);
        RectTransform Prefab(int type) => type == 0 ? itemPrefab : extraPrefabs[type - 1];

        /// <summary>Size of prefab 0's items (Vertical/Horizontal), or the cell (Grid).</summary>
        public Vector2 ItemSize => SizeOfItem(0);

        Vector2 SizeOfItem(int index)
        {
            if (layout == LayoutType.Grid) return cellSize;
            var main = SizeOf(index);
            float cross;
            if (layout == LayoutType.Masonry) cross = CrossOf(cellSize);
            else if (controlChildSize) cross = ViewportCross - CrossPadStart - CrossPadEnd;
            else
            {
                var prefab = Prefab(PrefabOf(index));
                cross = prefab != null ? CrossOf(prefab.rect.size) : 0f;
            }
            return VerticalScroll ? new Vector2(cross, main) : new Vector2(main, cross);
        }

        int CrossCount
        {
            get
            {
                if (Linear) return 1;
                if (constraint != GridConstraint.Flexible) return Mathf.Max(1, constraintCount);
                var available = ViewportCross - CrossPadStart - CrossPadEnd;
                return Mathf.Max(1, Mathf.FloorToInt((available + CrossSpacing) / (CrossOf(cellSize) + CrossSpacing)));
            }
        }

        // Alignment factors from the anchor: 0 = start, 0.5 = middle, 1 = end of each axis.
        float AlignFactor(bool horizontal) => horizontal ? (int)childAlignment % 3 * 0.5f : (int)childAlignment / 3 * 0.5f;

        /// <summary>Top-left of an item in content space (x right, y down from the content's top-left).</summary>
        public Vector2 ItemPosition(int index) => index >= 0 && index < _rects.Count ? _rects[index].position : Vector2.zero;

        // Every item's rect, once per layout change.
        void ComputeRects()
        {
            _rects.Clear();
            var mains = new float[0];
            var cross = CrossCount;
            var crossAvailable = ViewportCross - CrossPadStart - CrossPadEnd;
            if (layout == LayoutType.Masonry) mains = new float[cross];   // ponytail: one small array per relayout
            for (var c = 0; c < mains.Length; c++) mains[c] = PadStart;

            var main = PadStart;
            for (var i = 0; i < _count; i++)
            {
                var size = SizeOfItem(i);
                float m, c;
                switch (layout)
                {
                    case LayoutType.Grid:
                    {
                        var line = i / cross;
                        var column = i % cross;
                        var used = cross * CrossOf(size) + (cross - 1) * CrossSpacing;
                        m = PadStart + line * (MainOf(size) + MainSpacing);
                        c = CrossPadStart + Mathf.Max(0f, crossAvailable - used) * AlignFactor(VerticalScroll) + column * (CrossOf(size) + CrossSpacing);
                        break;
                    }
                    case LayoutType.Masonry:
                    {
                        var column = 0;
                        for (var k = 1; k < cross; k++) if (mains[k] < mains[column]) column = k;   // the shortest column
                        var used = cross * CrossOf(size) + (cross - 1) * CrossSpacing;
                        m = mains[column];
                        c = CrossPadStart + Mathf.Max(0f, crossAvailable - used) * AlignFactor(VerticalScroll) + column * (CrossOf(size) + CrossSpacing);
                        mains[column] += MainOf(size) + MainSpacing;
                        break;
                    }
                    default:
                        m = main;
                        c = CrossPadStart + Mathf.Max(0f, crossAvailable - CrossOf(size)) * AlignFactor(VerticalScroll);
                        main += MainOf(size) + MainSpacing;
                        break;
                }
                _rects.Add(VerticalScroll ? new Rect(c, m, size.x, size.y) : new Rect(m, c, size.x, size.y));
            }

            float end;
            if (_count == 0) end = PadStart;
            else if (layout == LayoutType.Masonry)
            {
                end = PadStart;
                foreach (var columnEnd in mains) end = Mathf.Max(end, columnEnd - MainSpacing);
            }
            else end = MainOf(_rects[_count - 1].position) + MainOf(_rects[_count - 1].size);
            _contentMain = end + PadEnd;
            _cycle = _count == 0 ? 0f : end - PadStart + MainSpacing;

            if (reverseArrangement && Linear)   // mirror inside the padding: the last item first
                for (var i = 0; i < _count; i++)
                {
                    var r = _rects[i];
                    var mirrored = _contentMain - PadEnd - (MainOf(r.position) - PadStart) - MainOf(r.size);
                    _rects[i] = VerticalScroll ? new Rect(r.x, mirrored, r.width, r.height) : new Rect(mirrored, r.y, r.width, r.height);
                }

            // Content shorter than the viewport: child alignment places the items along the scroll axis too.
            var slack = Looping ? 0f : Mathf.Max(0f, ViewportMain - _contentMain) * AlignFactor(!VerticalScroll);
            if (slack > 0f)
                for (var i = 0; i < _count; i++)
                {
                    var r = _rects[i];
                    _rects[i] = VerticalScroll ? new Rect(r.x, r.y + slack, r.width, r.height) : new Rect(r.x + slack, r.y, r.width, r.height);
                }
        }

        // With Measure Items: each index bound to a hidden instance of its prefab, its preferred size read once.
        // Indices from `from` to `to` (exclusive) are measured again; sizes outside stay. -1 = up to the count.
        void Measure(int from = 0, int to = -1)
        {
            if (!measureItems || _count == 0)
            {
                _measured.Clear();
                return;
            }
            if (to < 0) to = _count;
            if (_measured.Count > _count) _measured.RemoveRange(_count, _measured.Count - _count);
            while (_measured.Count < _count) _measured.Add(0f);
            if (from >= to) return;
            var probes = new Dictionary<int, Item>();
            for (var i = from; i < to; i++)
            {
                var type = PrefabOf(i);
                if (!probes.TryGetValue(type, out var probe))
                {
                    var pool = Pool(type);
                    probes[type] = probe = pool.Count > 0 ? pool.Pop() : Create(type);   // a pooled object, not a new one each time
                }
                probe.index = i;
                probe.rect.gameObject.SetActive(true);
                var cross = controlChildSize ? ViewportCross - CrossPadStart - CrossPadEnd : CrossOf(Prefab(type).rect.size);
                probe.rect.sizeDelta = VerticalScroll ? new Vector2(cross, probe.rect.sizeDelta.y) : new Vector2(probe.rect.sizeDelta.x, cross);
                _bind?.Invoke(probe);
                LayoutRebuilder.ForceRebuildLayoutImmediate(probe.rect);
                _measured[i] = LayoutUtility.GetPreferredSize(probe.rect, VerticalScroll ? 1 : 0);
                _unbind?.Invoke(probe);
            }
            foreach (var probe in probes.Values)
            {
                probe.rect.gameObject.SetActive(false);
                Pool(probe.prefab).Push(probe);
            }
        }

        int Wrap(int slot) => _count == 0 ? 0 : ((slot % _count) + _count) % _count;

        // A slot's start along the scroll axis: loops repeat the items every cycle.
        float MainStart(int slot)
        {
            var index = Wrap(slot);
            var cycles = Looping ? (slot - index) / _count : 0;
            return MainOf(_rects[index].position) + cycles * _cycle;
        }

        /// <summary>How far the list is scrolled along its axis, in pixels from the start.</summary>
        public float Offset => Content == null ? 0f : VerticalScroll ? Content.anchoredPosition.y : -Content.anchoredPosition.x;

        float MaxOffset => Mathf.Max(0f, _contentMain - ViewportMain);

        float OffsetFor(int slot, Alignment alignment)
        {
            if (_count == 0) return 0f;
            if (!Looping) slot = Mathf.Clamp(slot, 0, _count - 1);
            var factor = alignment == Alignment.Start ? 0f : alignment == Alignment.Center ? 0.5f : 1f;
            return MainStart(slot) - (ViewportMain - SizeOf(Wrap(slot))) * factor;
        }

        internal void SetOffset(float offset, bool clamp = true)
        {
            if (Content == null) return;
            if (clamp && !Looping) offset = Mathf.Clamp(offset, 0f, MaxOffset);
            Content.anchoredPosition = VerticalScroll ? new Vector2(Content.anchoredPosition.x, offset) : new Vector2(-offset, Content.anchoredPosition.y);
            UpdateVisible();
        }

        // ---------- recycling ----------

        /// <summary>Lays the content out again (after changing layout settings or the viewport size) and refreshes what is visible.</summary>
        public void Rebuild() => Relayout();

        void Relayout()
        {
            if (Content == null) return;
            ComputeRects();
            Scroll.vertical = VerticalScroll;
            Scroll.horizontal = !VerticalScroll;
            if (Looping) Scroll.movementType = ScrollRect.MovementType.Unrestricted;
            Content.pivot = new Vector2(0f, 1f);
            if (VerticalScroll)
            {
                Content.anchorMin = new Vector2(0f, 1f);
                Content.anchorMax = new Vector2(1f, 1f);
                Content.sizeDelta = new Vector2(0f, _contentMain);
            }
            else
            {
                Content.anchorMin = new Vector2(0f, 0f);
                Content.anchorMax = new Vector2(0f, 1f);
                Content.sizeDelta = new Vector2(_contentMain, 0f);
            }
            foreach (var item in _active.Values) Place(item);
            SetOffset(Offset);
        }

        internal void UpdateVisible()
        {
            if (Content == null) return;
            if (Looping && _cycle > 0f && Mathf.Abs(Offset) > 100f * _cycle && !_scrollMotion.IsActive()) Recenter();

            CollectVisible();
            _release.Clear();
            foreach (var slot in _active.Keys)
                if (!_visibleSet.Contains(slot)) _release.Add(slot);
            foreach (var slot in _release) Release(slot);
            var first = int.MaxValue;
            var last = int.MinValue;
            foreach (var slot in _visible)
            {
                if (!_active.ContainsKey(slot)) Acquire(slot);
                var index = Wrap(slot);
                first = Mathf.Min(first, index);
                last = Mathf.Max(last, index);
            }
            VisibleRange = _visible.Count == 0 ? (0, -1) : (first, last);
            if (!string.IsNullOrEmpty(scrubState)) Scrub();
            UpdateStickyHeader();

            var current = _visible.Count == 0 ? -1 : NearestIndex(snapAlignment);
            if (current != _current)
            {
                _current = current;
                if (current >= 0) CurrentIndexChanged?.Invoke(current);
            }
        }

        // Slots whose rect overlaps the viewport. Linear layouts: a binary search over the index order; Masonry: a scan.
        void CollectVisible()
        {
            _visible.Clear();
            _visibleSet.Clear();
            if (_count == 0 || ViewportMain <= 0f) return;
            var from = Offset;
            var to = from + ViewportMain;
            if (Looping)
            {
                var firstCycle = Mathf.FloorToInt((from - PadStart) / _cycle) - 1;
                var lastCycle = Mathf.FloorToInt((to - PadStart) / _cycle) + 1;
                for (var k = firstCycle; k <= lastCycle; k++) AddOverlapping(from - k * _cycle, to - k * _cycle, k * _count);
            }
            else AddOverlapping(from, to, 0);
        }

        void AddOverlapping(float from, float to, int slotBase)
        {
            if (layout == LayoutType.Masonry)
            {
                for (var i = 0; i < _count; i++) Overlap(i);
                return;
            }
            // Items in index order have non-decreasing starts (reverse: non-increasing); find the first that ends after `from`.
            var reverse = reverseArrangement && Linear;
            int Ordered(int o) => reverse ? _count - 1 - o : o;
            int lo = 0, hi = _count;
            while (lo < hi)
            {
                var mid = (lo + hi) / 2;
                var r = _rects[Ordered(mid)];
                if (MainOf(r.position) + MainOf(r.size) <= from) lo = mid + 1;
                else hi = mid;
            }
            for (var o = lo; o < _count; o++)
            {
                var i = Ordered(o);
                if (MainOf(_rects[i].position) >= to) break;
                Overlap(i);
            }

            void Overlap(int i)
            {
                var r = _rects[i];
                var start = MainOf(r.position);
                if (start >= to || start + MainOf(r.size) <= from) return;
                var slot = slotBase + i;
                if (_visibleSet.Add(slot)) _visible.Add(slot);
            }
        }

        // Far into a loop: move everything back by whole cycles so positions stay small.
        void Recenter()
        {
            var cycles = Mathf.RoundToInt(Offset / _cycle);
            var shift = cycles * _cycle;
            _release.Clear();
            _release.AddRange(_active.Keys);
            var moved = new List<Item>(_active.Count);
            foreach (var slot in _release)
            {
                var item = _active[slot];
                _active.Remove(slot);
                item.slot -= cycles * _count;
                moved.Add(item);
            }
            foreach (var item in moved) _active[item.slot] = item;
            var offset = Offset - shift;
            Content.anchoredPosition = VerticalScroll ? new Vector2(Content.anchoredPosition.x, offset) : new Vector2(-offset, Content.anchoredPosition.y);
            foreach (var item in _active.Values) Place(item);
        }

        void Acquire(int slot)
        {
            var index = Wrap(slot);
            var type = PrefabOf(index);
            var pool = Pool(type);
            var item = pool.Count > 0 ? pool.Pop() : Create(type);
            item.index = index;
            item.slot = slot;
            _active[slot] = item;
            item.rect.gameObject.SetActive(true);
            Place(item);
            _bind?.Invoke(item);
            Shown(item, scrolledIn: _filled);
        }

        void Release(int slot)
        {
            var item = _active[slot];
            _active.Remove(slot);
            ItemHidden?.Invoke(item.index, item.rect);
            _unbind?.Invoke(item);
            item.rect.gameObject.SetActive(false);
            Pool(item.prefab).Push(item);
        }

        void ReleaseAll()
        {
            _release.Clear();
            _release.AddRange(_active.Keys);
            foreach (var slot in _release) Release(slot);
        }

        Stack<Item> Pool(int type)
        {
            while (_pools.Count <= type) _pools.Add(new Stack<Item>());
            return _pools[type];
        }

        Item Create(int type)
        {
            var rect = Instantiate(Prefab(type), Content, false);
            if (_preview) rect.gameObject.hideFlags = HideFlags.DontSave;
            return new Item { rect = rect, anim = rect.GetComponent<UIAnimation>(), prefab = type };
        }

        void Place(Item item)
        {
            var rect = item.rect;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            var r = _rects[item.index];
            rect.sizeDelta = r.size;
            var position = r.position;
            if (VerticalScroll) position.y = MainStart(item.slot);
            else position.x = MainStart(item.slot);
            rect.anchoredPosition = new Vector2(position.x, -position.y);
        }

        // ---------- sticky section headers ----------

        void UpdateStickyHeader()
        {
            if (stickyHeaderPrefab < 0 || Looping || !Linear || _count == 0)
            {
                if (_stickyHeader != null) _stickyHeader.rect.gameObject.SetActive(false);
                return;
            }
            // The last header whose start is above the viewport's start edge, and the header after it (which pushes it up).
            int header = -1, next = -1;
            for (var i = 0; i < _count; i++)   // ponytail: linear scan; keep a header index list if lists get huge
            {
                if (PrefabOf(i) != stickyHeaderPrefab) continue;
                if (MainStart(i) <= Offset) header = i;
                else
                {
                    next = i;
                    break;
                }
            }
            if (header < 0 || MainStart(header) >= Offset)
            {
                if (_stickyHeader != null) _stickyHeader.rect.gameObject.SetActive(false);
                return;
            }
            if (_stickyHeader == null)
            {
                _stickyHeader = Create(stickyHeaderPrefab);
                _stickyHeader.rect.SetParent(Viewport, false);
                _stickyHeader.index = -1;
            }
            var rect = _stickyHeader.rect;
            rect.gameObject.SetActive(true);
            if (_stickyHeader.index != header)
            {
                _stickyHeader.index = header;
                _stickyHeader.slot = header;
                _bind?.Invoke(_stickyHeader);
            }
            var size = SizeOf(header);
            var main = next >= 0 ? Mathf.Min(0f, MainStart(next) - Offset - size) : 0f;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = _rects[header].size;
            var cross = CrossOf(_rects[header].position);
            rect.anchoredPosition = VerticalScroll ? new Vector2(cross, -main) : new Vector2(main, -cross);
            rect.SetAsLastSibling();
        }

        // ---------- edit-mode preview (used by the inspector) ----------

        internal bool Previewing => _preview;

        internal void BeginPreview(int count)
        {
            _preview = true;
            SetCount<RectTransform>(count, null);
        }

        internal void EndPreview()
        {
            if (!_preview) return;
            _entryMotion.TryCancel();
            _scrollMotion.TryCancel();
            ReleaseAll();
            foreach (var pool in _pools)
                while (pool.Count > 0) DestroyImmediate(pool.Pop().rect.gameObject);
            if (_stickyHeader != null) DestroyImmediate(_stickyHeader.rect.gameObject);
            _stickyHeader = null;
            _count = 0;
            _rects.Clear();
            _preview = false;
        }

        /// <summary>Scroll position as 0 (start) … 1 (end); a loop maps it to one cycle.</summary>
        internal float NormalizedOffset
        {
            get => Looping ? (_cycle > 0f ? Offset / _cycle : 0f) : MaxOffset > 0f ? Offset / MaxOffset : 0f;
            set => SetOffset(Looping ? value * _cycle : value * MaxOffset);
        }
    }
}
