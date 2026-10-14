using UnityEditor;
using UnityEngine;

namespace SeoBYP.TimelineAnimation.Editor
{
    /// <summary>The scroll helpers' inspectors: their fields as usual, and every state as the same Edit / Create row.</summary>
    internal abstract class StateFieldsEditor : UnityEditor.Editor
    {
        protected abstract (string field, string ownerField, string newName)[] States { get; }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var states = States;
            var excluded = new string[states.Length + 1];
            excluded[0] = "m_Script";
            for (var i = 0; i < states.Length; i++) excluded[i + 1] = states[i].field;
            DrawPropertiesExcluding(serializedObject, excluded);
            foreach (var (field, ownerField, newName) in states)
            {
                var property = serializedObject.FindProperty(field);
                var owner = serializedObject.FindProperty(ownerField).objectReferenceValue as Component;
                UIAnimStateField.Draw(property, new GUIContent(property.displayName, property.tooltip), owner, newName);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(UIAnimPageIndicator))]
    internal sealed class UIAnimPageIndicatorEditor : StateFieldsEditor
    {
        protected override (string, string, string)[] States => new[]
        {
            (nameof(UIAnimPageIndicator.onState), nameof(UIAnimPageIndicator.itemPrefab), "On"),
            (nameof(UIAnimPageIndicator.offState), nameof(UIAnimPageIndicator.itemPrefab), "Off"),
        };
    }

    [CustomEditor(typeof(UIAnimScrollScrub))]
    internal sealed class UIAnimScrollScrubEditor : StateFieldsEditor
    {
        protected override (string, string, string)[] States => new[]
        {
            (nameof(UIAnimScrollScrub.state), nameof(UIAnimScrollScrub.target), "Scroll"),
        };
    }

    [CustomEditor(typeof(UIAnimPullToRefresh))]
    internal sealed class UIAnimPullToRefreshEditor : StateFieldsEditor
    {
        protected override (string, string, string)[] States => new[]
        {
            (nameof(UIAnimPullToRefresh.pullState), nameof(UIAnimPullToRefresh.target), "Pull"),
            (nameof(UIAnimPullToRefresh.refreshingState), nameof(UIAnimPullToRefresh.target), "Refreshing"),
        };
    }
}
