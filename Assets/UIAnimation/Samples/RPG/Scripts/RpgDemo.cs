using System;
using System.Linq;
using SeoBYP.TimelineAnimation;
using UnityEngine;

/// <summary>
/// RPG sample: a lobby and the screens a mobile RPG opens from it (heroes, bag, summon, adventure, quests), each a page
/// with its own transitions, plus popups (item info, summon results, battle results). All the motion is UIAnimation
/// states on the prefabs; this class only opens and closes screens and holds the player's currencies.
/// </summary>
public sealed class RpgDemo : MonoBehaviour
{
    public static RpgDemo Instance { get; private set; }

    public UIAnimPageContainer pages;
    public UIAnimModalContainer modals;
    public UIAnimScreen lobby, heroes, heroDetail, bag, summon, adventure, quests;
    public UIAnimScreen itemInfo, summonResult, victory, defeat;
    [Tooltip("Icons, hero portraits … looked up by name.")]
    public Sprite[] sprites;

    public int Gold { get; private set; } = 12450;
    public int Gems { get; private set; } = 3800;
    public int Stamina { get; private set; } = 60;
    public const int MaxStamina = 60, StageStamina = 6, SummonCost = 300;
    public event Action CurrencyChanged;

    // What the screen being opened shows.
    public int SelectedHero { get; private set; }
    public RectTransform SelectedPortrait { get; private set; }
    public RpgData.Item SelectedItem { get; private set; }
    public RectTransform SelectedIcon { get; private set; }
    public int SummonCount { get; private set; }
    public (int chapter, int stage) SelectedStage { get; private set; }

    static bool Busy => UIAnimScreenContainer.AnyInTransition;

    void Awake() => Instance = this;
    async void Start() => await pages.Push(lobby);

    public Sprite Sprite(string spriteName) => sprites.FirstOrDefault(s => s.name == spriteName);

    public void Add(int gold = 0, int gems = 0, int stamina = 0)
    {
        Gold += gold;
        Gems += gems;
        Stamina = Mathf.Clamp(Stamina + stamina, 0, 999);
        CurrencyChanged?.Invoke();
    }

    public async void Open(UIAnimScreen page)
    {
        if (Busy) return;
        await pages.Push(page);
    }

    /// <summary>Closes the top popup, or goes back a page.</summary>
    public async void Back()
    {
        if (Busy) return;
        if (modals.Modals.Count > 0) await modals.Pop();
        else if (pages.Pages.Count > 1) await pages.Pop();
    }

    public async void ToLobby()
    {
        if (Busy) return;
        while (modals.Modals.Count > 0) await modals.Pop();
        while (pages.Pages.Count > 1) await pages.Pop(animated: pages.Pages.Count == 2);   // only the last step is animated
    }

    public void ShowHero(int index, RectTransform portrait)
    {
        if (Busy) return;
        SelectedHero = index;
        SelectedPortrait = portrait;
        Open(heroDetail);
    }

    public void ShowItem(RpgData.Item item, RectTransform icon)
    {
        if (Busy) return;
        SelectedItem = item;
        SelectedIcon = icon;
        Popup(itemInfo);
    }

    public bool Summon(int count)
    {
        if (Busy || Gems < count * SummonCost) return false;
        Add(gems: -count * SummonCost);
        SummonCount = count;
        Popup(summonResult);
        return true;
    }

    /// <summary>A battle is not part of this sample: the boss stage (the 10th) is lost, the others are won.</summary>
    public bool StartStage(int chapter, int stage)
    {
        if (Busy || Stamina < StageStamina) return false;
        Add(stamina: -StageStamina);
        SelectedStage = (chapter, stage);
        var won = stage < 9;
        if (won) Add(gold: 250 + chapter * 100);
        Popup(won ? victory : defeat);
        return true;
    }

    async void Popup(UIAnimScreen modal) => await modals.Push(modal);
}
