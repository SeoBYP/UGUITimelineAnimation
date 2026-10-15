using SeoBYP.TimelineAnimation;
using UnityEngine;

namespace Bench
{
    /// <summary>UI Animation: a UIAnimation per row (like a prefab instance) with the motion as a state; Pulse and Text loop.</summary>
    public sealed class UIAnimationDriver : IBenchDriver
    {
        public Approach Approach => Approach.UIAnimation;
        public bool Supports(Scenario scenario) => true;

        UIAnimation[] _anims;
        string _state;

        static UIAnimKey K(float time, Vector4 value, UIAnimEase ease = UIAnimEase.Linear) => new() { time = time, value = value, ease = ease };

        static UIAnimTrack Track(Component target, UIAnimProperty property, params UIAnimKey[] keys)
        {
            var track = new UIAnimTrack { target = target, property = property, start = UIAnimStart.KeyValue };
            track.keys.AddRange(keys);
            return track;
        }

        public void Setup(Scenario scenario, BenchItems items)
        {
            _anims = new UIAnimation[items.Count];
            _state = scenario.ToString();
            for (var i = 0; i < items.Count; i++)
            {
                var host = scenario == Scenario.Text ? items.texts[i].gameObject : items.holders[i].gameObject;
                var state = new UIAnimState { name = _state, loop = scenario != Scenario.Entry };
                switch (scenario)
                {
                    case Scenario.Pulse:
                        var h = BenchValues.PulseHalf;
                        state.tracks.Add(Track(items.bodies[i], UIAnimProperty.Scale,
                            K(0, Vector3.one), K(h, Vector3.one * BenchValues.PulseScale, UIAnimEase.InOutSine), K(2 * h, Vector3.one, UIAnimEase.InOutSine)));
                        state.tracks.Add(Track(items.images[i], UIAnimProperty.GraphicColor,
                            K(0, Color.white), K(h, BenchValues.Gold, UIAnimEase.InOutSine), K(2 * h, Color.white, UIAnimEase.InOutSine)));
                        break;
                    case Scenario.Entry:
                        state.tracks.Add(Track(items.bodies[i], UIAnimProperty.AnchoredPosition,
                            K(0, new Vector4(BenchValues.EntryFrom, 0)), K(BenchValues.EntryDuration, Vector4.zero, UIAnimEase.OutCubic)));
                        state.tracks.Add(Track(items.images[i], UIAnimProperty.GraphicAlpha,
                            K(0, Vector4.zero), K(BenchValues.EntryDuration, new Vector4(1, 0))));
                        break;
                    case Scenario.Text:
                        var t = Track(items.texts[i], UIAnimProperty.CharOffset,
                            K(0, Vector4.zero), K(BenchValues.TextHalf, new Vector4(0, BenchValues.TextHeight), UIAnimEase.InOutSine),
                            K(2 * BenchValues.TextHalf, Vector4.zero, UIAnimEase.InOutSine));
                        t.stagger = BenchValues.TextStagger;
                        state.tracks.Add(t);
                        break;
                }
                host.SetActive(false);   // the states are there before Awake, like a prefab
                var anim = host.AddComponent<UIAnimation>();
                anim.states.Add(state);
                host.SetActive(true);
                _anims[i] = anim;
                anim.Play(_state);
            }
        }

        public void Replay()
        {
            foreach (var anim in _anims) anim.Play(_state);
        }

        public void Tick(float time) { }

        public void Teardown()
        {
            foreach (var anim in _anims)
                if (anim != null) anim.Stop(_state);
        }
    }
}
