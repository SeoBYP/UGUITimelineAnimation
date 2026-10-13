using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SeoBYP.TimelineAnimation.Editor
{
    [CustomEditor(typeof(UIAnimation))]
    internal sealed class UIAnimationInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new Button(() => UIAnimationEditorWindow.Open((UIAnimation)target))
            {
                text = "UI Animation Editor 열기",
                style = { height = 26, marginBottom = 6 },
            });
            var raw = new Foldout { text = "원본 데이터", value = false };
            InspectorElement.FillDefaultInspector(raw, serializedObject, this);
            root.Add(raw);
            return root;
        }
    }
}
