using System.Collections.Generic;
using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Custom animation event: picked from the event menu in the editor, received with AddListener.</summary>
[UIAnimEvent("Popup Sound")]
public struct PopupSoundEvent
{
    public string clip;
    public float volume;
}

/// <summary>
/// Dev sample: opens a popup, awaits its Close animation, then destroys it.
/// A Close interrupted by Open (e.g. the "Close > Open" button) is canceled, so the popup is kept.
/// </summary>
public sealed class PopupSample : MonoBehaviour
{
    public UIAnimation popupPrefab;
    public RectTransform popupParent;
    public TMP_Text log;
    [Tooltip("Pauses the editor when Close>Open reopens, so the canceled frame can be stepped through.")]
    public bool pauseOnReopen;

    UIAnimation _popup;
    readonly Queue<string> _lines = new();

    public void Open()
    {
        if (_popup == null)
        {
            _popup = Instantiate(popupPrefab, popupParent);
            _popup.GetComponentInChildren<Button>().onClick.AddListener(Close);
            _popup.EventRaised += (in UIAnimEventInfo e) => Log($"Event {e.State}:{e.Name}");
            _popup.AddListener((in UIAnimEventInfo e, in PopupSoundEvent sound) => Log($"Sound {sound.clip} ({sound.volume:0.0})"));
            _popup.Play("Close");       // start from the closed pose
            _popup.Complete("Close");
            Log("Instantiate");
        }
        _popup.Play("Open");
        Log("Open");
    }

    // async void: a button handler that awaits (exceptions go to the console). No UniTask needed: PlayAsync is an Awaitable.
    public async void Close()
    {
        var popup = _popup;
        if (popup == null) return;
        Log("Close started");
        try
        {
            await popup.PlayAsync("Close", cancellationToken: popup.destroyCancellationToken);
        }
        catch (System.OperationCanceledException)
        {
            Log("Close canceled -> popup kept");
            return;
        }
        if (_popup == popup) _popup = null;
        Destroy(popup.gameObject);
        Log("Close completed -> Destroy");
    }

    /// <summary>Closes, then reopens 0.15s later: the Close await is canceled mid-animation.</summary>
    public async void CloseThenOpen()
    {
        if (_popup == null) return;
        Close();
        await Awaitable.WaitForSecondsAsync(0.15f);
        Open();
        if (pauseOnReopen) Debug.Break();
    }

    void Log(string line)
    {
        Debug.Log($"[PopupSample] {line}");
        _lines.Enqueue($"{Time.unscaledTime:0.00}s  {line}");
        while (_lines.Count > 4) _lines.Dequeue();
        if (log != null) log.text = string.Join("\n", _lines);
    }
}
