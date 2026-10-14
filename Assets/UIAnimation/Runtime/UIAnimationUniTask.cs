#if UIANIM_UNITASK
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SeoBYP.TimelineAnimation
{
    /// <summary>
    /// UniTask integration, compiled only when UniTask (com.cysharp.unitask) is installed. The core API returns Unity's
    /// Awaitable; these return UniTask so states combine with UniTask.WhenAll / WhenAny, timeouts and the rest of UniTask.
    /// </summary>
    public static class UIAnimationUniTaskExtensions
    {
        /// <summary><see cref="UIAnimation.PlayAsync"/> as a UniTask (same completion and cancellation).</summary>
        public static UniTask PlayUniTaskAsync(this UIAnimation animation, string stateName,
            UIAnimInterrupt interrupt = UIAnimInterrupt.Cancel, CancellationToken cancellationToken = default) =>
            animation.PlayAsync(stateName, interrupt, cancellationToken).AsUniTask();

        /// <summary><see cref="UIAnimation.PlayReversedAsync"/> as a UniTask.</summary>
        public static UniTask PlayReversedUniTaskAsync(this UIAnimation animation, string stateName,
            UIAnimInterrupt interrupt = UIAnimInterrupt.Cancel, CancellationToken cancellationToken = default) =>
            animation.PlayReversedAsync(stateName, interrupt, cancellationToken).AsUniTask();

        /// <summary><see cref="UIAnimation.WaitEventAsync"/> as a UniTask.</summary>
        public static UniTask<UIAnimEventInfo> WaitEventUniTaskAsync(this UIAnimation animation, string eventName,
            CancellationToken cancellationToken = default) =>
            animation.WaitEventAsync(eventName, cancellationToken).AsUniTask();
    }
}
#endif
