using System.Linq;
using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Heroes: a grid (scroll list) of hero cards that enter one after another; the sort button re-orders and replays it.</summary>
public sealed class RpgHeroesPage : MonoBehaviour
{
    public UIAnimScrollList list;
    public Button sort;
    public TMP_Text sortLabel;

    bool _byStars;
    int[] _order;

    void Awake()
    {
        sort.onClick.AddListener(() =>
        {
            _byStars = !_byStars;
            Fill();
            list.PlayEntry();
        });
        Fill();
    }

    void Fill()
    {
        var heroes = Enumerable.Range(0, RpgData.Heroes.Length);
        _order = (_byStars
            ? heroes.OrderByDescending(i => RpgData.Heroes[i].stars).ThenByDescending(i => RpgData.Heroes[i].level)
            : heroes.OrderByDescending(i => RpgData.Heroes[i].level)).ToArray();
        sortLabel.text = _byStars ? "BY RARITY" : "BY LEVEL";
        list.SetData(_order, (RpgHeroCard card, int hero, int _) => card.Set(hero));
    }
}
