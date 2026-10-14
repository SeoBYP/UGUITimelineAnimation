using TMPro;
using UnityEngine;

/// <summary>Dev sample: what a scroll list item shows. Bound by ScrollSample through UIAnimScrollList.</summary>
public sealed class ScrollItemView : MonoBehaviour
{
    public TMP_Text title;
    public TMP_Text index;

    public void Set(string text, int i)
    {
        title.text = text;
        if (index != null) index.text = $"#{i + 1}";
    }
}
