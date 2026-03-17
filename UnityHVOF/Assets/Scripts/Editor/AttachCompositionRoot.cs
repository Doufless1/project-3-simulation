using UnityEngine;
using UnityEditor;
using HVOFSim.Presentation;

public class AttachCompositionRoot
{
    [MenuItem("HVOF Tools/Attach Composition Root")]
    public static void Attach()
    {
        var existing = Object.FindObjectOfType<CompositionRoot>();
        if (existing != null)
        {
            Debug.Log("CompositionRoot is already attached to: " + existing.gameObject.name);
            return;
        }

        var go = new GameObject("SimulationEntryPoint");
        go.AddComponent<CompositionRoot>();
        Debug.Log("Created SimulationEntryPoint and attached CompositionRoot.");
        
        // Mark scene as dirty so it saves
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }
}
