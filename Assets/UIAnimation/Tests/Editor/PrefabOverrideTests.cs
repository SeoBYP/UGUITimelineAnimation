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
    /// Button1.prefab reused inside a new popup prefab. Overriding Hover tracks in the popup (click a dimmed track in the UI
    /// Animation Editor) changes only that popup, per track: the other tracks keep following Button1.prefab, which itself never
    /// changes. × reverts one track, ↺ all of them. Uses real prefab assets in a temporary folder (a copy of the sample button).
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
            // The real editor window needs a graphics device: skipped under -nographics (CI without a virtual display).
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("에디터 창 테스트: 그래픽 장치가 필요해서 건너뜀 (-nographics)");
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

        // Edits Button1.prefab itself (someone changing the shared button later).
        void EditButton(System.Action<UIAnimState> edit)
        {
            var contents = PrefabUtility.LoadPrefabContents(Button1Path);
            edit(contents.GetComponent<UIAnimation>().FindState("Hover"));
            PrefabUtility.SaveAsPrefabAsset(contents, Button1Path);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        // The popup's Open plays Button1's Hover at 0.3s (no override yet).
        void AddOpenCallingHover() => EditPopup((popup, button) =>
        {
            _window.AddState();
            Set("Name", "Open");
            _window.AddCall(button, "Hover", 0.3f);   // right-click → Add Child Call → Button1 › Hover
        });

        // Click the dimmed track (overrides just it), then end key 0.2s → 0.1s in the key inspector.
        UIAnimTrack OverrideFast(UIAnimation popup, UIAnimChildCall call, string trackOwner)
        {
            var original = call.child.FindState("Hover").tracks.Single(t => t.target.name == trackOwner);
            _window.Timeline.OverrideRequested(call, original);
            var copy = UIAnimTimelineView.OverrideOf(popup, call).Find(original);
            Assert.AreNotSame(original, copy, "복사본을 편집");
            _window.Timeline.KeyClicked(copy.keys[^1], false);
            Set("Time (s)", 0.1f);
            return copy;
        }

        void OverrideHoverToFast(params string[] trackOwners)
        {
            AddOpenCallingHover();
            EditPopup((popup, button) =>
            {
                var call = popup.states[0].childCalls.Single();
                Assert.AreSame(button.FindState("Hover"), call.Target, "처음엔 원본을 재생");
                foreach (var owner in trackOwners) OverrideFast(popup, call, owner);
                Assert.AreEqual(trackOwners.Length, popup.overrides.Single().tracks.Count, "바꾼 트랙만 저장");
                Assert.AreEqual(0.2f, button.FindState("Hover").Duration, 1e-4f, "버튼 자신의 상태는 0.2초 그대로");
            });
        }

        UIAnimation PopupButton()
        {
            _popupInstance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath));
            var popupAnim = _popupInstance.GetComponent<UIAnimation>();
            return _popupInstance.GetComponentsInChildren<UIAnimation>().Single(a => a != popupAnim);
        }

        UIAnimation LoneButton()
        {
            _loneButton = (GameObject)PrefabUtility.InstantiatePrefab(_button1Asset);
            return _loneButton.GetComponent<UIAnimation>();
        }

        static Color ColorAfter(UIAnimation button, string child, float seconds)
        {
            UIAnimation.Step(button.Prepare("Hover", UIAnimInterrupt.Cancel, null), seconds);
            return button.transform.Find(child).GetComponent<UnityEngine.UI.Graphic>().color;
        }

        // ---------- tests ----------

        [Test]
        public void 팝업에서_오버라이드하면_팝업의_Button1만_바뀌고_원본_Prefab은_그대로다()
        {
            OverrideHoverToFast("Fill", "Label");

            Assert.AreEqual(_button1File, File.ReadAllText(Button1Path), "Button1.prefab 파일이 바뀌지 않음");
            var inPopup = PopupButton();
            Assert.AreEqual(0.1f, inPopup.EffectiveState("Hover").Duration, 1e-4f, "팝업 안 Button1은 오버라이드 사용");
            Assert.AreEqual(1f, ColorAfter(inPopup, "Fill", 0.1f).a, 1e-3f, "0.1초에 Hover 완료");

            var lone = LoneButton();
            Assert.AreEqual(0.2f, lone.EffectiveState("Hover").Duration, 1e-4f, "다른 곳의 Button1은 원본");
            Assert.Less(ColorAfter(lone, "Fill", 0.1f).a, 1f, "원본은 0.1초엔 아직 진행 중");
        }

        [Test]
        public void 팝업의_Open이_재생하는_자식_Hover도_오버라이드다()
        {
            OverrideHoverToFast("Fill", "Label");
            var button = PopupButton();
            var popup = _popupInstance.GetComponent<UIAnimation>();

            UIAnimation.Step(popup.Prepare("Open", UIAnimInterrupt.Cancel, null), 0.3f);   // the call at 0.3s starts the child's Hover
            Assert.IsTrue(button.IsPlaying("Hover"));
            Assert.AreEqual(0.4f, popup.FindState("Open").Duration, 1e-4f, "부모 길이 = 호출 0.3 + 오버라이드 0.1");
        }

        [Test]
        public void 원본에서_오버라이드하지_않은_트랙을_바꾸면_팝업도_따라간다()
        {
            OverrideHoverToFast("Fill");   // only the fill is overridden; the label follows Button1.prefab
            EditButton(hover =>
            {
                foreach (var track in hover.tracks) track.keys[^1].value = Color.blue;   // later, the shared button changes
            });

            var inPopup = PopupButton();
            Assert.AreEqual(Color.blue, ColorAfter(inPopup, "Label", 1f), "오버라이드 안 한 Label은 바뀐 원본");
            Assert.AreNotEqual(Color.blue, ColorAfter(inPopup, "Fill", 1f), "오버라이드한 Fill은 팝업 값 유지");
            Assert.AreEqual(Color.blue, ColorAfter(LoneButton(), "Fill", 1f), "따로 놓인 Button1은 바뀐 원본");
        }

        [Test]
        public void 원본에서_트랙을_지우면_오버라이드_안_한_트랙만_사라진다()
        {
            OverrideHoverToFast("Fill");
            EditButton(hover => hover.tracks.RemoveAll(t => t.target.name == "Fill" || t.target.name == "Label"));   // both removed
            var inPopup = PopupButton();
            var tracks = inPopup.EffectiveState("Hover").tracks;
            Assert.That(tracks.Any(t => t.target.name == "Fill"), "오버라이드한 Fill은 원본에서 지워져도 팝업에 남음");
            Assert.That(tracks.All(t => t.target.name != "Label"), "오버라이드 안 한 Label은 원본처럼 사라짐");
        }

        [Test]
        public void 원본에_추가한_트랙은_팝업에도_생긴다()
        {
            OverrideHoverToFast("Fill");
            EditButton(hover =>
            {
                var root = hover.tracks[0].target.transform.root;
                hover.tracks.Add(new UIAnimTrack
                {
                    target = root, property = UIAnimProperty.Scale,
                    keys = { new UIAnimKey { time = 0f }, new UIAnimKey { time = 0.2f, value = Vector3.one * 1.1f } },
                });
            });
            var inPopup = PopupButton();
            Assert.That(inPopup.EffectiveState("Hover").tracks.Any(t => t.property == UIAnimProperty.Scale), "새 Scale 트랙이 팝업에도");
        }

        [Test]
        public void 트랙_하나만_되돌리면_그_트랙만_원본으로_돌아간다()
        {
            OverrideHoverToFast("Fill", "Label");
            EditPopup((popup, button) =>
            {
                var call = popup.states[0].childCalls.Single();
                var fill = popup.overrides.Single().tracks.Single(t => t.target.name == "Fill");
                _window.Timeline.RemoveRequested(fill);   // × on the overridden Fill track
                Assert.AreEqual(1, popup.overrides.Single().tracks.Count, "Label만 오버라이드로 남음");
                var merged = call.Target.tracks;
                Assert.AreSame(button.FindState("Hover").tracks.Single(t => t.target.name == "Fill"),
                    merged.Single(t => t.target.name == "Fill"), "Fill은 원본 트랙");
            });
            Assert.AreEqual(_button1File, File.ReadAllText(Button1Path), "Button1.prefab 파일이 바뀌지 않음");
        }

        [Test]
        public void 전체_되돌리기하면_팝업도_원본으로_돌아오고_원본_Prefab은_계속_그대로다()
        {
            OverrideHoverToFast("Fill", "Label");
            EditPopup((popup, button) =>
            {
                _window.RemoveOverride(popup.states[0].childCalls.Single());   // ↺ (after its confirmation)
                Assert.AreEqual(0, popup.overrides.Count);
                Assert.AreSame(button.FindState("Hover"), popup.states[0].childCalls.Single().Target, "다시 원본을 재생");
            });

            Assert.AreEqual(_button1File, File.ReadAllText(Button1Path), "Button1.prefab 파일이 바뀌지 않음");
            Assert.AreEqual(0.2f, PopupButton().EffectiveState("Hover").Duration, 1e-4f, "팝업 안 Button1도 원본");
        }

        [Test]
        public void 씬의_팝업_인스턴스에서_오버라이드해도_Unity_Revert로_되돌아온다()
        {
            AddOpenCallingHover();
            var popupFile = File.ReadAllText(PopupPath);

            var button = PopupButton();
            var popup = _popupInstance.GetComponent<UIAnimation>();
            _window.SetTarget(popup);
            var call = popup.states[0].childCalls.Single();
            OverrideFast(popup, call, "Fill");
            OverrideFast(popup, call, "Label");
            Assert.AreEqual(0.1f, button.EffectiveState("Hover").Duration, 1e-4f, "씬의 팝업은 오버라이드");
            Assert.IsTrue(PrefabUtility.HasPrefabInstanceAnyOverrides(_popupInstance, false), "Unity가 인스턴스 오버라이드로 기록");
            Assert.AreEqual(popupFile, File.ReadAllText(PopupPath), "팝업 Prefab 파일은 그대로");
            Assert.AreEqual(_button1File, File.ReadAllText(Button1Path), "Button1 Prefab 파일도 그대로");

            PrefabUtility.RevertObjectOverride(popup, InteractionMode.AutomatedAction);   // Inspector → Overrides → Revert
            Assert.AreEqual(0, popup.overrides.Count, "오버라이드가 사라짐");
            Assert.AreEqual(0.2f, button.EffectiveState("Hover").Duration, 1e-4f, "원본 Hover로 돌아옴");
        }

        [Test]
        public void 타임라인에서_오버라이드한_트랙만_파란_이름으로_편집되고_나머지는_흐리다()
        {
            AddOpenCallingHover();
            EditPopup((popup, button) =>
            {
                var call = popup.states[0].childCalls.Single();
                var revertAll = _window.Timeline.Query<Button>().ToList().Single(b => b.text == "↺");
                Assert.IsFalse(revertAll.enabledSelf, "오버라이드가 없으면 ↺ 비활성");

                OverrideFast(popup, call, "Fill");
                var rows = _window.Timeline.Query(className: "uia-row").ToList().Where(r => r.Q(className: "uia-lane") != null).ToList();
                var fillRow = rows.Single(r => r.Q<Label>().text.StartsWith("Fill"));
                var labelRow = rows.Single(r => r.Q<Label>().text.StartsWith("Label"));
                Assert.That(fillRow.Q<Label>().ClassListContains("uia-track-override"), "Fill: 오버라이드 색");
                Assert.IsFalse(fillRow.ClassListContains("uia-row--readonly"), "Fill: 편집 가능");
                Assert.That(labelRow.ClassListContains("uia-row--readonly"), "Label: 흐리게(원본)");
                Assert.IsTrue(_window.Timeline.Query<Button>().ToList().Single(b => b.text == "↺").enabledSelf, "이제 ↺ 활성");
            });
        }
    }
}
