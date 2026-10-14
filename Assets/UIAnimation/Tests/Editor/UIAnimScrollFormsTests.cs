using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>The scroll forms beyond a plain list: sizes, prefab types, masonry, loop, pages, accordion, chat, sticky headers, nesting, helpers.</summary>
    public sealed class UIAnimScrollFormsTests
    {
        readonly List<Object> _destroy = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _destroy) Object.DestroyImmediate(o);
            _destroy.Clear();
        }

        // A 300×500 list (viewport = list) with 300×100 items, no canvas.
        UIAnimScrollList List(Transform parent = null, float width = 300, float height = 500)
        {
            var root = new GameObject("List", typeof(RectTransform));
            if (parent != null) root.transform.SetParent(parent, false);
            else _destroy.Add(root);
            ((RectTransform)root.transform).sizeDelta = new Vector2(width, height);
            var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
            viewport.SetParent(root.transform, false);
            viewport.sizeDelta = new Vector2(width, height);
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            var scroll = root.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.inertia = false;
            var list = root.AddComponent<UIAnimScrollList>();
            list.itemPrefab = Template("Item", 300, 100);
            list.playEntryOnFill = false;
            return list;
        }

        RectTransform Template(string name, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            ((RectTransform)go.transform).sizeDelta = new Vector2(width, height);
            go.SetActive(false);
            _destroy.Add(go);
            return (RectTransform)go.transform;
        }

        static void Fill(UIAnimScrollList list, int count) => list.SetCount(count, (RectTransform view, int i) => view.name = $"{view.name.Split('(', ' ')[0]} {i}");

        static PointerEventData Drag(Vector2 press, Vector2 position) => new(null) { pressPosition = press, position = position };

        static void Call(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, null);

        // ---------- 크기와 종류 ----------

        [Test]
        public void 아이템마다_크기가_달라도_이어서_배치된다()
        {
            var list = List();
            list.spacing = 10;
            list.sizeOf = i => 50 + i * 10;
            Fill(list, 20);
            Assert.AreEqual(0, list.ItemPosition(0).y, 0.01f);
            Assert.AreEqual(60, list.ItemPosition(1).y, 0.01f, "50 + 간격 10");
            Assert.AreEqual(130, list.ItemPosition(2).y, 0.01f, "60 + 10 더");
            Assert.AreEqual(70, list.GetItem(2).rect.height, 0.01f, "실제 오브젝트 높이");

            list.ScrollTo(15, UIAnimScrollList.Alignment.Start);
            Assert.AreEqual(list.ItemPosition(15).y, list.Offset, 0.01f);
            Assert.AreEqual(15, list.VisibleRange.first);
        }

        [Test]
        public void 인덱스마다_다른_Prefab을_쓰고_종류별로_재사용한다()
        {
            var list = List();
            list.extraPrefabs.Add(Template("Header", 300, 40));
            list.prefabOf = i => i % 5 == 0 ? 1 : 0;
            Fill(list, 50);
            StringAssert.StartsWith("Header", list.GetItem(0).name);
            StringAssert.StartsWith("Item", list.GetItem(1).name);
            Assert.AreEqual(40, list.GetItem(0).rect.height, 0.01f, "헤더 Prefab 크기");
            Assert.AreEqual(140, list.ItemPosition(2).y, 0.01f, "40 + 100");

            list.ScrollTo(25, UIAnimScrollList.Alignment.Start);
            StringAssert.StartsWith("Header", list.GetItem(25).name, "재사용돼도 종류가 맞음");
            StringAssert.StartsWith("Item", list.GetItem(26).name);
        }

        [Test]
        public void 측정_모드는_레이아웃의_선호_크기를_쓴다()
        {
            var list = List();
            list.itemPrefab.gameObject.AddComponent<LayoutElement>();
            list.measureItems = true;
            list.SetCount(10, (RectTransform view, int i) => view.GetComponent<LayoutElement>().preferredHeight = 40 + i * 20);
            Assert.AreEqual(40, list.SizeOf(0), 0.01f);
            Assert.AreEqual(120, list.SizeOf(4), 0.01f);
            Assert.AreEqual(40 + 60 + 80, list.ItemPosition(3).y, 0.01f);
        }

        [Test]
        public void 메이슨리는_가장_짧은_열에_쌓는다()
        {
            var list = List();
            list.layout = UIAnimScrollList.LayoutType.Masonry;
            list.constraint = UIAnimScrollList.GridConstraint.FixedColumnCount;
            list.constraintCount = 3;
            list.cellSize = new Vector2(100, 100);
            float[] heights = { 200, 100, 150, 50, 300 };
            list.sizeOf = i => heights[i];
            Fill(list, 5);
            Assert.AreEqual(new Vector2(0, 0), list.ItemPosition(0));
            Assert.AreEqual(new Vector2(100, 0), list.ItemPosition(1));
            Assert.AreEqual(new Vector2(200, 0), list.ItemPosition(2));
            Assert.AreEqual(new Vector2(100, 100), list.ItemPosition(3), "가장 짧은 열 = 2열(100)");
            Assert.AreEqual(new Vector2(100, 150), list.ItemPosition(4), "그다음 = 2열(150)");
        }

        // ---------- 무한 루프, 페이지 ----------

        [Test]
        public void 루프는_끝에서_처음_아이템으로_이어진다()
        {
            var list = List();
            list.loop = true;
            Fill(list, 10);   // 1000 px cycle
            list.SetOffset(950);
            Assert.AreEqual("Item 9", list.GetItem(9).name);
            Assert.IsNotNull(list.GetItem(0), "끝 뒤에 0번이 이어서 보임");
            Assert.AreEqual(-1000, list.GetItem(0).anchoredPosition.y, 0.01f, "다음 바퀴 자리");

            list.SetOffset(-150);
            Assert.IsNotNull(list.GetItem(8), "앞쪽으로도 이어짐");
            Assert.AreEqual(ScrollRect.MovementType.Unrestricted, list.GetComponent<ScrollRect>().movementType);

            list.ScrollTo(3);
            Assert.AreEqual(3, list.NearestIndex(UIAnimScrollList.Alignment.Start));
        }

        [Test]
        public void 한_장씩_넘기기는_드래그_한_번에_한_장만_이동한다()
        {
            var list = List();
            list.layout = UIAnimScrollList.LayoutType.Horizontal;
            list.itemPrefab = Template("Page", 300, 500);
            list.snap = UIAnimScrollList.SnapMode.OnePerSwipe;
            list.snapAlignment = UIAnimScrollList.Alignment.Start;
            list.snapDuration = 0f;
            var changes = new List<int>();
            list.CurrentIndexChanged += changes.Add;
            Fill(list, 5);

            // A long fast swipe to the left still moves one page.
            list.OnBeginDrag(Drag(Vector2.zero, new Vector2(-20, 0)));
            list.SetOffset(700);
            list.OnEndDrag(Drag(Vector2.zero, new Vector2(-700, 0)));
            Assert.AreEqual(300, list.Offset, 0.01f, "1페이지");
            Assert.AreEqual(1, list.CurrentIndex);

            // A tiny drag goes back to the page it started on.
            list.OnBeginDrag(Drag(Vector2.zero, new Vector2(-20, 0)));
            list.SetOffset(320);
            list.OnEndDrag(Drag(Vector2.zero, new Vector2(-20, 0)));
            Assert.AreEqual(300, list.Offset, 0.01f);

            CollectionAssert.Contains(changes, 0);
            CollectionAssert.Contains(changes, 1);
        }

        // ---------- 아코디언, 채팅, 섹션 헤더 ----------

        [Test]
        public void 아이템_크기를_바꾸면_뒤_아이템이_밀린다()
        {
            var list = List();
            Fill(list, 10);
            list.ResizeItem(1, 250, 0f);
            Assert.AreEqual(250, list.GetItem(1).rect.height, 0.01f);
            Assert.AreEqual(350, list.ItemPosition(2).y, 0.01f);
            Assert.AreEqual(-350, list.GetItem(2).anchoredPosition.y, 0.01f, "보이는 오브젝트도 이동");
        }

        [Test]
        public void 끝에_붙기는_추가돼도_끝을_유지하고_적으면_아래부터_쌓는다()
        {
            var list = List();
            list.stickToEnd = true;
            list.childAlignment = TextAnchor.LowerLeft;
            Fill(list, 2);
            Assert.AreEqual(300, list.ItemPosition(0).y, 0.01f, "2개는 아래쪽에 (500 - 200)");

            Fill(list, 10);
            Assert.IsTrue(list.AtEnd, "채우면 끝에서 시작");
            list.SetCount(12, 0f);
            Assert.IsTrue(list.AtEnd, "새 메시지가 와도 끝");
            Assert.IsNotNull(list.GetItem(11));

            list.SetOffset(0);
            list.SetCount(13, 0f);
            Assert.AreEqual(0, list.Offset, 0.01f, "위를 보고 있으면 그대로");
        }

        [Test]
        public void 섹션_헤더는_위에_고정되고_다음_헤더가_밀어올린다()
        {
            var list = List();
            var header = Template("Header", 300, 50);
            list.extraPrefabs.Add(header);
            list.prefabOf = i => i % 4 == 0 ? 1 : 0;   // headers at 0, 4, 8 …; section = 50 + 300
            list.stickyHeaderPrefab = 1;
            Fill(list, 20);
            var viewport = list.GetComponent<ScrollRect>().viewport;

            list.SetOffset(100);
            Assert.AreEqual(2, viewport.childCount, "콘텐츠 + 고정 헤더");
            var sticky = (RectTransform)viewport.GetChild(1);
            Assert.IsTrue(sticky.gameObject.activeSelf);
            Assert.AreEqual("Header 0", sticky.name, "첫 섹션 헤더로 바인딩");
            Assert.AreEqual(0, sticky.anchoredPosition.y, 0.01f, "맨 위");

            list.SetOffset(320);   // next header at 350 → 30 px below the top: pushes 20 px up
            Assert.AreEqual(20, sticky.anchoredPosition.y, 0.01f);

            list.SetOffset(0);
            Assert.IsFalse(sticky.gameObject.activeSelf, "진짜 헤더가 보이면 숨김");
        }

        // ---------- 중첩 ----------

        [Test]
        public void 가로_드래그는_세로_리스트를_감싼_스크롤로_넘어간다()
        {
            var outer = new GameObject("Outer", typeof(RectTransform));
            _destroy.Add(outer);
            ((RectTransform)outer.transform).sizeDelta = new Vector2(300, 500);
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(outer.transform, false);
            content.sizeDelta = new Vector2(2000, 500);
            var outerScroll = outer.AddComponent<ScrollRect>();
            outerScroll.viewport = (RectTransform)outer.transform;
            outerScroll.content = content;
            outerScroll.vertical = false;
            outerScroll.inertia = false;
            outerScroll.movementType = ScrollRect.MovementType.Unrestricted;

            var inner = List(content);
            Fill(inner, 20);
            var innerScroll = inner.GetComponent<ScrollRect>();

            var start = content.anchoredPosition.x;
            inner.OnBeginDrag(Drag(Vector2.zero, new Vector2(-20, 2)));
            Assert.IsFalse(innerScroll.enabled, "안쪽은 이번 드래그 동안 멈춤");
            inner.OnDrag(Drag(Vector2.zero, new Vector2(-80, 2)));
            Assert.AreEqual(start - 60, content.anchoredPosition.x, 0.5f, "바깥이 따라감");
            inner.OnEndDrag(Drag(Vector2.zero, new Vector2(-80, 2)));
            Assert.IsTrue(innerScroll.enabled);
            Assert.AreEqual(0, inner.Offset, 0.01f, "안쪽은 그대로");
        }

        // ---------- 에디터 미리보기 ----------

        [Test]
        public void 미리보기는_저장되지_않는_아이템을_만들고_지우면_남지_않는다()
        {
            var list = List();
            var content = list.GetComponent<ScrollRect>().content;
            list.BeginPreview(30);
            Assert.Greater(content.childCount, 0);
            foreach (Transform item in content) Assert.AreEqual(HideFlags.DontSave, item.gameObject.hideFlags, "씬에 저장 안 됨");
            list.NormalizedOffset = 1f;
            Assert.IsTrue(list.AtEnd, "스크롤 슬라이더 끝 = 리스트 끝");
            Assert.IsNotNull(list.GetItem(29));
            list.EndPreview();
            Assert.AreEqual(0, content.childCount, "미리보기 오브젝트 정리");
            Assert.AreEqual(0, list.Count);
        }

        // ---------- 회전 속성 ----------

        [Test]
        public void Rotation은_음수_각도로_읽고_쓴다()
        {
            var go = new GameObject("R", typeof(RectTransform));
            _destroy.Add(go);
            UIAnimation.Write(go.transform, UIAnimProperty.Rotation, new Vector4(-30, 0, 45));
            var v = UIAnimation.Read(go.transform, UIAnimProperty.Rotation);
            Assert.AreEqual(-30, v.x, 0.01f, "330이 아니라 -30");
            Assert.AreEqual(45, v.z, 0.01f);
        }

        // ---------- 도우미 ----------

        static UIAnimation Animated(GameObject go, string state, Vector3 from, Vector3 to)
        {
            var anim = go.GetComponent<UIAnimation>();
            if (anim == null) anim = go.AddComponent<UIAnimation>();
            var track = new UIAnimTrack { target = go.transform, property = UIAnimProperty.Scale, start = UIAnimStart.KeyValue };
            track.keys.Add(new UIAnimKey { time = 0f, value = from });
            track.keys.Add(new UIAnimKey { time = 1f, value = to, ease = UIAnimEase.Linear });
            anim.states.Add(new UIAnimState { name = state, tracks = { track } });
            return anim;
        }

        [Test]
        public void 페이지_표시는_아이템마다_점을_만들고_현재_점에_On을_재생한다()
        {
            var list = List();
            list.layout = UIAnimScrollList.LayoutType.Horizontal;
            list.itemPrefab = Template("Page", 300, 500);
            var dot = Template("Dot", 20, 20);
            Animated(dot.gameObject, "On", Vector3.one, Vector3.one * 2);
            Animated(dot.gameObject, "Off", Vector3.one * 2, Vector3.one);
            dot.gameObject.SetActive(true);

            var indicator = new GameObject("Dots", typeof(RectTransform)).AddComponent<UIAnimPageIndicator>();
            _destroy.Add(indicator.gameObject);
            indicator.list = list;
            indicator.itemPrefab = dot;
            Call(indicator, "OnEnable");
            Fill(list, 4);

            Assert.AreEqual(4, indicator.transform.childCount, "점 4개");
            Assert.IsTrue(indicator.transform.GetChild(0).GetComponent<UIAnimation>().IsPlaying("On"));
            list.ScrollTo(2);
            Assert.IsTrue(indicator.transform.GetChild(2).GetComponent<UIAnimation>().IsPlaying("On"));
            Assert.IsTrue(indicator.transform.GetChild(0).GetComponent<UIAnimation>().IsPlaying("Off"));
        }

        [Test]
        public void 스크롤_스크럽은_스크롤한_거리로_상태를_보여준다()
        {
            var list = List();
            Fill(list, 30);
            var header = new GameObject("Header", typeof(RectTransform));
            _destroy.Add(header);
            var anim = Animated(header, "Collapse", Vector3.one, Vector3.one * 0.5f);
            var scrub = header.AddComponent<UIAnimScrollScrub>();
            scrub.list = list;
            scrub.target = anim;
            scrub.state = "Collapse";
            scrub.distance = 200;

            list.SetOffset(100);
            Call(scrub, "LateUpdate");
            Assert.AreEqual(0.75f, header.transform.localScale.x, 1e-3f, "반쯤 접힘");
            list.SetOffset(800);
            Call(scrub, "LateUpdate");
            Assert.AreEqual(0.5f, header.transform.localScale.x, 1e-3f, "끝까지");
        }

        [Test]
        public void 당겨서_새로고침은_거리를_넘겨_놓으면_이벤트를_낸다()
        {
            var list = List();
            Fill(list, 30);
            var spinner = new GameObject("Spinner", typeof(RectTransform));
            _destroy.Add(spinner);
            var anim = Animated(spinner, "Pull", Vector3.zero, Vector3.one);
            var pull = list.gameObject.AddComponent<UIAnimPullToRefresh>();
            pull.target = anim;
            pull.refreshingState = "";
            pull.distance = 100;
            Call(pull, "Awake");
            var refreshed = 0;
            pull.onRefresh.AddListener(() => refreshed++);

            var content = list.GetComponent<ScrollRect>().content;
            content.anchoredPosition = new Vector2(0, -50);   // pulled 50 (overscroll)
            Call(pull, "LateUpdate");
            Assert.AreEqual(0.5f, spinner.transform.localScale.x, 1e-3f, "당긴 만큼");
            pull.OnEndDrag(Drag(Vector2.zero, Vector2.zero));
            Assert.AreEqual(0, refreshed, "모자라면 안 함");

            content.anchoredPosition = new Vector2(0, -120);
            pull.OnEndDrag(Drag(Vector2.zero, Vector2.zero));
            Assert.AreEqual(1, refreshed);
            Assert.IsTrue(pull.Refreshing);
            pull.EndRefresh();
            Assert.IsFalse(pull.Refreshing);
        }
    }
}
