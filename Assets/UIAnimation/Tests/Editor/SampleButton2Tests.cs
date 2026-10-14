using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>
    /// The Button2 sample prefab as built by Tools/UIAnimation Dev/Rebuild Sample Buttons: a ripple from the press point.
    /// Skipped where the sample isn't in the project (the package on its own).
    /// </summary>
    public sealed class SampleButton2Tests
    {
        const string Path = "Assets/Prefabs/Button2.prefab";
        GameObject _go;
        UIAnimation _anim;
        Image _fill, _hover;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            if (prefab == null) Assert.Ignore($"{Path} 샘플이 없음");
            _go = Object.Instantiate(prefab);
            _anim = _go.GetComponent<UIAnimation>();
            _fill = _go.transform.Find("Mask/Fill").GetComponent<Image>();
            _hover = _go.transform.Find("Mask/Hover").GetComponent<Image>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        static PointerEventData Pointer(Vector2 screen) => new(null) { position = screen };

        Vector2 FillWorld => _fill.rectTransform.position;

        static void Near(Vector2 expected, Vector2 actual, string message = null) =>
            Assert.Less(Vector2.Distance(expected, actual), 0.01f, $"{message} expected {expected} but was {actual}");

        UIAnimation.Playback Play(string state, PointerEventData pointer = null, UIAnimInterrupt interrupt = UIAnimInterrupt.Cancel) =>
            _anim.Prepare(state, interrupt, pointer);

        [Test]
        public void UI_이벤트_바인딩은_상태마다_하나다()
        {
            var bindings = _go.GetComponent<UIAnimTrigger>().bindings;
            Assert.AreEqual(4, bindings.Count);
            Assert.AreEqual(bindings.Count, bindings.Select(b => b.state).Distinct().Count(), "상태당 바인딩 하나");
            Assert.That(bindings.Any(b => b.evt == UIAnimEvent.PointerEnter && b.state == "Hover"));
            Assert.That(bindings.Any(b => b.evt == UIAnimEvent.PointerExit && b.state == "Unhover"));
            Assert.That(bindings.Any(b => b.evt == UIAnimEvent.PointerDown && b.state == "Press" && b.interrupt == UIAnimInterrupt.Complete));
            Assert.That(bindings.Any(b => b.evt == UIAnimEvent.PointerUp && b.state == "Release" && b.interrupt == UIAnimInterrupt.Complete));
        }

        [Test]
        public void 모든_트랙의_첫_키는_0초다()
        {
            foreach (var state in _anim.states)
            foreach (var track in state.tracks)
                Assert.AreEqual(0f, track.keys[0].time, $"{state.name} · {track.target.name} · {track.property}");
        }

        [Test]
        public void Hover는_배경을_켜고_Unhover는_끈다()
        {
            UIAnimation.Step(Play("Hover"), 1f);
            Assert.AreEqual(1f, _hover.color.a, 1e-3f);
            UIAnimation.Step(Play("Unhover"), 1f);
            Assert.AreEqual(0f, _hover.color.a, 1e-3f);
        }

        [Test]
        public void Press는_누른_자리에서_원이_퍼진다()
        {
            var press = Play("Press", Pointer(new Vector2(120, 30)));
            Near(new Vector2(120, 30), FillWorld, "시작하자마자 누른 자리");
            Assert.AreEqual(0f, _fill.color.a, 1e-3f, "투명에서");
            Assert.AreEqual(0f, _fill.rectTransform.localScale.x, 1e-3f, "크기 0에서");

            UIAnimation.Step(press, 0.15f);
            Assert.AreEqual(1f, _fill.color.a, 1e-3f, "보이게");
            Assert.AreEqual(1f, _fill.rectTransform.localScale.x, 1e-3f, "끝까지 커짐");
            Near(new Vector2(120, 30), FillWorld, "퍼지는 동안 자리 유지");
        }

        [Test]
        public void 다시_누르면_새로_누른_자리에서_퍼진다()
        {
            UIAnimation.Step(Play("Press", Pointer(new Vector2(120, 30))), 1f);
            Play("Press", Pointer(new Vector2(-90, -20)), UIAnimInterrupt.Complete);
            Near(new Vector2(-90, -20), FillWorld);
        }

        [Test]
        public void 코드로_Press하면_포인터가_없어_자리를_옮기지_않는다()
        {
            _fill.rectTransform.anchoredPosition = new Vector2(15, 5);
            UIAnimation.Step(Play("Press"), 1f);
            Near(new Vector2(15, 5), _fill.rectTransform.anchoredPosition);
        }

        [Test]
        public void Release는_원을_흐리게_지우고_자리는_그대로_둔다()
        {
            UIAnimation.Step(Play("Press", Pointer(new Vector2(120, 30))), 1f);
            UIAnimation.Step(Play("Release", null, UIAnimInterrupt.Complete), 0.25f);
            Assert.AreEqual(0f, _fill.color.a, 1e-3f);
            Near(new Vector2(120, 30), FillWorld);
        }

        [Test]
        public void Release_중에_누르면_Release를_끝내고_Press를_시작한다()
        {
            UIAnimation.Step(Play("Press", Pointer(new Vector2(120, 30))), 1f);
            var release = Play("Release", null, UIAnimInterrupt.Complete);
            UIAnimation.Step(release, 0.1f);   // fading out

            Play("Press", Pointer(new Vector2(-90, -20)), UIAnimInterrupt.Complete);
            Assert.IsFalse(_anim.IsPlaying("Release"));
            Assert.IsTrue(_anim.IsPlaying("Press"));
            Near(new Vector2(-90, -20), FillWorld);
        }
    }
}
