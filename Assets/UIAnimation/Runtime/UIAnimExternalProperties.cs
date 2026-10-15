using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeoBYP.TimelineAnimation
{
    /// <summary>
    /// Track properties of components from optional packages (UIEffect …). The core lists them in UIAnimProperty but does
    /// not reference those packages: each package's integration assembly (compiled only when the package is installed)
    /// registers how to read and write its properties. Unregistered ones (package not installed) are skipped.
    /// </summary>
    public static class UIAnimExternalProperties
    {
        public sealed class Entry
        {
            public UIAnimProperty property;
            /// <summary>The component it animates (an object without one does not offer it).</summary>
            public Type componentType;
            /// <summary>Shown in the editor, e.g. "UIEffect · Tone Intensity".</summary>
            public string name;
            /// <summary>The serialized field, for edit-mode preview (AnimationMode restores it afterwards).</summary>
            public string serializedField;
            public bool isColor;
            /// <summary>Slider range of a number (min == max: a plain number field).</summary>
            public float min, max;
            public Color trackColor = new Color32(171, 71, 188, 255);
            public Func<Component, Vector4> read;
            public Action<Component, Vector4> write;
        }

        static readonly Dictionary<UIAnimProperty, Entry> Entries = new();

        public static void Register(Entry entry) => Entries[entry.property] = entry;
        public static bool TryGet(UIAnimProperty property, out Entry entry) => Entries.TryGetValue(property, out entry);
        public static IEnumerable<Entry> All => Entries.Values;

        public static bool IsExternal(UIAnimProperty property) => property >= UIAnimProperty.UIEffectToneIntensity;
    }
}
