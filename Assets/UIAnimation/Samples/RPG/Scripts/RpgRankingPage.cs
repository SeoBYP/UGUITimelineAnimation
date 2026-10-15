using System.Collections.Generic;
using SeoBYP.TimelineAnimation;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ranking: the top three stand on a podium that rises with the page (pillars grow, then portraits drop in and the
/// crown bounces), the rest is a list (ranks 4–50) whose rows enter one after another. MY RANK scrolls the list to the
/// player's row and makes it flash ("Highlight").
/// </summary>
public sealed class RpgRankingPage : MonoBehaviour
{
    public UIAnimScrollList list;
    public RpgRow[] podium;
    public RpgRow mine;
    public Button myRank;

    List<RpgData.Rank> _ranks;
    int _myIndex;

    public int MyListIndex => _myIndex;

    void Awake()
    {
        var demo = RpgDemo.Instance;
        _ranks = RpgData.Ranking();
        for (var i = 0; i < podium.Length; i++) Set(podium[i], _ranks[i], i);
        var rest = _ranks.GetRange(podium.Length, _ranks.Count - podium.Length);
        _myIndex = rest.FindIndex(r => r.me);
        Set(mine, rest[_myIndex], _myIndex + podium.Length);
        list.SetData(rest, (RpgRow row, RpgData.Rank r, int i) =>
        {
            Set(row, r, i + podium.Length);
            row.GetComponent<UIAnimation>().Sample("Highlight", 0f);
        });
        myRank.onClick.AddListener(ShowMine);
    }

    static void Set(RpgRow row, RpgData.Rank r, int index)
    {
        row.title.text = r.name;
        row.value.text = r.score.ToString("N0");
        if (row.text != null) row.text.text = (index + 1).ToString();
        row.icon.sprite = RpgDemo.Instance.Sprite(r.hero);
    }

    async void ShowMine()
    {
        list.ScrollTo(_myIndex, UIAnimScrollList.Alignment.Center, 0.5f);
        await Awaitable.WaitForSecondsAsync(0.55f);
        var row = list.GetItem(_myIndex);
        if (row != null) row.GetComponent<UIAnimation>().Play("Highlight");
    }
}
