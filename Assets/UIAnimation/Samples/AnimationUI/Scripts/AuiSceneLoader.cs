using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Unity-AnimationUI sample: loads a scene when an animation event asks for it (the original's LoadScene step).</summary>
public sealed class AuiSceneLoader : MonoBehaviour
{
    public void Load(string scene) => SceneManager.LoadScene(scene);
}
