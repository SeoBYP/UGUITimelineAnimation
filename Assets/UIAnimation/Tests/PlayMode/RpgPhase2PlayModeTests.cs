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
    /// <summary>The RPG sample's second set of screens (title, loading, upgrade, shop, formation, mail, attendance) in Play mode.</summary>
    public sealed class RpgPhase2PlayModeTests
    {
        const string ScenePath = "Assets/UIAnimation/Samples/RPG/Scenes/RPG.unity";
        UIAnimPageContainer _pages;
        UIAnimModalContainer _modals;

        IEnumerator Load(bool toLobby = true)
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
            if (toLobby) yield return RpgPlayModeTests.ToLobby(_pages);
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
        static string Text(Component root, string path) => root.transform.Find(path).GetComponent<TMP_Text>().text;
        static int Number(string text) => int.Parse(new string(text.TakeWhile(c => c != '/').Where(char.IsDigit).ToArray()));

        IEnumerator Click(Component root, string name, float extra = 0.1f)
        {
            Named(root, name).onClick.Invoke();
            yield return Idle(extra);
        }

        [UnityTest]
        public IEnumerator 타이틀은_탭하면_로딩을_거쳐_로비로_가고_기록에_남지_않는다()
        {
            yield return Load(toLobby: false);
            yield return Idle(0.5f);
            StringAssert.StartsWith("Title", Page.name);
            Assert.AreEqual(1f, Page.transform.Find("Content/Crown").localScale.x, 0.001f, "왕관 등장 끝");
            Named(Page, "TapButton").onClick.Invoke();
            yield return Idle();
            StringAssert.StartsWith("Loading", Page.name);
            var fill = Page.transform.Find("Content/Bar/Fill");
            yield return new WaitForSeconds(0.6f);
            Assert.That(fill.localScale.x, Is.InRange(0.2f, 0.9f), "막대가 차는 중");
            StringAssert.EndsWith("%", Text(Page, "Content/Percent"));
            var end = Time.realtimeSinceStartup + 6f;
            while (!Page.name.StartsWith("Lobby") && Time.realtimeSinceStartup < end) yield return null;
            yield return Idle();
            StringAssert.StartsWith("Lobby", Page.name);
            Assert.AreEqual(1, _pages.Pages.Count, "타이틀·로딩은 기록에 남지 않음");
        }

        [UnityTest]
        public IEnumerator 강화는_골드를_쓰고_레벨이_오르며_장비가_차례로_들어간다()
        {
            yield return Load();
            yield return Click(Page, "HEROESButton");
            Page.GetComponentsInChildren<Button>().First(b => b.name == "Body").onClick.Invoke();   // the first card
            yield return Idle();
            var detailLevel = Text(Page, "Content/Info/Head/Level");
            yield return Click(Page, "UpgradeButton");
            StringAssert.StartsWith("Upgrade", Page.name);
            var level = Number(Text(Page, "Content/Panel/Level"));
            Assert.AreEqual(Number(detailLevel), level, "상세와 같은 레벨");
            var gold = Number(Text(Page, "Content/Header/GoldPill/Value"));
            var cost = Number(Text(Page, "Content/Panel/UpgradeButton/Cost"));
            Assert.AreEqual(level * 120, cost);

            Named(Page, "UpgradeButton").onClick.Invoke();
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(level + 1, Number(Text(Page, "Content/Panel/Level")));
            Assert.AreEqual(gold - cost, Number(Text(Page, "Content/Header/GoldPill/Value")), "골드가 비용만큼 줄어듦");
            Assert.AreEqual(1f, Page.transform.Find("Content/Panel/Level").localScale.x, 0.01f, "Punch가 끝나면 원래 크기");

            var slots = Enumerable.Range(0, 3).Select(i => Page.transform.Find($"Content/Panel/GearSlot{i}/Icon").GetComponent<Image>()).ToArray();
            Assert.IsTrue(slots.All(s => s.color.a < 0.01f), "처음엔 빈 칸");
            Named(Page, "AutoEquipButton").onClick.Invoke();
            yield return new WaitForSeconds(0.1f);
            Assert.Greater(slots[0].color.a, 0.5f, "첫 칸부터");
            Assert.Less(slots[2].color.a, 0.01f, "마지막 칸은 아직");
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(slots.All(s => s.color.a > 0.99f && Mathf.Abs(s.rectTransform.localScale.x - 1f) < 0.01f), "세 칸 모두 들어감");

            yield return Click(Page, "BackButton");
            StringAssert.StartsWith("HeroDetail", Page.name);
            Assert.AreEqual(level + 1, Number(Text(Page, "Content/Info/Head/Level")), "상세로 돌아오면 오른 레벨");
        }

        [UnityTest]
        public IEnumerator 상점은_구매_팝업을_거쳐_재화가_바뀌고_다_사면_품절이_찍힌다()
        {
            yield return Load();
            yield return Click(Page, "SHOPButton");
            StringAssert.StartsWith("Shop", Page.name);
            yield return new WaitForSeconds(0.6f);   // the grid enters one by one
            var countdown = Text(Page, "Content/Deal/Countdown");
            StringAssert.IsMatch(@"^\d\d:\d\d:\d\d$", countdown);
            var gems = Number(Text(Page, "Content/Header/GemsPill/Value"));
            var gold = Number(Text(Page, "Content/Header/GoldPill/Value"));

            var card = Page.GetComponentsInChildren<Button>().First(b => b.name == "Body" && b.transform.Find("Name")?.GetComponent<TMP_Text>().text == "Hero Chest");
            card.onClick.Invoke();
            yield return Idle();
            StringAssert.StartsWith("Purchase", Modal.name);
            Assert.AreEqual("Hero Chest", Text(Modal, "Panel/Name"));
            Assert.AreEqual(1f, Modal.transform.Find("Panel").localScale.x, 0.001f, "팝업 등장 끝");
            yield return Click(Modal, "BuyButton", 1f);
            Assert.AreEqual(0, _modals.Modals.Count, "사면 닫힘");
            Assert.AreEqual(gems - 900, Number(Text(Page, "Content/Header/GemsPill/Value")));
            Assert.AreEqual(gold + 8000, Number(Text(Page, "Content/Header/GoldPill/Value")));
            var stamp = card.transform.Find("Stamp").GetComponent<TMP_Text>();
            Assert.AreEqual(1f, stamp.color.a, 0.01f, "재고 1개를 사면 품절 도장");
            Assert.IsFalse(card.interactable);
        }

        [UnityTest]
        public IEnumerator 편성은_영웅을_넣고_빼면_전투력이_바뀐다()
        {
            yield return Load();
            yield return Click(Page, "ADVENTUREButton");
            yield return Click(Page, "StartButton");
            StringAssert.StartsWith("Formation", Page.name);
            yield return new WaitForSeconds(0.7f);
            var power = Number(Text(Page, "Content/PowerBox/Value"));
            Group(3, out var slot3);
            Assert.AreEqual(0f, slot3.alpha, 0.001f, "4번째 칸은 빈 칸");
            Assert.IsFalse(Named(Page, "Hero0").interactable, "이미 편성된 영웅은 못 고름");

            Named(Page, "Hero3").onClick.Invoke();
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(1f, slot3.alpha, 0.001f, "4번째 칸에 들어감");
            Assert.AreEqual(0f, ((RectTransform)slot3.transform).anchoredPosition.y, 0.5f, "떨어져 자리 잡음");
            var more = Number(Text(Page, "Content/PowerBox/Value"));
            Assert.Greater(more, power, "전투력 증가");

            Named(Page, "Slot0").onClick.Invoke();
            yield return new WaitForSeconds(0.8f);
            Group(0, out var slot0);
            Assert.AreEqual(0f, slot0.alpha, 0.01f, "빼면 비워짐(역재생)");
            Assert.Less(Number(Text(Page, "Content/PowerBox/Value")), more);
            Assert.IsTrue(Named(Page, "Hero0").interactable, "뺀 영웅은 다시 고를 수 있음");

            void Group(int i, out CanvasGroup group) => group = Page.transform.Find($"Content/Slot{i}/Portrait").GetComponent<CanvasGroup>();
        }

        [UnityTest]
        public IEnumerator 우편은_모두_받기로_한_통씩_접히고_빈_안내가_뜬다()
        {
            yield return Load();
            yield return Click(Page, "MAILButton", 0.6f);
            StringAssert.StartsWith("Mail", Page.name);
            Assert.AreEqual("6 LETTERS", Text(Page, "Content/Count"));
            var gold = Number(Text(Page, "Content/Header/GoldPill/Value"));
            var gems = Number(Text(Page, "Content/Header/GemsPill/Value"));
            Named(Page, "ClaimAllButton").onClick.Invoke();
            yield return new WaitForSeconds(0.9f);
            Assert.AreEqual("5 LETTERS", Text(Page, "Content/Count"), "한 통씩");
            var end = Time.realtimeSinceStartup + 8f;
            while (Text(Page, "Content/Count") != "0 LETTERS" && Time.realtimeSinceStartup < end) yield return null;
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(1f, Page.transform.Find("Content/Empty").GetComponent<CanvasGroup>().alpha, 0.001f, "빈 안내");
            Assert.AreEqual(gold + 7000, Number(Text(Page, "Content/Header/GoldPill/Value")));
            Assert.AreEqual(gems + 530, Number(Text(Page, "Content/Header/GemsPill/Value")));
            Assert.AreEqual("90/60", Text(Page, "Content/Header/StaminaPill/Value"), "스태미나는 최대를 넘을 수 있음");
        }

        [UnityTest]
        public IEnumerator 출석은_오늘_칸에_도장을_찍고_보상을_준다()
        {
            yield return Load();
            var gems = Number(Text(Page, "Content/GemsPill/Value"));
            yield return Click(Page, "DAILYButton");
            StringAssert.StartsWith("Attendance", Modal.name);
            TMP_Text Stamp(int day) => Modal.transform.Find($"Panel/Day{day}/Stamp").GetComponent<TMP_Text>();
            Assert.AreEqual(1f, Stamp(4).color.a, 0.01f, "지난 날은 도장");
            Assert.AreEqual(0f, Stamp(5).color.a, 0.01f, "오늘은 아직");
            Named(Modal, "ClaimButton").onClick.Invoke();
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(1f, Stamp(5).color.a, 0.01f, "오늘 도장");
            Assert.AreEqual(1f, Modal.transform.Find("Panel/Day5/Body").localScale.x, 0.01f, "반짝임 멈춤");
            Assert.IsFalse(Named(Modal, "ClaimButton").interactable);
            yield return Click(Modal, "CloseButton");
            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual(gems + 100, Number(Text(Page, "Content/GemsPill/Value")), "5일차 젬 100");
        }
    }
}
