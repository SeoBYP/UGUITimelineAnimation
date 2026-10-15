using SeoBYP.TimelineAnimation;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Title: the logo drops in letter by letter ("PushEnter"), TAP TO START blinks (a looping state played on enable); a tap anywhere goes on to loading.</summary>
public sealed class RpgTitle : MonoBehaviour
{
    public Button tap;

    void Awake() => tap.onClick.AddListener(() => RpgDemo.Instance.ShowLoading());
}
