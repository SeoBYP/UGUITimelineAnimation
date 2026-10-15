using System.Collections.Generic;
using UnityEngine;

namespace SeoBYP.TimelineAnimation
{
    /// <summary>
    /// Sheets (tab pages) without history: Show switches to one, the old one leaves at the same time.
    /// Moving to a lower index plays "EnterBackward" / "ExitBackward" when the sheet has them (e.g. slide the other way).
    /// </summary>
    [AddComponentMenu("UI Animation/UI Anim Sheet Container")]
    public sealed class UIAnimSheetContainer : UIAnimScreenContainer
    {
        readonly List<UIAnimScreen> _sheets = new();
        public IReadOnlyList<UIAnimScreen> Sheets => _sheets;
        public int ActiveIndex { get; private set; } = -1;
        public UIAnimScreen Active => ActiveIndex >= 0 ? _sheets[ActiveIndex] : null;

        /// <summary>Adds a sheet (hidden) and returns its index.</summary>
        public int Register(UIAnimScreen prefab)
        {
            var sheet = Spawn(prefab);
            sheet.gameObject.SetActive(false);
            _sheets.Add(sheet);
            return _sheets.Count - 1;
        }

        public async Awaitable Show(int index, bool animated = true)
        {
            if (index == ActiveIndex || index < 0 || index >= _sheets.Count) return;
            BeginTransition();
            try
            {
                var exit = Active;
                var enter = _sheets[index];
                var backward = exit != null && index < ActiveIndex;
                enter.gameObject.SetActive(true);
                enter.transform.SetAsLastSibling();
                enter.Group.alpha = 0f;
                ActiveIndex = index;

                var enterInfo = new UIAnimTransition(UIAnimTransitionKind.Show, exit, backward, animated);
                var exitInfo = new UIAnimTransition(UIAnimTransitionKind.Hide, enter, backward, animated);
                await Both(enter.WillEnter(enterInfo), exit != null ? exit.WillExit(exitInfo) : null);
                enter.Group.alpha = 1f;
                await Both(enter.Play(backward ? "EnterBackward" : "Enter", "Enter", UIAnimDefaultTransitions.FadeIn, animated),
                    exit != null ? exit.Play(backward ? "ExitBackward" : "Exit", "Exit", UIAnimDefaultTransitions.FadeOut, animated) : null);
                if (exit != null)
                {
                    exit.gameObject.SetActive(false);
                    exit.DidExit(exitInfo);
                }
                enter.DidEnter(enterInfo);
            }
            finally { EndTransition(); }
        }
    }
}
