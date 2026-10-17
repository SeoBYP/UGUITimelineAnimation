using System.Collections;
using System.Linq;
using NUnit.Framework;
using SeoBYP.TimelineAnimation.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>
    /// Record mode (● in the window): a change made in the Inspector / Scene view becomes a key at the playhead, on the
    /// property's track or on a new one, and the object itself keeps its original value.
    /// </summary>
    public sealed class EditorRecordTests
    {
        UIAnimationEditorWindow _window;
        GameObject _root;
        RectTransform _child;

        [SetUp]
        public void SetUp()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("에디터 창 테스트: 그래픽 장치가 필요해서 건너뜀 (-nographics)");
            _root = new GameObject("Root", typeof(RectTransform));
            _child = (RectTransform)new GameObject("Child", typeof(RectTransform)).transform;
            _child.SetParent(_root.transform, false);
            _root.AddComponent<UIAnimation>().states.Add(new UIAnimState
            {
                name = "Pop",
                tracks =
                {
                    new UIAnimTrack
                    {
                        target = _child, property = UIAnimProperty.Scale, start = UIAnimStart.KeyValue,
                        keys =
                        {
                            new UIAnimKey { time = 0, value = Vector3.one, ease = UIAnimEase.Linear },
                            new UIAnimKey { time = 1, value = Vector3.one * 2, ease = UIAnimEase.Linear },
                        },
                    },
                },
            });
            _window = EditorWindow.GetWindow<UIAnimationEditorWindow>();
            _window.SetTarget(_root.GetComponent<UIAnimation>());
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null) _window.Close();
            Object.DestroyImmediate(_root);
            UnityEditor.SceneManagement.StageUtility.GoToMainStage();
            AssetDatabase.DeleteAsset(PrefabPath);
        }

        const string PrefabPath = "Assets/__RecordTest.prefab";

        UIAnimState State => _root.GetComponent<UIAnimation>().states[0];

        // What the Inspector does when a field is changed: a SerializedObject edit, reported to the Undo system.
        static void EditInInspector(Object target, string field, System.Action<SerializedProperty> set)
        {
            var so = new SerializedObject(target);
            set(so.FindProperty(field));
            so.ApplyModifiedProperties();
            Undo.FlushUndoRecordObjects();   // the Undo system reports the change (the editor does this every frame)
        }

        void RecordAt(float time)
        {
            _window.Timeline.SetTime(time);
            _window.ToggleRecord();
            Assert.IsTrue(_window.Recording);
        }

        [Test]
        public void 트랙이_없는_속성을_바꾸면_새_트랙에_0초와_재생_막대_키가_생긴다()
        {
            RecordAt(0.5f);
            EditInInspector(_child, "m_AnchoredPosition", p => p.vector2Value = new Vector2(100, 0));
            _window.ApplyRecorded();

            var track = State.tracks.Find(t => t.target == _child && t.property == UIAnimProperty.AnchoredPosition);
            Assert.NotNull(track, "Position 트랙이 생김");
            Assert.AreEqual(2, track.keys.Count);
            Assert.AreEqual((0f, Vector4.zero), (track.keys[0].time, track.keys[0].value), "0초 = 원래 값");
            Assert.AreEqual(0.5f, track.keys[1].time, 1e-4f);
            Assert.AreEqual(new Vector4(100, 0), track.keys[1].value, "재생 막대 = 바꾼 값");
            Assert.AreEqual(new Vector2(100, 0), _child.anchoredPosition, "미리보기는 새 키를 보여 줌");

            _window.Preview.Stop();
            Assert.AreEqual(Vector2.zero, _child.anchoredPosition, "오브젝트 자체는 원래 값");
        }

        [Test]
        public void 트랙이_있는_속성을_바꾸면_재생_막대에_키가_들어간다()
        {
            RecordAt(0.5f);
            EditInInspector(_child, "m_LocalScale", p => p.vector3Value = Vector3.one * 3);
            _window.ApplyRecorded();

            var track = State.tracks.Single();
            CollectionAssert.AreEqual(new[] { 0f, 0.5f, 1f }, track.keys.ConvertAll(k => k.time), "가운데에 키가 들어감");
            Assert.AreEqual(3f, track.keys[1].value.x, 1e-4f);
            Assert.AreEqual(3f, _child.localScale.x, 1e-4f, "미리보기 = 0.5초의 새 키");

            _window.Preview.Stop();
            Assert.AreEqual(1f, _child.localScale.x, 1e-4f, "오브젝트 자체는 원래 값");
        }

        [Test]
        public void 색_알파_회전_활성은_그_트랙으로_가고_바꾸기_전_값을_되찾는다()
        {
            var image = _child.gameObject.AddComponent<UnityEngine.UI.Image>();
            Assert.IsTrue(UIAnimRecorder.TryMap(image, "m_Color.a", State, out var target, out var property));
            Assert.AreEqual((image, UIAnimProperty.GraphicColor), ((UnityEngine.UI.Image)target, property), "알파 트랙이 없으면 색 트랙");
            State.tracks.Add(new UIAnimTrack { target = image, property = UIAnimProperty.GraphicAlpha });
            UIAnimRecorder.TryMap(image, "m_Color.a", State, out _, out property);
            Assert.AreEqual(UIAnimProperty.GraphicAlpha, property, "알파 트랙이 있으면 그 트랙");
            Assert.AreEqual(0.25f, UIAnimRecorder.Previous(new Vector4(1, 0), UIAnimProperty.GraphicAlpha,
                new System.Collections.Generic.Dictionary<string, string> { ["m_Color.a"] = "0.25" }).x, 1e-4f);

            Assert.IsTrue(UIAnimRecorder.TryMap(_child.gameObject, "m_IsActive", State, out target, out property));
            Assert.AreEqual((_child, UIAnimProperty.Active), ((RectTransform)target, property));

            // The rotate tool reports only the quaternion: 30 degrees around z.
            var q = Quaternion.Euler(0, 0, 30);
            var before = UIAnimRecorder.Previous(new Vector4(0, 0, 90), UIAnimProperty.Rotation, new System.Collections.Generic.Dictionary<string, string>
            {
                ["m_LocalRotation.x"] = q.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                ["m_LocalRotation.y"] = q.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                ["m_LocalRotation.z"] = q.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                ["m_LocalRotation.w"] = q.w.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            });
            Assert.AreEqual(30f, before.z, 1e-3f);
        }

        [UnityTest]
        public IEnumerator Prefab_모드에서_녹화하면_키가_Prefab에_저장되고_오브젝트_값은_그대로다()
        {
            PrefabUtility.SaveAsPrefabAsset(_root, PrefabPath);
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.OpenPrefab(PrefabPath);
            var root = stage.prefabContentsRoot;
            var child = (RectTransform)root.transform.Find("Child");
            _window.SetTarget(root.GetComponent<UIAnimation>());
            yield return null;
            RecordAt(0.5f);
            EditInInspector(child, "m_AnchoredPosition", p => p.vector2Value = new Vector2(100, 0));
            _window.ApplyRecorded();
            var end = Time.realtimeSinceStartup + 5f;
            while (UIAnimSave.HasPending && Time.realtimeSinceStartup < end) yield return null;   // auto save

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var track = asset.GetComponent<UIAnimation>().states[0].tracks.Find(t => t.property == UIAnimProperty.AnchoredPosition);
            Assert.NotNull(track, "녹화한 트랙이 Prefab에 저장");
            Assert.AreEqual(new Vector4(100, 0), track.keys.Last().value);
            Assert.AreEqual(Vector2.zero, ((RectTransform)asset.transform.Find("Child")).anchoredPosition, "Prefab의 오브젝트 값은 원래 값 (미리보기 값이 저장되지 않음)");
            Assert.IsTrue(_window.Preview.IsActive, "저장 뒤에도 미리보기 유지");
        }

        [Test]
        public void 원래_값_기준_트랙은_원래_값과의_차이로_녹화된다()
        {
            _child.localScale = Vector3.one * 2;   // the rest value
            var scale = State.tracks.Single();
            scale.relativeToRest = true;
            scale.keys[0].value = Vector4.zero;
            scale.keys[1].value = Vector3.one;
            RecordAt(0.5f);   // the preview captures the rest (2)
            EditInInspector(_child, "m_LocalScale", p => p.vector3Value = Vector3.one * 5);
            _window.ApplyRecorded();

            Assert.AreEqual(3f, scale.keys.Single(k => Mathf.Approximately(k.time, 0.5f)).value.x, 1e-4f, "5 - 원래 값 2 = 3");
            _window.Preview.Stop();
            Assert.AreEqual(2f, _child.localScale.x, 1e-4f, "오브젝트는 원래 값");
        }

        [Test]
        public void 시작이_현재_값인_트랙에_0초로_녹화하면_그_값에서_시작하게_된다()
        {
            var scale = State.tracks.Single();
            scale.start = UIAnimStart.Current;
            RecordAt(0f);
            EditInInspector(_child, "m_LocalScale", p => p.vector3Value = Vector3.one * 0.5f);
            _window.ApplyRecorded();

            Assert.AreEqual(UIAnimStart.KeyValue, scale.start, "0초 키가 실제로 쓰이도록 Key Value로");
            Assert.AreEqual(0.5f, scale.keys[0].value.x, 1e-4f);
        }

        [Test]
        public void 녹화한_키는_Ctrl_Z_한_번으로_되돌린다()
        {
            RecordAt(0.5f);
            EditInInspector(_child, "m_AnchoredPosition", p => p.vector2Value = new Vector2(100, 0));
            _window.ApplyRecorded();
            Assert.AreEqual(2, State.tracks.Count);

            Undo.PerformUndo();
            Assert.AreEqual(1, State.tracks.Count, "새 트랙이 없어짐");
            _window.Preview.Stop();
            Assert.AreEqual(Vector2.zero, _child.anchoredPosition, "오브젝트도 원래 값");
        }

        [UnityTest]
        public IEnumerator Play_중_녹화하면_키가_원본_Prefab에_저장된다()
        {
            PrefabUtility.SaveAsPrefabAsset(_root, PrefabPath);
            yield return new EnterPlayMode();

            var copy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var child = (RectTransform)copy.transform.Find("Child");
            var window = EditorWindow.GetWindow<UIAnimationEditorWindow>();
            window.SetTarget(copy.GetComponent<UIAnimation>());
            yield return null;
            window.Timeline.SetTime(0.5f);
            window.ToggleRecord();
            EditInInspector(child, "m_AnchoredPosition", p => p.vector2Value = new Vector2(100, 0));
            window.ApplyRecorded();
            Assert.AreEqual(2, copy.GetComponent<UIAnimation>().states[0].tracks.Count, "실행 중 복사본에 녹화됨");
            var end = Time.realtimeSinceStartup + 5f;
            while (UIAnimSave.HasPending && Time.realtimeSinceStartup < end) yield return null;   // auto save
            yield return new ExitPlayMode();

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var track = asset.GetComponent<UIAnimation>().states[0].tracks.Find(t => t.property == UIAnimProperty.AnchoredPosition);
            Assert.NotNull(track, "Play 중 녹화한 트랙이 Prefab에 남음");
            Assert.AreSame(asset.transform.Find("Child"), track.target, "Prefab 안의 Child를 가리킴");
            Assert.AreEqual(new Vector4(100, 0), track.keys.Last().value);
        }
    }
}
