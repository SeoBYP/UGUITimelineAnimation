using SeoBYP.TimelineAnimation;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The summon altar: the magic circle turns (a looping state played on enable); x1 / x10 spend gems and open the results, or shake when short.</summary>
public sealed class RpgSummonPage : MonoBehaviour
{
    public Button single, ten;

    void Awake()
    {
        single.onClick.AddListener(() => Summon(single, 1));
        ten.onClick.AddListener(() => Summon(ten, 10));
    }

    static void Summon(Button button, int count)
    {
        if (!RpgDemo.Instance.Summon(count) && !UIAnimScreenContainer.AnyInTransition)
            button.GetComponent<UIAnimation>().Play("Deny");   // not enough gems
    }
}
