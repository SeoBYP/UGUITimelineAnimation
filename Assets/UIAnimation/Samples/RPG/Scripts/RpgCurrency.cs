using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;

/// <summary>A currency counter: counts to the new value and bumps (state "Bump") when it changes.</summary>
public sealed class RpgCurrency : MonoBehaviour
{
    public enum Kind { Gold, Gems, Stamina }
    public Kind kind;
    public TMP_Text text;
    [Min(0)] public float countSeconds = 0.6f;

    int _shown;
    float _from, _to, _time = 1f;

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
        _time = 1f;
        Show(_shown);
    }

    void OnDisable() => RpgDemo.Instance.CurrencyChanged -= Changed;

    void Changed()
    {
        if (Value == _shown) return;
        _from = _shown;
        _to = Value;
        _time = 0f;
        GetComponent<UIAnimation>().Play("Bump");
    }

    void Update()
    {
        if (_time >= 1f) return;
        _time = Mathf.Min(1f, _time + Time.deltaTime / Mathf.Max(0.01f, countSeconds));
        _shown = Mathf.RoundToInt(Mathf.Lerp(_from, _to, 1f - (1f - _time) * (1f - _time)));   // ease out
        Show(_shown);
    }

    void Show(int value) => text.text = kind == Kind.Stamina ? $"{value}/{RpgDemo.MaxStamina}" : value.ToString("N0");
}
