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

    // Every frame-ish (30 fps) through the page opens and quest claims, to find jumps between frames.
    [UnityTest]
    public IEnumerator CaptureDense()
    {
        yield return Setup();
        yield return new WaitForSeconds(2f);
        var steps = Enumerable.Range(1, 30).Select(i => i / 30f).ToArray();
        foreach (var menu in new[] { "HEROES", "BAG", "QUESTS" })
        {
            Click(Page, menu + "Button");
            yield return Snaps("d_" + menu, steps);
            yield return Idle(0.8f);
            if (menu == "QUESTS") break;
            Click(Page, "BackButton");
            yield return Snaps("d_" + menu + "back", steps);
            yield return Idle();
        }
        for (var claim = 0; claim < 2; claim++)
        {
            Page.GetComponentsInChildren<Button>().First(b => b.name == "ClaimButton" && b.interactable).onClick.Invoke();
            yield return Snaps($"d_claim{claim}", Enumerable.Range(1, 45).Select(i => i / 30f).ToArray());
            yield return new WaitForSeconds(0.5f);
        }
    }

    IEnumerator Setup(bool toLobby = true)
    {
        _dir = System.Environment.GetEnvironmentVariable("UIANIM_CAPTURE");
        if (string.IsNullOrEmpty(_dir)) Assert.Ignore("UIANIM_CAPTURE not set");
        Directory.CreateDirectory(_dir);
        LogAssert.ignoreFailingMessages = true;
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
        if (!toLobby) yield break;
        yield return Idle();
        Click(Page, "TapButton");   // title → loading → lobby
        while (Page == null || !Page.name.StartsWith("Lobby")) yield return null;
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
        yield return Idle();
        Click(Page, "TapButton");   // title → loading → lobby
        while (Page == null || !Page.name.StartsWith("Lobby")) yield return null;

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

    // The second set of screens: title, loading, shop and purchase, upgrade, formation and battle, mail, attendance.
    [UnityTest]
    public IEnumerator CapturePhase2()
    {
        yield return Setup(toLobby: false);
        yield return Snaps("p2_01title", 0.2f, 0.6f, 1.0f, 1.6f, 2.4f);
        Click(Page, "TapButton");
        yield return Snaps("p2_02loading", 0.3f, 0.9f, 1.5f, 2.2f);
        while (!Page.name.StartsWith("Lobby")) yield return null;
        yield return Snaps("p2_03lobby", 0.5f, 1.6f);
        yield return Idle();

        Click(Page, "SHOPButton");
        yield return Snaps("p2_04shop", 0.2f, 0.6f, 1.2f);
        yield return Idle();
        Page.GetComponentsInChildren<Button>().First(b => b.name == "Body").onClick.Invoke();
        yield return Snaps("p2_05purchase", 0.15f, 0.5f);
        yield return Idle();
        Click(Modal, "BuyButton");
        yield return Snaps("p2_06bought", 0.3f, 0.8f);
        yield return Idle();
        Click(Page, "BackButton");
        yield return Idle();

        Click(Page, "HEROESButton");
        yield return Idle();
        Page.GetComponentsInChildren<Button>().First(b => b.name == "Body").onClick.Invoke();
        yield return Idle();
        Click(Page, "UpgradeButton");
        yield return Snaps("p2_07upgrade", 0.2f, 0.7f);
        yield return Idle();
        Click(Page, "UpgradeButton");
        yield return Snaps("p2_08levelup", 0.08f, 0.3f, 0.8f);
        Click(Page, "AutoEquipButton");
        yield return Snaps("p2_09equip", 0.1f, 0.35f, 0.6f, 1.2f);
        yield return Idle();
        Click(Page, "BackButton");
        yield return Idle();
        Click(Page, "BackButton");
        yield return Idle();
        Click(Page, "BackButton");
        yield return Idle();

        Click(Page, "ADVENTUREButton");
        yield return Idle(1.2f);
        Click(Page, "StartButton");
        yield return Snaps("p2_10formation", 0.2f, 0.6f);
        yield return Idle();
        Click(Page, "Hero3");
        yield return Snaps("p2_11slotin", 0.1f, 0.25f, 0.5f);
        Click(Page, "BattleButton");
        yield return Snaps("p2_12battle", 0.2f, 0.45f, 0.75f, 1.2f, 2.5f);
        yield return Idle(2.5f);
        Click(Modal, "LobbyButton");
        yield return Idle();
        yield return Idle();

        Click(Page, "MAILButton");
        yield return Snaps("p2_13mail", 0.3f, 0.8f);
        yield return Idle(0.5f);
        Click(Page, "ClaimAllButton");
        yield return Snaps("p2_14claimall", 0.2f, 0.5f, 1.5f, 4.5f);
        yield return Idle(0.5f);
        Click(Page, "BackButton");
        yield return Idle();

        Click(Page, "DAILYButton");
        yield return Snaps("p2_15attendance", 0.15f, 0.5f, 1.2f);
        Click(Modal, "ClaimButton");
        yield return Snaps("p2_16stamp", 0.05f, 0.2f, 0.6f);
    }

    // The third set: settings and confirm and toast, quantity wheel, battle HUD, guild chat, ranking, story.
    [UnityTest]
    public IEnumerator CapturePhase3()
    {
        yield return Setup();
        yield return Idle(1f);
        // The toast canvas is an overlay too: put it on the camera as well.
        Click(Page, "SETTINGSButton");
        yield return Snaps("p3_01settings", 0.15f, 0.5f);
        yield return Idle();
        Click(Modal, "MusicSwitch");
        yield return Snaps("p3_02switch", 0.1f, 0.3f);
        Click(Modal, "ResetButton");
        yield return Snaps("p3_03confirm", 0.15f, 0.5f);
        yield return Idle();
        Click(Modal, "OkButton");
        yield return Snaps("p3_04toast", 0.2f, 0.5f, 1.5f, 2.4f);
        yield return Idle();
        Click(Modal, "CloseButton");
        yield return Idle();

        Click(Page, "BAGButton");
        yield return Idle(0.8f);
        Page.GetComponentsInChildren<Button>().First(b => b.transform.Find("Icon")?.GetComponent<Image>().sprite?.name == "helm").onClick.Invoke();
        yield return Idle();
        Click(Modal, "UseButton");
        yield return Snaps("p3_05quantity", 0.15f, 0.5f);
        yield return Idle();
        Modal.GetComponentInChildren<UIAnimScrollList>().ScrollTo(1, UIAnimScrollList.Alignment.Center, 0.4f);
        yield return Snaps("p3_06wheel", 0.15f, 0.3f, 0.6f);
        Click(Modal, "UseButton");
        yield return Snaps("p3_07used", 0.3f, 0.6f);
        yield return Idle(2f);
        Click(Page, "BackButton");
        yield return Idle();

        Click(Page, "ADVENTUREButton");
        yield return Idle(1.2f);
        Page.GetComponentInChildren<UIAnimScrollList>().ScrollTo(2, UIAnimScrollList.Alignment.Center, 0f);
        yield return new WaitForSeconds(1.3f);
        Click(Page, "StartButton");
        yield return Idle();
        Click(Page, "BattleButton");
        yield return Snaps("p3_08battle", 0.5f, 1.2f, 1.6f, 2.0f);
        Click(Page, "Skill0");
        yield return Snaps("p3_09skill", 0.05f, 0.2f, 0.5f);
        while (Modal == null) yield return null;
        yield return Snaps("p3_10won", 0.1f, 1.5f);
        yield return Idle(2.5f);
        Click(Modal, "LobbyButton");
        yield return Idle();
        yield return Idle();

        Click(Page, "GUILDButton");
        yield return Snaps("p3_11guild", 0.3f, 0.8f);
        yield return Idle(0.5f);
        Click(Page, "Quick0");
        yield return Snaps("p3_12chat", 0.1f, 0.3f, 1.0f, 1.5f, 1.8f);
        yield return Idle(1f);
        Click(Page, "BackButton");
        yield return Idle();

        Click(Page, "RANKButton");
        yield return Snaps("p3_13ranking", 0.3f, 0.7f, 1.1f, 1.6f);
        yield return Idle();
        Click(Page, "MyRankButton");
        yield return Snaps("p3_14myrank", 0.3f, 0.7f, 1.0f);
        yield return Idle(0.5f);
        Click(Page, "BackButton");
        yield return Idle();

        Click(Page, "STORYButton");
        yield return Snaps("p3_15story", 0.2f, 0.6f, 1.2f, 2.0f);
        Click(Page, "NextButton");
        Click(Page, "NextButton");
        yield return Snaps("p3_16story2", 0.15f, 0.5f, 1.0f);
        Click(Page, "NextButton");
        yield return null;
        Click(Page, "NextButton");
        yield return null;
        Click(Page, "NextButton");
        yield return Snaps("p3_17choices", 0.1f, 0.4f);
    }
}
