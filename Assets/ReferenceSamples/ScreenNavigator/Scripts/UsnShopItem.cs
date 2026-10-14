using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A shop slot: locked, or an item with its cost and a Buy button (Connecting… then Sold Out).</summary>
public sealed class UsnShopItem : MonoBehaviour
{
    public GameObject locked, unlocked;
    public Image thumbnail;
    public TMP_Text cost, buyLabel;
    public Button buy;

    void Awake() => buy.onClick.AddListener(Buy);

    public void ShowLocked()
    {
        locked.SetActive(true);
        unlocked.SetActive(false);
    }

    public void Show(Sprite sprite, int price)
    {
        locked.SetActive(false);
        unlocked.SetActive(true);
        thumbnail.sprite = sprite;
        cost.text = price.ToString();
    }

    async void Buy()
    {
        buy.interactable = false;
        await UsnDemo.Instance.Connect();
        buyLabel.text = "Sold Out";
    }
}
