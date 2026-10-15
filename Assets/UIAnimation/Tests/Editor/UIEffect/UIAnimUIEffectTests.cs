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

        // Renders a green card through the editor preview (UIAnimPreview.Seek) and compares its mean saturation.
        [Test]
        public void 미리보기에서_시간을_옮기면_UIEffect가_화면에_그려진다()
        {
            // The first render in a fresh batchmode editor logs an unrelated URP asset error (RuntimeDebugWindow.uxml).
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var cam = new GameObject("Cam").AddComponent<Camera>();
            _destroy.Add(cam.gameObject);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            var rt = new RenderTexture(64, 64, 24);
            cam.targetTexture = rt;
            var canvas = new GameObject("Canvas", typeof(Canvas)).GetComponent<Canvas>();
            _destroy.Add(canvas.gameObject);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            var effect = Effect();
            effect.transform.SetParent(canvas.transform, false);
            ((RectTransform)effect.transform).sizeDelta = new Vector2(2000, 2000);
            effect.GetComponent<Image>().color = new Color(0.2f, 0.8f, 0.3f);
            effect.toneFilter = ToneFilter.Grayscale;

            effect.gameObject.SetActive(false);
            var anim = effect.gameObject.AddComponent<UIAnimation>();
            var state = new UIAnimState { name = "Unlock" };
            state.tracks.Add(Track(effect, UIAnimProperty.UIEffectToneIntensity, new Vector4(1, 0), Vector4.zero));
            anim.states.Add(state);
            effect.gameObject.SetActive(true);

            var read = new Texture2D(64, 64);
            float Saturation()
            {
                Canvas.ForceUpdateCanvases();
                cam.Render();
                RenderTexture.active = rt;
                read.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                RenderTexture.active = null;
                var sum = 0f;
                foreach (var c in read.GetPixels())
                {
                    Color.RGBToHSV(c, out _, out var sat, out _);
                    sum += sat;
                }
                return sum / (64 * 64);
            }

            using var preview = new UIAnimPreview();
            preview.Begin(anim, state);
            preview.Seek(0f);
            var grey = Saturation();
            preview.Seek(1f);
            var colored = Saturation();
            preview.Stop();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(read);
            Assert.Less(grey, 0.05f, "0초: Tone 1 = 회색");
            Assert.Greater(colored, 0.4f, "1초: Tone 0 = 원래 초록");
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
