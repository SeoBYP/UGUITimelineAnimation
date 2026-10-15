using System.Collections.Generic;
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
/// Dev-only (not in the package): builds the RPG sample (Assets/UIAnimation/Samples/RPG) — a title, a lobby and the
/// screens a mobile RPG opens from it, in a fantasy style. Every motion is a UIAnimation state written here, so it can be opened
/// and edited in the UI Animation window afterwards. Art: Tools/rpg_art.py.
/// </summary>
static class RpgSampleBuilder
{
    const string Root = "Assets/UIAnimation/Samples/RPG/";
    const string Prefabs = Root + "Prefabs/";
    const string Items = Prefabs + "Items/";
    static TMP_FontAsset _font;

    static readonly Color Gold = new Color32(255, 222, 140, 255);
    static readonly Color Ink = new Color32(70, 44, 24, 255);
    static readonly Color Light = new Color32(245, 236, 216, 255);
    static readonly Color Dim = new Color32(190, 172, 146, 255);
    static readonly Color Purple = new Color32(190, 110, 255, 255);
    static readonly Color Legend = new Color32(255, 196, 70, 255);

    [MenuItem("Tools/UIAnimation Dev/Rebuild RPG Sample")]
    public static void Build()
    {
        ImportArt();
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "Font/PassionOne-Regular SDF.asset");
        Directory.CreateDirectory(Items);

        var tab = SaveItem(TabItem());
        var dot = SaveItem(DotItem());
        var banner = SaveItem(BannerItem());
        var heroCard = SaveItem(HeroCardItem());
        var statRow = SaveItem(StatRowItem());
        var skillRow = SaveItem(SkillRowItem());
        var section = SaveItem(SectionItem());
        var storyRow = SaveItem(StoryRowItem());
        var slot = SaveItem(SlotItem());
        var bagTab = SaveItem(BagTabItem(slot));
        var chapterCard = SaveItem(ChapterItem());
        var questRow = SaveItem(QuestRowItem());
        var shopCard = SaveItem(ShopCardItem());
        var mailRow = SaveItem(MailRowItem());

