using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>R3 integration (compiled only when R3 is installed): states and animation events as Observables.</summary>
    public sealed class R3PlayModeTests
    {
        GameObject _go;

        [TearDown]
        public void TearDown() => UnityEngine.Object.Destroy(_go);

        // x 0 → 100 in 0.3 s, events "Start" (0) and "Shown" (0.2).
        UIAnimation Anim()
        {
            _go = new GameObject("Anim", typeof(RectTransform));
            _go.SetActive(false);
            var anim = _go.AddComponent<UIAnimation>();
            var state = new UIAnimState { name = "Open" };
            var track = new UIAnimTrack { target = _go.transform, property = UIAnimProperty.AnchoredPosition, start = UIAnimStart.KeyValue };
            track.keys.Add(new UIAnimKey { time = 0, value = Vector4.zero });
            track.keys.Add(new UIAnimKey { time = 0.3f, value = new Vector4(100, 0), ease = UIAnimEase.Linear });
            state.tracks.Add(track);
            state.AddEvent(new UIAnimEventMarker { time = 0f, name = "Start" });
            state.AddEvent(new UIAnimEventMarker { time = 0.2f, name = "Shown" });
            anim.states.Add(state);
            anim.states.Add(new UIAnimState { name = "Other", tracks = { new UIAnimTrack { target = _go.transform, property = UIAnimProperty.AnchoredPosition, start = UIAnimStart.KeyValue,
                keys = { new UIAnimKey { time = 0, value = Vector4.zero }, new UIAnimKey { time = 0.3f, value = Vector4.one } } } } });
            _go.SetActive(true);
            return anim;
        }

        [UnityTest]
        public IEnumerator PlayAsObservable은_끝나면_한_번_내보내고_완료된다()
        {
            var anim = Anim();
            var values = 0;
            Result? completed = null;
            anim.PlayAsObservable("Open").Subscribe(_ => values++, r => completed = r);
            yield return null;
            Assert.IsTrue(anim.IsPlaying("Open"), "구독하면 재생");
            Assert.AreEqual(0, values);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(1, values);
            Assert.IsTrue(completed.HasValue && completed.Value.IsSuccess);
            Assert.AreEqual(100f, ((RectTransform)anim.transform).anchoredPosition.x, 0.01f);
        }

        [UnityTest]
        public IEnumerator 구독을_끊으면_그_자리에서_멈춘다()
        {
            var anim = Anim();
            var subscription = anim.PlayAsObservable("Open").Subscribe();
            yield return new WaitForSeconds(0.1f);
            subscription.Dispose();
            Assert.IsFalse(anim.IsPlaying("Open"));
            var x = ((RectTransform)anim.transform).anchoredPosition.x;
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(x, ((RectTransform)anim.transform).anchoredPosition.x, 0.001f, "더 움직이지 않음");
        }

        [UnityTest]
        public IEnumerator 다른_상태가_가져가면_오류로_완료된다()
        {
            var anim = Anim();
            Result? completed = null;
            anim.PlayAsObservable("Open").Subscribe(_ => { }, r => completed = r);
            yield return new WaitForSeconds(0.1f);
            anim.Play("Other");
            yield return null;
            Assert.IsTrue(completed.HasValue && completed.Value.IsFailure);
            Assert.IsInstanceOf<OperationCanceledException>(completed.Value.Exception);
        }

        [UnityTest]
        public IEnumerator 이벤트를_Observable로_받는다()
        {
            var anim = Anim();
            var all = new List<string>();
            var shown = 0;
            using var a = anim.EventsAsObservable().Subscribe(e => all.Add(e.Name));
            using var b = anim.EventAsObservable("Shown").Subscribe(e => { shown++; Assert.AreEqual("Open", e.State); });
            anim.Play("Open");
            yield return new WaitForSeconds(0.5f);
            CollectionAssert.AreEqual(new[] { "Start", "Shown" }, all);
            Assert.AreEqual(1, shown);
        }
    }
}
