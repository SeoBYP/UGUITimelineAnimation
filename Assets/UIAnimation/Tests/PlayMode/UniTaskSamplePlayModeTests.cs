#if UIANIM_UNITASK
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
    /// <summary>The UniTask sample scene (Samples/UniTask), compiled only when UniTask is installed.</summary>
    public sealed class UniTaskSamplePlayModeTests
    {
        const string ScenePath = "Assets/UIAnimation/Samples/UniTask/Scenes/UniTaskPopup.unity";

        static Button Named(string name) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b => b.name == name);
        static bool Has(TMP_Text log, string part) => log.text.Split('\n').Any(l => l.StartsWith(part));

        [UnityTest]
        public IEnumerator WhenAll_Timeout_이벤트_대기가_로그대로_동작한다()
        {
#if UNITY_EDITOR
            if (!System.IO.File.Exists(ScenePath)) Assert.Ignore("UniTask 샘플 장면이 없음");
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            var popup = GameObject.Find("Popup");
            var log = GameObject.Find("Log").GetComponent<TMP_Text>();

            Named("Wait ShownButton").onClick.Invoke();
            Named("OpenButton").onClick.Invoke();
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(Has(log, "이벤트 Shown"), log.text);
            Assert.IsTrue(Has(log, "Open + Dim 끝"), log.text);
            Assert.AreEqual(1f, popup.transform.localScale.x, 0.01f);
            Assert.AreEqual(0.6f, GameObject.Find("Dim").GetComponent<Image>().color.a, 0.01f, "Dim도 같이 끝남");

            Named("Slow (1s 제한)Button").onClick.Invoke();
            yield return new WaitForSeconds(1.3f);
            Assert.IsTrue(Has(log, "Slow: 1초 제한 넘김"), log.text);
            Assert.AreEqual(0f, Mathf.DeltaAngle(0, popup.transform.localEulerAngles.z), 0.5f, "끝(360°)으로 건너뜀");

            Named("CloseButton").onClick.Invoke();
            yield return null;
            Named("OpenButton").onClick.Invoke();   // takes the popup over mid-close
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(Has(log, "Close 중단됨"), log.text);
        }
    }
}
#endif
