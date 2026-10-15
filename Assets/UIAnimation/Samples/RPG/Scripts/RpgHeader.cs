using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The top bar of every sub page: back button, title, currency counters.</summary>
public sealed class RpgHeader : MonoBehaviour
{
    public Button back;
    public TMP_Text title;

    void Awake() => back.onClick.AddListener(() => RpgDemo.Instance.Back());
}
