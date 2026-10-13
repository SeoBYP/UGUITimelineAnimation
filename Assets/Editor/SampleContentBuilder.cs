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
        var scene = BuildSceneIfMissing(ButtonsScene, font, "- Button -", (button1, -120), (button2, 120));
        Debug.Log($"[SampleContentBuilder] Built {Button1Path}, {Button2Path}{scene}");
    }

    // Sample Button_1 = Hover1 (fill/label color) + Button1 (press shrinks size and font).
    static GameObject BuildButton1(TMP_FontAsset font, Sprite circle, Sprite circleFrame)
    {
        var root = Rect("Button1", null);
        root.sizeDelta = new Vector2(200, 70);
        var fill = Img("Fill", root, circle, Clear(Red), Image.Type.Sliced, 10f);
        var label = Label("Label", root, font, "Button 1", Red, 24);
        Img("Frame", root, circleFrame, Red, Image.Type.Sliced, 10f);

        var anim = AddComponents<Button>(root.gameObject);
        anim.States(
            State("Hover", To(fill, UIAnimProperty.GraphicColor, Red, 0.2f, Ease.OutSine),
                           To(label, UIAnimProperty.GraphicColor, Color.white, 0.2f, Ease.OutSine)),
            State("Unhover", BackToRest(fill, UIAnimProperty.GraphicColor, 0.2f, Ease.OutSine),
                             BackToRest(label, UIAnimProperty.GraphicColor, 0.2f, Ease.OutSine)),
            State("Press", Relative(root, UIAnimProperty.SizeDelta, new Vector4(-14, -7), 0.07f, Ease.OutQuad),
                           Relative(label, UIAnimProperty.FontSize, new Vector4(-1, 0), 0.07f, Ease.OutQuad)),
            State("Release", BackToRest(root, UIAnimProperty.SizeDelta, 0.07f, Ease.OutQuad),
                             BackToRest(label, UIAnimProperty.FontSize, 0.07f, Ease.OutQuad)));
        BindButton(root.gameObject, UIAnimInterrupt.Cancel, UIAnimInterrupt.Cancel);
        return root.gameObject;
    }

    // Sample Button_2: hover overlay fades in; press spawns a circle at the pointer that grows, release fades it.
    static GameObject BuildButton2(TMP_FontAsset font, Sprite circle, Sprite circleFrame)
    {
        var root = Rect("Button2", null);
        root.sizeDelta = new Vector2(200, 70);

        var mask = Img("Mask", root, circle, Color.white, Image.Type.Sliced, 8f);
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var hover = Img("Hover", mask.rectTransform, null, new Color(0.20784315f, 0.1254902f, 0.2f, 0f), Image.Type.Simple, 1f);
        var fill = Img("Fill", mask.rectTransform, circle, Red, Image.Type.Simple, 1f);
        fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        fill.rectTransform.sizeDelta = new Vector2(450, 450);
        fill.rectTransform.localScale = Vector3.zero;
        Label("Label", root, font, "Button 2", Color.white, 24);
        Img("Frame", root, circleFrame, Red, Image.Type.Sliced, 8f);

        var anim = AddComponents<Button>(root.gameObject);
        var press = State("Press",
            FromTo(fill, UIAnimProperty.GraphicAlpha, Vector4.zero, new Vector4(1, 0), 0.15f),
            FromTo(fill.rectTransform, UIAnimProperty.Scale, Vector3.zero, Vector3.one, 0.15f));
        press.actions.Add(new UIAnimAction { type = UIAnimActionType.MoveToPointer, target = fill.rectTransform });
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
        var scene = BuildSceneIfMissing(TogglesScene, font, "- Toggle -", (toggle1, -90), (toggle2, 110));
        Debug.Log($"[SampleContentBuilder] Built {Toggle1Path}, {Toggle2Path}{scene}");
    }

    // Sample Toggle_1 (checkbox): the checkmark pops in, then the box fills 0.025s later; off reverses the order.
    static GameObject BuildToggle1(TMP_FontAsset font)
    {
        const float duration = 0.1f, delay = 0.025f;
        var root = Rect("Toggle1", null);
        root.sizeDelta = new Vector2(200, 70);

        var label = Label("Label", root, font, "Toggle", Color.white, 27);
        label.alignment = TextAlignmentOptions.Left;
        label.rectTransform.anchoredPosition = new Vector2(33.5f, 0);
        label.rectTransform.sizeDelta = new Vector2(-67, 0);

        var frame = Img("Frame", root, Sprite("Circle Frame"), new Color(1, 1, 1, 0.2f), Image.Type.Sliced, 9f);
        frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = new Vector2(0, 0.5f);
        frame.rectTransform.anchoredPosition = new Vector2(25, 0);
        frame.rectTransform.sizeDelta = new Vector2(50, 50);
        var fill = Img("Fill", frame.rectTransform, Sprite("Circle"), Clear(Red), Image.Type.Sliced, 9f);
        var check = Img("Checkmark", frame.rectTransform, Sprite("Checkmark"), Color.white, Image.Type.Simple, 10f);
        check.rectTransform.anchorMin = check.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        check.rectTransform.pivot = new Vector2(0.4f, 0.3f);
        check.rectTransform.anchoredPosition = new Vector2(-3.3f, -6.6f);
        check.rectTransform.sizeDelta = new Vector2(33, 33);
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
        root.sizeDelta = new Vector2(100, 60);

        Img("Frame", root, Sprite("Circle Frame - Thin"), new Color(1, 1, 1, 0.2f), Image.Type.Sliced, 4.2f);
        var fill = Img("Fill", root, Sprite("Circle"), Clear(Red), Image.Type.Sliced, 4.2f);
        var handle = Img("Handle", root, Sprite("Circle"), new Color(0.9f, 0.9f, 0.9f, 1f), Image.Type.Sliced, 3.5f);
        handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        handle.rectTransform.anchoredPosition = new Vector2(-20, 0);
        handle.rectTransform.sizeDelta = new Vector2(48, 48);

        var anim = AddComponents<Toggle>(root.gameObject);
        anim.States(
            State("On", Relative(handle.rectTransform, UIAnimProperty.AnchoredPosition, new Vector4(40, 0), duration, Ease.OutSine),
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
        var scene = open ? " (Text.unity is open, so its buttons were not rebuilt)" : BuildSceneIfMissing(TextScene, font, "- Text -", 190f, panel => PopulateTextScene(panel, demo, button));
        Debug.Log($"[SampleContentBuilder] Built {TextDemoPath}{scene}");
    }

    // Sample 3_Text: one text, one state per effect (the sample's TextAnimation.cs Animation1/3/4/5/6).
    static GameObject BuildTextDemo(TMP_FontAsset font)
    {
        var root = Rect("TextDemo", null);
        root.sizeDelta = new Vector2(600, 200);
        var text = root.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = SampleText;
        text.fontSize = 30;
        text.alignment = TextAlignmentOptions.Center;

        var anim = root.gameObject.AddComponent<UIAnimation>();
        anim.States(
            // Animation1: typewriter, 0.05s per character, rich text kept
            State("Typewriter", Keys(text, UIAnimProperty.TextReveal, 0f, (0f, Vector4.zero, Ease.Linear), (SampleTextLength * 0.05f, new Vector4(1, 0), Ease.Linear))),
            // Animation2: same typewriter, unrevealed characters shown as random lowercase letters
            State("Scramble", Scrambled(Keys(text, UIAnimProperty.TextReveal, 0f, (0f, Vector4.zero, Ease.Linear), (SampleTextLength * 0.05f, new Vector4(1, 0), Ease.Linear)), ScrambleMode.Lowercase)),
            // Animation3: each character flips in around Y (-90 -> 0, OutBack 0.25s), 0.05s apart
            State("Flip", Keys(text, UIAnimProperty.CharRotation, 0.05f, (0f, new Vector3(0, -90, 0), Ease.Linear), (0.25f, Vector3.zero, Ease.OutBack))),
            // Animation4: scale 0 -> 1 and rise 50px (0.2s), then turn red 0.3s later; 0.05s apart
            State("PopUp",
                Keys(text, UIAnimProperty.CharScale, 0.05f, (0f, Vector3.zero, Ease.Linear), (0.2f, Vector3.one, Ease.OutSine)),
                Keys(text, UIAnimProperty.CharOffset, 0.05f, (0f, new Vector3(0, -50, 0), Ease.Linear), (0.2f, Vector3.zero, Ease.OutSine)),
                Keys(text, UIAnimProperty.CharColor, 0.05f, (0.3f, Color.white, Ease.Linear), (0.5f, Red, Ease.OutSine))),
            // Animation5: LMotion.Punch(1, 0.7, 1.4s).WithEase(OutQuad).WithFrequency(7), 0.025s apart (7 over 1.4s = 5 per second)
            State("Punch", Vibrate(Keys(text, UIAnimProperty.CharScale, 0.025f, (0f, Vector3.one, Ease.Linear)),
                UIAnimCurveType.Punch, 1.4f, Vector3.one * 0.7f, Ease.OutQuad, 5f)),
            // Animation6: flash red -> white (0.07s, 0.025s apart) and LMotion.Shake(0, 30, 1.4 x i x 0.025s) with
            // frequency (int)(duration / 0.2) = 5 per second, damping 1: every character starts at once and shakes longer the later it is
            State("Shake",
                Keys(text, UIAnimProperty.CharColor, 0.025f, (0f, Red, Ease.Linear), (0.07f, Color.white, Ease.OutSine)),
                PerCharDuration(Vibrate(Keys(text, UIAnimProperty.CharOffset, 0f, (0f, Vector4.zero, Ease.Linear)),
                    UIAnimCurveType.Shake, 1f, new Vector3(30, 30, 30), Ease.Linear, 5f), 1.4f * 0.025f)));
        return root.gameObject;
    }

    static void PopulateTextScene(RectTransform panel, GameObject demo, GameObject button)
    {
        var text = (GameObject)PrefabUtility.InstantiatePrefab(demo, panel);
        var rt = (RectTransform)text.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -170);
        var anim = text.GetComponent<UIAnimation>();

        var states = new[] { "Typewriter", "Scramble", "Flip", "PopUp", "Punch", "Shake" };
        var positions = new[] { new Vector2(-220, -120), new Vector2(0, -120), new Vector2(220, -120), new Vector2(-220, -200), new Vector2(0, -200), new Vector2(220, -200) };
        for (var i = 0; i < states.Length; i++)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(button, panel);
            instance.name = $"Button {states[i]}";
            ((RectTransform)instance.transform).anchoredPosition = positions[i];
            instance.GetComponentInChildren<TMP_Text>().text = states[i];
            UnityEditor.Events.UnityEventTools.AddStringPersistentListener(instance.GetComponent<Button>().onClick, anim.PlayState, states[i]);
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
        BuildSceneIfMissing(path, font, titleText, 100f, panel =>
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
        scaler.referenceResolution = new Vector2(800, 600);
        scaler.matchWidthOrHeight = 0f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var panel = Rect("Panel", canvasGo.transform);
        panel.sizeDelta = new Vector2(800, 600);

        var title = Rect("Title", panel).gameObject.AddComponent<TextMeshProUGUI>();
        title.rectTransform.sizeDelta = new Vector2(400, 50);
        title.rectTransform.anchoredPosition = new Vector2(0, titleY);
        title.font = font;
        title.text = titleText;
        title.fontSize = 35;
        title.alignment = TextAlignmentOptions.Center;

        populate(panel);

        new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));

        EditorSceneManager.SaveScene(scene, path);
        if (previous.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
        EditorSceneManager.CloseScene(scene, true);
        return $", {path}";
    }
}
