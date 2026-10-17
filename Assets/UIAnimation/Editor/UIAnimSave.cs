using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SeoBYP.TimelineAnimation.Editor
{
    /// <summary>
    /// The window's Auto Save and Save: edits go where the UIAnimation lives, the same in every mode.
    /// - Prefab Mode: the prefab. A prefab asset picked in the Project window: that asset.
    /// - A scene object, in edit mode: not handled here (the window shows Auto Save / Save off). The edit is on the object
    ///   already and is written with the scene (Unity's Ctrl+S), so the scene's other unsaved changes are never saved by us.
    /// - Play mode, a running copy of a prefab (Instantiate keeps no link to it): that prefab, found through
    ///   <see cref="UIAnimation.source"/> (written on import). The other running copies take the edit too.
    /// - Play mode, an object of the scene: the same object in the scene once Play ends (Unity would drop the edit),
    ///   again on the object only.
    /// </summary>
    [InitializeOnLoad]
    internal static class UIAnimSave
    {
        const string AutoSaveKey = "SeoBYP.UIAnimation.AutoSave";
        const double SaveDelay = 0.4;   // seconds after the last edit (a dragged value saves once, not every frame)

        public static bool AutoSave
        {
            get => EditorPrefs.GetBool(AutoSaveKey, true);
            set => EditorPrefs.SetBool(AutoSaveKey, value);
        }

        /// <summary>Around a save that writes a whole prefab or scene: a preview steps aside, so its values are not saved.</summary>
        public static event Action Saving, Saved;

        static readonly HashSet<Component> Pending = new();
        // Play mode edits to objects of a scene, by GlobalObjectId: written into the scene when Play ends.
        static readonly Dictionary<string, (string json, List<(string path, string reference)> references)> SceneEdits = new();
        static double _saveAt = double.MaxValue;

        /// <summary>Edits not saved yet (auto save off, or within the save delay).</summary>
        public static bool HasPending => Pending.Count > 0;

        static UIAnimSave()
        {
            EditorApplication.update += () =>
            {
                if (EditorApplication.timeSinceStartup >= _saveAt) Save();
            };
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.ExitingEditMode)
                {
                    NoteSceneObjects();
                    _sceneIds = null;
                    _editIds = null;
                }
                else if (change == PlayModeStateChange.ExitingPlayMode)
                {
                    if (AutoSave) Save();   // the running objects are still there
                    Pending.Clear();
                }
                else if (change == PlayModeStateChange.EnteredEditMode) ApplySceneEdits();
            };
        }

        /// <summary>Where Save writes this animation's edits (shown in the window).</summary>
        public static string Destination(UIAnimation anim)
        {
            if (anim == null) return "";
            if (EditorUtility.IsPersistent(anim)) return $"Prefab '{Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(anim))}'";
            if (EditorApplication.isPlaying)
                return InScene(anim, out _) ? $"the object in scene '{SceneName(anim.gameObject.scene)}' when Play ends"
                    : Parse(anim.source, out var asset, out _) ? $"Prefab '{Path.GetFileNameWithoutExtension(asset)}'"
                    : "nowhere (its prefab is gone)";
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(anim.gameObject);
            return stage != null ? $"Prefab '{Path.GetFileNameWithoutExtension(stage.assetPath)}'" : $"the object in scene '{SceneName(anim.gameObject.scene)}'";
        }

        /// <summary>Edit mode, an object of a scene: its scene's name (saved with the scene, not by Auto Save / Save), else null.</summary>
        public static string SceneFileOf(UIAnimation anim) =>
            anim == null || EditorApplication.isPlaying || EditorUtility.IsPersistent(anim)
            || UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(anim.gameObject) != null
                ? null : SceneName(anim.gameObject.scene);

        static string SceneName(Scene scene) => string.IsNullOrEmpty(scene.path) ? "Untitled" : scene.name;

        /// <summary>The window changed these (the edited animation and the ancestors holding its overrides).</summary>
        public static void Changed(IEnumerable<UIAnimation> owners)
        {
            foreach (var owner in owners)
            {
                if (owner == null || SceneFileOf(owner) != null) continue;   // written with its scene
                if (EditorApplication.isPlaying && !EditorUtility.IsPersistent(owner))
                {
                    if (owner.HasUnplayableState) owner.Reinitialize();   // a new state: playable from code right away
                    if (!InScene(owner, out _)) CopyToOtherCopies(owner);
                }
                Pending.Add(owner);
                if (owner.TryGetComponent<UIAnimTrigger>(out var trigger)) Pending.Add(trigger);
            }
            if (AutoSave && Pending.Count > 0) _saveAt = EditorApplication.timeSinceStartup + SaveDelay;
        }

        /// <summary>Save (button, Ctrl+S): saves this animation's place now, edited or not.</summary>
        public static void SaveNow(UIAnimation anim)
        {
            if (anim != null) Changed(new[] { anim });
            Save();
        }

        /// <summary>Writes the pending edits where each lives.</summary>
        public static void Save()
        {
            _saveAt = double.MaxValue;
            var saving = Pending.Where(c => c != null).ToList();
            Pending.Clear();
            var stages = new HashSet<UnityEditor.SceneManagement.PrefabStage>();
            foreach (var component in saving)
            {
                if (EditorUtility.IsPersistent(component)) AssetDatabase.SaveAssetIfDirty(component);
                else if (EditorApplication.isPlaying)
                {
                    if (InScene(component, out var id)) KeepForScene(component, id);
                    else SaveToPrefab(component);
                }
                else if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(component.gameObject) is { } stage) stages.Add(stage);
            }
            if (stages.Count == 0) return;
            Saving?.Invoke();
            try
            {
                foreach (var stage in stages)
                {
                    if (stage == null || stage.prefabContentsRoot == null) continue;
                    PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath);
                    stage.ClearDirtiness();
                }
            }
            finally { Saved?.Invoke(); }
        }

        // ---------- Play mode: a running copy → its prefab ----------

        static void SaveToPrefab(Component component)
        {
            if (!Parse(Source(component), out var assetPath, out var path)) return;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            var into = asset != null ? Find(asset.transform, path) : null;
            if (into == null) return;
            if (!into.TryGetComponent(component.GetType(), out var target))
            {
                AddToPrefab(component, assetPath, path);   // e.g. Play On set while playing: its UIAnimTrigger is new
                return;
            }
            CopyInto(component, CopyRoot(component.transform, path), target, asset.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);   // a nested prefab's part: an override here
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        // A component the prefab does not have yet. An asset takes no new component in place, so this opens it as prefab
        // contents (which runs Awake, so a few fields Unity fills in on load may be saved with it); only for this case.
        static void AddToPrefab(Component component, string assetPath, string path)
        {
            var contents = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                var into = Find(contents.transform, path);
                if (into == null) return;
                CopyInto(component, CopyRoot(component.transform, path), into.gameObject.AddComponent(component.GetType()), contents.transform);
                PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        // ---------- Play mode: an object of a scene → the scene, when Play ends ----------

        // Play mode: an object of the scene, not a running copy. Neither the id (in a saved scene Unity gives copies made
        // while playing a scene id too) nor the prefab link (not reported while playing) can tell, so the scenes' objects
        // are noted just before Play starts (kept across the domain reload) and looked up.
        // While playing, a part of a prefab instance placed in the scene has a different id than in edit mode (one number,
        // its file id in the scene, instead of the pair prefab part + instance), so both forms are noted.
        const string SceneIdsKey = "SeoBYP.UIAnimation.SceneObjectIds";      // the animations, Play mode form
        const string EditIdsKey = "SeoBYP.UIAnimation.SceneObjectEditIds";   // Play mode form = edit mode form, where they differ
        static HashSet<string> _sceneIds;
        static Dictionary<string, string> _editIds;

        static void NoteSceneObjects()
        {
            var objects = new List<Object>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    objects.Add(t.gameObject);
                    objects.AddRange(t.GetComponents<Component>().Where(c => c != null));
                }
            }
            var ids = new GlobalObjectId[objects.Count];
            GlobalObjectId.GetGlobalObjectIdsSlow(objects.ToArray(), ids);
            var animations = new List<string>();
            var renamed = new List<string>();
            for (var i = 0; i < ids.Length; i++)
            {
                if (ids[i].targetObjectId == 0) continue;   // no id (a scene never saved): nothing to find again after Play
                var play = PlayForm(ids[i]);
                if (objects[i] is UIAnimation) animations.Add(play);
                if (ids[i].targetPrefabId != 0) renamed.Add(play + "=" + ids[i]);
            }
            SessionState.SetString(SceneIdsKey, string.Join("|", animations));
            SessionState.SetString(EditIdsKey, string.Join("|", renamed));
        }

        // A prefab instance's part in Play mode: its file id in the scene, (part ^ instance) without the sign bit.
        static string PlayForm(GlobalObjectId id) => id.targetPrefabId == 0 ? id.ToString()
            : $"GlobalObjectId_V1-{id.identifierType}-{id.assetGUID}-{(id.targetObjectId ^ id.targetPrefabId) & long.MaxValue}-0";

        static string EditForm(string play)
        {
            _editIds ??= SessionState.GetString(EditIdsKey, "").Split('|').Where(x => x.Contains('='))
                .Select(x => x.Split('=')).ToDictionary(x => x[0], x => x[1]);
            return _editIds.TryGetValue(play, out var edit) ? edit : play;
        }

        static bool InScene(Component c, out GlobalObjectId id)
        {
            var anim = c as UIAnimation ?? c.GetComponent<UIAnimation>();
            id = GlobalObjectId.GetGlobalObjectIdSlow(c);
            _sceneIds ??= new HashSet<string>(SessionState.GetString(SceneIdsKey, "").Split('|'));
            return anim != null && _sceneIds.Contains(GlobalObjectId.GetGlobalObjectIdSlow(anim).ToString());
        }

        static void KeepForScene(Component c, GlobalObjectId id)
        {
            var references = new List<(string, string)>();
            var p = new SerializedObject(c).GetIterator();
            while (p.NextVisible(true))
                if (p.propertyType == SerializedPropertyType.ObjectReference && p.name != "m_Script" && p.objectReferenceValue != null
                    && !EditorUtility.IsPersistent(p.objectReferenceValue))
                    references.Add((p.propertyPath, EditForm(GlobalObjectId.GetGlobalObjectIdSlow(p.objectReferenceValue).ToString())));
            SceneEdits[EditForm(id.ToString())] = (EditorJsonUtility.ToJson(c), references);
        }

        static void ApplySceneEdits()
        {
            if (SceneEdits.Count == 0) return;
            foreach (var (id, (json, references)) in SceneEdits)
            {
                if (!GlobalObjectId.TryParse(id, out var gid) || GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) is not Component c) continue;
                Undo.RecordObject(c, "Play Mode Edits");
                EditorJsonUtility.FromJsonOverwrite(json, c);
                // Scene objects were referenced by their Play mode copies: point at the scene's own.
                var so = new SerializedObject(c);
                foreach (var (path, reference) in references)
                    if (so.FindProperty(path) is { } property && GlobalObjectId.TryParse(reference, out var r))
                        property.objectReferenceValue = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(r);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                EditorUtility.SetDirty(c);   // the scene shows it is modified; saving the scene writes it
            }
            SceneEdits.Clear();
        }

        // ---------- copies ----------

        static void CopyToOtherCopies(UIAnimation edited)
        {
            if (string.IsNullOrEmpty(edited.source)) return;
            Parse(edited.source, out _, out var path);
            var from = CopyRoot(edited.transform, path);
            edited.TryGetComponent<UIAnimTrigger>(out var trigger);
            foreach (var other in Object.FindObjectsByType<UIAnimation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (other == edited || other.source != edited.source || EditorUtility.IsPersistent(other)) continue;
                var to = CopyRoot(other.transform, path);
                CopyInto(edited, from, other, to);
                if (trigger != null)
                    CopyInto(trigger, from, Get(other.gameObject, typeof(UIAnimTrigger)), to);
                other.Reinitialize();
            }
        }

        /// <summary>
        /// Copies <paramref name="from"/>'s data onto <paramref name="to"/>. References to objects under
        /// <paramref name="fromRoot"/> become the object at the same place under <paramref name="toRoot"/>; assets stay.
        /// </summary>
        internal static void CopyInto(Component from, Transform fromRoot, Component to, Transform toRoot)
        {
            var src = new SerializedObject(from);
            var dst = new SerializedObject(to);
            var it = src.GetIterator();
            if (it.NextVisible(true))
                do
                {
                    if (it.name != "m_Script") dst.CopyFromSerializedProperty(it);
                } while (it.NextVisible(false));
            var p = dst.GetIterator();
            while (p.NextVisible(true))
                if (p.propertyType == SerializedPropertyType.ObjectReference && p.name != "m_Script" && p.objectReferenceValue != null)
                    p.objectReferenceValue = Map(p.objectReferenceValue, fromRoot, toRoot);
            dst.ApplyModifiedPropertiesWithoutUndo();
        }

        static Object Map(Object obj, Transform fromRoot, Transform toRoot)
        {
            if (obj == null || EditorUtility.IsPersistent(obj)) return obj;
            var t = obj switch { GameObject go => go.transform, Component c => c.transform, _ => null };
            var path = t != null ? PathFrom(fromRoot, t) : null;
            var at = path != null ? Find(toRoot, path) : null;
            if (at == null) return null;   // outside the prefab: nothing to point at there
            if (obj is GameObject) return at.gameObject;
            var mine = t.GetComponents(obj.GetType());
            var theirs = at.GetComponents(obj.GetType());
            var i = Array.IndexOf(mine, obj);
            return i >= 0 && i < theirs.Length ? theirs[i] : null;
        }

        // ---------- source: "<guid>/<path>" ----------

        internal static string SourceOf(string guid, Transform root, Transform t) => guid + "/" + PathFrom(root, t);

        static string Source(Component c) => c is UIAnimation a ? a.source : c.GetComponent<UIAnimation>()?.source;

        static bool Parse(string source, out string assetPath, out string path)
        {
            assetPath = path = null;
            var slash = source?.IndexOf('/') ?? -1;
            if (slash < 0) return false;
            assetPath = AssetDatabase.GUIDToAssetPath(source.Substring(0, slash));
            path = source.Substring(slash + 1);
            return !string.IsNullOrEmpty(assetPath);
        }

        // The prefab root of a running copy: as many parents up as the object sits deep in the prefab.
        static Transform CopyRoot(Transform t, string path)
        {
            if (path.Length > 0)
                for (var depth = path.Count(ch => ch == '/') + 1; depth > 0 && t != null; depth--) t = t.parent;
            return t;
        }

        static string PathFrom(Transform root, Transform t)
        {
            if (t == root) return "";
            var names = new List<string>();
            for (; t != null && t != root; t = t.parent) names.Add(t.name);
            if (t != root) return null;
            names.Reverse();
            return string.Join("/", names);
        }

        // ponytail: the first child of that name; siblings sharing a name map to the first one.
        static Transform Find(Transform root, string path) => path.Length == 0 ? root : root.Find(path);

        static Component Get(GameObject go, Type type) => go.TryGetComponent(type, out var c) ? c : go.AddComponent(type);
    }

    /// <summary>
    /// Writes <see cref="UIAnimation.source"/> into every prefab with a UIAnimation: when it is imported, and once for the
    /// prefabs already in a project (the first time the editor loads this).
    /// </summary>
    internal sealed class UIAnimSourceStamp : AssetPostprocessor
    {
        const string StampedKey = "SeoBYP.UIAnimation.SourceStamped";

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
                if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) Stamp(path);
        }

        [InitializeOnLoadMethod]
        static void StampExistingOnce()
        {
            if (EditorUserSettings.GetConfigValue(StampedKey) == "1") return;
            EditorApplication.delayCall += StampAll;
        }

        internal static void StampAll()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })) Stamp(AssetDatabase.GUIDToAssetPath(guid));
            EditorUserSettings.SetConfigValue(StampedKey, "1");
        }

        // Edits the asset in place (opening it as prefab contents would run Awake and could change other fields).
        static void Stamp(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return;
            var guid = AssetDatabase.AssetPathToGUID(path);
            foreach (var a in asset.GetComponentsInChildren<UIAnimation>(true))
            {
                var source = UIAnimSave.SourceOf(guid, asset.transform, a.transform);
                if (a.source == source) continue;   // also stops the save's own reimport here
                a.source = source;
                EditorUtility.SetDirty(a);
                PrefabUtility.RecordPrefabInstancePropertyModifications(a);
            }
            AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
}
