using UnityEngine;
using UnityEditor;

public class ExpandMeshBounds : Editor
{
    [MenuItem("Assets/Expand Mesh Bounds (Fix Shadow Popping)")]
    public static void ExpandBounds()
    {
        int successCount = 0;

        // Loop through everything the user selected
        foreach (Object obj in Selection.objects)
        {
            Mesh selectedMesh = obj as Mesh;

            // If they selected a GameObject/Prefab instead of the raw Mesh, try to find the mesh on it
            if (selectedMesh == null && obj is GameObject go)
            {
                MeshFilter filter = go.GetComponentInChildren<MeshFilter>();
                if (filter != null)
                {
                    selectedMesh = filter.sharedMesh;
                }
            }

            // If we found a mesh, expand it!
            if (selectedMesh != null)
            {
                Bounds newBounds = selectedMesh.bounds;
                newBounds.extents *= 5f; 
                selectedMesh.bounds = newBounds;

                EditorUtility.SetDirty(selectedMesh);
                successCount++;
            }
        }

        if (successCount > 0)
        {
            // Save the changes to the assets
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Success!", $"Successfully expanded the bounds of {successCount} mesh(es)! Your painted terrain trees should no longer disappear when you look away.", "Awesome");
        }
        else
        {
            EditorUtility.DisplayDialog("Error", "Please select one or more 3D Meshes (or Prefabs with MeshFilters) in your Project window first!", "OK");
        }
    }
}
