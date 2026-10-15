using System.Collections.Generic;
using System.Linq;
using Coffee.UIEffects;
using NUnit.Framework;
using SeoBYP.TimelineAnimation.Editor;
using UnityEngine;
using UnityEngine.UI;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>UIEffect values as keyframe tracks (compiled only when UIEffect is installed).</summary>
    public sealed class UIAnimUIEffectTests
    {
        readonly List<Object> _destroy = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _destroy) Object.DestroyImmediate(o);
            _destroy.Clear();
        }

        UIEffect Effect()
        {
            var go = new GameObject("Effect", typeof(RectTransform), typeof(Image));
            _destroy.Add(go);
            return go.AddComponent<UIEffect>();
        }

        static UIAnimTrack Track(Component target, UIAnimProperty property, Vector4 from, Vector4 to) => new()
        {
            target = target, property = property, start = UIAnimStart.KeyValue,
            keys = { new UIAnimKey { time = 0, value = from, ease = UIAnimEase.Linear }, new UIAnimKey { time = 1, value = to, ease = UIAnimEase.Linear } },
        };

        [Test]
        public void UIEffect_값을_키프레임_트랙으로_움직인다()
        {
            var effect = Effect();
            var state = new UIAnimState { name = "Dissolve" };
            state.tracks.Add(Track(effect, UIAnimProperty.UIEffectTransitionRate, Vector4.zero, new Vector4(1, 0)));
            state.tracks.Add(Track(effect, UIAnimProperty.UIEffectToneIntensity, new Vector4(1, 0), Vector4.zero));
            state.tracks.Add(Track(effect, UIAnimProperty.UIEffectEdgeColor, Color.white, Color.red));
            UIAnimation.Resolve(state);
            UIAnimation.Evaluate(state, 0.25f);
            Assert.AreEqual(0.25f, effect.transitionRate, 1e-4f);
            Assert.AreEqual(0.75f, effect.toneIntensity, 1e-4f);
            Assert.AreEqual(Color.Lerp(Color.white, Color.red, 0.25f), effect.edgeColor);
            Assert.AreEqual(0.25f, UIAnimation.Read(effect, UIAnimProperty.UIEffectTransitionRate).x, 1e-4f, "읽기도 됨 (Current 시작값)");
        }

        [Test]
        public void UIEffect가_있는_오브젝트는_트랙_고르기에_14가지가_나온다()
        {
            var effect = Effect();
            var offered = UIAnimPropertyPicker.Properties(effect.transform).Where(p => p.component == effect).Select(p => p.property).ToList();
            Assert.AreEqual(14, offered.Count, string.Join(", ", offered));
            Assert.Contains(UIAnimProperty.UIEffectGradationRotation, offered);
            Assert.AreEqual("UIEffect · Transition Rate", UIAnimTimelineView.PropertyName(UIAnimProperty.UIEffectTransitionRate));
        }
    }
}
