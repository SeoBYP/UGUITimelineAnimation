using System;
using SeoBYP.TimelineAnimation;
using UnityEngine;

/// <summary>
/// UI Animation showcase: one lobby page with tabs (Home / Shop / Theme) and three modals (item detail, reward, level up),
/// built three times in different styles. Each theme is the same screens with different art and different motion
/// (Casual: bouncy, SciFi: fast and digital, Fantasy: slow and soft) — only the UIAnimation states differ, not the code.
/// </summary>
public sealed class ShowcaseDemo : MonoBehaviour
{
    [Serializable]
    public sealed class Theme
    {
        public string name;
        public UIAnimScreen lobby, itemDetail, reward, levelUp;
    }

    public static ShowcaseDemo Instance { get; private set; }

    public UIAnimPageContainer pages;
    public UIAnimModalContainer modals;
    public Theme[] themes;

    public int ThemeIndex { get; private set; }
    /// <summary>The shop card whose item the detail modal shows (and whose icon it grows out of).</summary>
    public ShowcaseShopCard SelectedCard { get; private set; }

    static bool Busy => UIAnimScreenContainer.AnyInTransition;
    Theme Current => themes[ThemeIndex];

    void Awake() => Instance = this;

    // Each lobby is pushed without a history entry, so a theme change replaces it.
    async void Start() => await pages.Push(Current.lobby, animated: true, stack: false);

    public async void SetTheme(int index)
    {
        if (Busy || index == ThemeIndex) return;
        ThemeIndex = index;
        await pages.Push(Current.lobby, stack: false);
    }

    public void ShowItem(ShowcaseShopCard card)
    {
        if (Busy) return;
        SelectedCard = card;
        Show(Current.itemDetail);
    }

    public void ShowReward() => Show(Current.reward);
    public void ShowLevelUp() => Show(Current.levelUp);

    async void Show(UIAnimScreen modal)
    {
        if (Busy) return;
        await modals.Push(modal);
    }

    public async void Close()
    {
        if (Busy) return;
        await modals.Pop();
    }
}
