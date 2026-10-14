using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>
    /// R3 measurements on the sample prefabs (EditMode, this PC): time and managed allocations of starting states and of
    /// one frame of evaluation, for many objects at once. Results go to the test log; the GC-free paths are asserted.
    /// </summary>
    public sealed class PerformanceTests
    {
        const string Button1 = "Assets/Prefabs/Buttons/Button1.prefab";
        const string TextDemo = "Assets/Prefabs/Text/TextDemo.prefab";
        const string ScrollTile = "Assets/Prefabs/Scroll/Items/ScrollTile.prefab";
        const string ChatLeft = "Assets/Prefabs/Scroll/Items/ChatLeft.prefab";
        readonly List<Object> _destroy = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _destroy) Object.DestroyImmediate(o);
            _destroy.Clear();
        }

        static GameObject Load(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) Assert.Ignore($"{path} 샘플이 없음");
            return prefab;
        }

        // Canvas parent so UI and TMP behave as in a scene.
        Transform Canvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _destroy.Add(go);
            return go.transform;
        }

        List<UIAnimation> Spawn(string path, int count)
        {
            var prefab = Load(path);
            var parent = Canvas();
            return Enumerable.Range(0, count).Select(_ => Object.Instantiate(prefab, parent).GetComponent<UIAnimation>()).ToList();
        }

        static (double ms, double allocs, long bytes) Measure(Action action, int repeat) => PerfMeasure.Run(action, repeat);

        static void Log(string what, (double ms, double allocs, long bytes) r, string per) =>
            TestContext.WriteLine($"{what}: {r.ms * 1000:0.0} µs/{per}, GC 할당 {r.allocs:0.##}회 · 약 {r.bytes} bytes/{per}");

        // The allocation counter must see known allocations, or every "0" below means nothing.
        [Test]
        public void 측정_도구_검증_알려진_할당을_잡는다()
        {
            object keep = null;
            var none = Measure(() => keep = keep ?? "x", 100);
            var some = Measure(() => keep = new byte[1000], 100);
            TestContext.WriteLine($"할당 없음: {none.allocs}회 / 1KB 배열: {some.allocs}회 · 약 {some.bytes} bytes");
            Assert.AreEqual(0, none.allocs, "할당 없는 코드 = 0회");
            Assert.AreEqual(1, some.allocs, "1KB 배열 1번 = 1회");
            Assert.GreaterOrEqual(some.bytes, 1000);
        }

        // ---------- buttons ----------

        [Test]
        public void 버튼_100개_동시_Hover()
        {
            var buttons = Spawn(Button1, 100);

            // Starting: Play on all 100 (LitMotion motion created per state), Hover and Unhover in turn.
            var hover = true;
            var start = Measure(() =>
            {
                var state = hover ? "Hover" : "Unhover";
                foreach (var b in buttons) b.Play(state);
                hover = !hover;
            }, 50);
            Log("Play(Hover/Unhover) × 100개", start, "100개 시작");

            // One frame: evaluating all 100 running states.
            var playbacks = buttons.Select(b => b.Prepare("Hover", UIAnimInterrupt.Cancel, null)).ToList();
            var duration = buttons[0].states.First(s => s.name == "Hover").Duration;
            var frame = 0;
            var step = Measure(() =>
            {
                var t = frame++ % 60 / 60f * duration;
                foreach (var p in playbacks) UIAnimation.Step(p, t);
            }, 600);
            Log("Hover 진행 × 100개", step, "프레임");
            Assert.AreEqual(0, step.allocs, "진행 중 매 프레임 할당 없음");
        }

        // ---------- text ----------

        [Test]
        public void 텍스트_10개_동시_재생_상태별()
        {
            var texts = Spawn(TextDemo, 10);
            foreach (var state in new[] { "Typewriter", "Scramble", "Flip", "PopUp", "Punch", "Shake" })
            {
                var playbacks = texts.Select(t => t.Prepare(state, UIAnimInterrupt.Cancel, null)).ToList();
                var duration = texts[0].states.First(s => s.name == state).Duration;
                var frame = 0;
                var step = Measure(() =>
                {
                    var t = (frame++ % 60 + 1) / 60f * duration;
                    foreach (var p in playbacks) UIAnimation.Step(p, t);
                }, 120);
                Log($"{state} × 10개", step, "프레임");
                if (state != "Scramble") Assert.AreEqual(0, step.allocs, $"{state}: 매 프레임 할당 없음");
            }
        }

        // ---------- heavy scroll forms ----------

        static UIAnimScrollList List(Transform parent, GameObject item, Vector2 size)
        {
            var root = new GameObject("List", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            ((RectTransform)root.transform).sizeDelta = size;
            var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
            viewport.SetParent(root.transform, false);
            viewport.sizeDelta = size;
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            var scroll = root.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            var list = root.AddComponent<UIAnimScrollList>();
            list.itemPrefab = (RectTransform)item.transform;
            list.playEntryOnFill = false;
            return list;
        }

        static (double ms, double allocs, long bytes) Jumps(UIAnimScrollList list, int count)
        {
            var random = new System.Random(1);
            return Measure(() => list.ScrollTo(random.Next(count)), 1000);
        }

        [Test]
        public void 무거운_스크롤_형태()
        {
            var parent = Canvas();
            var tile = Load(ScrollTile);

            var masonry = List(parent, tile, new Vector2(1000, 680));
            masonry.layout = UIAnimScrollList.LayoutType.Masonry;
            masonry.constraint = UIAnimScrollList.GridConstraint.FixedColumnCount;
            masonry.constraintCount = 4;
            masonry.cellSize = new Vector2(220, 220);
            masonry.sizeOf = i => 140 + i * 53 % 4 * 60;
            masonry.SetCount(1000, (RectTransform _, int _) => { });
            var masonryJumps = Jumps(masonry, 1000);
            Log("메이슨리 1000개, 임의 위치로 점프", masonryJumps, "점프");

            var sticky = List(parent, tile, new Vector2(840, 680));
            sticky.extraPrefabs.Add((RectTransform)tile.transform);
            sticky.prefabOf = i => i % 6 == 0 ? 1 : 0;
            sticky.stickyHeaderPrefab = 1;
            sticky.SetCount(1000, (RectTransform _, int _) => { });
            var stickyJumps = Jumps(sticky, 1000);
            Log("섹션 헤더 1000개(헤더 167개), 임의 위치로 점프", stickyJumps, "점프");

            var chat = List(parent, Load(ChatLeft), new Vector2(840, 560));
            chat.measureItems = true;
            chat.controlChildSize = true;
            var count = 200;
            var fill = Measure(() => chat.SetCount(count, (RectTransform v, int i) => v.GetComponentInChildren<TMPro.TMP_Text>().text = new string('x', i % 120 + 1)), 5);
            Log("측정 리스트(말풍선) 200개 채우기", fill, "회");
            var add = Measure(() => chat.SetCount(++count, 0f), 20);
            Log("측정 리스트에 1개 추가(새 것만 측정)", add, "회");
            Assert.Less(add.ms, fill.ms / 10, "1개 추가가 전체를 다시 재지 않음");

            // Masonry reuses an item at a different height; TMP allocates when a text's rect is resized (not this code:
            // with equal heights, or the text disabled, it is 0). Logged, not asserted.
            Assert.Less(stickyJumps.allocs, 0.01, "섹션 헤더 스크롤 중 할당 거의 없음 (보이는 개수가 새 최대가 될 때 풀에 하나 만드는 정도)");
        }
    }

    /// <summary>Shared by the performance tests.</summary>
    internal static class PerfMeasure
    {
        /// <summary>
        /// Time per call and managed allocations per call. GC.GetAllocatedBytesForCurrentThread always returns 0 in Unity's
        /// Mono, so allocations are counted with the profiler's "GC.Alloc" marker (as Unity Test Framework's
        /// Is.Not.AllocatingGCMemory does): allocs = number of allocations, bytes = rough GetTotalMemory growth.
        /// </summary>
        public static (double ms, double allocs, long bytes) Run(Action action, int repeat)
        {
            action();   // warm up: first-use allocations (JIT, caches) are not the steady state
            var recorder = UnityEngine.Profiling.Recorder.Get("GC.Alloc");
            recorder.FilterToCurrentThread();
            recorder.enabled = false;
            GC.Collect();
            var memory = GC.GetTotalMemory(false);
            var watch = new Stopwatch();   // created outside the counted window (it is an allocation itself)
            recorder.enabled = true;
            watch.Start();
            for (var i = 0; i < repeat; i++) action();
            watch.Stop();
            recorder.enabled = false;
            var allocs = recorder.sampleBlockCount;
            var bytes = Math.Max(0, GC.GetTotalMemory(false) - memory);
            return (watch.Elapsed.TotalMilliseconds / repeat, (double)allocs / repeat, bytes / repeat);
        }

    }
}
