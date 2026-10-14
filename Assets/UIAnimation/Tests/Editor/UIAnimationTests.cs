using System.Collections.Generic;
using System.Threading;
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

        static UIAnimKey Key(float time, Vector4 value) => new() { time = time, value = value, ease = UIAnimEase.Linear };

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
        public void 포인터에서_시작하면_누른_위치로_옮겨진다()
        {
            // Button2 ripple: the fill starts where the pointer pressed. No canvas: screen point (100, 50) = world (100, 50).
            var parent = new GameObject("Mask", typeof(RectTransform));
            try
            {
                var fill = new GameObject("Fill", typeof(RectTransform)).GetComponent<RectTransform>();
                fill.SetParent(parent.transform, false);
                var press = State("Press", new UIAnimTrack
                {
                    target = fill, property = UIAnimProperty.AnchoredPosition, start = UIAnimStart.Pointer, keys = { new UIAnimKey() },
                });
                var anim = parent.AddComponent<UIAnimation>();
                anim.states.Add(press);

                var pointer = new UnityEngine.EventSystems.PointerEventData(null) { position = new Vector2(100, 50) };
                anim.Prepare("Press", UIAnimInterrupt.Cancel, pointer);
                Assert.AreEqual(new Vector2(100, 50), fill.anchoredPosition, "누른 위치");

                fill.anchoredPosition = new Vector2(7, 7);
                anim.Prepare("Press", UIAnimInterrupt.Cancel, null);
                Assert.AreEqual(new Vector2(7, 7), fill.anchoredPosition, "포인터가 없으면(코드 호출) 현재 위치 그대로");
            }
            finally { Object.DestroyImmediate(parent); }
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

        UIAnimation.Playback PlayAwaitable(string name, out Awaitable task, UIAnimInterrupt interrupt = UIAnimInterrupt.Cancel,
            CancellationToken ct = default)
        {
            var playback = _anim.Prepare(name, interrupt, null);
            task = UIAnimation.Await(playback, ct);
            return playback;
        }

        // Unity's Awaitable has no status: completed or not, and whether getting the result throws (canceled).
        // Get the result only once per awaitable (it goes back to Unity's pool after that).
        static string Status(Awaitable a)
        {
            if (!a.GetAwaiter().IsCompleted) return "Pending";
            try { a.GetAwaiter().GetResult(); return "Succeeded"; }
            catch (System.OperationCanceledException) { return "Canceled"; }
        }

        static string Status<T>(Awaitable<T> a)
        {
            if (!a.GetAwaiter().IsCompleted) return "Pending";
            try { a.GetAwaiter().GetResult(); return "Succeeded"; }
            catch (System.OperationCanceledException) { return "Canceled"; }
        }

        UIAnimState Fade(string name, float to) =>
            State(name, Track(_image, UIAnimProperty.GraphicAlpha, true, false, Key(1f, new Vector4(to, 0))));

        [Test]
        public void 끝까지_재생되면_대기가_완료된다()
        {
            _anim.states.Add(Fade("Close", 0f));
            var playback = PlayAwaitable("Close", out var task);
            Assert.AreEqual("Pending", Status(task));

            UIAnimation.Finish(playback);   // 타이머가 끝에 도달했을 때 부르는 것
            Assert.AreEqual("Succeeded", Status(task));
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
            Assert.AreEqual("Canceled", Status(task));
        }

        [Test]
        public void 같은_상태를_다시_재생하면_이전_대기는_취소된다()
        {
            _anim.states.Add(Fade("Close", 0f));
            PlayAwaitable("Close", out var first);
            PlayAwaitable("Close", out var second);

            Assert.AreEqual("Canceled", Status(first));
            Assert.AreEqual("Pending", Status(second));
        }

        [Test]
        public void Complete로_건너뛰면_끝_값이_되고_대기가_완료된다()
        {
            _anim.states.Add(Fade("Show", 1f));
            PlayAwaitable("Show", out var task);

            _anim.Complete("Show");
            Assert.AreEqual(1f, _image.color.a, 1e-4f);
            Assert.AreEqual("Succeeded", Status(task));
            Assert.IsFalse(_anim.IsPlaying("Show"));
        }

        [Test]
        public void Complete_인터럽트로_끝난_상태의_대기는_완료된다()
        {
            _anim.states.Add(Fade("Show", 1f));
            _anim.states.Add(Fade("Hide", 0f));
            PlayAwaitable("Show", out var task);

            _anim.Prepare("Hide", UIAnimInterrupt.Complete, null);
            Assert.AreEqual("Succeeded", Status(task));
        }

        [Test]
        public void 토큰이_취소되면_재생이_멈추고_대기가_취소된다()
        {
            _anim.states.Add(Fade("Close", 0f));
            using var cts = new CancellationTokenSource();
            PlayAwaitable("Close", out var task, ct: cts.Token);

            cts.Cancel();
            Assert.AreEqual("Canceled", Status(task));
            Assert.IsFalse(_anim.IsPlaying("Close"));
        }

        [Test]
        public void 이미_취소된_토큰이면_재생하지_않는다()
        {
            _anim.states.Add(Fade("Close", 0f));
            var task = _anim.PlayAsync("Close", cancellationToken: new CancellationToken(true));
            Assert.AreEqual("Canceled", Status(task));
            Assert.IsFalse(_anim.IsPlaying("Close"));
        }

        [Test]
        public void FillAmount_트랙은_Image_채우기_비율을_움직인다()
        {
            _image.type = Image.Type.Filled;
            _image.fillAmount = 0f;
            var fill = State("Fill", Track(_image, UIAnimProperty.FillAmount, true, false, Key(1f, new Vector4(1, 0))));
            _anim.states.Add(fill);

            _anim.Prepare("Fill", UIAnimInterrupt.Cancel, null);
            UIAnimation.Evaluate(fill, 0.5f);
            Assert.AreEqual(0.5f, _image.fillAmount, 1e-4f);
            UIAnimation.Evaluate(fill, 1f);
            Assert.AreEqual(1f, _image.fillAmount, 1e-4f);
        }

        // ---------- 애니메이션 이벤트 ----------

        UIAnimState WithSignals(string name, params (float time, string name)[] events)
        {
            var state = Fade(name, 0f);
            foreach (var (time, e) in events) state.AddEvent(new UIAnimEventMarker { time = time, name = e });
            _anim.states.Add(state);
            return state;
        }

        List<string> Record()
        {
            var raised = new List<string>();
            _anim.EventRaised += (in UIAnimEventInfo e) => raised.Add($"{e.State}:{e.Name}");
            return raised;
        }

        [Test]
        public void 애니메이션_이벤트는_그_시간을_지날_때_한_번씩_발생한다()
        {
            WithSignals("Close", (0.3f, "A"), (0.6f, "B"));
            var raised = Record();
            var playback = _anim.Prepare("Close", UIAnimInterrupt.Cancel, null);

            UIAnimation.Step(playback, 0.2f);
            CollectionAssert.IsEmpty(raised);
            UIAnimation.Step(playback, 0.5f);
            UIAnimation.Step(playback, 0.5f);
            CollectionAssert.AreEqual(new[] { "Close:A" }, raised, "같은 시간에 두 번 지나도 한 번만");
            UIAnimation.Step(playback, 1f);
            CollectionAssert.AreEqual(new[] { "Close:A", "Close:B" }, raised);
        }

        [Test]
        public void 영초_이벤트는_재생을_시작하자마자_발생한다()
        {
            WithSignals("Open", (0f, "Start"));
            var raised = Record();
            _anim.Prepare("Open", UIAnimInterrupt.Cancel, null);
            CollectionAssert.AreEqual(new[] { "Open:Start" }, raised);
        }

        [Test]
        public void Complete로_건너뛰면_남은_이벤트가_순서대로_발생한다()
        {
            WithSignals("Close", (0.3f, "A"), (0.6f, "B"), (0.9f, "C"));
            var raised = Record();
            var playback = _anim.Prepare("Close", UIAnimInterrupt.Cancel, null);
            UIAnimation.Step(playback, 0.4f);

            _anim.Complete("Close");
            CollectionAssert.AreEqual(new[] { "Close:A", "Close:B", "Close:C" }, raised);
        }

        [Test]
        public void 취소되면_남은_이벤트는_발생하지_않는다()
        {
            WithSignals("Close", (0.3f, "A"), (0.6f, "B"));
            _anim.states.Add(Fade("Open", 1f));
            var raised = Record();
            var playback = _anim.Prepare("Close", UIAnimInterrupt.Cancel, null);
            UIAnimation.Step(playback, 0.4f);

            _anim.Prepare("Open", UIAnimInterrupt.Cancel, null);   // Open takes the alpha: Close is canceled
            CollectionAssert.AreEqual(new[] { "Close:A" }, raised);
        }

        [Test]
        public void WaitEventAsync는_그_이벤트가_오면_완료된다()
        {
            WithSignals("Close", (0.3f, "A"), (0.6f, "Impact"));
            var playback = _anim.Prepare("Close", UIAnimInterrupt.Cancel, null);
            var wait = _anim.WaitEventAsync("Impact");

            UIAnimation.Step(playback, 0.4f);
            Assert.AreEqual("Pending", Status(wait), "다른 이벤트(A)로는 끝나지 않는다");
            UIAnimation.Step(playback, 0.7f);
            Assert.AreEqual("Succeeded", Status(wait));
        }

        // ---------- 자식 상태 호출 ----------

        UIAnimation Child(out Image image)
        {
            var go = new GameObject("Child", typeof(RectTransform));
            go.transform.SetParent(_go.transform, false);
            image = go.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0);
            var child = go.AddComponent<UIAnimation>();
            child.states.Add(State("Show", Track(image, UIAnimProperty.GraphicAlpha, false, false, Key(0f, Vector4.zero), Key(1f, new Vector4(1, 0)))));
            return child;
        }

        UIAnimState Caller(UIAnimation child, float time)
        {
            var parent = State("Open");
            parent.childCalls.Add(new UIAnimChildCall { time = time, child = child, state = "Show" });
            _anim.states.Add(parent);
            return parent;
        }

        [Test]
        public void 부모가_지정한_시간에_자식_상태를_재생한다()
        {
            var child = Child(out _);
            Caller(child, 0.3f);
            var playback = _anim.Prepare("Open", UIAnimInterrupt.Cancel, null);

            UIAnimation.Step(playback, 0.2f);
            Assert.IsFalse(child.IsPlaying("Show"));
            UIAnimation.Step(playback, 0.4f);
            Assert.IsTrue(child.IsPlaying("Show"));
        }

        [Test]
        public void 부모_길이는_자식_상태가_끝나는_시간까지_포함한다()
        {
            var parent = Caller(Child(out _), 0.5f);
            Assert.AreEqual(1.5f, parent.Duration, 1e-4f, "호출 0.5초 + 자식 길이 1초");
        }

        [Test]
        public void 부모를_Complete하면_자식도_끝_값이_된다()
        {
            var child = Child(out var image);
            Caller(child, 0.3f);
            var playback = _anim.Prepare("Open", UIAnimInterrupt.Cancel, null);
            UIAnimation.Step(playback, 0.2f);   // 아직 호출 전

            _anim.Complete("Open");
            Assert.AreEqual(1f, image.color.a, 1e-4f);
            Assert.IsFalse(child.IsPlaying("Show"));
        }

        [Test]
        public void 미리보기는_자식_상태를_호출_후_지난_시간으로_그린다()
        {
            var child = Child(out var image);
            var parent = Caller(child, 0.5f);

            UIAnimation.ResolveTree(parent);
            UIAnimation.EvaluateTree(parent, 0.8f);
            Assert.AreEqual(0.3f, image.color.a, 1e-4f);
        }

        [Test]
        public void 부모와_자식이_서로_호출해도_무한히_반복하지_않는다()
        {
            var child = Child(out _);
            Caller(child, 0f);
            child.states[0].childCalls.Add(new UIAnimChildCall { time = 0f, child = _anim, state = "Open" });

            Assert.DoesNotThrow(() => _ = _anim.states[0].Duration);
            Assert.DoesNotThrow(() => _anim.Prepare("Open", UIAnimInterrupt.Cancel, null));
        }

        // ---------- 커스텀 struct 이벤트와 인스펙터 리스너 ----------

        UIAnimEventMarker WithData<T>(string state, float time, string name, T data) where T : struct
        {
            var marker = new UIAnimEventMarker
            {
                time = time, name = name,
                dataType = UIAnimEventMarker.TypeKey(typeof(T)), dataJson = JsonUtility.ToJson(data),
            };
            var s = Fade(state, 0f);
            s.AddEvent(marker);
            _anim.states.Add(s);
            return marker;
        }

        [Test]
        public void 커스텀_struct_데이터를_AddListener로_받는다()
        {
            WithData("Hit", 0.5f, "Impact", new TestHitEvent { damage = 12f, sfx = "hit" });
            TestHitEvent received = default;
            var count = 0;
            _anim.AddListener((in UIAnimEventInfo e, in TestHitEvent hit) =>
            {
                received = hit;
                count++;
            });
            var playback = _anim.Prepare("Hit", UIAnimInterrupt.Cancel, null);

            UIAnimation.Step(playback, 0.6f);
            Assert.AreEqual(1, count);
            Assert.AreEqual(12f, received.damage, 1e-4f);
            Assert.AreEqual("hit", received.sfx);
        }

        [Test]
        public void 다른_struct를_기다리는_리스너는_호출되지_않는다()
        {
            WithData("Hit", 0.5f, "Impact", new TestHitEvent { damage = 1f });
            var other = 0;
            _anim.AddListener((in UIAnimEventInfo e, in TestSoundEvent sound) => other++);
            var playback = _anim.Prepare("Hit", UIAnimInterrupt.Cancel, null);

            UIAnimation.Step(playback, 0.6f);
            Assert.AreEqual(0, other);
        }

        [Test]
        public void 리스너를_Dispose하면_더_이상_받지_않는다()
        {
            WithData("Hit", 0.5f, "Impact", new TestHitEvent { damage = 1f });
            var count = 0;
            var subscription = _anim.AddListener((in UIAnimEventInfo e, in TestHitEvent hit) => count++);
            subscription.Dispose();
            var playback = _anim.Prepare("Hit", UIAnimInterrupt.Cancel, null);

            UIAnimation.Step(playback, 0.6f);
            Assert.AreEqual(0, count);
        }

        [Test]
        public void 이벤트_정보로_struct_데이터를_꺼낼_수_있다()
        {
            WithData("Hit", 0.5f, "Impact", new TestHitEvent { damage = 7f });
            float damage = 0f;
            var hasSound = true;
            _anim.EventRaised += (in UIAnimEventInfo e) =>
            {
                if (e.TryGetData(out TestHitEvent hit)) damage = hit.damage;
                hasSound = e.TryGetData(out TestSoundEvent _);
            };
            var playback = _anim.Prepare("Hit", UIAnimInterrupt.Cancel, null);

            UIAnimation.Step(playback, 0.6f);
            Assert.AreEqual(7f, damage, 1e-4f);
            Assert.IsFalse(hasSound);
        }

        [Test]
        public void 인스펙터_리스너는_이름이_같은_이벤트에_UnityEvent를_호출한다()
        {
            WithSignals("Close", (0.3f, "A"), (0.6f, "B"));
            var listener = _go.AddComponent<UIAnimEventListener>();
            var count = 0;
            var binding = new UIAnimEventListener.Binding { eventName = "B" };
            binding.onRaised.AddListener(() => count++);
            listener.bindings.Add(binding);
            listener.OnEnable();   // EditMode tests do not run OnEnable for added components
            var playback = _anim.Prepare("Close", UIAnimInterrupt.Cancel, null);

            UIAnimation.Step(playback, 0.4f);
            Assert.AreEqual(0, count, "A는 연결되지 않음");
            UIAnimation.Step(playback, 0.7f);
            Assert.AreEqual(1, count);
            listener.OnDisable();
        }

        // ---------- 사운드 ----------

        UIAnimState WithSound(float time, float clipSeconds)
        {
            var state = Fade("Close", 0f);
            var clip = AudioClip.Create("Test", Mathf.RoundToInt(44100 * clipSeconds), 1, 44100, false);
            state.AddSound(new UIAnimSound { time = time, clip = clip });
            _anim.states.Add(state);
            return state;
        }

        [Test]
        public void 상태_길이는_사운드가_끝나는_시간까지_포함한다()
        {
            var state = WithSound(0.5f, 2f);
            Assert.AreEqual(2.5f, state.Duration, 1e-3f, "사운드 0.5초 + 클립 2초");
        }

        [Test]
        public void 사운드는_AudioSource_없이_공용_2D_소스로_재생한다()
        {
            WithSound(0.3f, 1f);
            var played = new List<AudioSource>();
            UIAnimation.SoundPlayed = (source, _) => played.Add(source);
            try
            {
                var playback = _anim.Prepare("Close", UIAnimInterrupt.Cancel, null);
                UIAnimation.Step(playback, 0.2f);
                Assert.AreEqual(0, played.Count, "아직 재생 전");

                UIAnimation.Step(playback, 0.4f);
                Assert.AreEqual(1, played.Count, "시간을 지나면 한 번 재생");
                Assert.AreNotEqual(_go, played[0].gameObject, "공용 소스로 재생");
                Assert.AreEqual(0f, played[0].spatialBlend, "2D");
                Assert.IsNull(_go.GetComponent<AudioSource>(), "UI 오브젝트에 AudioSource를 붙이지 않음");
            }
            finally { UIAnimation.SoundPlayed = null; }
        }

        [Test]
        public void Complete로_건너뛴_사운드는_재생하지_않는다()
        {
            WithSound(0.3f, 1f);
            var playback = _anim.Prepare("Close", UIAnimInterrupt.Cancel, null);
            UIAnimation.Step(playback, 0.1f);

            var played = 0;
            UIAnimation.SoundPlayed = (_, _) => played++;
            try { _anim.Complete("Close"); }
            finally { UIAnimation.SoundPlayed = null; }
            Assert.AreEqual(0, played);
        }

        // ---------- 현재 Prefab 오버라이드 ----------

        UIAnimState FadeTo(Image image, float to, float seconds = 1f) =>
            State("Show", Track(image, UIAnimProperty.GraphicAlpha, false, false, Key(0f, Vector4.zero), Key(seconds, new Vector4(to, 0))));

        static float AlphaAtEnd(UIAnimation anim)
        {
            var playback = anim.Prepare("Show", UIAnimInterrupt.Cancel, null);
            UIAnimation.Step(playback, 10f);
            return ((Image)playback.state.tracks[0].target).color.a;
        }

        [Test]
        public void 바깥_Prefab의_오버라이드가_자식_상태를_대신한다()
        {
            var child = Child(out var image);
            _anim.overrides.Add(new UIAnimOverride { child = child, state = "Show", tracks = FadeTo(image, 0.5f).tracks });

            Assert.AreEqual(0.5f, AlphaAtEnd(child), 1e-4f);
            Assert.AreEqual(1f, child.states[0].tracks[0].keys[1].value.x, "자식 원본 상태는 그대로");
        }

        [Test]
        public void 오버라이드한_Prefab_밖의_같은_자식은_원래_상태를_재생한다()
        {
            var child = Child(out var image);
            child.transform.SetParent(null, false);   // no longer inside the popup
            _anim.overrides.Add(new UIAnimOverride { child = child, state = "Show", tracks = FadeTo(image, 0.5f).tracks });

            Assert.AreEqual(1f, AlphaAtEnd(child), 1e-4f);
            Object.DestroyImmediate(child.gameObject);
        }

        [Test]
        public void 가장_바깥_Prefab의_오버라이드가_우선한다()
        {
            var middle = new GameObject("Middle", typeof(RectTransform)).AddComponent<UIAnimation>();
            middle.transform.SetParent(_go.transform, false);
            var child = Child(out var image);
            child.transform.SetParent(middle.transform, false);
            middle.overrides.Add(new UIAnimOverride { child = child, state = "Show", tracks = FadeTo(image, 0.3f).tracks });
            _anim.overrides.Add(new UIAnimOverride { child = child, state = "Show", tracks = FadeTo(image, 0.5f).tracks });

            Assert.AreEqual(0.5f, AlphaAtEnd(child), 1e-4f);
        }

        [Test]
        public void 자식_호출_길이는_오버라이드한_상태의_길이를_쓴다()
        {
            var child = Child(out var image);
            var parent = Caller(child, 0.5f);
            _anim.overrides.Add(new UIAnimOverride { child = child, state = "Show", tracks = FadeTo(image, 1f, 2f).tracks });

            Assert.AreEqual(2.5f, parent.Duration, 1e-4f, "호출 0.5초 + 오버라이드 길이 2초");
        }

        [Test]
        public void 오버라이드하지_않은_트랙은_자식_원본을_따라간다()
        {
            var child = Child(out var image);
            var scale = Track(child.transform, UIAnimProperty.Scale, false, false, Key(0f, Vector3.one), Key(1f, Vector3.one * 2f));
            child.states[0].tracks.Add(scale);
            _anim.overrides.Add(new UIAnimOverride { child = child, state = "Show", tracks = FadeTo(image, 0.5f).tracks });

            var effective = child.EffectiveState("Show");
            Assert.AreEqual(2, effective.tracks.Count);
            Assert.AreNotSame(child.states[0].tracks[0], effective.tracks[0], "Alpha는 오버라이드");
            Assert.AreSame(scale, effective.tracks[1], "Scale은 원본 그대로(같은 트랙)");

            scale.keys[1].value = Vector3.one * 3f;   // the child prefab changes later
            UIAnimation.Step(child.Prepare("Show", UIAnimInterrupt.Cancel, null), 10f);
            Assert.AreEqual(3f, child.transform.localScale.x, 1e-4f, "원본 변경이 따라옴");
            Assert.AreEqual(0.5f, image.color.a, 1e-4f, "오버라이드한 트랙은 유지");
        }

        [Test]
        public void 예전_형식의_전체_상태_오버라이드는_트랙_오버라이드로_옮겨진다()
        {
            var o = JsonUtility.FromJson<UIAnimOverride>(
                "{\"state\":\"Show\",\"replacement\":{\"name\":\"Show\",\"tracks\":[{\"property\":4,\"keys\":[{\"time\":0.5}]}]}}");
            Assert.AreEqual(1, o.tracks.Count);
            Assert.AreEqual(0.5f, o.tracks[0].keys[0].time, 1e-4f);
        }

        [Test]
        public void 상태_복사본은_참조는_같고_값은_독립적이다()
        {
            var child = Child(out var image);
            var source = child.states[0];
            var copy = source.Clone();

            Assert.AreSame(image, copy.tracks[0].target, "대상 오브젝트 참조 유지");
            copy.tracks[0].keys[1].value = new Vector4(0.2f, 0);
            Assert.AreEqual(1f, source.tracks[0].keys[1].value.x, "복사본을 고쳐도 원본은 그대로");
        }

        [Test]
        public void 예전_형식의_이벤트와_사운드는_불러올_때_트랙으로_옮겨진다()
        {
            var state = JsonUtility.FromJson<UIAnimState>(
                "{\"name\":\"Open\",\"events\":[{\"time\":0.5,\"name\":\"Shown\"}],\"sounds\":[{\"time\":0.1,\"volume\":0.5}]}");

            Assert.AreEqual(1, state.eventTracks.Count);
            Assert.AreEqual("Shown", state.eventTracks[0].events[0].name);
            Assert.AreEqual(1, state.soundTracks.Count);
            Assert.AreEqual(0.5f, state.soundTracks[0].sounds[0].volume, 1e-4f);
        }
    }

    [UIAnimEvent("테스트 타격")]
    public struct TestHitEvent
    {
        public float damage;
        public string sfx;
    }

    [UIAnimEvent]
    public struct TestSoundEvent
    {
        public string clip;
    }
}
