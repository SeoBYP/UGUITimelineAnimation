using System.Collections.Generic;
using LitMotion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace SeoBYP.TimelineAnimation.Tests
{
    public sealed class UIAnimationTests
    {
        GameObject _go;
        Image _image;
        UIAnimation _anim;

        RectTransform Rect => (RectTransform)_go.transform;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Button", typeof(RectTransform));
            Rect.sizeDelta = new Vector2(200, 70);
            _image = _go.AddComponent<Image>();
            _image.color = new Color(1, 0, 0, 0);
            _anim = _go.AddComponent<UIAnimation>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        static UIAnimState State(string name, params UIAnimTrack[] tracks) =>
            new() { name = name, tracks = new List<UIAnimTrack>(tracks) };

        static UIAnimTrack Track(Component target, UIAnimProperty property, bool fromCurrent, bool relative, params UIAnimKey[] keys) =>
            new() { target = target, property = property, startFromCurrent = fromCurrent, relativeToRest = relative, keys = new List<UIAnimKey>(keys) };

        static UIAnimKey Key(float time, Vector4 value) => new() { time = time, value = value, ease = Ease.Linear };

        [Test]
        public void ThreeKeys_GiveTheMiddleValue()
        {
            // 1 → 1.2 → 1
            var pulse = State("Pulse", Track(_go.transform, UIAnimProperty.Scale, false, false,
                Key(0f, Vector3.one), Key(0.1f, Vector3.one * 1.2f), Key(0.2f, Vector3.one)));
            _anim.states.Add(pulse);

            _anim.Prepare("Pulse", UIAnimInterrupt.Cancel, null);
            UIAnimation.Evaluate(pulse, 0.05f);
            Assert.AreEqual(1.1f, _go.transform.localScale.x, 1e-4f);
            UIAnimation.Evaluate(pulse, 0.1f);
            Assert.AreEqual(1.2f, _go.transform.localScale.x, 1e-4f);
            UIAnimation.Evaluate(pulse, 0.2f);
            Assert.AreEqual(1f, _go.transform.localScale.x, 1e-4f);
        }

        [Test]
        public void SingleKey_StartsFromCurrentValue()
        {
            // Hover: whatever alpha it has now → 1.
            var hover = State("Hover", Track(_image, UIAnimProperty.GraphicAlpha, true, false, Key(1f, new Vector4(1, 0))));
            _anim.states.Add(hover);

            _image.color = new Color(1, 0, 0, 0.4f);
            _anim.Prepare("Hover", UIAnimInterrupt.Cancel, null);
            UIAnimation.Evaluate(hover, 0f);
            Assert.AreEqual(0.4f, _image.color.a, 1e-4f);
            UIAnimation.Evaluate(hover, 0.5f);
            Assert.AreEqual(0.7f, _image.color.a, 1e-4f);
            UIAnimation.Evaluate(hover, 1f);
            Assert.AreEqual(1f, _image.color.a, 1e-4f);
        }

        [Test]
        public void FirstKeyAfterZero_IsADelay_WhenStartingFromCurrent()
        {
            // Toggle1 sample: hold the current value for 0.5s, then go to 2.
            var delayed = State("Delayed", Track(_go.transform, UIAnimProperty.Scale, true, false,
                Key(0.5f, Vector3.zero /* ignored: becomes the live value */), Key(1f, Vector3.one * 2f)));
            _anim.states.Add(delayed);

            _anim.Prepare("Delayed", UIAnimInterrupt.Cancel, null);   // live scale is 1
            UIAnimation.Evaluate(delayed, 0.25f);
            Assert.AreEqual(1f, _go.transform.localScale.x, 1e-4f, "Still waiting.");
            UIAnimation.Evaluate(delayed, 0.75f);
            Assert.AreEqual(1.5f, _go.transform.localScale.x, 1e-4f);
            UIAnimation.Evaluate(delayed, 1f);
            Assert.AreEqual(2f, _go.transform.localScale.x, 1e-4f);
        }

        [Test]
        public void Relative_IsMeasuredFromTheRestValue_EvenAfterItMoved()
        {
            // Button1 sample: press shrinks the rest size by (14, 7); release returns to rest from wherever it is.
            var press = State("Press", Track(_go.transform, UIAnimProperty.SizeDelta, true, true, Key(0.07f, new Vector4(-14, -7))));
            var release = State("Release", Track(_go.transform, UIAnimProperty.SizeDelta, true, true, Key(1f, Vector4.zero)));
            _anim.states.Add(press);
            _anim.states.Add(release);

            _anim.Prepare("Press", UIAnimInterrupt.Cancel, null);
            UIAnimation.Evaluate(press, 0.07f);
            Assert.AreEqual(new Vector2(186, 63), Rect.sizeDelta);

            Rect.sizeDelta = new Vector2(150, 40);   // somewhere mid-animation
            _anim.Prepare("Release", UIAnimInterrupt.Cancel, null);
            UIAnimation.Evaluate(release, 0.5f);
            Assert.AreEqual(new Vector2(175, 55), Rect.sizeDelta);
            UIAnimation.Evaluate(release, 1f);
            Assert.AreEqual(new Vector2(200, 70), Rect.sizeDelta);
        }

        [Test]
        public void KeyEase_ShapesTheArrivalAtThatKey()
        {
            var key = Key(1f, Vector3.one * 2f);
            key.curveType = UIAnimCurveType.Curve;
            key.curve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 1), new Keyframe(1, 1));
            var grow = State("Grow", Track(_go.transform, UIAnimProperty.Scale, false, false, Key(0f, Vector3.one), key));
            _anim.states.Add(grow);

            _anim.Prepare("Grow", UIAnimInterrupt.Cancel, null);
            UIAnimation.Evaluate(grow, 0.5f);
            Assert.AreEqual(2f, _go.transform.localScale.x, 1e-4f, "The curve reaches 1 halfway, so the value is already at the key.");
        }

        [Test]
        public void Cancel_NewStateTakesOverOnlyItsOwnProperties()
        {
            // Press animates alpha + scale; Release only alpha. Press keeps scale.
            var press = State("Press",
                Track(_image, UIAnimProperty.GraphicAlpha, true, false, Key(1f, new Vector4(1, 0))),
                Track(_go.transform, UIAnimProperty.Scale, true, false, Key(1f, Vector3.one * 2)));
            var release = State("Release", Track(_image, UIAnimProperty.GraphicAlpha, true, false, Key(1f, Vector4.zero)));
            _anim.states.Add(press);
            _anim.states.Add(release);

            _anim.Prepare("Press", UIAnimInterrupt.Cancel, null);
            _anim.Prepare("Release", UIAnimInterrupt.Cancel, null);

            Assert.IsTrue(_anim.IsPlaying("Press"), "Press still owns scale, so it keeps playing.");
            UIAnimation.Evaluate(press, 1f);
            Assert.AreEqual(Vector3.one * 2, _go.transform.localScale, "Press still drives scale.");
            Assert.AreEqual(0f, _image.color.a, "Press no longer drives alpha.");
        }

        [Test]
        public void Cancel_StateLosingEveryPropertyStops()
        {
            var hover = State("Hover", Track(_image, UIAnimProperty.GraphicAlpha, true, false, Key(1f, new Vector4(1, 0))));
            var unhover = State("Unhover", Track(_image, UIAnimProperty.GraphicAlpha, true, false, Key(1f, Vector4.zero)));
            _anim.states.Add(hover);
            _anim.states.Add(unhover);

            _anim.Prepare("Hover", UIAnimInterrupt.Cancel, null);
            _anim.Prepare("Unhover", UIAnimInterrupt.Cancel, null);

            Assert.IsFalse(_anim.IsPlaying("Hover"));
            Assert.IsTrue(_anim.IsPlaying("Unhover"));
        }

        [Test]
        public void Complete_SnapsTheInterruptedStateToItsEnd()
        {
            // Button2 sample: releasing mid-press first completes the press (fill fully grown).
            var press = State("Press",
                Track(_image, UIAnimProperty.GraphicAlpha, false, false, Key(0f, Vector4.zero), Key(1f, new Vector4(1, 0))),
                Track(_go.transform, UIAnimProperty.Scale, false, false, Key(0f, Vector3.zero), Key(1f, Vector3.one)));
            var release = State("Release", Track(_image, UIAnimProperty.GraphicAlpha, true, false, Key(1f, Vector4.zero)));
            _anim.states.Add(press);
            _anim.states.Add(release);

            _anim.Prepare("Press", UIAnimInterrupt.Cancel, null);
            UIAnimation.Evaluate(press, 0.3f);
            _anim.Prepare("Release", UIAnimInterrupt.Complete, null);

            Assert.IsFalse(_anim.IsPlaying("Press"));
            Assert.AreEqual(Vector3.one, _go.transform.localScale);
            Assert.AreEqual(1f, _image.color.a, "Release starts from the completed alpha.");
        }
    }
}
