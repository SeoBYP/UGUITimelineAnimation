using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Graphic = UnityEngine.UI.Graphic;

namespace SeoBYP.TimelineAnimation.Editor
{
    internal enum UIAnimPickKind { Property, Event, Sound }

    /// <summary>
    /// Dropdown for choosing tracks: the animated object's hierarchy on the left, what the selected object can get as cards
    /// on the right (properties with their current value, animation events on the root, sounds on objects with an AudioSource).
    /// Adding: click cards to pick several (across objects), then "Add N"; double-click adds right away.
    /// Retargeting an existing track (<c>current</c> set): one click picks.
    /// </summary>
    internal sealed class UIAnimPropertyPicker : EditorWindow
    {
        UIAnimation _root;
        HashSet<(Component, UIAnimProperty)> _used;
        (Component, UIAnimProperty)? _current;
        Action<List<(UIAnimPickKind kind, Component component, UIAnimProperty property)>> _onPick;
        readonly List<(UIAnimPickKind kind, Component component, UIAnimProperty property)> _picked = new();
        Button _addButton;

        Transform _selected;
        string _search = "";
        VisualElement _tree, _cards;
        Label _path;

        public static void Show(EditorWindow owner, Rect activatorWorldBound, UIAnimation root,
            HashSet<(Component, UIAnimProperty)> used, (Component, UIAnimProperty)? current,
            Action<List<(UIAnimPickKind kind, Component component, UIAnimProperty property)>> onPick)
        {
            var picker = CreateInstance<UIAnimPropertyPicker>();
            picker._root = root;
            picker._used = used;
            picker._current = current;
            picker._onPick = onPick;
            picker._selected = current?.Item1 != null ? current.Value.Item1.transform : root.transform;

            var screen = new Rect(owner.position.position + activatorWorldBound.position, activatorWorldBound.size);
            picker.ShowAsDropDown(screen, new Vector2(560, 420));
        }

        /// <summary>Properties an object can animate, with the component each one drives.</summary>
        public static IEnumerable<(Component component, UIAnimProperty property)> Properties(Transform tr)
        {
            yield return (tr, UIAnimProperty.Scale);
            yield return (tr, UIAnimProperty.Rotation);
            yield return (tr, UIAnimProperty.Active);
            if (tr is RectTransform)
            {
                yield return (tr, UIAnimProperty.SizeDelta);
                yield return (tr, UIAnimProperty.AnchoredPosition);
                yield return (tr, UIAnimProperty.AnchorMin);
                yield return (tr, UIAnimProperty.AnchorMax);
                yield return (tr, UIAnimProperty.Pivot);
            }
            if (tr.GetComponent<CanvasGroup>() is { } group) yield return (group, UIAnimProperty.CanvasGroupAlpha);
            if (tr.GetComponent<Camera>() is { } camera)
            {
                yield return (camera, UIAnimProperty.CameraBackground);
                yield return (camera, UIAnimProperty.CameraOrthoSize);
            }
            var graphic = tr.GetComponent<Graphic>();
            if (graphic != null)
            {
                yield return (graphic, UIAnimProperty.GraphicColor);
                yield return (graphic, UIAnimProperty.GraphicAlpha);
            }
            if (graphic is UnityEngine.UI.Image { type: UnityEngine.UI.Image.Type.Filled } image) yield return (image, UIAnimProperty.FillAmount);
            var text = tr.GetComponent<TMP_Text>();
            if (text != null)
            {
                yield return (text, UIAnimProperty.FontSize);
                yield return (text, UIAnimProperty.CharacterSpacing);
                yield return (text, UIAnimProperty.TextReveal);
                yield return (text, UIAnimProperty.CharScale);
                yield return (text, UIAnimProperty.CharOffset);
                yield return (text, UIAnimProperty.CharRotation);
                yield return (text, UIAnimProperty.CharColor);
                yield return (text, UIAnimProperty.CharAlpha);
            }
        }

