using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A stage on the chapter map: cleared (gold, stars), next (pulses), or locked (dim); selected shows a ring.</summary>
public sealed class RpgStageNode : MonoBehaviour
{
    public enum State { Cleared, Next, Locked }

    public Image frame;
    public TMP_Text label;
    public GameObject stars, ring;

    public State Current { get; private set; }

    public void Set(int stage, State state)
    {
        Current = state;
        label.text = (stage + 1).ToString();
        frame.color = state switch { State.Cleared => new Color32(255, 214, 120, 255), State.Next => Color.white, _ => new Color32(110, 100, 96, 255) };
        stars.SetActive(state == State.Cleared);
        GetComponent<Button>().interactable = state != State.Locked;
        var repeat = GetComponent<RpgRepeat>();
        repeat.enabled = state == State.Next;
        if (state != State.Next) GetComponent<UIAnimation>().Sample("Pulse", 0f);
    }

    public void Select(bool on) => ring.SetActive(on);
}
