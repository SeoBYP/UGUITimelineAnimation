using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SeoBYP.TimelineAnimation.Editor
{
    /// <summary>
    /// Record mode: which track a change made in the Scene view or Inspector belongs to (the serialized field the
    /// Undo system reports → target + property), and the value it had before the change.
    /// </summary>
    internal static class UIAnimRecorder
    {
        /// <summary>
        /// The track a changed field belongs to. <paramref name="obj"/> is the changed object (a component, or a
        /// GameObject for Active). <paramref name="state"/> decides alpha: an existing Alpha track takes a color's alpha.
        /// </summary>
        public static bool TryMap(Object obj, string path, UIAnimState state, out Component target, out UIAnimProperty property)
        {
            target = null;
            property = default;
            var field = path.Split('.')[0];
            switch (obj)
            {
                case GameObject go when field == "m_IsActive":
                    (target, property) = (go.transform, UIAnimProperty.Active);
                    return true;
                case Transform t:
                    target = t;
                    switch (field)
                    {
                        case "m_LocalScale": property = UIAnimProperty.Scale; return true;
                        case "m_LocalRotation":
                        case "m_LocalEulerAnglesHint": property = UIAnimProperty.Rotation; return true;
                    }
                    if (t is not RectTransform) return false;
                    switch (field)
                    {
                        case "m_AnchoredPosition": property = UIAnimProperty.AnchoredPosition; return true;
                        case "m_SizeDelta": property = UIAnimProperty.SizeDelta; return true;
                        case "m_AnchorMin": property = UIAnimProperty.AnchorMin; return true;
                        case "m_AnchorMax": property = UIAnimProperty.AnchorMax; return true;
                        case "m_Pivot": property = UIAnimProperty.Pivot; return true;
                    }
                    return false;
                case CanvasGroup group when field == "m_Alpha":
                    (target, property) = (group, UIAnimProperty.CanvasGroupAlpha);
                    return true;
                case Image image when field == "m_FillAmount":
                    (target, property) = (image, UIAnimProperty.FillAmount);
                    return true;
                case TMP_Text text when field == "m_fontSize":
                    (target, property) = (text, UIAnimProperty.FontSize);
                    return true;
                case TMP_Text text when field == "m_characterSpacing":
                    (target, property) = (text, UIAnimProperty.CharacterSpacing);
                    return true;
                case Graphic graphic when field == (graphic is TMP_Text ? "m_fontColor" : "m_Color"):
                    target = graphic;
                    var alphaOnly = path.EndsWith(".a");
                    var hasAlpha = state.tracks.Exists(x => x.target == graphic && x.property == UIAnimProperty.GraphicAlpha);
                    var hasColor = state.tracks.Exists(x => x.target == graphic && x.property == UIAnimProperty.GraphicColor);
                    property = alphaOnly && hasAlpha && !hasColor ? UIAnimProperty.GraphicAlpha : UIAnimProperty.GraphicColor;
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The property's value before the change: the value now with each changed component put back to what the Undo
        /// system recorded as its previous value (path → previous, e.g. "m_LocalScale.x" → "1").
        /// </summary>
        public static Vector4 Previous(Vector4 now, UIAnimProperty property, IReadOnlyDictionary<string, string> previous)
        {
            var before = now;
            var quaternion = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
            foreach (var (path, text) in previous)
            {
                if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) continue;
                var dot = path.LastIndexOf('.');
                var field = dot < 0 ? path : path.Substring(0, dot);
                var i = dot < 0 ? 0 : Component(path.Substring(dot + 1));
                if (i < 0) continue;
                if (field == "m_LocalRotation") quaternion[i] = v;
                else if (property == UIAnimProperty.GraphicAlpha) before.x = v;   // a color's .a, kept as the alpha value
                else before[i] = v;
            }
            // The rotate tool reports the quaternion only: turn it back into angles when all four are known.
            if (property == UIAnimProperty.Rotation && !previous.ContainsKey("m_LocalEulerAnglesHint.x") && !float.IsNaN(quaternion.x + quaternion.y + quaternion.z + quaternion.w))
            {
                var e = new Quaternion(quaternion.x, quaternion.y, quaternion.z, quaternion.w).eulerAngles;
                before = new Vector4(Mathf.DeltaAngle(0f, e.x), Mathf.DeltaAngle(0f, e.y), Mathf.DeltaAngle(0f, e.z), 0f);
            }
            return before;
        }

        static int Component(string c) => c switch
        {
            "x" or "r" => 0,
            "y" or "g" => 1,
            "z" or "b" => 2,
            "w" or "a" => 3,
            _ => -1,
        };
    }
}
