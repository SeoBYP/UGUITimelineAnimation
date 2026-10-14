using UnityEngine;
using UnityEngine.UI;

/// <summary>Top page: tap anywhere to start.</summary>
public sealed class UsnTopPage : MonoBehaviour
{
    public Button tap;
    void Awake() => tap.onClick.AddListener(() => UsnDemo.Instance.ShowLoading());
}
