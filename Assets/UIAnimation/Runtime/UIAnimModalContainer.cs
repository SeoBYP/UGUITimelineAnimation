using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SeoBYP.TimelineAnimation
{
    /// <summary>
    /// Modals on top of each other. Each has its own backdrop (a dimmed full-screen image) that fades with it;
    /// the modal under the new one does not move.
    /// </summary>
    [AddComponentMenu("UI Animation/UI Anim Modal Container")]
    public sealed class UIAnimModalContainer : UIAnimScreenContainer
    {
        [Tooltip("Shown behind each modal. Empty: a black image at 75 % that fades in and out.")]
        public UIAnimScreen backdropPrefab;

        readonly List<UIAnimScreen> _modals = new();
        readonly List<UIAnimScreen> _backdrops = new();

        public IReadOnlyList<UIAnimScreen> Modals => _modals;
        public UIAnimScreen Current => _modals.Count > 0 ? _modals[^1] : null;

        public async Awaitable<UIAnimScreen> Push(UIAnimScreen prefab, bool animated = true)
        {
            BeginTransition();
            try
            {
                var under = Current;
                var backdrop = backdropPrefab != null ? Spawn(backdropPrefab) : DefaultBackdrop();
                var modal = Spawn(prefab);
                modal.Group.alpha = 0f;
                _backdrops.Add(backdrop);
                _modals.Add(modal);

                var info = new UIAnimTransition(UIAnimTransitionKind.Push, under, false, animated);
                await modal.WillEnter(info);
                modal.Group.alpha = 1f;
                await Both(backdrop.Play("Enter", null, UIAnimDefaultTransitions.FadeIn, animated),
                    modal.Play("Enter", null, UIAnimDefaultTransitions.ModalEnter, animated));
                modal.DidEnter(info);
                return modal;
            }
            finally { EndTransition(); }
        }

        public async Awaitable Pop(bool animated = true)
        {
            if (_modals.Count == 0) return;
            BeginTransition();
            try
            {
                var modal = _modals[^1];
                var backdrop = _backdrops[^1];
                var info = new UIAnimTransition(UIAnimTransitionKind.Pop, _modals.Count > 1 ? _modals[^2] : null, true, animated);
                await modal.WillExit(info);
                await Both(backdrop.Play("Exit", null, UIAnimDefaultTransitions.FadeOut, animated),
                    modal.Play("Exit", null, UIAnimDefaultTransitions.ModalExit, animated));
                _modals.RemoveAt(_modals.Count - 1);
                _backdrops.RemoveAt(_backdrops.Count - 1);
                modal.DidExit(info);
                Destroy(modal.gameObject);
                Destroy(backdrop.gameObject);
            }
            finally { EndTransition(); }
        }

        UIAnimScreen DefaultBackdrop()
        {
            var go = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasGroup), typeof(UIAnimation), typeof(Image));
            go.transform.SetParent(transform, false);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.749f);
            var screen = go.AddComponent<UIAnimScreen>();
            screen.FillParent();
            return screen;
        }
    }
}
