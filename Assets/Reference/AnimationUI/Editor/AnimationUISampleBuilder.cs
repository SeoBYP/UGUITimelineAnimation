using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SeoBYP.TimelineAnimation;
using SeoBYP.TimelineAnimation.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Dev-only: rebuilds the Unity-AnimationUI demo with UI Animation. The original scenes (Original~, layout untouched) are
/// copied to Scenes/, the original scripts are removed, and every AnimationUI sequence and ButtonUI is replaced by
/// UIAnimation states built from Data/AnimationUI-Demo.json (values read from the original scene files).
/// </summary>
static class AnimationUISampleBuilder
{
    const string Root = "Assets/Reference/AnimationUI/";
    static readonly string[] SoundFiles = { "ButtonEnter", "ButtonMove", "ButtonClick", "ButtonHover" };   // the original's SFX indices 0..3

    // ---------- data (Data/AnimationUI-Demo.json) ----------

    [Serializable] class Demo { public List<SceneData> scenes; }
    [Serializable] class SceneData { public string name; public List<AnimData> animations; public List<ButtonData> buttons; public List<ClickData> clicks; }
    [Serializable] class AnimData { public string owner; public bool playOnStart; public float totalDuration; public List<StepData> steps; }
    [Serializable] class StepData { public string type, ease, target, targetKind, scene; public float time, duration; public List<PropData> props; public bool active; public int sfx; }
    [Serializable] class PropData { public string property; public Vector4 from, to; }
    [Serializable]
    class ButtonData
    {
        public string path, graphic, text;
        public Color normal, highlighted, pressed, selected, textNormal, textHighlighted, textPressed, textSelected;
        public float normalScale, highlightedScale, pressedScale, selectedScale, duration;
    }
    [Serializable] class ClickData { public string button, target, targetKind, method; public bool boolArgument; }

    [MenuItem("Tools/UIAnimation Dev/Rebuild AnimationUI Sample")]
    public static void Build()
    {
        var json = File.ReadAllText(Root + "Data/AnimationUI-Demo.json");
        var demo = JsonUtility.FromJson<Demo>(json);
        var clips = SoundFiles.Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}Audio/{n}.wav")).ToArray();
        var particles = ParticleMaterial();

