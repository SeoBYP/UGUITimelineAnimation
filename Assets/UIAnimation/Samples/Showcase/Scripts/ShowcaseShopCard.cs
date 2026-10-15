using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A shop item: tapping it opens the item detail, whose icon grows out of this card's icon.</summary>
public sealed class ShowcaseShopCard : MonoBehaviour
{
    public Image icon;
    public TMP_Text itemName, price;
    [TextArea] public string description;

    void Awake() => GetComponent<Button>().onClick.AddListener(() => ShowcaseDemo.Instance.ShowItem(this));
}
