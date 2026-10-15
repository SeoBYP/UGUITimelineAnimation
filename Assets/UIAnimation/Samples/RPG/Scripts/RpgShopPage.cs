using System.Collections.Generic;
using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;

/// <summary>
/// Shop: a deal banner with a running countdown and a shine that sweeps across it (looping state), and a grid of offers
/// (scroll list) that enter one after another. Tapping an offer opens the purchase popup; a bought-out card plays "SoldOut".
/// </summary>
public sealed class RpgShopPage : MonoBehaviour
{
    public UIAnimScrollList list;
    public TMP_Text countdown;
    [Tooltip("Seconds left on the deal when the page opens.")]
    public float dealSeconds = 5 * 3600 + 23 * 60 + 7;

    List<RpgData.Offer> _offers;
    float _left;

    void Awake()
    {
        _offers = RpgDemo.Instance.Offers;
        list.SetData(_offers, (RpgShopCard card, RpgData.Offer o, int _) => card.Set(o));
    }

    void OnEnable() => _left = dealSeconds;

    void Update()
    {
        _left = Mathf.Max(0f, _left - Time.deltaTime);
        var s = Mathf.CeilToInt(_left);
        countdown.text = $"{s / 3600:00}:{s / 60 % 60:00}:{s % 60:00}";
    }

    /// <summary>After a purchase: refresh the bought card (and play SoldOut on it when none are left).</summary>
    public void Bought(RpgData.Offer offer)
    {
        var index = _offers.IndexOf(offer);
        var item = list.GetItem(index);
        if (item == null) return;
        var card = item.GetComponent<RpgShopCard>();
        card.Set(offer);
        item.GetComponent<UIAnimation>().Play(offer.stock == 0 ? "SoldOut" : "Bought");
    }
}
