using SeoBYP.TimelineAnimation;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Settings modal: VOICE / BGM / SE (toggle + volume), saved to PlayerPrefs on exit if anything changed; LOCKED buttons open a confirmation.</summary>
public sealed class UsnSettingsModal : MonoBehaviour, IUIAnimScreenLifecycle
{
    public Button close;
    public Button[] lockedButtons;
    [Tooltip("VOICE, BGM, SE")]
    public Toggle[] toggles;
    public Slider[] sliders;
    static readonly string[] Keys = { "Demo_Voice", "Demo_Bgm", "Demo_Se" };   // + "Volume" / "Muted", as the original
    bool _dirty;

    void Awake()
    {
        close.onClick.AddListener(() => UsnDemo.Instance.Back());
        foreach (var b in lockedButtons) b.onClick.AddListener(() => UsnDemo.Instance.ShowLockedConfirmation());
        for (var i = 0; i < Keys.Length; i++)
        {
            var slider = sliders[i];
            slider.value = PlayerPrefs.GetFloat(Keys[i] + "Volume", 1f);
            toggles[i].isOn = PlayerPrefs.GetInt(Keys[i] + "Muted", 0) == 0;
            slider.interactable = toggles[i].isOn;
            slider.onValueChanged.AddListener(_ => _dirty = true);
            toggles[i].onValueChanged.AddListener(on =>
            {
                slider.interactable = on;
                _dirty = true;
            });
        }
    }

    public Awaitable WillExit(UIAnimTransition t)
    {
        if (_dirty)
            for (var i = 0; i < Keys.Length; i++)
            {
                PlayerPrefs.SetFloat(Keys[i] + "Volume", sliders[i].value);
                PlayerPrefs.SetInt(Keys[i] + "Muted", toggles[i].isOn ? 0 : 1);
            }
        return UIAnimScreen.Completed();
    }
}
