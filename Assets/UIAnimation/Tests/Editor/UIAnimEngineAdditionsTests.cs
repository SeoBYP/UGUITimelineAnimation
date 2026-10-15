using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>
    /// Engine additions for the reference samples: the new tracks (CanvasGroup alpha, anchors, pivot, camera, active),
    /// reversed playback and Unity-AnimationUI's OutBack eases.
    /// </summary>
    public sealed class UIAnimEngineAdditionsTests
    {
        readonly List<Object> _destroy = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _destroy) Object.DestroyImmediate(o);
            _destroy.Clear();
        }

        GameObject Go(string name = "Go")
        {
            var go = new GameObject(name, typeof(RectTransform));
            _destroy.Add(go);
            return go;
        }

        static UIAnimTrack Track(Component target, UIAnimProperty property, params (float time, Vector4 value)[] keys)
        {
            var track = new UIAnimTrack { target = target, property = property, start = UIAnimStart.KeyValue };
            foreach (var (time, value) in keys) track.keys.Add(new UIAnimKey { time = time, value = value, ease = UIAnimEase.Linear });
            return track;
        }

        // The component wakes up with its states already there, like a prefab.
        UIAnimation Anim(GameObject go, params UIAnimState[] states)
        {
            go.SetActive(false);
            var anim = go.AddComponent<UIAnimation>();
            anim.states.AddRange(states);
            go.SetActive(true);
            return anim;
        }

        // ---------- A1: new tracks ----------

        [Test]
        public void 새_트랙은_값을_읽고_쓴다()
        {
            var go = Go();
            var rect = (RectTransform)go.transform;
            var group = go.AddComponent<CanvasGroup>();
            var camera = Go("Camera").AddComponent<Camera>();

            void RoundTrip(Component target, UIAnimProperty property, Vector4 value, string what)
            {
                UIAnimation.Write(target, property, value);
                var read = UIAnimation.Read(target, property);
                Assert.Less(Vector4.Distance(value, read), 1e-4f, $"{what}: {value} → {read}");
            }
            RoundTrip(group, UIAnimProperty.CanvasGroupAlpha, new Vector4(0.25f, 0, 0, 0), "CanvasGroup Alpha");
            RoundTrip(rect, UIAnimProperty.AnchorMin, new Vector4(0.1f, 0.2f, 0, 0), "Anchor Min");
            RoundTrip(rect, UIAnimProperty.AnchorMax, new Vector4(0.9f, 0.8f, 0, 0), "Anchor Max");
            RoundTrip(rect, UIAnimProperty.Pivot, new Vector4(0f, 1f, 0, 0), "Pivot");
            RoundTrip(camera, UIAnimProperty.CameraBackground, new Vector4(0.5f, 0.9f, 0.6f, 1f), "Camera Background");
            RoundTrip(camera, UIAnimProperty.CameraOrthoSize, new Vector4(0.5f, 0, 0, 0), "Camera Size");

            var child = Go("Child");
            UIAnimation.Write(child.transform, UIAnimProperty.Active, Vector4.zero);
            Assert.IsFalse(child.activeSelf);
            Assert.AreEqual(0f, UIAnimation.Read(child.transform, UIAnimProperty.Active).x);
            UIAnimation.Write(child.transform, UIAnimProperty.Active, new Vector4(1, 0, 0, 0));
            Assert.IsTrue(child.activeSelf);
        }

        [Test]
        public void Active_트랙은_키_시간에_바로_바뀐다()
        {
            var panel = Go("Panel");
            var track = Track(panel.transform, UIAnimProperty.Active, (0f, Vector4.zero), (0.5f, new Vector4(1, 0, 0, 0)), (1f, Vector4.zero));
            var anim = Anim(Go("Root"), new UIAnimState { name = "Show", tracks = { track } });
            var playback = anim.Prepare("Show", UIAnimInterrupt.Cancel, null);

            UIAnimation.Step(playback, 0.49f);
            Assert.IsFalse(panel.activeSelf, "0.5초 전까지는 꺼짐 (중간값 없음)");
            UIAnimation.Step(playback, 0.5f);
            Assert.IsTrue(panel.activeSelf, "키 시간에 켜짐");
            UIAnimation.Step(playback, 0.99f);
            Assert.IsTrue(panel.activeSelf);
            UIAnimation.Step(playback, 1f);
            Assert.IsFalse(panel.activeSelf);
        }

        [Test]
        public void Anchor_트랙은_해상도와_상관없이_부모_비율로_움직인다()
        {
            var parent = Go("Parent");
            ((RectTransform)parent.transform).sizeDelta = new Vector2(1000, 500);
            var row = Go("Row");
            row.transform.SetParent(parent.transform, false);
            var rect = (RectTransform)row.transform;
            var track = Track(rect, UIAnimProperty.AnchorMin, (0f, new Vector4(0, 0.5f)), (1f, new Vector4(0.5f, 0.5f)));
            var anim = Anim(Go("Root"), new UIAnimState { name = "In", tracks = { track } });
            var playback = anim.Prepare("In", UIAnimInterrupt.Cancel, null);
            UIAnimation.Step(playback, 0.5f);
            Assert.AreEqual(new Vector2(0.25f, 0.5f), rect.anchorMin);
        }

        // ---------- A2: reversed playback ----------

        UIAnimState ScaleState(GameObject target, List<string> raised)
        {
            var state = new UIAnimState
            {
                name = "Open",
                tracks = { Track(target.transform, UIAnimProperty.Scale, (0f, Vector3.zero), (1f, Vector3.one)) },
            };
            state.eventTracks.Add(new UIAnimEventTrack
            {
                events = { new UIAnimEventMarker { time = 0.2f, name = "A" }, new UIAnimEventMarker { time = 0.8f, name = "B" } },
            });
            return state;
        }

        [Test]
        public void 역재생은_끝에서_시작으로_가고_이벤트도_거꾸로_한_번씩()
        {
            var box = Go("Box");
            var raised = new List<string>();
            var anim = Anim(Go("Root"), ScaleState(box, raised));
            anim.EventRaised += (in UIAnimEventInfo e) => raised.Add($"{e.Name}");

            var playback = anim.Prepare("Open", UIAnimInterrupt.Cancel, null, reversed: true);
            Assert.AreEqual(1f, box.transform.localScale.x, 1e-4f, "끝 모습에서 시작");
            UIAnimation.Step(playback, 0.75f);
            Assert.AreEqual(0.75f, box.transform.localScale.x, 1e-4f);
            CollectionAssert.AreEqual(new[] { "B" }, raised, "0.8의 B가 먼저");
            UIAnimation.Step(playback, 0f);
            Assert.AreEqual(0f, box.transform.localScale.x, 1e-4f, "시작 모습에서 끝");
            CollectionAssert.AreEqual(new[] { "B", "A" }, raised);
            UIAnimation.Finish(playback);
            CollectionAssert.AreEqual(new[] { "B", "A" }, raised, "끝낼 때 다시 발생하지 않음");
            Assert.IsFalse(anim.IsPlaying("Open"));
        }

        [Test]
        public void 역재생은_현재값에서_시작한_트랙을_정방향이_시작한_자리로_되돌린다()
        {
            var box = Go("Box");
            var track = new UIAnimTrack { target = box.transform, property = UIAnimProperty.Scale, start = UIAnimStart.Current };
            track.keys.Add(new UIAnimKey { time = 0f });
            track.keys.Add(new UIAnimKey { time = 1f, value = Vector3.one * 2f, ease = UIAnimEase.Linear });
            var anim = Anim(Go("Root"), new UIAnimState { name = "Grow", tracks = { track } });

            box.transform.localScale = Vector3.one * 0.5f;   // where the forward play starts
            var forward = anim.Prepare("Grow", UIAnimInterrupt.Cancel, null);
            UIAnimation.Step(forward, 1f);
            UIAnimation.Finish(forward);
            Assert.AreEqual(2f, box.transform.localScale.x, 1e-4f);

            var back = anim.Prepare("Grow", UIAnimInterrupt.Cancel, null, reversed: true);
            UIAnimation.Step(back, 0f);
            Assert.AreEqual(0.5f, box.transform.localScale.x, 1e-4f, "현재 값(2)이 아니라 열기 전 자리(0.5)로");
        }

        [Test]
        public void 역재생_중_Complete는_시작_모습으로_끝낸다()
        {
            var box = Go("Box");
            var anim = Anim(Go("Root"), ScaleState(box, null));
            anim.PlayReversed("Open");
            Assert.IsTrue(anim.IsPlaying("Open"));
            anim.Complete("Open");
            Assert.AreEqual(0f, box.transform.localScale.x, 1e-4f);
            Assert.IsFalse(anim.IsPlaying("Open"));
        }

        [Test]
        public void 역재생하면_자식_호출도_역재생된다()
        {
            var childGo = Go("Child");
            var child = Anim(childGo, new UIAnimState
            {
                name = "Pop",
                tracks = { Track(childGo.transform, UIAnimProperty.Scale, (0f, Vector3.zero), (0.5f, Vector3.one)) },
            });
            var parent = Anim(Go("Parent"), new UIAnimState
            {
                name = "Open",
                tracks = { Track(Go("Dummy").transform, UIAnimProperty.Scale, (0f, Vector3.zero), (1f, Vector3.one)) },
                childCalls = { new UIAnimChildCall { time = 0.5f, child = child, state = "Pop" } },
            });
            var playback = parent.Prepare("Open", UIAnimInterrupt.Cancel, null, reversed: true);
            UIAnimation.Step(playback, 0.4f);   // passes 0.5 going back
            Assert.IsTrue(child.IsPlaying("Pop"));
            Assert.AreEqual(1f, childGo.transform.localScale.x, 1e-4f, "자식도 끝 모습에서 거꾸로 시작");
        }

        // ---------- A3: Unity-AnimationUI OutBack eases ----------

        [Test]
        public void AnimationUI_OutBack_이징은_원본_공식과_같다()
        {
            // f(x) = -(x-1)^4 + p(x-1)^3 + p(x-1)^2 + 1, at x = 0.5: -0.0625 - p/8 + p/4 + 1
            foreach (var (ease, p) in new[] { (UIAnimEase.OutBackQuad, 2f), (UIAnimEase.OutBackCubic, 3f), (UIAnimEase.OutBackQuart, 4f), (UIAnimEase.OutBackQuint, 5f) })
            {
                Assert.AreEqual(-0.0625f - p / 8f + p / 4f + 1f, UIAnimEasing.Evaluate(0.5f, ease), 1e-6f, ease.ToString());
                Assert.AreEqual(0f, UIAnimEasing.Evaluate(0f, ease), 1e-6f);
                Assert.AreEqual(1f, UIAnimEasing.Evaluate(1f, ease), 1e-6f);
            }
            Assert.AreEqual(1.4375f, UIAnimEasing.Evaluate(0.5f, UIAnimEase.OutBackQuart), 1e-6f, "버튼에 쓰는 OutBackQuart: 중간에 1.44배까지 넘침");
            Assert.AreEqual(34, (int)UIAnimEase.OutBackQuart, "저장 번호는 기존 뒤에 추가");
        }
    }
}
