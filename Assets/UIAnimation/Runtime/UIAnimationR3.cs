#if UIANIM_R3
using System;
using System.Threading;
using R3;

namespace SeoBYP.TimelineAnimation
{
    /// <summary>
    /// R3 integration, compiled only when R3 (com.cysharp.r3) is installed: states and animation events as Observables,
    /// so they compose with the rest of a reactive UI (button clicks, model changes, throttling …).
    /// </summary>
    public static class UIAnimationR3Extensions
    {
        /// <summary>
        /// Plays the state when subscribed: one Unit when it reaches its end, then completes. It completes with an error
        /// (OperationCanceledException) when the state does not finish (taken over, played again, object disabled).
        /// Disposing the subscription stops the state where it is.
        /// </summary>
        public static Observable<Unit> PlayAsObservable(this UIAnimation animation, string stateName,
            UIAnimInterrupt interrupt = UIAnimInterrupt.Cancel) =>
            Observable.Create<Unit, (UIAnimation, string, UIAnimInterrupt)>((animation, stateName, interrupt), static (observer, s) =>
            {
                var cancel = new CancellationTokenSource();
                Run(observer, s.Item1, s.Item2, s.Item3, cancel.Token);
                return Disposable.Create(cancel, static c => { c.Cancel(); c.Dispose(); });
            });

        static async void Run(Observer<Unit> observer, UIAnimation animation, string state, UIAnimInterrupt interrupt, CancellationToken token)
        {
            try
            {
                await animation.PlayAsync(state, interrupt, token);
                observer.OnNext(Unit.Default);
                observer.OnCompleted();
            }
            catch (OperationCanceledException e)
            {
                if (!token.IsCancellationRequested) observer.OnCompleted(Result.Failure(e));   // not our own dispose
            }
        }

        /// <summary>Every animation event raised by any state of this animation, while subscribed.</summary>
        public static Observable<UIAnimEventInfo> EventsAsObservable(this UIAnimation animation) =>
            Observable.Create<UIAnimEventInfo, UIAnimation>(animation, static (observer, a) =>
            {
                void OnEvent(in UIAnimEventInfo e) => observer.OnNext(e);
                a.EventRaised += OnEvent;
                return Disposable.Create(() => { if (a != null) a.EventRaised -= OnEvent; });
            });

        /// <summary>The animation events of one name (e.g. "Shown").</summary>
        public static Observable<UIAnimEventInfo> EventAsObservable(this UIAnimation animation, string eventName) =>
            animation.EventsAsObservable().Where(eventName, static (e, name) => e.Name == name);
    }
}
#endif
