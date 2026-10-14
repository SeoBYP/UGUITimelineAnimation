using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SeoBYP.TimelineAnimation.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>
    /// Scroll animations made from nothing the way a user does: the list's (or indicator's) state row → Create opens the
    /// UI Animation Editor on a new state, tracks come from the picker and keys are typed into the inspector. Then the list plays them.
    /// </summary>
    public sealed class ScrollEditorWorkflowTests
    {
        UIAnimationEditorWindow _window;
        readonly List<Object> _destroy = new();

        [SetUp]
        public void SetUp()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("에디터 창 테스트: 그래픽 장치가 필요해서 건너뜀 (-nographics)");
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null) _window.Close();
            foreach (var o in _destroy) Object.DestroyImmediate(o);
            _destroy.Clear();
        }

        VisualElement FieldByLabel(string label) =>
            _window.Inspector.Query<Label>(className: "unity-base-field__label").ToList().FirstOrDefault(l => l.text == label)?.parent
            ?? throw new AssertionException($"인스펙터에 '{label}' 칸이 없음. 있는 칸: " +
                                            string.Join(", ", _window.Inspector.Query<Label>(className: "unity-base-field__label").ToList().Select(l => l.text)));

        void Set<T>(string label, T value) => ((INotifyValueChanged<T>)FieldByLabel(label)).value = value;

        GameObject Go(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            else _destroy.Add(go);
            ((RectTransform)go.transform).sizeDelta = size;
            return go;
        }

        // Create in a state row = UIAnimStateField.Edit: adds UIAnimation if needed, the state, and opens the editor on it.
        UIAnimState Create(GameObject owner, string state)
        {
            var anim = UIAnimStateField.Edit(owner.transform, state);
            _window = EditorWindow.GetWindow<UIAnimationEditorWindow>();
            return anim.states.Single(s => s.name == state);
        }

        // Keys 0 / 0.5 / 1 = start edge / center / end edge of the viewport.
        void EdgeCenterEdge<T>(UIAnimTrack track, T edgeStart, T center, T edgeEnd)
        {
            _window.Timeline.TrackSelected(track);
            Set("Start", UIAnimStart.KeyValue);
            _window.Timeline.KeyClicked(track.keys[1], false);
            Set("Time (s)", 1f);
            Set("Value", edgeEnd);
            _window.Timeline.AddKeyRequested(track, 0.5f);   // double-click in the middle
            Set("Value", center);
            _window.Timeline.KeyClicked(track.keys[0], false);
            Set("Value", edgeStart);
        }

        [Test]
        public void 휠_피커_줄을_에디터로_처음부터_만들면_가운데만_똑바로_선다()
        {
            // The row: Wheel Row > Label (an Image stands in for the text).
            var row = Go("Wheel Row", null, new Vector2(400, 80));
            row.SetActive(false);   // a template, like a prefab
            var label = Go("Label", row.transform, new Vector2(400, 80));
            var graphic = label.AddComponent<Image>();

            var wheel = Create(row, "Wheel");
            Assert.AreEqual(1, row.GetComponent<UIAnimation>().states.Count, "Create가 상태를 추가");
            _window.AddTracks(new List<(UIAnimPickKind, Component, UIAnimProperty)>
            {
                (UIAnimPickKind.Property, label.transform, UIAnimProperty.Rotation),
                (UIAnimPickKind.Property, graphic, UIAnimProperty.GraphicAlpha),
            });
            var rotation = wheel.tracks.Single(t => t.property == UIAnimProperty.Rotation);
            var alpha = wheel.tracks.Single(t => t.property == UIAnimProperty.GraphicAlpha);
            EdgeCenterEdge(rotation, new Vector3(70, 0, 0), Vector3.zero, new Vector3(-70, 0, 0));
            EdgeCenterEdge(alpha, 0.2f, 1f, 0.2f);
            Assert.AreEqual(new[] { 0f, 0.5f, 1f }, rotation.keys.Select(k => k.time).ToArray());

            // The list: 400×400 viewport, rows can reach the center, Scrub State = Wheel.
            var list = Go("Wheel", null, new Vector2(400, 400));
            var viewport = Go("Viewport", list.transform, new Vector2(400, 400));
            var content = Go("Content", viewport.transform, Vector2.zero);
            var scroll = list.AddComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = (RectTransform)content.transform;
            var picker = list.AddComponent<UIAnimScrollList>();
            picker.itemPrefab = (RectTransform)row.transform;
            picker.padding = new RectOffset(0, 0, 160, 160);
            picker.scrubState = "Wheel";
            picker.playEntryOnFill = false;
            picker.SetCount(24, (RectTransform _, int _) => { });
            picker.ScrollTo(10, UIAnimScrollList.Alignment.Center);

            float AngleX(int i) => Mathf.DeltaAngle(0, picker.GetItem(i).Find("Label").localEulerAngles.x);
            Assert.AreEqual(0f, AngleX(10), 0.5f, "가운데 줄은 똑바로");
            Assert.Greater(AngleX(8), 20f, "위 줄은 위로 기울어짐");
            Assert.Less(AngleX(12), -20f, "아래 줄은 아래로");
            Assert.AreEqual(1f, picker.GetItem(10).GetComponentInChildren<Image>().color.a, 0.01f);
            Assert.Less(picker.GetItem(12).GetComponentInChildren<Image>().color.a, 0.8f, "가장자리로 갈수록 흐려짐");
        }

        [Test]
        public void 페이지_점의_On_Off를_에디터로_만들면_현재_페이지_점이_커진다()
        {
            var dot = Go("Dot", null, new Vector2(24, 24));
            dot.SetActive(false);
            dot.AddComponent<Image>();

            foreach (var (state, scale) in new[] { ("On", 1.5f), ("Off", 1f) })
            {
                var created = Create(dot, state);
                _window.AddTracks(new List<(UIAnimPickKind, Component, UIAnimProperty)> { (UIAnimPickKind.Property, dot.transform, UIAnimProperty.Scale) });
                var track = created.tracks.Single();
                _window.Timeline.KeyClicked(track.keys[1], false);
                Set("Value", scale);   // linked scale: x = y = z
            }
            CollectionAssert.AreEqual(new[] { "On", "Off" }, dot.GetComponent<UIAnimation>().states.Select(s => s.name).ToArray());

            var pages = Go("Pages", null, new Vector2(300, 300));
            var viewport = Go("Viewport", pages.transform, new Vector2(300, 300));
            var content = Go("Content", viewport.transform, Vector2.zero);
            var scroll = pages.AddComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = (RectTransform)content.transform;
            var list = pages.AddComponent<UIAnimScrollList>();
            list.layout = UIAnimScrollList.LayoutType.Horizontal;
            list.itemPrefab = (RectTransform)Go("Page", null, new Vector2(300, 300)).transform;
            list.itemPrefab.gameObject.SetActive(false);
            list.playEntryOnFill = false;

            var indicator = Go("Dots", null, new Vector2(200, 30)).AddComponent<UIAnimPageIndicator>();
            indicator.list = list;
            indicator.itemPrefab = (RectTransform)dot.transform;
            typeof(UIAnimPageIndicator).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(indicator, null);
            list.SetCount(3, (RectTransform _, int _) => { });
            list.ScrollTo(1);

            var dots = Enumerable.Range(0, 3).Select(i => indicator.transform.GetChild(i).GetComponent<UIAnimation>()).ToArray();
            Assert.IsTrue(dots[1].IsPlaying("On"), "현재 페이지 점 On");
            Assert.IsTrue(dots[0].IsPlaying("Off"), "떠난 점 Off");
            dots[1].Sample("On", float.MaxValue);
            Assert.AreEqual(1.5f, dots[1].transform.localScale.x, 1e-3f, "에디터에서 넣은 값");
        }
    }
}
