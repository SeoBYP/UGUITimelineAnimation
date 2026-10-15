using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The lobby page: a tab bar whose indicator moves to the chosen tab (states "Tab0".."Tab2" on the page), the tab
/// contents in a sheet container, and a toast that drops in and goes back the same way (its "Show" state played reversed).
/// </summary>
public sealed class ShowcaseLobby : MonoBehaviour
{
    public Button[] tabs;
    public UIAnimSheetContainer sheets;
    [Tooltip("Home, Shop, Theme")]
    public UIAnimScreen[] sheetPrefabs;
    public UIAnimation toast;
    public TMP_Text toastText;
    [Min(0)] public float toastSeconds = 1.5f;

    UIAnimation _animation;
    bool _toasting;

    void Awake()
    {
        _animation = GetComponent<UIAnimation>();
        foreach (var prefab in sheetPrefabs) sheets.Register(prefab);
        for (var i = 0; i < tabs.Length; i++)
        {
            var index = i;
            tabs[i].onClick.AddListener(() => Select(index));
        }
    }

    async void Start() => await sheets.Show(0, animated: false);

    async void Select(int index)
    {
        if (UIAnimScreenContainer.AnyInTransition || index == sheets.ActiveIndex) return;
        _animation.Play("Tab" + index);
        await sheets.Show(index);
    }

    public async void Toast(string message)
    {
        if (_toasting) return;
        _toasting = true;
        toastText.text = message;
        await toast.PlayAsync("Show");
        await Awaitable.WaitForSecondsAsync(toastSeconds);
        await toast.PlayReversedAsync("Show");
        _toasting = false;
    }
}
