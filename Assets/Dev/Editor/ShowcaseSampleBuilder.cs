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
/// Dev-only (not in the package): builds the Showcase sample (Assets/UIAnimation/Samples/Showcase) — the same screens
/// in three themes. Layout and art differ a little per theme; the motion differs a lot, and all of it is UIAnimation
/// states written here, so each can be opened and edited in the UI Animation window afterwards.
/// </summary>
static class ShowcaseSampleBuilder
{
    const string Root = "Assets/UIAnimation/Samples/Showcase/";
    static TMP_FontAsset _font;

    sealed class Style
    {
        public string name;
        public Sprite bg, panel, button, bar;
        public Color title, panelText, buttonText, barText, accent, price;
        public bool Casual => name == "Casual";
        public bool SciFi => name == "SciFi";
        public string Prefabs => $"{Root}Prefabs/{name}/";
    }

    static readonly (string name, string icon, int price, string text)[] Items =
    {
        ("Sword", "sword", 300, "A sharp blade. Attack +12."),
        ("Shield", "shield", 250, "Blocks the first hit of every fight."),
        ("Potion", "potion", 50, "Restores 50 HP."),
        ("Gem", "gem", 120, "Premium currency, shiny."),
        ("Scroll", "scroll", 80, "Teaches a random skill."),
        ("Coin Pack", "coin", 99, "1,000 coins."),
    };

