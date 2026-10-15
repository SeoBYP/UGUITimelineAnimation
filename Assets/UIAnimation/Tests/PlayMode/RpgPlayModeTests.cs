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
    /// <summary>The RPG sample (Assets/UIAnimation/Samples/RPG) in Play mode: each screen opens, moves as built, and closes.</summary>
    public sealed class RpgPlayModeTests
    {
        const string ScenePath = "Assets/UIAnimation/Samples/RPG/Scenes/RPG.unity";
        UIAnimPageContainer _pages;
        UIAnimModalContainer _modals;

        IEnumerator Load()
        {
#if UNITY_EDITOR
            if (!System.IO.File.Exists(ScenePath)) Assert.Ignore("RPG 샘플이 없음");
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("에디터 전용");
#endif
            yield return null;
            _pages = Object.FindAnyObjectByType<UIAnimPageContainer>();
            _modals = Object.FindAnyObjectByType<UIAnimModalContainer>();
            yield return Idle();
        }

        static IEnumerator Idle(float extra = 0.1f)
        {
            yield return null;
            var end = Time.realtimeSinceStartup + 8f;
            while (UIAnimScreenContainer.AnyInTransition && Time.realtimeSinceStartup < end) yield return null;
            yield return new WaitForSeconds(extra);
        }

        UIAnimScreen Page => _pages.Current;
        UIAnimScreen Modal => _modals.Current;
        static Button Named(Component root, string name) => root.GetComponentsInChildren<Button>(true).First(b => b.name == name);
        static Vector3 CenterOf(RectTransform rt) => rt.TransformPoint(rt.rect.center);
        static string Text(Component root, string path) => root.transform.Find(path).GetComponent<TMP_Text>().text;

        IEnumerator Open(string menu)
        {
            Named(Page, menu + "Button").onClick.Invoke();
            yield return Idle();
        }

        [UnityTest]
        public IEnumerator 로비의_메뉴마다_화면이_열리고_뒤로_돌아온다()
        {
            yield return Load();
            var lobby = Page;
            Assert.AreEqual(1f, lobby.transform.Find("Content").GetComponent<CanvasGroup>().alpha, 0.001f, "로비 등장 끝");
            foreach (var (menu, page) in new[] { ("HEROES", "Heroes"), ("BAG", "Bag"), ("SUMMON", "Summon"), ("ADVENTURE", "Adventure"), ("QUESTS", "Quests") })
            {
                yield return Open(menu);
                Assert.AreEqual(2, _pages.Pages.Count, menu);
                StringAssert.StartsWith(page, Page.name);
                Assert.AreEqual(1f, Page.transform.Find("Content").GetComponent<CanvasGroup>().alpha, 0.001f);
                Assert.AreEqual(1f, Page.transform.localScale.x, 0.001f);
                Named(Page, "BackButton").onClick.Invoke();
                yield return Idle();
                Assert.AreSame(lobby, Page, menu + " → 로비");
                Assert.AreEqual(1f, lobby.transform.localScale.x, 0.001f, "로비 원래 크기로");
            }
        }

        [UnityTest]
        public IEnumerator 영웅_초상이_카드에서_상세_자리로_이어지고_뒤로_가면_카드로_돌아간다()
        {
            yield return Load();
            yield return Open("HEROES");
            var card = Page.GetComponentsInChildren<Button>().First(b => b.name == "Body");
            var cardPortrait = (RectTransform)card.transform.Find("Portrait");
            var cardCenter = CenterOf(cardPortrait);

            card.onClick.Invoke();
            yield return null;
            yield return null;
            var detail = Page;
            var portrait = (RectTransform)detail.transform.Find("Portrait");
            Assert.IsFalse(cardPortrait.GetComponent<Image>().enabled, "카드의 초상은 상세로 넘어감");
            Assert.Less(portrait.rect.width, 300f, "처음엔 카드 초상 크기 근처");
            yield return Idle();
            Assert.Less(Vector2.Distance(new Vector2(460, 598), portrait.sizeDelta), 0.01f, "상세 자리 크기");
            Assert.AreEqual("Aria", Text(detail, "Content/Info/Head/Name"));
            Assert.AreEqual(240f, ((RectTransform)detail.transform.Find("Content/Info/Head")).sizeDelta.y, 0.5f, "스크롤 전엔 제목 영역이 펼쳐짐");

            var list = detail.GetComponentInChildren<UIAnimScrollList>();
            list.ScrollTo(8, UIAnimScrollList.Alignment.Start, 0f);
            yield return null;
            yield return null;
            Assert.AreEqual(120f, ((RectTransform)detail.transform.Find("Content/Info/Head")).sizeDelta.y, 0.5f, "스크롤하면 접힘");

            Named(detail, "BackButton").onClick.Invoke();
            var end = Time.realtimeSinceStartup + 3f;
            var last = Vector3.zero;
            while (detail != null && Time.realtimeSinceStartup < end)
            {
                last = CenterOf(portrait);
                yield return null;
            }
            Assert.Less(Vector2.Distance(cardCenter, last), 8f, "카드 자리로 돌아감 (마지막 프레임)");
            yield return Idle();
            Assert.IsTrue(cardPortrait.GetComponent<Image>().enabled);
        }

        [UnityTest]
        public IEnumerator 가방은_탭으로_페이지가_넘어가고_아이템_아이콘이_정보_팝업으로_이어진다()
        {
            yield return Load();
            yield return Open("BAG");
            var pager = Page.GetComponentsInChildren<UIAnimScrollList>().First(l => l.name == "Pager");
            Page.GetComponentsInChildren<Button>().First(b => b.name.StartsWith("Tab 3")).onClick.Invoke();
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(2, pager.CurrentIndex, "MATERIALS");

            var slot = pager.GetItem(2).GetComponentsInChildren<Button>().Where(b => b.name == "Body")   // the first slot: top left
                .OrderByDescending(b => Mathf.Round(b.transform.position.y)).ThenBy(b => b.transform.position.x).First();
            slot.onClick.Invoke();
            yield return Idle();
            var info = Modal;
            Assert.AreEqual("Royal Seal", Text(info, "Panel/Name"));
            Assert.AreEqual("LEGENDARY", Text(info, "Panel/Rarity"));
            Assert.Less(Vector2.Distance(new Vector2(280, 280), ((RectTransform)info.transform.Find("Icon")).sizeDelta), 0.01f);
            Assert.IsFalse(slot.transform.Find("Icon").GetComponent<Image>().enabled);
            Named(info, "CloseButton").onClick.Invoke();
            yield return Idle();
            Assert.AreEqual(0, _modals.Modals.Count);
            Assert.IsTrue(slot.transform.Find("Icon").GetComponent<Image>().enabled, "슬롯에 다시 보임");
        }

        [UnityTest]
        public IEnumerator 소환_10회는_카드가_떨어지고_모두_앞면으로_뒤집힌다()
        {
            yield return Load();
            yield return Open("SUMMON");
            Named(Page, "TenButton").onClick.Invoke();
            yield return Idle();
            var result = Modal;
            var ok = Named(result, "OkButton");
            var end = Time.realtimeSinceStartup + 20f;
            while (!ok.interactable && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(ok.interactable, "다 뒤집힌 뒤 OK");
            yield return new WaitForSeconds(0.6f);
            for (var i = 1; i <= 10; i++)
            {
                var card = result.transform.Find("Card" + i);
                Assert.AreEqual(1f, card.GetComponent<CanvasGroup>().alpha, 0.001f, "Card" + i);
                Assert.IsTrue(card.Find("Body/Face").gameObject.activeSelf, "앞면 " + i);
                Assert.IsFalse(card.Find("Body/Back").gameObject.activeSelf);
                Assert.AreEqual(0f, Mathf.DeltaAngle(0, card.Find("Body").localEulerAngles.y), 0.5f);
            }
            Assert.IsFalse(result.transform.Find("SingleCard").gameObject.activeSelf);
            Assert.IsTrue(Enumerable.Range(1, 10).Any(i => result.transform.Find($"Card{i}/Body/Face/Stars/Star3").gameObject.activeSelf), "4★ 이상 보장");
            Assert.AreEqual("800", Text(Page, "Content/Header/GemsPill/Value"), "3,800 - 3,000");
            ok.onClick.Invoke();
            yield return Idle();
            Assert.AreEqual(0, _modals.Modals.Count);
        }

        [UnityTest]
        public IEnumerator 모험은_챕터를_고르면_스테이지가_나오고_승리와_패배_결과가_뜬다()
        {
            yield return Load();
            yield return Open("ADVENTURE");
            var chapters = Page.GetComponentInChildren<UIAnimScrollList>();
            chapters.ScrollTo(2, UIAnimScrollList.Alignment.Center, 0f);
            yield return new WaitForSeconds(1.3f);   // the stages pop in over 1.04 s
            StringAssert.StartsWith("CHAPTER 3", Text(Page, "Content/ChapterTitle"));
            Assert.IsTrue(Page.transform.Find("Content/Stage7/Ring").gameObject.activeSelf, "다음 스테이지(7) 선택");
            Assert.IsFalse(Named(Page, "Stage8").interactable, "그 뒤는 잠김");
            Assert.AreEqual(1f, Page.transform.Find("Content/Stage10").localScale.x, 0.001f, "스테이지 등장 끝");

            Named(Page, "StartButton").onClick.Invoke();
            yield return Idle(3f);
            var victory = Modal;
            StringAssert.StartsWith("Victory", victory.name);
            for (var i = 0; i < 3; i++)
            {
                Assert.AreEqual(1f, victory.transform.Find("Star" + i).GetComponent<Image>().color.a, 0.001f);
                Assert.AreEqual(1f, victory.transform.Find("Star" + i).localScale.x, 0.01f);
            }
            Assert.AreEqual(1100f, ((RectTransform)victory.transform.Find("Banner")).sizeDelta.x, 0.5f);
            Assert.AreEqual("+450", Text(victory, "Rewards/GoldAmount"));
            Named(victory, "ContinueButton").onClick.Invoke();
            yield return Idle();

            chapters.ScrollTo(0, UIAnimScrollList.Alignment.Center, 0f);
            yield return new WaitForSeconds(1f);
            Named(Page, "Stage10").onClick.Invoke();
            Named(Page, "StartButton").onClick.Invoke();
            yield return Idle(3.5f);
            var defeat = Modal;
            StringAssert.StartsWith("Defeat", defeat.name);
            Assert.AreEqual("STAGE 1-10", Text(defeat, "Stage"));
            Assert.AreEqual(1f, Named(defeat, "RetryButton").GetComponent<CanvasGroup>().alpha, 0.001f);
            Assert.AreEqual("48/60", Text(Page, "Content/Header/StaminaPill/Value"), "스테이지마다 6");
            Named(defeat, "LobbyButton").onClick.Invoke();
            yield return Idle();
            yield return Idle();
            Assert.AreEqual(0, _modals.Modals.Count);
            Assert.AreEqual(1, _pages.Pages.Count, "로비로");
        }

        [UnityTest]
        public IEnumerator 퀘스트_보상을_받으면_도장이_찍히고_그_줄만_접혀_사라진다()
        {
            yield return Load();
            yield return Open("QUESTS");
            yield return new WaitForSeconds(0.8f);
            var list = Page.GetComponentInChildren<UIAnimScrollList>();
            string Titles() => string.Join(",", Page.GetComponentsInChildren<RectTransform>()
                .Where(t => t.name == "QuestRow(Clone)" && t.rect.height > 100f)
                .OrderByDescending(t => t.position.y).Select(t => t.Find("Body/Title").GetComponent<TMP_Text>().text));
            StringAssert.StartsWith("Win 3 battles,Summon a hero", Titles());

            Page.GetComponentsInChildren<Button>().First(b => b.name == "ClaimButton" && b.interactable).onClick.Invoke();
            yield return new WaitForSeconds(1.6f);
            StringAssert.StartsWith("Summon a hero,Use 5 potions", Titles(), "받은 줄만 빠짐");
            Assert.AreEqual("12,950", Text(Page, "Content/Header/GoldPill/Value"), "12,450 + 500");
            Assert.AreEqual(14, list.Count, "헤더 3 + 퀘스트 11");
        }
    }
}
