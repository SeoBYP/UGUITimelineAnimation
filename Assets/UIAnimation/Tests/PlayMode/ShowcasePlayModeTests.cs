using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>The Showcase sample (Assets/UIAnimation/Samples/Showcase) in Play mode, in each of its three themes.</summary>
    public sealed class ShowcasePlayModeTests
    {
        const string ScenePath = "Assets/UIAnimation/Samples/Showcase/Scenes/Showcase.unity";
        static readonly string[] Themes = { "Casual", "SciFi", "Fantasy" };
        UIAnimPageContainer _pages;
        UIAnimModalContainer _modals;

        IEnumerator Load()
        {
#if UNITY_EDITOR
            if (!System.IO.File.Exists(ScenePath)) Assert.Ignore("Showcase 샘플이 없음");
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("에디터 전용");
#endif
            yield return null;
            _pages = Object.FindAnyObjectByType<UIAnimPageContainer>();
            _modals = Object.FindAnyObjectByType<UIAnimModalContainer>();
            yield return WaitUntilIdle();
        }

        static IEnumerator WaitUntilIdle(float max = 8f)
        {
            var end = Time.realtimeSinceStartup + max;
            yield return null;
            while (UIAnimScreenContainer.AnyInTransition && Time.realtimeSinceStartup < end) yield return null;
            yield return null;
        }

        static IEnumerator WaitStatesDone(UIAnimation anim, float max = 8f)
        {
            var end = Time.realtimeSinceStartup + max;
            while (anim.states.Any(s => anim.IsPlaying(s.name)) && Time.realtimeSinceStartup < end) yield return null;
        }

        UIAnimScreen Lobby => _pages.Current;
        static Button Named(Component root, string name) => root.GetComponentsInChildren<Button>(true).First(b => b.name == name);

        IEnumerator SwitchTheme(int index)
        {
            Named(Lobby, "THEMETab").onClick.Invoke();
            yield return WaitUntilIdle();
            var sheets = Lobby.GetComponentInChildren<UIAnimSheetContainer>();
            Named(sheets.Active, Themes[index] + "Button").onClick.Invoke();
            yield return WaitUntilIdle();
        }

        [UnityTest]
        public IEnumerator 테마마다_로비가_바뀌고_탭_인디케이터가_탭_가운데로_간다()
        {
            yield return Load();
            for (var theme = 0; theme < Themes.Length; theme++)
            {
                if (theme > 0) yield return SwitchTheme(theme);
                Assert.AreEqual(1, _pages.Pages.Count, "로비는 기록 없이 교체");
                Assert.AreEqual(Themes[theme].ToUpperInvariant() + " THEME", Lobby.transform.Find("Theme").GetComponent<TMP_Text>().text);
                Assert.AreEqual(1f, Lobby.Group.alpha, 0.001f, Themes[theme]);

                var indicator = (RectTransform)Lobby.transform.Find("TabBar/Indicator");
                var sheets = Lobby.GetComponentInChildren<UIAnimSheetContainer>();
                foreach (var (tab, index) in new[] { ("SHOPTab", 1), ("HOMETab", 0) })
                {
                    Named(Lobby, tab).onClick.Invoke();
                    yield return WaitUntilIdle();
                    yield return WaitStatesDone(Lobby.Animation);
                    Assert.AreEqual(index, sheets.ActiveIndex);
                    Assert.AreEqual((index + 0.5f) / 3f, indicator.anchorMin.x, 0.001f, $"{Themes[theme]} {tab}");
                    Assert.AreEqual(1f, sheets.Active.Group.alpha, 0.001f);
                    Assert.AreEqual(0f, sheets.Active.Rect.anchoredPosition.x, 0.5f);
                }
            }
        }

        [UnityTest]
        public IEnumerator 상점_아이템_아이콘이_상세_화면으로_이어지고_닫으면_카드로_돌아간다()
        {
            yield return Load();
            for (var theme = 0; theme < Themes.Length; theme++)
            {
                if (theme > 0) yield return SwitchTheme(theme);
                Named(Lobby, "SHOPTab").onClick.Invoke();
                yield return WaitUntilIdle();
                var card = Lobby.GetComponentInChildren<UIAnimSheetContainer>().Active.transform.Find("Card3");
                var cardIcon = (RectTransform)card.Find("Icon");
                var cardCenter = cardIcon.TransformPoint(cardIcon.rect.center);

                card.GetComponent<Button>().onClick.Invoke();
                yield return null;
                var detail = _modals.Current;
                var icon = (RectTransform)detail.transform.Find("Icon");
                Assert.IsFalse(cardIcon.GetComponent<Image>().enabled, "카드의 아이콘은 상세로 넘어감");
                Assert.AreSame(cardIcon.GetComponent<Image>().sprite, icon.GetComponent<Image>().sprite);
                Assert.Less(icon.rect.width, 300f, $"{Themes[theme]}: 처음엔 카드 아이콘 크기 근처");
                Assert.AreEqual("Gem", detail.transform.Find("Panel/Name").GetComponent<TMP_Text>().text);
                yield return WaitUntilIdle();
                Assert.Less(Vector2.Distance(new Vector2(380, 380), icon.sizeDelta), 0.01f, "상세 자리 크기");
                Assert.Less(Vector2.Distance(new Vector2(-300, 20), icon.anchoredPosition), 0.01f);

                Named(detail, "CloseButton").onClick.Invoke();
                var end = Time.realtimeSinceStartup + 3f;
                var last = Vector3.zero;
                while (detail != null && Time.realtimeSinceStartup < end)
                {
                    last = icon.TransformPoint(icon.rect.center);
                    yield return null;
                }
                Assert.Less(Vector2.Distance(cardCenter, last), 8f, $"{Themes[theme]}: 카드 자리로 돌아감 (마지막 프레임)");
                yield return WaitUntilIdle();
                Assert.IsTrue(cardIcon.GetComponent<Image>().enabled, "카드에 다시 보임");
                Assert.AreEqual(0, _modals.Modals.Count);
            }
        }

        [UnityTest]
        public IEnumerator 보상_레벨업_팝업과_토스트가_끝_모습으로_끝난다()
        {
            yield return Load();
            for (var theme = 0; theme < Themes.Length; theme++)
            {
                if (theme > 0) yield return SwitchTheme(theme);
                var home = Lobby.GetComponentInChildren<UIAnimSheetContainer>().Active;

                Named(home, "RewardButton").onClick.Invoke();
                yield return WaitUntilIdle();
                var reward = _modals.Current;
                yield return WaitStatesDone(reward.Animation);
                for (var i = 0; i < 3; i++)
                {
                    var slot = (RectTransform)reward.transform.Find("Panel/Slot" + i);
                    Assert.AreEqual(1f, slot.GetComponent<CanvasGroup>().alpha, 0.001f, $"{Themes[theme]} slot {i}");
                    Assert.AreEqual(1f, slot.localScale.x, 0.001f);
                    Assert.AreEqual(-260 + i * 260, slot.anchoredPosition.x, 0.5f);
                }
                Assert.AreEqual(1f, reward.transform.Find("Panel/ClaimButton").GetComponent<CanvasGroup>().alpha, 0.001f);
                Named(reward, "ClaimButton").onClick.Invoke();
                yield return WaitUntilIdle();
                Assert.AreEqual(0, _modals.Modals.Count);

                Named(home, "LevelUpButton").onClick.Invoke();
                yield return WaitUntilIdle();
                var levelUp = _modals.Current;
                yield return WaitStatesDone(levelUp.Animation);
                Assert.AreEqual(1300f, ((RectTransform)levelUp.transform.Find("Banner")).sizeDelta.x, 0.5f, Themes[theme]);
                Assert.AreEqual(1f, levelUp.transform.Find("Star1").GetComponent<Image>().color.a, 0.001f);
                Assert.AreEqual(1f, levelUp.transform.Find("Star1").localScale.x, 0.001f);
                Named(levelUp, "OkButton").onClick.Invoke();
                yield return WaitUntilIdle();

                // Toast: in, held, then out the same way (its Show state reversed).
                var toast = Lobby.transform.Find("Toast");
                var group = toast.GetComponent<CanvasGroup>();
                Named(home, "ToastButton").onClick.Invoke();
                yield return new WaitForSeconds(1f);
                Assert.AreEqual(1f, group.alpha, 0.001f, "보임");
                Assert.AreEqual(-40f, ((RectTransform)toast).anchoredPosition.y, 0.5f);
                yield return new WaitForSeconds(2.5f);   // Fantasy: 0.7 in + 1.5 held + 0.7 out
                Assert.AreEqual(0f, group.alpha, 0.001f, $"{Themes[theme]}: 다시 숨음");
            }
        }
    }
}
