using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>An offer in the shop grid: icon, name, price, a badge with the stock left. Sold out shows a stamp and can't be tapped.</summary>
public sealed class RpgShopCard : MonoBehaviour
{
    public Image icon, currency;
    public TMP_Text title, price, stock;
    public Button buy;

    RpgData.Offer _offer;

    void Awake() => buy.onClick.AddListener(() => RpgDemo.Instance.AskPurchase(_offer));

    public void Set(RpgData.Offer o)
    {
        _offer = o;
        var demo = RpgDemo.Instance;
        icon.sprite = demo.Sprite(o.icon);
        currency.sprite = demo.Sprite(o.currency);
        title.text = o.name;
        price.text = o.price.ToString("N0");
        stock.transform.parent.gameObject.SetActive(o.stock > 0);   // the "N LEFT" badge: limited offers only
        stock.text = $"{o.stock} LEFT";
        buy.interactable = o.stock != 0;
        GetComponent<UIAnimation>().Sample("SoldOut", o.stock == 0 ? 1f : 0f);   // a reused card may show another offer's stamp
    }
}