        var scene = BuildScene(new Dictionary<string, UIAnimScreen>
        {
            ["lobby"] = Save(Lobby(banner, dot)),
            ["heroes"] = Save(Heroes(heroCard)),
            ["heroDetail"] = Save(HeroDetail(statRow, skillRow, section, storyRow)),
            ["bag"] = Save(Bag(bagTab, tab)),
            ["summon"] = Save(Summon()),
            ["adventure"] = Save(Adventure(chapterCard)),
            ["quests"] = Save(Quests(questRow, section)),
            ["itemInfo"] = Save(ItemInfo()),
            ["summonResult"] = Save(SummonResult()),
            ["victory"] = Save(Victory()),
            ["defeat"] = Save(Defeat()),
            ["title"] = Save(Title()),
            ["loading"] = Save(Loading()),
            ["upgrade"] = Save(Upgrade()),
            ["shop"] = Save(Shop(shopCard)),
            ["formation"] = Save(Formation()),
            ["mail"] = Save(Mail(mailRow)),
            ["purchase"] = Save(Purchase()),
            ["attendance"] = Save(Attendance()),
        });
        var list = EditorBuildSettings.scenes.ToList();
        if (list.All(e => e.path != scene)) list.Add(new EditorBuildSettingsScene(scene, true));
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log($"[RpgSampleBuilder] Built {Prefabs}, {scene}");
    }

    static void ImportArt()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "Art" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spriteBorder = path.Contains("/Frames/") ? new Vector4(44, 44, 44, 44) : Vector4.zero;
            importer.SaveAndReimport();
        }
    }

    static Sprite Art(string folder, string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}Art/{folder}/{name}.png") ?? throw new System.Exception("no art " + folder + "/" + name);
    static Sprite Frame(string name) => Art("Frames", name);
    static Sprite Icon(string name) => Art("Icons", name);
    static Sprite Fx(string name) => Art("Effects", name);

    // ---------- layout ----------

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

    static RectTransform Fill(string name, Transform parent, float inset = 0)
    {
        var rt = Node(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        return rt;
    }

    // Stretched with margins (left, right, top, bottom).
    static RectTransform Stretch(string name, Transform parent, float left, float right, float top, float bottom)
    {
        var rt = Node(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
        return rt;
    }

    static Image Img(RectTransform rt, Sprite sprite, Color? color = null, bool raycast = false)
    {
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color ?? Color.white;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.raycastTarget = raycast;
        return image;
    }

    static Image Pic(string name, Transform parent, Sprite sprite, float x, float y, float w, float h, Color? color = null) => Img(Center(name, parent, x, y, w, h), sprite, color);

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

    // A button that dips when pressed and springs back (states "Press" / "Release", played by a UIAnimTrigger).
    static Button Btn(Transform parent, string name, string label, float x, float y, float w, float h, bool gold = true)
    {
        var rt = Center(name, parent, x, y, w, h);
        Img(rt, Frame(gold ? "button" : "button_dark"), raycast: true);
        var button = rt.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        button.colors = colors;
        if (label != null) Text(Fill("Label", rt), label, h * 0.4f, gold ? Ink : Gold);
        Pressable(rt);
        return button;
    }

    static void Pressable(RectTransform rt)
    {
        var anim = Get<UIAnimation>(rt);
        anim.states.Add(State("Press", To(rt, UIAnimProperty.Scale, S(0.94f), 0.12f, UIAnimEase.OutSine)));
        anim.states.Add(State("Release", To(rt, UIAnimProperty.Scale, S(1), 0.35f, UIAnimEase.OutBack)));
        var trigger = rt.gameObject.AddComponent<UIAnimTrigger>();
        trigger.bindings.Add(new UIAnimBinding { evt = UIAnimEvent.PointerDown, state = "Press" });
        trigger.bindings.Add(new UIAnimBinding { evt = UIAnimEvent.PointerUp, state = "Release" });
    }

    static CanvasGroup Group(Component c) => Get<CanvasGroup>(c);

    // Not `??`: a missing component is a fake null in the editor.
    static T Get<T>(Component c) where T : Component
    {
        var existing = c.GetComponent<T>();
        return existing != null ? existing : c.gameObject.AddComponent<T>();
    }

    static (GameObject go, UIAnimScreen screen) NewScreen(string name)
    {
        var rt = Node(name, null, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        rt.gameObject.AddComponent<CanvasGroup>();
        rt.gameObject.AddComponent<UIAnimation>();
        return (rt.gameObject, rt.gameObject.AddComponent<UIAnimScreen>());
    }

    static UIAnimScreen Save(GameObject go)
    {
        var path = $"{Prefabs}{go.name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return AssetDatabase.LoadAssetAtPath<UIAnimScreen>(path);
    }

    static RectTransform SaveItem(GameObject go)
    {
        var path = $"{Items}{go.name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return AssetDatabase.LoadAssetAtPath<RectTransform>(path);
    }

    // A list item: the list positions the root, so the states move the inner Body.
    static (GameObject root, RectTransform body) ItemRoot(string name, float w, float h)
    {
        var root = Node(name, null, Half, Half, Vector2.zero, new Vector2(w, h));
        var body = Fill("Body", root);
        root.gameObject.AddComponent<UIAnimation>();
        return (root.gameObject, body);
    }

    static UIAnimScrollList ScrollList(string name, RectTransform rt, RectTransform itemPrefab)
    {
        rt.gameObject.name = name;
        Img(rt, null, new Color(0, 0, 0, 0), raycast: true);   // catches drags
        var viewport = Fill("Viewport", rt);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Node("Content", viewport, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero, new Vector2(0, 1));
        var scroll = rt.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.scrollSensitivity = 40f;
        var list = rt.gameObject.AddComponent<UIAnimScrollList>();
        list.itemPrefab = itemPrefab;
        return list;
    }

    // Top bar of a sub page: back, title, currencies.
    static RectTransform Header(Transform parent, string title, params RpgCurrency.Kind[] currencies)
    {
        var bar = Node("Header", parent, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 120), new Vector2(0.5f, 1));
        Img(bar, Frame("header"));
        var header = bar.gameObject.AddComponent<RpgHeader>();
        var back = Btn(bar, "BackButton", "<", 0, 0, 96, 84, gold: false);
        var backRt = (RectTransform)back.transform;
        backRt.anchorMin = backRt.anchorMax = new Vector2(0, 0.5f);
        backRt.anchoredPosition = new Vector2(80, 0);
        header.back = back;
        header.title = Text(Node("Title", bar, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(150, 0), new Vector2(700, 100), new Vector2(0, 0.5f)), title, 64, Gold, TextAlignmentOptions.Left);
        for (var i = 0; i < currencies.Length; i++) Pill(bar, currencies[currencies.Length - 1 - i], new Vector2(1, 0.5f), new Vector2(-40 - i * 290, 0), new Vector2(1, 0.5f));
        return bar;
    }

    static RectTransform Pill(Transform parent, RpgCurrency.Kind kind, Vector2 anchor, Vector2 position, Vector2 pivot)
    {
        var pill = Node(kind + "Pill", parent, anchor, anchor, position, new Vector2(260, 72), pivot);
        Img(pill, Frame("pill"));
        Pic("Icon", pill, Icon(kind switch { RpgCurrency.Kind.Gold => "gold", RpgCurrency.Kind.Gems => "gem", _ => "stamina" }), -100, 2, 72, 72);
        var currency = pill.gameObject.AddComponent<RpgCurrency>();
        currency.kind = kind;
        currency.text = Label(pill, "Value", "0", 24, 0, 170, 60, 40, Light, TextAlignmentOptions.Right);
        pill.gameObject.AddComponent<UIAnimation>().states.Add(State("Bump",
            Keys(pill, UIAnimProperty.Scale, K(0, S(1)), Vibrate(UIAnimCurveType.Punch, 0.4f, S(0.12f, 0.12f), 5, 0.6f)),
            Keys(currency.text, UIAnimProperty.GraphicColor, K(0, Gold), K(0.4f, (Vector4)Light, UIAnimEase.InOutSine))));
        return pill;
    }

    static GameObject[] StarRow(Transform parent, float y, float size, float spacing, int count = 5)
    {
        var row = Center("Stars", parent, 0, y, count * spacing, size);
        return Enumerable.Range(0, count).Select(i => Pic("Star" + i, row, Icon("star"), (i - (count - 1) / 2f) * spacing, 0, size, size).gameObject).ToArray();
    }

    // ---------- motion ----------

    static Vector4 V(float x, float y) => new(x, y, 0, 0);
    static Vector4 A(float a) => new(a, 0, 0, 0);
    static Vector4 S(float s) => new(s, s, 1, 0);
    static Vector4 S(float x, float y) => new(x, y, 1, 0);
    static Vector4 Rot(float x, float y, float z) => new(x, y, z, 0);

    static UIAnimKey K(float time, Vector4 value, UIAnimEase ease = UIAnimEase.Linear) => new() { time = time, value = value, ease = ease };

    static UIAnimKey Vibrate(UIAnimCurveType type, float time, Vector4 strength, float frequency, float damping) =>
        new() { time = time, value = strength, curveType = type, ease = UIAnimEase.Linear, frequency = frequency, damping = damping };

    static UIAnimTrack Keys(Component target, UIAnimProperty property, params UIAnimKey[] keys)
    {
        var track = new UIAnimTrack { target = target, property = property, start = UIAnimStart.KeyValue };
        track.keys.AddRange(keys);
        UIAnimationEditorWindow.EnsureStartKey(track);
        return track;
    }

    static UIAnimTrack Go(Component target, UIAnimProperty property, Vector4 from, Vector4 to, float start, float duration, UIAnimEase ease) =>
        Keys(target, property, K(start, from), K(start + duration, to, ease));

    static UIAnimTrack Fade(Component target, float from, float to, float start, float duration, UIAnimEase ease = UIAnimEase.InOutSine) =>
        Go(target is CanvasGroup ? target : Group(target), UIAnimProperty.CanvasGroupAlpha, A(from), A(to), start, duration, ease);

    static UIAnimTrack Move(RectTransform target, Vector2 from, float start, float duration, UIAnimEase ease = UIAnimEase.OutCubic) =>
        Go(target, UIAnimProperty.AnchoredPosition, from, target.anchoredPosition, start, duration, ease);

    static UIAnimTrack Hold(Component target, UIAnimProperty property, Vector4 value) => Keys(target, property, K(0, value), K(0.01f, value));

    // From wherever it is now.
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

    // Scroll scrub (0 = start edge, 0.5 = center, 1 = end edge).
    static UIAnimTrack Scrub(Component target, UIAnimProperty property, Vector4 edge, Vector4 center) =>
        Keys(target, property, K(0, edge), K(0.5f, center, UIAnimEase.InOutSine), K(1, edge, UIAnimEase.InOutSine));

    // A looping state (state Loop), optionally started by Play On: On Enable — both set in the UI Animation window.
    static void Loop(Component c, string state, bool playOnEnable)
    {
        c.GetComponent<UIAnimation>().states.Find(s => s.name == state).loop = true;
        if (playOnEnable) Get<UIAnimTrigger>(c).bindings.Add(new UIAnimBinding { evt = UIAnimEvent.OnEnable, state = state });
    }

    static UIAnimState State(string name, params UIAnimTrack[] tracks) => new() { name = name, tracks = tracks.Where(t => t != null).ToList() };
    static UIAnimState State(string name, IEnumerable<UIAnimTrack> tracks) => State(name, tracks.ToArray());
    static void States(GameObject go, params UIAnimState[] states) => go.GetComponent<UIAnimation>().states.AddRange(states);

    // Sub pages: fade in with a slight settle, stay under the next page, fade out when popped.
    static UIAnimState[] PageStates(UIAnimScreen screen, CanvasGroup content, RectTransform header, IEnumerable<UIAnimTrack> enterExtra = null)
    {
        var rt = screen.Rect;
        var enter = new List<UIAnimTrack>
        {
            Fade(content, 0, 1, 0, 0.45f),
            Go(rt, UIAnimProperty.Scale, S(1.04f), S(1), 0, 0.6f, UIAnimEase.OutSine),
            Move(header, new Vector2(0, 130), 0.1f, 0.5f, UIAnimEase.OutCubic),
        };
        if (enterExtra != null) enter.AddRange(enterExtra);
        return new[]
        {
            State("PushEnter", enter),
            State("PushExit", Go(rt, UIAnimProperty.Scale, S(1), S(0.97f), 0, 0.5f, UIAnimEase.InOutSine)),
            State("PopEnter", Go(rt, UIAnimProperty.Scale, S(0.97f), S(1), 0, 0.5f, UIAnimEase.OutSine)),
            State("PopExit", Fade(content, 1, 0, 0, 0.4f), Go(rt, UIAnimProperty.Scale, S(1), S(1.04f), 0, 0.45f, UIAnimEase.InOutSine)),
        };
    }

    static RectTransform Content(GameObject go, string background)
    {
        var content = Fill("Content", go.transform);
        Img(content, Art("Backgrounds", background));
        content.gameObject.AddComponent<CanvasGroup>();
        return content;
    }

    // ---------- list items ----------

    static GameObject TabItem()
    {
        var (root, body) = ItemRoot("Tab", 300, 84);
        root.AddComponent<Button>().transition = Selectable.Transition.None;
        Img((RectTransform)root.transform, null, new Color(0, 0, 0, 0), raycast: true);
        var label = Text(Fill("Label", body), "TAB", 44, Dim);
        var line = Node("Underline", body, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(220, 8), new Vector2(0.5f, 0));
        Img(line, Frame("bar"), Gold);
        States(root,
            State("On", To(label, UIAnimProperty.GraphicColor, Gold, 0.25f, UIAnimEase.OutSine), To(line, UIAnimProperty.Scale, S(1), 0.35f, UIAnimEase.OutBack)),
            State("Off", To(label, UIAnimProperty.GraphicColor, Dim, 0.25f, UIAnimEase.OutSine), To(line, UIAnimProperty.Scale, S(0, 1), 0.2f, UIAnimEase.InSine)));
        return root;
    }

    static GameObject DotItem()
    {
        var (root, body) = ItemRoot("Dot", 26, 26);
        var dot = Img(body, Fx("dot"), Dim);
        States(root,
            State("On", To(body, UIAnimProperty.Scale, S(1.3f), 0.3f, UIAnimEase.OutBack), To(dot, UIAnimProperty.GraphicColor, Gold, 0.3f, UIAnimEase.OutSine)),
            State("Off", To(body, UIAnimProperty.Scale, S(0.8f), 0.3f, UIAnimEase.OutSine), To(dot, UIAnimProperty.GraphicColor, new Color(1, 1, 1, 0.35f), 0.3f, UIAnimEase.OutSine)));
        return root;
    }

    // Event banner: Parallax (scrub) slides the art against the card as it passes.
    static GameObject BannerItem()
    {
        var (root, body) = ItemRoot("Banner", 900, 300);
        Img(body, Frame("dark"));
        var art = Center("Art", body, -300, 0, 240, 240);
        Pic("Glow", art, Fx("glow"), 0, 0, 360, 360, new Color(1, 0.8f, 0.4f, 0.5f));
        var icon = Pic("Icon", art, null, 0, 0, 220, 220);
        var row = root.AddComponent<RpgRow>();
        row.icon = icon;
        row.title = Label(body, "Title", "EVENT", 120, 50, 560, 90, 66, Gold, TextAlignmentOptions.Left);
        row.text = Label(body, "Text", "", 120, -40, 560, 80, 36, Light, TextAlignmentOptions.Left);
        States(root, State("Parallax",
            Scrub(art, UIAnimProperty.AnchoredPosition, V(-120, 0), V(-300, 0)),
            Scrub(row.title.rectTransform, UIAnimProperty.AnchoredPosition, V(260, 50), V(120, 50)),
            Scrub(Group(body), UIAnimProperty.CanvasGroupAlpha, A(0.4f), A(1))));
        return root;
    }

    static GameObject HeroCardItem()
    {
        var (root, body) = ItemRoot("HeroCard", 230, 340);
        Img(body, Frame("dark"), raycast: true);
        body.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        Pressable(body);
        var card = root.AddComponent<RpgHeroCard>();
        card.portrait = Pic("Portrait", body, null, 0, 26, 192, 250);
        card.frame = Img(Fill("Frame", card.portrait.rectTransform, -6), Frame("card"));
        card.level = Label(card.portrait.transform, "Level", "Lv. 1", -40, 105, 120, 40, 30, Light, TextAlignmentOptions.Left);
        card.stars = StarRow(body, -114, 26, 24);
        card.heroName = Label(body, "Name", "Hero", 0, -146, 210, 44, 36, Gold);
        States(root, State("Show", Fade(Group(body), 0, 1, 0, 0.25f), Go(body, UIAnimProperty.Scale, S(0.85f), S(1), 0, 0.4f, UIAnimEase.OutBack)));
        return root;
    }

    // Detail rows: Show slides each in from the right; the stat bar fills after it.
    static UIAnimState RowShow(RectTransform body, params UIAnimTrack[] more) =>
        State("Show", new[] { Fade(Group(body), 0, 1, 0, 0.3f), Go(body, UIAnimProperty.AnchoredPosition, V(60, 0), V(0, 0), 0, 0.4f, UIAnimEase.OutCubic) }.Concat(more));

    static GameObject StatRowItem()
    {
        var (root, body) = ItemRoot("StatRow", 940, 92);
        var row = root.AddComponent<RpgRow>();
        row.icon = Pic("Icon", body, null, -420, 0, 64, 64);
        row.title = Label(body, "Name", "ATK", -280, 0, 160, 60, 40, Dim, TextAlignmentOptions.Left);
        var bar = Center("Bar", body, 90, 0, 520, 24);
        Img(bar, Frame("bar"), new Color(0, 0, 0, 0.5f));
        row.fill = Node("Fill", bar, Vector2.zero, new Vector2(0.6f, 1), Vector2.zero, Vector2.zero, new Vector2(0, 0.5f));
        Img(row.fill, Frame("bar"), Gold);
        row.value = Label(body, "Value", "0", 400, 0, 160, 60, 42, Light, TextAlignmentOptions.Right);
        States(root, RowShow(body, Go(row.fill, UIAnimProperty.Scale, S(0, 1), S(1), 0.15f, 0.6f, UIAnimEase.OutCubic)));
        return root;
    }

    static GameObject SkillRowItem()
    {
        var (root, body) = ItemRoot("SkillRow", 940, 120);
        var row = root.AddComponent<RpgRow>();
        Img(Center("Slot", body, -400, 0, 104, 104), Frame("slot"), Purple);
        row.icon = Pic("Icon", body, null, -400, 0, 84, 84);
        row.title = Label(body, "Name", "Skill", 60, 22, 800, 50, 42, Gold, TextAlignmentOptions.Left);
        row.text = Label(body, "Text", "", 60, -24, 800, 50, 32, Light, TextAlignmentOptions.Left);
        States(root, RowShow(body));
        return root;
    }

    static GameObject SectionItem()
    {
        var (root, body) = ItemRoot("Section", 940, 72);
        Img(body, Frame("header"), new Color(1, 1, 1, 0.9f));
        var row = root.AddComponent<RpgRow>();
        row.title = Text(Stretch("Title", body, 30, 30, 0, 0), "SECTION", 40, Gold, TextAlignmentOptions.Left);
        States(root, State("Show", Fade(Group(body), 0, 1, 0, 0.3f)));
        return root;
    }

    static GameObject StoryRowItem()
    {
        var (root, body) = ItemRoot("StoryRow", 940, 200);
        var row = root.AddComponent<RpgRow>();
        row.text = Text(Stretch("Text", body, 30, 30, 20, 20), "", 36, Light, TextAlignmentOptions.TopLeft);
        row.text.fontStyle = FontStyles.Italic;
        States(root, State("Show", Fade(Group(body), 0, 1, 0, 0.6f)));
        return root;
    }

    static GameObject SlotItem()
    {
        var (root, body) = ItemRoot("ItemSlot", 180, 180);
        body.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        var slot = root.AddComponent<RpgItemSlot>();
        slot.frame = Img(body, Frame("slot"), raycast: true);
        slot.icon = Pic("Icon", body, null, 0, 4, 140, 140);
        slot.count = Label(body, "Count", "", 30, -60, 110, 40, 32, Light, TextAlignmentOptions.Right);
        Pressable(body);
        States(root, State("Show", Fade(Group(body), 0, 1, 0, 0.2f), Go(body, UIAnimProperty.Scale, S(0.6f), S(1), 0, 0.35f, UIAnimEase.OutBack)));
        return root;
    }

    // A bag page: a vertical grid nested in the horizontal pager.
    static GameObject BagTabItem(RectTransform slot)
    {
        var root = Node("BagTab", null, Half, Half, Vector2.zero, new Vector2(1760, 760));
        var grid = ScrollList("Grid", Fill("Grid", root), slot);
        grid.layout = UIAnimScrollList.LayoutType.Grid;
        grid.cellSize = new Vector2(180, 180);
        grid.gridSpacing = new Vector2(28, 28);
        grid.padding = new RectOffset(20, 20, 20, 20);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.entryState = "Show";
        grid.entryStagger = 0.03f;
        grid.appearState = "Show";
        root.gameObject.AddComponent<RpgBagTab>().grid = grid;
        return root.gameObject;
    }

    // A chapter in the carousel: Focus (scrub) grows and lights the centered one.
    static GameObject ChapterItem()
    {
        var (root, body) = ItemRoot("ChapterCard", 380, 280);
        Img(body, Frame("dark"));
        var row = root.AddComponent<RpgRow>();
        Pic("Glow", body, Fx("glow"), 0, 30, 300, 300, new Color(1, 0.8f, 0.4f, 0.35f));
        row.icon = Pic("Icon", body, null, 0, 30, 150, 150);
        row.title = Label(body, "Name", "Chapter", 0, -78, 360, 50, 40, Gold);
        row.text = Label(body, "Progress", "0/10", 0, -114, 200, 40, 30, Light);
        States(root, State("Focus", Scrub(body, UIAnimProperty.Scale, S(0.78f), S(1)), Scrub(Group(body), UIAnimProperty.CanvasGroupAlpha, A(0.45f), A(1))));
        return root;
    }

    static GameObject QuestRowItem()
    {
        var (root, body) = ItemRoot("QuestRow", 1500, 128);
        root.AddComponent<RectMask2D>();   // folding (ResizeItem) clips it
        var frame = Stretch("Frame", body, 0, 0, 6, 6);
        Img(frame, Frame("dark"));
        var q = root.AddComponent<RpgQuestRow>();
        Img(Node("Slot", body, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(70, 0), new Vector2(96, 96)), Frame("slot"), Gold);
        q.rewardIcon = Img(Node("Reward", body, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(70, 8), new Vector2(70, 70)), null);
        q.reward = Text(Node("Amount", body, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(70, -32), new Vector2(110, 34)), "0", 28, Light);
        q.title = Text(Node("Title", body, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(150, 22), new Vector2(800, 54), new Vector2(0, 0.5f)), "Quest", 44, Light, TextAlignmentOptions.Left);
        var bar = Node("Bar", body, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(150, -28), new Vector2(560, 22), new Vector2(0, 0.5f));
        Img(bar, Frame("bar"), new Color(0, 0, 0, 0.5f));
        q.fill = Node("Fill", bar, Vector2.zero, new Vector2(0.5f, 1), Vector2.zero, Vector2.zero, new Vector2(0, 0.5f));
        Img(q.fill, Frame("bar"), Gold);
        q.progress = Text(Node("Progress", body, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(730, -28), new Vector2(240, 40), new Vector2(0, 0.5f)), "0/1", 32, Dim, TextAlignmentOptions.Left);
        q.claim = Btn(body, "ClaimButton", null, 0, 0, 220, 84);
        var claimRt = (RectTransform)q.claim.transform;
        claimRt.anchorMin = claimRt.anchorMax = new Vector2(1, 0.5f);
        claimRt.anchoredPosition = new Vector2(-150, 0);
        q.buttonLabel = Text(Fill("Label", claimRt), "CLAIM", 36, Ink);
        var stamp = Text(Node("Stamp", body, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-420, 0), new Vector2(300, 100)), "CLEAR!", 80, new Color32(220, 70, 50, 255));
        stamp.rectTransform.localEulerAngles = new Vector3(0, 0, -12);
        States(root,
            State("Show", Fade(Group(body), 0, 1, 0, 0.3f), Go(body, UIAnimProperty.AnchoredPosition, V(80, 0), V(0, 0), 0, 0.45f, UIAnimEase.OutCubic)),
            State("Claim",
                Keys(stamp, UIAnimProperty.GraphicAlpha, K(0, A(0)), K(0.08f, A(1))),
                Go(stamp.rectTransform, UIAnimProperty.Scale, S(2.4f), S(1), 0, 0.22f, UIAnimEase.InQuad),
                Keys(body, UIAnimProperty.AnchoredPosition, K(0.22f, V(0, 0)), Vibrate(UIAnimCurveType.Shake, 0.42f, V(10, 4), 20, 0.6f)),
                Go(Group(frame), UIAnimProperty.CanvasGroupAlpha, A(1), A(0.55f), 0.3f, 0.3f, UIAnimEase.InOutSine)));
        stamp.color = new Color(stamp.color.r, stamp.color.g, stamp.color.b, 0);
        return root;
    }

    // ---------- pages ----------

    static GameObject Lobby(RectTransform banner, RectTransform dot)
    {
        var (go, screen) = NewScreen("Lobby");
        var content = Content(go, "lobby");
        var group = content.GetComponent<CanvasGroup>();
        var lobby = go.AddComponent<RpgLobby>();

        // Player
        var player = Node("Player", content, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -24), new Vector2(560, 140), new Vector2(0, 1));
        Img(player, Frame("dark"));
        Img(Center("Avatar", player, -200, 0, 110, 110), Frame("slot"), Gold);
        Pic("Crown", player, Icon("crown"), -200, 2, 86, 86);
        Label(player, "Name", "COMMANDER", 60, 30, 340, 50, 44, Gold, TextAlignmentOptions.Left);
        Label(player, "Level", "Lv. 27", 60, -6, 340, 40, 34, Light, TextAlignmentOptions.Left);
        var exp = Center("Exp", player, 60, -42, 340, 20);
        Img(exp, Frame("bar"), new Color(0, 0, 0, 0.5f));
        Img(Node("Fill", exp, Vector2.zero, new Vector2(0.62f, 1), Vector2.zero, Vector2.zero, new Vector2(0, 0.5f)), Frame("bar"), Gold);

        // Currencies
        var pills = new[] { RpgCurrency.Kind.Stamina, RpgCurrency.Kind.Gold, RpgCurrency.Kind.Gems }
            .Select((k, i) => Pill(content, k, new Vector2(1, 1), new Vector2(-30 - (2 - i) * 285, -50), new Vector2(1, 1))).ToArray();

        // Hero, breathing
        var hero = Center("Hero", content, -540, -40, 500, 650);
        var heroGroup = Group(hero);
        var glow = Pic("Glow", hero, Fx("glow"), 0, 40, 900, 900, new Color(1, 0.8f, 0.45f, 0.5f));
        var heroBody = Center("Body", hero, 0, 0, 460, 598);
        Img(heroBody, Art("Heroes", "aria"));
        Img(Fill("Frame", heroBody, -10), Frame("card"), Legend);
        var plate = Center("Plate", hero, 0, -330, 380, 70);
        Img(plate, Frame("dark"));
        Text(Fill("Text", plate), "ARIA  ·  RANGER", 36, Gold);
        var idle = hero.gameObject.AddComponent<UIAnimation>();
        idle.states.Add(State("Idle",
            Keys(heroBody, UIAnimProperty.Scale, K(0, S(1)), K(1.6f, S(1.025f), UIAnimEase.InOutSine), K(3.2f, S(1), UIAnimEase.InOutSine)),
            Keys(heroBody, UIAnimProperty.AnchoredPosition, K(0, V(0, 0)), K(1.6f, V(0, 8), UIAnimEase.InOutSine), K(3.2f, V(0, 0), UIAnimEase.InOutSine)),
            Keys(glow, UIAnimProperty.GraphicAlpha, K(0, A(0.35f)), K(1.6f, A(0.65f), UIAnimEase.InOutSine), K(3.2f, A(0.35f), UIAnimEase.InOutSine))));
        Loop(hero, "Idle", playOnEnable: true);

        // Event banner: loops, one per swipe, turns by itself every 3 s
        var bannerRt = Center("Banners", content, 380, 170, 900, 300);
        var banners = ScrollList("Banners", bannerRt, banner);
        banners.layout = UIAnimScrollList.LayoutType.Horizontal;
        banners.controlChildSize = true;
        banners.loop = true;
        banners.snap = UIAnimScrollList.SnapMode.OnePerSwipe;
        banners.snapDuration = 0.5f;
        banners.autoScrollInterval = 3f;
        banners.scrubState = "Parallax";
        banners.playEntryOnFill = false;
        lobby.banners = banners;
        var dots = Center("Dots", content, 380, 0, 300, 40);
        var layout = dots.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 18;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var indicator = dots.gameObject.AddComponent<UIAnimPageIndicator>();
        indicator.list = banners;
        indicator.itemPrefab = dot;

        // Menu
        var menu = new[] { ("HEROES", "menu_heroes"), ("BAG", "menu_bag"), ("SUMMON", "menu_summon"), ("ADVENTURE", "menu_adventure"), ("QUESTS", "menu_quests") }
            .Select((m, i) =>
            {
                var b = Btn(content, m.Item1 + "Button", null, -40 + i * 220, -350, 200, 200, gold: false);
                Pic("Icon", b.transform, Icon(m.Item2), 0, 18, 124, 124);
                Label(b.transform, "Label", m.Item1, 0, -70, 200, 40, 32, Gold);
                return b;
            }).ToArray();
        (lobby.heroes, lobby.bag, lobby.summon, lobby.adventure, lobby.quests) = (menu[0], menu[1], menu[2], menu[3], menu[4]);

        // Side buttons under the player panel.
        var side = new[] { ("SHOP", "chest"), ("MAIL", "scroll"), ("DAILY", "hourglass") }
            .Select((m, i) =>
            {
                var b = Btn(content, m.Item1 + "Button", null, -870, 250 - i * 150, 130, 130, gold: false);
                var rt = (RectTransform)b.transform;
                Pic("Icon", rt, Icon(m.Item2), 0, 12, 80, 80);
                Label(rt, "Label", m.Item1, 0, -44, 130, 34, 26, Gold);
                return b;
            }).ToArray();
        (lobby.shop, lobby.mail, lobby.attendance) = (side[0], side[1], side[2]);
        var badge = Center("Badge", menu[4].transform, 74, 74, 48, 48);
        Img(badge, Fx("dot"), new Color32(230, 60, 50, 255));
        Text(Fill("Mark", badge), "!", 36, Color.white);
        badge.gameObject.AddComponent<UIAnimation>().states.Add(State("Pulse",
            Keys(badge, UIAnimProperty.Scale, K(0, S(1)), K(0.25f, S(1.3f), UIAnimEase.OutSine), K(0.6f, S(1), UIAnimEase.OutBack), K(1.4f, S(1)))));
        Loop(badge, "Pulse", playOnEnable: true);

        // Intro: the world fades in, the panels slide in from their edges, the hero steps in, the menu rises one by one.
        var enter = new List<UIAnimTrack>
        {
            Fade(group, 0, 1, 0, 0.6f),
            Move(player, new Vector2(-620, -24), 0.2f, 0.7f),
            Fade(heroGroup, 0, 1, 0.3f, 0.8f),
            Move(hero, new Vector2(-640, -40), 0.3f, 0.9f, UIAnimEase.OutSine),
            Fade(bannerRt, 0, 1, 0.5f, 0.6f),
            Go(bannerRt, UIAnimProperty.Scale, S(0.94f), S(1), 0.5f, 0.7f, UIAnimEase.OutSine),
            Fade(dots, 0, 1, 0.8f, 0.4f),
        };
        enter.AddRange(pills.Select((p, i) => Move(p, new Vector2(p.anchoredPosition.x, 120), 0.3f + i * 0.08f, 0.6f)));
        foreach (var (b, i) in side.Select((b, i) => (b, i)))
        {
            var rt = (RectTransform)b.transform;
            enter.Add(Fade(Group(b), 0, 1, 0.4f + i * 0.08f, 0.3f));
            enter.Add(Move(rt, new Vector2(-1100, rt.anchoredPosition.y), 0.4f + i * 0.08f, 0.5f, UIAnimEase.OutBack));
        }
        foreach (var (b, i) in menu.Select((b, i) => (b, i)))
        {
            var rt = (RectTransform)b.transform;
            enter.Add(Fade(Group(b), 0, 1, 0.6f + i * 0.08f, 0.3f));
            enter.Add(Move(rt, new Vector2(rt.anchoredPosition.x, -560), 0.6f + i * 0.08f, 0.55f, UIAnimEase.OutBack));
        }
        States(go,
            State("PushEnter", enter),
            State("PushExit", Go(screen.Rect, UIAnimProperty.Scale, S(1), S(1.05f), 0, 0.5f, UIAnimEase.InOutSine)),
            State("PopEnter", Go(screen.Rect, UIAnimProperty.Scale, S(1.05f), S(1), 0, 0.5f, UIAnimEase.OutSine)));
        return go;
    }

    static GameObject Heroes(RectTransform heroCard)
    {
        var (go, screen) = NewScreen("Heroes");
        var content = Content(go, "hall");
        var header = Header(content, "HEROES", RpgCurrency.Kind.Gold, RpgCurrency.Kind.Gems);
        var page = go.AddComponent<RpgHeroesPage>();
        page.sort = Btn(content, "SortButton", null, 0, 0, 280, 80, gold: false);
        var sortRt = (RectTransform)page.sort.transform;
        sortRt.anchorMin = sortRt.anchorMax = new Vector2(1, 1);
        sortRt.anchoredPosition = new Vector2(-180, -175);
        page.sortLabel = Text(Fill("Label", sortRt), "BY LEVEL", 34, Gold);
        var list = ScrollList("List", Stretch("List", content, 120, 120, 230, 30), heroCard);
        list.layout = UIAnimScrollList.LayoutType.Grid;
        list.cellSize = new Vector2(230, 340);
        list.gridSpacing = new Vector2(36, 36);
        list.padding = new RectOffset(10, 10, 10, 10);
        list.childAlignment = TextAnchor.UpperCenter;
        list.entryState = "Show";
        list.entryStagger = 0.06f;
        list.appearState = "Show";
        page.list = list;
        States(go, PageStates(screen, content.GetComponent<CanvasGroup>(), header));
        return go;
    }

    static GameObject HeroDetail(RectTransform statRow, RectTransform skillRow, RectTransform section, RectTransform storyRow)
    {
        var (go, screen) = NewScreen("HeroDetail");
        var content = Content(go, "hall");
        var group = content.GetComponent<CanvasGroup>();
        var header = Header(content, "HERO", RpgCurrency.Kind.Gold, RpgCurrency.Kind.Gems);
        var detail = go.AddComponent<RpgHeroDetail>();

        var info = Center("Info", content, 300, -60, 1000, 840);
        Img(info, Frame("dark"));
        var list = ScrollList("List", Fill("List", info, 20), statRow);
        list.extraPrefabs.AddRange(new[] { skillRow, section, storyRow });
        list.layout = UIAnimScrollList.LayoutType.Vertical;
        list.controlChildSize = true;
        list.padding = new RectOffset(0, 0, 250, 30);
        list.spacing = 6;
        list.entryState = "Show";
        list.entryStagger = 0.05f;
        list.appearState = "Show";
        detail.list = list;

        // The title area sits over the top of the list and collapses as it scrolls (0 → 130 px).
        var head = Node("Head", info, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -20), new Vector2(-40, 240), new Vector2(0.5f, 1));
        Img(head, Frame("header"));
        detail.heroName = Text(Node("Name", head, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -20), new Vector2(600, 100), new Vector2(0, 1)), "Hero", 88, Gold, TextAlignmentOptions.TopLeft);
        detail.role = Text(Node("Role", head, new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -118), new Vector2(400, 50), new Vector2(0, 1)), "ROLE", 40, Light, TextAlignmentOptions.TopLeft);
        detail.level = Text(Node("Level", head, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -26), new Vector2(240, 60), new Vector2(1, 1)), "Lv. 1", 52, Light, TextAlignmentOptions.TopRight);
        var stars = Node("StarsArea", head, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -170), new Vector2(260, 50), new Vector2(0, 1));
        detail.stars = StarRow(stars, 0, 46, 48);
        detail.weapon = Img(Node("Weapon", head, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -90), new Vector2(110, 110), new Vector2(1, 1)), null);
        var collapse = head.gameObject.AddComponent<UIAnimation>();
        collapse.states.Add(State("Collapse",
            Go(head, UIAnimProperty.SizeDelta, V(-40, 240), V(-40, 120), 0, 1, UIAnimEase.Linear),
            Go(detail.heroName, UIAnimProperty.FontSize, A(88), A(60), 0, 1, UIAnimEase.Linear),
            Go(Group(detail.role), UIAnimProperty.CanvasGroupAlpha, A(1), A(0), 0, 0.5f, UIAnimEase.Linear),
            Go(Group(stars), UIAnimProperty.CanvasGroupAlpha, A(1), A(0), 0, 0.5f, UIAnimEase.Linear),
            Go(detail.weapon.rectTransform, UIAnimProperty.Scale, S(1), S(0.7f), 0, 1, UIAnimEase.Linear)));
        var scrub = info.gameObject.AddComponent<UIAnimScrollScrub>();
        scrub.list = list;
        scrub.target = collapse;
        scrub.state = "Collapse";
        scrub.distance = 130;

        detail.upgrade = Btn(content, "UpgradeButton", "UPGRADE", -540, -440, 340, 100);

        // The portrait (outside Content, so it is visible while Content fades) is the shared element.
        var portrait = Center("Portrait", go.transform, -540, -60, 460, 598);
        detail.portrait = Img(portrait, null);
        Img(Fill("Frame", portrait, -10), Frame("card"), Legend);
        var (pos, size) = ((Vector4)portrait.anchoredPosition, (Vector4)portrait.sizeDelta);
        UIAnimTrack[] Travel(float start, float duration) => new[]
        {
            Go(portrait, UIAnimProperty.AnchoredPosition, pos, pos, start, duration, UIAnimEase.InOutSine),
            Go(portrait, UIAnimProperty.SizeDelta, size, size, start, duration, UIAnimEase.InOutSine),
        };
        States(go,
            State("PushEnter", Travel(0, 0.55f).Concat(new[]
            {
                Fade(group, 0, 1, 0, 0.45f),
                Move(info, new Vector2(500, -60), 0.1f, 0.7f),
                Move(header, new Vector2(0, 130), 0.1f, 0.5f),
            })),
            State("PopExit", Travel(0.05f, 0.45f).Concat(new[] { Fade(group, 1, 0, 0, 0.35f) })));
        return go;
    }

    static GameObject Bag(RectTransform bagTab, RectTransform tab)
    {
        var (go, screen) = NewScreen("Bag");
        var content = Content(go, "hall");
        var header = Header(content, "BAG", RpgCurrency.Kind.Gold, RpgCurrency.Kind.Gems);
        var tabs = Node("Tabs", content, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -175), new Vector2(960, 84));
        var layout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var pager = ScrollList("Pager", Stretch("Pager", content, 80, 80, 240, 30), bagTab);
        pager.layout = UIAnimScrollList.LayoutType.Horizontal;
        pager.controlChildSize = true;
        pager.snap = UIAnimScrollList.SnapMode.OnePerSwipe;
        pager.snapDuration = 0.35f;
        pager.playEntryOnFill = false;
        var indicator = tabs.gameObject.AddComponent<UIAnimPageIndicator>();
        indicator.list = pager;
        indicator.itemPrefab = tab;
        indicator.labels = RpgData.Tabs.ToList();
        go.AddComponent<RpgBagPage>().pager = pager;
        States(go, PageStates(screen, content.GetComponent<CanvasGroup>(), header, new[] { Fade(tabs, 0, 1, 0.2f, 0.4f) }));
        return go;
    }

    static GameObject Summon()
    {
        var (go, screen) = NewScreen("Summon");
        var content = Content(go, "summon");
        var header = Header(content, "SUMMON", RpgCurrency.Kind.Gems);
        var page = go.AddComponent<RpgSummonPage>();
        var glow = Pic("Glow", content, Fx("glow"), 0, 40, 1100, 1100, new Color(0.7f, 0.4f, 1f, 0.5f));
        UIAnimation Spinner(string name, float size, float degrees, float seconds, Color color)
        {
            var circle = Pic(name, content, Fx("summon_circle"), 0, 40, size, size, color);
            var anim = circle.gameObject.AddComponent<UIAnimation>();
            anim.states.Add(State("Spin", Go(circle.rectTransform, UIAnimProperty.Rotation, Rot(0, 0, 0), Rot(0, 0, degrees), 0, seconds, UIAnimEase.Linear)));
            Loop(circle, "Spin", playOnEnable: true);
            return anim;
        }
        var outer = Spinner("Circle", 640, -360, 24, new Color(0.95f, 0.8f, 1f, 0.9f));
        Spinner("InnerCircle", 380, 360, 16, new Color(1f, 0.85f, 0.5f, 0.6f));
        glow.gameObject.AddComponent<UIAnimation>().states.Add(State("Pulse",
            Keys(glow, UIAnimProperty.GraphicAlpha, K(0, A(0.35f)), K(1.2f, A(0.7f), UIAnimEase.InOutSine), K(2.4f, A(0.35f), UIAnimEase.InOutSine))));
        Loop(glow, "Pulse", playOnEnable: true);
        var note = Label(content, "Note", "Legendary heroes appear twice as often this week", 0, -300, 1200, 50, 38, Light);
        Button SummonButton(string name, string label, string cost, float x, bool gold)
        {
            var b = Btn(content, name, null, x, -420, 400, 130, gold);
            Label(b.transform, "Label", label, 0, 20, 380, 60, 52, gold ? Ink : Gold);
            Pic("Gem", b.transform, Icon("gem"), -60, -32, 44, 44);
            Label(b.transform, "Cost", cost, 30, -32, 160, 44, 36, gold ? Ink : Light, TextAlignmentOptions.Left);
            b.GetComponent<UIAnimation>().states.Add(State("Deny", Keys(b.transform, UIAnimProperty.Rotation, K(0, Rot(0, 0, 0)), Vibrate(UIAnimCurveType.Shake, 0.4f, Rot(0, 0, 6), 25, 0.6f))));
            return b;
        }
        page.single = SummonButton("SingleButton", "SUMMON x1", $"{RpgDemo.SummonCost}", -260, false);
        page.ten = SummonButton("TenButton", "SUMMON x10", $"{RpgDemo.SummonCost * 10:N0}", 260, true);
        States(go, PageStates(screen, content.GetComponent<CanvasGroup>(), header, new[]
        {
            Go(outer.transform, UIAnimProperty.Scale, S(0.5f), S(1), 0, 1.2f, UIAnimEase.OutSine),
            Fade(note, 0, 1, 0.5f, 0.6f),
            Move((RectTransform)page.single.transform, new Vector2(-260, -620), 0.4f, 0.6f),
            Move((RectTransform)page.ten.transform, new Vector2(260, -620), 0.5f, 0.6f),
        }));
        return go;
    }

    static GameObject Adventure(RectTransform chapterCard)
    {
        var (go, screen) = NewScreen("Adventure");
        var content = Content(go, "lobby");
        Img(Fill("Shade", content), null, new Color(0, 0, 0, 0.45f));
        var header = Header(content, "ADVENTURE", RpgCurrency.Kind.Stamina, RpgCurrency.Kind.Gold);
        var page = go.AddComponent<RpgAdventurePage>();
        var chaptersRt = Center("Chapters", content, 0, 250, 1800, 300);
        var chapters = ScrollList("Chapters", chaptersRt, chapterCard);
        chapters.layout = UIAnimScrollList.LayoutType.Horizontal;
        chapters.padding = new RectOffset(710, 710, 0, 0);   // the first and last cards can reach the center
        chapters.spacing = 20;
        chapters.childAlignment = TextAnchor.MiddleLeft;
        chapters.scrubState = "Focus";
        chapters.snap = UIAnimScrollList.SnapMode.Nearest;
        chapters.playEntryOnFill = false;
        page.chapters = chapters;
        page.chapterTitle = Label(content, "ChapterTitle", "", 0, 70, 1400, 60, 48, Gold);

        // The stage path: ten nodes zigzagging left to right, dots between them.
        var path = Center("Path", content, 0, -150, 1800, 300);
        var pathGroup = Group(path);
        Vector2 At(int i) => new(-720 + i * 160, i % 2 == 0 ? 50 : -50);
        for (var i = 0; i < 9; i++)
            for (var d = 1; d <= 3; d++)
                Pic($"Dot{i}_{d}", path, Fx("dot"), Vector2.Lerp(At(i), At(i + 1), d / 4f).x, Vector2.Lerp(At(i), At(i + 1), d / 4f).y, 14, 14, new Color(1, 0.9f, 0.7f, 0.6f));
        page.stages = Enumerable.Range(0, 10).Select(i =>
        {
            var boss = i == 9;
            var size = boss ? 130 : 100;
            var rt = Center("Stage" + (i + 1), content, At(i).x, At(i).y - 150, size, size);
            Img(rt, null, new Color(0, 0, 0, 0), raycast: true);
            rt.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            var node = rt.gameObject.AddComponent<RpgStageNode>();
            var ring = Pic("Ring", rt, Fx("glow"), 0, 0, size * 2.2f, size * 2.2f, new Color(1, 0.85f, 0.4f, 0.9f));
            node.ring = ring.gameObject;
            var body = Fill("Body", rt);
            node.frame = Img(body, Fx("dot"));
            Img(Fill("Inner", body, 8), Fx("dot"), new Color32(60, 40, 30, 255));
            node.label = Text(Fill("Label", body), "1", boss ? 56 : 44, Light);
            if (boss) Pic("Boss", body, Icon("ch_dragon"), 0, size * 0.62f, 70, 70);
            var starRow = Center("Stars", rt, 0, -size * 0.62f, 90, 28);
            node.stars = starRow.gameObject;
            for (var s = 0; s < 3; s++) Pic("Star" + s, starRow, Icon("star"), (s - 1) * 28, 0, 28, 28);
            var anim = rt.gameObject.AddComponent<UIAnimation>();
            anim.states.Add(State("Pulse", Keys(body, UIAnimProperty.Scale, K(0, S(1)), K(0.6f, S(1.12f), UIAnimEase.InOutSine), K(1.2f, S(1), UIAnimEase.InOutSine))));
            Loop(rt, "Pulse", playOnEnable: false);   // RpgStageNode plays it on the next stage only
            return node;
        }).ToArray();

        page.stageInfo = Label(content, "StageInfo", "", 0, -360, 1200, 50, 40, Light);
        page.start = Btn(content, "StartButton", "START", 0, -450, 320, 110);

        // Each chapter change: the path fades in and the stages pop in one after another.
        var stages = new List<UIAnimTrack> { Fade(pathGroup, 0, 1, 0, 0.5f), Fade(page.chapterTitle, 0, 1, 0, 0.4f) };
        for (var i = 0; i < page.stages.Length; i++)
            stages.Add(Go(page.stages[i].transform, UIAnimProperty.Scale, S(0), S(1), 0.1f + i * 0.06f, 0.4f, UIAnimEase.OutBack));
        States(go, PageStates(screen, content.GetComponent<CanvasGroup>(), header, new[]
        {
            Move(chaptersRt, new Vector2(400, 250), 0, 0.8f),
            Move((RectTransform)page.start.transform, new Vector2(0, -650), 0.3f, 0.6f),
        }).Append(State("Stages", stages)).ToArray());
        return go;
    }

    static GameObject Quests(RectTransform questRow, RectTransform section)
    {
        var (go, screen) = NewScreen("Quests");
        var content = Content(go, "hall");
        var header = Header(content, "QUESTS", RpgCurrency.Kind.Stamina, RpgCurrency.Kind.Gold, RpgCurrency.Kind.Gems);
        var list = ScrollList("List", Stretch("List", content, 160, 160, 150, 20), questRow);
        list.extraPrefabs.Add(section);
        list.layout = UIAnimScrollList.LayoutType.Vertical;
        list.controlChildSize = true;
        list.spacing = 0;   // the gap is inside each row (its frame is inset), so a folded row leaves none behind
        list.padding = new RectOffset(0, 0, 10, 30);
        list.stickyHeaderPrefab = 1;
        list.entryState = "Show";
        list.entryStagger = 0.04f;
        list.appearState = "Show";
        go.AddComponent<RpgQuestsPage>().list = list;
        States(go, PageStates(screen, content.GetComponent<CanvasGroup>(), header));
        return go;
    }

    // ---------- popups ----------

    static GameObject ItemInfo()
    {
        var (go, screen) = NewScreen("ItemInfo");
        var info = go.AddComponent<RpgItemInfo>();
        var panel = Center("Panel", go.transform, 0, 0, 1040, 580);
        Img(panel, Frame("panel"));
        var group = Group(panel);
        info.frame = Img(Center("Frame", panel, -300, 30, 330, 330), Frame("slot"));
        info.itemName = Label(panel, "Name", "", 190, 180, 560, 90, 76, Ink, TextAlignmentOptions.Left);
        info.rarity = Label(panel, "Rarity", "", 190, 110, 560, 50, 40, Ink, TextAlignmentOptions.Left);
        info.description = Label(panel, "Description", "", 190, -10, 560, 150, 38, Ink, TextAlignmentOptions.TopLeft);
        info.count = Label(panel, "Count", "", 190, -120, 560, 50, 34, new Color32(140, 100, 60, 255), TextAlignmentOptions.Left);
        info.use = Btn(panel, "UseButton", "USE", 330, -205, 260, 96);
        info.close = Btn(panel, "CloseButton", "X", 470, 240, 84, 84, gold: false);
        var icon = Center("Icon", go.transform, -300, 30, 280, 280);
        info.icon = Img(icon, null);
        var (pos, size) = ((Vector4)icon.anchoredPosition, (Vector4)icon.sizeDelta);
        UIAnimTrack[] Travel(float start, float duration) => new[]
        {
            Go(icon, UIAnimProperty.AnchoredPosition, pos, pos, start, duration, UIAnimEase.InOutSine),
            Go(icon, UIAnimProperty.SizeDelta, size, size, start, duration, UIAnimEase.InOutSine),
        };
        States(go,
            State("Enter", Travel(0, 0.45f).Concat(new[]
            {
                Fade(group, 0, 1, 0.1f, 0.4f),
                Go(panel, UIAnimProperty.Scale, S(0.92f), S(1), 0.1f, 0.55f, UIAnimEase.OutSine),
                Chars(Go(info.itemName, UIAnimProperty.CharAlpha, A(0), A(1), 0.3f, 0.3f, UIAnimEase.InOutSine), 0.03f),
            })),
            State("Exit", Travel(0.05f, 0.4f).Concat(new[] { Fade(group, 1, 0, 0, 0.3f) })));
        return go;
    }

    // A summon card: Body turns over (Rotation Y), the root drops in and, for a 5★, shakes and bursts with light first.
    static RpgSummonCard SummonCard(Transform parent, string name, float x, float y)
    {
        var rt = Center(name, parent, x, y, 260, 360);
        var group = Group(rt);
        var card = rt.gameObject.AddComponent<RpgSummonCard>();
        var glow = Pic("Glow", rt, Fx("glow"), 0, 0, 640, 640, Purple);   // 4★
        var halo = Pic("Halo", rt, Fx("glow"), 0, 0, 640, 640, Legend);   // 5★
        var rays = Pic("Rays", rt, Fx("rays"), 0, 0, 760, 760, Legend);
        var body = Fill("Body", rt);
        var back = Img(Fill("Back", body), Fx("card_back"));
        var face = Fill("Face", body);
        card.portrait = Img(Fill("Portrait", face, 6), null);
        card.frame = Img(Fill("Frame", face), Frame("card"));
        var plate = Node("Plate", face, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 8), new Vector2(-16, 56), new Vector2(0.5f, 0));
        Img(plate, Frame("dark"));
        card.heroName = Text(Fill("Name", plate), "Hero", 36, Gold);
        card.stars = StarRow(face, 150, 30, 30);
        face.gameObject.SetActive(false);
        var anim = rt.gameObject.AddComponent<UIAnimation>();

        UIAnimTrack[] Turn(float at) => new[]
        {
            Keys(body, UIAnimProperty.Rotation, K(at, Rot(0, 0, 0)), K(at + 0.14f, Rot(0, 90, 0), UIAnimEase.InSine), K(at + 0.36f, Rot(0, 0, 0), UIAnimEase.OutCubic)),
            Keys(back.gameObject.transform, UIAnimProperty.Active, K(0, A(1)), K(at + 0.14f, A(0))),
            Keys(face, UIAnimProperty.Active, K(0, A(0)), K(at + 0.14f, A(1))),
        };
        anim.states.Add(State("Drop",
            Fade(group, 0, 1, 0, 0.15f, UIAnimEase.Linear),
            Go(rt, UIAnimProperty.AnchoredPosition, V(x, y + 260), V(x, y), 0, 0.4f, UIAnimEase.OutCubic),
            Go(rt, UIAnimProperty.Scale, S(1.25f), S(1), 0, 0.4f, UIAnimEase.OutCubic)));
        anim.states.Add(State("Flip", Turn(0)));
        anim.states.Add(State("FlipEpic", Turn(0.3f).Concat(new[]
        {
            Keys(glow, UIAnimProperty.GraphicAlpha, K(0, A(0)), K(0.3f, A(0.9f), UIAnimEase.OutSine), K(0.9f, A(0.45f), UIAnimEase.InOutSine)),
            Go(glow.rectTransform, UIAnimProperty.Scale, S(0.5f), S(1.1f), 0, 0.4f, UIAnimEase.OutSine),
        })));
        anim.states.Add(State("FlipLegend", Turn(0.75f).Concat(new[]
        {
            Keys(rt, UIAnimProperty.Rotation, K(0, Rot(0, 0, 0)), Vibrate(UIAnimCurveType.Shake, 0.7f, Rot(0, 0, 5), 18, 0.3f)),
            Keys(halo, UIAnimProperty.GraphicAlpha, K(0, A(0)), K(0.7f, A(1), UIAnimEase.InSine), K(1.6f, A(0.6f), UIAnimEase.InOutSine)),
            Go(halo.rectTransform, UIAnimProperty.Scale, S(0.4f), S(1.3f), 0, 0.8f, UIAnimEase.InSine),
            Keys(rays, UIAnimProperty.GraphicAlpha, K(0.7f, A(0)), K(0.85f, A(0.9f)), K(2.2f, A(0.5f), UIAnimEase.InOutSine)),
            Go(rays.rectTransform, UIAnimProperty.Rotation, Rot(0, 0, 0), Rot(0, 0, -60), 0.7f, 2.2f, UIAnimEase.Linear),
            Keys(rt, UIAnimProperty.Scale, K(1.1f, S(1)), Vibrate(UIAnimCurveType.Punch, 1.5f, S(0.18f, 0.18f), 5, 0.5f)),
        })));
        glow.color = new Color(glow.color.r, glow.color.g, glow.color.b, 0);
        halo.color = new Color(halo.color.r, halo.color.g, halo.color.b, 0);
        rays.color = new Color(rays.color.r, rays.color.g, rays.color.b, 0);
        group.alpha = 0;
        return card;
    }

    static GameObject SummonResult()
    {
        var (go, screen) = NewScreen("SummonResult");
        var result = go.AddComponent<RpgSummonResult>();
        var circle = Pic("Circle", go.transform, Fx("summon_circle"), 0, 0, 1000, 1000, new Color(0.95f, 0.8f, 1f, 0));
        var flash = Img(Fill("Flash", go.transform), null, new Color(1, 1, 1, 0));
        result.cards = Enumerable.Range(0, 10).Select(i => SummonCard(go.transform, "Card" + (i + 1), -640 + i % 5 * 320, i < 5 ? 200 : -200)).ToArray();
        result.single = SummonCard(go.transform, "SingleCard", 0, 0);
        result.ok = Btn(go.transform, "OkButton", "OK", 0, -450, 300, 100);
        var okGroup = Group(result.ok);
        okGroup.alpha = 0;
        States(go,
            State("Enter",
                Keys(flash, UIAnimProperty.GraphicAlpha, K(0, A(0)), K(0.15f, A(0.6f), UIAnimEase.OutSine), K(0.7f, A(0), UIAnimEase.InOutSine)),
                Keys(circle, UIAnimProperty.GraphicAlpha, K(0, A(0)), K(0.2f, A(0.9f)), K(1.1f, A(0), UIAnimEase.InOutSine)),
                Go(circle.rectTransform, UIAnimProperty.Scale, S(0.3f), S(1.5f), 0, 1.1f, UIAnimEase.OutCubic),
                Go(circle.rectTransform, UIAnimProperty.Rotation, Rot(0, 0, 0), Rot(0, 0, -200), 0, 1.1f, UIAnimEase.OutCubic)),
            State("Done", Fade(okGroup, 0, 1, 0, 0.4f), Move((RectTransform)result.ok.transform, new Vector2(0, -490), 0, 0.5f, UIAnimEase.OutBack)),
            State("Exit", Fade(screen.Group, 1, 0, 0, 0.35f)));
        return go;
    }

    static GameObject Victory()
    {
        var (go, screen) = NewScreen("Victory");
        var result = go.AddComponent<RpgResult>();
        result.victory = true;
        var rays = Pic("Rays", go.transform, Fx("rays"), 0, 230, 1000, 1000, new Color(1, 0.85f, 0.45f, 1));
        var trophy = Pic("Trophy", go.transform, Icon("trophy"), 0, 400, 180, 180);
        var banner = Center("Banner", go.transform, 0, 200, 1100, 190);
        Img(banner, Frame("dark"));
        var title = Text(Fill("Title", banner), "VICTORY", 130, Gold);
        var stars = new[] { (-180f, 30f, 130f), (0f, 50f, 160f), (180f, 30f, 130f) }.Select((s, i) =>
        {
            Pic("StarSlot" + i, go.transform, Icon("star"), s.Item1, s.Item2, s.Item3, s.Item3, new Color(0.2f, 0.15f, 0.1f, 0.8f));
            return Pic("Star" + i, go.transform, Icon("star"), s.Item1, s.Item2, s.Item3, s.Item3);
        }).ToArray();
        var rewards = Center("Rewards", go.transform, 0, -170, 900, 220);
        Img(rewards, Frame("panel"));
        Label(rewards, "Title", "REWARDS", 0, 70, 600, 50, 40, Ink);
        Pic("Gold", rewards, Icon("gold"), -280, -20, 100, 100);
        result.detail = Label(rewards, "GoldAmount", "+0", -150, -20, 200, 60, 54, Ink, TextAlignmentOptions.Left);
        Pic("Rune", rewards, Icon("rune"), 120, -20, 100, 100);
        Label(rewards, "RuneCount", "x2", 240, -20, 120, 60, 54, Ink, TextAlignmentOptions.Left);
        var exp = Center("Exp", go.transform, 0, -330, 760, 28);
        Img(exp, Frame("bar"), new Color(0, 0, 0, 0.6f));
        var fill = Node("Fill", exp, Vector2.zero, new Vector2(0.78f, 1), Vector2.zero, Vector2.zero, new Vector2(0, 0.5f));
        Img(fill, Frame("bar"), Gold);
        var expLabel = Label(go.transform, "ExpLabel", "Lv. 27  ·  EXP +120", 0, -290, 600, 44, 36, Light);
        result.primary = Btn(go.transform, "ContinueButton", "CONTINUE", 190, -440, 320, 100);
        result.lobby = Btn(go.transform, "LobbyButton", "LOBBY", -190, -440, 320, 100, gold: false);
        var buttons = new[] { Group(result.primary), Group(result.lobby) };

        var tracks = new List<UIAnimTrack>
        {
            Fade(rays, 0, 0.7f, 0, 0.8f),
            Go(rays.rectTransform, UIAnimProperty.Rotation, Rot(0, 0, 0), Rot(0, 0, -50), 0, 5f, UIAnimEase.Linear),
            Fade(Group(banner), 0, 1, 0.1f, 0.4f),
            Go(banner, UIAnimProperty.SizeDelta, V(300, 190), V(1100, 190), 0.1f, 0.6f, UIAnimEase.OutBack),
            Chars(Go(title, UIAnimProperty.CharOffset, V(0, 120), V(0, 0), 0.35f, 0.5f, UIAnimEase.OutBack), 0.06f),
            Chars(Go(title, UIAnimProperty.CharAlpha, A(0), A(1), 0.35f, 0.2f, UIAnimEase.Linear), 0.06f),
            Go(trophy.rectTransform, UIAnimProperty.Scale, S(0), S(1), 0.5f, 0.5f, UIAnimEase.OutBack),
            Fade(Group(rewards), 0, 1, 1.6f, 0.4f),
            Move(rewards, new Vector2(0, -230), 1.6f, 0.5f),
            Fade(expLabel, 0, 1, 1.9f, 0.3f),
            Fade(Group(exp), 0, 1, 1.9f, 0.3f),
            Go(fill, UIAnimProperty.Scale, S(0.6f, 1), S(1), 2.0f, 0.8f, UIAnimEase.OutCubic),
        };
        for (var i = 0; i < 3; i++)
        {
            var t = 0.9f + i * 0.25f;
            tracks.Add(Keys(stars[i], UIAnimProperty.GraphicAlpha, K(0, A(0)), K(t, A(0)), K(t + 0.05f, A(1))));
            tracks.Add(Keys(stars[i].rectTransform, UIAnimProperty.Scale, K(t, S(2.2f)), K(t + 0.2f, S(1), UIAnimEase.InQuad), Vibrate(UIAnimCurveType.Punch, t + 0.5f, S(0.25f, 0.25f), 5, 0.5f)));
        }
        foreach (var b in buttons) tracks.Add(Fade(b, 0, 1, 2.4f, 0.4f));
        States(go, State("Enter", tracks), State("Exit", Fade(screen.Group, 1, 0, 0, 0.35f)));
        return go;
    }

    static GameObject Defeat()
    {
        var (go, screen) = NewScreen("Defeat");
        var result = go.AddComponent<RpgResult>();
        var tint = Img(Fill("Tint", go.transform), null, new Color(0.35f, 0, 0, 0));
        var skull = Pic("Skull", go.transform, Icon("skull"), 0, 300, 220, 220);
        var title = Label(go.transform, "Title", "DEFEAT", 0, 110, 1000, 160, 140, new Color32(200, 190, 180, 255));
        result.detail = Label(go.transform, "Stage", "", 0, -10, 800, 60, 44, Dim);
        var tip = Label(go.transform, "Tip", "Tip: level up your heroes, or bring a Guardian against bosses.", 0, -150, 1300, 120, 40, Light);
        result.primary = Btn(go.transform, "RetryButton", "RETRY", 190, -400, 320, 100);
        result.lobby = Btn(go.transform, "LobbyButton", "LOBBY", -190, -400, 320, 100, gold: false);
        States(go,
            State("Enter",
                Fade(tint, 0, 1, 0, 0.8f),   // the alpha is the color's: 0 → 0.35 red
                Keys(skull, UIAnimProperty.GraphicAlpha, K(0, A(0)), K(0.3f, A(1), UIAnimEase.InOutSine)),
                Go(skull.rectTransform, UIAnimProperty.Scale, S(1.6f), S(1), 0, 0.35f, UIAnimEase.InQuad),
                Keys(skull.rectTransform, UIAnimProperty.Rotation, K(0.35f, Rot(0, 0, 0)), Vibrate(UIAnimCurveType.Shake, 0.85f, Rot(0, 0, 10), 22, 0.4f)),
                Chars(Go(title, UIAnimProperty.CharOffset, V(0, 90), V(0, 0), 0.5f, 0.6f, UIAnimEase.OutBounce), 0.08f),
                Chars(Go(title, UIAnimProperty.CharAlpha, A(0), A(1), 0.5f, 0.3f, UIAnimEase.Linear), 0.08f),
                Chars(Go(title, UIAnimProperty.CharRotation, Rot(0, 0, 25), Rot(0, 0, 0), 0.5f, 0.6f, UIAnimEase.OutBounce), 0.08f),
                Fade(result.detail, 0, 1, 1.0f, 0.4f),
                Go(tip, UIAnimProperty.TextReveal, A(0), A(1), 1.3f, 1.6f, UIAnimEase.Linear),
                Fade(Group(result.primary), 0, 1, 2.6f, 0.4f),
                Fade(Group(result.lobby), 0, 1, 2.6f, 0.4f)),
            State("Exit", Fade(screen.Group, 1, 0, 0, 0.35f)));
        tint.color = new Color(0.35f, 0, 0, 0.35f);
        return go;
    }

    // ---------- 2nd set: title, loading, upgrade, shop, formation, mail, purchase, attendance ----------

    static UIAnimTrack Blink(Component target, float low, float period) =>
        Keys(Group(target), UIAnimProperty.CanvasGroupAlpha, K(0, A(1)), K(period / 2, A(low), UIAnimEase.InOutSine), K(period, A(1), UIAnimEase.InOutSine));

    static UIAnimState Deny(Component target) =>
        State("Deny", Keys(target.transform, UIAnimProperty.Rotation, K(0, Rot(0, 0, 0)), Vibrate(UIAnimCurveType.Shake, 0.4f, Rot(0, 0, 6), 25, 0.6f)));

    // A page that fades in and out as a whole (title, loading).
    static UIAnimState[] FadePage(CanvasGroup content, IEnumerable<UIAnimTrack> enterExtra = null) => new[]
    {
        State("PushEnter", new[] { Fade(content, 0, 1, 0, 0.5f) }.Concat(enterExtra ?? Enumerable.Empty<UIAnimTrack>())),
        State("PushExit", Fade(content, 1, 0, 0, 0.4f)),
    };

    static GameObject Title()
    {
        var (go, _) = NewScreen("Title");
        var content = Content(go, "lobby");
        var group = content.GetComponent<CanvasGroup>();
        Img(Fill("Shade", content), null, new Color(0, 0, 0, 0.35f));
        var title = go.AddComponent<RpgTitle>();
        var glow = Pic("Glow", content, Fx("glow"), 0, 190, 1100, 700, new Color(1, 0.8f, 0.45f, 0.45f));
        var crown = Pic("Crown", content, Icon("crown"), 0, 330, 180, 180);
        var logo = Label(content, "Logo", "CHRONICLES OF AETHER", 0, 150, 1700, 200, 150, Gold);
        var subtitle = Label(content, "Subtitle", "A UI ANIMATION SAMPLE", 0, 30, 1000, 60, 44, Light);
        // The page fades the Tap node in; the label inside blinks on its own (another property owner, so neither stops the other).
        var tap = Center("Tap", content, 0, -330, 800, 80);
        var tapLabel = Text(Fill("TapLabel", tap), "TAP TO START", 60, Light);
        tapLabel.gameObject.AddComponent<UIAnimation>().states.Add(State("Blink", Blink(tapLabel, 0.25f, 1.6f)));
        Loop(tapLabel, "Blink", playOnEnable: true);
        glow.gameObject.AddComponent<UIAnimation>().states.Add(State("Pulse",
            Keys(glow, UIAnimProperty.GraphicAlpha, K(0, A(0.3f)), K(1.5f, A(0.6f), UIAnimEase.InOutSine), K(3f, A(0.3f), UIAnimEase.InOutSine))));
        Loop(glow, "Pulse", playOnEnable: true);
        var button = Fill("TapButton", content);
        Img(button, null, new Color(0, 0, 0, 0), raycast: true);
        title.tap = button.gameObject.AddComponent<Button>();
        title.tap.transition = Selectable.Transition.None;
        States(go, FadePage(group, new[]
        {
            Go(crown.rectTransform, UIAnimProperty.Scale, S(0), S(1), 0.2f, 0.5f, UIAnimEase.OutBack),
            Chars(Go(logo, UIAnimProperty.CharOffset, V(0, 160), V(0, 0), 0.3f, 0.6f, UIAnimEase.OutBounce), 0.04f),
            Chars(Go(logo, UIAnimProperty.CharAlpha, A(0), A(1), 0.3f, 0.2f, UIAnimEase.Linear), 0.04f),
            Fade(subtitle, 0, 1, 1.3f, 0.5f),
            Fade(tap, 0, 1, 1.6f, 0.4f),
        }));
        return go;
    }

    static GameObject Loading()
    {
        var (go, _) = NewScreen("Loading");
        var content = Content(go, "hall");
        Img(Fill("Shade", content), null, new Color(0, 0, 0, 0.55f));
        var loading = go.AddComponent<RpgLoading>();
        var rune = Pic("Rune", content, Fx("summon_circle"), 0, 80, 360, 360, new Color(1, 0.85f, 0.5f, 0.8f));
        rune.gameObject.AddComponent<UIAnimation>().states.Add(State("Spin", Go(rune.rectTransform, UIAnimProperty.Rotation, Rot(0, 0, 0), Rot(0, 0, -360), 0, 4, UIAnimEase.Linear)));
        Loop(rune, "Spin", playOnEnable: true);
        Label(content, "Title", "LOADING", 0, -170, 600, 70, 56, Gold);
        var bar = Center("Bar", content, 0, -260, 1000, 30);
        Img(bar, Frame("bar"), new Color(0, 0, 0, 0.6f));
        loading.fill = Node("Fill", bar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0, 0.5f));
        Img(loading.fill, Frame("bar"), Gold);
        loading.fill.localScale = new Vector3(0, 1, 1);
        loading.percent = Label(content, "Percent", "0%", 0, -320, 300, 60, 44, Light);
        loading.tip = Label(content, "Tip", "", 0, -420, 1400, 60, 38, Dim);
        States(go, FadePage(content.GetComponent<CanvasGroup>()).Append(State("Progress",
            Keys(loading.fill, UIAnimProperty.Scale, K(0, S(0, 1)), K(0.5f, S(0.35f, 1), UIAnimEase.OutSine), K(1.1f, S(0.55f, 1), UIAnimEase.InOutSine),
                K(1.8f, S(1), UIAnimEase.InOutSine)))).ToArray());
        return go;
    }

    static GameObject Upgrade()
    {
        var (go, screen) = NewScreen("Upgrade");
        var content = Content(go, "hall");
        var header = Header(content, "UPGRADE", RpgCurrency.Kind.Gold, RpgCurrency.Kind.Gems);
        var page = go.AddComponent<RpgUpgradePage>();

        var card = Center("Card", content, -500, -50, 440, 572);
        var glow = Pic("Glow", card, Fx("glow"), 0, 0, 900, 900, new Color(1, 0.85f, 0.4f, 0));
        page.portrait = Img(Fill("Portrait", card), null);
        Img(Fill("Frame", card, -10), Frame("card"), Legend);

        var panel = Center("Panel", content, 330, -50, 960, 800);
        Img(panel, Frame("dark"));
        page.heroName = Label(panel, "Name", "Hero", -150, 330, 600, 90, 80, Gold, TextAlignmentOptions.Left);
        page.level = Label(panel, "Level", "Lv. 1", 300, 330, 300, 90, 70, Light, TextAlignmentOptions.Right);
        page.stats = RpgData.Stats.Select((s, i) =>
        {
            var y = 220 - i * 70;
            Pic("StatIcon" + i, panel, Icon(RpgData.StatIcons[i]), -400, y, 56, 56);
            Label(panel, "StatName" + i, s.name, -270, y, 160, 56, 40, Dim, TextAlignmentOptions.Left);
            return Label(panel, "Stat" + i, "0", 0, y, 300, 56, 44, Light, TextAlignmentOptions.Right);
        }).ToArray();
        Label(panel, "PowerTitle", "POWER", 260, 150, 300, 50, 36, Dim);
        page.power = Label(panel, "Power", "0", 260, 90, 360, 80, 72, Gold);

        Label(panel, "GearTitle", "EQUIPMENT", -270, -60, 300, 50, 40, Dim, TextAlignmentOptions.Left);
        page.slotIcons = Enumerable.Range(0, 3).Select(i =>
        {
            var slot = Center("GearSlot" + i, panel, -310 + i * 190, -170, 160, 160);
            Img(slot, Frame("slot"), Gold);
            var ring = Pic("Ring", slot, Fx("glow"), 0, 0, 340, 340, new Color(1, 0.85f, 0.4f, 0));
            var icon = Pic("Icon", slot, null, 0, 0, 130, 130);
            icon.color = new Color(1, 1, 1, 0);
            slot.gameObject.AddComponent<UIAnimation>().states.Add(State("Equip",
                Keys(icon, UIAnimProperty.GraphicAlpha, K(0, A(0)), K(0.06f, A(1))),
                Go(icon.rectTransform, UIAnimProperty.Scale, S(2.2f), S(1), 0, 0.22f, UIAnimEase.InQuad),
                Keys(ring, UIAnimProperty.GraphicAlpha, K(0, A(0)), K(0.22f, A(0.9f)), K(0.8f, A(0), UIAnimEase.InOutSine)),
                Keys(slot, UIAnimProperty.Scale, K(0.22f, S(1)), Vibrate(UIAnimCurveType.Punch, 0.6f, S(0.12f, 0.12f), 5, 0.5f))));
            return icon;
        }).ToArray();
        page.autoEquip = Btn(panel, "AutoEquipButton", "AUTO EQUIP", 290, -170, 300, 96, gold: false);

        page.upgrade = Btn(panel, "UpgradeButton", null, 220, -320, 420, 120);
        Label(page.upgrade.transform, "Label", "UPGRADE", 0, 18, 400, 60, 52, Ink);
        Pic("Coin", page.upgrade.transform, Icon("gold"), -60, -30, 44, 44);
        page.cost = Label(page.upgrade.transform, "Cost", "0", 40, -30, 200, 44, 36, Ink, TextAlignmentOptions.Left);
        page.upgrade.GetComponent<UIAnimation>().states.Add(Deny(page.upgrade));
        Label(panel, "Hint", "Each level costs more gold.", -200, -320, 420, 50, 32, Dim, TextAlignmentOptions.Left);

        var flash = Img(Fill("Flash", content), null, new Color(1, 0.95f, 0.8f, 0));
        var levelUp = new List<UIAnimTrack>
        {
            Keys(flash, UIAnimProperty.GraphicAlpha, K(0, A(0)), K(0.06f, A(0.3f)), K(0.3f, A(0), UIAnimEase.OutSine)),
            Keys(glow, UIAnimProperty.GraphicAlpha, K(0, A(0)), K(0.15f, A(0.8f)), K(1f, A(0), UIAnimEase.InOutSine)),
            Keys(page.level.rectTransform, UIAnimProperty.Scale, K(0, S(1)), Vibrate(UIAnimCurveType.Punch, 0.6f, S(0.35f, 0.35f), 5, 0.5f)),
            Keys(card, UIAnimProperty.Scale, K(0, S(1)), Vibrate(UIAnimCurveType.Punch, 0.5f, S(0.06f, 0.06f), 4, 0.5f)),
        };
        levelUp.AddRange(page.stats.Select((t, i) => Keys(t, UIAnimProperty.GraphicColor, K(0, Gold), K(0.1f + i * 0.08f, Gold), K(0.6f + i * 0.08f, (Vector4)Light, UIAnimEase.InOutSine))));
        States(go, PageStates(screen, content.GetComponent<CanvasGroup>(), header, new[]
        {
            Move(card, new Vector2(-900, -50), 0.1f, 0.6f),
            Move(panel, new Vector2(800, -50), 0.15f, 0.6f),
        }).Append(State("LevelUp", levelUp)).ToArray());
        return go;
    }

    static GameObject ShopCardItem()
    {
        var (root, body) = ItemRoot("ShopCard", 320, 300);
        Img(body, Frame("dark"), raycast: true);
        var card = root.AddComponent<RpgShopCard>();
        card.buy = body.gameObject.AddComponent<Button>();
        card.buy.transition = Selectable.Transition.None;
        Pressable(body);
        Pic("Glow", body, Fx("glow"), 0, 40, 300, 300, new Color(1, 0.85f, 0.45f, 0.35f));
        card.icon = Pic("Icon", body, null, 0, 40, 140, 140);
        card.title = Label(body, "Name", "Offer", 0, -60, 300, 44, 36, Gold);
        var price = Center("Price", body, 0, -110, 260, 56);
        Img(price, Frame("pill"));
        card.currency = Pic("Currency", price, null, -80, 2, 50, 50);
        card.price = Label(price, "Value", "0", 30, 0, 160, 50, 38, Light);
        card.stock = Label(body, "Stock", "", 90, 125, 140, 36, 28, Dim, TextAlignmentOptions.Right);
        var stamp = Label(body, "Stamp", "SOLD OUT", 0, 30, 300, 90, 64, new Color32(220, 70, 50, 255));
        stamp.rectTransform.localEulerAngles = new Vector3(0, 0, -14);
        States(root,
            State("Show", Fade(Group(body), 0, 1, 0, 0.25f), Go(body, UIAnimProperty.Scale, S(0.8f), S(1), 0, 0.4f, UIAnimEase.OutBack)),
            State("Bought", Keys(body, UIAnimProperty.Scale, K(0, S(1)), Vibrate(UIAnimCurveType.Punch, 0.5f, S(0.1f, 0.1f), 5, 0.5f))),
            State("SoldOut",
                Keys(stamp, UIAnimProperty.GraphicAlpha, K(0, A(0)), K(0.08f, A(1))),
                Go(stamp.rectTransform, UIAnimProperty.Scale, S(2.4f), S(1), 0, 0.22f, UIAnimEase.InQuad),
                Go(card.icon, UIAnimProperty.GraphicAlpha, A(1), A(0.35f), 0.25f, 0.3f, UIAnimEase.InOutSine)));
        stamp.color = new Color(stamp.color.r, stamp.color.g, stamp.color.b, 0);
        return root;
    }

    static GameObject Shop(RectTransform shopCard)
    {
        var (go, screen) = NewScreen("Shop");
        var content = Content(go, "hall");
        var header = Header(content, "SHOP", RpgCurrency.Kind.Gold, RpgCurrency.Kind.Gems);
        var page = go.AddComponent<RpgShopPage>();

        var deal = Center("Deal", content, 0, 300, 1500, 170);
        Img(deal, Frame("dark"));
        deal.gameObject.AddComponent<RectMask2D>();
        var shine = Pic("Shine", deal, Fx("glow"), -900, 0, 300, 420, new Color(1, 0.95f, 0.7f, 0.5f));
        shine.gameObject.AddComponent<UIAnimation>().states.Add(State("Sweep",
            Keys(shine.rectTransform, UIAnimProperty.AnchoredPosition, K(0, V(-900, 0)), K(1.4f, V(900, 0), UIAnimEase.InOutSine), K(3f, V(900, 0)))));
        Loop(shine, "Sweep", playOnEnable: true);
        Pic("Chest", deal, Icon("chest"), -620, 0, 130, 130);
        Label(deal, "Title", "DAILY DEAL", -200, 30, 600, 70, 60, Gold, TextAlignmentOptions.Left);
        Label(deal, "Text", "New offers every day at midnight", -200, -32, 700, 50, 34, Light, TextAlignmentOptions.Left);
        Pic("Clock", deal, Icon("hourglass"), 420, 0, 70, 70);
        page.countdown = Label(deal, "Countdown", "00:00:00", 600, 0, 280, 70, 54, Light, TextAlignmentOptions.Left);

        var list = ScrollList("List", Stretch("List", content, 160, 160, 410, 20), shopCard);
        list.layout = UIAnimScrollList.LayoutType.Grid;
        list.cellSize = new Vector2(320, 300);
        list.gridSpacing = new Vector2(40, 30);
        list.padding = new RectOffset(10, 10, 10, 10);
        list.childAlignment = TextAnchor.UpperCenter;
        list.entryState = "Show";
        list.entryStagger = 0.05f;
        list.appearState = "Show";
        page.list = list;
        States(go, PageStates(screen, content.GetComponent<CanvasGroup>(), header, new[]
        {
            Fade(deal, 0, 1, 0.1f, 0.4f),
            Move(deal, new Vector2(0, 420), 0.1f, 0.5f, UIAnimEase.OutBack),
        }));
        return go;
    }

    static GameObject Purchase()
    {
        var (go, _) = NewScreen("Purchase");
        var p = go.AddComponent<RpgPurchase>();
        var panel = Center("Panel", go.transform, 0, 0, 900, 560);
        Img(panel, Frame("panel"));
        var group = Group(panel);
        p.title = Label(panel, "Name", "Offer", 0, 205, 800, 80, 64, Ink);
        Pic("Glow", panel, Fx("glow"), 0, 60, 420, 420, new Color(1, 0.85f, 0.45f, 0.5f));
        p.icon = Pic("Icon", panel, null, 0, 60, 180, 180);
        p.gives = Label(panel, "Gives", "", 0, -60, 800, 50, 38, new Color32(140, 100, 60, 255));
        var price = Center("Price", panel, 0, -125, 300, 64);
        Img(price, Frame("pill"));
        p.currency = Pic("Currency", price, null, -100, 2, 56, 56);
        p.price = Label(price, "Value", "0", 30, 0, 200, 56, 42, Light);
        p.buy = Btn(panel, "BuyButton", "BUY", 160, -215, 280, 96);
        p.cancel = Btn(panel, "CancelButton", "CANCEL", -160, -215, 280, 96, gold: false);
        States(go,
            State("Enter", Fade(group, 0, 1, 0, 0.25f), Go(panel, UIAnimProperty.Scale, S(0.85f), S(1), 0, 0.4f, UIAnimEase.OutBack),
                Go(p.icon.rectTransform, UIAnimProperty.Scale, S(0), S(1), 0.15f, 0.45f, UIAnimEase.OutBack)),
            State("Exit", Fade(group, 1, 0, 0, 0.25f), Go(panel, UIAnimProperty.Scale, S(1), S(0.92f), 0, 0.25f, UIAnimEase.InSine)),
            State("Deny",
                Keys(price, UIAnimProperty.AnchoredPosition, K(0, V(0, -125)), Vibrate(UIAnimCurveType.Shake, 0.4f, V(14, 0), 25, 0.6f)),
                Keys(p.price, UIAnimProperty.GraphicColor, K(0, (Vector4)(Color)new Color32(255, 90, 70, 255)), K(0.6f, (Vector4)Light, UIAnimEase.InOutSine))));
        return go;
    }

    static GameObject Formation()
    {
        var (go, screen) = NewScreen("Formation");
        var content = Content(go, "lobby");
        Img(Fill("Shade", content), null, new Color(0, 0, 0, 0.5f));
        var header = Header(content, "FORMATION", RpgCurrency.Kind.Stamina, RpgCurrency.Kind.Gold);
        var f = go.AddComponent<RpgFormation>();
        f.stage = Label(content, "Stage", "STAGE 1-1", 0, 330, 900, 70, 60, Gold);
        Label(content, "Hint", "Tap a hero to add them, tap a slot to take them out", 0, 270, 1200, 50, 34, Dim);

        var slots = new RectTransform[5];
        f.slotPortraits = new Image[5];
        f.slots = new Button[5];
        for (var i = 0; i < 5; i++)
        {
            var slot = Center("Slot" + i, content, -560 + i * 280, 60, 230, 300);
            slots[i] = slot;
            Img(slot, Frame("dark"), raycast: true);
            f.slots[i] = slot.gameObject.AddComponent<Button>();
            f.slots[i].transition = Selectable.Transition.None;
            Label(slot, "Empty", "+", 0, 0, 200, 120, 110, new Color(1, 1, 1, 0.15f));
            var portrait = Fill("Portrait", slot, 10);
            f.slotPortraits[i] = Img(portrait, null);
            var frame = Img(Fill("Frame", slot, -4), Frame("card"), Gold);
            var anim = slot.gameObject.AddComponent<UIAnimation>();
            anim.states.Add(State("In",
                Go(Group(portrait), UIAnimProperty.CanvasGroupAlpha, A(0), A(1), 0, 0.12f, UIAnimEase.Linear),
                Go(portrait, UIAnimProperty.AnchoredPosition, V(0, 180), V(0, 0), 0, 0.4f, UIAnimEase.OutBounce),
                Go(frame, UIAnimProperty.GraphicAlpha, A(0), A(1), 0.2f, 0.2f, UIAnimEase.Linear)));
            Group(portrait).alpha = 0;
            frame.color = new Color(frame.color.r, frame.color.g, frame.color.b, 0);
        }

        f.roster = Enumerable.Range(0, RpgData.Heroes.Length).Select(i =>
        {
            var b = Btn(content, "Hero" + i, null, -575 + i * 230, -240, 190, 190, gold: false);
            Img(Fill("Portrait", b.transform, 14), null);
            Group(b);
            return b;
        }).ToArray();
        var powerBox = Center("PowerBox", content, -520, -440, 480, 100);
        Img(powerBox, Frame("dark"));
        Label(powerBox, "Title", "POWER", -140, 0, 160, 60, 38, Dim);
        f.power = Label(powerBox, "Value", "0", 70, 0, 280, 80, 64, Gold, TextAlignmentOptions.Left);
        f.battle = Btn(content, "BattleButton", "BATTLE", 560, -440, 360, 120);
        f.battle.GetComponent<UIAnimation>().states.Add(Deny(f.battle));

        var flash = Img(Fill("Flash", content), null, new Color(1, 1, 1, 0));
        var battle = new List<UIAnimTrack>
        {
            Keys(flash, UIAnimProperty.GraphicAlpha, K(0.45f, A(0)), K(0.75f, A(0.9f), UIAnimEase.InSine), K(1.1f, A(0), UIAnimEase.OutSine)),
        };
        for (var i = 0; i < 5; i++)
        {
            var at = slots[i].anchoredPosition;
            battle.Add(Keys(slots[i], UIAnimProperty.AnchoredPosition, K(0, (Vector4)at), K(i * 0.06f, (Vector4)at),
                K(i * 0.06f + 0.12f, V(at.x - 40, at.y), UIAnimEase.OutSine), K(i * 0.06f + 0.5f, V(at.x + 1500, at.y), UIAnimEase.InCubic)));
        }
        States(go, PageStates(screen, content.GetComponent<CanvasGroup>(), header,
                slots.Select((s, i) => Go(s, UIAnimProperty.Scale, S(0.6f), S(1), 0.1f + i * 0.06f, 0.4f, UIAnimEase.OutBack)))
            .Append(State("Battle", battle)).ToArray());
        return go;
    }

    static GameObject MailRowItem()
    {
        var (root, body) = ItemRoot("MailRow", 1500, 140);
        root.AddComponent<RectMask2D>();   // folding (ResizeItem) clips it
        var frame = Stretch("Frame", body, 0, 0, 6, 6);
        Img(frame, Frame("dark"));
        var row = root.AddComponent<RpgMailRow>();
        Img(Node("Letter", body, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(80, 0), new Vector2(100, 100)), Frame("slot"), Gold);
        Img(Node("LetterIcon", body, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(80, 0), new Vector2(76, 76)), Icon("scroll"));
        row.from = Text(Node("From", body, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(160, 26), new Vector2(700, 44), new Vector2(0, 0.5f)), "From", 32, Dim, TextAlignmentOptions.Left);
        row.title = Text(Node("Title", body, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(160, -18), new Vector2(800, 54), new Vector2(0, 0.5f)), "Title", 44, Light, TextAlignmentOptions.Left);
        var reward = Node("Reward", body, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-470, 0), new Vector2(220, 100));
        row.rewardIcon = Pic("Icon", reward, null, -60, 0, 80, 80);
        row.amount = Label(reward, "Amount", "0", 50, 0, 140, 50, 40, Gold, TextAlignmentOptions.Left);
        row.claim = Btn(body, "ClaimButton", "CLAIM", 0, 0, 220, 84);
        var claimRt = (RectTransform)row.claim.transform;
        claimRt.anchorMin = claimRt.anchorMax = new Vector2(1, 0.5f);
        claimRt.anchoredPosition = new Vector2(-150, 0);
        States(root,
            State("Show", Fade(Group(body), 0, 1, 0, 0.3f), Go(body, UIAnimProperty.AnchoredPosition, V(80, 0), V(0, 0), 0, 0.45f, UIAnimEase.OutCubic)),
            State("Claim",
                Go(reward, UIAnimProperty.AnchoredPosition, V(-470, 0), V(-470, 90), 0, 0.35f, UIAnimEase.OutCubic),
                Go(Group(reward), UIAnimProperty.CanvasGroupAlpha, A(1), A(0), 0.1f, 0.25f, UIAnimEase.Linear),
                Go(Group(frame), UIAnimProperty.CanvasGroupAlpha, A(1), A(0.5f), 0.1f, 0.25f, UIAnimEase.InOutSine)));
        return root;
    }

    static GameObject Mail(RectTransform mailRow)
    {
        var (go, screen) = NewScreen("Mail");
        var content = Content(go, "hall");
        var header = Header(content, "MAIL", RpgCurrency.Kind.Stamina, RpgCurrency.Kind.Gold, RpgCurrency.Kind.Gems);
        var page = go.AddComponent<RpgMailPage>();
        page.count = Label(content, "Count", "0 LETTERS", -500, 345, 600, 60, 44, Light, TextAlignmentOptions.Left);
        page.claimAll = Btn(content, "ClaimAllButton", "CLAIM ALL", 600, 345, 340, 90);
        var list = ScrollList("List", Stretch("List", content, 160, 160, 240, 20), mailRow);
        list.layout = UIAnimScrollList.LayoutType.Vertical;
        list.controlChildSize = true;
        list.spacing = 0;
        list.padding = new RectOffset(0, 0, 10, 30);
        list.entryState = "Show";
        list.entryStagger = 0.05f;
        list.appearState = "Show";
        page.list = list;
        var empty = Label(content, "Empty", "No letters. The guild will write soon.", 0, 0, 1200, 70, 48, Dim);
        Group(empty).alpha = 0;
        States(go, PageStates(screen, content.GetComponent<CanvasGroup>(), header).Append(State("Empty",
            Fade(empty, 0, 1, 0.1f, 0.5f), Go(empty.rectTransform, UIAnimProperty.Scale, S(0.9f), S(1), 0.1f, 0.5f, UIAnimEase.OutBack))).ToArray());
        return go;
    }

    static GameObject Attendance()
    {
        var (go, _) = NewScreen("Attendance");
        var a = go.AddComponent<RpgAttendance>();
        var panel = Center("Panel", go.transform, 0, 0, 1500, 720);
        Img(panel, Frame("panel"));
        var group = Group(panel);
        Label(panel, "Title", "DAILY ATTENDANCE", 0, 290, 1000, 90, 72, Ink);
        Label(panel, "Text", "Log in every day. Day 7 is a treasure chest!", 0, 220, 1100, 50, 38, new Color32(140, 100, 60, 255));
        a.close = Btn(panel, "CloseButton", "X", 700, 310, 84, 84, gold: false);
        a.days = new UIAnimation[7];
        a.rewardIcons = new Image[7];
        a.amounts = new TMP_Text[7];
        var cells = new RectTransform[7];
        for (var i = 0; i < 7; i++)
        {
            var big = i == 6;
            var cell = Center("Day" + (i + 1), panel, -600 + i * 200, 10, 180, big ? 260 : 230);
            cells[i] = cell;
            var glow = Pic("Glow", cell, Fx("glow"), 0, 0, 380, 420, new Color(1, 0.85f, 0.4f, 0));
            var body = Fill("Body", cell);
            Img(body, Frame("dark"));
            Label(body, "Day", "DAY " + (i + 1), 0, big ? 100 : 85, 170, 44, 34, Gold);
            a.rewardIcons[i] = Pic("Icon", body, null, 0, 0, big ? 130 : 100, big ? 130 : 100);
            a.amounts[i] = Label(body, "Amount", "0", 0, big ? -95 : -80, 170, 44, 36, Light);
            var stamp = Label(cell, "Stamp", "DONE", 0, 0, 200, 80, 56, new Color32(220, 70, 50, 255));
            stamp.rectTransform.localEulerAngles = new Vector3(0, 0, -16);
            var anim = cell.gameObject.AddComponent<UIAnimation>();
            anim.states.Add(State("Stamp",
                Keys(stamp, UIAnimProperty.GraphicAlpha, K(0, A(0)), K(0.08f, A(1))),
                Go(stamp.rectTransform, UIAnimProperty.Scale, S(2.4f), S(1), 0, 0.22f, UIAnimEase.InQuad),
                Keys(body, UIAnimProperty.AnchoredPosition, K(0.22f, V(0, 0)), Vibrate(UIAnimCurveType.Shake, 0.42f, V(8, 4), 20, 0.6f)),
                Go(Group(body), UIAnimProperty.CanvasGroupAlpha, A(1), A(0.55f), 0.3f, 0.3f, UIAnimEase.InOutSine)));
            anim.states.Add(State("Today",
                Keys(glow, UIAnimProperty.GraphicAlpha, K(0, A(0.25f)), K(0.7f, A(0.8f), UIAnimEase.InOutSine), K(1.4f, A(0.25f), UIAnimEase.InOutSine)),
                Keys(body, UIAnimProperty.Scale, K(0, S(1)), K(0.7f, S(1.06f), UIAnimEase.InOutSine), K(1.4f, S(1), UIAnimEase.InOutSine))));
            Loop(cell, "Today", playOnEnable: false);   // RpgAttendance plays it on today's cell
            stamp.color = new Color(stamp.color.r, stamp.color.g, stamp.color.b, 0);
            a.days[i] = anim;
        }
        a.claim = Btn(panel, "ClaimButton", "CLAIM", 0, -270, 340, 100);
        var enter = new List<UIAnimTrack> { Fade(group, 0, 1, 0, 0.25f), Go(panel, UIAnimProperty.Scale, S(0.88f), S(1), 0, 0.4f, UIAnimEase.OutBack) };
        enter.AddRange(cells.Select((c, i) => Go(c, UIAnimProperty.Scale, S(0), S(1), 0.15f + i * 0.05f, 0.35f, UIAnimEase.OutBack)));
        States(go, State("Enter", enter), State("Exit", Fade(group, 1, 0, 0, 0.25f), Go(panel, UIAnimProperty.Scale, S(1), S(0.92f), 0, 0.25f, UIAnimEase.InSine)));
        return go;
    }

    // ---------- scene ----------

    static string BuildScene(Dictionary<string, UIAnimScreen> screens)
    {
        var path = Root + "Scenes/RPG.unity";
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

        var demo = new GameObject("RpgDemo").AddComponent<RpgDemo>();
        demo.pages = Fill("Pages", Canvas("PageCanvas", 0)).gameObject.AddComponent<UIAnimPageContainer>();
        demo.modals = Fill("Modals", Canvas("ModalCanvas", 10)).gameObject.AddComponent<UIAnimModalContainer>();
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        foreach (var field in typeof(RpgDemo).GetFields().Where(f => f.FieldType == typeof(UIAnimScreen)))
            field.SetValue(demo, screens[field.Name]);
        demo.sprites = AssetDatabase.FindAssets("t:Sprite", new[] { Root + "Art/Icons", Root + "Art/Heroes" })
            .Select(g => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        EditorSceneManager.SaveScene(scene, path);
        return path;
    }
}
