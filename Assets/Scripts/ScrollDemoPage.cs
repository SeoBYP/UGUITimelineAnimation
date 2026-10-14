using System.Collections;
using System.Collections.Generic;
using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dev sample: one page of the Scroll scene, one scroll form. Fills its list when the page is shown;
/// the layout, snapping and animations are set in the inspector (built by SampleContentBuilder).
/// </summary>
public sealed class ScrollDemoPage : MonoBehaviour
{
    public enum Kind
    {
        Vertical, Horizontal, Grid, HorizontalGrid, Carousel, Paging, InfiniteLoop, AutoBanner, WheelPicker,
        StickyHeaders, VariableSize, Chat, Accordion, Masonry, Nested, Tabs, CollapsingHeader, PullToRefresh,
    }

    public Kind kind;
    public string title;
    public UIAnimScrollList list;
    [Tooltip("Picker selection, refresh state …")]
    public TMP_Text status;
    public UIAnimPullToRefresh pull;

    static readonly string[] Words =
    {
        "Hello!", "Are you coming to the meetup tonight?", "Yes, I'll be there around eight.",
        "Great. Bring the slides from last week if you can, we want to go through the animation part again.",
        "Sure.", "The scroll list now measures every bubble, so long messages take as much room as they need and short ones stay small.",
        "Nice!", "See you!",
    };

    readonly List<(bool me, string text)> _messages = new();
    readonly HashSet<int> _open = new();
    int _count = -1, _added;

    void OnEnable()
    {
        if (list == null) return;
        list.CurrentIndexChanged -= OnCurrent;
        list.CurrentIndexChanged += OnCurrent;
        Fill();
    }

    void OnDisable()
    {
        if (list != null) list.CurrentIndexChanged -= OnCurrent;
    }

    /// <summary>How many items the list shows now (kept while the page is away).</summary>
    public int Count => Mathf.Max(0, _count);

    int DefaultCount => kind switch
    {
        Kind.Vertical or Kind.CollapsingHeader => 100,
        Kind.Grid or Kind.HorizontalGrid or Kind.Masonry => 60,
        Kind.Horizontal or Kind.PullToRefresh => 30,
        Kind.Carousel => 20,
        Kind.InfiniteLoop => 8,
        Kind.Paging or Kind.AutoBanner => 5,
        Kind.Tabs => 4,
        Kind.WheelPicker => 24,
        Kind.StickyHeaders => 48,
        Kind.VariableSize => 40,
        Kind.Chat => 12,
        Kind.Accordion => 20,
        Kind.Nested => 12,
        _ => 20,
    };

