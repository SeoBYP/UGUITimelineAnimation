using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>State Loop: a looping state runs again from its start at its end, without a gap, until stopped.</summary>
    public sealed class UIAnimLoopTests
    {
        readonly List<Object> _destroy = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _destroy) Object.DestroyImmediate(o);
            _destroy.Clear();
        }

        // x 0 → 100 over 1 s (Linear), events "Start" at 0 and "Mid" at 0.5.
        (UIAnimation anim, RectTransform rect) Looping()
        {
            var go = new GameObject("Loop", typeof(RectTransform));
            _destroy.Add(go);
            var rect = (RectTransform)go.transform;
            var state = new UIAnimState { name = "Idle", loop = true };
            var track = new UIAnimTrack { target = rect, property = UIAnimProperty.AnchoredPosition, start = UIAnimStart.KeyValue };
            track.keys.Add(new UIAnimKey { time = 0f, value = Vector4.zero, ease = UIAnimEase.Linear });
            track.keys.Add(new UIAnimKey { time = 1f, value = new Vector4(100, 0), ease = UIAnimEase.Linear });
            state.tracks.Add(track);
            state.AddEvent(new UIAnimEventMarker { time = 0f, name = "Start" });
            state.AddEvent(new UIAnimEventMarker { time = 0.5f, name = "Mid" });
            go.SetActive(false);
            var anim = go.AddComponent<UIAnimation>();
            anim.states.Add(state);
            go.SetActive(true);
            return (anim, rect);
        }

        [Test]
        public void 반복_상태는_끝에서_처음으로_이어진다()
        {
            var (anim, rect) = Looping();
            var p = anim.Prepare("Idle", UIAnimInterrupt.Cancel, null);
            UIAnimation.Lap(p, 0.5f);
            Assert.AreEqual(50f, rect.anchoredPosition.x, 0.01f);
            UIAnimation.Lap(p, 1.25f);
            Assert.AreEqual(25f, rect.anchoredPosition.x, 0.01f, "두 번째 바퀴 0.25초");
            UIAnimation.Lap(p, 7.75f);
            Assert.AreEqual(75f, rect.anchoredPosition.x, 0.01f, "여덟 번째 바퀴");
        }

        [Test]
        public void 바퀴마다_이벤트가_다시_울린다()
        {
            var (anim, _) = Looping();
            var raised = new List<string>();
            anim.EventRaised += (in UIAnimEventInfo e) => raised.Add(e.Name);
            var p = anim.Prepare("Idle", UIAnimInterrupt.Cancel, null);   // Start (0)
            UIAnimation.Lap(p, 0.6f);    // Mid
            UIAnimation.Lap(p, 1.2f);    // lap 2: Start
            UIAnimation.Lap(p, 1.7f);    // Mid
            UIAnimation.Lap(p, 2.1f);    // lap 3: Start
            CollectionAssert.AreEqual(new[] { "Start", "Mid", "Start", "Mid", "Start" }, raised);
        }

        [Test]
        public void 역재생_반복은_처음에서_끝으로_이어진다()
        {
            var (anim, rect) = Looping();
            var p = anim.Prepare("Idle", UIAnimInterrupt.Cancel, null, reversed: true);
            UIAnimation.Lap(p, 0.25f);
            Assert.AreEqual(75f, rect.anchoredPosition.x, 0.01f);
            UIAnimation.Lap(p, 1.25f);
            Assert.AreEqual(75f, rect.anchoredPosition.x, 0.01f, "두 번째 바퀴도 끝에서부터");
        }

        [Test]
        public void 반복_상태는_Stop할_때까지_재생_중이다()
        {
            var (anim, _) = Looping();
            anim.Play("Idle");
            Assert.IsTrue(anim.IsPlaying("Idle"));
            Assert.AreEqual(1f, anim.Duration("Idle"), 1e-4f, "길이는 한 바퀴");
            anim.Stop("Idle");
            Assert.IsFalse(anim.IsPlaying("Idle"));
        }
    }
}
