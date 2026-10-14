using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeoBYP.TimelineAnimation.Editor
{
    /// <summary>
    /// Context menu drawn inside the editor window with its stylesheet (Unity's native menu can't be themed).
    /// Items are added by path: "Add Child Call/Window › Button Close/Hover" makes nested submenus.
    /// </summary>
    internal sealed class UIAnimMenu
    {
        sealed class Item
        {
            public string label;
            public Action action;
            public bool enabled = true, isChecked, danger, separator;
            public readonly List<Item> children = new();
        }

        readonly Item _root = new();

        public bool IsEmpty => _root.children.Count == 0;

        public void Add(string path, Action action, bool enabled = true, bool isChecked = false, bool danger = false)
        {
            var parent = _root;
            var parts = path.Split('/');
            for (var i = 0; i < parts.Length - 1; i++)
            {
                var sub = parent.children.Find(c => c.label == parts[i] && c.action == null && !c.separator);
                if (sub == null) parent.children.Add(sub = new Item { label = parts[i] });
                parent = sub;
            }
            parent.children.Add(new Item { label = parts[^1], action = action, enabled = enabled && action != null, isChecked = isChecked, danger = danger });
        }

        public void Separator()
        {
            if (_root.children.Count > 0 && !_root.children[^1].separator) _root.children.Add(new Item { separator = true });
        }

        /// <summary>Opens at a world (panel) position, on the nearest element carrying the editor stylesheet.</summary>
        public void Show(VisualElement anchor, Vector2 worldPosition)
        {
            if (IsEmpty) return;
            var host = anchor;
            while (host.parent != null && !host.ClassListContains("uia-root")) host = host.parent;

            var blocker = new VisualElement { focusable = true, style = { position = Position.Absolute, left = 0, top = 0, right = 0, bottom = 0 } };
            blocker.RegisterCallback<PointerDownEvent>(e => { if (e.target == blocker) blocker.RemoveFromHierarchy(); });
            blocker.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Escape) blocker.RemoveFromHierarchy(); });
            host.Add(blocker);
            Open(blocker, _root, host.WorldToLocal(worldPosition), null, new List<VisualElement>());
            blocker.Focus();
        }

        // levels[i] = the open panel at depth i; opening a submenu closes everything deeper.
        static void Open(VisualElement blocker, Item item, Vector2 position, VisualElement opener, List<VisualElement> levels)
        {
            var depth = opener == null ? 0 : (int)opener.parent.userData + 1;
            while (levels.Count > depth) { levels[^1].RemoveFromHierarchy(); levels.RemoveAt(levels.Count - 1); }

            var panel = new VisualElement { userData = depth, style = { left = position.x, top = position.y } };
            panel.AddToClassList("uia-menu");
            foreach (var child in item.children)
            {
                if (child.separator)
                {
                    var line = new VisualElement();
                    line.AddToClassList("uia-menu-separator");
                    panel.Add(line);
                    continue;
                }
                var row = new VisualElement();
                row.AddToClassList("uia-menu-item");
                row.EnableInClassList("uia-menu-item--disabled", !child.enabled && child.children.Count == 0);
                row.EnableInClassList("uia-menu-item--danger", child.danger);
                row.Add(new Label(child.isChecked ? "✓" : "") { name = "check" });
                row.Add(new Label(child.label) { name = "label" });
                row.Add(new Label(child.children.Count > 0 ? "›" : "") { name = "arrow" });

                var c = child;
                row.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    foreach (var sibling in panel.Children()) sibling.RemoveFromClassList("uia-menu-item--open");
                    if (c.children.Count > 0)
                    {
                        row.AddToClassList("uia-menu-item--open");
                        var at = blocker.WorldToLocal(new Vector2(row.worldBound.xMax - 2, row.worldBound.y - 5));
                        Open(blocker, c, at, row, levels);
                    }
                    else while (levels.Count > depth + 1) { levels[^1].RemoveFromHierarchy(); levels.RemoveAt(levels.Count - 1); }
                });
                row.RegisterCallback<PointerUpEvent>(e =>
                {
                    if (c.children.Count > 0 || !c.enabled) return;
                    blocker.RemoveFromHierarchy();
                    c.action();
                    e.StopPropagation();
                });
                panel.Add(row);
            }

            // Keep it inside the window: flip a submenu to the opener's left, push a root menu back in.
            panel.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var size = panel.layout.size;
                var bounds = blocker.layout.size;
                var x = position.x;
                if (x + size.x > bounds.x)
                    x = opener != null ? blocker.WorldToLocal(opener.worldBound.position).x - size.x + 2 : bounds.x - size.x;
                panel.style.left = Mathf.Max(0f, x);
                panel.style.top = Mathf.Max(0f, Mathf.Min(position.y, bounds.y - size.y));
            });
            blocker.Add(panel);
            levels.Add(panel);
        }
    }
}
