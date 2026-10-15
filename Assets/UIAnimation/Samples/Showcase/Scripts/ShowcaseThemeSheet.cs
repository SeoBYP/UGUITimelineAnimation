using UnityEngine;
using UnityEngine.UI;

/// <summary>Theme tab: one button per theme; the current one cannot be pressed.</summary>
public sealed class ShowcaseThemeSheet : MonoBehaviour
{
    public Button[] themes;

    void Awake()
    {
        for (var i = 0; i < themes.Length; i++)
        {
            var index = i;
            themes[i].onClick.AddListener(() => ShowcaseDemo.Instance.SetTheme(index));
        }
    }

    void OnEnable()
    {
        for (var i = 0; i < themes.Length; i++) themes[i].interactable = i != ShowcaseDemo.Instance.ThemeIndex;
    }
}
