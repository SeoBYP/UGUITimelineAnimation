using SeoBYP.TimelineAnimation;
using UnityEngine;

/// <summary>Plays a state over and over while enabled (idle breathing, a turning magic circle, a pulsing badge).</summary>
[RequireComponent(typeof(UIAnimation))]
public sealed class RpgRepeat : MonoBehaviour
{
    public string state = "Idle";
    UIAnimation _animation;

    void Awake() => _animation = GetComponent<UIAnimation>();

    void Update()
    {
        if (!_animation.IsPlaying(state)) _animation.Play(state);
    }
}
