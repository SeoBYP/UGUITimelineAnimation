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
    int _unit;

    void Awake()
    {
        buy.onClick.AddListener(Buy);
        thumbnail.GetComponent<Button>().onClick.AddListener(() => UsnDemo.Instance.ShowUnitInfo(_unit));
    }

    public void ShowLocked()
    {
        locked.SetActive(true);
        unlocked.SetActive(false);
    }

    public void Show(int unit, Sprite sprite, int price)
    {
        _unit = unit;
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
