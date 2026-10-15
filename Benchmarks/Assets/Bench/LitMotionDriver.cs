using System.Collections.Generic;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;

namespace Bench
{
    /// <summary>LitMotion: motions built in code (Yoyo loops for Pulse, one motion per character for Text).</summary>
    public sealed class LitMotionDriver : IBenchDriver
    {
        public Approach Approach => Approach.LitMotion;
        public bool Supports(Scenario scenario) => true;

        readonly List<MotionHandle> _handles = new();
        Scenario _scenario;
        BenchItems _items;

        public void Setup(Scenario scenario, BenchItems items)
        {
            (_scenario, _items) = (scenario, items);
            for (var i = 0; i < items.Count; i++)
            {
                switch (scenario)
                {
                    case Scenario.Pulse:
                        _handles.Add(LMotion.Create(Vector3.one, Vector3.one * BenchValues.PulseScale, BenchValues.PulseHalf)
                            .WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo).BindToLocalScale(items.bodies[i]));
                        _handles.Add(LMotion.Create(Color.white, BenchValues.Gold, BenchValues.PulseHalf)
                            .WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo).BindToColor(items.images[i]));
                        break;
                    case Scenario.Entry:
                        Start(i);
                        break;
                    case Scenario.Text:
                        for (var c = 0; c < BenchValues.TextContent.Length; c++)
                            _handles.Add(LMotion.Create(0f, BenchValues.TextHeight, BenchValues.TextHalf)
                                .WithEase(Ease.InOutSine).WithLoops(-1, LoopType.Yoyo).WithDelay(c * BenchValues.TextStagger)
                                .BindToTMPCharPositionY(items.texts[i], c));
                        break;
                }
            }
        }

        void Start(int i)
        {
            _handles.Add(LMotion.Create(BenchValues.EntryFrom, 0f, BenchValues.EntryDuration).WithEase(Ease.OutCubic)
                .BindToAnchoredPositionX(_items.bodies[i]));
            _handles.Add(LMotion.Create(0f, 1f, BenchValues.EntryDuration).BindToColorA(_items.images[i]));
        }

        public void Replay()
        {
            foreach (var h in _handles) h.TryCancel();
            _handles.Clear();
            for (var i = 0; i < _items.Count; i++) Start(i);
        }

        public void Tick(float time) { }

        public void Teardown()
        {
            foreach (var h in _handles) h.TryCancel();
            _handles.Clear();
        }
    }
}
