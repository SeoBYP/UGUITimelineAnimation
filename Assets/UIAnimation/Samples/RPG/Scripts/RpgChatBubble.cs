using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A chat message: the sender and time above a bubble that is as wide as its text, up to maxWidth (then the text wraps).
/// Others' bubbles have a portrait; the player's own have none.
/// </summary>
public sealed class RpgChatBubble : MonoBehaviour
{
    public TMP_Text meta, message;
    public Image portrait;
    [Tooltip("The bubble's width: the message's width plus padding, at most Max Width.")]
    public LayoutElement bubble;
    public float padding = 56f, minWidth = 90f, maxWidth = 900f;

    public void Set(string who, string time, string text, Sprite face)
    {
        meta.text = string.IsNullOrEmpty(who) ? time : $"{who}  <color=#8f826e>{time}</color>";
        message.text = text;
        if (portrait != null) portrait.sprite = face;
        var width = message.GetPreferredValues(text, maxWidth - padding, 0f).x + padding;
        bubble.preferredWidth = Mathf.Clamp(width, minWidth, maxWidth);
    }
}
