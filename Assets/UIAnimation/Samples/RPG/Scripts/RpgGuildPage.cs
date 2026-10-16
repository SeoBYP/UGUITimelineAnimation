using System.Collections.Generic;
using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Guild chat: a scroll list of bubbles (two prefabs, others / mine), each as wide as its text and measured to its
/// height, appearing as it arrives ("Appear"); the list stays at the newest message (Stick To End). Type in the input
/// field and SEND (or Enter): the message goes in, a guild member's typing bubble bounces (a looping state) and their
/// answer follows.
/// </summary>
public sealed class RpgGuildPage : MonoBehaviour
{
    public UIAnimScrollList list;
    public TMP_InputField input;
    public Button send;
    [Tooltip("The typing bubble (shown while a member is answering) and the name next to it.")]
    public GameObject typing;
    public TMP_Text typingName;
    [Min(0)] public float answerSeconds = 1.2f;

    readonly List<(bool mine, string who, string hero, string text)> _messages = new();
    int _replies;
    bool _waiting;

    public int MessageCount => _messages.Count;

    void Awake()
    {
        foreach (var (who, hero, text) in RpgData.GuildChat) _messages.Add((false, who, hero, text));
        list.prefabOf = i => _messages[i].mine ? 1 : 0;
        list.SetCount(_messages.Count, (RpgChatBubble b, int i) => Bind(b, i));
        send.onClick.AddListener(SendTyped);
        input.onSubmit.AddListener(_ => SendTyped());
        typing.SetActive(false);
    }

    // A clock that moves a minute per message, so the times read like a conversation.
    static string Time(int i) => $"21:{4 + i:00}";

    void Bind(RpgChatBubble b, int i)
    {
        var m = _messages[i];
        b.Set(m.mine ? null : m.who, Time(i), m.text, m.mine ? null : RpgDemo.Instance.Sprite(m.hero));
    }

    void SendTyped()
    {
        var text = input.text.Trim();
        if (text.Length == 0 || _waiting) return;
        input.text = "";
        var (who, hero, answer) = RpgData.ChatReplies[_replies++ % RpgData.ChatReplies.Length];
        Send(text, who, hero, answer);
    }

    async void Send(string mine, string who, string hero, string answer)
    {
        if (_waiting) return;
        _waiting = true;
        Interactable(false);
        Add(true, "COMMANDER", "aria", mine);
        typingName.text = $"{who} is typing";
        typing.SetActive(true);   // its "Bounce" plays on enable
        await Awaitable.WaitForSecondsAsync(answerSeconds);
        if (this == null) return;
        typing.SetActive(false);
        Add(false, who, hero, answer);
        Interactable(true);
        _waiting = false;
    }

    void Interactable(bool on) => send.interactable = on;

    void Add(bool mine, string who, string hero, string text)
    {
        _messages.Add((mine, who, hero, text));
        list.SetCount(_messages.Count);
    }
}
