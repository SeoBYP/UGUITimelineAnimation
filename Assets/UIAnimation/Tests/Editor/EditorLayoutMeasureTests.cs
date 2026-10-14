using System.Collections;
using System.Linq;
using NUnit.Framework;
using SeoBYP.TimelineAnimation.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>
    /// Measures the laid-out UI Animation Editor (real window, a few editor frames) instead of looking at a screenshot:
    /// row heights, key positions, opacity, label colors, button states. Values are written to the test output.
    /// Needs batchmode with a graphics device (no -nographics).
    /// </summary>
    public sealed class EditorLayoutMeasureTests
    {
        const string PopupPath = "Assets/Prefabs/Popup/Popup1.prefab";
        UIAnimationEditorWindow _window;
        GameObject _popup, _screen;

        [SetUp]
        public void SetUp()
        {
            // The real editor window needs a graphics device: skipped under -nographics (CI without a virtual display).
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("에디터 창 테스트: 그래픽 장치가 필요해서 건너뜀 (-nographics)");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath);
            if (asset == null) Assert.Ignore($"{PopupPath} 샘플이 없음");
            _popup = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            _window = EditorWindow.GetWindow<UIAnimationEditorWindow>();
            _window.position = new Rect(0, 0, 1400, 700);
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null) _window.Close();
            if (_screen != null) Object.DestroyImmediate(_screen);
            else if (_popup != null) Object.DestroyImmediate(_popup);
        }

        static IEnumerator Frames(int n = 5)
        {
            for (var i = 0; i < n; i++) yield return null;
        }

        static string Rgb(Color c) => $"({Mathf.RoundToInt(c.r * 255)},{Mathf.RoundToInt(c.g * 255)},{Mathf.RoundToInt(c.b * 255)})";

        // Every keyframe row with what it looks like on screen.
        string Describe(VisualElement row)
        {
            var label = row.Q<Label>();
            var lane = row.Q(className: "uia-lane");
            var keys = lane.Query(className: "uia-key").ToList();
            var keyX = string.Join(",", keys.Select(k => (k.worldBound.center.x - lane.worldBound.xMin).ToString("0.0")));
            var buttons = row.Query<Button>().ToList().Select(b => $"{b.text}{(b.enabledInHierarchy ? "" : "(off)")}");
            return $"{label.text,-22} h={row.worldBound.height:0} opacity={row.resolvedStyle.opacity:0.00} " +
                   $"name={Rgb(label.resolvedStyle.color)} keys@[{keyX}] keyW={keys.FirstOrDefault()?.worldBound.width:0.0} " +
                   $"readonly={row.ClassListContains("uia-row--readonly")} buttons=[{string.Join(" ", buttons)}]";
        }

        [UnityTest]
        public IEnumerator Popup1_Open의_자식_트랙을_화면_값으로_잰다()
        {
            _window.SetTarget(_popup.GetComponent<UIAnimation>());
            yield return Frames();

            var rows = _window.Timeline.Query(className: "uia-row").ToList().Where(r => r.Q(className: "uia-lane") != null).ToList();
            foreach (var row in rows) TestContext.WriteLine(Describe(row));
            foreach (var header in _window.Timeline.Query(className: "uia-callee-row").ToList())
            {
                var revert = header.Q<Button>();
                TestContext.WriteLine($"[call] {header.Q<Label>().text} h={header.worldBound.height:0} ↺={(revert.enabledInHierarchy ? "on" : "off")}");
            }

            Assert.That(rows.Count, Is.GreaterThan(0), "레이아웃이 계산됨");
            Assert.That(rows.All(r => Mathf.Approximately(r.worldBound.height, rows[0].worldBound.height)), "모든 트랙 줄 높이 같음");
            foreach (var row in rows)
            {
                var lane = row.Q(className: "uia-lane");
                foreach (var key in lane.Query(className: "uia-key").ToList())
                    Assert.GreaterOrEqual(key.worldBound.xMin, lane.worldBound.xMin - 0.5f, $"{row.Q<Label>().text}: 키가 줄 왼쪽에서 잘리지 않음");
            }
        }

        [UnityTest]
        public IEnumerator 바깥_Prefab이_같은_트랙을_오버라이드하면_안쪽_팝업_에디터에서_어떻게_보이는지_잰다()
        {
            // Screen > Popup1 > Button Close. Popup1 already overrides Button Close's Hover (Fill, Label → 0.1s);
            // the Screen overrides the Fill track again (→ 0.05s). The outermost wins when it plays.
            _screen = new GameObject("Screen", typeof(RectTransform));
            var screen = _screen.AddComponent<UIAnimation>();
            _popup.transform.SetParent(_screen.transform, false);
            var popup = _popup.GetComponent<UIAnimation>();
            var button = popup.states[0].childCalls.First(c => c.state == "Hover").child;
            var popupFill = popup.overrides.Single(o => o.state == "Hover").tracks.Single(t => t.target.name == "Fill");
            var screenFill = popupFill.Clone();
            foreach (var key in screenFill.keys) key.time *= 0.5f;
            screen.overrides.Add(new UIAnimOverride { child = button, state = "Hover", tracks = { screenFill } });

            _window.SetTarget(popup);
            yield return Frames();
            var call = popup.states[0].childCalls.First(c => c.state == "Hover");

            VisualElement FillRow() => _window.Timeline.Query(className: "uia-row").ToList()
                .Where(r => r.Q(className: "uia-lane") != null).First(r => r.Q<Label>().text.StartsWith("Fill"));
            void Log(string step) => TestContext.WriteLine($"{step,-24} {Describe(FillRow())} tooltip=\"{FillRow().Q(className: "uia-header").tooltip}\"");

            // Like Unity: what plays in this context is shown, marked overridden, and editable.
            Log("바깥 Screen이 덮어씀");
            Assert.AreSame(screenFill, button.EffectiveState("Hover").tracks.First(t => t.target.name == "Fill"), "재생되는 건 Screen의 Fill");
            Assert.IsFalse(FillRow().ClassListContains("uia-row--readonly"), "편집 가능");
            Assert.That(FillRow().Q<Label>().ClassListContains("uia-track-override"), "오버라이드 색");
            StringAssert.Contains("Screen", FillRow().Q(className: "uia-header").tooltip, "어느 Prefab이 덮었는지");

            // Editing it edits that layer (Screen's), not the popup's own override.
            var popupEnd = popupFill.keys[^1].time;
            _window.Timeline.KeyClicked(screenFill.keys[^1], false);
            ((INotifyValueChanged<float>)_window.Inspector.Query<Label>(className: "unity-base-field__label").ToList()
                .First(l => l.text == "Time (s)").parent).value = 0.07f;
            Assert.AreEqual(0.07f, screenFill.keys[^1].time, 1e-4f, "Screen의 Fill이 바뀜");
            Assert.AreEqual(popupEnd, popupFill.keys[^1].time, 1e-4f, "팝업 자신의 Fill은 그대로");

            // × removes only the outermost layer: the popup's own override shows again, then the original.
            _window.Timeline.RemoveRequested(screenFill);
            yield return Frames();
            Log("× 한 번 (Screen 제거)");
            Assert.AreSame(popupFill, button.EffectiveState("Hover").tracks.First(t => t.target.name == "Fill"), "팝업의 Fill이 다시 재생");
            Assert.That(FillRow().Q<Label>().ClassListContains("uia-track-override"), "여전히 오버라이드(팝업 단계)");
            StringAssert.Contains(popup.name, FillRow().Q(className: "uia-header").tooltip);

            _window.Timeline.RemoveRequested(popupFill);
            yield return Frames();
            Log("× 두 번 (팝업 제거)");
            Assert.That(FillRow().ClassListContains("uia-row--readonly"), "원본(흐림)");
            Assert.AreSame(button.FindState("Hover").tracks.First(t => t.target.name == "Fill"),
                button.EffectiveState("Hover").tracks.First(t => t.target.name == "Fill"), "Button Close 자신의 Fill");
        }

        [UnityTest]
        public IEnumerator 전체_되돌리기는_가장_바깥_단계만_되돌린다()
        {
            _screen = new GameObject("Screen", typeof(RectTransform));
            var screen = _screen.AddComponent<UIAnimation>();
            _popup.transform.SetParent(_screen.transform, false);
            var popup = _popup.GetComponent<UIAnimation>();
            var call = popup.states[0].childCalls.First(c => c.state == "Hover");
            var popupLayer = popup.overrides.Single(o => o.state == "Hover");
            var screenFill = popupLayer.tracks.Single(t => t.target.name == "Fill").Clone();
            screen.overrides.Add(new UIAnimOverride { child = call.child, state = "Hover", tracks = { screenFill } });
            _window.SetTarget(popup);
            yield return Frames();

            _window.RemoveOverride(call);   // ↺ (after its confirmation)
            yield return Frames();
            TestContext.WriteLine($"↺ 후: Screen 오버라이드 {screen.overrides.Count}개, Popup1 오버라이드 트랙 {popupLayer.tracks.Count}개");
            Assert.AreEqual(0, screen.overrides.Count, "바깥(Screen) 단계만 사라짐");
            Assert.AreEqual(2, popup.overrides.Single(o => o.state == "Hover").tracks.Count, "Popup1 자신의 오버라이드는 남음");
            Assert.That(call.Target.tracks.All(t => popupLayer.tracks.Contains(t)), "이제 Popup1의 값이 재생");

            _window.RemoveOverride(call);
            Assert.AreEqual(0, popup.overrides.Count(o => o.state == "Hover"), "한 번 더 누르면 Popup1 단계도 사라짐");
        }
    }
}
