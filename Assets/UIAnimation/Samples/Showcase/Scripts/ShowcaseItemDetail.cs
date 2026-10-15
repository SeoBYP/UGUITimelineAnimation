using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Item detail with a shared element: the icon grows out of the tapped shop card's icon into its place here and
/// shrinks back into the card on close. The motion is the "Enter" / "Exit" states (edit them in the UI Animation
/// window); only the card's end is filled in here, just before they play.
/// </summary>
public sealed class ShowcaseItemDetail : MonoBehaviour, IUIAnimScreenLifecycle
{
    public Image icon;
    public TMP_Text itemName, description, price;
    public Button close, buy;

    ShowcaseShopCard _card;

    void Awake()
    {
        close.onClick.AddListener(() => ShowcaseDemo.Instance.Close());
        buy.onClick.AddListener(() => ShowcaseDemo.Instance.Close());
    }

    public Awaitable WillEnter(UIAnimTransition t)
    {
        _card = ShowcaseDemo.Instance.SelectedCard;
        icon.sprite = _card.icon.sprite;
        itemName.text = _card.itemName.text;
        description.text = _card.description;
        price.text = _card.price.text;
        UIAnimSharedElement.SetPartnerKeys(GetComponent<UIAnimation>(), "Enter", icon.rectTransform, _card.icon.rectTransform, atStart: true);
        _card.icon.enabled = false;   // the icon leaves the card…
        return UIAnimScreen.Completed();
    }

    public Awaitable WillExit(UIAnimTransition t)
    {
        UIAnimSharedElement.SetPartnerKeys(GetComponent<UIAnimation>(), "Exit", icon.rectTransform, _card.icon.rectTransform, atStart: false);
        return UIAnimScreen.Completed();
    }

    public void DidExit(UIAnimTransition t) => _card.icon.enabled = true;   // …and lands back in it
}
