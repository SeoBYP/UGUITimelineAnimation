using UnityEngine;

/// <summary>One tab's 8 slots: the first shows the tab's item, the rest are locked.</summary>
public sealed class UsnShopItemSet : MonoBehaviour
{
    public UsnShopItem[] slots;
    [Tooltip("Thumbnails of the three items (Regular, Special, Sale).")]
    public Sprite[] thumbnails;
    static readonly int[] Costs = { 1000, 10000, 500 };   // master data ShopItem 0001..0003

    public void Show(int tab)
    {
        for (var i = 0; i < slots.Length; i++)
            if (i == 0) slots[i].Show(thumbnails[tab], Costs[tab]);
            else slots[i].ShowLocked();
    }
}
