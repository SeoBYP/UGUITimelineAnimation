using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Victory or defeat popup: everything it shows is its "Enter" state. Continue / Retry closes it, Lobby goes home.</summary>
public sealed class RpgResult : MonoBehaviour, IUIAnimScreenLifecycle
{
    public Button primary, lobby;
    [Tooltip("Victory: the gold reward. Defeat: the stage.")]
    public TMP_Text detail;
    public bool victory;

    void Awake()
    {
        primary.onClick.AddListener(() => RpgDemo.Instance.Back());
        lobby.onClick.AddListener(() => RpgDemo.Instance.ToLobby());
    }

    public Awaitable WillEnter(UIAnimTransition t)
    {
        var (chapter, stage) = RpgDemo.Instance.SelectedStage;
        detail.text = victory ? $"+{250 + chapter * 100:N0}" : $"STAGE {chapter + 1}-{stage + 1}";
        return UIAnimScreen.Completed();
    }
}
