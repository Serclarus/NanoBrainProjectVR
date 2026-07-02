using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class TerrainTreeBulkAdder : EditorWindow
{
    private Terrain targetTerrain;
    private List<GameObject> treePrefabs = new List<GameObject>();
    private Vector2 scrollPos;

    [MenuItem("Tools/Terrain Bulk Tree Adder")]
    public static void ShowWindow()
    {
        GetWindow<TerrainTreeBulkAdder>("Bulk Add Trees");
    }

    private void OnGUI()
    {
        GUILayout.Label("Bulk Add Trees to Terrain", EditorStyles.boldLabel);

        targetTerrain = (Terrain)EditorGUILayout.ObjectField("Target Terrain", targetTerrain, typeof(Terrain), true);

        GUILayout.Space(10);
        GUILayout.Label("Drag and drop multiple Tree Prefabs below:");

        // Drag and drop area
        Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "Drag & Drop Tree Prefabs Here", EditorStyles.helpBox);
        HandleDragAndDrop(dropArea);

        GUILayout.Space(10);

        scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(150));
        for (int i = 0; i < treePrefabs.Count; i++)
        {
            GUILayout.BeginHorizontal();
            treePrefabs[i] = (GameObject)EditorGUILayout.ObjectField(treePrefabs[i], typeof(GameObject), false);
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                treePrefabs.RemoveAt(i);
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();

        if (GUILayout.Button("Clear List"))
        {
            treePrefabs.Clear();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Add to Terrain", GUILayout.Height(30)))
        {
            if (targetTerrain == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a Target Terrain first!", "OK");
                return;
            }

            if (treePrefabs.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", "No tree prefabs to add!", "OK");
                return;
            }

            BulkAddTrees();
        }
    }

    private void HandleDragAndDrop(Rect dropArea)
    {
        Event currentEvent = Event.current;
        EventType currentEventType = currentEvent.type;

        if (dropArea.Contains(currentEvent.mousePosition))
        {
            if (currentEventType == EventType.DragUpdated || currentEventType == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (currentEventType == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (Object draggedObject in DragAndDrop.objectReferences)
                    {
                        if (draggedObject is GameObject prefab)
                        {
                            if (!treePrefabs.Contains(prefab))
                            {
                                treePrefabs.Add(prefab);
                            }
                        }
                    }
                }
                currentEvent.Use();
            }
        }
    }

    private void BulkAddTrees()
    {
        Undo.RecordObject(targetTerrain.terrainData, "Bulk Add Trees");

        List<TreePrototype> newPrototypes = new List<TreePrototype>(targetTerrain.terrainData.treePrototypes);

        foreach (var prefab in treePrefabs)
        {
            if (prefab == null) continue;

            // Check if it already exists
            bool exists = false;
            foreach (var proto in newPrototypes)
            {
                if (proto.prefab == prefab)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                TreePrototype newProto = new TreePrototype();
                newProto.prefab = prefab;
                newPrototypes.Add(newProto);
            }
        }

        targetTerrain.terrainData.treePrototypes = newPrototypes.ToArray();
        targetTerrain.Flush();
        EditorUtility.SetDirty(targetTerrain.terrainData);
        
        Debug.Log($"Successfully added {treePrefabs.Count} trees to the terrain!");
        treePrefabs.Clear();
    }
}
