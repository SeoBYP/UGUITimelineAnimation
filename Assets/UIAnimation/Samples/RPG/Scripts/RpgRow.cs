using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A list item view with the usual parts (any may be empty): banners, stat rows, skill rows, section headers.</summary>
public sealed class RpgRow : MonoBehaviour
{
    public Image icon;
    public TMP_Text title, text, value;
    [Tooltip("A gauge fill, anchored at its left: its width is set as a share of the bar.")]
    public RectTransform fill;

    public void SetFill(float share)
    {
        var max = fill.anchorMax;
        max.x = Mathf.Clamp01(share);
        fill.anchorMax = max;
    }
}
