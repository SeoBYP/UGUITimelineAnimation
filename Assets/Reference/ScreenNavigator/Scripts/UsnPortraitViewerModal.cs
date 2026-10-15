using SeoBYP.TimelineAnimation;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-size portrait with a shared element transition: the portrait grows out of the unit information modal's portrait
/// area into its own place, and shrinks back into it on close (the original's UnitPortraitModalTransitionAnimation).
/// The motion is the "Enter" / "Exit" states (0.3 s OutQuart, editable in the UI Animation window); only the partner's
/// end of each — where its portrait area is on screen — is filled in here, before they play.
/// </summary>
public sealed class UsnPortraitViewerModal : MonoBehaviour, IUIAnimScreenLifecycle
{
    public Image portrait;
    public Button close;

    void Awake() => close.onClick.AddListener(() => UsnDemo.Instance.Back());

    public Awaitable WillEnter(UIAnimTransition t)
    {
        portrait.sprite = UsnDemo.Instance.SelectedPortrait;
        if (Partner(t) is { } area)
        {
            SetPartnerKeys("Enter", 0, area);
            area.gameObject.SetActive(false);   // the portrait leaves the partner
        }
        return UIAnimScreen.Completed();
    }

    public Awaitable WillExit(UIAnimTransition t)
    {
        if (Partner(t) is { } area) SetPartnerKeys("Exit", -1, area);
        return UIAnimScreen.Completed();
    }

    public void DidExit(UIAnimTransition t)
    {
        if (Partner(t) is { } area) area.gameObject.SetActive(true);   // and lands back in it
    }

    static RectTransform Partner(UIAnimTransition t) =>
        t.Partner != null && t.Partner.TryGetComponent(out UsnUnitInfoModal info) ? info.PortraitArea : null;

    void SetPartnerKeys(string state, int key, RectTransform area) =>
        UIAnimSharedElement.SetPartnerKeys(GetComponent<UIAnimation>(), state, portrait.rectTransform, area, atStart: key == 0);
}
