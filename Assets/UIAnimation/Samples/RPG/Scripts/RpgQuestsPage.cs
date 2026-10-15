using System.Collections.Generic;
using SeoBYP.TimelineAnimation;
using UnityEngine;

/// <summary>
/// Quests: one list with section headers that stick to the top (DAILY / WEEKLY / ACHIEVEMENTS). Claiming stamps the
/// row ("Claim"), pays the reward (the counters count up) and folds the row away (ResizeItem) before it is removed.
/// </summary>
public sealed class RpgQuestsPage : MonoBehaviour
{
    public UIAnimScrollList list;
    [Min(0)] public float foldSeconds = 0.3f;

    // A row is a section header (quest == null) or a quest.
    readonly List<(string section, RpgData.Quest quest)> _rows = new();
    bool _claiming;

    public int QuestCount => _rows.FindAll(r => r.quest != null).Count;

    void Awake()
    {
        string section = null;
        foreach (var q in RpgData.Quests())
        {
            if (q.section != section) _rows.Add((section = q.section, null));
            _rows.Add((q.section, q));
        }
        list.prefabOf = i => _rows[i].quest == null ? 1 : 0;
        list.sizeOf = i => _rows[i].quest == null ? 72f : 128f;
        list.SetCount(_rows.Count, Bind);
    }

    void Bind(RectTransform item, int i)
    {
        var (section, quest) = _rows[i];
        if (quest == null)
        {
            item.GetComponent<RpgRow>().title.text = section;
            return;
        }
        var row = item.GetComponent<RpgQuestRow>();
        row.Set(quest);
        item.GetComponent<UIAnimation>().Sample("Claim", 0f);   // a reused row may still show a claimed stamp
        row.claim.onClick.RemoveAllListeners();
        row.claim.onClick.AddListener(() => Claim(quest, item.GetComponent<UIAnimation>()));
    }

    async void Claim(RpgData.Quest quest, UIAnimation row)
    {
        if (_claiming) return;
        _claiming = true;
        await row.PlayAsync("Claim");
        RpgDemo.Instance.Add(gold: quest.rewardIcon == "gold" ? quest.reward : 0, gems: quest.rewardIcon == "gem" ? quest.reward : 0,
            stamina: quest.rewardIcon == "stamina" ? quest.reward : 0);
        var index = _rows.FindIndex(r => r.quest == quest);
        await list.ResizeItem(index, 0f, foldSeconds);
        _rows.RemoveAt(index);
        if (index - 1 >= 0 && _rows[index - 1].quest == null && (index >= _rows.Count || _rows[index].quest == null))
            _rows.RemoveAt(index - 1);   // the section is empty now
        list.SetCount(_rows.Count);   // keeps the scroll position and rebinds the rows that moved up (no entry again)
        _claiming = false;
    }
}
