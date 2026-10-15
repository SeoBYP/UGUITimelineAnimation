using System;
using System.Collections.Generic;
using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;

/// <summary>
/// Toasts: a message slides down from the top, stays a moment and slides back ("Show", one state); messages sent
/// meanwhile wait their turn. It sits on its own canvas above the popups, so any screen can use it.
/// </summary>
public sealed class RpgToast : MonoBehaviour
{
    public TMP_Text text;

    readonly Queue<string> _queue = new();
    bool _showing;

    public int Pending => _queue.Count + (_showing ? 1 : 0);

    public void Show(string message)
    {
        _queue.Enqueue(message);
        if (!_showing) Run();
    }

    async void Run()
    {
        _showing = true;
        var anim = GetComponent<UIAnimation>();
        while (_queue.Count > 0)
        {
            text.text = _queue.Dequeue();
            try { await anim.PlayAsync("Show"); }
            catch (OperationCanceledException) { break; }   // the scene is going away
        }
        _showing = false;
    }
}
