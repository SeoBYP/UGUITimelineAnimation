using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SeoBYP.TimelineAnimation.Tests
{
    public sealed class UIAnimScrollListTests
    {
        GameObject _root, _template;
        UIAnimScrollList _list;
        ScrollRect _scroll;
        readonly List<int> _bound = new(), _unbound = new();

        // ScrollRect 300×500 viewport; items 300×100 (no canvas: screen point = world point).
        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("List", typeof(RectTransform));
            ((RectTransform)_root.transform).sizeDelta = new Vector2(300, 500);
            var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
            viewport.SetParent(_root.transform, false);
            viewport.sizeDelta = new Vector2(300, 500);
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            _scroll = _root.AddComponent<ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = content;
            _scroll.inertia = false;

            _template = new GameObject("Item", typeof(RectTransform));
            ((RectTransform)_template.transform).sizeDelta = new Vector2(300, 100);
            _template.SetActive(false);

            _list = _root.AddComponent<UIAnimScrollList>();
            _list.itemPrefab = (RectTransform)_template.transform;
            _list.playEntryOnFill = false;
            _bound.Clear();
            _unbound.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_template);
        }

        void Fill(int count) => _list.SetCount<RectTransform>(count,
            (view, i) => { view.name = $"Item {i}"; _bound.Add(i); },
            (view, i) => _unbound.Add(i));

        int Instances => _scroll.content.childCount;

        static void Near(Vector2 expected, Vector2 actual, string message = null) =>
            Assert.Less(Vector2.Distance(expected, actual), 0.01f, $"{message} expected {expected} but was {actual}");

        // ---------- 정렬 ----------

        [Test]
        public void 세로_리스트는_패딩과_간격대로_배치된다()
        {
            _list.padding = new RectOffset(0, 0, 10, 20);
            _list.spacing = 4;
            Fill(10);
            Near(new Vector2(0, 10), _list.ItemPosition(0));
            Near(new Vector2(0, 10 + 2 * 104), _list.ItemPosition(2));
            Assert.AreEqual(10 + 10 * 104 - 4 + 20, _scroll.content.rect.height, 0.01f, "콘텐츠 높이 = 패딩 + 아이템·간격");
            Near(new Vector2(0, -(10 + 104)), ((RectTransform)_list.GetItem(1)).anchoredPosition, "실제 오브젝트 위치");
        }

        [Test]
        public void 가운데_정렬과_자식_크기_제어()
        {
            ((RectTransform)_template.transform).sizeDelta = new Vector2(200, 100);
            _list.childAlignment = TextAnchor.UpperCenter;
            Fill(3);
            Near(new Vector2(50, 0), _list.ItemPosition(0), "남는 폭 100의 절반");

            _list.padding = new RectOffset(20, 20, 0, 0);
            _list.controlChildSize = true;
            _list.Rebuild();
            Assert.AreEqual(260, _list.ItemSize.x, 0.01f, "뷰포트 폭 - 좌우 패딩");
            Assert.AreEqual(260, _list.GetItem(0).rect.width, 0.01f);
        }

        [Test]
        public void 반대_배치는_마지막_아이템이_맨_위다()
        {
            _list.reverseArrangement = true;
            Fill(10);
            Near(Vector2.zero, _list.ItemPosition(9));
            Near(new Vector2(0, 900), _list.ItemPosition(0));
            Assert.AreEqual((5, 9), _list.VisibleRange, "처음 보이는 건 마지막 쪽");
        }

        [Test]
        public void 그리드_Flexible은_뷰포트에_들어가는_만큼_열을_만든다()
        {
            _list.layout = UIAnimScrollList.LayoutType.Grid;
            _list.cellSize = new Vector2(90, 90);
            _list.gridSpacing = new Vector2(10, 10);
            Fill(20);
            Near(new Vector2(100, 100), _list.ItemPosition(4), "3열: 4번은 2행 2열");
            Assert.IsTrue(_scroll.vertical && !_scroll.horizontal);
        }

        [Test]
        public void 그리드_행_고정은_가로로_스크롤한다()
        {
            _list.layout = UIAnimScrollList.LayoutType.Grid;
            _list.constraint = UIAnimScrollList.GridConstraint.FixedRowCount;
            _list.constraintCount = 2;
            _list.cellSize = new Vector2(90, 90);
            _list.gridSpacing = new Vector2(10, 10);
            Fill(20);
            Near(new Vector2(100, 100), _list.ItemPosition(3), "2행: 3번은 2열 2행");
            Assert.IsTrue(_scroll.horizontal && !_scroll.vertical);
            Assert.AreEqual(10 * 100 - 10, _scroll.content.rect.width, 0.01f);
        }

        // ---------- 재사용과 바인딩 ----------

        [Test]
        public void 보이는_아이템만_만들고_스크롤하면_오브젝트를_재사용한다()
        {
            Fill(100);
            Assert.AreEqual((0, 4), _list.VisibleRange);
            Assert.AreEqual(5, Instances, "뷰포트 500 / 아이템 100 → 5개");

            _list.ScrollTo(50);
            Assert.AreEqual((50, 54), _list.VisibleRange);
            Assert.AreEqual(5, Instances, "새로 만들지 않고 재사용");
            Assert.AreEqual("Item 50", _list.GetItem(50).name);
            Assert.IsNull(_list.GetItem(0), "화면 밖은 없음");
        }

        [Test]
        public void 바인딩과_해제는_정확한_인덱스로_불린다()
        {
            Fill(100);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, _bound);
            _bound.Clear();
            _list.ScrollTo(1);   // 0 leaves, 5 arrives
            CollectionAssert.AreEqual(new[] { 0 }, _unbound);
            CollectionAssert.AreEqual(new[] { 5 }, _bound);

            _bound.Clear();
            _list.RefreshItem(3);
            CollectionAssert.AreEqual(new[] { 3 }, _bound);
            _bound.Clear();
            _list.Refresh();
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4, 5 }, _bound);
        }

        [Test]
        public void 데이터_목록으로_바인딩한다()
        {
            var names = new List<string> { "A", "B", "C" };
            _list.SetData<string, RectTransform>(names, (view, name, i) => view.name = $"{i}:{name}");
            Assert.AreEqual("1:B", _list.GetItem(1).name);
            names[1] = "Z";
            _list.Refresh();
            Assert.AreEqual("1:Z", _list.GetItem(1).name);
        }

        [Test]
        public void 개수를_줄이면_스크롤이_범위_안으로_들어온다()
        {
            Fill(100);
            _list.ScrollTo(90);
            _list.SetCount(10);
            Assert.AreEqual(500, _list.Offset, 0.01f, "콘텐츠 1000 - 뷰포트 500");
            Assert.AreEqual((5, 9), _list.VisibleRange);
            Assert.AreEqual("Item 9", _list.GetItem(9).name);
        }

        // ---------- 특정 위치 아이템 ----------

        [Test]
        public void ScrollTo는_시작_가운데_끝에_맞추고_끝에서는_멈춘다()
        {
            Fill(100);
            _list.ScrollTo(50, UIAnimScrollList.Alignment.Center);
            Assert.AreEqual(5000 - 200, _list.Offset, 0.01f, "아이템 가운데 = 뷰포트 가운데");
            _list.ScrollTo(50, UIAnimScrollList.Alignment.End);
            Assert.AreEqual(5000 - 400, _list.Offset, 0.01f);
            _list.ScrollTo(99);
            Assert.AreEqual(10000 - 500, _list.Offset, 0.01f, "더 내려갈 수 없음");
            Assert.AreEqual(95, _list.NearestIndex(UIAnimScrollList.Alignment.Start), "지금 맨 위에 가장 가까운 아이템");
        }

        [Test]
        public void 화면_좌표로_아이템을_찾는다()
        {
            Fill(100);
            _list.ScrollTo(20);
            var item = _list.GetItem(22);
            Vector2 screen = item.TransformPoint(item.rect.center);
            Assert.AreEqual(22, _list.IndexAt(screen));
        }

        // ---------- 애니메이션 ----------

        // Item states: Show (scale 0 → 1, 0.2s), Appear (alpha 0 → 1, 0.2s), Focus (scale 1 → 2 → 1 over 1s).
        void AnimatedItem()
        {
            var image = _template.AddComponent<Image>();
            var anim = _template.AddComponent<UIAnimation>();
            UIAnimTrack Track(Component target, UIAnimProperty property, params (float t, Vector4 v)[] keys)
            {
                var track = new UIAnimTrack { target = target, property = property, start = UIAnimStart.KeyValue };
                foreach (var (t, v) in keys) track.keys.Add(new UIAnimKey { time = t, value = v, ease = LitMotion.Ease.Linear });
                return track;
            }
            anim.states.Add(new UIAnimState { name = "Show", tracks = { Track(_template.transform, UIAnimProperty.Scale, (0f, Vector3.zero), (0.2f, Vector3.one)) } });
            anim.states.Add(new UIAnimState { name = "Appear", tracks = { Track(image, UIAnimProperty.GraphicAlpha, (0f, Vector4.zero), (0.2f, new Vector4(1, 0))) } });
            anim.states.Add(new UIAnimState { name = "Focus", tracks = { Track(_template.transform, UIAnimProperty.Scale, (0f, Vector3.one), (0.5f, Vector3.one * 2f), (1f, Vector3.one)) } });
        }

        static UIAnimation Anim(RectTransform item) => item.GetComponent<UIAnimation>();

        [Test]
        public void 진입은_보이는_아이템이_간격을_두고_차례로_재생된다()
        {
            AnimatedItem();
            _list.entryState = "Show";
            _list.entryStagger = 0.05f;
            _list.playEntryOnFill = true;
            Fill(100);

            Assert.IsTrue(Anim(_list.GetItem(0)).IsPlaying("Show"), "첫 아이템은 바로");
            Assert.IsFalse(Anim(_list.GetItem(1)).IsPlaying("Show"), "두 번째는 아직");
            Assert.AreEqual(0f, _list.GetItem(1).localScale.x, 1e-4f, "기다리는 동안 시작 모습");

            _list.EntryAt(0.1f);
            Assert.IsTrue(Anim(_list.GetItem(2)).IsPlaying("Show"), "0.1초 → 세 번째까지");
            Assert.IsFalse(Anim(_list.GetItem(3)).IsPlaying("Show"));
        }

        [Test]
        public void 진입이_끝난_뒤_스크롤로_들어온_아이템은_끝_모습이고_등장_상태를_재생한다()
        {
            AnimatedItem();
            _list.entryState = "Show";
            _list.appearState = "Appear";
            Fill(100);   // playEntryOnFill = false: no entry running

            Assert.AreEqual(1f, _list.GetItem(0).localScale.x, 1e-4f, "진입 끝 모습");
            Assert.IsFalse(Anim(_list.GetItem(0)).IsPlaying("Appear"), "처음 채울 때는 등장 재생 안 함");

            _list.ScrollTo(1);
            Assert.IsTrue(Anim(_list.GetItem(5)).IsPlaying("Appear"), "스크롤로 들어온 5번은 등장 재생");
            Assert.AreEqual(1f, _list.GetItem(5).localScale.x, 1e-4f, "재사용된 오브젝트도 진입 끝 모습");
        }

        [Test]
        public void 스크럽_상태는_뷰포트_안_위치로_정해진다()
        {
            AnimatedItem();
            _list.scrubState = "Focus";
            Fill(100);
            _list.ScrollTo(20, UIAnimScrollList.Alignment.Center);

            Assert.AreEqual(2f, _list.GetItem(20).localScale.x, 1e-3f, "가운데 아이템 = Focus 0.5초 = 2배");
            Assert.AreEqual(1.6f, _list.GetItem(19).localScale.x, 1e-3f, "한 칸 위(뷰포트 0.3 → Focus 0.3초) = 1.6배");
        }

        [Test]
        public void Sample은_재생하지_않고_그_시간의_모습만_보여_준다()
        {
            AnimatedItem();
            _template.SetActive(true);
            var anim = _template.GetComponent<UIAnimation>();
            var raised = 0;
            anim.EventRaised += (in UIAnimEventInfo _) => raised++;
            anim.states[0].AddEvent(new UIAnimEventMarker { time = 0.1f, name = "Mid" });

            anim.Sample("Show", 0.1f);
            Assert.AreEqual(0.5f, _template.transform.localScale.x, 1e-4f);
            Assert.IsFalse(anim.IsPlaying("Show"));
            Assert.AreEqual(0, raised, "이벤트 없음");
            Assert.AreEqual(0.2f, anim.Duration("Show"), 1e-4f);
        }

        // ---------- 성능 (첫 측정) ----------

        // Jumps around a 100-item list 1000 times (several items recycle per step); logs time and GC bytes.
        (double ms, long bytes) MeasureScrolling()
        {
            for (var i = 0; i < 50; i++) _list.ScrollTo(i % 95);   // warm up: pool, dictionary, JIT
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < 1000; i++) _list.ScrollTo(i * 7 % 95);
            watch.Stop();
            return (watch.Elapsed.TotalMilliseconds, System.GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Test]
        public void 성능_바인딩만_스크롤_1000번()
        {
            _list.SetCount<RectTransform>(100, (view, i) => { });
            var (ms, bytes) = MeasureScrolling();
            TestContext.WriteLine($"바인딩만: 1000 steps {ms:0.0} ms total = {ms:0.0} µs/step, GC {bytes} bytes");
            Assert.AreEqual(5, Instances, "재사용으로 오브젝트 5개 유지");
        }

        [Test]
        public void 성능_스크럽_애니메이션_스크롤_1000번()
        {
            AnimatedItem();
            _list.scrubState = "Focus";
            _list.SetCount<RectTransform>(100, (view, i) => { });
            var (ms, bytes) = MeasureScrolling();
            TestContext.WriteLine($"바인딩 + 스크럽(UIAnimation.Sample): 1000 steps {ms:0.0} ms total = {ms:0.0} µs/step, GC {bytes} bytes");
            Assert.AreEqual(5, Instances);
        }
    }
}
