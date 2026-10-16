using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>The lobby's event banner (loop + one per swipe + auto scroll) never stays between two pages.</summary>
    public sealed class RpgBannerPlayModeTests
    {
        const string ScenePath = "Assets/UIAnimation/Samples/RPG/Scenes/RPG.unity";
        UIAnimPageContainer _pages;

        static IEnumerator Idle(float extra = 0.2f)
        {
            yield return null;
            var end = Time.realtimeSinceStartup + 8f;
            while (UIAnimScreenContainer.AnyInTransition && Time.realtimeSinceStartup < end) yield return null;
            yield return new WaitForSeconds(extra);
        }

        UIAnimScrollList Banners => _pages.Current.GetComponentsInChildren<UIAnimScrollList>().First(l => l.name == "Banners");

        // How far the banner is from the nearest page (0 = on a page).
        static float OffPage(UIAnimScrollList list)
        {
            var page = list.ItemSize.x + list.spacing;
            var r = Mathf.Repeat(list.Offset - list.padding.left, page);
            return Mathf.Min(r, page - r);
        }

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
            yield return RpgPlayModeTests.ToLobby(_pages);
        }

        // Waits for the auto scroll to start moving, and returns a little way into the move.
        IEnumerator MidAutoScroll(UIAnimScrollList list)
        {
            var start = list.Offset;
            var end = Time.realtimeSinceStartup + 6f;
            while (Mathf.Abs(list.Offset - start) < 1f && Time.realtimeSinceStartup < end) yield return null;
            yield return new WaitForSeconds(0.1f);
            Assert.Greater(OffPage(list), 20f, "넘어가는 중");
        }

        [UnityTest]
        public IEnumerator 넘어가는_중에_다른_화면으로_갔다_오면_배너가_페이지에_멈춘다()
        {
            yield return Load();
            var list = Banners;
            yield return MidAutoScroll(list);
            _pages.Current.GetComponentsInChildren<Button>().First(b => b.name == "HEROESButton").onClick.Invoke();   // the lobby goes inactive mid-move
            yield return Idle();
            _pages.Current.GetComponentsInChildren<Button>().First(b => b.name == "BackButton").onClick.Invoke();
            yield return Idle();
            Assert.Less(OffPage(list), 1f, "두 장 사이에 걸리지 않음");
        }

        [UnityTest]
        public IEnumerator 넘어가는_중에_목록이_꺼지면_가던_페이지에_도착한다()
        {
            yield return Load();
            var list = Banners;
            yield return MidAutoScroll(list);
            list.gameObject.SetActive(false);
            Assert.Less(OffPage(list), 1f, "꺼지는 순간 도착");
            list.gameObject.SetActive(true);
        }

        [UnityTest]
        public IEnumerator 끄는_중에_화면이_바뀌어도_자동_넘김이_계속된다()
        {
            yield return Load();
            var list = Banners;
            var pointer = new PointerEventData(EventSystem.current) { position = new Vector2(500, 500), pressPosition = new Vector2(520, 500) };
            ExecuteEvents.Execute(list.gameObject, pointer, ExecuteEvents.beginDragHandler);   // a drag that never ends: the page closes under it
            list.SetOffset(list.Offset + 400f);   // dragged to between two banners
            Assert.Greater(OffPage(list), 20f);
            list.gameObject.SetActive(false);
            list.gameObject.SetActive(true);
            yield return null;
            Assert.Less(OffPage(list), 1f, "페이지에 맞춰짐");
            var start = list.Offset;
            yield return new WaitForSeconds(list.autoScrollInterval + list.snapDuration + 0.3f);
            Assert.Greater(Mathf.Abs(list.Offset - start), 1f, "자동 넘김이 다시 동작");
        }
    }
}
