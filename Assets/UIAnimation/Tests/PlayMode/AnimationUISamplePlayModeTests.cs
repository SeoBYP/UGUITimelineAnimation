using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>
    /// The Unity-AnimationUI demo rebuilt with UI Animation (Assets/UIAnimation/Samples/AnimationUI), in real Play mode.
    /// Expected values are the original's (Data/AnimationUI-Demo.json, read from the original scenes).
    /// </summary>
    public sealed class AnimationUISamplePlayModeTests
    {
        const string Folder = "Assets/UIAnimation/Samples/AnimationUI/Scenes/";
        static readonly Color Red = new(0.9490196f, 0.43529412f, 0.40392157f, 1f);
        static readonly Color Green = new(0.5137255f, 0.91764706f, 0.5764706f, 1f);
        readonly List<string> _sounds = new();

        [SetUp]
        public void SetUp()
        {
            _sounds.Clear();
            UIAnimation.SoundPlayed = (_, sound) => _sounds.Add(sound.clip.name);
        }

        [TearDown]
        public void TearDown() => UIAnimation.SoundPlayed = null;

        static IEnumerator Load(string scene)
        {
#if UNITY_EDITOR
            if (!System.IO.File.Exists(Folder + scene + ".unity")) Assert.Ignore("AnimationUI 샘플이 없음");
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(Folder + scene + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("에디터 전용");
#endif
            yield return null;
        }

        // Paths as in the original scene. Inactive objects included.
        static Transform Find(string path)
        {
            var parts = path.Split('/');
            var root = SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == parts[0]).transform;
            foreach (var part in parts.Skip(1)) root = root.Cast<Transform>().First(c => c.name == part);
            return root;
        }

        static IEnumerator WaitForScene(string name, float seconds)
        {
            var end = Time.realtimeSinceStartup + seconds;
            while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < end) yield return null;
            yield return null;
        }

        static void Click(string path) => Find(path).GetComponent<Button>().onClick.Invoke();
        static bool Active(string path) => Find(path).gameObject.activeSelf;
        static bool InputBlocked => Find("InputBlocker/Block").gameObject.activeSelf;
        static Camera Camera => Find("Main Camera").GetComponent<Camera>();
        static RectTransform Rect(string path) => (RectTransform)Find(path);

        static void Near(Vector2 expected, Vector2 actual, string what) =>
            Assert.Less(Vector2.Distance(expected, actual), 0.5f, $"{what}: 기대 {expected}, 실제 {actual}");

        static void Near(Color expected, Color actual, string what) =>
            Assert.Less(Vector4.Distance(expected, actual), 0.01f, $"{what}: 기대 {expected}, 실제 {actual}");

        [UnityTest]
        public IEnumerator Home_시작_페이드_후_Settings로_갔다가_Back으로_되돌아온다()
        {
            yield return Load("Home");
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(InputBlocked, "시작 페이드 동안 입력 막힘 (0.01 ~ 0.51초)");
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0f, Find("Home/Image").GetComponent<Image>().color.a, 0.01f, "흰 화면 사라짐 (0.5초 OutQuart)");
            Assert.IsFalse(InputBlocked);

            Click("Home/Settings");
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(InputBlocked, "전환 중 입력 막힘");
            yield return new WaitForSeconds(1.1f);
            Assert.IsTrue(Active("Settings"));
            Assert.IsFalse(Active("Home"), "1.01초에 Home 끔");
            Near(new Vector2(0.5f, 0.5f), Rect("Settings/1").anchorMin, "행 1 앵커");
            Near(new Vector2(-371f, 113f), Rect("Settings/1").anchoredPosition, "행 1 위치");
            Near(new Vector2(485.19f, -123f), Rect("Settings/4").anchoredPosition, "행 4 위치");
            Near(Green, Camera.backgroundColor, "카메라 배경 초록");
            Assert.IsFalse(InputBlocked);
            Assert.AreEqual(8, _sounds.Count(s => s == "ButtonMove"), "단계 효과음 8번 (원본 SFX 단계 수)");

            Click("Settings/Back");   // the original's PlayReversed
            yield return new WaitForSeconds(1.4f);
            Assert.IsTrue(Active("Home"));
            Assert.IsFalse(Active("Settings"));
            Near(Red, Camera.backgroundColor, "카메라 배경 다시 빨강");
            Near(new Vector2(342f, 455f), Rect("Home/Start").anchoredPosition, "메뉴 버튼 제자리");
            Near(new Vector2(0f, 223f), Rect("Home/Title").anchoredPosition, "제목 제자리");
            Assert.IsFalse(InputBlocked);
        }

        [UnityTest]
        public IEnumerator Upgrade로_갔다가_Back으로_되돌아온다()
        {
            yield return Load("Home");
            yield return new WaitForSeconds(0.6f);
            Click("Home/Upgrade");
            yield return new WaitForSeconds(2.0f);
            Assert.IsTrue(Active("Upgrade"));
            Assert.IsFalse(Active("Home"));
            Near(new Vector2(1f, 1f), Rect("Upgrade/BGYellow").anchorMax, "노란 배경 펼침 (앵커)");
            Near(new Vector2(-585f, 0f), Rect("Upgrade/BGYellow").sizeDelta, "노란 배경 크기");
            foreach (var card in new[] { "Upgrade1", "Upgrade1 (1)", "Upgrade1 (2)", "Upgrade1 (3)" })
                Assert.AreEqual(1f, Find("Upgrade/" + card).localScale.x, 0.01f, card);
            Near(new Vector2(-282.83f, -69f), Rect("Upgrade/Option 1").anchoredPosition, "옵션 1");

            Click("Upgrade/Back");
            yield return new WaitForSeconds(1.9f);
            Assert.IsTrue(Active("Home"));
            Assert.IsFalse(Active("Upgrade"));
            Assert.AreEqual(0f, Find("Upgrade/Upgrade1").localScale.x, 0.01f, "카드 다시 0");
            Near(new Vector2(342f, 345f), Rect("Home/Upgrade").anchoredPosition, "메뉴 제자리");
        }

        [UnityTest]
        public IEnumerator Start는_카메라를_줌하고_줄무늬로_덮은_뒤_Level1로_간다()
        {
            yield return Load("Home");
            yield return new WaitForSeconds(0.6f);
            Click("Home/Start");
            yield return new WaitForSeconds(0.85f);
            Assert.AreEqual(0.5f, Camera.orthographicSize, 0.01f, "카메라 크기 5 → 0.5 (0.8초 InCubic)");
            yield return new WaitForSeconds(0.6f);
            // Left (2): 0.71 s + 0.7 s = 1.41 s, done; the last pair ends with the scene change at 1.61 s.
            Assert.AreEqual(1f, Find("Home/TransitionToStart/Left (2)").GetComponent<Image>().fillAmount, 0.01f, "줄무늬로 덮음");
            yield return WaitForScene("Level1", 2f);
            Assert.AreEqual("Level1", SceneManager.GetActiveScene().name, "1.61초에 씬 전환");
        }

        [UnityTest]
        public IEnumerator Level1_박스_등장_후_Win과_Lose_그리고_Home으로()
        {
            yield return Load("Level1");
            yield return new WaitForSeconds(2.4f);
            foreach (var box in new[] { "Box", "Box (1)", "Box (2)", "Box (3)", "Box (4)" })
            {
                Assert.AreEqual(1f, Find("Home/1/" + box).localScale.x, 0.01f, box + " 크기");
                Assert.AreEqual(0f, Mathf.DeltaAngle(0, Find("Home/1/" + box).localEulerAngles.z), 0.5f, box + " 회전");
            }
            Assert.AreEqual(1f, Find("Home/Next").localScale.x, 0.01f);
            Assert.IsFalse(InputBlocked);

            Click("Home/1/Box");   // a wrong box
            yield return new WaitForSeconds(0.7f);
            Assert.IsTrue(Active("CanvasLose"));
            Assert.AreEqual(1f, Find("CanvasLose").GetComponent<CanvasGroup>().alpha, 0.01f, "Lose 팝업 알파");
            Assert.AreEqual(1f, Find("CanvasLose/Blocker").localScale.x, 0.01f, "1.5 → 1");

            Find("CanvasLose").gameObject.SetActive(false);
            Click("Home/1/Box (4)");   // the right one
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(Active("CanvasWin"));
            Assert.AreEqual(1f, Find("CanvasWin").GetComponent<CanvasGroup>().alpha, 0.01f, "Win 팝업 알파");

            Click("CanvasWin/Blocker/Home");
            yield return WaitForScene("Home", 2f);
            Assert.AreEqual("Home", SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator 버튼은_hover에_커지고_색이_나타나며_나가면_돌아온다()
        {
            yield return Load("Home");
            yield return new WaitForSeconds(0.6f);
            var button = Find("Home/Settings");
            var image = button.GetComponent<Image>();
            Assert.AreEqual(0f, image.color.a, 0.01f, "메뉴 버튼은 평소 투명");
            var data = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(1.1f, button.localScale.x, 0.01f, "hover 1.1배");
            Assert.AreEqual(1f, image.color.a, 0.01f, "hover 색");
            Assert.Contains("ButtonHover", _sounds);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerExitHandler);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(1f, button.localScale.x, 0.01f);
            Assert.AreEqual(0f, image.color.a, 0.01f);
        }
    }
}
