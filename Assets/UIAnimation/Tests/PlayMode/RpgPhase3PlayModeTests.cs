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
    /// <summary>The RPG sample's third set (settings, confirm, toast, quantity wheel, battle HUD, guild chat, ranking, story) in Play mode.</summary>
    public sealed class RpgPhase3PlayModeTests
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
            yield return RpgPlayModeTests.ToLobby(_pages);
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
            Assert.AreEqual(1f, hp.localScale.x, 0.01f);

            Named(Page, "Skill0").onClick.Invoke();
            yield return new WaitForSeconds(0.3f);
            Assert.Less(hp.localScale.x, 0.9f, "스킬 한 방");
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
        public IEnumerator 길드_채팅은_빠른_답장을_보내면_내_말풍선과_답장이_붙는다()
        {
            yield return Load();
            yield return Click(Page, "GUILDButton", 0.5f);
            StringAssert.StartsWith("Guild", Page.name);
            var list = Page.GetComponentInChildren<UIAnimScrollList>();
            Assert.AreEqual(4, list.Count);
            var typing = Page.transform.Find("Content/Typing").gameObject;
            Named(Page, "Quick0").onClick.Invoke();
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(5, list.Count, "내 말");
            Assert.IsTrue(typing.activeSelf, "입력 중 표시");
            Assert.IsFalse(Named(Page, "Quick1").interactable);
            var mine = list.GetItem(4);
            Assert.IsNotNull(mine, "새 말풍선이 보이는 곳에");
            StringAssert.StartsWith("ChatMine", mine.name);
            Assert.AreEqual("On my way!", mine.Find("Bubble/Text").GetComponent<TMP_Text>().text);
            yield return new WaitForSeconds(1.4f);
            Assert.AreEqual(6, list.Count, "답장");
            Assert.IsFalse(typing.activeSelf);
            Assert.IsTrue(Named(Page, "Quick1").interactable);
            var answer = list.GetItem(5);
            Assert.IsNotNull(answer, "끝에 붙어 따라감(Stick To End)");
            Assert.Greater(answer.rect.height, 100f, "글 길이만큼 측정");
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
