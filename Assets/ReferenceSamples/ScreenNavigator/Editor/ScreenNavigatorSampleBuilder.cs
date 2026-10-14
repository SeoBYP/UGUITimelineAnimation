using System.IO;
using System.Linq;
using SeoBYP.TimelineAnimation;
using SeoBYP.TimelineAnimation.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Dev-only: rebuilds the UnityScreenNavigator demo (Top → Loading → Home → Shop) with UI Animation.
/// The original prefabs (Original~, layout untouched, same GUIDs) are copied to Prefabs/, their scripts and Timeline
/// objects removed, and the transitions written as UIAnimation states with the original values
/// (the timelines' UnityUIPlayables clips and the SimpleTransitionAnimation assets).
/// </summary>
static class ScreenNavigatorSampleBuilder
{
    const string Root = "Assets/ReferenceSamples/ScreenNavigator/";
    const string Prefabs = Root + "Prefabs/";
    static readonly string[] Order =   // nested prefabs first, so the ones that contain them load clean copies
    {
        "prefab_unit_thumbnail", "prefab_unit_shop_item", "prefab_unit_shop_item_set_sheet", "prefab_home_text_button",
        "prefab_top_page", "prefab_loading_page", "prefab_home_page", "prefab_unit_shop_page",
    };

    [MenuItem("Tools/UIAnimation Dev/Rebuild ScreenNavigator Sample")]
    public static void Build()
    {
        Directory.CreateDirectory(Prefabs);
        foreach (var name in Order)
        {
            File.Copy($"{Root}Original~/{name}.prefab", $"{Prefabs}{name}.prefab", true);
            File.Copy($"{Root}Original~/{name}.prefab.meta", $"{Prefabs}{name}.prefab.meta", true);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        foreach (var name in Order) Convert(name);
        var scene = BuildScene();
        AddToBuildSettings(scene);
        Debug.Log($"[ScreenNavigatorSampleBuilder] Built {Prefabs}*.prefab, {scene}");
    }

    // ---------- prefabs ----------

    static void Convert(string name)
    {
        var path = $"{Prefabs}{name}.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (!PrefabUtility.IsPartOfPrefabInstance(t.gameObject) || PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        // The original's Timeline transitions (PlayableDirector) are replaced by states.
        foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t.name is "Transitions" or "Transition").ToList())
            Object.DestroyImmediate(t.gameObject);

        switch (name)
        {
            case "prefab_unit_shop_item": ShopItem(root); break;
            case "prefab_unit_shop_item_set_sheet": ItemSetSheet(root); break;
            case "prefab_top_page": TopPage(root); break;
            case "prefab_loading_page": LoadingPage(root); break;
            case "prefab_home_page": HomePage(root); break;
            case "prefab_unit_shop_page": ShopPage(root); break;
        }
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static T Get<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static Transform Find(GameObject root, string path)
    {
        var t = root.transform.Find(path);
        if (t == null) throw new System.Exception($"[ScreenNavigatorSampleBuilder] no {path} in {root.name}");
        return t;
    }

    static UIAnimScreen Screen(GameObject root)
    {
        Get<CanvasGroup>(root);
        Get<UIAnimation>(root);
        return Get<UIAnimScreen>(root);
    }

    // A track from → to over [start, start + duration] (holding `from` before), as a UnityUIPlayables clip.
    static UIAnimTrack Clip(Component target, UIAnimProperty property, Vector4 from, Vector4 to, float start, float duration, UIAnimEase ease)
    {
        var track = new UIAnimTrack { target = target, property = property, start = UIAnimStart.KeyValue };
        track.keys.Add(new UIAnimKey { time = start, value = from });
        track.keys.Add(new UIAnimKey { time = start + duration, value = to, ease = ease });
        UIAnimationEditorWindow.EnsureStartKey(track);
        return track;
    }

    static void States(GameObject root, params UIAnimState[] states) => root.GetComponent<UIAnimation>().states.AddRange(states);
    static UIAnimState State(string name, params UIAnimTrack[] tracks) => new() { name = name, tracks = tracks.ToList() };
    static Vector4 A(float a) => new(a, 0, 0, 0);