    void Fill()
    {
        if (_count < 0) _count = DefaultCount;
        SyncMessages();
        switch (kind)
        {
            case Kind.Vertical:
            case Kind.CollapsingHeader:
                list.SetCount<ScrollItemView>(_count, (v, i) => v.Set($"Item {i + 1}", i));
                break;
            case Kind.PullToRefresh:
                list.SetCount<ScrollItemView>(_count, BindRefreshed);
                SetStatus("Pull down to refresh");
                break;
            case Kind.Horizontal:
            case Kind.Grid:
            case Kind.HorizontalGrid:
            case Kind.Masonry:
            case Kind.Carousel:
            case Kind.InfiniteLoop:
                if (kind == Kind.Masonry) list.sizeOf = i => 140 + i * 53 % 4 * 60;
                list.SetCount<ScrollItemView>(_count, (v, i) => v.Set($"{i + 1}", i));
                break;
            case Kind.Paging:
            case Kind.AutoBanner:
                list.SetCount<ScrollItemView>(_count, (v, i) => v.Set(kind == Kind.Paging ? $"Page {i + 1}" : $"Banner {i + 1}", i));
                break;
            case Kind.Tabs:
                string[] tabs = { "Home", "Search", "Inbox", "Profile" };
                list.SetCount<ScrollItemView>(_count, (v, i) => v.Set(i < tabs.Length ? tabs[i] : $"Tab {i + 1}", i));
                break;
            case Kind.WheelPicker:
                list.SetCount<ScrollItemView>(_count, (v, i) => v.Set($"{i:00}:00", i));
                break;
            case Kind.StickyHeaders:
                list.prefabOf = i => i % 6 == 0 ? 1 : 0;   // a header, then five rows
                list.SetCount(_count, (RectTransform item, int i) =>
                {
                    if (i % 6 == 0) item.GetComponentInChildren<TMP_Text>().text = $"Section {(char)('A' + i / 6 % 26)}";
                    else item.GetComponent<ScrollItemView>().Set($"{(char)('A' + i / 6 % 26)}{i % 6}", i);
                });
                break;
            case Kind.VariableSize:
                list.SetCount<ScrollItemView>(_count, (v, i) => v.Set(Words[i * 5 % Words.Length], i));
                break;
            case Kind.Chat:
                list.prefabOf = i => _messages[i].me ? 1 : 0;
                list.SetCount<ScrollItemView>(_count, (v, i) => v.Set(_messages[i].text, i));
                break;
            case Kind.Accordion:
                list.sizeOf = i => _open.Contains(i) ? 300 : 110;
                list.SetCount(_count, (RectTransform item, int i) =>
                {
                    item.GetComponent<ScrollItemView>().Set($"Question {i + 1}", i);
                    item.GetComponent<UIAnimation>().Sample(_open.Contains(i) ? "Open" : "Close", float.MaxValue);
                    var button = item.GetComponent<Button>();
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => Toggle(item, i));
                });
                break;
            case Kind.Nested:
                list.SetCount(_count, (RectTransform row, int i) =>
                {
                    row.GetComponentInChildren<TMP_Text>().text = $"Row {i + 1}";
                    row.GetComponentInChildren<UIAnimScrollList>().SetCount<ScrollItemView>(15, (v, j) => v.Set($"{i + 1}-{j + 1}", j));
                });
                break;
        }
        OnCurrent(list.CurrentIndex);
    }

    // ---------- item count: the scene's slider, Add and Remove ----------

    /// <summary>Shows <paramref name="count"/> items, keeping the scroll position.</summary>
    public void SetItemCount(int count)
    {
        _count = Mathf.Clamp(count, 0, MaxCount);
        SyncMessages();
        list.SetCount(_count);
    }

    public const int MaxCount = 200;

    /// <summary>One more item at the end, scrolled into view (its Appear state plays).</summary>
    public void Add()
    {
        if (_count >= MaxCount) return;
        if (kind == Kind.Chat)
        {
            Send();
            return;
        }
        SetItemCount(_count + 1);
        list.ScrollTo(_count - 1, UIAnimScrollList.Alignment.End, 0.3f);
    }

    /// <summary>The last item goes away.</summary>
    public void Remove() => SetItemCount(_count - 1);

    // The chat's message list follows the count (new ones alternate sides).
    void SyncMessages()
    {
        if (kind != Kind.Chat) return;
        while (_messages.Count < _count) _messages.Add((_messages.Count % 3 == 1, Words[_messages.Count % Words.Length]));
        if (_messages.Count > _count) _messages.RemoveRange(_count, _messages.Count - _count);
    }

    // ---------- what the scene's "Edit Animation" button opens ----------

    /// <summary>The object whose UIAnimation this page shows off, and the state to open (empty = its first state).</summary>
    public (Component owner, string state) EditTarget => kind switch
    {
        Kind.CollapsingHeader => (transform.Find("Demo/Header"), "Collapse"),
        Kind.PullToRefresh => (pull.target, "Pull"),
        Kind.Nested => (list.itemPrefab.GetComponentInChildren<UIAnimScrollList>(true).itemPrefab, "Show"),
        Kind.Accordion => (list.itemPrefab, "Open"),
        _ => (list.itemPrefab, !string.IsNullOrEmpty(list.scrubState) ? list.scrubState
                             : !string.IsNullOrEmpty(list.entryState) ? list.entryState : list.appearState),
    };

    void OnCurrent(int index)
    {
        if (kind == Kind.WheelPicker) SetStatus($"Selected  {index:00}:00");
    }

    void SetStatus(string text)
    {
        if (status != null) status.text = text;
    }

    // ---------- accordion ----------

    void Toggle(RectTransform item, int index)
    {
        var open = !_open.Remove(index);
        if (open) _open.Add(index);
        item.GetComponent<UIAnimation>().Play(open ? "Open" : "Close");
        list.ResizeItem(index, open ? 300 : 110, 0.3f);
    }

    // ---------- chat ----------

    /// <summary>Send button: my message, then an answer.</summary>
    public void Send()
    {
        if (kind != Kind.Chat) return;
        _messages.Add((true, Words[_messages.Count % Words.Length]));
        _count = _messages.Count;
        list.SetCount(_count);
        StartCoroutine(Answer());
    }

    IEnumerator Answer()
    {
        yield return new WaitForSeconds(0.8f);
        _messages.Add((false, Words[(_messages.Count + 3) % Words.Length]));
        _count = _messages.Count;
        list.SetCount(_count);
    }

    // ---------- pull to refresh ----------

    // Newest first: row i shows item number (count - i), so a refresh adds the next numbers on top and nothing renumbers.
    void BindRefreshed(ScrollItemView view, int i)
    {
        var number = _count - i;
        view.Set(i < _added ? $"Item {number}  New" : $"Item {number}", number - 1);   // badge "#number"
    }

    /// <summary>Pull to Refresh's On Refresh: pretend to load, then add three items on top.</summary>
    public void Refresh() => StartCoroutine(Load());

    IEnumerator Load()
    {
        SetStatus("Refreshing…");
        yield return new WaitForSeconds(1.2f);
        _added += 3;
        _count += 3;
        list.SetCount(_count);
        SetStatus($"Updated: {_added} new");
        pull.EndRefresh();
    }
}
