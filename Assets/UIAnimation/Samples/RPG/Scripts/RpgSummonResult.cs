using System.Linq;
using SeoBYP.TimelineAnimation;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Summon results: the circle flares up ("Enter"), the cards drop in one after another, then turn over one by one —
/// a 4★ glows, a 5★ shakes and bursts with light before it turns. OK closes when all are shown.
/// Rates: 5★ 10 %, 4★ 30 %, 3★ the rest; ten summons always include a 4★ or better.
/// </summary>
public sealed class RpgSummonResult : MonoBehaviour, IUIAnimScreenLifecycle
{
    [Tooltip("The ten cards of a x10 summon.")]
    public RpgSummonCard[] cards;
    [Tooltip("The card of a x1 summon, in the center.")]
    public RpgSummonCard single;
    public Button ok;
    [Min(0)] public float dropInterval = 0.08f;

    /// <summary>Every card is face up (tests).</summary>
    public bool Done { get; private set; }

    void Awake() => ok.onClick.AddListener(() => RpgDemo.Instance.Back());

    public Awaitable WillEnter(UIAnimTransition t)
    {
        var ten = RpgDemo.Instance.SummonCount == 10;
        single.gameObject.SetActive(!ten);
        if (!ten) single.Set(Roll(false));
        for (var i = 0; i < cards.Length; i++)
        {
            cards[i].gameObject.SetActive(ten);
            if (ten) cards[i].Set(Roll(guaranteeEpic: i == cards.Length - 1));
        }
        ok.interactable = false;
        return UIAnimScreen.Completed();
    }

    public async void DidEnter(UIAnimTransition t)
    {
        var shown = single.gameObject.activeSelf ? new[] { single } : cards;
        foreach (var card in shown)
        {
            card.Animation.Play("Drop");
            await Awaitable.WaitForSecondsAsync(dropInterval);
        }
        await Awaitable.WaitForSecondsAsync(0.4f);
        foreach (var card in shown)
        {
            if (this == null) return;
            await card.Animation.PlayAsync(card.FlipState);
        }
        ok.interactable = true;
        GetComponent<UIAnimation>().Play("Done");
        Done = true;
    }

    static RpgData.Hero Roll(bool guaranteeEpic)
    {
        var r = Random.value;
        var stars = r < 0.1f ? 5 : r < 0.4f || guaranteeEpic ? 4 : 3;
        var pool = RpgData.Heroes.Where(h => h.stars == stars).ToArray();
        return pool[Random.Range(0, pool.Length)];
    }
}
