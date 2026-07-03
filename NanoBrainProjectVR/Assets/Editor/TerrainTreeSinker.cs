using UnityEngine;
using UnityEditor;

public class TerrainTreeSinker : EditorWindow
{
    public Terrain targetTerrain;
    public float yOffsetAmount = -0.2f;

    [MenuItem("Tools/Terrain Tree Sinker")]
    public static void ShowWindow()
    {
        GetWindow<TerrainTreeSinker>("Sink Terrain Trees");
    }

    private void OnGUI()
    {
        GUILayout.Label("Fix Floating Trees on Slopes", EditorStyles.boldLabel);
        
        targetTerrain = (Terrain)EditorGUILayout.ObjectField("Target Terrain", targetTerrain, typeof(Terrain), true);
        yOffsetAmount = EditorGUILayout.FloatField("Y Offset Amount", yOffsetAmount);

        EditorGUILayout.HelpBox("This tool will push all trees on the terrain up or down by the specified amount. Use a negative number (e.g. -0.2) to sink them into the ground.", MessageType.Info);

        if (GUILayout.Button("Apply Offset to All Trees"))
        {
            if (targetTerrain == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a Terrain first!", "OK");
                return;
            }

            ApplyTreeOffset();
        }
    }

    private void ApplyTreeOffset()
    {
        TerrainData tData = targetTerrain.terrainData;
        Undo.RecordObject(tData, "Sink Terrain Trees");

        TreeInstance[] trees = tData.treeInstances;
        float terrainHeight = tData.size.y;

        // TreeInstance.position.y is normalized (0 to 1) based on the terrain's total height.
        // We must convert our absolute offset (-0.2 units) into normalized space.
        float normalizedOffset = yOffsetAmount / terrainHeight;

        for (int i = 0; i < trees.Length; i++)
        {
            trees[i].position.y += normalizedOffset;
        }

        tData.treeInstances = trees;
        targetTerrain.Flush();
        EditorUtility.SetDirty(tData);

        Debug.Log($"Successfully offset {trees.Length} trees by {yOffsetAmount} units on {targetTerrain.name}!");
    }
}
