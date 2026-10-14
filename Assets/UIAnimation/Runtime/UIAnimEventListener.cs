using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SeoBYP.TimelineAnimation
{
    /// <summary>
    /// Connects animation events to UnityEvents in the Inspector, so designers can react without code
    /// (e.g. "Shown" → AudioSource.PlayOneShot). Custom [UIAnimEvent] struct data is for code: use UIAnimation.AddListener&lt;T&gt;.
    /// </summary>
    [AddComponentMenu("UI Animation/UI Anim Event Listener")]
    public sealed class UIAnimEventListener : MonoBehaviour
    {
        [Serializable]
        public sealed class Binding
        {
            public string eventName = "";
            public UnityEvent onRaised = new();
        }

        [Tooltip("The UIAnimation whose events are listened to. Empty = the one on this GameObject.")]
        public UIAnimation source;
        public List<Binding> bindings = new();

        UIAnimation _subscribed;

        internal void OnEnable()
        {
            if (source == null) source = GetComponent<UIAnimation>();
            _subscribed = source;
            if (_subscribed != null) _subscribed.EventRaised += OnEvent;
        }

        internal void OnDisable()
        {
            if (_subscribed != null) _subscribed.EventRaised -= OnEvent;
            _subscribed = null;
        }

        void OnEvent(in UIAnimEventInfo e)
        {
            foreach (var binding in bindings)
                if (binding.eventName == e.Name) binding.onRaised.Invoke();
        }
    }
}
