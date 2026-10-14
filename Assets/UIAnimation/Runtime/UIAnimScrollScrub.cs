using UnityEngine;

namespace SeoBYP.TimelineAnimation
{
    /// <summary>
    /// Shows a state by how far a list is scrolled instead of by time: 0 px = the state's start, Distance = its end
    /// (a header that collapses as the list scrolls, a toolbar that fades in).
    /// </summary>
    [AddComponentMenu("UI Animation/UI Anim Scroll Scrub")]
    public sealed class UIAnimScrollScrub : MonoBehaviour
    {
        public UIAnimScrollList list;
        [Tooltip("The animation whose state is shown (e.g. on the header).")]
        public UIAnimation target;
        public string state = "";
        [Tooltip("Scroll distance in pixels from the state's start to its end.")]
        [Min(1f)] public float distance = 300f;

        float _last = -1f;

        void LateUpdate()
        {
            if (list == null || target == null || string.IsNullOrEmpty(state)) return;
            var t = Mathf.Clamp01(list.Offset / distance);
            if (Mathf.Approximately(t, _last)) return;
            _last = t;
            target.Sample(state, t * target.Duration(state));
        }
    }
}
