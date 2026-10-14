using System.Collections.Generic;
using LitMotion;
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
    const string Button1Path = "Assets/Prefabs/Button1.prefab";
    const string Button2Path = "Assets/Prefabs/Button2.prefab";
    const string ButtonsScene = "Assets/Scenes/Buttons.unity";
    const string Toggle1Path = "Assets/Prefabs/Toggle1.prefab";
    const string Toggle2Path = "Assets/Prefabs/Toggle2.prefab";
    const string TogglesScene = "Assets/Scenes/Toggles.unity";
    const string TextDemoPath = "Assets/Prefabs/TextDemo.prefab";
    const string TextScene = "Assets/Scenes/Text.unity";
    const string Popup1Path = "Assets/Prefabs/Popup1.prefab";
    const string HoverScene = "Assets/Scenes/Hover.unity";
    static string HoverPath(int n) => $"Assets/Prefabs/Hover{n}.prefab";
    const string PopupScene = "Assets/Scenes/Popup.unity";

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
            State("Hover", To(fill, UIAnimProperty.GraphicColor, Red, 0.2f, Ease.OutSine),
                           To(label, UIAnimProperty.GraphicColor, Color.white, 0.2f, Ease.OutSine)),
            State("Unhover", BackToRest(fill, UIAnimProperty.GraphicColor, 0.2f, Ease.OutSine),
                             BackToRest(label, UIAnimProperty.GraphicColor, 0.2f, Ease.OutSine)),
            State("Press", Relative(root, UIAnimProperty.SizeDelta, new Vector4(-34, -17), 0.07f, Ease.OutQuad),
                           Relative(label, UIAnimProperty.FontSize, new Vector4(-2.4f, 0), 0.07f, Ease.OutQuad)),
            State("Release", BackToRest(root, UIAnimProperty.SizeDelta, 0.07f, Ease.OutQuad),
                             BackToRest(label, UIAnimProperty.FontSize, 0.07f, Ease.OutQuad)));
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
            State("Hover", To(hover, UIAnimProperty.GraphicAlpha, new Vector4(1, 0), 0.15f, Ease.OutSine)),
            State("Unhover", To(hover, UIAnimProperty.GraphicAlpha, Vector4.zero, 0.15f, Ease.OutSine)),
            press,
            State("Release", To(fill, UIAnimProperty.GraphicAlpha, Vector4.zero, 0.25f, Ease.OutSine)));
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
            State("On", To(check.rectTransform, UIAnimProperty.Scale, Vector3.one, duration, Ease.OutSine),
                        Delayed(fill, UIAnimProperty.GraphicAlpha, new Vector4(1, 0), delay, duration)),
            State("Off", Delayed(check.rectTransform, UIAnimProperty.Scale, new Vector3(0, 0, 1), delay, duration),
                         To(fill, UIAnimProperty.GraphicAlpha, Vector4.zero, duration, Ease.OutSine)));
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
            State("On", Relative(handle.rectTransform, UIAnimProperty.AnchoredPosition, new Vector4(96, 0), duration, Ease.OutSine),
                        To(fill, UIAnimProperty.GraphicAlpha, new Vector4(1, 0), duration, Ease.OutSine)),
            State("Off", BackToRest(handle.rectTransform, UIAnimProperty.AnchoredPosition, duration, Ease.OutSine),
                         To(fill, UIAnimProperty.GraphicAlpha, Vector4.zero, duration, Ease.OutSine)));
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
            State("Typewriter", Keys(text, UIAnimProperty.TextReveal, 0f, (0f, Vector4.zero, Ease.Linear), (SampleTextLength * 0.05f, new Vector4(1, 0), Ease.Linear))),
            // Animation2: same typewriter, unrevealed characters shown as random lowercase letters
            State("Scramble", Scrambled(Keys(text, UIAnimProperty.TextReveal, 0f, (0f, Vector4.zero, Ease.Linear), (SampleTextLength * 0.05f, new Vector4(1, 0), Ease.Linear)), ScrambleMode.Lowercase)),
            // Animation3: each character flips in around Y (-90 -> 0, OutBack 0.25s), 0.05s apart
            State("Flip", Keys(text, UIAnimProperty.CharRotation, 0.05f, (0f, new Vector3(0, -90, 0), Ease.Linear), (0.25f, Vector3.zero, Ease.OutBack))),
            // Animation4: scale 0 -> 1 and rise 120px (0.2s; the sample's 50 on its 800 canvas), then turn red 0.3s later; 0.05s apart
            State("PopUp",
                Keys(text, UIAnimProperty.CharScale, 0.05f, (0f, Vector3.zero, Ease.Linear), (0.2f, Vector3.one, Ease.OutSine)),
                Keys(text, UIAnimProperty.CharOffset, 0.05f, (0f, new Vector3(0, -120, 0), Ease.Linear), (0.2f, Vector3.zero, Ease.OutSine)),
                Keys(text, UIAnimProperty.CharColor, 0.05f, (0.3f, Color.white, Ease.Linear), (0.5f, Red, Ease.OutSine))),
            // Animation5: LMotion.Punch(1, 0.7, 1.4s).WithEase(OutQuad).WithFrequency(7), 0.025s apart (7 over 1.4s = 5 per second)
            State("Punch", Vibrate(Keys(text, UIAnimProperty.CharScale, 0.025f, (0f, Vector3.one, Ease.Linear)),
                UIAnimCurveType.Punch, 1.4f, Vector3.one * 0.7f, Ease.OutQuad, 5f)),
            // Animation6: flash red -> white (0.07s, 0.025s apart) and LMotion.Shake(0, 72, 1.4 x i x 0.025s) (the sample's 30 on its 800 canvas) with
            // frequency (int)(duration / 0.2) = 5 per second, damping 1: every character starts at once and shakes longer the later it is
            State("Shake",
                Keys(text, UIAnimProperty.CharColor, 0.025f, (0f, Red, Ease.Linear), (0.07f, Color.white, Ease.OutSine)),
                PerCharDuration(Vibrate(Keys(text, UIAnimProperty.CharOffset, 0f, (0f, Vector4.zero, Ease.Linear)),
                    UIAnimCurveType.Shake, 1f, new Vector3(72, 72, 72), Ease.Linear, 5f), 1.4f * 0.025f)));
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
            State("Hover", To(fill, UIAnimProperty.GraphicColor, Red, 0.2f, Ease.OutSine), To(label, UIAnimProperty.GraphicColor, Color.white, 0.2f, Ease.OutSine)),
            State("Unhover", BackToRest(fill, UIAnimProperty.GraphicColor, 0.2f, Ease.OutSine), BackToRest(label, UIAnimProperty.GraphicColor, 0.2f, Ease.OutSine)));
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
            State("Hover", To(fill, UIAnimProperty.FillAmount, new Vector4(1, 0), 0.2f, Ease.OutSine), To(label, UIAnimProperty.GraphicColor, Color.white, 0.2f, Ease.OutSine)),
            State("Unhover", To(fill, UIAnimProperty.FillAmount, Vector4.zero, 0.2f, Ease.OutSine), BackToRest(label, UIAnimProperty.GraphicColor, 0.2f, Ease.OutSine)));
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
            State("Hover", To(fill.rectTransform, UIAnimProperty.Scale, Vector3.one, 0.2f, Ease.OutSine), To(label, UIAnimProperty.GraphicColor, Color.white, 0.2f, Ease.OutSine)),
            State("Unhover", To(fill.rectTransform, UIAnimProperty.Scale, Vector3.zero, 0.2f, Ease.OutSine), BackToRest(label, UIAnimProperty.GraphicColor, 0.2f, Ease.OutSine)));
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
            State("Hover", To(normal.rectTransform, UIAnimProperty.AnchoredPosition, new Vector4(480, 0), 0.2f, Ease.OutSine),
                           To(hover.rectTransform, UIAnimProperty.AnchoredPosition, Vector4.zero, 0.2f, Ease.OutSine)),
            State("Unhover", To(normal.rectTransform, UIAnimProperty.AnchoredPosition, Vector4.zero, 0.2f, Ease.OutSine),
                             To(hover.rectTransform, UIAnimProperty.AnchoredPosition, new Vector4(-480, 0), 0.2f, Ease.OutSine)));
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
            State("Hover", To(fill, UIAnimProperty.GraphicColor, Red, 0.25f, Ease.OutSine), To(label, UIAnimProperty.GraphicColor, Color.white, 0.25f, Ease.OutSine),
                           To(label, UIAnimProperty.CharacterSpacing, new Vector4(12, 0), 0.25f, Ease.OutSine)),
            State("Unhover", BackToRest(fill, UIAnimProperty.GraphicColor, 0.25f, Ease.OutSine), BackToRest(label, UIAnimProperty.GraphicColor, 0.25f, Ease.OutSine),
                             To(label, UIAnimProperty.CharacterSpacing, Vector4.zero, 0.25f, Ease.OutSine)));
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
            keys = { new UIAnimKey { time = 0f }, new UIAnimKey { time = 0.35f, value = new Vector4(144, 144), ease = Ease.OutSine } },
        };
        BindHover(root.gameObject, UIAnimInterrupt.Complete,
            State("Hover", grow, FromTo(pulse, UIAnimProperty.GraphicAlpha, new Vector4(1, 0), Vector4.zero, 0.35f),
                           To(label, UIAnimProperty.GraphicColor, Color.white, 0.35f, Ease.OutSine)),
            State("Unhover", BackToRest(label, UIAnimProperty.GraphicColor, 0.35f, Ease.OutSine)));
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
            State("Open", To(dim, UIAnimProperty.GraphicAlpha, new Vector4(0.6f, 0), 0.25f, Ease.OutQuad),
                          To(window, UIAnimProperty.Scale, Vector3.one, 0.35f, Ease.OutBack),
                          BackToRest(window, UIAnimProperty.AnchoredPosition, 0.35f, Ease.OutCubic)),
            State("Close", To(dim, UIAnimProperty.GraphicAlpha, Vector4.zero, 0.4f, Ease.OutQuad),
                           To(window, UIAnimProperty.Scale, Vector3.one * 0.6f, 0.4f, Ease.InBack),
                           Relative(window, UIAnimProperty.AnchoredPosition, new Vector4(0, -192), 0.4f, Ease.InCubic)));
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
        anim.overrides.Add(new UIAnimOverride { child = closeAnim, state = "Hover", replacement = fastHover });
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

    // Keys at absolute values (text tracks never start from the live value).
    static UIAnimTrack Keys(Component target, UIAnimProperty property, float stagger, params (float time, Vector4 value, Ease ease)[] keys)
    {
        var track = new UIAnimTrack { target = target, property = property, startFromCurrent = false, stagger = stagger };
        foreach (var (time, value, ease) in keys) track.keys.Add(new UIAnimKey { time = time, value = value, ease = ease });
        return track;
    }

    static UIAnimTrack Scrambled(UIAnimTrack reveal, ScrambleMode mode)
    {
        reveal.scramble = mode;
        return reveal;
    }

    // Adds a Punch/Shake key: the track oscillates around its previous value with this strength and ends back on it.
    static UIAnimTrack Vibrate(UIAnimTrack track, UIAnimCurveType type, float time, Vector4 strength, Ease ease, float perSecond)
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
    static UIAnimTrack To(Component target, UIAnimProperty property, Vector4 value, float time, Ease ease) =>
        new() { target = target, property = property, startFromCurrent = true, keys = { new UIAnimKey { time = time, value = value, ease = ease } } };

    // hold the current value for `delay`, then go to value: the first key becomes the live value.
    static UIAnimTrack Delayed(Component target, UIAnimProperty property, Vector4 value, float delay, float duration) =>
        new()
        {
            target = target, property = property, startFromCurrent = true,
            keys = { new UIAnimKey { time = delay }, new UIAnimKey { time = delay + duration, value = value, ease = Ease.OutSine } },
        };

    // current value → rest value + offset
    static UIAnimTrack Relative(Component target, UIAnimProperty property, Vector4 offset, float time, Ease ease) =>
        new() { target = target, property = property, startFromCurrent = true, relativeToRest = true, keys = { new UIAnimKey { time = time, value = offset, ease = ease } } };

    static UIAnimTrack BackToRest(Component target, UIAnimProperty property, float time, Ease ease) =>
        Relative(target, property, Vector4.zero, time, ease);

    // from → to, regardless of the current value
    static UIAnimTrack FromTo(Component target, UIAnimProperty property, Vector4 from, Vector4 to, float time) =>
        new()
        {
            target = target, property = property, startFromCurrent = false,
            keys = { new UIAnimKey { time = 0f, value = from }, new UIAnimKey { time = time, value = to, ease = Ease.OutSine } },
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
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
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
        if (previous.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
        EditorSceneManager.CloseScene(scene, true);
        return $", {path}";
    }
}