        var built = new List<string>();
        foreach (var scene in demo.scenes) built.Add(BuildScene(scene, clips, particles));
        AddToBuildSettings(built);
        Debug.Log($"[AnimationUISampleBuilder] Built {string.Join(", ", built)}");
    }

    static string BuildScene(SceneData data, AudioClip[] clips, Material particles)
    {
        var path = $"{Root}Scenes/{data.name}.unity";
        File.Copy($"{Root}Original~/{data.name}.unity", path, true);   // the .meta (GUID) stays
        AssetDatabase.ImportAsset(path);
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        foreach (var t in all) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        var byPath = new Dictionary<string, Transform>();
        foreach (var t in all)
        {
            var p = PathOf(t);
            if (!byPath.ContainsKey(p)) byPath[p] = t;
        }
        Transform Find(string p) => byPath.TryGetValue(p, out var t) ? t : throw new Exception($"[AnimationUISampleBuilder] {data.name}: no object '{p}'");

        // The new Input System only: the original StandaloneInputModule is replaced.
        foreach (var es in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include))
        {
            foreach (var module in es.GetComponents<StandaloneInputModule>()) Object.DestroyImmediate(module);
            if (!es.GetComponent<InputSystemUIInputModule>()) es.gameObject.AddComponent<InputSystemUIInputModule>();
        }
        foreach (var r in Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include)) r.sharedMaterial = particles;

        var blocker = InputBlocker();
        foreach (var anim in data.animations) BuildSequence(anim, Find, blocker, clips);
        foreach (var button in data.buttons) BuildButton(button, Find, clips);
        foreach (var click in data.clicks) Wire(click, Find);

        EditorSceneManager.SaveScene(scene, path);
        return path;
    }

    // A missing component is Unity's fake null, which the null-coalescing operator treats as present.
    static T Get<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

    // The original's "SetActiveAllInput": one full-screen raycast catcher above everything, on while input is off.
    static GameObject InputBlocker()
    {
        var canvas = new GameObject("InputBlocker", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.GetComponent<Canvas>().sortingOrder = 100;
        var block = new GameObject("Block", typeof(RectTransform), typeof(Image));
        block.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)block.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        block.GetComponent<Image>().color = Color.clear;
        block.SetActive(false);
        return block;
    }

    // ---------- one AnimationUI sequence → one UIAnimation state ----------

    static void BuildSequence(AnimData data, Func<string, Transform> find, GameObject blocker, AudioClip[] clips)
    {
        var owner = find(data.owner).gameObject;
        var stateName = owner.name;
        var state = new UIAnimState { name = stateName };
        var tracks = new Dictionary<(Component, UIAnimProperty), UIAnimTrack>();

        UIAnimTrack Track(Component target, UIAnimProperty property)
        {
            if (tracks.TryGetValue((target, property), out var track)) return track;
            track = new UIAnimTrack { target = target, property = property, start = UIAnimStart.KeyValue };
            tracks[(target, property)] = track;
            state.tracks.Add(track);
            return track;
        }
        // A key that changes nothing until `time` (the value before it), then the move to `to`.
        void Tween(UIAnimTrack track, float time, float duration, Vector4 from, Vector4 to, UIAnimEase ease)
        {
            if (track.keys.All(k => !Mathf.Approximately(k.time, time))) track.keys.Add(new UIAnimKey { time = time, value = from, ease = UIAnimEase.Linear });
            track.keys.Add(new UIAnimKey { time = time + duration, value = to, ease = ease });
        }
        void Switch(UIAnimTrack track, float time, bool on, bool initial)
        {
            if (track.keys.Count == 0) track.keys.Add(new UIAnimKey { time = 0f, value = new Vector4(initial ? 1 : 0, 0, 0, 0) });
            track.keys.Add(new UIAnimKey { time = time, value = new Vector4(on ? 1 : 0, 0, 0, 0) });
        }

        UIAnimSoundTrack sounds = null;
        UIAnimEventTrack events = null;
        foreach (var step in data.steps)
        {
            switch (step.type)
            {
                case "Animation":
                {
                    var target = find(step.target);
                    foreach (var prop in step.props)
                    {
                        var property = (UIAnimProperty)Enum.Parse(typeof(UIAnimProperty), prop.property);
                        Tween(Track(Component(target, step.targetKind, property), property), step.time, step.duration, prop.from, prop.to, Ease(step.ease));
                    }
                    break;
                }
                case "SetActive":
                {
                    var target = find(step.target);
                    Switch(Track(target, UIAnimProperty.Active), step.time, step.active, target.gameObject.activeSelf);
                    break;
                }
                case "SetActiveAllInput":
                    Switch(Track(blocker.transform, UIAnimProperty.Active), step.time, !step.active, false);
                    break;
                case "SFX":
                    if (sounds == null) state.soundTracks.Add(sounds = new UIAnimSoundTrack());
                    sounds.sounds.Add(new UIAnimSound { time = step.time, clip = clips[step.sfx], volume = 1f });
                    break;
                case "LoadScene":
                {
                    if (events == null) state.eventTracks.Add(events = new UIAnimEventTrack());
                    events.events.Add(new UIAnimEventMarker { time = step.time, name = "LoadScene" });
                    var loader = Get<AuiSceneLoader>(owner);
                    var listener = owner.AddComponent<UIAnimEventListener>();
                    var binding = new UIAnimEventListener.Binding { eventName = "LoadScene" };
                    UnityEventTools.AddStringPersistentListener(binding.onRaised, loader.Load, step.scene);
                    listener.bindings.Add(binding);
                    break;
                }
            }
        }
        foreach (var track in state.tracks)
        {
            track.keys.Sort((a, b) => a.time.CompareTo(b.time));
            UIAnimationEditorWindow.EnsureStartKey(track);
        }

        var anim = owner.AddComponent<UIAnimation>();
        anim.states.Add(state);
        foreach (var listener in owner.GetComponents<UIAnimEventListener>()) listener.source = anim;
        if (data.playOnStart)
            owner.AddComponent<UIAnimTrigger>().bindings.Add(new UIAnimBinding { evt = UIAnimEvent.Start, state = stateName });
    }

    static Component Component(Transform target, string kind, UIAnimProperty property) => kind switch
    {
        "Image" => target.GetComponent<Image>(),
        "CanvasGroup" => target.GetComponent<CanvasGroup>(),
        "Camera" => target.GetComponent<Camera>(),
        "TextMeshPro" => target.GetComponent<TMP_Text>(),
        _ => target,
    };

    // "InOutQuart" etc. are our names too; AnimationUI's Linear ignores In/Out.
    static UIAnimEase Ease(string name) => name.EndsWith("Linear") ? UIAnimEase.Linear : (UIAnimEase)Enum.Parse(typeof(UIAnimEase), name);

    // ---------- ButtonUI → Button + UIAnimation + UIAnimTrigger ----------

    static void BuildButton(ButtonData data, Func<string, Transform> find, AudioClip[] clips)
    {
        var go = find(data.path).gameObject;
        var graphic = find(data.graphic).GetComponent<Graphic>();
        var text = string.IsNullOrEmpty(data.text) ? null : find(data.text).GetComponent<TMP_Text>();
        var button = Get<Button>(go);
        button.transition = Selectable.Transition.None;
        button.targetGraphic = graphic;

        // Every state goes from where it is now to the target (ButtonUI tweens from the current value), 0.15 s.
        UIAnimState To(string name, float scale, Color color, Color textColor, int sound)
        {
            var state = new UIAnimState { name = name };
            state.tracks.Add(Go(graphic.transform, UIAnimProperty.Scale, Vector3.one * scale, UIAnimEase.OutBackQuart));
            state.tracks.Add(Go(graphic, UIAnimProperty.GraphicColor, color, UIAnimEase.OutQuart));
            if (text != null) state.tracks.Add(Go(text, UIAnimProperty.GraphicColor, textColor, UIAnimEase.OutQuart));
            if (sound >= 0) state.soundTracks.Add(new UIAnimSoundTrack { sounds = { new UIAnimSound { time = 0f, clip = clips[sound] } } });
            return state;
        }
        UIAnimTrack Go(Component target, UIAnimProperty property, Vector4 value, UIAnimEase ease)
        {
            var track = new UIAnimTrack { target = target, property = property, start = UIAnimStart.Current };
            track.keys.Add(new UIAnimKey { time = data.duration, value = value, ease = ease });
            UIAnimationEditorWindow.EnsureStartKey(track);
            return track;
        }
        // Click: from pressed back to highlighted, whatever the current value (ButtonUI's OnPointerClick).
        UIAnimTrack FromTo(Component target, UIAnimProperty property, Vector4 from, Vector4 to, UIAnimEase ease) => new()
        {
            target = target, property = property, start = UIAnimStart.KeyValue,
            keys = { new UIAnimKey { time = 0f, value = from }, new UIAnimKey { time = data.duration, value = to, ease = ease } },
        };

        var click = new UIAnimState { name = "Click" };
        click.tracks.Add(FromTo(graphic.transform, UIAnimProperty.Scale, Vector3.one * data.pressedScale, Vector3.one * data.highlightedScale, UIAnimEase.OutBackQuart));
        click.tracks.Add(FromTo(graphic, UIAnimProperty.GraphicColor, data.pressed, data.highlighted, UIAnimEase.OutQuart));
        if (text != null) click.tracks.Add(FromTo(text, UIAnimProperty.GraphicColor, data.textPressed, data.textHighlighted, UIAnimEase.OutQuart));
        click.soundTracks.Add(new UIAnimSoundTrack { sounds = { new UIAnimSound { time = 0f, clip = clips[2] } } });

        var anim = Get<UIAnimation>(go);
        anim.states.AddRange(new[]
        {
            To("Hover", data.highlightedScale, data.highlighted, data.textHighlighted, 3),
            To("Unhover", data.normalScale, data.normal, data.textNormal, -1),
            To("Press", data.pressedScale, data.pressed, data.textPressed, 0),
            To("Release", data.normalScale, data.normal, data.textNormal, -1),
            To("Select", data.selectedScale, data.selected, data.textSelected, 3),
            To("Deselect", data.normalScale, data.normal, data.textNormal, -1),
            click,
        });
        var trigger = Get<UIAnimTrigger>(go);
        foreach (var (evt, state) in new[]
                 {
                     (UIAnimEvent.PointerEnter, "Hover"), (UIAnimEvent.PointerExit, "Unhover"), (UIAnimEvent.PointerDown, "Press"),
                     (UIAnimEvent.PointerUp, "Release"), (UIAnimEvent.Select, "Select"), (UIAnimEvent.Deselect, "Deselect"), (UIAnimEvent.Click, "Click"),
                 })
            trigger.bindings.Add(new UIAnimBinding { evt = evt, state = state });
    }

    // ---------- the original's click wiring, pointed at the new components ----------

    static void Wire(ClickData data, Func<string, Transform> find)
    {
        var button = find(data.button).GetComponent<Button>();
        var target = find(data.target);
        switch (data.method)
        {
            case "Play":
                UnityEventTools.AddStringPersistentListener(button.onClick, target.GetComponent<UIAnimation>().PlayState, target.name);
                break;
            case "PlayReversed":
                UnityEventTools.AddStringPersistentListener(button.onClick, target.GetComponent<UIAnimation>().PlayStateReversed, target.name);
                break;
            case "SetActive":
                UnityEventTools.AddBoolPersistentListener(button.onClick, target.gameObject.SetActive, data.boolArgument);
                break;
            default:
                throw new Exception($"[AnimationUISampleBuilder] unknown click method {data.method}");
        }
    }

    // ---------- shared assets ----------

    // The original's legacy particle material renders pink under URP: same texture on a shader URP draws as is.
    static Material ParticleMaterial()
    {
        var path = Root + "Art/Squares.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Sprites/Default")) { mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/Square.png") };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static void AddToBuildSettings(List<string> scenes)
    {
        var list = EditorBuildSettings.scenes.ToList();
        foreach (var s in scenes)
            if (list.All(e => e.path != s)) list.Add(new EditorBuildSettingsScene(s, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
