using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A bag slot: frame colored by rarity, icon, count. Tapping it opens the item info, whose icon grows out of this one.</summary>
public sealed class RpgItemSlot : MonoBehaviour
{
    public Image frame, icon;
    public TMP_Text count;

    RpgData.Item _item;

    void Awake() => GetComponentInChildren<Button>().onClick.AddListener(() => RpgDemo.Instance.ShowItem(_item, icon.rectTransform));

    public void Set(RpgData.Item item)
    {
        _item = item;
        frame.color = RpgData.RarityColor(item.rarity);
        icon.sprite = RpgDemo.Instance.Sprite(item.icon);
        icon.enabled = true;
        count.text = item.count > 1 ? "x" + item.count : "";
    }
}
