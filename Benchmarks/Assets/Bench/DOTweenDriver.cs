#if BENCH_DOTWEEN   // set by BenchBuild when DOTween (Pro) is imported into Assets/Plugins/Demigiant
using DG.Tweening;
using UnityEngine;

namespace Bench
{
    /// <summary>DOTween Pro: tweens built in code (Yoyo loops for Pulse, DOTweenTMPAnimator per character for Text).</summary>
    public sealed class DOTweenDriver : IBenchDriver
    {
        public Approach Approach => Approach.DOTween;
        public bool Supports(Scenario scenario) => true;

        BenchItems _items;

        public void Setup(Scenario scenario, BenchItems items)
        {
            _items = items;
            DOTween.SetTweensCapacity(20000, 200);
            for (var i = 0; i < items.Count; i++)
            {
                switch (scenario)
                {
                    case Scenario.Pulse:
                        items.bodies[i].DOScale(BenchValues.PulseScale, BenchValues.PulseHalf).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
                        items.images[i].DOColor(BenchValues.Gold, BenchValues.PulseHalf).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
                        break;
                    case Scenario.Entry:
                        Start(i);
                        break;
                    case Scenario.Text:
                        var animator = new DOTweenTMPAnimator(items.texts[i]);
                        for (var c = 0; c < animator.textInfo.characterCount; c++)
                            animator.DOOffsetChar(c, new Vector3(0, BenchValues.TextHeight, 0), BenchValues.TextHalf)
                                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetDelay(c * BenchValues.TextStagger);
                        break;
                }
            }
        }

        void Start(int i)
        {
            _items.bodies[i].DOAnchorPosX(0f, BenchValues.EntryDuration).From(new Vector2(BenchValues.EntryFrom, 0f)).SetEase(Ease.OutCubic);
            _items.images[i].DOFade(1f, BenchValues.EntryDuration).From(0f).SetEase(Ease.Linear);
        }

        public void Replay()
        {
            DOTween.Kill(null, true);   // the previous entry tweens (all tweens of this case)
            for (var i = 0; i < _items.Count; i++) Start(i);
        }

        public void Tick(float time) { }
        public void Teardown() => DOTween.KillAll();
    }
}
#endif
