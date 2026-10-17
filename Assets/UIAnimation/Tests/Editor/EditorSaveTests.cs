using System.Collections;
using System.Linq;
using NUnit.Framework;
using SeoBYP.TimelineAnimation.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>
    /// The window's Auto Save / Save, in every mode: Prefab Mode saves the prefab; in Play mode a running copy of a prefab
    /// (Instantiate, like screens and list items) saves to that prefab and the other running copies take the edit, and an
    /// object of the scene keeps the edit in the scene when Play ends.
    /// </summary>
    public sealed class EditorSaveTests
    {
        const string PrefabPath = "Assets/__SaveTest.prefab";
        const string ScenePath = "Assets/__SaveTest.unity";
        const string OuterPath = "Assets/__SaveTestOuter.prefab";

        [SetUp]
        public void SetUp()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("에디터 창 테스트: 그래픽 장치가 필요해서 건너뜀 (-nographics)");
        }

        [TearDown]
        public void TearDown()
        {
            UIAnimSave.AutoSave = true;
            if (EditorWindow.HasOpenInstances<UIAnimationEditorWindow>()) EditorWindow.GetWindow<UIAnimationEditorWindow>().Close();
            UnityEditor.SceneManagement.StageUtility.GoToMainStage();
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == ScenePath)
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            AssetDatabase.DeleteAsset(OuterPath);   // before the prefab nested in it
            AssetDatabase.DeleteAsset(PrefabPath);
            AssetDatabase.DeleteAsset(ScenePath);
        }

        // Root (UIAnimation "Grow") > Child, whose scale goes 1 → 3.
        static void MakePrefab()
        {
            var root = MakeRoot();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        static GameObject MakeRoot()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            var child = new GameObject("Child", typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
            root.AddComponent<UIAnimation>().states.Add(new UIAnimState
            {
                name = "Grow",
                tracks =
                {
                    new UIAnimTrack
                    {
                        target = child.transform, property = UIAnimProperty.Scale, start = UIAnimStart.KeyValue,
                        keys =
                        {
                            new UIAnimKey { time = 0, value = Vector3.one, ease = UIAnimEase.Linear },
                            new UIAnimKey { time = 1, value = Vector3.one * 3, ease = UIAnimEase.Linear },
                        },
                    },
                },
            });
            return root;
        }

        static IEnumerator UntilSaved()
        {
            var end = Time.realtimeSinceStartup + 5f;
            while (UIAnimSave.HasPending && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsFalse(UIAnimSave.HasPending, "자동 저장됨");
        }

        static UIAnimKey EndKey(GameObject go) => go.GetComponent<UIAnimation>().states[0].tracks[0].keys[1];

        // The end key's Value field in the window (the linked scale slider, 0..2), as the user drags it.
        static void SetEndKeyInWindow(UIAnimationEditorWindow window, UIAnimKey key, float value)
        {
            window.Timeline.KeyClicked(key, false);
            var field = window.Inspector.Query<Label>(className: "unity-base-field__label").ToList().First(l => l.text == "Value").parent;
            ((INotifyValueChanged<float>)field).value = value;
        }

        [UnityTest]
        public IEnumerator Play_중_수정은_다른_복사본과_원본_Prefab에_반영된다()
        {
            MakePrefab();
            Assert.IsNotEmpty(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<UIAnimation>().source, "가져올 때 출처가 적힘");
            // A saved scene (like the RPG sample): there Unity gives copies made while playing a scene id too. The scene also
            // has an instance of the prefab placed in it, which is the scene's (saved with the scene, not into the prefab).
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)).name = "Placed";
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, ScenePath);
            yield return new EnterPlayMode();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var a = Object.Instantiate(prefab);
            var b = Object.Instantiate(prefab);
            StringAssert.StartsWith("Prefab", UIAnimSave.Destination(a.GetComponent<UIAnimation>()), "실행 중 복사본 → Prefab");
            StringAssert.Contains("scene", UIAnimSave.Destination(GameObject.Find("Placed").GetComponent<UIAnimation>()), "씬에 놓인 인스턴스 → 씬");
            var window = EditorWindow.GetWindow<UIAnimationEditorWindow>();
            window.SetTarget(a.GetComponent<UIAnimation>());
            yield return null;   // the window builds its UI
            SetEndKeyInWindow(window, EndKey(a), 1.5f);
            Assert.AreEqual(1.5f, EndKey(a).value.x, 1e-4f, "고친 복사본");
            Assert.IsNotEmpty(a.GetComponent<UIAnimation>().source, "복사본도 출처를 앎");

            Assert.AreEqual(1.5f, EndKey(b).value.x, 1e-4f, "다른 복사본도 바로 바뀜");
            Assert.AreSame(b.transform.Find("Child"), b.GetComponent<UIAnimation>().states[0].tracks[0].target, "대상은 그 복사본의 Child");
            b.GetComponent<UIAnimation>().Sample("Grow", 1f);
            Assert.AreEqual(1.5f, b.transform.Find("Child").localScale.x, 1e-4f, "복사본이 새 값으로 재생");

            var end = Time.realtimeSinceStartup + 5f;
            while (UIAnimSave.HasPending && Time.realtimeSinceStartup < end) yield return null;   // auto save after a short delay
            Assert.IsFalse(UIAnimSave.HasPending, "자동 저장됨");
            yield return new ExitPlayMode();

            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.AreEqual(1.5f, EndKey(saved).value.x, 1e-4f, "Play를 끝내도 Prefab에 남음");
            Assert.AreSame(saved.transform.Find("Child"), saved.GetComponent<UIAnimation>().states[0].tracks[0].target, "Prefab 안의 Child를 가리킴");
        }

        [UnityTest]
        public IEnumerator 자동_저장을_끄면_저장_버튼을_눌러야_남는다()
        {
            MakePrefab();
            UIAnimSave.AutoSave = false;
            yield return new EnterPlayMode();

            var a = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var window = EditorWindow.GetWindow<UIAnimationEditorWindow>();
            window.SetTarget(a.GetComponent<UIAnimation>());
            yield return null;   // the window builds its UI
            SetEndKeyInWindow(window, EndKey(a), 1.8f);
            for (var i = 0; i < 30; i++) yield return null;
            Assert.IsTrue(UIAnimSave.HasPending, "저장 대기");
            Assert.AreEqual(3f, EndKey(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)).value.x, 1e-4f, "아직 Prefab은 그대로");

            UIAnimSave.Save();   // the Save button
            Assert.AreEqual(1.8f, EndKey(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)).value.x, 1e-4f, "누르면 저장");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Prefab_모드에서_고치면_Prefab에_자동_저장된다()
        {
            MakePrefab();
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.OpenPrefab(PrefabPath);
            var anim = stage.prefabContentsRoot.GetComponent<UIAnimation>();
            var window = EditorWindow.GetWindow<UIAnimationEditorWindow>();
            window.SetTarget(anim);
            yield return null;
            SetEndKeyInWindow(window, EndKey(anim.gameObject), 1.5f);
            yield return UntilSaved();

            Assert.AreEqual(1.5f, EndKey(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)).value.x, 1e-4f, "Prefab 파일에 저장");
            Assert.IsFalse(stage.scene.isDirty, "저장 안 됨(*) 표시 없음");
        }

        // Both ways Play mode can start: without a scene reload (this project: the Play mode objects are the scene's own) and
        // with one (Unity's default: Play mode has fresh copies with other ids). Scripts are not reloaded either way: a
        // domain reload inside a test cannot restore the settings after it, and the ids do not depend on it.
        [UnityTest]
        public IEnumerator Play_중_씬_오브젝트_수정은_Play가_끝나면_씬에_남는다([Values(false, true)] bool reload)
        {
            var options = (EditorSettings.enterPlayModeOptionsEnabled, EditorSettings.enterPlayModeOptions);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = reload ? EnterPlayModeOptions.DisableDomainReload
                : EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            try
            {
                foreach (var step in EditInPlayMode()) yield return step;
            }
            finally { (EditorSettings.enterPlayModeOptionsEnabled, EditorSettings.enterPlayModeOptions) = options; }
        }

        IEnumerable EditInPlayMode()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            MakeRoot();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, ScenePath);
            yield return new EnterPlayMode();

            var playing = GameObject.Find("Root");
            var window = EditorWindow.GetWindow<UIAnimationEditorWindow>();
            window.SetTarget(playing.GetComponent<UIAnimation>());
            yield return null;
            window.UpdateStatus();
            Assert.IsTrue(window.SaveButton.enabledSelf && window.AutoSaveToggle.enabledSelf, "Play 중 씬 오브젝트는 켜짐");
            SetEndKeyInWindow(window, EndKey(playing), 1.5f);
            yield return new ExitPlayMode();

            var root = GameObject.Find("Root");
            Assert.AreEqual(1.5f, EndKey(root).value.x, 1e-4f, "Play가 끝나도 남음");
            Assert.AreSame(root.transform.Find("Child"), root.GetComponent<UIAnimation>().states[0].tracks[0].target, "씬의 Child를 가리킴");
            Assert.IsTrue(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty, "씬은 변경됨(*) 표시만");
            StringAssert.DoesNotContain("x: 1.5", System.IO.File.ReadAllText(ScenePath), "씬 파일은 그대로 (다른 변경까지 저장하지 않음)");
        }

        [UnityTest]
        public IEnumerator 편집_모드의_씬_오브젝트는_저장_버튼이_꺼지고_씬_저장으로_저장된다()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            var root = MakeRoot();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, ScenePath);
            var window = EditorWindow.GetWindow<UIAnimationEditorWindow>();
            window.SetTarget(root.GetComponent<UIAnimation>());
            yield return null;
            window.UpdateStatus();
            Assert.IsFalse(window.SaveButton.enabledSelf, "Save 꺼짐");
            Assert.IsFalse(window.AutoSaveToggle.enabledSelf, "Auto Save 꺼짐");
            StringAssert.Contains("Ctrl+S", window.SaveButton.tooltip, "이유와 저장 방법");

            SetEndKeyInWindow(window, EndKey(root), 1.5f);
            for (var i = 0; i < 30; i++) yield return null;
            Assert.IsFalse(UIAnimSave.HasPending, "저장 대기 없음");
            Assert.AreEqual(1.5f, EndKey(root).value.x, 1e-4f, "오브젝트 값은 바뀜");
            Assert.IsTrue(scene.isDirty, "씬은 변경됨(*)");
            StringAssert.DoesNotContain("x: 1.5", System.IO.File.ReadAllText(ScenePath), "씬 파일은 씬을 저장할 때만");
        }

        [UnityTest]
        public IEnumerator 중첩_Prefab의_부분은_바깥_Prefab에_오버라이드로_저장되고_안쪽_Prefab은_그대로다()
        {
            MakePrefab();   // the inner one (Root > Child)
            var outer = new GameObject("Screen", typeof(RectTransform));
            ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), outer.transform)).name = "Inner";
            PrefabUtility.SaveAsPrefabAsset(outer, OuterPath);
            Object.DestroyImmediate(outer);
            yield return new EnterPlayMode();

            var screen = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(OuterPath));
            var inner = screen.transform.Find("Inner").gameObject;
            var window = EditorWindow.GetWindow<UIAnimationEditorWindow>();
            window.SetTarget(inner.GetComponent<UIAnimation>());
            yield return null;
            SetEndKeyInWindow(window, EndKey(inner), 1.5f);
            yield return UntilSaved();
            yield return new ExitPlayMode();

            var savedOuter = AssetDatabase.LoadAssetAtPath<GameObject>(OuterPath).transform.Find("Inner");
            Assert.AreEqual(1.5f, EndKey(savedOuter.gameObject).value.x, 1e-4f, "바깥 Prefab 안의 그 부분에 저장");
            Assert.AreSame(savedOuter.Find("Child"), savedOuter.GetComponent<UIAnimation>().states[0].tracks[0].target, "바깥 Prefab 안의 Child를 가리킴");
            Assert.IsTrue(PrefabUtility.GetPropertyModifications(savedOuter.gameObject).Any(m => m.propertyPath.StartsWith("states")), "오버라이드로 기록");
            Assert.AreEqual(3f, EndKey(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)).value.x, 1e-4f, "안쪽 Prefab은 그대로");
        }

        [UnityTest]
        public IEnumerator Play_중_Play_On_연결과_되돌리기도_Prefab에_저장된다()
        {
            MakePrefab();
            yield return new EnterPlayMode();

            var copy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var window = EditorWindow.GetWindow<UIAnimationEditorWindow>();
            window.SetTarget(copy.GetComponent<UIAnimation>());
            yield return null;
            // Play On, as the state inspector does it: a binding on the UIAnimTrigger next to the animation.
            var trigger = copy.AddComponent<UIAnimTrigger>();
            trigger.bindings.Add(new UIAnimBinding { evt = UIAnimEvent.PointerDown, state = "Grow" });
            UIAnimSave.SaveNow(copy.GetComponent<UIAnimation>());
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsTrue(saved.TryGetComponent<UIAnimTrigger>(out var savedTrigger) && savedTrigger.bindings.Count == 1 && savedTrigger.bindings[0].state == "Grow",
                "Play On 연결도 저장");

            SetEndKeyInWindow(window, EndKey(copy), 1.5f);
            yield return UntilSaved();
            Assert.AreEqual(1.5f, EndKey(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)).value.x, 1e-4f);
            Undo.PerformUndo();
            yield return UntilSaved();
            Assert.AreEqual(3f, EndKey(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)).value.x, 1e-4f, "Ctrl+Z도 Prefab에 반영");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Project에서_고른_Prefab_원본을_고치면_파일에_자동_저장된다()
        {
            MakePrefab();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var window = EditorWindow.GetWindow<UIAnimationEditorWindow>();
            window.SetTarget(asset.GetComponent<UIAnimation>());
            yield return null;
            SetEndKeyInWindow(window, EndKey(asset), 1.5f);
            yield return UntilSaved();
            StringAssert.Contains("x: 1.5", System.IO.File.ReadAllText(PrefabPath), "파일에 저장");
        }

        [Test]
        public void 창의_Ctrl_S는_UI_Animation_Save에_연결되어_있다()
        {
            var binding = UnityEditor.ShortcutManagement.ShortcutManager.instance.GetShortcutBinding("UI Animation/Save");
            var key = binding.keyCombinationSequence.Single();
            Assert.AreEqual((KeyCode.S, true), (key.keyCode, key.action), "Ctrl+S (Mac은 Cmd+S)");
        }
    }
}