    [MenuItem("Tools/UIAnimation Dev/Rebuild Showcase Sample")]
    public static void Build()
    {
        ImportArt();
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "Font/PassionOne-Regular SDF.asset");
        var styles = new[]
        {
            Make("Casual", new Color32(40, 52, 96, 255), new Color32(60, 64, 100, 255), new Color32(110, 60, 0, 255), new Color32(70, 80, 120, 255), new Color32(255, 110, 90, 255)),
            Make("SciFi", new Color32(140, 240, 255, 255), new Color32(180, 240, 255, 255), new Color32(140, 240, 255, 255), new Color32(140, 240, 255, 255), new Color32(60, 230, 255, 255)),
            Make("Fantasy", new Color32(255, 230, 170, 255), new Color32(80, 50, 25, 255), new Color32(255, 230, 170, 255), new Color32(255, 230, 170, 255), new Color32(255, 200, 90, 255)),
        };
        var themes = styles.Select(st =>
        {
            Directory.CreateDirectory(st.Prefabs);
            var sheets = new[] { HomeSheet(st), ShopSheet(st), ThemeSheet(st, styles) };
            return new ShowcaseDemo.Theme
            {
                name = st.name,
                lobby = Lobby(st, sheets),
                itemDetail = ItemDetail(st),
                reward = Reward(st),
                levelUp = LevelUp(st),
            };
        }).ToArray();
        var scene = BuildScene(themes);
        var list = EditorBuildSettings.scenes.ToList();
        if (list.All(e => e.path != scene)) list.Add(new EditorBuildSettingsScene(scene, true));
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log($"[ShowcaseSampleBuilder] Built {Root}Prefabs, {scene}");
    }

    static Style Make(string name, Color title, Color panelText, Color buttonText, Color barText, Color accent)
    {
        Sprite S(string file) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}Art/{name}/{file}.png");
        return new Style
        {
            name = name, bg = S("bg"), panel = S("panel"), button = S("button"), bar = S("bar"),
            title = title, panelText = panelText, buttonText = buttonText, barText = barText, accent = accent,
            price = name == "Fantasy" ? new Color32(170, 80, 20, 255) : accent,   // gold is too light on parchment
        };
    }

    static Sprite Icon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}Art/Icons/{name}.png");

    static void ImportArt()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "Art" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var sliced = path.EndsWith("panel.png") || path.EndsWith("button.png") || path.EndsWith("bar.png");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spriteBorder = sliced ? new Vector4(44, 44, 44, 44) : Vector4.zero;
            importer.SaveAndReimport();
        }
    }

    // ---------- layout helpers ----------

    static readonly Vector2 Half = new(0.5f, 0.5f);

    static RectTransform Node(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2? pivot = null)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        if (parent != null) rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot ?? Half;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    static RectTransform Center(string name, Transform parent, float x, float y, float w, float h) => Node(name, parent, Half, Half, new Vector2(x, y), new Vector2(w, h));
    static RectTransform Fill(string name, Transform parent) => Node(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

    static Image Img(RectTransform rt, Sprite sprite, Color? color = null, bool raycast = false)
    {
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color ?? Color.white;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.raycastTarget = raycast;
        return image;
    }

    static TMP_Text Text(RectTransform rt, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = _font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    static TMP_Text Label(Transform parent, string name, string text, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center) =>
        Text(Center(name, parent, x, y, w, h), text, size, color, align);

    // A button that dips when pressed (states "Press" / "Release", played by a UIAnimTrigger).
    static Button Btn(Style st, Transform parent, string name, string label, float x, float y, float w, float h, Sprite sprite = null)
    {
        var rt = Center(name, parent, x, y, w, h);
        Img(rt, sprite != null ? sprite : st.button, raycast: true);
        var button = rt.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        if (label != null) Text(Fill("Label", rt), label, h * 0.42f, sprite != null ? st.panelText : st.buttonText);
        Pressable(st, rt);
        return button;
    }

    static void Pressable(Style st, RectTransform rt)
    {
        var anim = rt.gameObject.AddComponent<UIAnimation>();
        var (down, downTime, downEase, upTime, upEase) = st.name switch
        {
            "Casual" => (0.9f, 0.08f, UIAnimEase.OutQuad, 0.5f, UIAnimEase.OutElastic),
            "SciFi" => (0.96f, 0.04f, UIAnimEase.Linear, 0.08f, UIAnimEase.Linear),
            _ => (0.96f, 0.2f, UIAnimEase.OutSine, 0.35f, UIAnimEase.OutSine),
        };
        anim.states.Add(State("Press", To(rt, UIAnimProperty.Scale, S(down), downTime, downEase)));
        anim.states.Add(State("Release", To(rt, UIAnimProperty.Scale, S(1), upTime, upEase)));
        var trigger = rt.gameObject.AddComponent<UIAnimTrigger>();
        trigger.bindings.Add(new UIAnimBinding { evt = UIAnimEvent.PointerDown, state = "Press" });
        trigger.bindings.Add(new UIAnimBinding { evt = UIAnimEvent.PointerUp, state = "Release" });
    }

    static (GameObject go, UIAnimScreen screen) NewScreen(string name)
    {
        var rt = Node(name, null, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        rt.gameObject.AddComponent<CanvasGroup>();
        rt.gameObject.AddComponent<UIAnimation>();
        return (rt.gameObject, rt.gameObject.AddComponent<UIAnimScreen>());
    }

    static T Save<T>(GameObject go, Style st) where T : Component
    {
        var path = $"{st.Prefabs}{go.name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    // ---------- motion helpers ----------

    static Vector4 V(float x, float y) => new(x, y, 0, 0);
    static Vector4 A(float a) => new(a, 0, 0, 0);
    static Vector4 S(float s) => new(s, s, 1, 0);
    static Vector4 S(float x, float y) => new(x, y, 1, 0);
    static Vector4 Z(float degrees) => new(0, 0, degrees, 0);

    static UIAnimKey K(float time, Vector4 value, UIAnimEase ease = UIAnimEase.Linear) => new() { time = time, value = value, ease = ease };

    static UIAnimKey Vibrate(UIAnimCurveType type, float time, Vector4 strength, float frequency, float damping) =>
        new() { time = time, value = strength, curveType = type, ease = UIAnimEase.Linear, frequency = frequency, damping = damping };

    // Keys written in the track (start = KeyValue); a first key after 0 holds until then (a delay).
    static UIAnimTrack Keys(Component target, UIAnimProperty property, params UIAnimKey[] keys)
    {
        var track = new UIAnimTrack { target = target, property = property, start = UIAnimStart.KeyValue };
        track.keys.AddRange(keys);
        UIAnimationEditorWindow.EnsureStartKey(track);
        return track;
    }

    static UIAnimTrack Go(Component target, UIAnimProperty property, Vector4 from, Vector4 to, float start, float duration, UIAnimEase ease) =>
        Keys(target, property, K(start, from), K(start + duration, to, ease));

    // From wherever it is now (an interrupted move continues from there).
    static UIAnimTrack To(Component target, UIAnimProperty property, Vector4 to, float duration, UIAnimEase ease)
    {
        var track = new UIAnimTrack { target = target, property = property, start = UIAnimStart.Current };
        track.keys.Add(K(0, to));
        track.keys.Add(K(duration, to, ease));
        return track;
    }

    static UIAnimTrack Chars(UIAnimTrack track, float stagger)
    {
        track.stagger = stagger;
        return track;
    }

    static UIAnimTrack Scrambled(TMP_Text text, float start, float duration, UIAnimScramble scramble)
    {
        var track = Go(text, UIAnimProperty.TextReveal, A(0), A(1), start, duration, UIAnimEase.Linear);
        track.scramble = scramble;
        return track;
    }

    // SciFi's "power on": alpha flickers before it settles at 1.
    static UIAnimTrack Flicker(Component target, UIAnimProperty property, float start, float to = 1f) =>
        Keys(target, property, K(start, A(0)), K(start + 0.04f, A(to)), K(start + 0.08f, A(to * 0.25f)), K(start + 0.12f, A(to)), K(start + 0.16f, A(to * 0.5f)), K(start + 0.2f, A(to)));

    static UIAnimState State(string name, params UIAnimTrack[] tracks) => new() { name = name, tracks = tracks.Where(t => t != null).ToList() };
    static void States(GameObject go, params UIAnimState[] states) => go.GetComponent<UIAnimation>().states.AddRange(states);

    // Sheets: Casual overshoots in from the side, SciFi snaps in from a little to the side while flickering on,
    // Fantasy cross-fades with a slight zoom.
    static void SheetStates(GameObject go, UIAnimScreen screen, Style st)
    {
        var rt = screen.Rect;
        var group = screen.Group;
        UIAnimTrack Slide(float from, float to, float duration, UIAnimEase ease)
        {
            var t = Go(rt, UIAnimProperty.AnchoredPosition, V(from, 0), V(to, 0), 0, duration, ease);
            t.sizeRelative = true;
            return t;
        }
        UIAnimState[] Pair(string suffix, float side)
        {
            if (st.Casual)
                return new[]
                {
                    State("Enter" + suffix, Slide(side, 0, 0.45f, UIAnimEase.OutBack)),
                    State("Exit" + suffix, Slide(0, -side, 0.3f, UIAnimEase.InOutQuad)),
                };
            if (st.SciFi)
                return new[]
                {
                    State("Enter" + suffix, Slide(side * 0.25f, 0, 0.2f, UIAnimEase.OutExpo), Flicker(group, UIAnimProperty.CanvasGroupAlpha, 0)),
                    State("Exit" + suffix, Slide(0, -side * 0.25f, 0.12f, UIAnimEase.InQuad), Go(group, UIAnimProperty.CanvasGroupAlpha, A(1), A(0), 0, 0.12f, UIAnimEase.Linear)),
                };
            return new[]
            {
                State("Enter" + suffix, Go(group, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), 0.1f, 0.5f, UIAnimEase.InOutSine), Go(rt, UIAnimProperty.Scale, S(0.96f), S(1), 0.1f, 0.6f, UIAnimEase.OutSine)),
                State("Exit" + suffix, Go(group, UIAnimProperty.CanvasGroupAlpha, A(1), A(0), 0, 0.4f, UIAnimEase.InOutSine), Go(rt, UIAnimProperty.Scale, S(1), S(1.02f), 0, 0.4f, UIAnimEase.OutSine)),
            };
        }
        States(go, Pair("", 1).Concat(Pair("Backward", -1)).ToArray());
    }

    // ---------- sheets ----------

    static UIAnimScreen HomeSheet(Style st)
    {
        var (go, screen) = NewScreen("HomeSheet");
        var home = go.AddComponent<ShowcaseHomeSheet>();
        Button Card(string name, string label, string icon, float x)
        {
            var b = Btn(st, go.transform, name, null, x, 0, 400, 420, st.panel);
            Img(Center("Icon", b.transform, 0, 50, 200, 200), Icon(icon));
            Label(b.transform, "Label", label, 0, -140, 360, 80, 56, st.panelText);
            return b;
        }
        home.reward = Card("RewardButton", "REWARD", "chest", -460);
        home.levelUp = Card("LevelUpButton", "LEVEL UP", "star", 0);
        home.toast = Card("ToastButton", "TOAST", "scroll", 460);
        SheetStates(go, screen, st);
        return Save<UIAnimScreen>(go, st);
    }

    static UIAnimScreen ShopSheet(Style st)
    {
        var (go, screen) = NewScreen("ShopSheet");
        for (var i = 0; i < Items.Length; i++)
        {
            var (name, icon, price, text) = Items[i];
            var b = Btn(st, go.transform, "Card" + i, null, -420 + i % 3 * 420, i < 3 ? 190 : -190, 360, 340, st.panel);
            var card = b.gameObject.AddComponent<ShowcaseShopCard>();
            card.icon = Img(Center("Icon", b.transform, 0, 40, 180, 180), Icon(icon));
            card.itemName = Label(b.transform, "Name", name, 0, -85, 330, 60, 46, st.panelText);
            card.price = Label(b.transform, "Price", price.ToString(), 0, -135, 330, 50, 38, st.price);
            card.description = text;
        }
        SheetStates(go, screen, st);
        return Save<UIAnimScreen>(go, st);
    }

    static UIAnimScreen ThemeSheet(Style st, Style[] all)
    {
        var (go, screen) = NewScreen("ThemeSheet");
        var sheet = go.AddComponent<ShowcaseThemeSheet>();
        sheet.themes = all.Select((other, i) =>
        {
            var b = Btn(st, go.transform, other.name + "Button", null, -460 + i * 460, 0, 400, 360, st.panel);
            Img(Center("Preview", b.transform, 0, 40, 340, 200), other.bg);
            Label(b.transform, "Label", other.name.ToUpperInvariant(), 0, -125, 360, 70, 52, st.panelText);
            return b;
        }).ToArray();
        SheetStates(go, screen, st);
        return Save<UIAnimScreen>(go, st);
    }

    // ---------- lobby page ----------

    static UIAnimScreen Lobby(Style st, UIAnimScreen[] sheetPrefabs)
    {
        var (go, screen) = NewScreen("Lobby");
        Img(screen.Rect, st.bg);
        var group = screen.Group;
        var title = Label(go.transform, "Title", "UI ANIMATION", 0, 0, 1400, 150, 120, st.title);
        ((RectTransform)title.transform).anchorMin = ((RectTransform)title.transform).anchorMax = new Vector2(0.5f, 1);
        ((RectTransform)title.transform).anchoredPosition = new Vector2(0, -120);
        var subtitle = Label(go.transform, "Theme", st.name.ToUpperInvariant() + " THEME", 0, 0, 800, 60, 44, st.title);
        ((RectTransform)subtitle.transform).anchorMin = ((RectTransform)subtitle.transform).anchorMax = new Vector2(0.5f, 1);
        ((RectTransform)subtitle.transform).anchoredPosition = new Vector2(0, -205);

        var area = Fill("SheetArea", go.transform);
        area.offsetMin = new Vector2(0, 160);
        area.offsetMax = new Vector2(0, -250);
        area.gameObject.AddComponent<RectMask2D>();
        var sheets = area.gameObject.AddComponent<UIAnimSheetContainer>();

        var bar = Node("TabBar", go.transform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 150), new Vector2(0.5f, 0));
        Img(bar, st.bar);
        var tabs = new[] { "HOME", "SHOP", "THEME" }.Select((label, i) =>
        {
            var tab = Node(label + "Tab", bar, new Vector2(i / 3f, 0), new Vector2((i + 1) / 3f, 1), Vector2.zero, Vector2.zero);
            Img(tab, null, Color.clear, raycast: true);
            Text(Fill("Label", tab), label, 52, st.barText);
            var b = tab.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            return b;
        }).ToArray();
        var indicator = Node("Indicator", bar, new Vector2(1 / 6f, 0), new Vector2(1 / 6f, 0), new Vector2(0, 16), new Vector2(240, 10), new Vector2(0.5f, 0));
        Img(indicator, null, st.accent);

        // Toast: hidden at its "Show" state's start; shown by playing it, hidden by playing it reversed.
        var toastRt = Node("Toast", go.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(760, 110), new Vector2(0.5f, 1));
        Img(toastRt, st.panel);
        var toastGroup = toastRt.gameObject.AddComponent<CanvasGroup>();
        toastGroup.blocksRaycasts = false;
        var toastText = Text(Fill("Text", toastRt), "", 50, st.panelText);
        var toast = toastRt.gameObject.AddComponent<UIAnimation>();
        var rest = V(0, -40);
        var (toastFrom, toastShow) = st.name switch
        {
            "Casual" => (V(0, 200), State("Show", Go(toastRt, UIAnimProperty.AnchoredPosition, V(0, 200), rest, 0, 0.5f, UIAnimEase.OutBack), Go(toastGroup, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), 0, 0.15f, UIAnimEase.Linear))),
            "SciFi" => (V(900, -40), State("Show", Go(toastRt, UIAnimProperty.AnchoredPosition, V(900, -40), rest, 0, 0.25f, UIAnimEase.OutExpo), Flicker(toastGroup, UIAnimProperty.CanvasGroupAlpha, 0))),
            _ => (V(0, 0), State("Show", Go(toastRt, UIAnimProperty.AnchoredPosition, V(0, 0), rest, 0, 0.7f, UIAnimEase.OutSine), Go(toastGroup, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), 0, 0.7f, UIAnimEase.InOutSine))),
        };
        toast.states.Add(toastShow);
        toastRt.anchoredPosition = toastFrom;
        toastGroup.alpha = 0;

        var tabMoves = Enumerable.Range(0, 3).Select(i =>
        {
            var x = new Vector4((i + 0.5f) / 3f, 0);
            var (d, e) = st.name switch { "Casual" => (0.45f, UIAnimEase.OutBack), "SciFi" => (0.15f, UIAnimEase.OutExpo), _ => (0.6f, UIAnimEase.InOutSine) };
            return State("Tab" + i, To(indicator, UIAnimProperty.AnchorMin, x, d, e), To(indicator, UIAnimProperty.AnchorMax, x, d, e));
        });

        // The lobby being replaced stays visible under the new one (it is hidden when both are done), so its exit
        // never fades out: the new lobby covers it instead of the screen going dark in between.
        UIAnimState enter, exit;
        if (st.Casual)
        {
            enter = State("PushEnter",
                Go(group, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), 0, 0.25f, UIAnimEase.OutQuad),
                Go(screen.Rect, UIAnimProperty.Scale, S(1.06f), S(1), 0, 0.5f, UIAnimEase.OutBack),
                Chars(Go(title, UIAnimProperty.CharOffset, V(0, 160), V(0, 0), 0.15f, 0.55f, UIAnimEase.OutBounce), 0.04f),
                Chars(Go(title, UIAnimProperty.CharAlpha, A(0), A(1), 0.15f, 0.12f, UIAnimEase.Linear), 0.04f),
                Go(bar, UIAnimProperty.AnchoredPosition, V(0, -170), V(0, 0), 0.25f, 0.5f, UIAnimEase.OutBack));
            exit = State("PushExit", Go(screen.Rect, UIAnimProperty.Scale, S(1), S(0.94f), 0, 0.25f, UIAnimEase.InQuad));
        }
        else if (st.SciFi)
        {
            enter = State("PushEnter",
                Flicker(group, UIAnimProperty.CanvasGroupAlpha, 0),
                Scrambled(title, 0.1f, 0.5f, UIAnimScramble.Uppercase),
                Scrambled(subtitle, 0.2f, 0.4f, UIAnimScramble.Uppercase),
                Go(bar, UIAnimProperty.AnchoredPosition, V(0, -170), V(0, 0), 0.15f, 0.25f, UIAnimEase.OutExpo));
            exit = State("PushExit", Keys(group, UIAnimProperty.CanvasGroupAlpha, K(0, A(1)), K(0.04f, A(0.3f)), K(0.08f, A(1)), K(0.12f, A(0.5f)), K(0.16f, A(1))));
        }
        else
        {
            enter = State("PushEnter",
                Go(group, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), 0, 0.9f, UIAnimEase.InOutSine),
                Chars(Go(title, UIAnimProperty.CharAlpha, A(0), A(1), 0.3f, 0.6f, UIAnimEase.InOutSine), 0.07f),
                Chars(Go(title, UIAnimProperty.CharOffset, V(0, -30), V(0, 0), 0.3f, 0.8f, UIAnimEase.OutSine), 0.07f),
                Go(subtitle, UIAnimProperty.GraphicAlpha, A(0), A(1), 0.9f, 0.8f, UIAnimEase.InOutSine),
                Go(bar, UIAnimProperty.AnchoredPosition, V(0, -170), V(0, 0), 0.4f, 0.8f, UIAnimEase.OutSine));
            exit = State("PushExit", Go(screen.Rect, UIAnimProperty.Scale, S(1), S(1.03f), 0, 0.9f, UIAnimEase.InOutSine));
        }
        States(go, new[] { enter, exit }.Concat(tabMoves).ToArray());

        var lobby = go.AddComponent<ShowcaseLobby>();
        lobby.tabs = tabs;
        lobby.sheets = sheets;
        lobby.sheetPrefabs = sheetPrefabs;
        lobby.toast = toast;
        lobby.toastText = toastText;
        return Save<UIAnimScreen>(go, st);
    }

    // ---------- modals ----------

    // Shop item detail: the icon (outside the panel so it can travel) grows out of the tapped card's icon.
    static UIAnimScreen ItemDetail(Style st)
    {
        var (go, screen) = NewScreen("ItemDetail");
        var panel = Center("Panel", go.transform, 0, 0, 1100, 620);
        Img(panel, st.panel);
        var group = panel.gameObject.AddComponent<CanvasGroup>();
        var detail = go.AddComponent<ShowcaseItemDetail>();
        detail.itemName = Label(panel, "Name", "", 180, 190, 620, 90, 76, st.panelText, TextAlignmentOptions.Left);
        detail.description = Label(panel, "Description", "", 180, 40, 620, 180, 40, st.panelText, TextAlignmentOptions.TopLeft);
        detail.price = Label(panel, "Price", "", 180, -120, 620, 70, 60, st.price, TextAlignmentOptions.Left);
        detail.buy = Btn(st, panel, "BuyButton", "BUY", 330, -220, 300, 110);
        detail.close = Btn(st, panel, "CloseButton", "X", 490, 250, 90, 90);
        var icon = Center("Icon", go.transform, -300, 20, 380, 380);
        detail.icon = Img(icon, null);
        var (pos, size) = ((Vector4)icon.anchoredPosition, (Vector4)icon.sizeDelta);

        // The card's end (first key of Enter, last of Exit) is a placeholder: ShowcaseItemDetail fills it in before playing.
        UIAnimTrack[] Travel(float start, float duration, UIAnimEase ease) => new[]
        {
            Go(icon, UIAnimProperty.AnchoredPosition, pos, pos, start, duration, ease),
            Go(icon, UIAnimProperty.SizeDelta, size, size, start, duration, ease),
        };
        UIAnimState enterState, exitState;
        if (st.Casual)
        {
            enterState = State("Enter", Travel(0, 0.5f, UIAnimEase.OutBack).Concat(new[]
            {
                Go(group, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), 0.1f, 0.2f, UIAnimEase.OutQuad),
                Go(panel, UIAnimProperty.Scale, S(0.7f), S(1), 0.1f, 0.45f, UIAnimEase.OutBack),
            }).ToArray());
            exitState = State("Exit", Travel(0.05f, 0.35f, UIAnimEase.OutCubic).Concat(new[]
            {
                Go(group, UIAnimProperty.CanvasGroupAlpha, A(1), A(0), 0, 0.15f, UIAnimEase.Linear),
                Go(panel, UIAnimProperty.Scale, S(1), S(0.8f), 0, 0.2f, UIAnimEase.InQuad),
            }).ToArray());
        }
        else if (st.SciFi)
        {
            enterState = State("Enter", Travel(0, 0.25f, UIAnimEase.OutExpo).Concat(new[]
            {
                Go(panel, UIAnimProperty.Scale, S(1, 0.02f), S(1), 0.1f, 0.2f, UIAnimEase.OutExpo),
                Flicker(group, UIAnimProperty.CanvasGroupAlpha, 0.1f),
            }).ToArray());
            exitState = State("Exit", Travel(0.05f, 0.2f, UIAnimEase.OutExpo).Concat(new[]
            {
                Go(panel, UIAnimProperty.Scale, S(1), S(1, 0.02f), 0, 0.12f, UIAnimEase.InQuad),
                Go(group, UIAnimProperty.CanvasGroupAlpha, A(1), A(0), 0, 0.12f, UIAnimEase.Linear),
            }).ToArray());
        }
        else
        {
            enterState = State("Enter", Travel(0, 0.7f, UIAnimEase.InOutSine).Concat(new[]
            {
                Go(group, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), 0.2f, 0.6f, UIAnimEase.InOutSine),
                Go(panel, UIAnimProperty.AnchoredPosition, V(0, -30), V(0, 0), 0.2f, 0.7f, UIAnimEase.OutSine),
            }).ToArray());
            exitState = State("Exit", Travel(0.1f, 0.6f, UIAnimEase.InOutSine).Concat(new[]
            {
                Go(group, UIAnimProperty.CanvasGroupAlpha, A(1), A(0), 0, 0.4f, UIAnimEase.InOutSine),
            }).ToArray());
        }
        States(go, enterState, exitState);
        return Save<UIAnimScreen>(go, st);
    }

    static UIAnimScreen Reward(Style st)
    {
        var (go, screen) = NewScreen("Reward");
        var panel = Center("Panel", go.transform, 0, 0, 1000, 760);
        Img(panel, st.panel);
        var group = panel.gameObject.AddComponent<CanvasGroup>();
        Label(panel, "Title", "REWARD!", 0, 300, 800, 110, 96, st.panelText);
        var rays = Img(Center("Rays", panel, 0, 90, 420, 420), Icon("rays"), st.accent);
        var chest = Center("Chest", panel, 0, 90, 260, 260);
        Img(chest, Icon("chest"));
        var slots = new[] { ("coin", "x500"), ("gem", "x20"), ("potion", "x3") }.Select((r, i) =>
        {
            var slot = Center("Slot" + i, panel, -260 + i * 260, -150, 220, 230);
            Img(slot, st.bar);
            var slotGroup = slot.gameObject.AddComponent<CanvasGroup>();
            Img(Center("Icon", slot, 0, 25, 120, 120), Icon(r.Item1));
            var amount = Label(slot, "Amount", r.Item2, 0, -75, 200, 60, 48, st.barText);
            return (slot, slotGroup, amount);
        }).ToArray();
        var claim = Btn(st, panel, "ClaimButton", "CLAIM", 0, -310, 320, 110);
        var claimGroup = claim.gameObject.AddComponent<CanvasGroup>();
        go.AddComponent<ShowcasePopup>().close = claim;

        var tracks = new System.Collections.Generic.List<UIAnimTrack>();
        UIAnimState exit;
        if (st.Casual)
        {
            // Pops in, the chest rattles, bursts open with rays, the items bounce out one by one.
            tracks.Add(Go(panel, UIAnimProperty.Scale, S(0.3f), S(1), 0, 0.4f, UIAnimEase.OutBack));
            tracks.Add(Go(group, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), 0, 0.15f, UIAnimEase.Linear));
            tracks.Add(Keys(chest, UIAnimProperty.Rotation, K(0.4f, Z(0)), Vibrate(UIAnimCurveType.Shake, 1.0f, Z(12), 14, 0.2f)));
            tracks.Add(Keys(chest, UIAnimProperty.Scale, K(1.0f, S(1)), Vibrate(UIAnimCurveType.Punch, 1.4f, S(0.35f, 0.35f), 6, 0.5f)));
            tracks.Add(Go(rays, UIAnimProperty.GraphicAlpha, A(0), A(0.6f), 1.0f, 0.2f, UIAnimEase.OutQuad));
            tracks.Add(Go(rays.rectTransform, UIAnimProperty.Rotation, Z(0), Z(-90), 1.0f, 3f, UIAnimEase.Linear));
            for (var i = 0; i < slots.Length; i++)
            {
                var t = 1.1f + i * 0.12f;
                tracks.Add(Go(slots[i].slotGroup, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), t, 0.1f, UIAnimEase.Linear));
                tracks.Add(Go(slots[i].slot, UIAnimProperty.Scale, S(0), S(1), t, 0.35f, UIAnimEase.OutBack));
                tracks.Add(Chars(Go(slots[i].amount, UIAnimProperty.CharScale, S(0), S(1), t + 0.15f, 0.3f, UIAnimEase.OutBack), 0.04f));
            }
            tracks.Add(Go(claimGroup, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), 1.6f, 0.1f, UIAnimEase.Linear));
            tracks.Add(Go(claim.transform, UIAnimProperty.Scale, S(0.5f), S(1), 1.6f, 0.35f, UIAnimEase.OutBack));
            exit = State("Exit", Go(group, UIAnimProperty.CanvasGroupAlpha, A(1), A(0), 0, 0.2f, UIAnimEase.InQuad), Go(panel, UIAnimProperty.Scale, S(1), S(0.9f), 0, 0.2f, UIAnimEase.InQuad));
        }
        else if (st.SciFi)
        {
            // Scans open, the chest flickers on and pulses, items snap in from the right with scrambled counts.
            tracks.Add(Go(panel, UIAnimProperty.Scale, S(1, 0.02f), S(1), 0, 0.25f, UIAnimEase.OutExpo));
            tracks.Add(Flicker(group, UIAnimProperty.CanvasGroupAlpha, 0));
            tracks.Add(Keys(chest.GetComponent<Image>(), UIAnimProperty.GraphicAlpha, K(0.25f, A(0)), K(0.3f, A(1)), K(0.35f, A(0.2f)), K(0.4f, A(1)), K(0.45f, A(0.5f)), K(0.5f, A(1))));
            tracks.Add(Keys(chest, UIAnimProperty.Scale, K(0.55f, S(1)), K(0.6f, S(1.15f)), K(0.75f, S(1), UIAnimEase.OutExpo)));
            tracks.Add(Flicker(rays, UIAnimProperty.GraphicAlpha, 0.55f, 0.5f));
            tracks.Add(Go(rays.rectTransform, UIAnimProperty.Rotation, Z(0), Z(-180), 0.55f, 3f, UIAnimEase.Linear));
            for (var i = 0; i < slots.Length; i++)
            {
                var t = 0.6f + i * 0.07f;
                var x = slots[i].slot.anchoredPosition.x;
                tracks.Add(Go(slots[i].slotGroup, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), t, 0.05f, UIAnimEase.Linear));
                tracks.Add(Go(slots[i].slot, UIAnimProperty.AnchoredPosition, V(x + 160, -150), V(x, -150), t, 0.2f, UIAnimEase.OutExpo));
                tracks.Add(Scrambled(slots[i].amount, t + 0.05f, 0.4f, UIAnimScramble.Numerals));
            }
            tracks.Add(Go(claim.transform, UIAnimProperty.Scale, S(1, 0), S(1), 0.9f, 0.15f, UIAnimEase.OutExpo));
            tracks.Add(Go(claimGroup, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), 0.9f, 0.05f, UIAnimEase.Linear));
            exit = State("Exit", Go(panel, UIAnimProperty.Scale, S(1), S(1, 0.02f), 0, 0.12f, UIAnimEase.InQuad), Go(group, UIAnimProperty.CanvasGroupAlpha, A(1), A(0), 0, 0.12f, UIAnimEase.Linear));
        }
        else
        {
            // Rises softly, the chest floats up, light slowly turns behind it, the items fade in one after another.
            tracks.Add(Go(group, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), 0, 0.7f, UIAnimEase.InOutSine));
            tracks.Add(Go(panel, UIAnimProperty.AnchoredPosition, V(0, -40), V(0, 0), 0, 0.9f, UIAnimEase.OutSine));
            tracks.Add(Go(chest, UIAnimProperty.AnchoredPosition, V(0, 40), V(0, 90), 0.4f, 0.9f, UIAnimEase.OutSine));
            tracks.Add(Go(chest.GetComponent<Image>(), UIAnimProperty.GraphicAlpha, A(0), A(1), 0.4f, 0.6f, UIAnimEase.InOutSine));
            tracks.Add(Go(rays, UIAnimProperty.GraphicAlpha, A(0), A(0.55f), 1.0f, 0.8f, UIAnimEase.InOutSine));
            tracks.Add(Go(rays.rectTransform, UIAnimProperty.Rotation, Z(0), Z(-40), 1.0f, 4f, UIAnimEase.Linear));
            for (var i = 0; i < slots.Length; i++)
            {
                var t = 1.2f + i * 0.25f;
                var x = slots[i].slot.anchoredPosition.x;
                tracks.Add(Go(slots[i].slotGroup, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), t, 0.6f, UIAnimEase.InOutSine));
                tracks.Add(Go(slots[i].slot, UIAnimProperty.AnchoredPosition, V(x, -180), V(x, -150), t, 0.7f, UIAnimEase.OutSine));
            }
            tracks.Add(Go(claimGroup, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), 2.0f, 0.5f, UIAnimEase.InOutSine));
            exit = State("Exit", Go(group, UIAnimProperty.CanvasGroupAlpha, A(1), A(0), 0, 0.45f, UIAnimEase.InOutSine));
        }
        States(go, State("Enter", tracks.ToArray()), exit);
        return Save<UIAnimScreen>(go, st);
    }

    static UIAnimScreen LevelUp(Style st)
    {
        var (go, screen) = NewScreen("LevelUp");
        var group = screen.Group;
        var rays = Img(Center("Rays", go.transform, 0, 60, 760, 760), Icon("rays"), st.accent);
        var banner = Center("Banner", go.transform, 0, 60, 1300, 220);
        Img(banner, st.bar);
        var bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        var title = Text(Fill("Title", banner), "LEVEL UP", 130, st.barText);
        var stars = new[] { (-170f, 250f, 110f), (0f, 290f, 150f), (170f, 250f, 110f) }
            .Select((s, i) => Img(Center("Star" + i, go.transform, s.Item1, s.Item2, s.Item3, s.Item3), Icon("star"))).ToArray();
        var level = Label(go.transform, "Level", "Lv. 12", 0, -140, 600, 120, 100, Color.white);
        var ok = Btn(st, go.transform, "OkButton", "OK", 0, -300, 300, 110);
        var okGroup = ok.gameObject.AddComponent<CanvasGroup>();
        go.AddComponent<ShowcasePopup>().close = ok;

        var tracks = new System.Collections.Generic.List<UIAnimTrack>();
        float exitTime;
        if (st.Casual)
        {
            tracks.Add(Go(rays, UIAnimProperty.GraphicAlpha, A(0), A(0.45f), 0, 0.3f, UIAnimEase.OutQuad));
            tracks.Add(Go(rays.rectTransform, UIAnimProperty.Rotation, Z(0), Z(-120), 0, 4f, UIAnimEase.Linear));
            tracks.Add(Go(banner, UIAnimProperty.SizeDelta, V(0, 220), V(1300, 220), 0.1f, 0.45f, UIAnimEase.OutBack));
            tracks.Add(Chars(Go(title, UIAnimProperty.CharOffset, V(0, 220), V(0, 0), 0.35f, 0.5f, UIAnimEase.OutBounce), 0.05f));
            tracks.Add(Chars(Go(title, UIAnimProperty.CharAlpha, A(0), A(1), 0.35f, 0.1f, UIAnimEase.Linear), 0.05f));
            for (var i = 0; i < 3; i++) tracks.Add(Go(stars[i].rectTransform, UIAnimProperty.Scale, S(0), S(1), 0.8f + (i == 1 ? 0.1f : i == 2 ? 0.2f : 0), 0.35f, UIAnimEase.OutBack));
            tracks.Add(Keys(level.transform, UIAnimProperty.Scale, K(1.05f, S(0)), K(1.4f, S(1), UIAnimEase.OutBack), Vibrate(UIAnimCurveType.Punch, 1.8f, S(0.25f, 0.25f), 5, 0.5f)));
            tracks.Add(Go(okGroup, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), 1.3f, 0.2f, UIAnimEase.Linear));
            exitTime = 0.2f;
        }
        else if (st.SciFi)
        {
            tracks.Add(Flicker(rays, UIAnimProperty.GraphicAlpha, 0, 0.4f));
            tracks.Add(Go(rays.rectTransform, UIAnimProperty.Rotation, Z(0), Z(-180), 0, 4f, UIAnimEase.Linear));
            tracks.Add(Go(banner, UIAnimProperty.SizeDelta, V(0, 220), V(1300, 220), 0, 0.25f, UIAnimEase.OutExpo));
            tracks.Add(Scrambled(title, 0.15f, 0.5f, UIAnimScramble.Uppercase));
            for (var i = 0; i < 3; i++)
            {
                var t = 0.5f + i * 0.08f;
                tracks.Add(Keys(stars[i].gameObject.transform, UIAnimProperty.Scale, K(t, S(1.4f)), K(t + 0.1f, S(1), UIAnimEase.OutExpo)));
                tracks.Add(Keys(stars[i], UIAnimProperty.GraphicAlpha, K(0, A(0)), K(t, A(0)), K(t + 0.02f, A(1))));
            }
            tracks.Add(Scrambled(level, 0.6f, 0.4f, UIAnimScramble.Numerals));
            tracks.Add(Flicker(okGroup, UIAnimProperty.CanvasGroupAlpha, 0.9f));
            exitTime = 0.12f;
        }
        else
        {
            tracks.Add(Go(rays, UIAnimProperty.GraphicAlpha, A(0), A(0.4f), 0, 1.2f, UIAnimEase.InOutSine));
            tracks.Add(Go(rays.rectTransform, UIAnimProperty.Rotation, Z(0), Z(-40), 0, 6f, UIAnimEase.Linear));
            tracks.Add(Go(bannerGroup, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), 0.2f, 0.8f, UIAnimEase.InOutSine));
            tracks.Add(Go(banner, UIAnimProperty.SizeDelta, V(600, 220), V(1300, 220), 0.2f, 1.0f, UIAnimEase.OutSine));
            tracks.Add(Chars(Go(title, UIAnimProperty.CharAlpha, A(0), A(1), 0.6f, 0.6f, UIAnimEase.InOutSine), 0.1f));
            tracks.Add(Chars(Go(title, UIAnimProperty.CharScale, S(1.5f), S(1), 0.6f, 0.8f, UIAnimEase.OutSine), 0.1f));
            for (var i = 0; i < 3; i++)
            {
                var t = 1.3f + i * 0.2f;
                var p = stars[i].rectTransform.anchoredPosition;
                tracks.Add(Go(stars[i], UIAnimProperty.GraphicAlpha, A(0), A(1), t, 0.6f, UIAnimEase.InOutSine));
                tracks.Add(Go(stars[i].rectTransform, UIAnimProperty.AnchoredPosition, V(p.x, p.y - 40), V(p.x, p.y), t, 0.8f, UIAnimEase.OutSine));
            }
            tracks.Add(Chars(Go(level, UIAnimProperty.CharAlpha, A(0), A(1), 1.6f, 0.5f, UIAnimEase.InOutSine), 0.08f));
            tracks.Add(Go(okGroup, UIAnimProperty.CanvasGroupAlpha, A(0), A(1), 2.0f, 0.5f, UIAnimEase.InOutSine));
            exitTime = 0.45f;
        }
        States(go, State("Enter", tracks.ToArray()),
            State("Exit", Go(group, UIAnimProperty.CanvasGroupAlpha, A(1), A(0), 0, exitTime, st.SciFi ? UIAnimEase.Linear : UIAnimEase.InOutSine)));
        return Save<UIAnimScreen>(go, st);
    }

    // ---------- scene ----------

    static string BuildScene(ShowcaseDemo.Theme[] themes)
    {
        var path = Root + "Scenes/Showcase.unity";
        Directory.CreateDirectory(Root + "Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;

        RectTransform Canvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            return (RectTransform)go.transform;
        }

        var pages = Fill("Pages", Canvas("PageCanvas", 0)).gameObject.AddComponent<UIAnimPageContainer>();
        var modals = Fill("Modals", Canvas("ModalCanvas", 10)).gameObject.AddComponent<UIAnimModalContainer>();
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var demo = new GameObject("ShowcaseDemo").AddComponent<ShowcaseDemo>();
        demo.pages = pages;
        demo.modals = modals;
        demo.themes = themes;
        EditorSceneManager.SaveScene(scene, path);
        return path;
    }
}
