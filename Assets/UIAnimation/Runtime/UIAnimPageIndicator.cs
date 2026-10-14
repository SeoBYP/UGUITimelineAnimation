using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SeoBYP.TimelineAnimation
{
    /// <summary>
    /// Dots or tabs for a scroll list's items (pages, banners, tab pages): one item per list item, laid out by this
    /// object's layout group. The current one plays the On state and the one it replaces plays Off. Clicking one scrolls there.
    /// </summary>
    [AddComponentMenu("UI Animation/UI Anim Page Indicator")]
    public sealed class UIAnimPageIndicator : MonoBehaviour
    {
        public UIAnimScrollList list;
        [Tooltip("A dot or a tab, with a UIAnimation holding the On and Off states. A Button on it scrolls the list there.")]
        public RectTransform itemPrefab;
        public string onState = "On";
        public string offState = "Off";
        [Tooltip("Tab labels: set on the first text of each item. Empty = left as in the prefab.")]
        public List<string> labels = new();

        readonly List<RectTransform> _items = new();
        int _current = -1;

        void OnEnable()
        {
            if (list == null) return;
            list.CurrentIndexChanged += Select;
            if (list.Count > 0) Select(list.CurrentIndex);
        }

        void OnDisable()
        {
            if (list != null) list.CurrentIndexChanged -= Select;
        }

        void Select(int index)
        {
            if (_items.Count != list.Count) Build();
            if (index == _current || index < 0 || index >= _items.Count) return;
            if (_current >= 0 && _current < _items.Count) Anim(_current)?.Play(offState);
            Anim(index)?.Play(onState);
            _current = index;
        }

        UIAnimation Anim(int i) => _items[i].GetComponent<UIAnimation>();

        void Build()
        {
            if (itemPrefab == null) return;
            while (_items.Count < list.Count)
            {
                var i = _items.Count;
                var item = Instantiate(itemPrefab, transform, false);
                item.name = $"{itemPrefab.name} {i + 1}";
                var button = item.GetComponent<Button>();
                if (button != null) button.onClick.AddListener(() => list.ScrollTo(i, list.snapAlignment, list.snapDuration));
                if (i < labels.Count && item.GetComponentInChildren<TMP_Text>() is { } text) text.text = labels[i];
                _items.Add(item);
            }
            while (_items.Count > list.Count)
            {
                Destroy(_items[^1].gameObject);
                _items.RemoveAt(_items.Count - 1);
            }
            for (var i = 0; i < _items.Count; i++) Anim(i)?.Sample(offState, float.MaxValue);   // rest pose: Off
            _current = -1;
        }
    }
}