    // Top: push exit slides the bars out and fades the texts and background (0.5 s OutQuad); pop enter is the reverse.
    // Push enter and pop exit: the container defaults.
    static void TopPage(GameObject root)
    {
        Screen(root);
        var top = Find(root, "Top");
        var bottom = Find(root, "Bottom");
        var title = Find(root, "TitleText").GetComponent<TMP_Text>();
        var tap = Find(root, "TapToStartText").GetComponent<TMP_Text>();
        var bg = root.GetComponent<Image>();
        Color white(float a) => new(1, 1, 1, a);
        UIAnimState Bars(string name, bool exit)
        {
            UIAnimTrack T(Component c, UIAnimProperty p, Vector4 shown, Vector4 gone) =>
                Clip(c, p, exit ? shown : gone, exit ? gone : shown, 0f, 0.5f, UIAnimEase.OutQuad);
            return State(name,
                T(top, UIAnimProperty.AnchoredPosition, Vector4.zero, new Vector4(0, 126)),
                T(bottom, UIAnimProperty.AnchoredPosition, Vector4.zero, new Vector4(0, -126)),
                T(title, UIAnimProperty.GraphicColor, white(0.7490196f), white(0)),
                T(tap, UIAnimProperty.GraphicColor, white(0.7490196f), white(0)),
                T(bg, UIAnimProperty.GraphicColor, white(1), white(0)));
        }
        States(root, Bars("PushExit", true), Bars("PopEnter", false));
        Get<UsnTopPage>(root).tap = Find(root, "Button").GetComponent<Button>();
    }

