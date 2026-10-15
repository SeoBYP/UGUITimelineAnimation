using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Bench
{
    /// <summary>
    /// Sets up and builds the benchmark from the command line:
    /// -executeMethod Bench.BenchBuild.Setup (defines, Animator assets, scene), then Bench.BenchBuild.BuildMono / BuildIl2cpp.
    /// </summary>
    public static class BenchBuild
    {
        const string Scene = "Assets/Bench/Bench.unity";
        const string Resources = "Assets/Bench/Resources/";

        [MenuItem("Bench/Setup")]
        public static void Setup()
        {
            // DOTween (Pro) is imported by hand (paid, not in the repository); compile its driver only when it is there.
            // DOTWEEN_TEXTMESHPRO turns on DOTween Pro's TextMesh Pro module (DOTweenTMPAnimator), as its setup panel would.
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone).Split(';')
                .Where(d => d != "" && d != "BENCH_DOTWEEN" && d != "DOTWEEN_TEXTMESHPRO").ToList();
            if (Directory.Exists("Assets/Plugins/Demigiant")) defines.AddRange(new[] { "BENCH_DOTWEEN", "DOTWEEN_TEXTMESHPRO" });
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, string.Join(";", defines));

            AnimatorAssets();
            BuildScene();

            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.runInBackground = true;   // keeps running when the window is not focused
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.usePlayerLog = true;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[BenchBuild] Setup done. Defines: " + string.Join(";", defines));
        }

        // Release builds measure time; Development builds measure GC (the allocation counter only works there).
        public static void BuildMono() => Build(ScriptingImplementation.Mono2x, "Builds/Mono/Bench.exe", false);
        public static void BuildIl2cpp() => Build(ScriptingImplementation.IL2CPP, "Builds/IL2CPP/Bench.exe", false);
        public static void BuildMonoDev() => Build(ScriptingImplementation.Mono2x, "Builds/MonoDev/Bench.exe", true);
        public static void BuildIl2cppDev() => Build(ScriptingImplementation.IL2CPP, "Builds/IL2CPPDev/Bench.exe", true);

        static void Build(ScriptingImplementation backend, string path, bool development)
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, backend);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development : BuildOptions.None,
            });
            Debug.Log($"[BenchBuild] {backend}{(development ? " dev" : "")}: {report.summary.result}, {report.summary.totalSize / 1024 / 1024} MB, {path}");
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        // ---------- Animator: one clip and controller per scenario (Text has no Animator version) ----------

        static void AnimatorAssets()
        {
            Directory.CreateDirectory(Resources);

            var pulse = new AnimationClip { name = "BenchPulse" };
            var h = BenchValues.PulseHalf;
            // Flat tangents at the keys: ease in-out between them.
            AnimationCurve Wave(float a, float b) => new(new Keyframe(0, a, 0, 0), new Keyframe(h, b, 0, 0), new Keyframe(2 * h, a, 0, 0));
            foreach (var axis in new[] { "x", "y", "z" })
                pulse.SetCurve("", typeof(RectTransform), "m_LocalScale." + axis, axis == "z" ? AnimationCurve.Constant(0, 2 * h, 1) : Wave(1f, BenchValues.PulseScale));
            pulse.SetCurve("", typeof(Image), "m_Color.r", Wave(1f, BenchValues.Gold.r));
            pulse.SetCurve("", typeof(Image), "m_Color.g", Wave(1f, BenchValues.Gold.g));
            pulse.SetCurve("", typeof(Image), "m_Color.b", Wave(1f, BenchValues.Gold.b));
            pulse.SetCurve("", typeof(Image), "m_Color.a", AnimationCurve.Constant(0, 2 * h, 1));
            var settings = AnimationUtility.GetAnimationClipSettings(pulse);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(pulse, settings);

            var entry = new AnimationClip { name = "BenchEntry" };
            var d = BenchValues.EntryDuration;
            // OutCubic: starts at 3 × the average speed, arrives flat.
            entry.SetCurve("", typeof(RectTransform), "m_AnchoredPosition.x",
                new AnimationCurve(new Keyframe(0, BenchValues.EntryFrom, 0, -3 * BenchValues.EntryFrom / d), new Keyframe(d, 0, 0, 0)));
            entry.SetCurve("", typeof(Image), "m_Color.a", AnimationCurve.Linear(0, 0, d, 1));

            foreach (var clip in new[] { pulse, entry })
            {
                var clipPath = $"{Resources}{clip.name}.anim";
                AssetDatabase.DeleteAsset(clipPath);
                AssetDatabase.CreateAsset(clip, clipPath);
                var controllerPath = $"{Resources}{clip.name}.controller";
                AssetDatabase.DeleteAsset(controllerPath);
                AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);   // state named like the clip
            }
        }

        static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var runner = new GameObject("BenchRunner").AddComponent<BenchRunner>();
            runner.root = (RectTransform)canvas.transform;
            runner.font = AssetDatabase.FindAssets("t:TMP_FontAsset LiberationSans").Select(g => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g))).First();
            EditorSceneManager.SaveScene(scene, Scene);
        }
    }
}
