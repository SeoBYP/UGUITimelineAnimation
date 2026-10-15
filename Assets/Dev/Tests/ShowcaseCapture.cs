using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SeoBYP.TimelineAnimation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Dev-only: plays the Showcase sample and saves frames as PNG, to look at the motion without opening the editor.
/// Runs only when UIANIM_CAPTURE is set to an output folder (skipped otherwise, e.g. on CI).
/// </summary>
public sealed class ShowcaseCapture
{
    const string ScenePath = "Assets/UIAnimation/Samples/Showcase/Scenes/Showcase.unity";
    static readonly string[] Themes = { "Casual", "SciFi", "Fantasy" };
    string _dir;
    Camera _camera;
    RenderTexture _rt;
    Texture2D _read;
    UIAnimPageContainer _pages;
    UIAnimModalContainer _modals;

    void Snap(string name)
    {
        _camera.Render();
        RenderTexture.active = _rt;
        _read.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(_dir, name + ".png"), _read.EncodeToPNG());
    }

    // Frames at these times after the action (seconds).
    IEnumerator Snaps(string name, params float[] times)
    {
        var start = Time.time;
        foreach (var t in times)
        {
            while (Time.time - start < t) yield return null;
            Snap($"{name}_{t:0.00}");
        }
    }

    static Button Named(Component root, string name) => root.GetComponentsInChildren<Button>(true).First(b => b.name == name);
    UIAnimSheetContainer Sheets => _pages.Current.GetComponentInChildren<UIAnimSheetContainer>();

    static IEnumerator Idle()
    {
        yield return null;
        while (UIAnimScreenContainer.AnyInTransition) yield return null;
        yield return new WaitForSeconds(0.2f);
    }

    [UnityTest]
    public IEnumerator Capture()
    {
        _dir = System.Environment.GetEnvironmentVariable("UIANIM_CAPTURE");
        if (string.IsNullOrEmpty(_dir)) Assert.Ignore("UIANIM_CAPTURE not set");
        Directory.CreateDirectory(_dir);
        LogAssert.ignoreFailingMessages = true;   // URP logs a resource error on manual renders in batch mode
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#endif
        _rt = new RenderTexture(1280, 720, 24);
        _read = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        _camera = Object.FindAnyObjectByType<Camera>();
        _camera.targetTexture = _rt;
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 10;
        }
        _pages = Object.FindAnyObjectByType<UIAnimPageContainer>();
        _modals = Object.FindAnyObjectByType<UIAnimModalContainer>();

        for (var theme = 0; theme < Themes.Length; theme++)
        {
            var n = Themes[theme];
            if (theme == 0) yield return Snaps($"{n}_0lobby", 0.1f, 0.3f, 0.6f, 1.2f);
            yield return Idle();

            Named(_pages.Current, "SHOPTab").onClick.Invoke();
            yield return Snaps($"{n}_1shop", 0.08f, 0.2f, 0.7f);
            yield return Idle();
            Sheets.Active.transform.Find("Card3").GetComponent<Button>().onClick.Invoke();
            yield return Snaps($"{n}_2detail", 0.05f, 0.15f, 0.3f, 1.0f);
            yield return Idle();
            Named(_modals.Current, "CloseButton").onClick.Invoke();
            yield return Snaps($"{n}_3detailclose", 0.05f, 0.15f, 0.3f);
            yield return Idle();

            Named(_pages.Current, "HOMETab").onClick.Invoke();
            yield return Idle();
            Named(Sheets.Active, "RewardButton").onClick.Invoke();
            yield return Snaps($"{n}_4reward", 0.15f, 0.5f, 0.8f, 1.05f, 1.3f, 2.6f);
            yield return Idle();
            Named(_modals.Current, "ClaimButton").onClick.Invoke();
            yield return Idle();
            Named(Sheets.Active, "LevelUpButton").onClick.Invoke();
            yield return Snaps($"{n}_5levelup", 0.15f, 0.45f, 0.8f, 1.3f, 2.6f);
            yield return Idle();
            Named(_modals.Current, "OkButton").onClick.Invoke();
            yield return Idle();
            Named(Sheets.Active, "ToastButton").onClick.Invoke();
            yield return Snaps($"{n}_6toast", 0.1f, 0.8f);
            yield return new WaitForSeconds(2.5f);

            if (theme + 1 < Themes.Length)
            {
                Named(_pages.Current, "THEMETab").onClick.Invoke();
                yield return Idle();
                Named(Sheets.Active, Themes[theme + 1] + "Button").onClick.Invoke();
                yield return Snaps($"{Themes[theme + 1]}_0lobby", 0.1f, 0.3f, 0.6f, 1.2f);
            }
        }
    }
}
