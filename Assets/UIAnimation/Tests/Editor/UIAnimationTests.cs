using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
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
        public void 키_3개면_가운데_키_값을_지난다()
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
        public void 키_하나면_현재_값에서_출발한다()
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
        public void 현재값에서_시작할_때_첫_키가_0초_뒤면_지연이_된다()
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
        public void 상대값은_움직인_뒤에도_원래_값_기준이다()
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
        public void 키의_Ease가_그_키로_도착하는_모양을_정한다()
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
        public void Cancel_새_상태는_자기_속성만_가져간다()
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
        public void Cancel_속성을_모두_뺏긴_상태는_멈춘다()
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
        public void Complete_중단된_상태를_끝_값으로_맞춘다()
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

        // ---------- 완료 대기 (PlayAsync) ----------

        UIAnimation.Playback PlayAwaitable(string name, out UniTask task, UIAnimInterrupt interrupt = UIAnimInterrupt.Cancel,
            CancellationToken ct = default)
        {
            var playback = _anim.Prepare(name, interrupt, null);
            task = UIAnimation.Await(playback, ct);
            return playback;
        }

        UIAnimState Fade(string name, float to) =>
            State(name, Track(_image, UIAnimProperty.GraphicAlpha, true, false, Key(1f, new Vector4(to, 0))));

        [Test]
        public void 끝까지_재생되면_대기가_완료된다()
        {
            _anim.states.Add(Fade("Close", 0f));
            var playback = PlayAwaitable("Close", out var task);
            Assert.AreEqual(UniTaskStatus.Pending, task.Status);

            playback.onComplete();   // LitMotion이 끝에 도달했을 때 부르는 콜백
            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
            Assert.IsFalse(_anim.IsPlaying("Close"));
        }

        [Test]
        public void 다른_상태가_속성을_모두_가져가면_대기가_취소된다()
        {
            // 팝업 Close 도중 Open: Close 뒤의 Destroy가 실행되면 안 된다.
            _anim.states.Add(Fade("Close", 0f));
            _anim.states.Add(Fade("Open", 1f));
            PlayAwaitable("Close", out var task);

            _anim.Prepare("Open", UIAnimInterrupt.Cancel, null);
            Assert.AreEqual(UniTaskStatus.Canceled, task.Status);
        }

        [Test]
        public void 같은_상태를_다시_재생하면_이전_대기는_취소된다()
        {
            _anim.states.Add(Fade("Close", 0f));
            PlayAwaitable("Close", out var first);
            PlayAwaitable("Close", out var second);

            Assert.AreEqual(UniTaskStatus.Canceled, first.Status);
            Assert.AreEqual(UniTaskStatus.Pending, second.Status);
        }

        [Test]
        public void Complete로_건너뛰면_끝_값이_되고_대기가_완료된다()
        {
            _anim.states.Add(Fade("Show", 1f));
            PlayAwaitable("Show", out var task);

            _anim.Complete("Show");
            Assert.AreEqual(1f, _image.color.a, 1e-4f);
            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
            Assert.IsFalse(_anim.IsPlaying("Show"));
        }

        [Test]
        public void Complete_인터럽트로_끝난_상태의_대기는_완료된다()
        {
            _anim.states.Add(Fade("Show", 1f));
            _anim.states.Add(Fade("Hide", 0f));
            PlayAwaitable("Show", out var task);

            _anim.Prepare("Hide", UIAnimInterrupt.Complete, null);
            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
        }

        [Test]
        public void 토큰이_취소되면_재생이_멈추고_대기가_취소된다()
        {
            _anim.states.Add(Fade("Close", 0f));
            using var cts = new CancellationTokenSource();
            PlayAwaitable("Close", out var task, ct: cts.Token);

            cts.Cancel();
            Assert.AreEqual(UniTaskStatus.Canceled, task.Status);
            Assert.IsFalse(_anim.IsPlaying("Close"));
        }

        [Test]
        public void 이미_취소된_토큰이면_재생하지_않는다()
        {
            _anim.states.Add(Fade("Close", 0f));
            var task = _anim.PlayAsync("Close", cancellationToken: new CancellationToken(true));
            Assert.AreEqual(UniTaskStatus.Canceled, task.Status);
            Assert.IsFalse(_anim.IsPlaying("Close"));
        }
    }
}
