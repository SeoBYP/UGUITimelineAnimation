using System.Collections.Generic;
using SeoBYP.TimelineAnimation;
using TMPro;
using TMPro.EditorUtilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Dev-only: rebuilds sample content from annulusgames/UGUIAnimationSamples (2_Button, 3_Text, 4_Toggle)
// with UIAnimation instead of hand-written LitMotion code. Not part of the UIAnimation package.
static class SampleContentBuilder
{
    const string FontPath = "Assets/Fonts/Montserrat/Montserrat-SemiBold SDF.asset";
    const string Button1Path = "Assets/Prefabs/Buttons/Button1.prefab";
    const string Button2Path = "Assets/Prefabs/Buttons/Button2.prefab";
    const string ButtonsScene = "Assets/Scenes/Buttons.unity";
    const string Toggle1Path = "Assets/Prefabs/Toggles/Toggle1.prefab";
    const string Toggle2Path = "Assets/Prefabs/Toggles/Toggle2.prefab";
    const string TogglesScene = "Assets/Scenes/Toggles.unity";
    const string TextDemoPath = "Assets/Prefabs/Text/TextDemo.prefab";
    const string TextScene = "Assets/Scenes/Text.unity";
    const string Popup1Path = "Assets/Prefabs/Popup/Popup1.prefab";
    const string HoverScene = "Assets/Scenes/Hover.unity";
    static string HoverPath(int n) => $"Assets/Prefabs/Hover/Hover{n}.prefab";
    const string PopupScene = "Assets/Scenes/Popup.unity";
    const string ScrollItemPath = "Assets/Prefabs/Scroll/Items/ScrollItem.prefab";
    const string ScrollCardPath = "Assets/Prefabs/Scroll/Items/ScrollCard.prefab";
    const string ScrollScene = "Assets/Scenes/Scroll.unity";

    static readonly Color Red = new(1f, 0.3254902f, 0.3254902f, 1f);
    static Color Clear(Color c) => new(c.r, c.g, c.b, 0f);

    [InitializeOnLoadMethod]
    static void AutoBuildOnce()
    {
        if (!System.IO.File.Exists(Button1Path) || IsOldFormat(Button1Path)) EditorApplication.delayCall += BuildButtons;
        if (!System.IO.File.Exists(Toggle1Path)) EditorApplication.delayCall += BuildToggles;
        // Rebuild when missing or from before the Scramble/Punch/Shake states existed.
        if (!System.IO.File.Exists(TextDemoPath) || !System.IO.File.ReadAllText(TextDemoPath).Contains("name: Scramble"))
            EditorApplication.delayCall += BuildText;
        if (!System.IO.File.Exists(Popup1Path)) EditorApplication.delayCall += BuildPopup;
        if (!System.IO.File.Exists(HoverPath(1))) EditorApplication.delayCall += BuildHovers;
        if (!System.IO.File.Exists(ScrollPages + "01 Vertical.prefab")) EditorApplication.delayCall += BuildScroll;
    }

    // Prefabs saved before the keyframe model serialized clips instead of keys. Checked on the file,
    // so a track the user emptied on purpose never triggers a rebuild.
    static bool IsOldFormat(string path) => System.IO.File.ReadAllText(path).Contains("      clips:");

