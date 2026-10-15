using System;
using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;

/// <summary>
/// Loading: the bar fills ("Progress") and the percentage follows the bar, the rune spins (a looping state), then the
/// lobby opens. There is nothing to load in this sample, so the progress is the state's own time.
/// </summary>
public sealed class RpgLoading : MonoBehaviour, IUIAnimScreenLifecycle
{
    public TMP_Text tip, percent;
    public RectTransform fill;

    public Awaitable WillEnter(UIAnimTransition t)
    {
        tip.text = RpgData.Tips[UnityEngine.Random.Range(0, RpgData.Tips.Length)];
        return UIAnimScreen.Completed();
    }

    public async void DidEnter(UIAnimTransition t)
    {
        try { await GetComponent<UIAnimation>().PlayAsync("Progress"); }
        catch (OperationCanceledException) { return; }
        RpgDemo.Instance.EnterLobby();
    }

    void Update() => percent.text = $"{Mathf.RoundToInt(fill.localScale.x * 100f)}%";
}
