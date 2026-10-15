using System;
using Cysharp.Threading.Tasks;
using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UniTask with UI Animation: states awaited and combined like any other UniTask.
/// Open: the popup and the dim together (UniTask.WhenAll). Close: a close cut short by another play is not an error
/// (SuppressCancellationThrow). Slow: a 3 s state given 1 s (Timeout), then skipped to its end. Wait Shown: waits for
/// the "Shown" animation event of the next Open. All are canceled when the sample is destroyed.
/// </summary>
public sealed class UniTaskPopupSample : MonoBehaviour
{
    public UIAnimation popup, dim;
    public Button open, close, slow, waitShown;
    public TMP_Text log;

    void Start()
    {
        open.onClick.AddListener(() => OpenAsync().Forget());
        close.onClick.AddListener(() => CloseAsync().Forget());
        slow.onClick.AddListener(() => SlowAsync().Forget());
        waitShown.onClick.AddListener(() => WaitShownAsync().Forget());
    }

    async UniTaskVoid OpenAsync()
    {
        var token = destroyCancellationToken;
        await UniTask.WhenAll(popup.PlayUniTaskAsync("Open", cancellationToken: token), dim.PlayUniTaskAsync("Show", cancellationToken: token));
        Log("Open + Dim 끝 (WhenAll)");
    }

    async UniTaskVoid CloseAsync()
    {
        var canceled = await UniTask.WhenAll(popup.PlayUniTaskAsync("Close", cancellationToken: destroyCancellationToken), dim.PlayUniTaskAsync("Hide"))
            .SuppressCancellationThrow();
        Log(canceled ? "Close 중단됨 (다른 재생이 가져감)" : "Close 끝");
    }

    async UniTaskVoid SlowAsync()
    {
        try
        {
            await popup.PlayUniTaskAsync("Slow", cancellationToken: destroyCancellationToken).Timeout(TimeSpan.FromSeconds(1));
            Log("Slow 끝");
        }
        catch (TimeoutException)
        {
            popup.Complete("Slow");   // skip to its end
            Log("Slow: 1초 제한 넘김 → 끝으로 건너뜀 (Timeout)");
        }
    }

    async UniTaskVoid WaitShownAsync()
    {
        Log("다음 Open의 Shown 이벤트를 기다림…");
        var e = await popup.WaitEventUniTaskAsync("Shown", destroyCancellationToken);
        Log($"이벤트 {e.Name} ({e.State} {e.Time:0.00}초)");
    }

    void Log(string line) => log.text = line + "\n" + log.text;
}
