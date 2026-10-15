using SeoBYP.TimelineAnimation;
using UnityEngine;

/// <summary>
/// The bag: tabs (UIAnimPageIndicator, On/Off underline) over a horizontal pager (scroll list, one page per swipe),
/// each page a grid of items. Swipe or tap a tab to switch.
/// </summary>
public sealed class RpgBagPage : MonoBehaviour
{
    public UIAnimScrollList pager;

    void Awake()
    {
        pager.sizeOf = _ => ((RectTransform)pager.transform).rect.width;   // a page is as wide as the pager
        pager.SetCount(RpgData.Tabs.Length, (RpgBagTab page, int tab) => page.Show(tab));
    }
}
