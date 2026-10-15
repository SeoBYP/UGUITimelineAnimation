using System;
using Coffee.UIEffects;
using UnityEngine;

namespace SeoBYP.TimelineAnimation
{
    /// <summary>
    /// UIEffect (com.coffee.ui-effect) values as UI Animation tracks: the same set UIEffect offers as Timeline tracks
    /// (intensities, colors, transition / shiny rate, gradation), keyed and eased like any other track and previewed in the
    /// editor. This assembly compiles only when UIEffect is installed (asmdef version define UIANIM_UIEFFECT).
    /// </summary>
    static class UIAnimUIEffectProperties
    {
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            Number(UIAnimProperty.UIEffectToneIntensity, "Tone Intensity", "m_ToneIntensity", 0, 1, e => e.toneIntensity, (e, v) => e.toneIntensity = v);
            Number(UIAnimProperty.UIEffectColorIntensity, "Color Intensity", "m_ColorIntensity", 0, 1, e => e.colorIntensity, (e, v) => e.colorIntensity = v);
            Colour(UIAnimProperty.UIEffectColor, "Color", "m_Color", e => e.color, (e, c) => e.color = c);
            Number(UIAnimProperty.UIEffectSamplingIntensity, "Sampling Intensity", "m_SamplingIntensity", 0, 1, e => e.samplingIntensity, (e, v) => e.samplingIntensity = v);
            Number(UIAnimProperty.UIEffectTransitionRate, "Transition Rate", "m_TransitionRate", 0, 1, e => e.transitionRate, (e, v) => e.transitionRate = v);
            Colour(UIAnimProperty.UIEffectTransitionColor, "Transition Color", "m_TransitionColor", e => e.transitionColor, (e, c) => e.transitionColor = c);
            Number(UIAnimProperty.UIEffectEdgeShinyRate, "Edge Shiny Rate", "m_EdgeShinyRate", 0, 1, e => e.edgeShinyRate, (e, v) => e.edgeShinyRate = v);
            Colour(UIAnimProperty.UIEffectEdgeColor, "Edge Color", "m_EdgeColor", e => e.edgeColor, (e, c) => e.edgeColor = c);
            Number(UIAnimProperty.UIEffectGradationIntensity, "Gradation Intensity", "m_GradationIntensity", 0, 1, e => e.gradationIntensity, (e, v) => e.gradationIntensity = v);
            Number(UIAnimProperty.UIEffectGradationOffset, "Gradation Offset", "m_GradationOffset", -1, 1, e => e.gradationOffset, (e, v) => e.gradationOffset = v);
            Number(UIAnimProperty.UIEffectGradationRotation, "Gradation Rotation", "m_GradationRotation", 0, 360, e => e.gradationRotation, (e, v) => e.gradationRotation = v);
            Number(UIAnimProperty.UIEffectGradationScale, "Gradation Scale", "m_GradationScale", 0.01f, 10, e => e.gradationScale, (e, v) => e.gradationScale = v);
            Number(UIAnimProperty.UIEffectDetailIntensity, "Detail Intensity", "m_DetailIntensity", 0, 1, e => e.detailIntensity, (e, v) => e.detailIntensity = v);
            Colour(UIAnimProperty.UIEffectDetailColor, "Detail Color", "m_DetailColor", e => e.detailColor, (e, c) => e.detailColor = c);
        }

        static void Number(UIAnimProperty property, string name, string field, float min, float max, Func<UIEffect, float> get, Action<UIEffect, float> set) =>
            UIAnimExternalProperties.Register(new UIAnimExternalProperties.Entry
            {
                property = property, componentType = typeof(UIEffect), name = "UIEffect · " + name, serializedField = field, min = min, max = max,
                read = c => new Vector4(get((UIEffect)c), 0, 0, 0),
                write = (c, v) => set((UIEffect)c, v.x),
            });

        static void Colour(UIAnimProperty property, string name, string field, Func<UIEffect, Color> get, Action<UIEffect, Color> set) =>
            UIAnimExternalProperties.Register(new UIAnimExternalProperties.Entry
            {
                property = property, componentType = typeof(UIEffect), name = "UIEffect · " + name, serializedField = field, isColor = true,
                read = c => get((UIEffect)c),
                write = (c, v) => set((UIEffect)c, v),
            });
    }
}