    // TMP needs its essential resources before any text can be created; the build resumes after the import.
    static bool EnsureTmp(EditorApplication.CallbackFunction resume)
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0) return true;
        Debug.Log("[SampleContentBuilder] Importing TMP Essential Resources first; the build continues after import.");
        void OnImported(string _)
        {
            AssetDatabase.importPackageCompleted -= OnImported;
            EditorApplication.delayCall += resume;
        }
        AssetDatabase.importPackageCompleted += OnImported;
        TMP_PackageResourceImporter.ImportResources(true, false, false);
        return false;
    }

    static Sprite Sprite(string file) => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Textures/{file}.png");

    // ---------- 2_Button ----------

    [MenuItem("Tools/UIAnimation Dev/Rebuild Sample Buttons")]
    static void BuildButtons()
    {
        if (!EnsureTmp(BuildButtons)) return;
        var font = LoadOrCreateFont();
        var button1 = SavePrefab(BuildButton1(font, Sprite("Circle"), Sprite("Circle Frame")), Button1Path);
        var button2 = SavePrefab(BuildButton2(font, Sprite("Circle"), Sprite("Circle Frame")), Button2Path);
        var scene = BuildSceneIfMissing(ButtonsScene, font, "- Button -", (button1, -288), (button2, 288));
        Debug.Log($"[SampleContentBuilder] Built {Button1Path}, {Button2Path}{scene}");
    }

    // Sample Button_1 = Hover1 (fill/label color) + Button1 (press shrinks size and font).
    static GameObject BuildButton1(TMP_FontAsset font, Sprite circle, Sprite circleFrame)
    {
        var root = Rect("Button1", null);
        root.sizeDelta = new Vector2(480, 168);
        var fill = Img("Fill", root, circle, Clear(Red), Image.Type.Sliced, 4.17f);
        var label = Label("Label", root, font, "Button 1", Red, 58);
        Img("Frame", root, circleFrame, Red, Image.Type.Sliced, 4.17f);

        var anim = AddComponents<Button>(root.gameObject);
        anim.States(
            State("Hover", To(fill, UIAnimProperty.GraphicColor, Red, 0.2f, UIAnimEase.OutSine),
                           To(label, UIAnimProperty.GraphicColor, Color.white, 0.2f, UIAnimEase.OutSine)),
            State("Unhover", BackToRest(fill, UIAnimProperty.GraphicColor, 0.2f, UIAnimEase.OutSine),
                             BackToRest(label, UIAnimProperty.GraphicColor, 0.2f, UIAnimEase.OutSine)),
            State("Press", Relative(root, UIAnimProperty.SizeDelta, new Vector4(-34, -17), 0.07f, UIAnimEase.OutQuad),
                           Relative(label, UIAnimProperty.FontSize, new Vector4(-2.4f, 0), 0.07f, UIAnimEase.OutQuad)),
            State("Release", BackToRest(root, UIAnimProperty.SizeDelta, 0.07f, UIAnimEase.OutQuad),
                             BackToRest(label, UIAnimProperty.FontSize, 0.07f, UIAnimEase.OutQuad)));
        BindButton(root.gameObject, UIAnimInterrupt.Cancel, UIAnimInterrupt.Cancel);
        return root.gameObject;
    }

    // Sample Button_2: hover overlay fades in; press spawns a circle at the pointer that grows, release fades it.
    static GameObject BuildButton2(TMP_FontAsset font, Sprite circle, Sprite circleFrame)
    {
        var root = Rect("Button2", null);
        root.sizeDelta = new Vector2(480, 168);

        var mask = Img("Mask", root, circle, Color.white, Image.Type.Sliced, 3.33f);
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var hover = Img("Hover", mask.rectTransform, null, new Color(0.20784315f, 0.1254902f, 0.2f, 0f), Image.Type.Simple, 1f);
        var fill = Img("Fill", mask.rectTransform, circle, Red, Image.Type.Simple, 1f);
        fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        fill.rectTransform.sizeDelta = new Vector2(1080, 1080);
        fill.rectTransform.localScale = Vector3.zero;
        Label("Label", root, font, "Button 2", Color.white, 58);
        Img("Frame", root, circleFrame, Red, Image.Type.Sliced, 3.33f);

        var anim = AddComponents<Button>(root.gameObject);
        var press = State("Press",
            // The fill starts where the pointer pressed, then grows: a ripple from the touch point.
            new UIAnimTrack { target = fill.rectTransform, property = UIAnimProperty.AnchoredPosition, start = UIAnimStart.Pointer, keys = { new UIAnimKey() } },
            FromTo(fill, UIAnimProperty.GraphicAlpha, Vector4.zero, new Vector4(1, 0), 0.15f),
            FromTo(fill.rectTransform, UIAnimProperty.Scale, Vector3.zero, Vector3.one, 0.15f));
        anim.States(
            State("Hover", To(hover, UIAnimProperty.GraphicAlpha, new Vector4(1, 0), 0.15f, UIAnimEase.OutSine)),
            State("Unhover", To(hover, UIAnimProperty.GraphicAlpha, Vector4.zero, 0.15f, UIAnimEase.OutSine)),
            press,
            State("Release", To(fill, UIAnimProperty.GraphicAlpha, Vector4.zero, 0.25f, UIAnimEase.OutSine)));
        // The sample completes the running press/release before starting the next one.
        BindButton(root.gameObject, UIAnimInterrupt.Complete, UIAnimInterrupt.Complete);
        return root.gameObject;
    }

    static void BindButton(GameObject go, UIAnimInterrupt press, UIAnimInterrupt release) =>
        go.GetComponent<UIAnimTrigger>().bindings = new List<UIAnimBinding>
        {
            new() { evt = UIAnimEvent.PointerEnter, state = "Hover" },
            new() { evt = UIAnimEvent.PointerExit, state = "Unhover" },
            new() { evt = UIAnimEvent.PointerDown, state = "Press", interrupt = press },
            new() { evt = UIAnimEvent.PointerUp, state = "Release", interrupt = release },
        };

    // ---------- 4_Toggle ----------

    [MenuItem("Tools/UIAnimation Dev/Rebuild Sample Toggles")]
    static void BuildToggles()
    {
        if (!EnsureTmp(BuildToggles)) return;
        var font = LoadOrCreateFont();
        var toggle1 = SavePrefab(BuildToggle1(font), Toggle1Path);
        var toggle2 = SavePrefab(BuildToggle2(), Toggle2Path);
        var scene = BuildSceneIfMissing(TogglesScene, font, "- Toggle -", (toggle1, -216), (toggle2, 264));
        Debug.Log($"[SampleContentBuilder] Built {Toggle1Path}, {Toggle2Path}{scene}");
    }

    // Sample Toggle_1 (checkbox): the checkmark pops in, then the box fills 0.025s later; off reverses the order.
    static GameObject BuildToggle1(TMP_FontAsset font)
    {
        const float duration = 0.1f, delay = 0.025f;
        var root = Rect("Toggle1", null);
        root.sizeDelta = new Vector2(480, 168);

        var label = Label("Label", root, font, "Toggle", Color.white, 65);
        label.alignment = TextAlignmentOptions.Left;
        label.rectTransform.anchoredPosition = new Vector2(80.4f, 0);
        label.rectTransform.sizeDelta = new Vector2(-160.8f, 0);

        var frame = Img("Frame", root, Sprite("Circle Frame"), new Color(1, 1, 1, 0.2f), Image.Type.Sliced, 3.75f);
        frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = new Vector2(0, 0.5f);
        frame.rectTransform.anchoredPosition = new Vector2(60, 0);
        frame.rectTransform.sizeDelta = new Vector2(120, 120);
        var fill = Img("Fill", frame.rectTransform, Sprite("Circle"), Clear(Red), Image.Type.Sliced, 3.75f);
        var check = Img("Checkmark", frame.rectTransform, Sprite("Checkmark"), Color.white, Image.Type.Simple, 10f);
        check.rectTransform.anchorMin = check.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        check.rectTransform.pivot = new Vector2(0.4f, 0.3f);
        check.rectTransform.anchoredPosition = new Vector2(-7.92f, -15.84f);
        check.rectTransform.sizeDelta = new Vector2(79.2f, 79.2f);
        check.rectTransform.localScale = new Vector3(0, 0, 1);

        var anim = AddComponents<Toggle>(root.gameObject);
        anim.States(
            State("On", To(check.rectTransform, UIAnimProperty.Scale, Vector3.one, duration, UIAnimEase.OutSine),
                        Delayed(fill, UIAnimProperty.GraphicAlpha, new Vector4(1, 0), delay, duration)),
            State("Off", Delayed(check.rectTransform, UIAnimProperty.Scale, new Vector3(0, 0, 1), delay, duration),
                         To(fill, UIAnimProperty.GraphicAlpha, Vector4.zero, duration, UIAnimEase.OutSine)));
        BindToggle(root.gameObject);
        return root.gameObject;
    }

    // Sample Toggle_2 (switch): the handle slides 40 to the right and the track fills.
    static GameObject BuildToggle2()
    {
        const float duration = 0.1f;
        var root = Rect("Toggle2", null);
        root.sizeDelta = new Vector2(240, 144);

        Img("Frame", root, Sprite("Circle Frame - Thin"), new Color(1, 1, 1, 0.2f), Image.Type.Sliced, 1.75f);
        var fill = Img("Fill", root, Sprite("Circle"), Clear(Red), Image.Type.Sliced, 1.75f);
        var handle = Img("Handle", root, Sprite("Circle"), new Color(0.9f, 0.9f, 0.9f, 1f), Image.Type.Sliced, 1.46f);
        handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        handle.rectTransform.anchoredPosition = new Vector2(-48, 0);
        handle.rectTransform.sizeDelta = new Vector2(115.2f, 115.2f);

        var anim = AddComponents<Toggle>(root.gameObject);
        anim.States(
            State("On", Relative(handle.rectTransform, UIAnimProperty.AnchoredPosition, new Vector4(96, 0), duration, UIAnimEase.OutSine),
                        To(fill, UIAnimProperty.GraphicAlpha, new Vector4(1, 0), duration, UIAnimEase.OutSine)),
            State("Off", BackToRest(handle.rectTransform, UIAnimProperty.AnchoredPosition, duration, UIAnimEase.OutSine),
                         To(fill, UIAnimProperty.GraphicAlpha, Vector4.zero, duration, UIAnimEase.OutSine)));
        BindToggle(root.gameObject);
        return root.gameObject;
    }

    static void BindToggle(GameObject go)
    {
        var toggle = go.GetComponent<Toggle>();
        toggle.isOn = false;
        go.GetComponent<UIAnimTrigger>().bindings = new List<UIAnimBinding>
        {
            new() { evt = UIAnimEvent.ToggleOn, state = "On" },
            new() { evt = UIAnimEvent.ToggleOff, state = "Off" },
        };
    }

    // ---------- 3_Text ----------

    const string SampleText = "<color=#FF5353>Grumpy wizards</color> make <b>toxic brew</b> for the evil <i>Queen</i> and <i>Jack</i>.";
    const int SampleTextLength = 59;   // characters without rich-text tags; the sample types 0.05s per character

    [MenuItem("Tools/UIAnimation Dev/Rebuild Sample Text")]
    static void BuildText()
    {
        if (!EnsureTmp(BuildText)) return;
        var font = LoadOrCreateFont();
        var demo = SavePrefab(BuildTextDemo(font), TextDemoPath);
        var button = AssetDatabase.LoadAssetAtPath<GameObject>(Button1Path);
        // Generated scene: rebuilt so new states get buttons, unless it is open right now (never overwrite an open scene).
        var open = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(TextScene).isLoaded;
        if (!open && System.IO.File.Exists(TextScene)) AssetDatabase.DeleteAsset(TextScene);
        var scene = open ? " (Text.unity is open, so its buttons were not rebuilt)" : BuildSceneIfMissing(TextScene, font, "- Text -", 450f, panel => PopulateTextScene(panel, demo, button));
        Debug.Log($"[SampleContentBuilder] Built {TextDemoPath}{scene}");
    }

    // Sample 3_Text: one text, one state per effect (the sample's TextAnimation.cs Animation1/3/4/5/6).
    static GameObject BuildTextDemo(TMP_FontAsset font)
    {
        var root = Rect("TextDemo", null);
        root.sizeDelta = new Vector2(1440, 480);
        var text = root.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = SampleText;
        text.fontSize = 72;
        text.alignment = TextAlignmentOptions.Center;

        var anim = root.gameObject.AddComponent<UIAnimation>();
        anim.States(
            // Animation1: typewriter, 0.05s per character, rich text kept
            State("Typewriter", Keys(text, UIAnimProperty.TextReveal, 0f, (0f, Vector4.zero, UIAnimEase.Linear), (SampleTextLength * 0.05f, new Vector4(1, 0), UIAnimEase.Linear))),
            // Animation2: same typewriter, unrevealed characters shown as random lowercase letters
            State("Scramble", Scrambled(Keys(text, UIAnimProperty.TextReveal, 0f, (0f, Vector4.zero, UIAnimEase.Linear), (SampleTextLength * 0.05f, new Vector4(1, 0), UIAnimEase.Linear)), UIAnimScramble.Lowercase)),
            // Animation3: each character flips in around Y (-90 -> 0, OutBack 0.25s), 0.05s apart
            State("Flip", Keys(text, UIAnimProperty.CharRotation, 0.05f, (0f, new Vector3(0, -90, 0), UIAnimEase.Linear), (0.25f, Vector3.zero, UIAnimEase.OutBack))),
            // Animation4: scale 0 -> 1 and rise 120px (0.2s; the sample's 50 on its 800 canvas), then turn red 0.3s later; 0.05s apart
            State("PopUp",
                Keys(text, UIAnimProperty.CharScale, 0.05f, (0f, Vector3.zero, UIAnimEase.Linear), (0.2f, Vector3.one, UIAnimEase.OutSine)),
                Keys(text, UIAnimProperty.CharOffset, 0.05f, (0f, new Vector3(0, -120, 0), UIAnimEase.Linear), (0.2f, Vector3.zero, UIAnimEase.OutSine)),
                Keys(text, UIAnimProperty.CharColor, 0.05f, (0.3f, Color.white, UIAnimEase.Linear), (0.5f, Red, UIAnimEase.OutSine))),
            // Animation5: LMotion.Punch(1, 0.7, 1.4s).WithEase(OutQuad).WithFrequency(7), 0.025s apart (7 over 1.4s = 5 per second)
            State("Punch", Vibrate(Keys(text, UIAnimProperty.CharScale, 0.025f, (0f, Vector3.one, UIAnimEase.Linear)),
                UIAnimCurveType.Punch, 1.4f, Vector3.one * 0.7f, UIAnimEase.OutQuad, 5f)),
            // Animation6: flash red -> white (0.07s, 0.025s apart) and LMotion.Shake(0, 72, 1.4 x i x 0.025s) (the sample's 30 on its 800 canvas) with
            // frequency (int)(duration / 0.2) = 5 per second, damping 1: every character starts at once and shakes longer the later it is
            State("Shake",
                Keys(text, UIAnimProperty.CharColor, 0.025f, (0f, Red, UIAnimEase.Linear), (0.07f, Color.white, UIAnimEase.OutSine)),
                PerCharDuration(Vibrate(Keys(text, UIAnimProperty.CharOffset, 0f, (0f, Vector4.zero, UIAnimEase.Linear)),
                    UIAnimCurveType.Shake, 1f, new Vector3(72, 72, 72), UIAnimEase.Linear, 5f), 1.4f * 0.025f)));
        return root.gameObject;
    }

    static void PopulateTextScene(RectTransform panel, GameObject demo, GameObject button)
    {
        var text = (GameObject)PrefabUtility.InstantiatePrefab(demo, panel);
        var rt = (RectTransform)text.transform;
        rt.anchoredPosition = new Vector2(0, 120);
        var anim = text.GetComponent<UIAnimation>();

        var states = new[] { "Typewriter", "Scramble", "Flip", "PopUp", "Punch", "Shake" };
        var positions = new[] { new Vector2(-528, -250), new Vector2(0, -250), new Vector2(528, -250), new Vector2(-528, -430), new Vector2(0, -430), new Vector2(528, -430) };
        for (var i = 0; i < states.Length; i++)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(button, panel);
            instance.name = $"Button {states[i]}";
            ((RectTransform)instance.transform).anchoredPosition = positions[i];
            instance.GetComponentInChildren<TMP_Text>().text = states[i];
            UnityEditor.Events.UnityEventTools.AddStringPersistentListener(instance.GetComponent<Button>().onClick, anim.PlayState, states[i]);
        }
    }

    // ---------- 1_Hover ----------
    // Sample 1_Hover, laid out x2.4 for the 1920x1080 canvas (button 200x70 -> 480x168, font 24 -> 58).
    // characterSpacing is in em/100, so the sample value 12 stays 12.

    [MenuItem("Tools/UIAnimation Dev/Rebuild Sample Hovers")]
    static void BuildHovers()
    {
        if (!EnsureTmp(BuildHovers)) return;
        var font = LoadOrCreateFont();
        EnsureSprite("Assets/Textures/Square.png");
        var builders = new System.Func<TMP_FontAsset, GameObject>[] { BuildHover1, BuildHover2, BuildHover3, BuildHover4, BuildHover5, BuildHover6 };
        var prefabs = new GameObject[builders.Length];
        for (var i = 0; i < builders.Length; i++) prefabs[i] = SavePrefab(builders[i](font), HoverPath(i + 1));
        var scene = BuildSceneIfMissing(HoverScene, font, "- Hover -", 408f, panel =>
        {
            for (var i = 0; i < prefabs.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], panel);
                ((RectTransform)instance.transform).anchoredPosition = new Vector2((i % 3 - 1) * 600, i < 3 ? 132 : -132);
            }
        });
        Debug.Log($"[SampleContentBuilder] Built Hover1~6{scene}");
    }

    static void EnsureSprite(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null || importer.textureType == TextureImporterType.Sprite) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.SaveAndReimport();
    }

    static RectTransform HoverRoot(string name)
    {
        var root = Rect(name, null);
        root.sizeDelta = new Vector2(480, 168);
        return root;
    }

    static TextMeshProUGUI CenterLabel(string name, Transform parent, TMP_FontAsset font, string text, Color color)
    {
        var label = Label(name, parent, font, text, color, 58);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(480, 168);
        return label;
    }

    static Image HoverFrame(Transform root, float ppu) => Img("Frame", root, Sprite("Circle Frame"), Red, Image.Type.Sliced, ppu);

    static Image HoverMask(Transform root)
    {
        var mask = Img("Mask", root, Sprite("Circle"), Color.white, Image.Type.Sliced, 3.33f);
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        return mask;
    }

    // Enter -> "Hover", Exit -> "Unhover".
    static void BindHover(GameObject go, UIAnimInterrupt interrupt, params UIAnimState[] states)
    {
        go.AddComponent<UIAnimation>().States(states);
        go.AddComponent<UIAnimTrigger>().bindings = new List<UIAnimBinding>
        {
            new() { evt = UIAnimEvent.PointerEnter, state = "Hover", interrupt = interrupt },
            new() { evt = UIAnimEvent.PointerExit, state = "Unhover", interrupt = interrupt },
        };
    }

    // Hover1: fill and label colors
    static GameObject BuildHover1(TMP_FontAsset font)
    {
        var root = HoverRoot("Hover1");
        var fill = Img("Fill", root, Sprite("Circle"), Clear(Red), Image.Type.Sliced, 4.17f);
        var label = Label("Label", root, font, "Hover 1", Red, 58);
        HoverFrame(root, 4.17f);
        BindHover(root.gameObject, UIAnimInterrupt.Cancel,
            State("Hover", To(fill, UIAnimProperty.GraphicColor, Red, 0.2f, UIAnimEase.OutSine), To(label, UIAnimProperty.GraphicColor, Color.white, 0.2f, UIAnimEase.OutSine)),
            State("Unhover", BackToRest(fill, UIAnimProperty.GraphicColor, 0.2f, UIAnimEase.OutSine), BackToRest(label, UIAnimProperty.GraphicColor, 0.2f, UIAnimEase.OutSine)));
        return root.gameObject;
    }

    // Hover2: a horizontal fill sweeps in (Image fillAmount 0 -> 1)
    static GameObject BuildHover2(TMP_FontAsset font)
    {
        var root = HoverRoot("Hover2");
        var mask = HoverMask(root);
        var fill = Img("Fill", mask.rectTransform, Sprite("Square"), Red, Image.Type.Filled, 0.42f);
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 0f;
        var label = Label("Label", root, font, "Hover 2", Red, 58);
        HoverFrame(root, 3.33f);
        BindHover(root.gameObject, UIAnimInterrupt.Cancel,
            State("Hover", To(fill, UIAnimProperty.FillAmount, new Vector4(1, 0), 0.2f, UIAnimEase.OutSine), To(label, UIAnimProperty.GraphicColor, Color.white, 0.2f, UIAnimEase.OutSine)),
            State("Unhover", To(fill, UIAnimProperty.FillAmount, Vector4.zero, 0.2f, UIAnimEase.OutSine), BackToRest(label, UIAnimProperty.GraphicColor, 0.2f, UIAnimEase.OutSine)));
        return root.gameObject;
    }

    // Hover3: a square grows from the center inside the rounded mask
    static GameObject BuildHover3(TMP_FontAsset font)
    {
        var root = HoverRoot("Hover3");
        var mask = HoverMask(root);
        var fill = Img("Fill", mask.rectTransform, Sprite("Circle"), Red, Image.Type.Sliced, 0.42f);
        fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        fill.rectTransform.sizeDelta = new Vector2(480, 480);
        fill.rectTransform.localScale = Vector3.zero;
        var label = Label("Label", root, font, "Hover 3", Red, 58);
        HoverFrame(root, 3.33f);
        BindHover(root.gameObject, UIAnimInterrupt.Cancel,
            State("Hover", To(fill.rectTransform, UIAnimProperty.Scale, Vector3.one, 0.2f, UIAnimEase.OutSine), To(label, UIAnimProperty.GraphicColor, Color.white, 0.2f, UIAnimEase.OutSine)),
            State("Unhover", To(fill.rectTransform, UIAnimProperty.Scale, Vector3.zero, 0.2f, UIAnimEase.OutSine), BackToRest(label, UIAnimProperty.GraphicColor, 0.2f, UIAnimEase.OutSine)));
        return root.gameObject;
    }

    // Hover4: the label slides out to the right while "Hello!" slides in from the left
    static GameObject BuildHover4(TMP_FontAsset font)
    {
        var root = HoverRoot("Hover4");
        var mask = HoverMask(root);
        var fill = Img("Fill", mask.rectTransform, Sprite("Circle"), Red, Image.Type.Sliced, 0.42f);
        fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        fill.rectTransform.sizeDelta = new Vector2(480, 480);
        fill.rectTransform.localScale = Vector3.zero;
        var normal = CenterLabel("Label_Normal", mask.rectTransform, font, "Hover 4", Red);
        var hover = CenterLabel("Label_Hover", mask.rectTransform, font, "Hello!", Color.white);
        hover.rectTransform.anchoredPosition = new Vector2(-480, 0);
        HoverFrame(root, 3.33f);
        BindHover(root.gameObject, UIAnimInterrupt.Cancel,
            State("Hover", To(normal.rectTransform, UIAnimProperty.AnchoredPosition, new Vector4(480, 0), 0.2f, UIAnimEase.OutSine),
                           To(hover.rectTransform, UIAnimProperty.AnchoredPosition, Vector4.zero, 0.2f, UIAnimEase.OutSine)),
            State("Unhover", To(normal.rectTransform, UIAnimProperty.AnchoredPosition, Vector4.zero, 0.2f, UIAnimEase.OutSine),
                             To(hover.rectTransform, UIAnimProperty.AnchoredPosition, new Vector4(-480, 0), 0.2f, UIAnimEase.OutSine)));
        return root.gameObject;
    }

    // Hover5: colors plus the label character spacing opening up (0 -> 12), 0.25s
    static GameObject BuildHover5(TMP_FontAsset font)
    {
        var root = HoverRoot("Hover5");
        var fill = Img("Fill", root, Sprite("Circle"), Clear(Red), Image.Type.Sliced, 3.33f);
        var label = CenterLabel("Label", root, font, "Hover 5", Red);
        HoverFrame(root, 3.33f);
        BindHover(root.gameObject, UIAnimInterrupt.Cancel,
            State("Hover", To(fill, UIAnimProperty.GraphicColor, Red, 0.25f, UIAnimEase.OutSine), To(label, UIAnimProperty.GraphicColor, Color.white, 0.25f, UIAnimEase.OutSine),
                           To(label, UIAnimProperty.CharacterSpacing, new Vector4(12, 0), 0.25f, UIAnimEase.OutSine)),
            State("Unhover", BackToRest(fill, UIAnimProperty.GraphicColor, 0.25f, UIAnimEase.OutSine), BackToRest(label, UIAnimProperty.GraphicColor, 0.25f, UIAnimEase.OutSine),
                             To(label, UIAnimProperty.CharacterSpacing, Vector4.zero, 0.25f, UIAnimEase.OutSine)));
        return root.gameObject;
    }

    // Hover6: a pulse behind the button grows 144px and fades out; enter/exit complete the running pulse first, 0.35s
    static GameObject BuildHover6(TMP_FontAsset font)
    {
        var root = HoverRoot("Hover6");
        var pulse = Img("Pulse", root, Sprite("Circle"), Red, Image.Type.Sliced, 3.33f);
        pulse.raycastTarget = false;
        Img("Background", root, Sprite("Circle"), new Color(0.058823533f, 0.1137255f, 0.19215688f, 1f), Image.Type.Sliced, 3.33f);
        var label = CenterLabel("Label", root, font, "Hover 6", Red);
        HoverFrame(root, 3.33f);
        var grow = new UIAnimTrack
        {
            target = pulse.rectTransform, property = UIAnimProperty.SizeDelta, startFromCurrent = false, relativeToRest = true,
            keys = { new UIAnimKey { time = 0f }, new UIAnimKey { time = 0.35f, value = new Vector4(144, 144), ease = UIAnimEase.OutSine } },
        };
        BindHover(root.gameObject, UIAnimInterrupt.Complete,
            State("Hover", grow, FromTo(pulse, UIAnimProperty.GraphicAlpha, new Vector4(1, 0), Vector4.zero, 0.35f),
                           To(label, UIAnimProperty.GraphicColor, Color.white, 0.35f, UIAnimEase.OutSine)),
            State("Unhover", BackToRest(label, UIAnimProperty.GraphicColor, 0.35f, UIAnimEase.OutSine)));
        return root.gameObject;
    }

    // ---------- popup ----------

    [MenuItem("Tools/UIAnimation Dev/Rebuild Sample Popup")]
    static void BuildPopup()
    {
        if (!EnsureTmp(BuildPopup)) return;
        var font = LoadOrCreateFont();
        var button = AssetDatabase.LoadAssetAtPath<GameObject>(Button1Path);
        var popup = SavePrefab(BuildPopup1(font, button), Popup1Path);
        var scene = BuildSceneIfMissing(PopupScene, font, "- Popup -", 450f, panel => PopulatePopupScene(panel, font, popup, button));
        Debug.Log($"[SampleContentBuilder] Built {Popup1Path}{scene}");
    }

    // Saved in the open pose so it is editable as seen; Open/Close both start from the current values,
    // so an Open that interrupts a Close continues from wherever the Close got to.
    static GameObject BuildPopup1(TMP_FontAsset font, GameObject button)
    {
        var root = Stretch("Popup1", null);
        var dim = Img("Dim", root, null, new Color(0f, 0f, 0f, 0.6f), Image.Type.Simple, 1f);
        dim.raycastTarget = false;   // sample only: keeps the scene buttons clickable while the popup is up

        var window = Rect("Window", root);
        window.sizeDelta = new Vector2(960, 528);
        window.anchoredPosition = new Vector2(0, 60);
        Img("Background", window, Sprite("Circle"), new Color(0.13f, 0.18f, 0.28f, 1f), Image.Type.Sliced, 2.5f);
        var title = Label("Title", window, font, "Popup", Color.white, 72);
        title.rectTransform.offsetMin = new Vector2(0, 384);
        var body = Label("Body", window, font, "Close plays to the end,\nthen the popup is destroyed.", new Color(1, 1, 1, 0.8f), 48);
        body.rectTransform.offsetMin = new Vector2(48, 204);
        body.rectTransform.offsetMax = new Vector2(-48, -144);

        var close = (GameObject)PrefabUtility.InstantiatePrefab(button, window);   // nested prefab: the common button
        close.name = "Button Close";
        ((RectTransform)close.transform).anchoredPosition = new Vector2(0, -156);
        close.GetComponentInChildren<TMP_Text>().text = "Close";

        var anim = root.gameObject.AddComponent<UIAnimation>();
        anim.States(
            State("Open", To(dim, UIAnimProperty.GraphicAlpha, new Vector4(0.6f, 0), 0.25f, UIAnimEase.OutQuad),
                          To(window, UIAnimProperty.Scale, Vector3.one, 0.35f, UIAnimEase.OutBack),
                          BackToRest(window, UIAnimProperty.AnchoredPosition, 0.35f, UIAnimEase.OutCubic)),
            State("Close", To(dim, UIAnimProperty.GraphicAlpha, Vector4.zero, 0.4f, UIAnimEase.OutQuad),
                           To(window, UIAnimProperty.Scale, Vector3.one * 0.6f, 0.4f, UIAnimEase.InBack),
                           Relative(window, UIAnimProperty.AnchoredPosition, new Vector4(0, -192), 0.4f, UIAnimEase.InCubic)));
        // An animation event carrying custom struct data; the sample logs both, so the marker can be checked in Play mode.
        anim.states[0].AddEvent(new UIAnimEventMarker
        {
            time = 0.2f, name = "Shown",
            dataType = UIAnimEventMarker.TypeKey(typeof(PopupSoundEvent)),
            dataJson = JsonUtility.ToJson(new PopupSoundEvent { clip = "popup_open", volume = 0.8f }),
        });
        // A sound at the start of Open (generated chirp, 0.4s): the timeline bar shows how long it plays.
        var openClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/PopupOpen.wav");
        if (openClip != null) anim.states[0].AddSound(new UIAnimSound { time = 0f, clip = openClip, volume = 0.8f });
        // Child calls: the nested common button flashes once when the popup has opened.
        var closeAnim = close.GetComponent<UIAnimation>();
        anim.states[0].childCalls.Add(new UIAnimChildCall { time = 0.3f, child = closeAnim, state = "Hover" });
        anim.states[0].childCalls.Add(new UIAnimChildCall { time = 0.6f, child = closeAnim, state = "Unhover" });
        // Current-prefab override: inside this popup only, the common button hovers twice as fast.
        // Button1.prefab itself is never changed (the copy lives on this prefab).
        var fastHover = closeAnim.states.Find(s => s.name == "Hover").Clone();
        foreach (var track in fastHover.tracks)
        foreach (var key in track.keys) key.time *= 0.5f;   // 0 stays the start key
        anim.overrides.Add(new UIAnimOverride { child = closeAnim, state = "Hover", tracks = fastHover.tracks });
        return root.gameObject;
    }

    static void PopulatePopupScene(RectTransform panel, TMP_FontAsset font, GameObject popup, GameObject button)
    {
        var sample = panel.gameObject.AddComponent<PopupSample>();
        sample.popupPrefab = popup.GetComponent<UIAnimation>();
        sample.popupParent = panel;

        var log = Rect("Log", panel).gameObject.AddComponent<TextMeshProUGUI>();
        log.rectTransform.sizeDelta = new Vector2(1680, 160);
        log.rectTransform.anchoredPosition = new Vector2(0, -290);
        log.font = font;
        log.fontSize = 30;
        log.color = new Color(1, 1, 1, 0.7f);
        log.alignment = TextAlignmentOptions.Top;
        log.text = "";
        sample.log = log;

        (string label, float x, UnityEngine.Events.UnityAction action)[] buttons =
            { ("Open", -288f, sample.Open), ("Close>Open", 288f, sample.CloseThenOpen) };
        foreach (var (label, x, action) in buttons)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(button, panel);
            instance.name = $"Button {label}";
            ((RectTransform)instance.transform).anchoredPosition = new Vector2(x, -455);
            instance.GetComponentInChildren<TMP_Text>().text = label;
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(instance.GetComponent<Button>().onClick, action);
        }
    }

    // ---------- Scroll (R7): 18 forms, one page each, in a pager ----------

    const string ScrollItems = "Assets/Prefabs/Scroll/Items/";
    const string ScrollPages = "Assets/Prefabs/Scroll/Pages/";
    static readonly Color Navy = new(0.13f, 0.18f, 0.28f, 1f);
    static readonly Color Slate = new(0.55f, 0.6f, 0.7f, 1f);

    [MenuItem("Tools/UIAnimation Dev/Rebuild Sample Scroll")]
    static void BuildScroll()
    {
        if (!EnsureTmp(BuildScroll)) return;
        var font = LoadOrCreateFont();
        var items = new ScrollPrefabs
        {
            row = SavePrefab(BuildScrollItem(font), ScrollItemPath),
            card = SavePrefab(BuildScrollCard(font), ScrollCardPath),
            tile = SavePrefab(BuildScrollTile(font), ScrollItems + "ScrollTile.prefab"),
            page = SavePrefab(BuildScrollPage(font), ScrollItems + "ScrollPage.prefab"),
            wheel = SavePrefab(BuildWheelItem(font), ScrollItems + "WheelItem.prefab"),
            header = SavePrefab(BuildSectionHeader(font), ScrollItems + "SectionHeader.prefab"),
            chatLeft = SavePrefab(BuildChatBubble(font, false), ScrollItems + "ChatLeft.prefab"),
            chatRight = SavePrefab(BuildChatBubble(font, true), ScrollItems + "ChatRight.prefab"),
            accordion = SavePrefab(BuildAccordionItem(font), ScrollItems + "AccordionItem.prefab"),
            dot = SavePrefab(BuildPageDot(), ScrollItems + "PageDot.prefab"),
            tab = SavePrefab(BuildTabButton(font), ScrollItems + "TabButton.prefab"),
            button = AssetDatabase.LoadAssetAtPath<GameObject>(Button1Path),
        };
        items.nested = SavePrefab(BuildNestedRow(font, items.tile), ScrollItems + "NestedRow.prefab");

        var pages = new List<GameObject>();
        var kinds = (ScrollDemoPage.Kind[])System.Enum.GetValues(typeof(ScrollDemoPage.Kind));
        for (var i = 0; i < kinds.Length; i++)
            pages.Add(SavePrefab(BuildScrollDemoPage(font, i + 1, kinds[i], items), $"{ScrollPages}{i + 1:00} {kinds[i]}.prefab"));

        var scene = BuildSceneIfMissing(ScrollScene, font, "- Scroll -", 470f, panel => PopulateScrollScene(panel, font, pages, items));
        Debug.Log($"[SampleContentBuilder] Built {ScrollItems}*, {pages.Count} pages in {ScrollPages}{scene}");
    }

    sealed class ScrollPrefabs
    {
        public GameObject row, card, tile, page, wheel, header, chatLeft, chatRight, accordion, nested, dot, tab, button;
    }

    // Root (positioned by the list) > Card (moved by the states) > Background.
    static RectTransform CardRoot(string name, Vector2 size, out RectTransform card, out Image background)
    {
        var root = Rect(name, null);
        root.sizeDelta = size;
        card = Stretch("Card", root);
        background = Img("Background", card, Sprite("Circle"), Navy, Image.Type.Sliced, 2.5f);
        return root;
    }

    static Vector4 Alpha(float a) => new(a, 0, 0, 0);

    // A state sampled by position (0 = start edge, 0.5 = center, 1 = end edge): edge → center → edge.
    static UIAnimTrack Scrub(Component target, UIAnimProperty property, Vector4 start, Vector4 center, Vector4 end)
    {
        var track = new UIAnimTrack { target = target, property = property, start = UIAnimStart.KeyValue };
        track.keys.Add(new UIAnimKey { time = 0f, value = start });
        track.keys.Add(new UIAnimKey { time = 0.5f, value = center, ease = UIAnimEase.InOutSine });
        track.keys.Add(new UIAnimKey { time = 1f, value = end, ease = UIAnimEase.InOutSine });
        return track;
    }

    static UIAnimTrack Scrub(Component target, UIAnimProperty property, Vector4 edge, Vector4 center) => Scrub(target, property, edge, center, edge);

    // A list row: Show (rises and fades in, the entry stagger) and Appear (a small pop when scrolled into view).
    // The list positions the root, so the states move the inner Card.
    static GameObject BuildScrollItem(TMP_FontAsset font)
    {
        var root = CardRoot("ScrollItem", new Vector2(840, 120), out var card, out var background);
        var title = Label("Title", card, font, "Item", Color.white, 44);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.rectTransform.offsetMin = new Vector2(48, 0);
        var index = Label("Index", card, font, "#1", Red, 36);
        index.alignment = TextAlignmentOptions.MidlineRight;
        index.rectTransform.offsetMax = new Vector2(-48, 0);
        var view = root.gameObject.AddComponent<ScrollItemView>();
        view.title = title;
        view.index = index;

        root.gameObject.AddComponent<UIAnimation>().States(
            State("Show", FromTo(card, UIAnimProperty.AnchoredPosition, new Vector4(0, -60), Vector4.zero, 0.35f),
                          FromTo(card, UIAnimProperty.Scale, Vector3.one * 0.9f, Vector3.one, 0.35f),
                          FromTo(background, UIAnimProperty.GraphicAlpha, Vector4.zero, Alpha(1), 0.3f),
                          FromTo(title, UIAnimProperty.GraphicAlpha, Vector4.zero, Alpha(1), 0.3f),
                          FromTo(index, UIAnimProperty.GraphicAlpha, Vector4.zero, Alpha(1), 0.3f)),
            State("Appear", FromTo(card, UIAnimProperty.Scale, Vector3.one * 0.94f, Vector3.one, 0.2f)));
        return root.gameObject;
    }

    // A carousel card: Focus is sampled by the card's place in the viewport (0 = left edge, 0.5 = center, 1 = right edge).
    static GameObject BuildScrollCard(TMP_FontAsset font)
    {
        var root = CardRoot("ScrollCard", new Vector2(360, 480), out var card, out var background);
        var number = Label("Number", card, font, "1", Color.white, 140);
        root.gameObject.AddComponent<ScrollItemView>().title = number;
        root.gameObject.AddComponent<UIAnimation>().States(
            State("Focus", Scrub(card, UIAnimProperty.Scale, Vector3.one * 0.75f, Vector3.one),
                           Scrub(background, UIAnimProperty.GraphicColor, Navy, Red)));
        return root.gameObject;
    }

    // A grid / masonry tile: Show pops it in.
    static GameObject BuildScrollTile(TMP_FontAsset font)
    {
        var root = CardRoot("ScrollTile", new Vector2(200, 200), out var card, out var background);
        var number = Label("Number", card, font, "1", Color.white, 56);
        root.gameObject.AddComponent<ScrollItemView>().title = number;
        root.gameObject.AddComponent<UIAnimation>().States(
            State("Show", FromTo(card, UIAnimProperty.Scale, Vector3.one * 0.6f, Vector3.one, 0.3f),
                          FromTo(background, UIAnimProperty.GraphicAlpha, Vector4.zero, Alpha(1), 0.25f),
                          FromTo(number, UIAnimProperty.GraphicAlpha, Vector4.zero, Alpha(1), 0.25f)));
        return root.gameObject;
    }

    // A page / banner / tab page: Parallax slides the text against the page as it moves through the viewport.
    static GameObject BuildScrollPage(TMP_FontAsset font)
    {
        var root = CardRoot("ScrollPage", new Vector2(900, 460), out var card, out _);
        var label = Label("Title", card, font, "Page", Color.white, 96);
        root.gameObject.AddComponent<ScrollItemView>().title = label;
        root.gameObject.AddComponent<UIAnimation>().States(
            State("Parallax", Scrub(label.rectTransform, UIAnimProperty.AnchoredPosition, new Vector4(-300, 0), Vector4.zero, new Vector4(300, 0)),
                              Scrub(label, UIAnimProperty.GraphicAlpha, Alpha(0), Alpha(1))));
        return root.gameObject;
    }

    // A wheel picker row: Wheel turns it on a cylinder (Rotation X) and fades it away from the center.
    static GameObject BuildWheelItem(TMP_FontAsset font)
    {
        var root = Rect("WheelItem", null);
        root.sizeDelta = new Vector2(400, 80);
        var label = Label("Title", root, font, "00:00", Color.white, 52);
        root.gameObject.AddComponent<ScrollItemView>().title = label;
        root.gameObject.AddComponent<UIAnimation>().States(
            State("Wheel", Scrub(label.rectTransform, UIAnimProperty.Rotation, new Vector3(70, 0, 0), Vector3.zero, new Vector3(-70, 0, 0)),
                           Scrub(label, UIAnimProperty.GraphicAlpha, Alpha(0.15f), Alpha(1)),
                           Scrub(label.rectTransform, UIAnimProperty.Scale, Vector3.one * 0.8f, Vector3.one)));
        return root.gameObject;
    }

    static GameObject BuildSectionHeader(TMP_FontAsset font)
    {
        var root = Rect("SectionHeader", null);
        root.sizeDelta = new Vector2(840, 64);
        Img("Background", root, Sprite("Square"), new Color(0.2f, 0.27f, 0.4f, 1f), Image.Type.Simple, 1f);
        var label = Label("Title", root, font, "Section", Red, 34);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.rectTransform.offsetMin = new Vector2(32, 0);
        return root.gameObject;
    }

    // A chat bubble whose height follows its text (the list measures it): Root (layout) > Bubble (layout, image) > Text.
    static GameObject BuildChatBubble(TMP_FontAsset font, bool me)
    {
        var root = Rect(me ? "ChatRight" : "ChatLeft", null);
        root.sizeDelta = new Vector2(840, 100);
        var rows = root.gameObject.AddComponent<VerticalLayoutGroup>();
        rows.padding = me ? new RectOffset(240, 0, 6, 6) : new RectOffset(0, 240, 6, 6);
        rows.childControlWidth = rows.childControlHeight = true;
        rows.childForceExpandHeight = false;

        var bubble = Rect("Bubble", root);
        // The image is a child outside the layout: a sliced Image reports its border size as a preferred height.
        var image = Img("Background", bubble, Sprite("Circle"), me ? Red : Navy, Image.Type.Sliced, 4f);
        image.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var inside = bubble.gameObject.AddComponent<VerticalLayoutGroup>();
        inside.padding = new RectOffset(32, 32, 20, 20);
        inside.childControlWidth = inside.childControlHeight = true;
        inside.childForceExpandHeight = false;

        var text = Rect("Text", bubble).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = 34;
        text.color = Color.white;
        text.text = "Message";
        text.alignment = me ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
        root.gameObject.AddComponent<ScrollItemView>().title = text;

        root.gameObject.AddComponent<UIAnimation>().States(
            State("Appear", FromTo(bubble, UIAnimProperty.Scale, Vector3.one * 0.85f, Vector3.one, 0.25f),
                            FromTo(image, UIAnimProperty.GraphicAlpha, Vector4.zero, Alpha(1), 0.2f),
                            FromTo(text, UIAnimProperty.GraphicAlpha, Vector4.zero, Alpha(1), 0.2f)));
        return root.gameObject;
    }

    // An accordion row: click → Open / Close (the + turns into ×, the answer fades in) while the list resizes the row.
    static GameObject BuildAccordionItem(TMP_FontAsset font)
    {
        var root = Rect("AccordionItem", null);
        root.sizeDelta = new Vector2(840, 110);
        var background = Img("Background", root, Sprite("Circle"), Navy, Image.Type.Sliced, 2.5f);
        root.gameObject.AddComponent<RectMask2D>();
        root.gameObject.AddComponent<Button>().targetGraphic = background;

        RectTransform Top(string name, float height, float y)
        {
            var rt = Rect(name, root);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(-96, height);
            rt.anchoredPosition = new Vector2(0, y);
            return rt;
        }
        var title = Label("Title", Top("Header", 110, 0), font, "Question", Color.white, 40);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        var plus = Label("Plus", title.rectTransform.parent, font, "+", Red, 64);
        plus.rectTransform.anchorMin = plus.rectTransform.anchorMax = new Vector2(1, 0.5f);
        plus.rectTransform.sizeDelta = new Vector2(80, 80);
        plus.rectTransform.anchoredPosition = new Vector2(-40, 0);
        var body = Label("Body", Top("Answer", 170, -110), font,
            "The list animates this row's height and moves the rows below; the row's own Open state turns the + and fades this text in.",
            Slate, 30);
        body.alignment = TextAlignmentOptions.TopLeft;
        var view = root.gameObject.AddComponent<ScrollItemView>();
        view.title = title;

        root.gameObject.AddComponent<UIAnimation>().States(
            State("Open", To(plus.rectTransform, UIAnimProperty.Rotation, new Vector3(0, 0, 45), 0.3f, UIAnimEase.OutCubic),
                          To(body, UIAnimProperty.GraphicAlpha, Alpha(1), 0.3f, UIAnimEase.OutSine)),
            State("Close", To(plus.rectTransform, UIAnimProperty.Rotation, Vector3.zero, 0.3f, UIAnimEase.OutCubic),
                           To(body, UIAnimProperty.GraphicAlpha, Alpha(0), 0.2f, UIAnimEase.OutSine)));
        return root.gameObject;
    }

    // A row with its own horizontal list (nested scrolling): vertical drags go to the outer list.
    static GameObject BuildNestedRow(TMP_FontAsset font, GameObject tile)
    {
        var root = Rect("NestedRow", null);
        root.sizeDelta = new Vector2(840, 300);
        var title = Label("Title", root, font, "Row", Color.white, 36);
        title.alignment = TextAlignmentOptions.TopLeft;
        var inner = ScrollList("Row List", root, new Vector2(840, 220), new Vector2(0, -40), tile);
        inner.layout = UIAnimScrollList.LayoutType.Horizontal;
        inner.spacing = 16;
        inner.childAlignment = TextAnchor.MiddleLeft;
        inner.entryState = "Show";
        inner.entryStagger = 0.04f;
        return root.gameObject;
    }

    static UIAnimation DotStates(GameObject go, Graphic graphic, Transform scaled, Color on, Color off, Vector3 onScale, Vector3 offScale)
    {
        var anim = go.AddComponent<UIAnimation>();
        anim.States(
            State("On", To(scaled, UIAnimProperty.Scale, onScale, 0.25f, UIAnimEase.OutBack), To(graphic, UIAnimProperty.GraphicColor, on, 0.2f, UIAnimEase.OutSine)),
            State("Off", To(scaled, UIAnimProperty.Scale, offScale, 0.25f, UIAnimEase.OutSine), To(graphic, UIAnimProperty.GraphicColor, off, 0.2f, UIAnimEase.OutSine)));
        return anim;
    }

    static GameObject BuildPageDot()
    {
        var root = Rect("PageDot", null);
        root.sizeDelta = new Vector2(24, 24);
        var image = root.gameObject.AddComponent<Image>();
        image.sprite = Sprite("Circle");
        image.color = Slate;
        root.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        DotStates(root.gameObject, image, root, Color.white, Slate, Vector3.one * 1.5f, Vector3.one);
        return root.gameObject;
    }

    static GameObject BuildTabButton(TMP_FontAsset font)
    {
        var root = Rect("TabButton", null);
        root.sizeDelta = new Vector2(200, 72);
        root.gameObject.AddComponent<Image>().color = Color.clear;   // click area
        root.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        var label = Label("Label", root, font, "Tab", Slate, 34);
        var underline = Rect("Underline", root);
        underline.anchorMin = new Vector2(0.1f, 0);
        underline.anchorMax = new Vector2(0.9f, 0);
        underline.sizeDelta = new Vector2(0, 6);
        underline.anchoredPosition = new Vector2(0, 3);
        underline.localScale = new Vector3(0, 1, 1);
        underline.gameObject.AddComponent<Image>().color = Red;
        DotStates(root.gameObject, label, underline, Color.white, Slate, Vector3.one, new Vector3(0, 1, 1));
        return root.gameObject;
    }

    static TextMeshProUGUI Caption(string name, Transform parent, TMP_FontAsset font, string text, Color color, float size, Vector2 position, Vector2 box)
    {
        var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.rectTransform.sizeDelta = box;
        label.rectTransform.anchoredPosition = position;
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    static UIAnimPageIndicator Dots(Transform parent, UIAnimScrollList list, GameObject prefab, Vector2 position, float spacing = 16)
    {
        var root = Rect("Dots", parent);
        root.sizeDelta = new Vector2(1200, 80);
        root.anchoredPosition = position;
        var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var indicator = root.gameObject.AddComponent<UIAnimPageIndicator>();
        indicator.list = list;
        indicator.itemPrefab = (RectTransform)prefab.transform;
        return indicator;
    }

    static readonly Dictionary<ScrollDemoPage.Kind, (string title, string description)> PageText = new()
    {
        [ScrollDemoPage.Kind.Vertical] = ("Vertical List", "100 rows, only the visible ones exist · entry stagger, appear on scroll"),
        [ScrollDemoPage.Kind.Horizontal] = ("Horizontal List", "Horizontal layout · entry stagger"),
        [ScrollDemoPage.Kind.Grid] = ("Grid", "Flexible columns · entry stagger, appear on scroll"),
        [ScrollDemoPage.Kind.HorizontalGrid] = ("Horizontal Grid", "Fixed Row Count 2 scrolls sideways"),
        [ScrollDemoPage.Kind.Carousel] = ("Carousel", "Scrub state by position: the centered card grows · snap to center"),
        [ScrollDemoPage.Kind.Paging] = ("Paging", "One page per swipe · dots (On / Off states) · parallax text"),
        [ScrollDemoPage.Kind.InfiniteLoop] = ("Infinite Loop", "8 cards, forever in both directions · snap to center"),
        [ScrollDemoPage.Kind.AutoBanner] = ("Auto Banner", "Moves on every 2.5 s, pauses while dragged · loop · dots"),
        [ScrollDemoPage.Kind.WheelPicker] = ("Wheel Picker", "Rotation X / alpha / scale scrubbed by position · snap to center"),
        [ScrollDemoPage.Kind.StickyHeaders] = ("Sticky Headers", "Two item prefabs; the section header stays on top, pushed by the next"),
        [ScrollDemoPage.Kind.VariableSize] = ("Variable Size", "Measure Items: each row as tall as its text"),
        [ScrollDemoPage.Kind.Chat] = ("Chat", "Two bubble prefabs · stacks from the bottom · stays at the end when messages arrive"),
        [ScrollDemoPage.Kind.Accordion] = ("Accordion", "Click a row: Open / Close state + animated row height"),
        [ScrollDemoPage.Kind.Masonry] = ("Masonry", "4 columns, each tile goes to the shortest column"),
        [ScrollDemoPage.Kind.Nested] = ("Nested Scroll", "Horizontal lists inside a vertical list: each drag goes to its own axis"),
        [ScrollDemoPage.Kind.Tabs] = ("Tabs", "Tab bar (On / Off states) + one page per swipe"),
        [ScrollDemoPage.Kind.CollapsingHeader] = ("Collapsing Header", "Scroll Scrub: the header's Collapse state follows the scroll distance"),
        [ScrollDemoPage.Kind.PullToRefresh] = ("Pull To Refresh", "Pull down past the line: Pull state by distance, Refreshing until done"),
    };

    static GameObject BuildScrollDemoPage(TMP_FontAsset font, int number, ScrollDemoPage.Kind kind, ScrollPrefabs items)
    {
        var (titleText, description) = PageText[kind];
        var root = Rect($"{number:00} {kind}", null);
        root.sizeDelta = new Vector2(1920, 860);
        root.gameObject.AddComponent<Image>().color = Color.clear;   // drags on the page turn pages
        Caption("Heading", root, font, $"{number}. {titleText}", Color.white, 56, new Vector2(0, 380), new Vector2(1800, 80));
        Caption("Description", root, font, description, Slate, 30, new Vector2(0, 320), new Vector2(1800, 50));
        var demo = Rect("Demo", root);
        demo.sizeDelta = new Vector2(1800, 680);
        demo.anchoredPosition = new Vector2(0, -60);

        var page = root.gameObject.AddComponent<ScrollDemoPage>();
        page.kind = kind;
        page.title = titleText;

        UIAnimScrollList MakeList(GameObject item, Vector2 size, Vector2 position = default)
        {
            var list = ScrollList("List", demo, size, position, item);
            page.list = list;
            return list;
        }
        UIAnimScrollList Rows(GameObject item, float width = 840, float height = 680, float y = 0)
        {
            var list = MakeList(item, new Vector2(width, height), new Vector2(0, y));
            list.spacing = 12;
            list.padding = new RectOffset(0, 0, 8, 8);
            list.controlChildSize = true;
            list.childAlignment = TextAnchor.UpperCenter;
            return list;
        }

        switch (kind)
        {
            case ScrollDemoPage.Kind.Vertical:
            {
                var list = Rows(items.row);
                list.entryState = "Show";
                list.entryStagger = 0.05f;
                list.appearState = "Appear";
                break;
            }
            case ScrollDemoPage.Kind.Horizontal:
            {
                var list = MakeList(items.tile, new Vector2(1600, 260));
                list.layout = UIAnimScrollList.LayoutType.Horizontal;
                list.padding = new RectOffset(20, 20, 0, 0);
                list.spacing = 20;
                list.childAlignment = TextAnchor.MiddleLeft;
                list.entryState = "Show";
                list.entryStagger = 0.04f;
                break;
            }
            case ScrollDemoPage.Kind.Grid:
            case ScrollDemoPage.Kind.HorizontalGrid:
            {
                var horizontal = kind == ScrollDemoPage.Kind.HorizontalGrid;
                var list = MakeList(items.tile, horizontal ? new Vector2(1600, 440) : new Vector2(1000, 680));
                list.layout = UIAnimScrollList.LayoutType.Grid;
                list.cellSize = new Vector2(180, 180);
                list.gridSpacing = new Vector2(20, 20);
                list.padding = new RectOffset(20, 20, 20, 20);
                list.childAlignment = horizontal ? TextAnchor.MiddleLeft : TextAnchor.UpperCenter;
                list.constraint = horizontal ? UIAnimScrollList.GridConstraint.FixedRowCount : UIAnimScrollList.GridConstraint.Flexible;
                list.constraintCount = 2;
                list.entryState = "Show";
                list.entryStagger = 0.02f;
                list.appearState = horizontal ? "" : "Show";
                break;
            }
            case ScrollDemoPage.Kind.Carousel:
            case ScrollDemoPage.Kind.InfiniteLoop:
            {
                var list = MakeList(items.card, new Vector2(1600, 560));
                list.layout = UIAnimScrollList.LayoutType.Horizontal;
                list.loop = kind == ScrollDemoPage.Kind.InfiniteLoop;
                list.padding = list.loop ? new RectOffset() : new RectOffset(620, 620, 0, 0);   // first and last cards can reach the center
                list.spacing = 24;
                list.childAlignment = TextAnchor.MiddleLeft;
                list.scrubState = "Focus";
                list.snap = UIAnimScrollList.SnapMode.Nearest;
                list.snapAlignment = UIAnimScrollList.Alignment.Center;
                list.playEntryOnFill = false;
                break;
            }
            case ScrollDemoPage.Kind.Paging:
            case ScrollDemoPage.Kind.AutoBanner:
            {
                var list = MakeList(items.page, new Vector2(900, 460), new Vector2(0, 40));
                list.layout = UIAnimScrollList.LayoutType.Horizontal;
                list.controlChildSize = true;
                list.loop = kind == ScrollDemoPage.Kind.AutoBanner;
                list.snap = UIAnimScrollList.SnapMode.OnePerSwipe;
                list.snapAlignment = UIAnimScrollList.Alignment.Center;
                list.snapDuration = 0.35f;
                list.scrubState = "Parallax";
                list.autoScrollInterval = kind == ScrollDemoPage.Kind.AutoBanner ? 2.5f : 0f;
                list.playEntryOnFill = false;
                Dots(demo, list, items.dot, new Vector2(0, -250));
                break;
            }
            case ScrollDemoPage.Kind.WheelPicker:
            {
                var band = Img("Selection", demo, Sprite("Circle"), new Color(1, 1, 1, 0.08f), Image.Type.Sliced, 4f).rectTransform;
                band.anchorMin = band.anchorMax = new Vector2(0.5f, 0.5f);
                band.sizeDelta = new Vector2(420, 84);
                var list = MakeList(items.wheel, new Vector2(400, 480));
                list.padding = new RectOffset(0, 0, 200, 200);   // first and last rows can reach the center
                list.controlChildSize = true;
                list.scrubState = "Wheel";
                list.snap = UIAnimScrollList.SnapMode.Nearest;
                list.snapAlignment = UIAnimScrollList.Alignment.Center;
                list.playEntryOnFill = false;
                list.GetComponent<Image>().color = Color.clear;
                page.status = Caption("Status", demo, font, "", Red, 44, new Vector2(520, 0), new Vector2(500, 80));
                break;
            }
            case ScrollDemoPage.Kind.StickyHeaders:
            {
                var list = Rows(items.row);
                list.extraPrefabs.Add((RectTransform)items.header.transform);
                list.stickyHeaderPrefab = 1;
                list.padding = new RectOffset();
                break;
            }
            case ScrollDemoPage.Kind.VariableSize:
            {
                var list = Rows(items.chatLeft);
                list.measureItems = true;
                list.spacing = 4;
                list.appearState = "Appear";
                break;
            }
            case ScrollDemoPage.Kind.Chat:
            {
                var list = Rows(items.chatLeft, 840, 560, 50);
                list.extraPrefabs.Add((RectTransform)items.chatRight.transform);
                list.measureItems = true;
                list.spacing = 4;
                list.stickToEnd = true;
                list.childAlignment = TextAnchor.LowerCenter;
                list.appearState = "Appear";
                var send = (GameObject)PrefabUtility.InstantiatePrefab(items.button, demo);
                send.name = "Button Send";
                var rect = (RectTransform)send.transform;
                rect.anchoredPosition = new Vector2(0, -290);
                rect.localScale = Vector3.one * 0.45f;
                send.GetComponentInChildren<TMP_Text>().text = "Send";
                UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(send.GetComponent<Button>().onClick, page.Send);
                break;
            }
            case ScrollDemoPage.Kind.Accordion:
                Rows(items.accordion);
                break;
            case ScrollDemoPage.Kind.Masonry:
            {
                var list = MakeList(items.tile, new Vector2(1000, 680));
                list.layout = UIAnimScrollList.LayoutType.Masonry;
                list.constraint = UIAnimScrollList.GridConstraint.FixedColumnCount;
                list.constraintCount = 4;
                list.cellSize = new Vector2(220, 220);
                list.gridSpacing = new Vector2(16, 16);
                list.padding = new RectOffset(20, 20, 20, 20);
                list.childAlignment = TextAnchor.UpperCenter;
                list.entryState = "Show";
                list.entryStagger = 0.02f;
                list.appearState = "Show";
                break;
            }
            case ScrollDemoPage.Kind.Nested:
            {
                var list = MakeList(items.nested, new Vector2(900, 680));
                list.spacing = 24;
                list.padding = new RectOffset(0, 0, 16, 16);
                list.childAlignment = TextAnchor.UpperCenter;
                break;
            }
            case ScrollDemoPage.Kind.Tabs:
            {
                var list = MakeList(items.page, new Vector2(900, 500), new Vector2(0, -60));
                list.layout = UIAnimScrollList.LayoutType.Horizontal;
                list.controlChildSize = true;
                list.snap = UIAnimScrollList.SnapMode.OnePerSwipe;
                list.snapAlignment = UIAnimScrollList.Alignment.Start;
                list.snapDuration = 0.3f;
                list.scrubState = "Parallax";
                list.playEntryOnFill = false;
                var tabs = Dots(demo, list, items.tab, new Vector2(0, 260), 0);
                tabs.labels = new List<string> { "Home", "Search", "Inbox", "Profile" };
                break;
            }
            case ScrollDemoPage.Kind.CollapsingHeader:
            {
                var list = Rows(items.row);
                list.padding = new RectOffset(0, 0, 312, 8);   // the rows start below the open header
                var header = Rect("Header", demo);
                header.pivot = new Vector2(0.5f, 1);
                header.sizeDelta = new Vector2(840, 300);
                header.anchoredPosition = new Vector2(0, 340);
                var background = Img("Background", header, Sprite("Circle"), Red, Image.Type.Sliced, 2.5f);
                background.raycastTarget = false;   // drags pass to the list below
                var name = Label("Title", header, font, "Profile", Color.white, 80);
                name.raycastTarget = false;
                var anim = header.gameObject.AddComponent<UIAnimation>();
                anim.States(State("Collapse",
                    FromTo(header, UIAnimProperty.SizeDelta, new Vector2(840, 300), new Vector2(840, 110), 1f),
                    FromTo(name, UIAnimProperty.FontSize, Alpha(80), Alpha(44), 1f),
                    FromTo(background, UIAnimProperty.GraphicColor, Red, Navy, 1f)));
                foreach (var track in anim.states[0].tracks) track.keys[^1].ease = UIAnimEase.Linear;   // the scroll distance is the time
                var scrub = header.gameObject.AddComponent<UIAnimScrollScrub>();
                scrub.list = list;
                scrub.target = anim;
                scrub.state = "Collapse";
                scrub.distance = 190;
                break;
            }
            case ScrollDemoPage.Kind.PullToRefresh:
            {
                var spinner = Rect("Spinner", demo);
                spinner.sizeDelta = new Vector2(72, 72);
                spinner.anchoredPosition = new Vector2(0, 190);   // in the gap that opens above the list (list top = 260), behind it until pulled
                var ring = spinner.gameObject.AddComponent<Image>();
                ring.sprite = Sprite("Circle Frame");
                ring.type = Image.Type.Filled;
                ring.fillMethod = Image.FillMethod.Radial360;
                ring.color = Red;
                ring.raycastTarget = false;
                var anim = spinner.gameObject.AddComponent<UIAnimation>();
                var spin = new UIAnimTrack { target = spinner, property = UIAnimProperty.Rotation, start = UIAnimStart.KeyValue };
                spin.keys.Add(new UIAnimKey { time = 0f, value = new Vector3(0, 0, -270) });
                spin.keys.Add(new UIAnimKey { time = 0.8f, value = new Vector3(0, 0, -630), ease = UIAnimEase.Linear });   // ≡ -270: repeats seamlessly
                anim.States(
                    State("Pull", FromTo(ring, UIAnimProperty.FillAmount, Alpha(0), Alpha(0.75f), 1f),
                                  FromTo(spinner, UIAnimProperty.Rotation, Vector3.zero, new Vector3(0, 0, -270), 1f),
                                  FromTo(ring, UIAnimProperty.GraphicAlpha, Alpha(0.2f), Alpha(1), 1f)),
                    State("Refreshing", spin));
                foreach (var track in anim.states[0].tracks) track.keys[^1].ease = UIAnimEase.Linear;

                var list = Rows(items.row, 840, 600, -40);   // drawn over the spinner, which shows when the list is pulled down
                var pull = list.gameObject.AddComponent<UIAnimPullToRefresh>();
                pull.target = anim;
                pull.distance = 140;
                UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(pull.onRefresh, page.Refresh);
                page.pull = pull;
                list.GetComponent<Image>().color = Color.clear;
                page.status = Caption("Status", demo, font, "", Slate, 30, new Vector2(0, 320), new Vector2(800, 40));
                break;
            }
        }
        return root.gameObject;
    }

    static void PopulateScrollScene(RectTransform panel, TMP_FontAsset font, List<GameObject> pages, ScrollPrefabs items)
    {
        var browser = panel.gameObject.AddComponent<ScrollBrowser>();
        var pager = ScrollList("Pages", panel, new Vector2(1920, 820), new Vector2(0, 0), pages[0]);   // y -410 … 410
        pager.GetComponent<Image>().color = Color.clear;
        for (var i = 1; i < pages.Count; i++) pager.extraPrefabs.Add((RectTransform)pages[i].transform);
        pager.layout = UIAnimScrollList.LayoutType.Horizontal;
        pager.controlChildSize = true;
        pager.snap = UIAnimScrollList.SnapMode.OnePerSwipe;
        pager.snapAlignment = UIAnimScrollList.Alignment.Start;
        pager.snapDuration = 0.35f;
        pager.playEntryOnFill = false;
        browser.pages = pager;
        browser.counter = Caption("Counter", panel, font, "1 / 18", Slate, 34, new Vector2(820, 470), new Vector2(240, 60));

        // Two rows under the pages: the toolbar (y -448) and page navigation (y -508), so nothing overlaps the title.
        Dots(panel, pager, items.dot, new Vector2(0, -508), 14);
        (string label, Vector2 at, float scale, UnityEngine.Events.UnityAction action)[] buttons =
        {
            ("Prev", new Vector2(-820, -508), 0.35f, browser.Previous), ("Next", new Vector2(820, -508), 0.35f, browser.Next),
            ("Remove", new Vector2(-40, -448), 0.3f, browser.RemoveItem), ("Add", new Vector2(120, -448), 0.3f, browser.AddItem),
            ("Edit Animation", new Vector2(380, -448), 0.35f, browser.EditAnimation),
        };
        foreach (var (label, at, scale, action) in buttons)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(items.button, panel);
            instance.name = $"Button {label}";
            var rect = (RectTransform)instance.transform;
            rect.anchoredPosition = at;
            rect.localScale = Vector3.one * scale;
            instance.GetComponentInChildren<TMP_Text>().text = label;
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(instance.GetComponent<Button>().onClick, action);
        }

        // Item count of the current page: a slider and its value.
        var slider = DefaultControls.CreateSlider(new DefaultControls.Resources()).GetComponent<Slider>();
        slider.name = "Item Count";
        var sliderRect = (RectTransform)slider.transform;
        sliderRect.SetParent(panel, false);
        sliderRect.sizeDelta = new Vector2(320, 24);
        sliderRect.anchoredPosition = new Vector2(-500, -448);
        slider.fillRect.GetComponent<Image>().color = Red;
        browser.itemCount = slider;
        browser.itemCountLabel = Caption("Item Count Label", panel, font, "Items", Slate, 30, new Vector2(-220, -448), new Vector2(200, 50));
        browser.itemCountLabel.alignment = TextAlignmentOptions.MidlineLeft;   // grows to the right, away from the slider
    }

    static UIAnimScrollList ScrollList(string name, Transform parent, Vector2 size, Vector2 position, GameObject itemPrefab)
    {
        var root = Rect(name, parent);
        root.sizeDelta = size;
        root.anchoredPosition = position;
        root.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.04f);   // backdrop, also catches drags
        var viewport = Stretch("Viewport", root);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", viewport);
        var scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.scrollSensitivity = 40f;
        var list = root.gameObject.AddComponent<UIAnimScrollList>();
        list.itemPrefab = (RectTransform)itemPrefab.transform;
        return list;
    }

    // Keys at absolute values (text tracks never start from the live value).
    static UIAnimTrack Keys(Component target, UIAnimProperty property, float stagger, params (float time, Vector4 value, UIAnimEase ease)[] keys)
    {
        var track = new UIAnimTrack { target = target, property = property, startFromCurrent = false, stagger = stagger };
        foreach (var (time, value, ease) in keys) track.keys.Add(new UIAnimKey { time = time, value = value, ease = ease });
        return track;
    }

    static UIAnimTrack Scrambled(UIAnimTrack reveal, UIAnimScramble mode)
    {
        reveal.scramble = mode;
        return reveal;
    }

    // Adds a Punch/Shake key: the track oscillates around its previous value with this strength and ends back on it.
    static UIAnimTrack Vibrate(UIAnimTrack track, UIAnimCurveType type, float time, Vector4 strength, UIAnimEase ease, float perSecond)
    {
        track.keys.Add(new UIAnimKey { time = time, value = strength, curveType = type, ease = ease, frequency = perSecond, damping = 1f });
        return track;
    }

    static UIAnimTrack PerCharDuration(UIAnimTrack track, float seconds)
    {
        track.durationPerChar = seconds;
        return track;
    }

    // ---------- track helpers ----------

    static UIAnimState State(string name, params UIAnimTrack[] tracks) => new() { name = name, tracks = new List<UIAnimTrack>(tracks) };

    static void States(this UIAnimation anim, params UIAnimState[] states)
    {
        anim.states = new List<UIAnimState>(states);
        // Every track starts with a key at 0, as the editor makes them (same playback, see EnsureStartKey).
        foreach (var state in states)
        foreach (var track in state.tracks)
            SeoBYP.TimelineAnimation.Editor.UIAnimationEditorWindow.EnsureStartKey(track);
    }

    // current value → value
    static UIAnimTrack To(Component target, UIAnimProperty property, Vector4 value, float time, UIAnimEase ease) =>
        new() { target = target, property = property, startFromCurrent = true, keys = { new UIAnimKey { time = time, value = value, ease = ease } } };

    // hold the current value for `delay`, then go to value: the first key becomes the live value.
    static UIAnimTrack Delayed(Component target, UIAnimProperty property, Vector4 value, float delay, float duration) =>
        new()
        {
            target = target, property = property, startFromCurrent = true,
            keys = { new UIAnimKey { time = delay }, new UIAnimKey { time = delay + duration, value = value, ease = UIAnimEase.OutSine } },
        };

    // current value → rest value + offset
    static UIAnimTrack Relative(Component target, UIAnimProperty property, Vector4 offset, float time, UIAnimEase ease) =>
        new() { target = target, property = property, startFromCurrent = true, relativeToRest = true, keys = { new UIAnimKey { time = time, value = offset, ease = ease } } };

    static UIAnimTrack BackToRest(Component target, UIAnimProperty property, float time, UIAnimEase ease) =>
        Relative(target, property, Vector4.zero, time, ease);

    // from → to, regardless of the current value
    static UIAnimTrack FromTo(Component target, UIAnimProperty property, Vector4 from, Vector4 to, float time) =>
        new()
        {
            target = target, property = property, startFromCurrent = false,
            keys = { new UIAnimKey { time = 0f, value = from }, new UIAnimKey { time = time, value = to, ease = UIAnimEase.OutSine } },
        };

    // ---------- object helpers ----------

    static UIAnimation AddComponents<TSelectable>(GameObject go) where TSelectable : Selectable
    {
        go.AddComponent<TSelectable>().transition = Selectable.Transition.None;
        var anim = go.AddComponent<UIAnimation>();
        go.AddComponent<UIAnimTrigger>();
        return anim;
    }

    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        if (parent != null) rt.SetParent(parent, false);
        return rt;
    }

    static RectTransform Stretch(string name, Transform parent)
    {
        var rt = Rect(name, parent);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    static Image Img(string name, Transform parent, Sprite sprite, Color color, Image.Type type, float pixelsPerUnitMultiplier)
    {
        var image = Stretch(name, parent).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = type;
        image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
        return image;
    }

    static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, string text, Color color, float size)
    {
        var label = Stretch(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        return label;
    }

    static GameObject SavePrefab(GameObject root, string path)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static TMP_FontAsset LoadOrCreateFont()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (existing != null) return existing;

        var ttf = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Montserrat/Montserrat-SemiBold.ttf");
        var font = TMP_FontAsset.CreateFontAsset(ttf);
        font.name = "Montserrat-SemiBold SDF";
        AssetDatabase.CreateAsset(font, FontPath);
        font.material.name = font.name + " Material";
        AssetDatabase.AddObjectToAsset(font.material, font);
        font.atlasTextures[0].name = font.name + " Atlas";
        AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
        AssetDatabase.SaveAssets();
        return font;
    }

    // The scene only places the prefabs; instances pick up prefab changes, so an existing scene is never overwritten (it may be open).
    static string BuildSceneIfMissing(string path, TMP_FontAsset font, string titleText, params (GameObject prefab, float x)[] items) =>
        BuildSceneIfMissing(path, font, titleText, 240f, panel =>
        {
            foreach (var (prefab, x) in items)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, panel);
                ((RectTransform)instance.transform).anchoredPosition = new Vector2(x, 0);
            }
        });

    static string BuildSceneIfMissing(string path, TMP_FontAsset font, string titleText, float titleY, System.Action<RectTransform> populate)
    {
        if (System.IO.File.Exists(path)) return "";

        // Additive + closed afterwards, so whatever scene is open (and unsaved) is never replaced.
        // Only an untitled scene (batchmode, a fresh editor) can't take an additive one: then it is replaced.
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var additive = !string.IsNullOrEmpty(previous.path);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, additive ? NewSceneMode.Additive : NewSceneMode.Single);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);   // new GameObjects land here

        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.058873262f, 0.11302212f, 0.1981132f, 0f);

        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var panel = Rect("Panel", canvasGo.transform);
        panel.sizeDelta = new Vector2(1920, 1080);

        var title = Rect("Title", panel).gameObject.AddComponent<TextMeshProUGUI>();
        title.rectTransform.sizeDelta = new Vector2(960, 120);
        title.rectTransform.anchoredPosition = new Vector2(0, titleY);
        title.font = font;
        title.text = titleText;
        title.fontSize = 84;
        title.alignment = TextAlignmentOptions.Center;

        populate(panel);

        new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));

        EditorSceneManager.SaveScene(scene, path);
        if (!additive) return $", {path}";
        if (previous.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
        EditorSceneManager.CloseScene(scene, true);
        return $", {path}";
    }
}
