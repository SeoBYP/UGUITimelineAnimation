using SeoBYP.TimelineAnimation;
using UnityEngine;

/// <summary>Loading page: as soon as it is in, Home is pushed (Home's own WillEnter keeps Loading on screen while it prepares).</summary>
public sealed class UsnLoadingPage : MonoBehaviour, IUIAnimScreenLifecycle
{
    public void DidEnter(UIAnimTransition t) => Next();

    static async void Next()
    {
        await Awaitable.NextFrameAsync();
        UsnDemo.Instance.ShowHome();
    }
}
