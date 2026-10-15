using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>Burst batch (UIAnimBatch): the values it computes for a frame are the ones UIAnimTrack.Sample gives, and Step writes them.</summary>
    public sealed class UIAnimBatchTests
    {
        readonly List<Object> _destroy = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _destroy) Object.DestroyImmediate(o);
            _destroy.Clear();
        }

        static UIAnimKey Key(float time, Vector4 value, UIAnimEase ease = UIAnimEase.Linear) => new() { time = time, value = value, ease = ease };

        // Position: 3 keys with eases. Scale: a lone key reached from the current value. Active: on / off. Size: an AnimationCurve (managed).
        (UIAnimation anim, RectTransform rect, UIAnimState state) Make(bool loop = false)
        {
            var go = new GameObject("Batch", typeof(RectTransform));
            _destroy.Add(go);
            var child = new GameObject("Child", typeof(RectTransform));
            child.transform.SetParent(go.transform);
            var rect = (RectTransform)go.transform;
            rect.localScale = new Vector3(0.5f, 0.5f, 1f);
            var state = new UIAnimState { name = "S", loop = loop };
            var position = new UIAnimTrack { target = rect, property = UIAnimProperty.AnchoredPosition, start = UIAnimStart.KeyValue };
            position.keys.Add(Key(0.1f, Vector4.zero));
            position.keys.Add(Key(0.5f, new Vector4(100, -40), UIAnimEase.OutBack));
            position.keys.Add(Key(1f, new Vector4(-20, 60), UIAnimEase.InOutSine));
            var scale = new UIAnimTrack { target = rect, property = UIAnimProperty.Scale, start = UIAnimStart.Current };
            scale.keys.Add(Key(0.6f, Vector4.one, UIAnimEase.OutElastic));
            var active = new UIAnimTrack { target = child.transform, property = UIAnimProperty.Active, start = UIAnimStart.KeyValue };
            active.keys.Add(Key(0f, Vector4.zero));
            active.keys.Add(Key(0.4f, Vector4.one));
            var size = new UIAnimTrack { target = rect, property = UIAnimProperty.SizeDelta, start = UIAnimStart.KeyValue };
            size.keys.Add(Key(0f, Vector4.zero));
            size.keys.Add(new UIAnimKey { time = 1f, value = new Vector4(10, 10), curveType = UIAnimCurveType.Curve, curve = AnimationCurve.EaseInOut(0, 0, 1, 1) });
            state.tracks.AddRange(new[] { position, scale, active, size });
            go.SetActive(false);
            var anim = go.AddComponent<UIAnimation>();
            anim.states.Add(state);
            go.SetActive(true);
            return (anim, rect, state);
        }

        static void AssertSame(UIAnimState state, float t)
        {
            foreach (var track in state.tracks)
            {
                if (track.lane < 0) continue;
                Vector4 batched = UIAnimBatch.Value(track.lane), managed = track.Sample(t);
                Assert.Less((batched - managed).magnitude, 1e-4f, $"{track.property} at {t}: {batched} vs {managed}");
            }
        }

        [Test]
        public void 배치_값은_관리_코드_샘플과_같다()
        {
            var (anim, _, state) = Make();
            var p = anim.Prepare("S", UIAnimInterrupt.Cancel, null);
            UIAnimation.StartMotion(p);
            Assert.AreEqual(3, state.tracks.FindAll(tr => tr.lane >= 0).Count, "커브 트랙만 관리 코드로 남는다");
            Assert.AreEqual(-1, state.tracks[3].lane);
            foreach (var at in new[] { 0.05, 0.2, 0.45, 0.55, 0.8, 0.99, 3.0 })
            {
                var now = p.handle.StartTime + at;
                UIAnimBatch.Run(now);
                var t = Mathf.Min((float)(now - p.handle.StartTime), p.duration);
                Assert.IsTrue(UIAnimBatch.Ready(p, t), $"{at}초");
                AssertSame(state, t);
            }
            anim.Stop("S");
        }

        [Test]
        public void 역재생과_반복도_같은_시간을_계산한다()
        {
            var (anim, _, state) = Make(loop: true);
            var p = anim.Prepare("S", UIAnimInterrupt.Cancel, null, reversed: true);
            UIAnimation.StartMotion(p);
            var now = p.handle.StartTime + 2.3;
            UIAnimBatch.Run(now);
            var t = p.duration - (float)(now - p.handle.StartTime) % p.duration;
            Assert.IsTrue(UIAnimBatch.Ready(p, t));
            AssertSame(state, t);
            anim.Stop("S");
        }

        [Test]
        public void Step은_배치_값을_쓰고_다시_재생하면_자리를_재사용한다()
        {
            var (anim, rect, state) = Make();
            var p = anim.Prepare("S", UIAnimInterrupt.Cancel, null);
            UIAnimation.StartMotion(p);
            var now = p.handle.StartTime + 0.3;
            UIAnimBatch.Run(now);
            var t = (float)(now - p.handle.StartTime);
            UIAnimation.Step(p, t);
            Assert.AreEqual((Vector2)(Vector4)UIAnimBatch.Value(state.tracks[0].lane), rect.anchoredPosition);
            anim.Stop("S");
            Assert.IsFalse(UIAnimBatch.Ready(p, t), "멈춘 재생은 배치 값을 쓰지 않는다");

            // Played again: the same slot is rewritten, not packed anew.
            var (group, lane) = (p.group, state.tracks[0].lane);
            p = anim.Prepare("S", UIAnimInterrupt.Cancel, null);
            UIAnimation.StartMotion(p);
            Assert.AreEqual((group, lane), (p.group, state.tracks[0].lane));
            now = p.handle.StartTime + 0.7;
            UIAnimBatch.Run(now);
            t = (float)(now - p.handle.StartTime);
            Assert.IsTrue(UIAnimBatch.Ready(p, t));
            AssertSame(state, t);
            anim.Stop("S");
        }
    }
}
