using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace SeoBYP.TimelineAnimation
{
    /// <summary>
    /// Pull to refresh on a list: pulling past the start shows the Pull state by distance; releasing past Distance
    /// raises On Refresh and plays the Refreshing state (repeated) until <see cref="EndRefresh"/>, holding the list open meanwhile.
    /// For vertical lists (pulled down from the top).
    /// </summary>
    [AddComponentMenu("UI Animation/UI Anim Pull To Refresh")]
    [RequireComponent(typeof(UIAnimScrollList))]
    public sealed class UIAnimPullToRefresh : MonoBehaviour, IEndDragHandler
    {
        [Tooltip("The animation of the refresh indicator.")]
        public UIAnimation target;
        [Tooltip("Shown by the pull: start = not pulled, end = pulled Distance.")]
        public string pullState = "Pull";
        [Tooltip("Played over and over while refreshing.")]
        public string refreshingState = "Refreshing";
        [Min(1f)] public float distance = 160f;
        public UnityEvent onRefresh = new();

        UIAnimScrollList _list;
        float _last = -1f;

        public bool Refreshing { get; private set; }

        void Awake() => _list = GetComponent<UIAnimScrollList>();

        void LateUpdate()
        {
            if (target == null) return;
            if (Refreshing)
            {
                if (!string.IsNullOrEmpty(refreshingState) && !target.IsPlaying(refreshingState)) target.Play(refreshingState);
                return;
            }
            var t = Mathf.Clamp01(-_list.Offset / distance);
            if (Mathf.Approximately(t, _last)) return;
            _last = t;
            target.Sample(pullState, t * target.Duration(pullState));
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (Refreshing || -_list.Offset < distance) return;
            Refreshing = true;
            Hold(distance);
            onRefresh.Invoke();
        }

        // While refreshing, the list stays open by Distance (extra top padding) so the indicator stays in view.
        // The offset moves by the same amount, so nothing jumps; the ScrollRect's elastic glides the rest.
        void Hold(float by)
        {
            var offset = _list.Offset;
            _list.padding.top += Mathf.RoundToInt(by);
            _list.Rebuild();
            _list.SetOffset(offset + by, clamp: false);
        }

        /// <summary>The refresh finished: the indicator goes back with the list.</summary>
        public void EndRefresh()
        {
            if (!Refreshing) return;
            Refreshing = false;
            Hold(-distance);
            if (target != null) target.Complete(refreshingState);
            _last = -1f;
        }
    }
}
