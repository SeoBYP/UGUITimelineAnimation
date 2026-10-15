using UnityEngine;
using UnityEngine.UI;

/// <summary>Reward and level-up popups: everything they show is their "Enter" state; the button closes them.</summary>
public sealed class ShowcasePopup : MonoBehaviour
{
    public Button close;
    void Awake() => close.onClick.AddListener(() => ShowcaseDemo.Instance.Close());
}
