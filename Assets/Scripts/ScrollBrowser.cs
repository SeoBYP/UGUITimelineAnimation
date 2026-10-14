using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dev sample: the Scroll scene's pager. Each page is its own prefab (one scroll form), swiped one per drag;
/// the dots, the counter and Prev / Next follow the current page. The toolbar changes the current page's item count
/// (slider, Add, Remove) and opens its animation in the UI Animation Editor (Edit or Play mode).
/// </summary>
public sealed class ScrollBrowser : MonoBehaviour
{
    public UIAnimScrollList pages;
    public TMP_Text counter;
    public Slider itemCount;
    public TMP_Text itemCountLabel;

    void Start()
    {
        pages.prefabOf = i => i;   // page i is prefab i (item prefab, then the extra prefabs)
        pages.CurrentIndexChanged += i =>
        {
            counter.text = $"{i + 1} / {pages.Count}";
            ShowCount();
        };
        itemCount.maxValue = ScrollDemoPage.MaxCount;
        itemCount.wholeNumbers = true;
        itemCount.onValueChanged.AddListener(v =>
        {
            Current?.SetItemCount((int)v);
            ShowCount();
        });
        pages.SetCount(pages.extraPrefabs.Count + 1, (RectTransform _, int _) => { });
        ShowCount();
    }

    ScrollDemoPage Current => pages.GetItem<ScrollDemoPage>(pages.CurrentIndex);

    int _shown = -1;

    // Pages also change their own count (chat answers, pull to refresh, Send): follow it.
    void Update()
    {
        var page = Current;
        if (page != null && page.Count != _shown) ShowCount();
    }

    void ShowCount()
    {
        var page = Current;
        if (page == null) return;
        _shown = page.Count;
        itemCount.SetValueWithoutNotify(page.Count);
        itemCountLabel.text = $"Items {page.Count}";
    }

    public void Next() => pages.ScrollTo(Mathf.Min(pages.CurrentIndex + 1, pages.Count - 1), pages.snapAlignment, pages.snapDuration);

    public void Previous() => pages.ScrollTo(Mathf.Max(pages.CurrentIndex - 1, 0), pages.snapAlignment, pages.snapDuration);

    public void AddItem()
    {
        Current?.Add();
        ShowCount();
    }

    public void RemoveItem()
    {
        Current?.Remove();
        ShowCount();
    }

    /// <summary>
    /// Opens the current page's animation in the UI Animation Editor. Objects of the live page are mapped to the page prefab,
    /// so edits go to the prefab (in Play mode they show on items created after the edit).
    /// </summary>
    public void EditAnimation()
    {
#if UNITY_EDITOR
        var page = Current;
        if (page == null) return;
        var (owner, state) = page.EditTarget;
        if (owner.transform.IsChildOf(page.transform))
        {
            var index = pages.CurrentIndex;
            var source = index == 0 ? pages.itemPrefab : pages.extraPrefabs[index - 1];
            owner = source.Find(UnityEditor.AnimationUtility.CalculateTransformPath(owner.transform, page.transform));
        }
        // The editor assembly is not visible to sample scripts: called by name.
        var window = System.Type.GetType("SeoBYP.TimelineAnimation.Editor.UIAnimationEditorWindow, SeoBYP.TimelineAnimation.Editor");
        window?.GetMethod("OpenState")?.Invoke(null, new object[] { owner, state, false });
#endif
    }
}
