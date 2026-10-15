using SeoBYP.TimelineAnimation;
using UnityEngine;

/// <summary>
/// UnityScreenNavigator demo rebuilt with UI Animation: Top → Loading → Home → Shop on a page container.
/// Pages are prefabs with UIAnimScreen; their transitions are UIAnimation states ("PushEnter", "PushExit", …).
/// No R3 / UniTask: Unity's Awaitable only.
/// </summary>
public sealed class UsnDemo : MonoBehaviour
{
    public static UsnDemo Instance { get; private set; }

    public UIAnimPageContainer pages;
    public UIAnimModalContainer modals;
    [Tooltip("The \"Connecting...\" overlay shown while buying.")]
    public CanvasGroup connecting;
    public UIAnimScreen topPage, loadingPage, homePage, shopPage;
    public UIAnimScreen settingsModal, confirmationModal, unitInfoModal, portraitViewerModal;

    /// <summary>The unit the information modal shows (0..2: 003, 004, 005) and the portrait the viewer shows.</summary>
    public int SelectedUnit { get; private set; }
    public Sprite SelectedPortrait { get; private set; }

    void Awake() => Instance = this;

    async void Start() => await pages.Push(topPage, animated: false);

    static bool Busy => UIAnimScreenContainer.AnyInTransition;

    /// <summary>Top tapped: the loading page, dropped from the history once Home is in (so Back from Home goes to Top).</summary>
    public async void ShowLoading()
    {
        if (Busy) return;
        await pages.Push(loadingPage, stack: false);
    }

    public async void ShowHome() => await pages.Push(homePage);

    public async void ShowShop()
    {
        if (Busy) return;
        await pages.Push(shopPage);
    }

    public void ShowSettings() => ShowModal(settingsModal);
    public void ShowLockedConfirmation() => ShowModal(confirmationModal);

    public void ShowUnitInfo(int unit)
    {
        if (Busy) return;
        SelectedUnit = unit;
        ShowModal(unitInfoModal);
    }

    public void ShowPortrait(Sprite portrait)
    {
        if (Busy) return;
        SelectedPortrait = portrait;
        ShowModal(portraitViewerModal);
    }

    async void ShowModal(UIAnimScreen modal)
    {
        if (Busy) return;
        await modals.Push(modal);
    }

    /// <summary>Closes the top modal, or goes back a page when there is none.</summary>
    public async void Back()
    {
        if (Busy) return;
        if (modals.Modals.Count > 0) await modals.Pop();
        else await pages.Pop();
    }

    /// <summary>The original's fake API call: the overlay for a second.</summary>
    public async Awaitable Connect(float seconds = 1f)
    {
        connecting.alpha = 1f;
        connecting.blocksRaycasts = true;
        await Awaitable.WaitForSecondsAsync(seconds);
        connecting.alpha = 0f;
        connecting.blocksRaycasts = false;
    }
}
