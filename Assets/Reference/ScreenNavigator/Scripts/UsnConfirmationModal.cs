using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A message with a close button ("This feature is locked.").</summary>
public sealed class UsnConfirmationModal : MonoBehaviour
{
    public TMP_Text message;
    public Button close;

    void Awake()
    {
        message.text = "This feature is locked.";
        close.onClick.AddListener(() => UsnDemo.Instance.Back());
    }
}
