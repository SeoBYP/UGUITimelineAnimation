using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace SeoBYP.TimelineAnimation.Tests
{
    public sealed class UIAnimationTextTests
    {
        GameObject _canvas;
        TextMeshProUGUI _text;
        UIAnimation _anim;

        [SetUp]
        public void SetUp()
        {
            if (TMP_Settings.defaultFontAsset == null) Assert.Ignore("TMP Essential Resources are not imported.");
            _canvas = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(_canvas.transform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(400, 100);
            _text = go.AddComponent<TextMeshProUGUI>();
            _text.font = TMP_Settings.defaultFontAsset;
            _text.text = "ABCD";
            _text.ForceMeshUpdate();
            _anim = go.AddComponent<UIAnimation>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_canvas);

        UIAnimState Add(UIAnimProperty property, float stagger, params UIAnimKey[] keys)
        {
            var state = new UIAnimState
            {
                name = property.ToString(),
                tracks = { new UIAnimTrack { target = _text, property = property, startFromCurrent = false, stagger = stagger, keys = new List<UIAnimKey>(keys) } },
            };
            _anim.states.Add(state);
            _anim.Prepare(state.name, UIAnimInterrupt.Cancel, null);
            return state;
        }

        static UIAnimKey Key(float time, Vector4 value) => new() { time = time, value = value, ease = UIAnimEase.Linear };

        [Test]
        public void 공개_비율만큼_글자가_보인다()
        {
            var reveal = Add(UIAnimProperty.TextReveal, 0f, Key(0f, Vector4.zero), Key(1f, new Vector4(1, 0)));

            UIAnimation.Evaluate(reveal, 0.5f);
            Assert.AreEqual(2, _text.maxVisibleCharacters);
            UIAnimation.Evaluate(reveal, 1f);
            Assert.GreaterOrEqual(_text.maxVisibleCharacters, 4, "Fully revealed.");
        }

        [Test]
        public void 글자_효과_중에_상자가_넓어지면_줄바꿈을_다시_한다()
        {
            // A title growing with its banner (Victory): laid out narrow first, then the rect widens.
            _text.text = "VICTORY";
            _text.textWrappingMode = TextWrappingModes.Normal;
            var rect = _text.rectTransform;
            rect.sizeDelta = new Vector2(60, 100);
            var offset = Add(UIAnimProperty.CharOffset, 0.05f, Key(0f, new Vector4(0, 50)), Key(0.5f, Vector4.zero));

            UIAnimation.Evaluate(offset, 0.1f);
            Assert.Greater(_text.textInfo.lineCount, 1, "좁으면 여러 줄");
            rect.sizeDelta = new Vector2(1000, 100);
            UIAnimation.Evaluate(offset, 0.2f);
            Assert.AreEqual(1, _text.textInfo.lineCount, "넓어지면 한 줄");
        }

        [Test]
        public void 글자_간격만큼_마지막_글자가_늦어져_길이가_늘어난다()
        {
            // 4 characters: the last one starts 3 × 0.1s later than the first.
            var pop = Add(UIAnimProperty.CharScale, 0.1f, Key(0f, Vector3.zero), Key(0.2f, Vector3.one));
            Assert.AreEqual(0.5f, pop.Duration, 1e-4f);
        }

        [Test]
        public void 글자_크기는_각_글자_중심_기준으로_줄었다가_돌아온다()
        {
            var pop = Add(UIAnimProperty.CharScale, 0f, Key(0f, Vector3.zero), Key(1f, Vector3.one));
            var ch = _text.textInfo.characterInfo[0];

            UIAnimation.Evaluate(pop, 0f);
            var verts = _text.textInfo.meshInfo[ch.materialReferenceIndex].vertices;
            Assert.AreEqual(verts[ch.vertexIndex], verts[ch.vertexIndex + 2], "Scale 0: the quad collapses to one point.");

            UIAnimation.Evaluate(pop, 1f);
            verts = _text.textInfo.meshInfo[ch.materialReferenceIndex].vertices;
            Assert.AreNotEqual(verts[ch.vertexIndex], verts[ch.vertexIndex + 2], "Scale 1: the character is back to normal.");
        }

        [Test]
        public void Punch_키는_흔들린_뒤_이전_값으로_돌아온다()
        {
            var track = new UIAnimTrack { keys = { Key(0f, Vector3.one),
                new UIAnimKey { time = 1f, value = Vector3.one * 0.5f, curveType = UIAnimCurveType.Punch, ease = UIAnimEase.Linear, frequency = 5f, damping = 1f } } };
            track.keys[0].resolved = Vector3.one;
            track.keys[1].resolved = Vector3.one;   // vibration keys hold the previous value

            Assert.AreEqual(1f, track.Sample(0f).x, 1e-4f);
            Assert.AreEqual(1f, track.Sample(1f).x, 1e-4f, "Ends at rest.");
            var moved = false;
            for (var t = 0.05f; t < 1f; t += 0.05f) moved |= Mathf.Abs(track.Sample(t).x - 1f) > 0.01f;
            Assert.IsTrue(moved, "Oscillates in between.");
        }

        [Test]
        public void 스크램블은_글자_수를_유지하고_끝나면_원문이_된다()
        {
            var reveal = Add(UIAnimProperty.TextReveal, 0f, Key(0f, Vector4.zero), Key(1f, new Vector4(1, 0)));
            reveal.tracks[0].scramble = UIAnimScramble.Lowercase;
            UIAnimation.Resolve(reveal);

            UIAnimation.Evaluate(reveal, 0.5f);
            StringAssert.StartsWith("AB", _text.text);
            Assert.AreEqual(4, _text.text.Length);
            UIAnimation.Evaluate(reveal, 1f);
            Assert.AreEqual("ABCD", _text.text);
        }

        [Test]
        public void 스크램블은_공백을_유지해서_줄바꿈이_바뀌지_않는다()
        {
            var sb = new System.Text.StringBuilder();
            var result = UIAnimVibration.Scramble("<b>ab</b> cd ef", 0.25f, UIAnimScramble.Lowercase, 0u, 1.0, sb);
            Assert.AreEqual(8, result.Length - "<b></b>".Length, "Same visible length.");
            StringAssert.StartsWith("<b>ab</b>", result);
            Assert.AreEqual(' ', result[9]);
            Assert.AreEqual(' ', result[12]);
        }

        // ---------- 대사창: 같은 TMP의 text를 바꿔 가며 재생 ----------

        [Test]
        public void 텍스트를_바꾸고_다시_재생하면_새_글자수로_공개된다()
        {
            var reveal = Add(UIAnimProperty.TextReveal, 0f, Key(0f, Vector4.zero), Key(1f, new Vector4(1, 0)));
            UIAnimation.Evaluate(reveal, 1f);

            _text.text = "ABCDEFGH";
            _anim.Prepare(reveal.name, UIAnimInterrupt.Cancel, null);
            UIAnimation.Evaluate(reveal, 0.5f);
            Assert.AreEqual(4, _text.maxVisibleCharacters, "8글자의 절반");
        }

        [Test]
        public void 텍스트를_바꾸고_재생하면_첫_프레임부터_새_텍스트가_숨겨져_있다()
        {
            var reveal = Add(UIAnimProperty.TextReveal, 0f, Key(0f, Vector4.zero), Key(1f, new Vector4(1, 0)));
            UIAnimation.Evaluate(reveal, 1f);   // 이전 대사가 다 나온 상태

            _text.text = "NEXT LINE";
            _anim.Prepare(reveal.name, UIAnimInterrupt.Cancel, null);   // Evaluate 없이 바로 확인
            Assert.AreEqual(0, _text.maxVisibleCharacters);
        }

        [Test]
        public void 텍스트를_바꾸면_글자_간격으로_늘어나는_길이도_새_글자수로_계산된다()
        {
            var state = Add(UIAnimProperty.CharScale, 0.1f, Key(0f, Vector3.zero), Key(1f, Vector3.one));
            Assert.AreEqual(1.3f, state.Duration, 1e-4f, "4글자: 1 + 3 x 0.1");

            _text.text = "ABCDEFGH";
            _anim.Prepare(state.name, UIAnimInterrupt.Cancel, null);
            Assert.AreEqual(1.7f, state.Duration, 1e-4f, "8글자: 1 + 7 x 0.1");
        }

        [Test]
        public void 스크램블_텍스트를_바꾸고_다시_재생하면_새_텍스트로_공개된다()
        {
            var reveal = Add(UIAnimProperty.TextReveal, 0f, Key(0f, Vector4.zero), Key(1f, new Vector4(1, 0)));
            reveal.tracks[0].scramble = UIAnimScramble.Lowercase;
            _anim.Prepare(reveal.name, UIAnimInterrupt.Cancel, null);
            UIAnimation.Evaluate(reveal, 1f);
            Assert.AreEqual("ABCD", _text.text);

            _text.text = "WXYZ";
            _anim.Prepare(reveal.name, UIAnimInterrupt.Cancel, null);
            UIAnimation.Evaluate(reveal, 0.5f);
            StringAssert.StartsWith("WX", _text.text);
            UIAnimation.Evaluate(reveal, 1f);
            Assert.AreEqual("WXYZ", _text.text);
        }

        [Test]
        public void 스크램블_재생_중에_텍스트를_바꾸면_새_텍스트를_덮어쓰지_않는다()
        {
            var reveal = Add(UIAnimProperty.TextReveal, 0f, Key(0f, Vector4.zero), Key(1f, new Vector4(1, 0)));
            reveal.tracks[0].scramble = UIAnimScramble.Lowercase;
            _anim.Prepare(reveal.name, UIAnimInterrupt.Cancel, null);
            UIAnimation.Evaluate(reveal, 0.5f);

            _text.text = "WXYZ";   // 다시 재생하지 않고 바꿈
            UIAnimation.Evaluate(reveal, 0.75f);
            StringAssert.StartsWith("WXY", _text.text);
            UIAnimation.Evaluate(reveal, 1f);
            Assert.AreEqual("WXYZ", _text.text);
        }

        [Test]
        public void CharacterSpacing_트랙은_자간을_움직이고_원래_값으로_돌아온다()
        {
            var spread = Add(UIAnimProperty.CharacterSpacing, 0f, Key(0f, Vector4.zero), Key(1f, new Vector4(12, 0)));

            UIAnimation.Evaluate(spread, 0.5f);
            Assert.AreEqual(6f, _text.characterSpacing, 1e-4f);
            UIAnimation.Evaluate(spread, 1f);
            Assert.AreEqual(12f, _text.characterSpacing, 1e-4f);
        }

        [Test]
        public void 숫자_트랙은_글자의_첫_숫자를_세고_끝값은_코드가_정한다()
        {
            _text.text = "21/100";
            var state = new UIAnimState
            {
                name = "Count",
                tracks = { new UIAnimTrack { target = _text, property = UIAnimProperty.TextNumber, start = UIAnimStart.Current,
                    keys = new List<UIAnimKey> { Key(0f, Vector4.zero), Key(1f, new Vector4(9999, 0)) } } },
            };
            _anim.states.Add(state);
            _anim.SetEndValue("Count", _text, UIAnimProperty.TextNumber, 1021);
            _anim.Prepare("Count", UIAnimInterrupt.Cancel, null);

            Assert.AreEqual("21/100", _text.text, "지금 보이는 숫자에서 시작");
            UIAnimation.Evaluate(state, 0.5f);
            Assert.AreEqual("521/100", _text.text);
            UIAnimation.Evaluate(state, 1f);
            Assert.AreEqual("1,021/100", _text.text, "코드가 정한 끝값, 천 단위 쉼표, 나머지 글자는 그대로");
            Assert.AreEqual(1021f, UIAnimation.Read(_text, UIAnimProperty.TextNumber).x, "쉼표가 있어도 읽음");

            _text.text = "";
            UIAnimation.Write(_text, UIAnimProperty.TextNumber, new Vector4(7, 0));
            Assert.AreEqual("7", _text.text, "숫자가 없으면 숫자만");
        }
    }
}
