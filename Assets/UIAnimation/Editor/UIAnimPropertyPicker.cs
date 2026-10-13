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
    /// <summary>
    /// Dropdown for choosing what a track animates: the animated object's hierarchy on the left,
    /// the properties of the selected object as cards on the right (with their current value).
    /// </summary>
    internal sealed class UIAnimPropertyPicker : EditorWindow
    {
        UIAnimation _root;
        HashSet<(Component, UIAnimProperty)> _used;
        (Component, UIAnimProperty)? _current;
        Action<Component, UIAnimProperty> _onPick;

        Transform _selected;
        string _search = "";
        VisualElement _tree, _cards;
        Label _path;

        public static void Show(EditorWindow owner, Rect activatorWorldBound, UIAnimation root,
            HashSet<(Component, UIAnimProperty)> used, (Component, UIAnimProperty)? current, Action<Component, UIAnimProperty> onPick)
        {
            var picker = CreateInstance<UIAnimPropertyPicker>();
            picker._root = root;
            picker._used = used;
            picker._current = current;
            picker._onPick = onPick;
            picker._selected = current?.Item1 != null ? current.Value.Item1.transform : root.transform;

            var screen = new Rect(owner.position.position + activatorWorldBound.position, activatorWorldBound.size);
            picker.ShowAsDropDown(screen, new Vector2(560, 380));
        }

        /// <summary>Properties an object can animate, with the component each one drives.</summary>
        public static IEnumerable<(Component component, UIAnimProperty property)> Properties(Transform tr)
        {
            yield return (tr, UIAnimProperty.Scale);
            if (tr is RectTransform)
            {
                yield return (tr, UIAnimProperty.SizeDelta);
                yield return (tr, UIAnimProperty.AnchoredPosition);
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
            header.Add(new Label("애니메이션할 속성 선택") { name = "title" });
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
                var count = Properties(tr).Count(p => !_used.Contains(p));
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
                    var tag = new Label("추가됨");
                    tag.AddToClassList("uia-picker-tag");
                    card.Add(tag);
                }
                else
                {
                    card.RegisterCallback<PointerDownEvent>(_ =>
                    {
                        _onPick?.Invoke(component, property);
                        Close();
                    });
                }
                _cards.Add(card);
            }
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
