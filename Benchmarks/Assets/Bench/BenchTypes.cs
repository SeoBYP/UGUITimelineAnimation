using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bench
{
    /// <summary>The motions every approach builds the same way (values in BenchValues).</summary>
    public enum Scenario
    {
        Pulse,   // N buttons pulse forever: scale 1 → 1.1 → 1 and white → gold → white, 1 s per cycle (InOutSine)
        Entry,   // N list rows enter: x +100 → 0 (OutCubic) and alpha 0 → 1 over 0.4 s, replayed for all every 0.5 s
        Text,    // N texts of 24 characters wave: each character y 0 → 20 → 0 in 1 s (InOutSine), 0.04 s apart
    }

    public enum Approach { Baseline, UIAnimation, Animator, DOTween, LitMotion, AnimationUI }

    /// <summary>The objects of one case: Pulse/Entry rows (holder → body with an Image), or texts.</summary>
    public sealed class BenchItems
    {
        public RectTransform[] holders, bodies;
        public Image[] images;
        public TMP_Text[] texts;
        public int Count => texts != null ? texts.Length : holders.Length;
    }

    /// <summary>One approach. Setup starts the motion; Replay restarts Entry; Tick runs every frame (only the baseline needs it).</summary>
    public interface IBenchDriver
    {
        Approach Approach { get; }
        bool Supports(Scenario scenario);
        void Setup(Scenario scenario, BenchItems items);
        void Replay();
        void Tick(float time);
        void Teardown();
    }

    public static class BenchValues
    {
        public const float PulseHalf = 0.5f, PulseScale = 1.1f;
        public static readonly Color Gold = new(1f, 0.8f, 0.2f, 1f);
        public const float EntryDuration = 0.4f, EntryFrom = 100f, EntryInterval = 0.5f;
        public const float TextHalf = 0.5f, TextHeight = 20f, TextStagger = 0.04f;
        public const string TextContent = "UI ANIMATION BENCH 2026!";   // 24 characters

        public static float InOutSine(float x) => -(Mathf.Cos(Mathf.PI * x) - 1f) / 2f;
        public static float OutCubic(float x) => 1f - (1f - x) * (1f - x) * (1f - x);
    }
}
