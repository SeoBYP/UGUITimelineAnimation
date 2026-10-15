using DhafinFawwaz.AnimationUILib;
using UnityEngine;

namespace Bench
{
    /// <summary>
    /// Unity-AnimationUI (coroutines): an AnimationUI per row; steps that start together run at once, Wait separates them.
    /// It has no loop and no sine ease: Pulse replays itself when it ends, InOut Quad stands in for InOutSine.
    /// No per-character text animation (Text is not supported).
    /// </summary>
    public sealed class AnimationUIDriver : IBenchDriver
    {
        public Approach Approach => Approach.AnimationUI;
        public bool Supports(Scenario scenario) => scenario != Scenario.Text;

        AnimationUI[] _anims;

        static Sequence Scale(RectTransform rt, float from, float to) => new()
        {
            SequenceType = Sequence.Type.Animation, TargetType = Sequence.ObjectType.RectTransform, TargetComp = rt,
            TargetRtTask = Sequence.RtTask.LocalScale, LocalScaleStart = Vector3.one * from, LocalScaleEnd = Vector3.one * to,
            Duration = BenchValues.PulseHalf, EaseType = Ease.Type.InOut, EasePower = Ease.Power.Quad,
        };

        static Sequence Tint(UnityEngine.UI.Image image, Color from, Color to, float duration, Ease.Type type, Ease.Power power) => new()
        {
            SequenceType = Sequence.Type.Animation, TargetType = Sequence.ObjectType.Image, TargetComp = image,
            TargetImgTask = Sequence.ImgTask.Color, ColorStart = from, ColorEnd = to, Duration = duration, EaseType = type, EasePower = power,
        };

        static Sequence Wait(float seconds) => new() { SequenceType = Sequence.Type.Wait, Duration = seconds };

        public void Setup(Scenario scenario, BenchItems items)
        {
            _anims = new AnimationUI[items.Count];
            for (var i = 0; i < items.Count; i++)
            {
                var body = items.bodies[i];
                var image = items.images[i];
                var host = items.holders[i].gameObject;
                host.SetActive(false);   // AnimationUI reads its sequences in Awake
                var anim = host.AddComponent<AnimationUI>();
                if (scenario == Scenario.Pulse)
                {
                    anim.AnimationSequence = new[]
                    {
                        Scale(body, 1f, BenchValues.PulseScale), Tint(image, UnityEngine.Color.white, BenchValues.Gold, BenchValues.PulseHalf, Ease.Type.InOut, Ease.Power.Quad),
                        Wait(BenchValues.PulseHalf),
                        Scale(body, BenchValues.PulseScale, 1f), Tint(image, BenchValues.Gold, UnityEngine.Color.white, BenchValues.PulseHalf, Ease.Type.InOut, Ease.Power.Quad),
                        Wait(BenchValues.PulseHalf),
                    };
                    anim.OnAnimationEnded += anim.Play;   // loop
                }
                else
                {
                    anim.AnimationSequence = new[]
                    {
                        new Sequence
                        {
                            SequenceType = Sequence.Type.Animation, TargetType = Sequence.ObjectType.RectTransform, TargetComp = body,
                            TargetRtTask = Sequence.RtTask.AnchoredPosition, AnchoredPositionStart = new Vector3(BenchValues.EntryFrom, 0), AnchoredPositionEnd = Vector3.zero,
                            Duration = BenchValues.EntryDuration, EaseType = Ease.Type.Out, EasePower = Ease.Power.Cubic,
                        },
                        Tint(image, new Color(1, 1, 1, 0), UnityEngine.Color.white, BenchValues.EntryDuration, Ease.Type.In, Ease.Power.Linear),
                        // Without a Wait the sequence coroutine ends in its first step, StartCoroutine returns null, and the
                        // next Play's Stop() throws "routine is null" (an AnimationUI bug): wait out the motion.
                        Wait(BenchValues.EntryDuration),
                    };
                }
                anim.TotalDuration = scenario == Scenario.Pulse ? 2 * BenchValues.PulseHalf : BenchValues.EntryDuration;
                host.SetActive(true);
                _anims[i] = anim;
                anim.Play();
            }
        }

        public void Replay()
        {
            foreach (var a in _anims) a.Play();
        }

        public void Tick(float time) { }

        public void Teardown()
        {
            foreach (var a in _anims)
            {
                if (a == null) continue;
                a.OnAnimationEnded = null;
                a.Stop();
            }
        }
    }
}
