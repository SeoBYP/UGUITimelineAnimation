using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>Page, modal and sheet containers on the real player loop: order of screens, transitions, lifecycle, input lock.</summary>
    public sealed class ScreenContainerPlayModeTests
    {
        readonly List<Object> _destroy = new();
        RectTransform _canvas;
        GameObject _templates;

        [SetUp]
        public void SetUp()
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas = (RectTransform)canvas.transform;
            _templates = new GameObject("Templates");
            _templates.SetActive(false);   // templates never run; their copies do
            _destroy.Add(canvas);
            _destroy.Add(_templates);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _destroy) Object.Destroy(o);
            _destroy.Clear();
        }

        T Container<T>() where T : UIAnimScreenContainer
        {
            var go = new GameObject(typeof(T).Name, typeof(RectTransform));
            go.transform.SetParent(_canvas, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            return go.AddComponent<T>();
        }

        UIAnimScreen Screen(string name, params UIAnimState[] states)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_templates.transform, false);
            var screen = go.AddComponent<UIAnimScreen>();
            screen.Animation.states.AddRange(states);
            return screen;
        }

        static IEnumerator Wait(Awaitable task)
        {
            while (!task.GetAwaiter().IsCompleted) yield return null;
            task.GetAwaiter().GetResult();
        }

        static IEnumerator Wait<T>(Awaitable<T> task)
        {
            while (!task.GetAwaiter().IsCompleted) yield return null;
            task.GetAwaiter().GetResult();
        }

        static float X(UIAnimScreen s) => s.Rect.anchoredPosition.x;

        [UnityTest]
        public IEnumerator 페이지는_밀어_넣고_되돌아가며_화면_폭만큼_미끄러진다()
        {
            var pages = Container<UIAnimPageContainer>();
            var width = ((RectTransform)pages.transform).rect.width;
            yield return Wait(pages.Push(Screen("A"), animated: false));
            var a = pages.Current;

            var push = pages.Push(Screen("B"));
            yield return null;
            var b = pages.Current;
            Assert.IsTrue(UIAnimScreenContainer.AnyInTransition, "전환 중");
            Assert.IsFalse(pages.GetComponent<CanvasGroup>().blocksRaycasts, "전환 중 입력 막힘");
            Assert.Greater(X(b), 0f, "B는 오른쪽에서 들어옴");
            Assert.LessOrEqual(X(b), width + 0.5f, "한 화면 폭 이내");
            Assert.Less(X(a), 0.5f, "A는 왼쪽으로 나감");
            yield return Wait(push);
            Assert.AreEqual(0f, X(b), 0.5f);
            Assert.IsFalse(a.gameObject.activeSelf, "나간 페이지는 꺼짐");
            Assert.IsTrue(pages.GetComponent<CanvasGroup>().blocksRaycasts, "끝나면 입력 복원");
            Assert.AreEqual(2, pages.Pages.Count);

            yield return Wait(pages.Pop());
            Assert.AreEqual(1, pages.Pages.Count);
            Assert.AreSame(a, pages.Current);
            Assert.IsTrue(a.gameObject.activeSelf);
            Assert.AreEqual(0f, X(a), 0.5f, "A가 왼쪽에서 제자리로");
            yield return null;
            Assert.IsTrue(b == null, "꺼낸 페이지는 파괴");
        }

        [UnityTest]
        public IEnumerator stack_false_페이지는_다음_페이지가_들어오면_기록에서_빠진다()
        {
            var pages = Container<UIAnimPageContainer>();
            yield return Wait(pages.Push(Screen("Top"), animated: false));
            yield return Wait(pages.Push(Screen("Loading"), animated: false, stack: false));
            var loading = pages.Current;
            yield return Wait(pages.Push(Screen("Home"), animated: false));
            CollectionAssert.AreEqual(new[] { "Top(Clone)", "Home(Clone)" }, new[] { pages.Pages[0].name, pages.Pages[1].name });
            yield return null;
            Assert.IsTrue(loading == null, "Loading 파괴");
            yield return Wait(pages.Pop(animated: false));
            Assert.AreEqual("Top(Clone)", pages.Current.name, "Home → Top (Loading 건너뜀)");
        }

        [UnityTest]
        public IEnumerator renderingOrder가_높은_페이지가_위에_그려진다()
        {
            var pages = Container<UIAnimPageContainer>();
            var loading = Screen("Loading");
            loading.renderingOrder = 100;
            var home = Screen("Home");
            home.renderingOrder = 10;
            yield return Wait(pages.Push(loading, animated: false, stack: false));
            var task = pages.Push(home);
            yield return null;
            Assert.Greater(pages.transform.Find("Loading(Clone)").GetSiblingIndex(), pages.transform.Find("Home(Clone)").GetSiblingIndex(),
                "Home이 들어오는 동안 Loading이 위");
            yield return Wait(task);
        }

        sealed class Recorder : MonoBehaviour, IUIAnimScreenLifecycle
        {
            public static readonly List<string> Log = new();
            public float delay;

            public async Awaitable WillEnter(UIAnimTransition t)
            {
                Log.Add($"{name}.WillEnter");
                if (delay > 0f) await Awaitable.WaitForSecondsAsync(delay);
                Log.Add($"{name}.WillEnter done");
            }

            public void DidEnter(UIAnimTransition t) => Log.Add($"{name}.DidEnter partner={(t.Partner != null ? t.Partner.name : "-")}");
            public Awaitable WillExit(UIAnimTransition t)
            {
                Log.Add($"{name}.WillExit");
                return UIAnimScreen.Completed();
            }

            public void DidExit(UIAnimTransition t) => Log.Add($"{name}.DidExit");
        }

        [UnityTest]
        public IEnumerator 생명주기_Will은_기다린_뒤_애니메이션이_시작된다()
        {
            Recorder.Log.Clear();
            var pages = Container<UIAnimPageContainer>();
            var a = Screen("A");
            a.gameObject.AddComponent<Recorder>();
            var b = Screen("B");
            b.gameObject.AddComponent<Recorder>().delay = 0.3f;
            yield return Wait(pages.Push(a, animated: false));
            Recorder.Log.Clear();

            var start = Time.realtimeSinceStartup;
            var task = pages.Push(b);
            yield return new WaitForSeconds(0.15f);
            var current = pages.Current;
            Assert.AreEqual(0f, current.Group.alpha, 0.01f, "Will 기다리는 동안 숨김");
            yield return Wait(task);
            TestContext.WriteLine($"Push 전체 {Time.realtimeSinceStartup - start:0.00}s: {string.Join(" → ", Recorder.Log)}");
            Assert.GreaterOrEqual(Time.realtimeSinceStartup - start, 0.55f, "Will 0.3초 + 전환 0.3초");
            CollectionAssert.AreEqual(new[]
            {
                "B(Clone).WillEnter", "A(Clone).WillExit", "B(Clone).WillEnter done", "A(Clone).DidExit", "B(Clone).DidEnter partner=A(Clone)",
            }, Recorder.Log);
        }

        [UnityTest]
        public IEnumerator 화면에_같은_이름의_상태가_있으면_기본_대신_그것을_쓴다()
        {
            var pages = Container<UIAnimPageContainer>();
            var fade = Screen("Fade");
            var alpha = new UIAnimTrack { target = fade.Group, property = UIAnimProperty.CanvasGroupAlpha, start = UIAnimStart.KeyValue };
            alpha.keys.Add(new UIAnimKey { time = 0f, value = Vector4.zero });
            alpha.keys.Add(new UIAnimKey { time = 0.3f, value = new Vector4(1, 0), ease = UIAnimEase.Linear });
            fade.Animation.states.Add(new UIAnimState { name = "PushEnter", tracks = { alpha } });

            var task = pages.Push(fade);
            yield return new WaitForSeconds(0.1f);
            var page = pages.Current;
            Assert.AreEqual(0f, X(page), 0.5f, "미끄러지지 않음 (기본 아님)");
            Assert.Less(page.Group.alpha, 0.9f, "페이드 중");
            yield return Wait(task);
            Assert.AreEqual(1f, page.Group.alpha, 0.01f);
        }

        [UnityTest]
        public IEnumerator 모달은_배경과_함께_열리고_닫힌다()
        {
            var modals = Container<UIAnimModalContainer>();
            var task = modals.Push(Screen("Dialog"));
            yield return null;
            var modal = modals.Current;
            Assert.Less(modal.Rect.localScale.x, 1f, "0.3배에서 커짐");
            Assert.AreEqual(2, modals.transform.childCount, "배경 + 모달");
            var backdrop = modals.transform.GetChild(0).GetComponent<UIAnimScreen>();
            yield return Wait(task);
            Assert.AreEqual(1f, modal.Rect.localScale.x, 0.01f);
            Assert.AreEqual(1f, backdrop.Group.alpha, 0.01f, "배경 보임");
            Assert.AreEqual(0.749f, backdrop.GetComponent<Image>().color.a, 0.01f, "검정 75 %");

            yield return Wait(modals.Pop());
            yield return null;
            Assert.AreEqual(0, modals.transform.childCount, "모달과 배경 파괴");
        }

        [UnityTest]
        public IEnumerator 시트는_높은_번호로_가면_Enter_낮은_번호로_가면_EnterBackward()
        {
            var sheets = Container<UIAnimSheetContainer>();
            UIAnimState SlideIn(UIAnimScreen s, string name, float fromX)
            {
                var track = new UIAnimTrack { target = s.Rect, property = UIAnimProperty.AnchoredPosition, start = UIAnimStart.KeyValue, sizeRelative = true };
                track.keys.Add(new UIAnimKey { time = 0f, value = new Vector4(fromX, 0) });
                track.keys.Add(new UIAnimKey { time = 0.2f, value = Vector4.zero, ease = UIAnimEase.OutQuart });
                return new UIAnimState { name = name, tracks = { track } };
            }
            for (var i = 0; i < 3; i++)
            {
                var tab = Screen("Tab" + i);
                tab.Animation.states.Add(SlideIn(tab, "Enter", 1f));            // forward: from the right
                tab.Animation.states.Add(SlideIn(tab, "EnterBackward", -1f));   // backward: from the left
                sheets.Register(tab);
            }
            yield return Wait(sheets.Show(0, animated: false));
            var task = sheets.Show(2);
            yield return null;
            Assert.Greater(X(sheets.Active), 0f, "0 → 2: 오른쪽에서");
            yield return Wait(task);
            Assert.IsFalse(sheets.Sheets[0].gameObject.activeSelf);
            task = sheets.Show(1);
            yield return null;
            Assert.Less(X(sheets.Active), 0f, "2 → 1: 왼쪽에서 (EnterBackward)");
            yield return Wait(task);
            Assert.AreEqual(1, sheets.ActiveIndex);
            Assert.AreEqual(0f, X(sheets.Active), 0.5f);
        }
    }
}
