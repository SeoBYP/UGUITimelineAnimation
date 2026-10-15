using System.Linq;
using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Purchase popup: the offer pops up ("Enter"). BUY pays and closes, and the shop card plays its "Bought"/"SoldOut";
/// short of the currency, the price shakes red ("Deny").
/// </summary>
public sealed class RpgPurchase : MonoBehaviour, IUIAnimScreenLifecycle
{
    public Image icon, currency;
    public TMP_Text title, price, gives;
    public Button buy, cancel;

    void Awake()
    {
        cancel.onClick.AddListener(() => RpgDemo.Instance.Back());
        buy.onClick.AddListener(Buy);
    }

    public Awaitable WillEnter(UIAnimTransition t)
    {
        var demo = RpgDemo.Instance;
        var o = demo.SelectedOffer;
        icon.sprite = demo.Sprite(o.icon);
        currency.sprite = demo.Sprite(o.currency);
        title.text = o.name;
        price.text = o.price.ToString("N0");
        gives.text = string.Join("   ", new[]
        {
            o.gold > 0 ? $"+{o.gold:N0} gold" : null, o.gems > 0 ? $"+{o.gems:N0} gems" : null, o.stamina > 0 ? $"+{o.stamina} stamina" : null,
        }.Where(s => s != null));
        return UIAnimScreen.Completed();
    }

    void Buy()
    {
        var demo = RpgDemo.Instance;
        if (!demo.Buy())
        {
            GetComponent<UIAnimation>().Play("Deny");
            return;
        }
        var shop = FindAnyObjectByType<RpgShopPage>();
        demo.Back();
        if (shop != null) shop.Bought(demo.SelectedOffer);
    }
}
