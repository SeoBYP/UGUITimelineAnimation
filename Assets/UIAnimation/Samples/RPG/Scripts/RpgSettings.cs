using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings popup: two switches (the knob slides and the track fills: "On" / "Off" on each switch), two volume sliders
/// whose value follows the handle, and RESET DATA, which asks first (the common confirm popup) and then says so (a toast).
/// </summary>
public sealed class RpgSettings : MonoBehaviour, IUIAnimScreenLifecycle
{
    public Button music, vibration, reset, close;
    public Slider musicVolume, effectsVolume;
    public TMP_Text musicValue, effectsValue;

    void Awake()
    {
        var demo = RpgDemo.Instance;
        close.onClick.AddListener(() => demo.Back());
        music.onClick.AddListener(() => Set(music, demo.Music = !demo.Music, true));
        vibration.onClick.AddListener(() => Set(vibration, demo.Vibration = !demo.Vibration, true));
        musicVolume.onValueChanged.AddListener(v => { demo.MusicVolume = v; musicValue.text = Percent(v); });
        effectsVolume.onValueChanged.AddListener(v => { demo.EffectsVolume = v; effectsValue.text = Percent(v); });
        reset.onClick.AddListener(() => demo.Confirm("RESET DATA", "Gold, gems and stamina go back to the start. Continue?", demo.ResetData));
    }

    static string Percent(float v) => Mathf.RoundToInt(v * 100) + "%";

    public Awaitable WillEnter(UIAnimTransition t)
    {
        var demo = RpgDemo.Instance;
        Set(music, demo.Music, false);
        Set(vibration, demo.Vibration, false);
        musicVolume.SetValueWithoutNotify(demo.MusicVolume);
        effectsVolume.SetValueWithoutNotify(demo.EffectsVolume);
        musicValue.text = Percent(demo.MusicVolume);
        effectsValue.text = Percent(demo.EffectsVolume);
        return UIAnimScreen.Completed();
    }

    // Animated when tapped; snapped to the end when the popup opens.
    static void Set(Button toggle, bool on, bool animated)
    {
        var anim = toggle.GetComponent<UIAnimation>();
        var state = on ? "On" : "Off";
        if (animated) anim.Play(state);
        else anim.Sample(state, anim.Duration(state));
    }
}
