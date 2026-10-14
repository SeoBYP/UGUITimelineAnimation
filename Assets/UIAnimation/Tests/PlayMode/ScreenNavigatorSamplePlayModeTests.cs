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
    /// The UnityScreenNavigator demo rebuilt with UI Animation (Assets/ReferenceSamples/ScreenNavigator), in Play mode:
    /// Top → Loading → Home → Shop (tabs, buy) → back to Home → back to Top, with the original transition values.
    /// </summary>
    public sealed class ScreenNavigatorSamplePlayModeTests
    {
        const string ScenePath = "Assets/ReferenceSamples/ScreenNavigator/Scenes/ScreenNavigatorDemo.unity";
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
    }
}
