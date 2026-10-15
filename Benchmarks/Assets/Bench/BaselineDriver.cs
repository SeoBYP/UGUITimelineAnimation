using UnityEngine;

namespace Bench
{
    /// <summary>
    /// The floor: one loop writes the same values every frame, with no animation system at all.
    /// What an approach costs above this is its own overhead (the uGUI rebuild of the changed objects is in both).
    /// </summary>
    public sealed class BaselineDriver : IBenchDriver
    {
        public Approach Approach => Approach.Baseline;
        public bool Supports(Scenario scenario) => scenario != Scenario.Text;

        Scenario _scenario;
        BenchItems _items;
        float _start, _replayed;

        public void Setup(Scenario scenario, BenchItems items)
        {
            (_scenario, _items) = (scenario, items);
            _start = _replayed = Time.time;
        }

        public void Replay() => _replayed = Time.time;

        public void Tick(float time)
        {
            if (_scenario == Scenario.Pulse)
            {
                var cycle = (time - _start) / BenchValues.PulseHalf;
                var leg = cycle % 2f;
                var t = BenchValues.InOutSine(leg <= 1f ? leg : 2f - leg);
                var scale = Vector3.one * Mathf.LerpUnclamped(1f, BenchValues.PulseScale, t);
                var color = Color.LerpUnclamped(Color.white, BenchValues.Gold, t);
                for (var i = 0; i < _items.Count; i++)
                {
                    _items.bodies[i].localScale = scale;
                    _items.images[i].color = color;
                }
            }
            else
            {
                var p = Mathf.Clamp01((time - _replayed) / BenchValues.EntryDuration);
                var x = Mathf.LerpUnclamped(BenchValues.EntryFrom, 0f, BenchValues.OutCubic(p));
                for (var i = 0; i < _items.Count; i++)
                {
                    _items.bodies[i].anchoredPosition = new Vector2(x, 0f);
                    var c = _items.images[i].color;
                    c.a = p;
                    _items.images[i].color = c;
                }
            }
        }

        public void Teardown() { }
    }
}
