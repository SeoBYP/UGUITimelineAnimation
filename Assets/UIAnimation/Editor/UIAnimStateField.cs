using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SeoBYP.TimelineAnimation.Editor
{
    /// <summary>
    /// The one inspector row for "a state of that animation": a popup of its states and Edit / Create, which opens the
    /// state in the UI Animation Editor (a prefab asset opens in prefab mode first). Used by every component that names a state.
    /// </summary>
    internal static class UIAnimStateField
    {
        /// <param name="owner">The object that has (or gets, on Create) the UIAnimation: a prefab asset or a scene object.</param>
        public static void Draw(SerializedProperty property, GUIContent label, Component owner, string newName)
        {
            var anim = owner != null ? owner.GetComponent<UIAnimation>() : null;
            var states = anim != null ? anim.states.Select(s => s.name).ToList() : new List<string>();
            var options = new List<string> { "None" };
            options.AddRange(states);
            var current = property.stringValue;
            if (!string.IsNullOrEmpty(current) && !states.Contains(current)) options.Add(current);   // keep a name the animation lost
            var index = string.IsNullOrEmpty(current) ? 0 : options.IndexOf(current);

            using (new EditorGUILayout.HorizontalScope())
            {
                var picked = EditorGUILayout.Popup(label, index, options.Select(o => new GUIContent(o)).ToArray());
                if (picked != index) property.stringValue = picked == 0 ? "" : options[picked];
                var exists = states.Contains(property.stringValue);
                using (new EditorGUI.DisabledScope(owner == null))
                {
                    var tooltip = owner == null ? "Assign the object first"
                        : exists ? "Open this state in the UI Animation Editor"
                        : "Add this state and open it in the UI Animation Editor";
                    if (GUILayout.Button(new GUIContent(exists ? "Edit" : "Create", tooltip), EditorStyles.miniButton, GUILayout.Width(52)))
                    {
                        var name = !string.IsNullOrEmpty(property.stringValue) ? property.stringValue : Unique(states, newName);
                        property.stringValue = name;
                        property.serializedObject.ApplyModifiedProperties();
                        Edit(owner, name);
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        static string Unique(List<string> taken, string name)
        {
            var result = name;
            for (var n = 1; taken.Contains(result); n++) result = $"{name} {n}";
            return result;
        }

        /// <summary>Opens the UI Animation Editor on <paramref name="state"/> (added when missing). A prefab asset is opened in prefab mode.</summary>
        public static UIAnimation Edit(Component owner, string state) => UIAnimationEditorWindow.OpenState(owner, state, create: true);
    }
}
