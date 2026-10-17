using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;

/// <summary>
/// A currency counter: when the amount changes the label counts to it (the label's "Count": a Text Number track that
/// starts from what it shows; this sets where it ends) and the pill bumps ("Bump").
/// </summary>
public sealed class RpgCurrency : MonoBehaviour
{
    public enum Kind { Gold, Gems, Stamina }
    public Kind kind;
    public TMP_Text text;

    int _shown;

    int Value => kind switch
    {
        Kind.Gold => RpgDemo.Instance.Gold,
        Kind.Gems => RpgDemo.Instance.Gems,
        _ => RpgDemo.Instance.Stamina,
    };

    void OnEnable()
    {
        RpgDemo.Instance.CurrencyChanged += Changed;
        _shown = Value;
        Show(_shown);
    }

    void OnDisable() => RpgDemo.Instance.CurrencyChanged -= Changed;

    void Changed()
    {
        if (Value == _shown) return;
        _shown = Value;
        var count = text.GetComponent<UIAnimation>();
        count.SetEndValue("Count", text, UIAnimProperty.TextNumber, _shown);
        count.Play("Count");
        GetComponent<UIAnimation>().Play("Bump");
    }

    void Show(int value) => text.text = kind == Kind.Stamina ? $"{value}/{RpgDemo.MaxStamina}" : value.ToString("N0");
}
