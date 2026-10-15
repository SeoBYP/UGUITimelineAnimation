using System.Collections.Generic;
using UnityEngine;

namespace SeoBYP.TimelineAnimation
{
    /// <summary>
    /// A history of pages: Push puts a page on top (the old one leaves at the same time), Pop goes back.
    /// A page pushed with stack = false is dropped from the history once the next page is in (e.g. a loading page).
    /// </summary>
    [AddComponentMenu("UI Animation/UI Anim Page Container")]
    public sealed class UIAnimPageContainer : UIAnimScreenContainer
    {
        readonly List<UIAnimScreen> _pages = new();
        readonly List<bool> _stacked = new();

        public IReadOnlyList<UIAnimScreen> Pages => _pages;
        public UIAnimScreen Current => _pages.Count > 0 ? _pages[^1] : null;

        public async Awaitable<UIAnimScreen> Push(UIAnimScreen prefab, bool animated = true, bool stack = true)
        {
            BeginTransition();
            try
            {
                var exit = Current;
                var exitStacked = _stacked.Count > 0 && _stacked[^1];
                var enter = Spawn(prefab);
                enter.Group.alpha = 0f;   // hidden while it gets ready
                _pages.Add(enter);
                _stacked.Add(stack);
                SortScreens(_pages);

                var enterInfo = new UIAnimTransition(UIAnimTransitionKind.Push, exit, false, animated);
                var exitInfo = new UIAnimTransition(UIAnimTransitionKind.Push, enter, false, animated);
                await Both(enter.WillEnter(enterInfo), exit != null ? exit.WillExit(exitInfo) : null);
                enter.Group.alpha = 1f;
                await Both(enter.Play("PushEnter", null, UIAnimDefaultTransitions.PagePushEnter, animated),
                    exit != null ? exit.Play("PushExit", null, UIAnimDefaultTransitions.PagePushExit, animated) : null);

                if (exit != null)
                {
                    exit.gameObject.SetActive(false);
                    exit.DidExit(exitInfo);
                    if (!exitStacked)
                    {
                        var index = _pages.IndexOf(exit);
                        _pages.RemoveAt(index);
                        _stacked.RemoveAt(index);
                        Destroy(exit.gameObject);
                    }
                }
                enter.DidEnter(enterInfo);
                return enter;
            }
            finally { EndTransition(); }
        }

        /// <summary>Back to the previous page; the current one is destroyed.</summary>
        public async Awaitable Pop(bool animated = true)
        {
            if (_pages.Count == 0) return;
            BeginTransition();
            try
            {
                var exit = Current;
                var enter = _pages.Count > 1 ? _pages[^2] : null;
                if (enter != null)
                {
                    enter.gameObject.SetActive(true);
                    enter.Group.alpha = 0f;
                }
                var enterInfo = new UIAnimTransition(UIAnimTransitionKind.Pop, exit, true, animated);
                var exitInfo = new UIAnimTransition(UIAnimTransitionKind.Pop, enter, true, animated);
                await Both(enter != null ? enter.WillEnter(enterInfo) : null, exit.WillExit(exitInfo));
                if (enter != null) enter.Group.alpha = 1f;
                await Both(enter != null ? enter.Play("PopEnter", null, UIAnimDefaultTransitions.PagePopEnter, animated) : null,
                    exit.Play("PopExit", null, UIAnimDefaultTransitions.PagePopExit, animated));

                _pages.RemoveAt(_pages.Count - 1);
                _stacked.RemoveAt(_stacked.Count - 1);
                exit.DidExit(exitInfo);
                Destroy(exit.gameObject);
                if (enter != null) enter.DidEnter(enterInfo);
            }
            finally { EndTransition(); }
        }
    }
}
