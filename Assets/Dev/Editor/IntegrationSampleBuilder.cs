using System;
using System.IO;
using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Dev-only: builds the integration samples' scenes (Samples/UniTask, Samples/R3). The sample scripts compile only when
/// their library is installed, so they are found by name: run this on the integration branch of that library.
/// </summary>
static class IntegrationSampleBuilder
{
    [MenuItem("Tools/UIAnimation Dev/Rebuild UniTask Sample")]
    public static void BuildUniTask() => Build("UniTask", "UniTaskPopupSample", new[] { "Open", "Close", "Slow (1s 제한)", "Wait Shown" });

    [MenuItem("Tools/UIAnimation Dev/Rebuild R3 Sample")]
    public static void BuildR3() => Build("R3", "R3PopupSample", new[] { "Open", "Close" });

    static void Build(string lib, string script, string[] buttons)
    {
        var root = $"Assets/UIAnimation/Samples/{lib}/";
        var type = Type.GetType($"{script}, SeoBYP.TimelineAnimation.Samples.{lib}")
                   ?? throw new Exception($"{script} not compiled: is {lib} installed (this is its integration branch)?");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UIAnimation/Samples/RPG/Font/PassionOne-Regular SDF.asset");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(28, 30, 40, 255);
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;

        RectTransform Node(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }
        TMP_Text Text(RectTransform rt, string text, float size, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }
        static UIAnimTrack Track(Component target, UIAnimProperty property, params (float t, Vector4 v, UIAnimEase e)[] keys)
        {
            var track = new UIAnimTrack { target = target, property = property, start = UIAnimStart.KeyValue };
            foreach (var (t, v, e) in keys) track.keys.Add(new UIAnimKey { time = t, value = v, ease = e });
            return track;
        }

        var title = Text(Node("Title", canvas.transform, new Vector2(0, 460), new Vector2(1600, 90)), $"UI Animation + {lib}", 72);
        title.color = new Color32(255, 222, 140, 255);

        // Dim + popup: Open pops in (scale OutBack, fade) and raises "Shown"; Close shrinks out; Slow turns for 3 s.
        var dim = Node("Dim", canvas.transform, Vector2.zero, Vector2.zero);
        dim.anchorMin = Vector2.zero;
        dim.anchorMax = Vector2.one;
        var dimImage = dim.gameObject.AddComponent<Image>();
        dimImage.color = new Color(0, 0, 0, 0);
        dimImage.raycastTarget = false;
        var dimAnim = dim.gameObject.AddComponent<UIAnimation>();
        dimAnim.states.Add(new UIAnimState { name = "Show", tracks = { Track(dimImage, UIAnimProperty.GraphicAlpha, (0, Vector4.zero, UIAnimEase.Linear), (0.25f, new Vector4(0.6f, 0), UIAnimEase.OutQuad)) } });
        dimAnim.states.Add(new UIAnimState { name = "Hide", tracks = { Track(dimImage, UIAnimProperty.GraphicAlpha, (0, new Vector4(0.6f, 0), UIAnimEase.Linear), (0.2f, Vector4.zero, UIAnimEase.OutQuad)) } });

        var popup = Node("Popup", canvas.transform, new Vector2(0, 60), new Vector2(640, 380));
        popup.gameObject.AddComponent<Image>().color = new Color32(245, 236, 216, 255);
        var group = popup.gameObject.AddComponent<CanvasGroup>();
        Text(Node("Label", popup, Vector2.zero, new Vector2(600, 200)), "POPUP", 96).color = new Color32(70, 44, 24, 255);
        var anim = popup.gameObject.AddComponent<UIAnimation>();
        var open = new UIAnimState { name = "Open", tracks =
        {
            Track(popup, UIAnimProperty.Scale, (0, new Vector4(0.6f, 0.6f, 1), UIAnimEase.Linear), (0.35f, Vector3.one, UIAnimEase.OutBack)),
            Track(group, UIAnimProperty.CanvasGroupAlpha, (0, Vector4.zero, UIAnimEase.Linear), (0.2f, new Vector4(1, 0), UIAnimEase.OutQuad)),
            Track(popup, UIAnimProperty.Rotation, (0, Vector4.zero, UIAnimEase.Linear), (0.01f, Vector4.zero, UIAnimEase.Linear)),
        } };
        open.AddEvent(new UIAnimEventMarker { time = 0.25f, name = "Shown" });
        anim.states.Add(open);
        anim.states.Add(new UIAnimState { name = "Close", tracks =
        {
            Track(popup, UIAnimProperty.Scale, (0, Vector3.one, UIAnimEase.Linear), (0.2f, new Vector4(0.8f, 0.8f, 1), UIAnimEase.InQuad)),
            Track(group, UIAnimProperty.CanvasGroupAlpha, (0, new Vector4(1, 0), UIAnimEase.Linear), (0.2f, Vector4.zero, UIAnimEase.InQuad)),
        } });
        anim.states.Add(new UIAnimState { name = "Slow", tracks =
        {
            Track(popup, UIAnimProperty.Rotation, (0, Vector4.zero, UIAnimEase.Linear), (3f, new Vector4(0, 0, 360), UIAnimEase.InOutSine)),
        } });
        group.alpha = 0;
        popup.localScale = new Vector3(0.6f, 0.6f, 1);

        // Buttons and the log.
        var sample = new GameObject(script).AddComponent(type);
        var so = new SerializedObject(sample);
        so.FindProperty("popup").objectReferenceValue = anim;
        so.FindProperty("dim")?.SetObject(dimAnim);
        for (var i = 0; i < buttons.Length; i++)
        {
            var b = Node(buttons[i] + "Button", canvas.transform, new Vector2((i - (buttons.Length - 1) / 2f) * 300, -330), new Vector2(270, 90));
            b.gameObject.AddComponent<Image>().color = new Color32(214, 150, 50, 255);
            var button = b.gameObject.AddComponent<Button>();
            Text(Node("Label", b, Vector2.zero, new Vector2(260, 80)), buttons[i], 34).color = new Color32(40, 24, 10, 255);
            var field = new[] { "open", "close", "slow", "waitShown" }[i];
            so.FindProperty(field).objectReferenceValue = button;
        }
        var log = Node("Log", canvas.transform, new Vector2(-640, 60), new Vector2(520, 520));
        so.FindProperty("log").objectReferenceValue = Text(log, "", 30, TextAlignmentOptions.TopLeft);
        so.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(root + "Scenes");
        var path = $"{root}Scenes/{lib}Popup.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[IntegrationSampleBuilder] Built {path}");
    }

    static void SetObject(this SerializedProperty property, UnityEngine.Object value) => property.objectReferenceValue = value;
}
