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
/// Dev-only: plays the RPG sample and saves frames as PNG, to look at the motion without opening the editor.
/// Runs only when UIANIM_CAPTURE is set to an output folder (skipped otherwise, e.g. on CI).
/// </summary>
public sealed class RpgCapture
{
    const string ScenePath = "Assets/UIAnimation/Samples/RPG/Scenes/RPG.unity";
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

    static IEnumerator Idle(float extra = 0.3f)
    {
        yield return null;
        while (UIAnimScreenContainer.AnyInTransition) yield return null;
        yield return new WaitForSeconds(extra);
    }

    static Button Named(Component root, string name) => root.GetComponentsInChildren<Button>(true).First(b => b.name == name);
    UIAnimScreen Page => _pages.Current;
    UIAnimScreen Modal => _modals.Current;

    void Click(Component root, string name) => Named(root, name).onClick.Invoke();

    IEnumerator Back(string name)
    {
        Click(Page, "BackButton");
        yield return Snaps(name, 0.15f, 0.3f);
        yield return Idle();
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

        yield return Snaps("01lobby", 0.1f, 0.4f, 0.8f, 1.4f, 4f);

        // Heroes → detail (shared portrait) → scroll (collapsing title) → back
        Click(Page, "HEROESButton");
        yield return Snaps("02heroes", 0.15f, 0.4f, 0.8f);
        yield return Idle();
        Page.GetComponentsInChildren<Button>().First(b => b.name == "Body").onClick.Invoke();
        yield return Snaps("03detail", 0.1f, 0.25f, 0.45f, 1.2f);
        yield return Idle();
        Page.GetComponentInChildren<UIAnimScrollList>().ScrollTo(8, UIAnimScrollList.Alignment.Start, 0.4f);
        yield return Snaps("04detailscroll", 0.6f);
        yield return Back("05detailback");
        yield return Back("06heroesback");

        // Bag → item info (shared icon) → tab
        Click(Page, "BAGButton");
        yield return Idle(0.6f);
        Snap("07bag");
        Page.GetComponentsInChildren<Button>().First(b => b.name == "Body").onClick.Invoke();
        yield return Snaps("08iteminfo", 0.1f, 0.25f, 0.6f);
        yield return Idle();
        Click(Modal, "CloseButton");
        yield return Snaps("09iteminfoclose", 0.15f);
        yield return Idle();
        Page.GetComponentsInChildren<Button>().First(b => b.name.StartsWith("Tab 2")).onClick.Invoke();
        yield return Snaps("10bagtab", 0.15f, 0.8f);
        yield return Back("11bagback");

        // Summon x10
        Click(Page, "SUMMONButton");
        yield return Idle(0.8f);
        Snap("12summon");
        Click(Page, "TenButton");
        yield return Snaps("13summon10", 0.15f, 0.5f, 1.1f, 1.6f, 2.4f, 3.4f, 4.6f, 6f);
        var result = Modal.GetComponent<MonoBehaviour>();
        var end = Time.time + 15f;
        while (Modal.transform.Find("OkButton").GetComponent<Button>().interactable == false && Time.time < end) yield return null;
        yield return new WaitForSeconds(0.6f);
        Snap("14summondone");
        Click(Modal, "OkButton");
        yield return Idle();
        yield return Back("15summonback");

        // Adventure: chapter carousel, victory, defeat
        Click(Page, "ADVENTUREButton");
        yield return Snaps("16adventure", 0.2f, 0.7f, 1.4f);
        yield return Idle();
        Page.GetComponentInChildren<UIAnimScrollList>().ScrollTo(2, UIAnimScrollList.Alignment.Center, 0.4f);
        yield return Snaps("17chapter3", 0.3f, 0.6f, 1.2f);
        Click(Page, "StartButton");
        yield return Snaps("18victory", 0.3f, 0.8f, 1.2f, 1.7f, 2.4f, 3.2f);
        yield return Idle();
        Click(Modal, "ContinueButton");
        yield return Idle();
        Page.GetComponentInChildren<UIAnimScrollList>().ScrollTo(0, UIAnimScrollList.Alignment.Center, 0.3f);
        yield return new WaitForSeconds(1.2f);
        Click(Page, "Stage10");
        Click(Page, "StartButton");
        yield return Snaps("19defeat", 0.3f, 0.8f, 1.5f, 2.2f, 3.2f);
        yield return Idle();
        Click(Modal, "LobbyButton");
        yield return Snaps("20tolobby", 0.2f, 0.5f);
        yield return Idle();

        // Quests: claim
        Click(Page, "QUESTSButton");
        yield return Snaps("21quests", 0.2f, 0.6f);
        yield return Idle(0.6f);
        Page.GetComponentsInChildren<Button>().First(b => b.name == "ClaimButton" && b.interactable).onClick.Invoke();
        yield return Snaps("22claim", 0.1f, 0.25f, 0.6f, 0.85f, 1.3f);
    }
}
