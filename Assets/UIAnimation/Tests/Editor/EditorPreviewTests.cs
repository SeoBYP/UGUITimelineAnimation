using System.Collections;
using NUnit.Framework;
using SeoBYP.TimelineAnimation.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>
    /// The window's preview where it is started from: a prefab asset picked in the Project window (opened in Prefab Mode
    /// first, since an asset is drawn nowhere) and Play mode (values written directly, put back on Stop).
    /// </summary>
    public sealed class EditorPreviewTests
    {
        const string PrefabPath = "Assets/__PreviewTest.prefab";
        UIAnimationEditorWindow _window;

        static GameObject Grow(out UIAnimation anim)
        {
            var go = new GameObject("Grow", typeof(RectTransform));
            anim = go.AddComponent<UIAnimation>();
            anim.states.Add(new UIAnimState
            {
                name = "Grow",
                tracks =
                {
                    new UIAnimTrack
                    {
                        target = go.transform, property = UIAnimProperty.Scale, start = UIAnimStart.KeyValue,
                        keys =
                        {
                            new UIAnimKey { time = 0, value = Vector3.one, ease = UIAnimEase.Linear },
                            new UIAnimKey { time = 1, value = Vector3.one * 3, ease = UIAnimEase.Linear },
                        },
                    },
                },
            });
            return go;
        }

        [SetUp]
        public void SetUp()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("에디터 창 테스트: 그래픽 장치가 필요해서 건너뜀 (-nographics)");
            _window = EditorWindow.GetWindow<UIAnimationEditorWindow>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null) _window.Close();
            StageUtility.GoToMainStage();
            AssetDatabase.DeleteAsset(PrefabPath);
        }

        [Test]
        public void Project에서_고른_Prefab을_재생하면_Prefab_모드로_열어_미리본다()
        {
            var source = Grow(out _);
            var asset = PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
            Object.DestroyImmediate(source);
            _window.SetTarget(asset.GetComponent<UIAnimation>());

            _window.TogglePlay();

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            Assert.NotNull(stage, "Prefab 모드로 열림");
            Assert.AreEqual(PrefabPath, stage.assetPath);
            Assert.IsFalse(EditorUtility.IsPersistent(_window.Target), "창이 Prefab 모드 안의 사본을 가리킴");
            Assert.AreEqual("Grow", _window.Preview.State?.name, "같은 상태를 미리보기");
            _window.Preview.Seek(0.5f);
            Assert.AreEqual(2f, _window.Target.transform.localScale.x, 1e-3f, "화면에 보이는 사본이 움직임");
            _window.Preview.Stop();
            Assert.AreEqual(1f, asset.transform.localScale.x, 1e-3f, "원본은 그대로");
        }

        [UnityTest]
        public IEnumerator Play_모드에서도_미리보고_멈추면_원래_값으로_돌아온다()
        {
            yield return new EnterPlayMode();
            var go = Grow(out var anim);
            _window = EditorWindow.GetWindow<UIAnimationEditorWindow>();
            _window.SetTarget(anim);

            _window.TogglePlay();
            Assert.IsTrue(_window.Preview.IsPlaying, "Play 모드에서도 재생");
            _window.Preview.Seek(0.5f);
            Assert.AreEqual(2f, go.transform.localScale.x, 1e-3f, "값이 바로 바뀜");
            _window.Preview.Stop();
            Assert.AreEqual(1f, go.transform.localScale.x, 1e-3f, "멈추면 원래 값");

            Object.Destroy(go);
            yield return new ExitPlayMode();
        }
    }
}
