using UnityEditor;
using UnityEngine;

public class PivotFixer: EditorWindow
{
    // This creates a new button in the Unity top menu bar under "Tools"
    [MenuItem("Tools/Center Parent Pivot")]
    static void CenterPivot() 
    {
        // Loop through every object you currently have selected in the Hierarchy
        foreach (GameObject obj in Selection.gameObjects) 
        {
            // Find all visual meshes attached to this object or its children
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            
            // If there are no meshes, skip it to avoid errors
            if (renderers.Length == 0) 
            {
                Debug.LogWarning($"Skipped {obj.name}: No renderers found to calculate center.");
                continue;
            }

            // Register the action so you can undo it with Ctrl+Z
            Undo.RegisterFullObjectHierarchyUndo(obj, "Center Pivot");

            // Calculate the combined center point of all child meshes
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) 
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            // Store the children in an array and detach them from the parent
            Transform[] children = new Transform[obj.transform.childCount];
            for (int i = 0; i < children.Length; i++) 
            {
                children[i] = obj.transform.GetChild(i);
            }
            obj.transform.DetachChildren();

            // Move the parent's pivot to the exact center we calculated earlier
            obj.transform.position = bounds.center;

            // Reattach all the children back to the parent
            foreach (Transform child in children) 
            {
                child.SetParent(obj.transform);
            }
            
            Debug.Log($"Successfully centered the pivot for {obj.name}");
        }
    }
}
