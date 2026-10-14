using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>
    /// PlayAsync on the real player loop: the animation timer drives the state and the await ends when it finishes or is canceled
    /// (design doc §4.3 — every stop goes through "finished" or "canceled", so no await is left hanging).
    /// </summary>
    public sealed class PlayAsyncPlayModeTests
    {
        const float Length = 0.3f;
        GameObject _go;
        UIAnimation _anim;

        // "Grow": scale 0 → 1 in 0.3 s with an event "Half" at 0.15 s. "Shrink": scale → 0.5 (takes Scale over).
        [SetUp]
        public void SetUp()
        {
            // Like a prefab: the states exist before the component wakes up (it builds its state table then).
            _go = new GameObject("Anim", typeof(RectTransform));
            _go.SetActive(false);
            _anim = _go.AddComponent<UIAnimation>();
            _anim.states.Add(State("Grow", Vector3.zero, Vector3.one));
            _anim.states[0].eventTracks.Add(new UIAnimEventTrack { events = { new UIAnimEventMarker { time = Length / 2, name = "Half" } } });
            _anim.states.Add(State("Shrink", Vector3.one, Vector3.one * 0.5f));
            _go.SetActive(true);
        }

        UIAnimState State(string name, Vector3 from, Vector3 to)
        {
            var track = new UIAnimTrack { target = _go.transform, property = UIAnimProperty.Scale, start = UIAnimStart.KeyValue };
            track.keys.Add(new UIAnimKey { time = 0f, value = from });
            track.keys.Add(new UIAnimKey { time = Length, value = to, ease = UIAnimEase.Linear });
            return new UIAnimState { name = name, tracks = { track } };
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_go);

        float Scale => _go.transform.localScale.x;

        // A test body written with async/await, run as a coroutine (no UniTask needed).
        static IEnumerator Run(Func<Awaitable> body)
        {
            var task = body();
            while (!task.IsCompleted) yield return null;
            task.GetAwaiter().GetResult();   // rethrows assertion failures
        }

        static async Awaitable<bool> Canceled(Awaitable task)
        {
            try
            {
                await task;
                return false;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
        }

        [UnityTest]
        public IEnumerator 끝까지_가면_길이만큼_기다린_뒤_끝_값으로_끝난다() => Run(async () =>
        {
            var events = new List<string>();
            _anim.EventRaised += (in UIAnimEventInfo e) => events.Add($"{e.Name}@{Time.realtimeSinceStartup:0.000}");
            var start = Time.realtimeSinceStartup;
            await _anim.PlayAsync("Grow");
            var elapsed = Time.realtimeSinceStartup - start;
            TestContext.WriteLine($"PlayAsync 대기 {elapsed:0.000}s (길이 {Length}s), 이벤트 {string.Join(", ", events)}");
            Assert.AreEqual(Length, elapsed, 0.1f, "상태 길이만큼 기다림");
            Assert.AreEqual(1f, Scale, 1e-4f, "끝 값");
            Assert.AreEqual(1, events.Count, "중간 이벤트가 끝나기 전에 한 번");
            Assert.IsFalse(_anim.IsPlaying("Grow"));
        });

        [UnityTest]
        public IEnumerator Complete를_부르면_바로_끝_값으로_완료된다() => Run(async () =>
        {
            var task = _anim.PlayAsync("Grow");
            await Awaitable.NextFrameAsync();
            _anim.Complete("Grow");
            Assert.IsFalse(await Canceled(task), "취소가 아니라 완료");
            Assert.AreEqual(1f, Scale, 1e-4f);
        });

        [UnityTest]
        public IEnumerator 같은_상태를_다시_재생하면_앞의_대기는_취소되고_뒤의_것이_끝난다() => Run(async () =>
        {
            var first = _anim.PlayAsync("Grow");
            await Awaitable.NextFrameAsync();
            var second = _anim.PlayAsync("Grow");
            Assert.IsTrue(await Canceled(first), "앞의 대기 취소");
            Assert.IsFalse(await Canceled(second), "뒤의 것은 완료");
            Assert.AreEqual(1f, Scale, 1e-4f);
        });

        [UnityTest]
        public IEnumerator 다른_상태가_속성을_가져가면_취소_Complete_인터럽트면_완료() => Run(async () =>
        {
            var grow = _anim.PlayAsync("Grow");
            await Awaitable.NextFrameAsync();
            _anim.Play("Shrink");   // Cancel: takes Scale, Grow stops where it is
            Assert.IsTrue(await Canceled(grow), "속성을 모두 빼앗기면 취소");

            grow = _anim.PlayAsync("Grow");
            await Awaitable.NextFrameAsync();
            _anim.Play("Shrink", UIAnimInterrupt.Complete);   // Complete: Grow jumps to its end first
            Assert.IsFalse(await Canceled(grow), "Complete 인터럽트면 완료");
        });

        [UnityTest]
        public IEnumerator 비활성화와_토큰_취소는_대기를_취소한다() => Run(async () =>
        {
            var grow = _anim.PlayAsync("Grow");
            await Awaitable.NextFrameAsync();
            _go.SetActive(false);
            Assert.IsTrue(await Canceled(grow), "OnDisable");
            _go.SetActive(true);

            using var cts = new CancellationTokenSource();
            grow = _anim.PlayAsync("Grow", cancellationToken: cts.Token);
            await Awaitable.NextFrameAsync();
            var stoppedAt = Scale;
            cts.Cancel();
            Assert.IsTrue(await Canceled(grow), "토큰 취소");
            await Awaitable.WaitForSecondsAsync(Length);
            Assert.AreEqual(stoppedAt, Scale, 0.05f, "취소된 자리에서 멈춤");
            Assert.IsFalse(_anim.IsPlaying("Grow"));
        });

        [UnityTest]
        public IEnumerator 이벤트를_기다릴_수_있다() => Run(async () =>
        {
            _anim.Play("Grow");
            var info = await _anim.WaitEventAsync("Half");
            Assert.AreEqual("Half", info.Name);
            Assert.That(Scale, Is.InRange(0.3f, 0.7f), "이벤트 시간(0.15s) 무렵의 값");
        });
    
#if UIANIM_UNITASK
        // ---------- UniTask integration (compiled only when UniTask is installed) ----------

        [UnityTest]
        public IEnumerator UniTask_연동_PlayUniTaskAsync는_WhenAll로_묶을_수_있다() => Cysharp.Threading.Tasks.UniTask.ToCoroutine(async () =>
        {
            var start = Time.realtimeSinceStartup;
            await Cysharp.Threading.Tasks.UniTask.WhenAll(_anim.PlayUniTaskAsync("Grow"), _anim.WaitEventUniTaskAsync("Half").AsUniTask());
            TestContext.WriteLine($"UniTask.WhenAll(PlayUniTaskAsync, WaitEventUniTaskAsync): {Time.realtimeSinceStartup - start:0.000}s");
            Assert.AreEqual(1f, Scale, 1e-4f);

            var canceled = false;
            var grow = _anim.PlayUniTaskAsync("Grow");
            await Cysharp.Threading.Tasks.UniTask.Yield();
            _anim.Play("Shrink");
            try { await grow; }
            catch (OperationCanceledException) { canceled = true; }
            Assert.IsTrue(canceled, "취소도 UniTask 취소로 전달");
        });
#endif
}
}
