using SeoBYP.TimelineAnimation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The common confirm popup ("Enter" / "Exit"): a title, a message, OK and CANCEL. OK closes it and then runs the action.</summary>
public sealed class RpgConfirm : MonoBehaviour, IUIAnimScreenLifecycle
{
    public TMP_Text title, message;
    public Button ok, cancel;

    void Awake()
    {
        cancel.onClick.AddListener(() => RpgDemo.Instance.Back());
        ok.onClick.AddListener(Ok);
    }

    public Awaitable WillEnter(UIAnimTransition t)
    {
        var c = RpgDemo.Instance.Confirmation;
        title.text = c.title;
        message.text = c.message;
        return UIAnimScreen.Completed();
    }

    async void Ok()
    {
        var demo = RpgDemo.Instance;
        var action = demo.Confirmation.ok;
        await demo.modals.Pop();
        action?.Invoke();
    }
}
