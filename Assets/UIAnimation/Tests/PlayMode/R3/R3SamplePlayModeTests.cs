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
    /// <summary>The R3 sample scene (Samples/R3): button streams → states, animation events → log.</summary>
    public sealed class R3SamplePlayModeTests
    {
        const string ScenePath = "Assets/UIAnimation/Samples/R3/Scenes/R3Popup.unity";

        static Button Named(string name) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b => b.name == name);

        [UnityTest]
        public IEnumerator Open_연타는_한_번만_재생되고_이벤트와_끝이_로그에_남는다()
        {
#if UNITY_EDITOR
            if (!System.IO.File.Exists(ScenePath)) Assert.Ignore("R3 샘플 장면이 없음");
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            var popup = GameObject.Find("Popup");
            var log = GameObject.Find("Log").GetComponent<TMP_Text>();
            var open = Named("OpenButton");
            open.onClick.Invoke();
            open.onClick.Invoke();   // within 0.5 s: thinned out (ThrottleFirst)
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(1f, popup.transform.localScale.x, 0.01f);
            Assert.AreEqual(1, Count(log.text, "Open 끝"), log.text);
            Assert.AreEqual(1, Count(log.text, "이벤트 Shown"), log.text);

            Named("CloseButton").onClick.Invoke();
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(0f, popup.GetComponent<CanvasGroup>().alpha, 0.01f);
            Assert.AreEqual(1, Count(log.text, "Close 끝"), log.text);
        }

        static int Count(string text, string part) => text.Split('\n').Count(l => l.StartsWith(part));
    }
}
