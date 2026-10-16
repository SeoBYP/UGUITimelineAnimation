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
    /// <summary>
    /// The RPG sample's screens beyond the lobby menu in Play mode: title and loading, upgrade, shop and purchase, formation and
    /// battle, mail, attendance, settings with the confirm popup and toasts, the quantity wheel, guild chat, ranking, story.
    /// </summary>
    public sealed class RpgScreensPlayModeTests
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
        static RectTransform Toast => (RectTransform)GameObject.Find("ToastCanvas/Toast").transform;
        static string ToastText => Toast.Find("Text").GetComponent<TMP_Text>().text;

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
            Assert.That(((RectTransform)fill).anchorMax.x, Is.InRange(0.2f, 0.9f), "막대가 차는 중");
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
        public IEnumerator 두_버튼을_동시에_눌러도_하나만_실행되고_손가락은_하나로_합쳐진다()
        {
            yield return Load();
            var module = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>().GetComponent("InputSystemUIInputModule");
            Assert.AreEqual("SingleUnifiedPointer", module.GetType().GetProperty("pointerBehavior").GetValue(module).ToString(), "여러 손가락이 한 포인터로");
            yield return Click(Page, "HEROESButton");
            Page.GetComponentsInChildren<Button>().First(b => b.name == "Body").onClick.Invoke();
            yield return Idle();
            StringAssert.StartsWith("HeroDetail", Page.name);
            // The same frame: UPGRADE first, then Back. The first starts a transition; the second is ignored.
            Named(Page, "UpgradeButton").onClick.Invoke();
            Named(Page, "BackButton").onClick.Invoke();
            yield return Idle();
            StringAssert.StartsWith("Upgrade", Page.name);
            Assert.AreEqual(4, _pages.Pages.Count, "로비·영웅·상세·강화 (뒤로가기는 무시됨)");
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
            var stockBadge = card.transform.Find("Stock").gameObject;
            Assert.IsTrue(stockBadge.activeSelf, "한정 상품은 남은 수량 배지");
            Assert.AreEqual("1 LEFT", stockBadge.GetComponentInChildren<TMP_Text>().text);
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
            Assert.IsFalse(stockBadge.activeSelf, "품절이면 수량 배지 대신 도장");
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
        public IEnumerator 우편은_모두_받기를_누르면_전부_동시에_받고_접히며_로비_배지가_사라진다()
        {
            yield return Load();
            Assert.AreEqual("6", Text(Page, "Content/MAILButton/Badge/Label"), "로비 우편 배지: 편지 수");
            yield return Click(Page, "MAILButton", 0.6f);
            StringAssert.StartsWith("Mail", Page.name);
            Assert.AreEqual("6 LETTERS", Text(Page, "Content/Count"));
            var gold = Number(Text(Page, "Content/Header/GoldPill/Value"));
            var gems = Number(Text(Page, "Content/Header/GemsPill/Value"));
            var list = Page.GetComponentInChildren<UIAnimScrollList>();
            var rewards = Enumerable.Range(0, 6).Select(i => list.GetItem(i)).Where(r => r != null).Select(r => r.Find("Body/Reward").GetComponent<CanvasGroup>()).ToArray();
            Assert.GreaterOrEqual(rewards.Length, 4, "보이는 줄");
            Named(Page, "ClaimAllButton").onClick.Invoke();
            yield return new WaitForSeconds(0.25f);
            Assert.IsTrue(rewards.All(r => r.alpha < 0.9f), "보이는 줄 전부가 동시에 받는 중");
            Assert.AreEqual("6 LETTERS", Text(Page, "Content/Count"), "아직 접히기 전");
            var end = Time.realtimeSinceStartup + 8f;
            while (Text(Page, "Content/Count") != "0 LETTERS" && Time.realtimeSinceStartup < end) yield return null;
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(1f, Page.transform.Find("Content/Empty").GetComponent<CanvasGroup>().alpha, 0.001f, "빈 안내");
            Assert.AreEqual(gold + 7000, Number(Text(Page, "Content/Header/GoldPill/Value")));
            Assert.AreEqual(gems + 530, Number(Text(Page, "Content/Header/GemsPill/Value")));
            Assert.AreEqual("90/60", Text(Page, "Content/Header/StaminaPill/Value"), "스태미나는 최대를 넘을 수 있음");
            yield return Click(Page, "BackButton");
            Assert.IsFalse(Page.transform.Find("Content/MAILButton/Badge").gameObject.activeSelf, "편지가 없으면 배지 없음");
        }

        [UnityTest]
        public IEnumerator 출석은_오늘_칸에_도장을_찍고_보상을_준다()
        {
            yield return Load();
            var gems = Number(Text(Page, "Content/GemsPill/Value"));
            var badge = Page.transform.Find("Content/DAILYButton/Badge").gameObject;
            Assert.IsTrue(badge.activeSelf, "오늘 출석 전엔 ! 배지");
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
            Assert.IsFalse(badge.activeSelf, "받으면 배지가 사라짐");
        }

        [UnityTest]
        public IEnumerator 설정_스위치는_손잡이가_옮겨가고_초기화는_확인_후_토스트로_알린다()
        {
            yield return Load();
            Assert.Greater(Toast.anchoredPosition.y, 100f, "토스트는 화면 위에 숨어 있음");
            yield return Click(Page, "SETTINGSButton");
            StringAssert.StartsWith("Settings", Modal.name);
            var knob = (RectTransform)Modal.transform.Find("Panel/MusicSwitch/Knob");
            Assert.AreEqual(46f, knob.anchoredPosition.x, 0.5f, "처음엔 켜짐");
            Named(Modal, "MusicSwitch").onClick.Invoke();
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(-46f, knob.anchoredPosition.x, 0.5f, "끄면 왼쪽으로");
            Modal.transform.Find("Panel/MusicVolume").GetComponent<Slider>().value = 0.25f;
            Assert.AreEqual("25%", Text(Modal, "Panel/MusicVolumeValue"));

            yield return Click(Modal, "CloseButton");
            yield return Click(Page, "SETTINGSButton");
            knob = (RectTransform)Modal.transform.Find("Panel/MusicSwitch/Knob");
            Assert.AreEqual(-46f, knob.anchoredPosition.x, 0.5f, "다시 열어도 꺼진 채");
            Assert.AreEqual("25%", Text(Modal, "Panel/MusicVolumeValue"));

            yield return Click(Modal, "ResetButton");
            StringAssert.StartsWith("Confirm", Modal.name, "확인 팝업이 위에 뜸");
            Assert.AreEqual(2, _modals.Modals.Count);
            Assert.AreEqual("RESET DATA", Text(Modal, "Panel/Title"));
            yield return Click(Modal, "OkButton", 0.6f);
            Assert.AreEqual(1, _modals.Modals.Count, "확인만 닫힘");
            Assert.AreEqual("Your data was reset", ToastText);
            Assert.Less(Toast.anchoredPosition.y, 0f, "토스트가 내려와 있음");
            yield return new WaitForSeconds(2f);
            Assert.Greater(Toast.anchoredPosition.y, 100f, "그리고 다시 올라감");
        }

        [UnityTest]
        public IEnumerator 수량_휠을_돌리면_개수가_바뀌고_사용하면_토스트와_보유량이_줄어든다()
        {
            yield return Load();
            yield return Click(Page, "BAGButton", 0.8f);
            // Iron Helm (3 owned) in the first tab.
            var slot = Page.GetComponentsInChildren<Button>().First(b => b.transform.Find("Icon")?.GetComponent<Image>().sprite?.name == "helm");
            slot.onClick.Invoke();
            yield return Idle();
            StringAssert.StartsWith("ItemInfo", Modal.name);
            Assert.AreEqual("Owned: 3", Text(Modal, "Panel/Count"));
            yield return Click(Modal, "UseButton");
            StringAssert.StartsWith("Quantity", Modal.name);
            Assert.AreEqual("x1", Text(Modal, "Panel/Amount"));
            var wheel = Modal.GetComponentInChildren<UIAnimScrollList>();
            wheel.ScrollTo(2, UIAnimScrollList.Alignment.Center, 0.3f);
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual("x3", Text(Modal, "Panel/Amount"));
            var center = wheel.GetItem(2).Find("Body/Value");
            Assert.AreEqual(1f, center.GetComponent<TMP_Text>().color.a, 0.05f, "가운데 줄은 또렷");
            Assert.Less(wheel.GetItem(0).Find("Body/Value").GetComponent<TMP_Text>().color.a, 0.6f, "멀어진 줄은 흐림");
            yield return Click(Modal, "UseButton", 0.5f);
            Assert.AreEqual(0, _modals.Modals.Count, "두 팝업 모두 닫힘");
            Assert.AreEqual("Used 3 x Iron Helm", ToastText);
            slot.onClick.Invoke();
            yield return Idle();
            Assert.AreEqual("Owned: 0", Text(Modal, "Panel/Count"));
        }

        [UnityTest]
        public IEnumerator 전투는_자동_공격과_스킬로_적을_쓰러뜨리고_승리_팝업이_뜬다()
        {
            yield return Load();
            yield return Click(Page, "ADVENTUREButton");
            var chapters = Page.GetComponentInChildren<UIAnimScrollList>();
            chapters.ScrollTo(2, UIAnimScrollList.Alignment.Center, 0f);
            yield return new WaitForSeconds(1.3f);
            yield return Click(Page, "StartButton");
            Named(Page, "BattleButton").onClick.Invoke();
            var end = Time.realtimeSinceStartup + 4f;
            while (!Page.name.StartsWith("Battle") && Time.realtimeSinceStartup < end) yield return null;
            yield return Idle();
            StringAssert.StartsWith("Battle", Page.name);
            Assert.AreEqual(3, Enumerable.Range(0, 5).Count(i => Page.transform.Find($"Content/Hero{i}").gameObject.activeSelf), "편성한 3명");
            var hp = Page.transform.Find("Content/EnemyHp/Fill");
            Assert.AreEqual(1f, ((RectTransform)hp).anchorMax.x, 0.01f);

            Named(Page, "Skill0").onClick.Invoke();
            yield return new WaitForSeconds(0.3f);
            Assert.Less(((RectTransform)hp).anchorMax.x, 0.9f, "스킬 한 방");
            Assert.IsFalse(Named(Page, "Skill0").interactable, "재사용 대기");
            Assert.Greater(Page.transform.Find("Content/Skill0/Cooldown").GetComponent<Image>().fillAmount, 0.8f, "대기 원이 가득");
            Assert.IsTrue(Page.GetComponentsInChildren<TMP_Text>().Any(t => t.name == "Value" && t.transform.parent.name.StartsWith("Number") && t.color.a > 0.5f), "피해 숫자");
            Named(Page, "Skill1").onClick.Invoke();
            Named(Page, "Skill2").onClick.Invoke();

            end = Time.realtimeSinceStartup + 15f;
            while (_modals.Modals.Count == 0 && Time.realtimeSinceStartup < end) yield return null;
            yield return Idle();
            StringAssert.StartsWith("Victory", Modal.name);
            StringAssert.StartsWith("Formation", Page.name, "전투 화면은 닫히고 편성 위에 결과");
        }

        [UnityTest]
        public IEnumerator 길드_채팅은_입력해서_보내면_내_말풍선과_답장이_붙는다()
        {
            yield return Load();
            yield return Click(Page, "GUILDButton", 0.5f);
            StringAssert.StartsWith("Guild", Page.name);
            var list = Page.GetComponentInChildren<UIAnimScrollList>();
            Assert.AreEqual(4, list.Count);
            var typing = Page.transform.Find("Content/Typing").gameObject;
            var input = Page.GetComponentInChildren<TMP_InputField>();
            input.text = "On my way!";
            Named(Page, "SendButton").onClick.Invoke();
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(5, list.Count, "내 말");
            Assert.IsTrue(typing.activeSelf, "입력 중 표시");
            Assert.AreEqual("", input.text, "보내면 입력창이 비워짐");
            Assert.IsFalse(Named(Page, "SendButton").interactable, "답을 기다리는 동안");
            var mine = list.GetItem(4);
            Assert.IsNotNull(mine, "새 말풍선이 보이는 곳에");
            StringAssert.StartsWith("ChatMine", mine.name);
            Assert.AreEqual("On my way!", mine.Find("Bubble/Text").GetComponent<TMP_Text>().text);
            yield return new WaitForSeconds(1.4f);
            Assert.AreEqual(6, list.Count, "답장");
            Assert.IsFalse(typing.activeSelf);
            Assert.IsTrue(Named(Page, "SendButton").interactable);
            var answer = list.GetItem(5);
            Assert.IsNotNull(answer, "끝에 붙어 따라감(Stick To End)");
            Assert.Greater(answer.rect.height, 100f, "글 길이만큼 측정");

            // Short messages get short bubbles; a long one stops at the limit and wraps.
            var shortBubble = (RectTransform)list.GetItem(4).Find("Bubble");   // "On my way!" (in view: the list sticks to the end)
            var longBubble = (RectTransform)list.GetItem(5).Find("Bubble");    // "Good. The portal closes in ten minutes."
            Assert.Less(shortBubble.rect.width, longBubble.rect.width, "글 길이만큼의 말풍선");
            Assert.LessOrEqual(longBubble.rect.width, 900.5f, "최대 폭");

            // Enter in the field sends as well.
            input.text = "Hello guild";
            input.onSubmit.Invoke(input.text);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(7, list.Count, "입력한 말");
            Assert.AreEqual("Hello guild", list.GetItem(6).Find("Bubble/Text").GetComponent<TMP_Text>().text);
            yield return new WaitForSeconds(1.4f);
            Assert.AreEqual(8, list.Count, "길드원의 답");
        }

        [UnityTest]
        public IEnumerator 랭킹은_단상이_솟고_내_순위로_스크롤하면_그_줄이_빛난다()
        {
            yield return Load();
            yield return Click(Page, "RANKButton");
            StringAssert.StartsWith("Ranking", Page.name);
            Assert.AreEqual(300f, ((RectTransform)Page.transform.Find("Content/Podium/Pillar1")).sizeDelta.y, 0.5f, "1위 기둥");
            Assert.AreEqual(1f, Page.transform.Find("Content/Podium/Hero1").GetComponent<CanvasGroup>().alpha, 0.001f);
            Assert.AreEqual("24", Text(Page, "Content/Mine/Rank"));
            var list = Page.GetComponentInChildren<UIAnimScrollList>();
            Assert.IsNull(list.GetItem(20), "처음엔 내 줄이 안 보임");
            Named(Page, "MyRankButton").onClick.Invoke();
            yield return new WaitForSeconds(0.75f);
            var row = list.GetItem(20);
            Assert.IsNotNull(row, "내 줄로 스크롤");
            Assert.AreEqual("24", row.Find("Body/Rank").GetComponent<TMP_Text>().text);
            Assert.IsTrue(row.GetComponent<UIAnimation>().IsPlaying("Highlight"), "빛남");
        }

        [UnityTest]
        public IEnumerator 스토리는_글자가_타자처럼_나오고_탭하면_한번에_끝나며_마지막에_선택지가_뜬다()
        {
            yield return Load();
            yield return Click(Page, "STORYButton");
            StringAssert.StartsWith("Story", Page.name);
            var line = Page.transform.Find("Content/Box/Line").GetComponent<TMP_Text>();
            yield return new WaitForSeconds(0.4f);
            line.ForceMeshUpdate();
            Assert.Less(line.maxVisibleCharacters, line.textInfo.characterCount, "한 글자씩");
            Assert.AreEqual("Aria", Text(Page, "Content/Box/Plate/Speaker"));
            Assert.AreEqual(1f, Page.transform.Find("Content/Left").GetComponent<Image>().color.a, 0.01f, "왼쪽에 등장");

            Named(Page, "NextButton").onClick.Invoke();   // finish the line
            yield return null;
            Assert.GreaterOrEqual(line.maxVisibleCharacters, line.textInfo.characterCount, "탭하면 한 번에");
            Named(Page, "NextButton").onClick.Invoke();   // next line: Merlin, on the right
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual("Merlin", Text(Page, "Content/Box/Plate/Speaker"));
            Assert.AreEqual(1f, Page.transform.Find("Content/Right").GetComponent<Image>().color.a, 0.01f, "오른쪽에 등장");
            Assert.AreEqual(0.92f, Page.transform.Find("Content/Left").localScale.x, 0.01f, "말 안 하는 쪽은 작아짐");

            var choices = Page.transform.Find("Content/Choices").gameObject;
            Assert.IsFalse(choices.activeSelf);
            Named(Page, "NextButton").onClick.Invoke();   // finish line 2
            yield return null;
            Named(Page, "NextButton").onClick.Invoke();   // line 3, the last
            yield return null;
            Assert.IsFalse(choices.activeSelf, "타자가 끝나기 전엔 선택지 없음");
            Named(Page, "NextButton").onClick.Invoke();   // finish it
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(choices.activeSelf, "마지막 줄 다음 선택지");
            Assert.AreEqual(1f, choices.GetComponent<CanvasGroup>().alpha, 0.01f);
            Named(Page, "Choice0").onClick.Invoke();
            yield return Idle(0.5f);
            StringAssert.StartsWith("Lobby", Page.name);
            Assert.AreEqual("You chose: We march at dawn", ToastText);
        }
    }
}
