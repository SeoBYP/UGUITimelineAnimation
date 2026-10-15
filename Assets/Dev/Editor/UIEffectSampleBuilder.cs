using Coffee.UIEffects;
using SeoBYP.TimelineAnimation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dev-only (integration/uieffect): builds a prefab whose states animate UIEffect values, to check the UIEffect tracks
/// in the UI Animation Editor preview without Play mode.
/// </summary>
static class UIEffectSampleBuilder
{
    const string Folder = "Assets/UIAnimation/Samples/UIEffect";

    [MenuItem("Tools/UIAnimation Dev/Build UIEffect Sample Prefab")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/UIAnimation/Samples", "UIEffect");

        var go = new GameObject("UIEffectCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(320, 320);
        var image = go.GetComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UIAnimation/Samples/RPG/Art/Heroes/aria.png");
        image.preserveAspect = true;

        var effect = go.AddComponent<UIEffect>();
        effect.toneFilter = ToneFilter.Grayscale;
        effect.toneIntensity = 0f;
        effect.transitionFilter = TransitionFilter.Shiny;
        effect.transitionRate = 0f;

        go.SetActive(false);
        var anim = go.AddComponent<UIAnimation>();
        // Unlock: grey → color, then a shine sweeps across.
        anim.states.Add(State("Unlock", false,
            Track(effect, UIAnimProperty.UIEffectToneIntensity, (0f, 1f, UIAnimEase.Linear), (0.6f, 0f, UIAnimEase.OutCubic)),
            Track(effect, UIAnimProperty.UIEffectTransitionRate, (0.4f, 0f, UIAnimEase.Linear), (1.2f, 1f, UIAnimEase.InOutSine))));
        // Lock: back to grey.
        anim.states.Add(State("Lock", false,
            Track(effect, UIAnimProperty.UIEffectToneIntensity, (0f, 0f, UIAnimEase.Linear), (0.4f, 1f, UIAnimEase.InCubic))));
        // Glow: a shine sweeps across again and again (an edge shine would need a transparent outline; this card is opaque).
        anim.states.Add(State("Glow", true,
            Track(effect, UIAnimProperty.UIEffectTransitionRate, (0f, 0f, UIAnimEase.Linear), (1.5f, 1f, UIAnimEase.InOutSine))));
        go.SetActive(true);

        var path = $"{Folder}/UIEffectCard.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Debug.Log($"[UIEffect sample] {path}");
    }

    static UIAnimState State(string name, bool loop, params UIAnimTrack[] tracks)
    {
        var state = new UIAnimState { name = name, loop = loop };
        state.tracks.AddRange(tracks);
        return state;
    }

    static UIAnimTrack Track(Component target, UIAnimProperty property, params (float time, float value, UIAnimEase ease)[] keys)
    {
        var track = new UIAnimTrack { target = target, property = property, start = UIAnimStart.KeyValue };
        foreach (var (time, value, ease) in keys)
            track.keys.Add(new UIAnimKey { time = time, value = new Vector4(value, 0, 0, 0), ease = ease });
        return track;
    }
}
