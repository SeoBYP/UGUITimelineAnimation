using System.IO;
using System.Linq;
using NUnit.Framework;
using SeoBYP.TimelineAnimation.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>
    /// Button1.prefab reused inside a new popup prefab: overriding its Hover in the popup (✎ in the UI Animation Editor)
    /// changes only that popup; Button1.prefab and Button1s elsewhere stay as they are; ↺ brings the original back.
    /// Uses real prefab assets in a temporary folder.
    /// </summary>
    public sealed class PrefabOverrideTests
    {
        const string SamplePath = "Assets/Prefabs/Button1.prefab";
        const string Folder = "Assets/__UIAnimOverrideTest";
        const string Button1Path = Folder + "/Button1.prefab";   // a copy of the sample: some tests edit the original button
        const string PopupPath = Folder + "/TestPopup.prefab";

        UIAnimationEditorWindow _window;
        GameObject _button1Asset;
        string _button1File;
        GameObject _contents, _popupInstance, _loneButton;

        [SetUp]
        public void SetUp()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SamplePath) == null) Assert.Ignore($"{SamplePath} 샘플이 없음");
            AssetDatabase.CreateFolder("Assets", Folder.Substring("Assets/".Length));
            AssetDatabase.CopyAsset(SamplePath, Button1Path);
            _button1Asset = AssetDatabase.LoadAssetAtPath<GameObject>(Button1Path);
            _button1File = File.ReadAllText(Button1Path);

            // A new popup with Button1.prefab inside it, saved as its own prefab.
            var popup = new GameObject("TestPopup", typeof(RectTransform), typeof(Image));
            popup.AddComponent<UIAnimation>();
            PrefabUtility.InstantiatePrefab(_button1Asset, popup.transform);
            PrefabUtility.SaveAsPrefabAsset(popup, PopupPath);
            Object.DestroyImmediate(popup);

            _window = EditorWindow.GetWindow<UIAnimationEditorWindow>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null) _window.Close();
            if (_contents != null) PrefabUtility.UnloadPrefabContents(_contents);
            if (_popupInstance != null) Object.DestroyImmediate(_popupInstance);
            if (_loneButton != null) Object.DestroyImmediate(_loneButton);
            AssetDatabase.DeleteAsset(Folder);
        }

        // ---------- the editor, as a user drives it ----------

        void Set<T>(string label, T value) =>
            ((INotifyValueChanged<T>)_window.Inspector.Query<Label>(className: "unity-base-field__label").ToList().First(l => l.text == label).parent).value = value;

        // Opens the popup prefab like Prefab Mode does, edits it in the window, saves.
        void EditPopup(System.Action<UIAnimation, UIAnimation> edit)
        {
            _contents = PrefabUtility.LoadPrefabContents(PopupPath);
            var popupAnim = _contents.GetComponent<UIAnimation>();
            var button = _contents.GetComponentsInChildren<UIAnimation>().Single(a => a != popupAnim);
            _window.SetTarget(popupAnim);
            edit(popupAnim, button);
            PrefabUtility.SaveAsPrefabAsset(_contents, PopupPath);
            PrefabUtility.UnloadPrefabContents(_contents);
            _contents = null;
        }

        // The popup's Open plays Button1's Hover at 0.3s (no override yet).
        void AddOpenCallingHover() => EditPopup((popup, button) =>
        {
            _window.AddState();
            Set("Name", "Open");
            _window.AddCall(button, "Hover", 0.3f);   // right-click → Add Child Call → Button1 › Hover
        });

        void OverrideHoverToFast()
        {
            AddOpenCallingHover();
            EditPopup((popup, button) =>
            {
                var call = popup.states[0].childCalls.Single();
                Assert.AreSame(button.FindState("Hover"), call.Target, "처음엔 원본을 재생");

                _window.Timeline.OverrideRequested(call);   // ✎
                Assert.AreEqual(1, popup.overrides.Count, "팝업에 오버라이드가 생김");
                Assert.AreNotSame(button.FindState("Hover"), call.Target, "이제 팝업의 복사본을 재생");

                // Edit the override in place: every end key 0.2s → 0.1s, through the key inspector.
                foreach (var track in call.Target.tracks)
                {
                    _window.Timeline.KeyClicked(track.keys[^1], false);
                    Set("Time (s)", 0.1f);
                }
                Assert.AreEqual(0.1f, call.Target.Duration, 1e-4f, "오버라이드는 0.1초");
                Assert.AreEqual(0.2f, button.FindState("Hover").Duration, 1e-4f, "버튼 자신의 상태는 0.2초 그대로");
            });
        }

        UIAnimation PopupButton()
        {
            _popupInstance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath));
            var popupAnim = _popupInstance.GetComponent<UIAnimation>();
            return _popupInstance.GetComponentsInChildren<UIAnimation>().Single(a => a != popupAnim);
        }

        static float FillAlphaAfter(UIAnimation button, float seconds)
        {
            var playback = button.Prepare("Hover", UIAnimInterrupt.Cancel, null);
            UIAnimation.Step(playback, seconds);
            return button.transform.Find("Fill").GetComponent<Image>().color.a;
        }

        // ---------- tests ----------

        [Test]
        public void 팝업에서_오버라이드하면_팝업의_Button1만_바뀌고_원본_Prefab은_그대로다()
        {
            OverrideHoverToFast();

            Assert.AreEqual(_button1File, File.ReadAllText(Button1Path), "Button1.prefab 파일이 바뀌지 않음");

            var inPopup = PopupButton();
            Assert.AreEqual(0.1f, inPopup.EffectiveState("Hover").Duration, 1e-4f, "팝업 안 Button1은 오버라이드 사용");
            Assert.AreEqual(1f, FillAlphaAfter(inPopup, 0.1f), 1e-3f, "0.1초에 Hover 완료");

            _loneButton = (GameObject)PrefabUtility.InstantiatePrefab(_button1Asset);
            var lone = _loneButton.GetComponent<UIAnimation>();
            Assert.AreEqual(0.2f, lone.EffectiveState("Hover").Duration, 1e-4f, "다른 곳의 Button1은 원본");
            Assert.Less(FillAlphaAfter(lone, 0.1f), 1f, "원본은 0.1초엔 아직 진행 중");
        }

        [Test]
        public void 팝업의_Open이_재생하는_자식_Hover도_오버라이드다()
        {
            OverrideHoverToFast();
            var button = PopupButton();
            var popup = _popupInstance.GetComponent<UIAnimation>();

            var open = popup.Prepare("Open", UIAnimInterrupt.Cancel, null);
            UIAnimation.Step(open, 0.3f);   // the call at 0.3s starts the child's Hover
            Assert.IsTrue(button.IsPlaying("Hover"));
            Assert.AreEqual(0.4f, popup.FindState("Open").Duration, 1e-4f, "부모 길이 = 호출 0.3 + 오버라이드 0.1");
        }

        [Test]
        public void 되돌리기하면_팝업도_원본으로_돌아오고_원본_Prefab은_계속_그대로다()
        {
            OverrideHoverToFast();

            EditPopup((popup, button) =>
            {
                _window.RemoveOverride(popup.states[0].childCalls.Single());   // ↺ (after its confirmation)
                Assert.AreEqual(0, popup.overrides.Count);
                Assert.AreSame(button.FindState("Hover"), popup.states[0].childCalls.Single().Target, "다시 원본을 재생");
            });

            Assert.AreEqual(_button1File, File.ReadAllText(Button1Path), "Button1.prefab 파일이 바뀌지 않음");
            var inPopup = PopupButton();
            Assert.AreEqual(0.2f, inPopup.EffectiveState("Hover").Duration, 1e-4f, "팝업 안 Button1도 원본");
        }
    
        [Test]
        public void 씬의_팝업_인스턴스에서_오버라이드해도_Unity_Revert로_되돌아온다()
        {
            AddOpenCallingHover();
            var popupFile = File.ReadAllText(PopupPath);

            // The popup placed in a scene; the override is made on that instance (Hierarchy selection → ✎).
            var button = PopupButton();
            var popup = _popupInstance.GetComponent<UIAnimation>();
            _window.SetTarget(popup);
            var call = popup.states[0].childCalls.Single();
            _window.Timeline.OverrideRequested(call);
            foreach (var track in call.Target.tracks)
            {
                _window.Timeline.KeyClicked(track.keys[^1], false);
                Set("Time (s)", 0.1f);
            }
            Assert.AreEqual(0.1f, button.EffectiveState("Hover").Duration, 1e-4f, "씬의 팝업은 오버라이드");
            Assert.IsTrue(PrefabUtility.HasPrefabInstanceAnyOverrides(_popupInstance, false), "Unity가 인스턴스 오버라이드로 기록");
            Assert.AreEqual(popupFile, File.ReadAllText(PopupPath), "팝업 Prefab 파일은 그대로");
            Assert.AreEqual(_button1File, File.ReadAllText(Button1Path), "Button1 Prefab 파일도 그대로");

            // Inspector → Overrides → Revert (on the UIAnimation component).
            PrefabUtility.RevertObjectOverride(popup, InteractionMode.AutomatedAction);
            Assert.AreEqual(0, popup.overrides.Count, "오버라이드가 사라짐");
            Assert.AreEqual(0.2f, button.EffectiveState("Hover").Duration, 1e-4f, "원본 Hover로 돌아옴");
        }

        [Test]
        public void 원본_Button1을_고치면_오버라이드하지_않은_곳만_따라가고_오버라이드한_팝업은_복사본을_유지한다()
        {
            OverrideHoverToFast();

            // Later, someone changes Button1.prefab's own Hover: the fill now turns blue.
            var contents = PrefabUtility.LoadPrefabContents(Button1Path);
            var hover = contents.GetComponent<UIAnimation>().FindState("Hover");
            var fillTrack = hover.tracks.Single(t => t.target.name == "Fill");
            fillTrack.keys[^1].value = Color.blue;
            PrefabUtility.SaveAsPrefabAsset(contents, Button1Path);
            PrefabUtility.UnloadPrefabContents(contents);

            _loneButton = (GameObject)PrefabUtility.InstantiatePrefab(_button1Asset);
            var lone = _loneButton.GetComponent<UIAnimation>();
            FillAlphaAfter(lone, 1f);
            Assert.AreEqual(Color.blue, lone.transform.Find("Fill").GetComponent<Image>().color, "따로 놓인 Button1은 바뀐 원본");

            var inPopup = PopupButton();
            FillAlphaAfter(inPopup, 1f);
            Assert.AreNotEqual(Color.blue, inPopup.transform.Find("Fill").GetComponent<Image>().color,
                "오버라이드한 팝업은 오버라이드할 때 복사한 값(빨강)을 유지 — 원본 변경이 따라오지 않음");
        }
    }
}
