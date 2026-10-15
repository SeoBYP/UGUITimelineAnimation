using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SeoBYP.TimelineAnimation.Tests
{
    /// <summary>
    /// Unity saves a component in a scene or prefab only if its class has a script file of the same name. A component
    /// class in another file still works when added at runtime, but is saved as "missing script".
    /// </summary>
    public sealed class ComponentScriptTests
    {
        [Test]
        public void 패키지의_모든_컴포넌트는_같은_이름의_스크립트_파일에_있다()
        {
            var components = typeof(UIAnimation).Assembly.GetTypes()
                .Where(t => typeof(MonoBehaviour).IsAssignableFrom(t) && !t.IsAbstract && !t.IsNested).ToList();
            var scripts = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/UIAnimation" })
                .Select(g => AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g)))
                .Select(s => s.GetClass()).Where(c => c != null).ToHashSet();
            var orphans = components.Where(t => !scripts.Contains(t)).Select(t => t.Name).ToList();
            TestContext.WriteLine($"컴포넌트 {components.Count}개: {string.Join(", ", components.Select(t => t.Name))}");
            CollectionAssert.IsEmpty(orphans, "스크립트 파일 이름과 클래스 이름이 다른 컴포넌트");
        }
    }
}
