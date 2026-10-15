using System.Collections.Generic;
using UnityEngine;

namespace SeoBYP.TimelineAnimation
{
    /// <summary>
    /// Shared by the page, modal and sheet containers: input is off on every container while any of them is in a
    /// transition (so a second tap cannot start another one), and screens get their default transitions from here.
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public abstract class UIAnimScreenContainer : MonoBehaviour
    {
        static readonly List<UIAnimScreenContainer> Containers = new();
        static int _transitions;

        /// <summary>Some container is between screens.</summary>
        public static bool AnyInTransition => _transitions > 0;
        public bool IsInTransition { get; private set; }

        protected virtual void OnEnable()
        {
            Containers.Add(this);
            ApplyInput();
        }

        protected virtual void OnDisable() => Containers.Remove(this);

        protected void BeginTransition()
        {
            IsInTransition = true;
            _transitions++;
            foreach (var c in Containers) c.ApplyInput();
        }

        protected void EndTransition()
        {
            IsInTransition = false;
            _transitions = Mathf.Max(0, _transitions - 1);
            foreach (var c in Containers) c.ApplyInput();
        }

        void ApplyInput()
        {
            var group = GetComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = _transitions == 0;
        }

        protected UIAnimScreen Spawn(UIAnimScreen prefab)
        {
            var screen = Instantiate(prefab, transform, false);
            screen.FillParent();
            return screen;
        }

        // Highest renderingOrder on top; equal orders keep the order they were added in.
        protected void SortScreens(IList<UIAnimScreen> screens)
        {
            var ordered = new List<UIAnimScreen>(screens);
            for (var i = 0; i < ordered.Count; i++)
            for (var j = i + 1; j < ordered.Count; j++)
                if (ordered[j].renderingOrder < ordered[i].renderingOrder) (ordered[i], ordered[j]) = (ordered[j], ordered[i]);
            foreach (var s in ordered) s.transform.SetAsLastSibling();
        }

        protected static async Awaitable Both(Awaitable a, Awaitable b)
        {
            if (a != null) await a;
            if (b != null) await b;
        }
    }
}
