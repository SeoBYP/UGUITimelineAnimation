using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SeoBYP.TimelineAnimation.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>
    /// Builds the Button2 ripple from scratch the way a user does in the UI Animation Editor: add the component, a state,
    /// tracks from the picker, then edit names, Start, key values and Play On through the inspector fields. Then checks
    /// that it plays right and that the editor draws every track/key/inspector in the same form.
    /// </summary>
    public sealed class EditorWorkflowTests
    {
        UIAnimationEditorWindow _window;
        GameObject _root;
        RectTransform _fill;
        Image _fillImage;

        [SetUp]
        public void SetUp()
        {
            // What the user makes in the Hierarchy: Button > Mask > Fill (a centered circle).
            _root = new GameObject("Button", typeof(RectTransform), typeof(Image));
            ((RectTransform)_root.transform).sizeDelta = new Vector2(480, 168);
            var mask = new GameObject("Mask", typeof(RectTransform), typeof(Image));
            mask.transform.SetParent(_root.transform, false);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(mask.transform, false);
            _fill = (RectTransform)fill.transform;
            _fill.sizeDelta = new Vector2(1080, 1080);
            _fillImage = fill.GetComponent<Image>();
            _window = EditorWindow.GetWindow<UIAnimationEditorWindow>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null) _window.Close();
            Object.DestroyImmediate(_root);
        }

        // ---------- driving the inspector like a user ----------

        VisualElement FieldByLabel(string label) =>
            _window.Inspector.Query<Label>(className: "unity-base-field__label").ToList().FirstOrDefault(l => l.text == label)?.parent
            ?? throw new AssertionException($"인스펙터에 '{label}' 칸이 없음. 있는 칸: " +
                                            string.Join(", ", _window.Inspector.Query<Label>(className: "unity-base-field__label").ToList().Select(l => l.text)));

        void Set<T>(string label, T value) => ((INotifyValueChanged<T>)FieldByLabel(label)).value = value;

        List<string> Sections() => _window.Inspector.Query<Foldout>().ToList().Select(f => f.text).ToList();

        UIAnimTrack Track(UIAnimState state, Component target, UIAnimProperty property) =>
            state.tracks.Single(t => t.target == target && t.property == property);

        void SelectKey(UIAnimKey key) => _window.Timeline.KeyClicked(key, false);

        UIAnimState BuildPressFromScratch(out UIAnimation anim)
        {
            // Getting Started → "Add UIAnimation", then the + tab, then rename.
            anim = Undo.AddComponent<UIAnimation>(_root);
            _window.SetTarget(anim);
            _window.AddState();
            Set("Name", "Press");

            // + Add Track (multi-select): Fill Position, Alpha, Scale.
            _window.AddTracks(new List<(UIAnimPickKind, Component, UIAnimProperty)>
            {
                (UIAnimPickKind.Property, _fill, UIAnimProperty.AnchoredPosition),
                (UIAnimPickKind.Property, _fillImage, UIAnimProperty.GraphicAlpha),
                (UIAnimPickKind.Property, _fill, UIAnimProperty.Scale),
            });
            var press = anim.states.Single();
            var position = Track(press, _fill, UIAnimProperty.AnchoredPosition);
            var alpha = Track(press, _fillImage, UIAnimProperty.GraphicAlpha);
            var scale = Track(press, _fill, UIAnimProperty.Scale);

            // Position: starts where the pointer pressed.
            _window.Timeline.TrackSelected(position);
            Set("Start", UIAnimStart.Pointer);

            // Alpha 0 → 1 and Scale 0 → 1, from the written values (not the current ones).
            _window.Timeline.TrackSelected(alpha);
            Set("Start", UIAnimStart.KeyValue);
            SelectKey(alpha.keys[0]);
            Set("Value", 0f);
            SelectKey(alpha.keys[1]);
            Set("Value", 1f);

            _window.Timeline.TrackSelected(scale);
            Set("Start", UIAnimStart.KeyValue);
            SelectKey(scale.keys[0]);
            Set("Value", 0f);   // linked scale slider: x = y = z
            SelectKey(scale.keys[1]);
            Set("Value", 1f);

            // Play On: PointerDown (state inspector, nothing selected).
            _window.Timeline.SelectionCleared();
            Set("Play On", nameof(UIAnimEvent.PointerDown));
            return press;
        }

        static PointerEventData Pointer(Vector2 screen) => new(null) { position = screen };

        static void Near(Vector2 expected, Vector2 actual, string message) =>
            Assert.Less(Vector2.Distance(expected, actual), 0.01f, $"{message}: expected {expected} but was {actual}");

        // ---------- tests ----------

        [Test]
        public void 에디터로_처음부터_만든_Press는_누른_자리에서_퍼진다()
        {
            var press = BuildPressFromScratch(out var anim);

            Assert.AreEqual(3, press.tracks.Count);
            Assert.That(press.tracks.All(t => t.keys.Count == 2 && t.keys[0].time == 0f), "트랙마다 0초 키 + 끝 키");
            var binding = _root.GetComponent<UIAnimTrigger>().bindings.Single();
            Assert.AreEqual((UIAnimEvent.PointerDown, "Press"), (binding.evt, binding.state), "Play On 바인딩");

            var playback = anim.Prepare("Press", UIAnimInterrupt.Cancel, Pointer(new Vector2(120, 30)));
            Near(new Vector2(120, 30), _fill.position, "시작하자마자 누른 자리");
            Assert.AreEqual(0f, _fillImage.color.a, 1e-3f, "투명에서");
            Assert.AreEqual(0f, _fill.localScale.x, 1e-3f, "크기 0에서");

            UIAnimation.Step(playback, press.Duration);
            Assert.AreEqual(1f, _fillImage.color.a, 1e-3f, "보이게");
            Assert.AreEqual(1f, _fill.localScale.x, 1e-3f, "끝까지 커짐");
            Near(new Vector2(120, 30), _fill.position, "퍼지는 동안 자리 유지");
        }

        [Test]
        public void Pointer_트랙에_키를_더해도_누른_자리를_기준으로_움직인다()
        {
            var press = BuildPressFromScratch(out var anim);
            var position = Track(press, _fill, UIAnimProperty.AnchoredPosition);

            // Double-click at 0.03s: the new key is "stay at the press point".
            _window.Timeline.AddKeyRequested(position, 0.03f);
            var added = position.keys.Single(k => Mathf.Approximately(k.time, 0.03f));
            Assert.AreEqual(Vector4.zero, added.value, "새 키 = 누른 자리 그대로(오프셋 0)");
            Assert.NotNull(FieldByLabel("Value (press point +)"), "값이 누른 자리 기준이라는 라벨");

            // Then the user sets it 20 up: it rises from wherever the press was.
            Set("Value (press point +)", new Vector2(0, 20));
            var playback = anim.Prepare("Press", UIAnimInterrupt.Cancel, Pointer(new Vector2(-90, -20)));
            UIAnimation.Step(playback, 0.03f);
            Near(new Vector2(-90, 0), _fill.position, "누른 자리에서 20 위");
            UIAnimation.Step(playback, press.Duration);
            Near(new Vector2(-90, -20), _fill.position, "끝 키(오프셋 0) = 누른 자리로 돌아옴");
        }

        [Test]
        public void 타임라인의_모든_트랙과_키는_같은_모양이다()
        {
            var press = BuildPressFromScratch(out _);
            var rows = _window.Timeline.Query(className: "uia-row").ToList().Where(r => r.Q(className: "uia-lane") != null).ToList();
            Assert.AreEqual(press.tracks.Count, rows.Count, "트랙마다 줄 하나");

            foreach (var row in rows)
            {
                var header = row.Q(className: "uia-header");
                Assert.AreEqual(1, header.Query(className: "uia-stripe").ToList().Count, "색 띠");
                Assert.AreEqual(2, header.Query(className: "uia-smallbtn").ToList().Count, "◆ ×");
                var keys = row.Q(className: "uia-lane").Query(className: "uia-key").ToList();
                Assert.AreEqual(2, keys.Count, "키 두 개(0초 + 끝)");
                var shape = keys.Select(k => string.Join(" ", k.GetClasses().Where(c => c != "uia-key--selected").OrderBy(c => c))).Distinct();
                CollectionAssert.AreEqual(new[] { "uia-key" }, shape, "같은 키 모양(선택 표시 말고는 같은 클래스)");
            }
        }

        [Test]
        public void 키_인스펙터는_어느_키든_같은_구성이다()
        {
            var press = BuildPressFromScratch(out _);
            var position = Track(press, _fill, UIAnimProperty.AnchoredPosition);
            var alpha = Track(press, _fillImage, UIAnimProperty.GraphicAlpha);

            foreach (var key in new[] { position.keys[0], position.keys[1], alpha.keys[0], alpha.keys[1] })
            {
                SelectKey(key);
                CollectionAssert.AreEqual(new[] { "Key", "Arrival" }, Sections(), "섹션 구성");
                Assert.NotNull(FieldByLabel("Time (s)"));
            }

            // Not applicable = disabled with the reason, never hidden.
            SelectKey(position.keys[0]);
            Assert.IsFalse(FieldByLabel("Value (press point +)").enabledSelf, "재생 시작 시 정해지는 값");
            Assert.That(_window.Inspector.Query<Label>().ToList().Any(l => l.text.StartsWith("Set when playback starts")), "이유 한 줄");
            Assert.That(_window.Inspector.Query<Label>().ToList().Any(l => l.text == "First key: nothing arrives before it."), "첫 키 Arrival 이유");

            SelectKey(alpha.keys[1]);
            Assert.IsTrue(FieldByLabel("Value").enabledSelf, "보통 키는 편집 가능");
        }
    }
}