    // Loading: SimpleFadeIn (0.3 s, QuadIn) / SimpleFadeOut (0.3 s, QuadOut) on the page's CanvasGroup. Drawn above Home.
    static void LoadingPage(GameObject root)
    {
        var screen = Screen(root);
        screen.renderingOrder = 100;
        var group = root.GetComponent<CanvasGroup>();
        States(root,
            State("PushEnter", Clip(group, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), 0f, 0.3f, UIAnimEase.InQuad)),
            State("PushExit", Clip(group, UIAnimProperty.CanvasGroupAlpha, A(1), A(0), 0f, 0.3f, UIAnimEase.OutQuad)));
        Get<UsnLoadingPage>(root);
    }

    // Home (timelines home_push_enter / push_exit / pop_enter / pop_exit, 0.5 s OutQuad).
    static void HomePage(GameObject root)
    {
        var screen = Screen(root);
        screen.renderingOrder = 10;
        var group = root.GetComponent<CanvasGroup>();
        var icons = Find(root, "TopLeftIcons");
        var menu = Find(root, "RightMenu");
        States(root,
            State("PushEnter", Clip(group, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), 0f, 0.5f, UIAnimEase.OutQuad)),
            State("PushExit",
                Clip(group, UIAnimProperty.CanvasGroupAlpha, A(1), A(0), 0.25f, 0.25f, UIAnimEase.OutQuad),
                Clip(icons, UIAnimProperty.AnchoredPosition, new Vector4(48, -48), new Vector4(48, 86), 0f, 0.5f, UIAnimEase.OutQuad),
                Clip(menu, UIAnimProperty.Rotation, new Vector4(0, 11), new Vector4(0, 58.9f), 0f, 0.5f, UIAnimEase.OutQuad)),
            State("PopEnter",
                Clip(group, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), 0f, 0.5f, UIAnimEase.OutQuad),
                Clip(icons, UIAnimProperty.AnchoredPosition, new Vector4(48, 86), new Vector4(48, -48), 0f, 0.5f, UIAnimEase.OutQuad),
                Clip(menu, UIAnimProperty.Rotation, new Vector4(0, 58.9f), new Vector4(0, 11), 0f, 0.5f, UIAnimEase.OutQuad)),
            State("PopExit", Clip(group, UIAnimProperty.CanvasGroupAlpha, A(1), A(0), 0f, 0.5f, UIAnimEase.OutQuad)));

        var page = Get<UsnHomePage>(root);
        page.back = Find(root, "TopLeftIcons/BackButton").GetComponent<Button>();
        page.menu = new[] { "UnitShopButton", "SettingsButton", "MainQuestButton", "EventQuestButton", "MissionButton" }
            .Select(n => root.GetComponentsInChildren<Button>(true).First(b => b.name == n)).ToArray();   // one sits outside RightMenu
        page.shop = page.menu[0];
        page.settings = page.menu[1];
    }

    // Shop: push enter / exit and pop enter use the defaults (the original's push_enter timeline is not assigned);
    // pop exit fades out (timeline shop_pop_exit, 0.5 s OutQuad).
    static void ShopPage(GameObject root)
    {
        Screen(root);
        var group = root.GetComponent<CanvasGroup>();
        States(root, State("PopExit", Clip(group, UIAnimProperty.CanvasGroupAlpha, A(1), A(0), 0f, 0.5f, UIAnimEase.OutQuad)));
        var page = Get<UsnShopPage>(root);
        var sheets = Find(root, "ItemList/TabGroup/Sheet Container").gameObject;
        page.sheets = Get<UIAnimSheetContainer>(sheets);
        page.itemSetSheet = AssetDatabase.LoadAssetAtPath<UIAnimScreen>($"{Prefabs}prefab_unit_shop_item_set_sheet.prefab");
        page.tabs = new[] { "RegularButton", "SpecialButton", "SaleButton" }.Select(n => Find(root, "ItemList/TabGroup/" + n).GetComponent<Button>()).ToArray();
        page.back = Find(root, "BackButton").GetComponent<Button>();
    }

    // Item set sheet: slides by one sheet width in 0.2 s OutQuart; a higher tab comes from the right, a lower one from the left.
    static void ItemSetSheet(GameObject root)
    {
        var screen = Screen(root);
        UIAnimState Slide(string name, float from, float to)
        {
            var track = Clip(screen.Rect, UIAnimProperty.AnchoredPosition, new Vector4(from, 0), new Vector4(to, 0), 0f, 0.2f, UIAnimEase.OutQuart);
            track.sizeRelative = true;
            return State(name, track);
        }
        States(root, Slide("Enter", 1, 0), Slide("Exit", 0, -1), Slide("EnterBackward", -1, 0), Slide("ExitBackward", 0, 1));
        var set = Get<UsnShopItemSet>(root);
        set.slots = Enumerable.Range(0, 8).Select(i => Find(root, i == 0 ? "prefab_shop_item" : $"prefab_shop_item ({i})").GetComponent<UsnShopItem>()).ToArray();
        set.thumbnails = new[] { "003", "004", "005" }
            .Select(u => AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}Art/Illustration/UnitThumbnail/tex_unit_thumb_{u}_1.png")).ToArray();
    }

    static void ShopItem(GameObject root)
    {
        var item = Get<UsnShopItem>(root);
        item.locked = Find(root, "Locked").gameObject;
        item.unlocked = Find(root, "Unlocked").gameObject;
        item.thumbnail = Find(root, "Unlocked/prefab_unit_thumbnail").GetComponent<Image>();
        item.cost = Find(root, "Unlocked/Cost/Text (TMP)").GetComponent<TMP_Text>();
        item.buy = Find(root, "Unlocked/BuyButton").GetComponent<Button>();
        item.buyLabel = Find(root, "Unlocked/BuyButton/Text (TMP) (1)").GetComponent<TMP_Text>();
    }

    // ---------- scene: three Screen Space - Camera canvases, like the original DemoEntryPoint ----------

    static string BuildScene()
    {
        var path = Root + "Scenes/ScreenNavigatorDemo.unity";
        Directory.CreateDirectory(Root + "Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.transform.position = new Vector3(0, 0, -10);

        RectTransform Canvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;   // the perspective makes Home's menu Y rotation visible
            canvas.worldCamera = camera;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            return (RectTransform)go.transform;
        }
        RectTransform Fill(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            return rt;
        }

        var pages = Fill("MainPageContainer", Canvas("MainCanvas", 0)).gameObject.AddComponent<UIAnimPageContainer>();
        var modals = Fill("MainModalContainer", Canvas("ModalCanvas", 10)).gameObject.AddComponent<UIAnimModalContainer>();
        var overlay = Fill("ConnectingView", Canvas("OverlayCanvas", 20));
        var dim = overlay.gameObject.AddComponent<Image>();
        dim.color = new Color(0, 0, 0, 0.5f);
        var connecting = overlay.gameObject.AddComponent<CanvasGroup>();
        connecting.alpha = 0f;
        connecting.blocksRaycasts = false;
        var label = Fill("Text", overlay).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = "Connecting...";
        label.fontSize = 64;
        label.alignment = TextAlignmentOptions.Center;

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var demo = new GameObject("UsnDemo").AddComponent<UsnDemo>();
        demo.pages = pages;
        demo.modals = modals;
        demo.connecting = connecting;
        UIAnimScreen Load(string name) => AssetDatabase.LoadAssetAtPath<UIAnimScreen>($"{Prefabs}{name}.prefab");
        demo.topPage = Load("prefab_top_page");
        demo.loadingPage = Load("prefab_loading_page");
        demo.homePage = Load("prefab_home_page");
        demo.shopPage = Load("prefab_unit_shop_page");

        EditorSceneManager.SaveScene(scene, path);
        return path;
    }

    static void AddToBuildSettings(string scene)
    {
        var list = EditorBuildSettings.scenes.ToList();
        if (list.All(e => e.path != scene)) list.Add(new EditorBuildSettingsScene(scene, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
