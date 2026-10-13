using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SeoBYP.TimelineAnimation
{
    public enum UIAnimEvent
    {
        OnEnable,
        PointerEnter,
        PointerExit,
        PointerDown,
        PointerUp,
        Click,
        Select,
        Deselect,
        ToggleOn,       // Toggle.isOn became true
        ToggleOff,      // Toggle.isOn became false
    }

    [Serializable]
    public sealed class UIAnimBinding
    {
        public UIAnimEvent evt;
        public string state;
        public UIAnimInterrupt interrupt;
    }

    /// <summary>Plays UIAnimation states when UI events happen on this object.</summary>
    [RequireComponent(typeof(UIAnimation))]
    public sealed class UIAnimTrigger : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        IPointerClickHandler, ISelectHandler, IDeselectHandler
    {
        public List<UIAnimBinding> bindings = new();

        UIAnimation _animation;
        Selectable _selectable;
        Toggle _toggle;

        void Awake()
        {
            _animation = GetComponent<UIAnimation>();
            _selectable = GetComponent<Selectable>();
            _toggle = GetComponent<Toggle>();
        }

        void OnEnable()
        {
            if (_toggle != null) _toggle.onValueChanged.AddListener(OnToggle);
            Fire(UIAnimEvent.OnEnable, null);
        }

        void OnDisable()
        {
            if (_toggle != null) _toggle.onValueChanged.RemoveListener(OnToggle);
        }

        void OnToggle(bool isOn) => Fire(isOn ? UIAnimEvent.ToggleOn : UIAnimEvent.ToggleOff, null);
        public void OnPointerEnter(PointerEventData e) => Fire(UIAnimEvent.PointerEnter, e);
        public void OnPointerExit(PointerEventData e) => Fire(UIAnimEvent.PointerExit, e);
        public void OnPointerDown(PointerEventData e) => Fire(UIAnimEvent.PointerDown, e);
        public void OnPointerUp(PointerEventData e) => Fire(UIAnimEvent.PointerUp, e);
        public void OnPointerClick(PointerEventData e) => Fire(UIAnimEvent.Click, e);
        public void OnSelect(BaseEventData e) => Fire(UIAnimEvent.Select, e as PointerEventData);
        public void OnDeselect(BaseEventData e) => Fire(UIAnimEvent.Deselect, e as PointerEventData);

        internal void Fire(UIAnimEvent evt, PointerEventData pointer)
        {
            // A non-interactable Selectable ignores input; OnEnable still plays.
            if (evt != UIAnimEvent.OnEnable && _selectable != null && !_selectable.IsInteractable()) return;

            foreach (var b in bindings)
                if (b.evt == evt) _animation.Play(b.state, b.interrupt, pointer);
        }
    }
}
