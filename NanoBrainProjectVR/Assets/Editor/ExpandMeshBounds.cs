using UnityEngine;
using UnityEditor;

public class ExpandMeshBounds : Editor
{
    [MenuItem("Assets/Expand Mesh Bounds (Fix Shadow Popping)")]
    public static void ExpandBounds()
    {
        // Get the selected mesh in the Project window
        Mesh selectedMesh = Selection.activeObject as Mesh;

        // If they selected a GameObject/Prefab instead of the raw Mesh, try to find the mesh on it
        if (selectedMesh == null && Selection.activeGameObject != null)
        {
            MeshFilter filter = Selection.activeGameObject.GetComponentInChildren<MeshFilter>();
            if (filter != null)
            {
                selectedMesh = filter.sharedMesh;
            }
        }

        if (selectedMesh == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a 3D Mesh (or a Prefab with a MeshFilter) in your Project window first!", "OK");
            return;
        }

        // Expand the bounds by a massive amount (e.g., 5x bigger)
        Bounds newBounds = selectedMesh.bounds;
        newBounds.extents *= 5f; 
        selectedMesh.bounds = newBounds;

        // Save the changes to the asset
        EditorUtility.SetDirty(selectedMesh);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Success!", $"Successfully expanded the bounds of {selectedMesh.name}! Your painted terrain trees should no longer disappear when you look away.", "Awesome");
    }
}
