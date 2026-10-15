using System.Collections.Generic;
using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Guild chat: a scroll list of bubbles (two prefabs, others / mine), each measured to its text and appearing as it
/// arrives ("Appear"); the list stays at the newest message (Stick To End). The quick replies send a message, the
/// "is typing" line blinks (a looping state), and a guild member answers.
/// </summary>
public sealed class RpgGuildPage : MonoBehaviour
{
    public UIAnimScrollList list;
    public Button[] quick;
    public TMP_Text typing;
    [Min(0)] public float answerSeconds = 1.2f;

    readonly List<(bool mine, string who, string hero, string text)> _messages = new();
    UIAnimation _typing;

    public int MessageCount => _messages.Count;

    void Awake()
    {
        _typing = typing.GetComponent<UIAnimation>();
        foreach (var (who, hero, text) in RpgData.GuildChat) _messages.Add((false, who, hero, text));
        list.prefabOf = i => _messages[i].mine ? 1 : 0;
        list.SetCount(_messages.Count, (RpgRow row, int i) => Bind(row, i));
        for (var i = 0; i < quick.Length; i++)
        {
            var reply = RpgData.QuickChat[i];
            quick[i].GetComponentInChildren<TMP_Text>().text = reply.mine;
            quick[i].onClick.AddListener(() => Send(reply));
        }
        typing.gameObject.SetActive(false);
    }

    void Bind(RpgRow row, int i)
    {
        var m = _messages[i];
        row.text.text = m.text;
        if (row.title != null) row.title.text = m.who;
        if (row.icon != null) row.icon.sprite = RpgDemo.Instance.Sprite(m.hero);
    }

    async void Send((string mine, string who, string hero, string answer) reply)
    {
        foreach (var b in quick) b.interactable = false;
        Add(true, "COMMANDER", "aria", reply.mine);
        typing.text = $"{reply.who} is typing...";
        typing.gameObject.SetActive(true);
        _typing.Play("Blink");
        await Awaitable.WaitForSecondsAsync(answerSeconds);
        if (this == null) return;
        _typing.Stop("Blink");
        typing.gameObject.SetActive(false);
        Add(false, reply.who, reply.hero, reply.answer);
        foreach (var b in quick) b.interactable = true;
    }

    void Add(bool mine, string who, string hero, string text)
    {
        _messages.Add((mine, who, hero, text));
        list.SetCount(_messages.Count);
    }
}
