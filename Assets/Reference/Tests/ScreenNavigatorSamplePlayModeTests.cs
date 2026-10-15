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
    /// The UnityScreenNavigator demo rebuilt with UI Animation (Assets/Reference/ScreenNavigator), in Play mode:
    /// Top → Loading → Home → Shop (tabs, buy) → back to Home → back to Top, with the original transition values;
    /// the modals (Settings, Confirmation, unit information, portrait viewer with its shared element transition).
    /// </summary>
    public sealed class ScreenNavigatorSamplePlayModeTests
    {
        const string ScenePath = "Assets/Reference/ScreenNavigator/Scenes/ScreenNavigatorDemo.unity";
        UIAnimPageContainer _pages;

        IEnumerator Load()
        {
#if UNITY_EDITOR
            if (!System.IO.File.Exists(ScenePath)) Assert.Ignore("ScreenNavigator 샘플이 없음");
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("에디터 전용");
#endif
            yield return null;
            yield return null;
            _pages = Object.FindAnyObjectByType<UIAnimPageContainer>();
        }

        static IEnumerator WaitUntilIdle(float max = 5f)
        {
            var end = Time.realtimeSinceStartup + max;
            yield return null;
            while (UIAnimScreenContainer.AnyInTransition && Time.realtimeSinceStartup < end) yield return null;
            yield return null;
        }

        UIAnimScreen Page(string prefab) => _pages.Pages.FirstOrDefault(p => p != null && p.name.StartsWith(prefab));
        static Transform Child(Component root, string path) => root.transform.Find(path);
        static float RotationY(Transform t) => Mathf.DeltaAngle(0, t.localEulerAngles.y);

        [UnityTest]
        public IEnumerator Top_Loading_Home_Shop을_오가며_원본_전환_수치대로_움직인다()
        {
            yield return Load();
            var top = Page("prefab_top_page");
            Assert.IsNotNull(top, "Top으로 시작");
            Assert.AreEqual(0.749f, Child(top, "TitleText").GetComponent<TMP_Text>().color.a, 0.01f);

            // Top → Loading (Top's custom push exit, Loading's fade in), then Home behind it after its 1 s "preload".
            Child(top, "Button").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(126f, ((RectTransform)Child(top, "Top")).anchoredPosition.y, 0.5f, "위 바가 화면 밖으로 (0.5초)");
            Assert.AreEqual(-126f, ((RectTransform)Child(top, "Bottom")).anchoredPosition.y, 0.5f);
            Assert.IsFalse(top.gameObject.activeSelf, "Top 꺼짐");
            var loading = Page("prefab_loading_page");
            Assert.IsNotNull(loading, "Loading 표시 중 (Home 준비 1초)");
            Assert.AreEqual(1f, loading.Group.alpha, 0.01f);
            yield return WaitUntilIdle();
            var home = Page("prefab_home_page");
            Assert.IsNotNull(home);
            Assert.IsTrue(loading == null, "기록에 안 남는 Loading은 Home이 들어오면 파괴");
            Assert.AreEqual(2, _pages.Pages.Count, "Top, Home");
            Assert.AreEqual(1f, home.Group.alpha, 0.01f);
            Assert.AreEqual(11f, RotationY(Child(home, "RightMenu")), 0.1f, "메뉴 원근 Y회전 11°");

            // Home → Shop: Home's push exit (icons up, menu turns to 58.9°, fade from 0.25 s), Shop slides in from the right.
            var shopButton = home.GetComponentsInChildren<Button>(true).First(b => b.name == "UnitShopButton");
            shopButton.onClick.Invoke();
            yield return null;
            var shop = Page("prefab_unit_shop_page");
            Assert.Greater(shop.Rect.anchoredPosition.x, 0f, "Shop은 오른쪽에서 (기본 슬라이드)");
            yield return WaitUntilIdle();
            Assert.AreEqual(0f, shop.Rect.anchoredPosition.x, 0.5f);
            Assert.AreEqual(58.9f, RotationY(Child(home, "RightMenu")), 0.1f, "메뉴 58.9°");
            Assert.AreEqual(86f, ((RectTransform)Child(home, "TopLeftIcons")).anchoredPosition.y, 0.5f);
            Assert.IsFalse(home.gameObject.activeSelf);

            // Tabs: a higher tab slides in from the right, a lower one from the left.
            var shopPage = shop.GetComponent<MonoBehaviour>();
            var sheets = shop.GetComponentInChildren<UIAnimSheetContainer>();
            Assert.AreEqual(0, sheets.ActiveIndex, "처음은 REGULAR");
            Child(shop, "ItemList/TabGroup/SaleButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.Greater(sheets.Active.Rect.anchoredPosition.x, 0f, "SALE은 오른쪽에서");
            yield return WaitUntilIdle();
            Assert.AreEqual(2, sheets.ActiveIndex);
            Child(shop, "ItemList/TabGroup/RegularButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.Less(sheets.Active.Rect.anchoredPosition.x, 0f, "REGULAR는 왼쪽에서");
            yield return WaitUntilIdle();

            // Buy: Connecting... for 1 s, then Sold Out.
            var item = sheets.Active.transform.Find("prefab_shop_item");
            Assert.AreEqual("1000", item.Find("Unlocked/Cost/Text (TMP)").GetComponent<TMP_Text>().text, "REGULAR 아이템 1000");
            item.Find("Unlocked/BuyButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            var connecting = GameObject.Find("ConnectingView").GetComponent<CanvasGroup>();
            Assert.AreEqual(1f, connecting.alpha, 0.01f, "Connecting 표시");
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(0f, connecting.alpha, 0.01f);
            Assert.AreEqual("Sold Out", item.Find("Unlocked/BuyButton/Text (TMP) (1)").GetComponent<TMP_Text>().text);
            Assert.IsTrue(sheets.Active.transform.Find("prefab_shop_item (1)/Locked").gameObject.activeSelf, "나머지 칸은 LOCKED");

            // Back: Shop fades out (its pop exit), Home comes back (pop enter: menu to 11°, icons down).
            Child(shop, "BackButton").GetComponent<Button>().onClick.Invoke();
            yield return WaitUntilIdle();
            Assert.IsTrue(home.gameObject.activeSelf);
            Assert.AreEqual(11f, RotationY(Child(home, "RightMenu")), 0.1f);
            Assert.AreEqual(-48f, ((RectTransform)Child(home, "TopLeftIcons")).anchoredPosition.y, 0.5f);
            Assert.IsTrue(shop == null, "Shop 파괴");

            // Back again: Top (Loading was not kept), its bars and texts come back.
            Child(home, "TopLeftIcons/BackButton").GetComponent<Button>().onClick.Invoke();
            yield return WaitUntilIdle();
            Assert.AreEqual(1, _pages.Pages.Count);
            Assert.AreSame(top, _pages.Current);
            Assert.AreEqual(0f, ((RectTransform)Child(top, "Top")).anchoredPosition.y, 0.5f);
            Assert.AreEqual(0.749f, Child(top, "TitleText").GetComponent<TMP_Text>().color.a, 0.01f);
        }

        [UnityTest]
        public IEnumerator 잠긴_메뉴는_LOCKED_표시()
        {
            yield return Load();
            var top = Page("prefab_top_page");
            Child(top, "Button").GetComponent<Button>().onClick.Invoke();
            yield return WaitUntilIdle();
            yield return WaitUntilIdle();
            var home = Page("prefab_home_page");
            Transform Menu(string n) => home.GetComponentsInChildren<Button>(true).First(b => b.name == n).transform;
            Assert.IsTrue(Menu("UnitShopButton").Find("Unlocked").gameObject.activeSelf);
            Assert.IsTrue(Menu("SettingsButton").Find("Unlocked").gameObject.activeSelf);
            foreach (var locked in new[] { "MainQuestButton", "EventQuestButton", "MissionButton" })
                Assert.IsTrue(Menu(locked).Find("Locked").gameObject.activeSelf, locked);
        }

        IEnumerator ToHome()
        {
            yield return Load();
            Child(Page("prefab_top_page"), "Button").GetComponent<Button>().onClick.Invoke();
            yield return WaitUntilIdle();
            yield return WaitUntilIdle();
        }

        static Button Named(Component root, string name) => root.GetComponentsInChildren<Button>(true).First(b => b.name == name);

        [UnityTest]
        public IEnumerator 설정_모달은_커지며_열리고_바꾼_값을_닫을_때_저장하며_잠긴_항목은_확인_모달을_쌓는다()
        {
            PlayerPrefs.DeleteKey("Demo_BgmVolume");
            yield return ToHome();
            var modals = Object.FindAnyObjectByType<UIAnimModalContainer>();
            Named(Page("prefab_home_page"), "SettingsButton").onClick.Invoke();
            yield return null;
            var settings = modals.Current;
            Assert.IsTrue(settings.name.StartsWith("prefab_settings_modal"));
            Assert.Less(settings.Rect.localScale.x, 0.9f, "기본 모달 진입: 0.3배에서 커짐");
            yield return WaitUntilIdle();
            Assert.AreEqual(1f, settings.Rect.localScale.x, 0.001f);
            Assert.AreEqual(1f, settings.Group.alpha, 0.001f);
            Assert.AreEqual(0.749f, modals.transform.GetChild(0).GetComponent<Image>().color.a, 0.01f, "배경 막 (원본 backdrop)");

            // LOCKED: a confirmation on top, the settings stay.
            Named(settings, "LockedButton").onClick.Invoke();
            yield return WaitUntilIdle();
            Assert.AreEqual(2, modals.Modals.Count);
            Assert.AreEqual("This feature is locked.", modals.Current.transform.Find("Background/Contents/Text (TMP)").GetComponent<TMP_Text>().text);
            Assert.AreEqual(1f, settings.Rect.localScale.x, 0.001f, "아래 모달은 그대로");
            Named(modals.Current, "CloseButton").onClick.Invoke();
            yield return WaitUntilIdle();
            Assert.AreEqual(1, modals.Modals.Count);

            // Change BGM, close: saved.
            settings.transform.Find("Background/Contents/SoundSettings/BGM/Slider").GetComponent<Slider>().value = 0.25f;
            Named(settings, "CloseButton").onClick.Invoke();
            yield return null;
            Assert.Less(settings.Rect.localScale.x, 1f, "닫힐 때 작아짐");
            yield return WaitUntilIdle();
            Assert.AreEqual(0, modals.Modals.Count);
            Assert.IsTrue(settings == null, "닫힌 모달 파괴");
            Assert.AreEqual(0.25f, PlayerPrefs.GetFloat("Demo_BgmVolume", 1f), 0.001f, "닫을 때 저장");
            Assert.IsTrue(Page("prefab_home_page").gameObject.activeSelf, "페이지는 그대로");
            PlayerPrefs.DeleteKey("Demo_BgmVolume");
        }

        [UnityTest]
        public IEnumerator 유닛_정보의_초상화가_뷰어의_위치와_크기로_이어지고_닫으면_돌아간다()
        {
            yield return ToHome();
            Named(Page("prefab_home_page"), "UnitShopButton").onClick.Invoke();
            yield return WaitUntilIdle();
            var shop = Page("prefab_unit_shop_page");
            var modals = Object.FindAnyObjectByType<UIAnimModalContainer>();

            // The shop item's thumbnail opens the unit information (REGULAR sells 003, Unknown Dragon).
            var sheet = shop.GetComponentInChildren<UIAnimSheetContainer>().Active;
            sheet.transform.Find("prefab_shop_item/Unlocked/prefab_unit_thumbnail").GetComponent<Button>().onClick.Invoke();
            yield return WaitUntilIdle();
            var info = modals.Current;
            Assert.AreEqual("Unknown Dragon", info.transform.Find("Background/TitleBar/Text (TMP)").GetComponent<TMP_Text>().text);
            var description = info.transform.Find("Background/Contents/TabGroup/RightPanel/Description/Text (TMP) (1)").GetComponent<TMP_Text>();
            StringAssert.EndsWith("rank of 1.", description.text);

            // Rank tabs: the thumbnails.
            var portraits = info.GetComponentInChildren<UIAnimSheetContainer>();
            info.transform.Find("Background/Contents/TabGroup/RightPanel/Thumbnails/prefab_unit_thumbnail (2)").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.Less(portraits.Active.Group.alpha, 1f, "시트 기본 페이드");
            yield return WaitUntilIdle();
            Assert.AreEqual(2, portraits.ActiveIndex);
            StringAssert.EndsWith("rank of 3.", description.text);

            // Expand: the viewer's portrait starts on the information modal's portrait area and grows into its own place.
            var area = (RectTransform)portraits.transform;
            var shown = portraits.Active.GetComponentInChildren<Image>().rectTransform;
            Assert.AreEqual(area.rect.size, shown.rect.size, "보이는 초상화 = 영역 (돌아왔을 때 크기가 튀지 않게)");
            Assert.AreEqual(area.TransformPoint(area.rect.center), shown.TransformPoint(shown.rect.center));
            var areaCenter = area.TransformPoint(area.rect.center);
            Named(info, "ExpandButton").onClick.Invoke();
            yield return null;
            var viewer = modals.Current;
            var portrait = (RectTransform)viewer.transform.Find("prefab_unit_portrait");
            Assert.AreSame(portraits.Active.GetComponentInChildren<Image>().sprite, portrait.GetComponent<Image>().sprite, "같은 랭크의 초상화");
            Assert.IsFalse(area.gameObject.activeSelf, "공유 요소는 원래 자리에서 사라짐");
            Assert.Less(portrait.rect.width, 800f, "처음엔 정보 모달의 초상화 영역 크기 근처");
            yield return WaitUntilIdle();
            Assert.Less(Vector2.Distance(new Vector2(810, 1080), portrait.sizeDelta), 0.01f, "뷰어 자리 크기");
            Assert.Less(portrait.anchoredPosition.magnitude, 0.01f, "뷰어 가운데");
            Assert.AreEqual(1f, viewer.Rect.localScale.x, 0.001f, "뷰어는 모달 기본 확대 없이 초상화만 움직임");

            // Close: back to the portrait area (checked just before the end, when the viewer is destroyed), which shows again.
            Named(viewer, "Button").onClick.Invoke();
            yield return new WaitForSeconds(0.27f);   // OutQuart: 99.99 % of the way
            Assert.AreEqual(area.rect.width, portrait.rect.width, 1f, "정보 모달의 영역 크기로");
            Assert.AreEqual(area.rect.height, portrait.rect.height, 1f);
            Assert.AreEqual(areaCenter.x, portrait.position.x, 0.01f, "같은 자리로");
            Assert.AreEqual(areaCenter.y, portrait.position.y, 0.01f);
            yield return WaitUntilIdle();
            Assert.IsTrue(area.gameObject.activeSelf, "원래 자리에 다시 보임");
            Assert.AreSame(info, modals.Current);

            Named(info, "CloseButton").onClick.Invoke();
            yield return WaitUntilIdle();
            Assert.AreEqual(0, modals.Modals.Count);
            Assert.AreSame(shop, Page("prefab_unit_shop_page"), "Back은 모달부터 닫음");
        }
    }
}