        void CreateGUI()
        {
            var root = rootVisualElement;
            root.AddToClassList("uia-root");
            root.AddToClassList("uia-picker");
            if (!EditorGUIUtility.isProSkin) root.AddToClassList("uia-light");
            var dir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)));
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(Path.Combine(dir ?? "", "UIAnimationEditor.uss").Replace('\\', '/'));
            if (sheet != null) root.styleSheets.Add(sheet);
            if (_root == null) { Close(); return; }   // lost after a domain reload

            var header = new VisualElement();
            header.AddToClassList("uia-picker-header");
            header.Add(new Label(_current.HasValue ? "Change Track Target" : "Add Tracks (multi-select)") { name = "title" });
            var search = new TextField { value = _search };
            search.AddToClassList("uia-picker-search");
            search.RegisterValueChangedCallback(e => { _search = e.newValue; Refresh(); });
            header.Add(search);
            root.Add(header);

            var body = new VisualElement();
            body.AddToClassList("uia-picker-body");
            _tree = new ScrollView();
            _tree.AddToClassList("uia-picker-tree");
            body.Add(_tree);

            var right = new VisualElement { style = { flexGrow = 1 } };
            _path = new Label();
            _path.AddToClassList("uia-picker-path");
            right.Add(_path);
            _cards = new ScrollView();
            _cards.AddToClassList("uia-picker-cards");
            right.Add(_cards);
            body.Add(right);
            root.Add(body);

            if (!_current.HasValue)
            {
                var footer = new VisualElement();
                footer.AddToClassList("uia-picker-footer");
                footer.Add(new Label("Click: select · Double-click: add now") { style = { flexGrow = 1 } });
                footer.Add(new Button(Close) { text = "Cancel" });
                _addButton = new Button(Commit);
                _addButton.AddToClassList("uia-picker-add");
                footer.Add(_addButton);
                root.Add(footer);
                UpdateAddButton();
            }

            Refresh();
            search.schedule.Execute(() => search.Focus());
        }

        void Refresh()
        {
            _tree.Clear();
            var all = _root.GetComponentsInChildren<Transform>(true);
            var matches = all.Where(Matches).ToList();
            if (!matches.Contains(_selected) && matches.Count > 0) _selected = matches[0];

            foreach (var tr in all)
            {
                if (!Matches(tr)) continue;
                var depth = Depth(tr);
                var row = new VisualElement { style = { paddingLeft = 8 + depth * 14 } };
                row.AddToClassList("uia-picker-row");
                row.EnableInClassList("uia-picker-row--selected", tr == _selected);
                if (!tr.gameObject.activeInHierarchy) row.AddToClassList("uia-picker-row--inactive");

                var icon = new Image { image = IconFor(tr), scaleMode = ScaleMode.ScaleToFit };
                icon.AddToClassList("uia-picker-icon");
                row.Add(icon);
                row.Add(new Label(tr.name) { style = { flexGrow = 1 } });
                var count = Properties(tr).Count(p => !_used.Contains(p)) + Specials(tr).Count();
                var badge = new Label(count.ToString());
                badge.AddToClassList("uia-picker-badge");
                row.Add(badge);

                var target = tr;
                row.RegisterCallback<PointerDownEvent>(_ => { _selected = target; Refresh(); });
                _tree.Add(row);
            }
            RefreshCards();
        }

        void RefreshCards()
        {
            _cards.Clear();
            if (_selected == null) { _path.text = ""; return; }
            _path.text = _selected == _root.transform ? _root.name : $"{_root.name}/{AnimationUtility.CalculateTransformPath(_selected, _root.transform)}";

            foreach (var (component, property) in Properties(_selected))
            {
                if (!MatchesProperty(property)) continue;
                var isCurrent = _current.HasValue && _current.Value.Item1 == component && _current.Value.Item2 == property;
                var taken = _used.Contains((component, property)) && !isCurrent;

                var card = new VisualElement();
                card.AddToClassList("uia-picker-card");
                card.EnableInClassList("uia-picker-card--taken", taken);
                card.EnableInClassList("uia-picker-card--current", isCurrent);

                var stripe = new VisualElement { style = { backgroundColor = UIAnimTimelineView.PropertyColor(property) } };
                stripe.AddToClassList("uia-picker-card-stripe");
                card.Add(stripe);

                var icon = new Image { image = EditorGUIUtility.ObjectContent(component, component.GetType()).image, scaleMode = ScaleMode.ScaleToFit };
                icon.AddToClassList("uia-picker-icon");
                card.Add(icon);

                var text = new VisualElement { style = { flexGrow = 1 } };
                text.Add(new Label(UIAnimTimelineView.PropertyName(property)) { name = "prop" });
                text.Add(new Label(component.GetType().Name) { name = "type" });
                card.Add(text);
                card.Add(ValuePreview(component, property));

                if (taken)
                {
                    var tag = new Label("Added");
                    tag.AddToClassList("uia-picker-tag");
                    card.Add(tag);
                }
                else Pickable(card, (UIAnimPickKind.Property, component, property));
                _cards.Add(card);
            }

            if (_current.HasValue) return;
            foreach (var (kind, component) in Specials(_selected))
            {
                var card = new VisualElement();
                card.AddToClassList("uia-picker-card");
                var stripe = new VisualElement { style = { backgroundColor = kind == UIAnimPickKind.Event ? UIAnimTimelineView.EventColor : UIAnimTimelineView.SoundColor } };
                stripe.AddToClassList("uia-picker-card-stripe");
                card.Add(stripe);
                var text = new VisualElement { style = { flexGrow = 1 } };
                text.Add(new Label(kind == UIAnimPickKind.Event ? "Animation Events" : "Sound") { name = "prop" });
                text.Add(new Label(kind == UIAnimPickKind.Event ? "Named points in time · [UIAnimEvent] data"
                    : component is AudioSource ? $"AudioSource ({component.name})" : "2D, no AudioSource needed") { name = "type" });
                card.Add(text);
                Pickable(card, (kind, component, default));
                _cards.Add(card);
            }
        }

        // Event tracks on the root; sound tracks on the root (UIAnimation's source) and on every object with its own AudioSource.
        IEnumerable<(UIAnimPickKind kind, Component component)> Specials(Transform tr)
        {
            if (tr == _root.transform)
            {
                yield return (UIAnimPickKind.Event, _root);
                yield return (UIAnimPickKind.Sound, _root);
            }
            var source = tr.GetComponent<AudioSource>();
            if (source != null && source != _root.audioSource) yield return (UIAnimPickKind.Sound, source);
        }

        void Pickable(VisualElement card, (UIAnimPickKind, Component, UIAnimProperty) pick)
        {
            card.EnableInClassList("uia-picker-card--picked", _picked.Contains(pick));
            card.RegisterCallback<PointerDownEvent>(e =>
            {
                if (_current.HasValue || e.clickCount == 2)
                {
                    if (!_picked.Contains(pick)) _picked.Add(pick);
                    Commit();
                    return;
                }
                if (!_picked.Remove(pick)) _picked.Add(pick);
                card.EnableInClassList("uia-picker-card--picked", _picked.Contains(pick));
                UpdateAddButton();
            });
        }

        void UpdateAddButton()
        {
            if (_addButton == null) return;
            _addButton.text = _picked.Count > 0 ? $"Add {_picked.Count}" : "Add";
            _addButton.SetEnabled(_picked.Count > 0);
        }

        void Commit()
        {
            if (_picked.Count == 0) return;
            var picks = new List<(UIAnimPickKind, Component, UIAnimProperty)>(_picked);
            Close();
            _onPick?.Invoke(picks);
        }

        static VisualElement ValuePreview(Component component, UIAnimProperty property)
        {
            var v = UIAnimation.Read(component, property);
            if (property == UIAnimProperty.GraphicColor)
            {
                var swatch = new VisualElement { style = { backgroundColor = (Color)v } };
                swatch.AddToClassList("uia-picker-swatch");
                return swatch;
            }
            var text = property switch
            {
                UIAnimProperty.Scale => Mathf.Approximately(v.x, v.y) && Mathf.Approximately(v.y, v.z) ? $"{v.x:0.##}" : $"{v.x:0.##}, {v.y:0.##}, {v.z:0.##}",
                UIAnimProperty.Rotation => $"{v.x:0.#}, {v.y:0.#}, {v.z:0.#}",
                UIAnimProperty.SizeDelta => $"{v.x:0.#} × {v.y:0.#}",
                UIAnimProperty.AnchoredPosition => $"{v.x:0.#}, {v.y:0.#}",
                _ => $"{v.x:0.##}",
            };
            var label = new Label(text);
            label.AddToClassList("uia-picker-value");
            return label;
        }

        bool Matches(Transform tr)
        {
            if (string.IsNullOrWhiteSpace(_search)) return true;
            return tr.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
                   || Properties(tr).Any(p => MatchesProperty(p.property));
        }

        bool MatchesProperty(UIAnimProperty property) =>
            string.IsNullOrWhiteSpace(_search)
            || UIAnimTimelineView.PropertyName(property).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
            || (_selected != null && _selected.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0);

        int Depth(Transform tr)
        {
            var d = 0;
            for (var t = tr; t != _root.transform && t != null; t = t.parent) d++;
            return d;
        }

        static Texture IconFor(Transform tr)
        {
            var graphic = tr.GetComponent<Graphic>();
            Component main = graphic != null ? graphic : tr;
            return EditorGUIUtility.ObjectContent(main, main.GetType()).image;
        }
    }
}
