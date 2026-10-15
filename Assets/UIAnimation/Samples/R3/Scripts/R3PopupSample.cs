using System;
using R3;
using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// R3 with UI Animation: button clicks, states and animation events as one stream.
/// Open: taps are thinned to one per 0.5 s, then the popup's "Open" state plays (PlayAsObservable) and the log says when it ended.
/// Close: plays "Close"; a new tap while closing replaces it (Switch), so only the last close finishes.
/// The "Shown" animation event (in "Open") is logged with its time (EventAsObservable).
/// </summary>
public sealed class R3PopupSample : MonoBehaviour
{
    public UIAnimation popup;
    public Button open, close;
    public TMP_Text log;

    void Start()
    {
        open.OnClickAsObservable()
            .ThrottleFirst(TimeSpan.FromSeconds(0.5))
            .SelectMany(_ => Played("Open"))
            .Subscribe(_ => Log("Open 끝"))
            .AddTo(this);

        close.OnClickAsObservable()
            .Select(_ => Played("Close"))
            .Switch()
            .Subscribe(_ => Log("Close 끝"))
            .AddTo(this);

        popup.EventAsObservable("Shown")
            .Subscribe(e => Log($"이벤트 {e.Name} ({e.State} {e.Time:0.00}초)"))
            .AddTo(this);
    }

    // A state that does not finish (replaced by another) completes with an error: end that play quietly, keep the button stream.
    Observable<Unit> Played(string state) =>
        popup.PlayAsObservable(state).Catch<Unit, OperationCanceledException>(_ => Observable.Empty<Unit>());

    void Log(string line) => log.text = line + "\n" + log.text;
}
