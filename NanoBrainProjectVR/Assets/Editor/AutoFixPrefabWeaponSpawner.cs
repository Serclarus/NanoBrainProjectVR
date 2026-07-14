using UnityEngine;
using UnityEditor;

public class AutoFixPrefabWeaponSpawner
{
    [MenuItem("Tools/Auto Fix VR Prefab Spawner")]
    public static void FixPrefab()
    {
        string path = "Assets/VRMPAssets/Prefabs/PrefabVariants/XR Origin Hands (XR Rig) MP Template Variant.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        
        if (prefab == null) 
        {
            Debug.LogError("Could not find the VR prefab at " + path);
            return;
        }

        // Get the old spawner
        PlayerWeaponSpawner oldSpawner = prefab.GetComponent<PlayerWeaponSpawner>();
        
        GameObject rifle = null;
        GameObject shotgun = null;
        GameObject pistol = null;

        if (oldSpawner != null) 
        {
            rifle = oldSpawner.riflePrefab;
            shotgun = oldSpawner.shotgunPrefab;
            pistol = oldSpawner.pistolPrefab;
            
            // Remove the old script
            Object.DestroyImmediate(oldSpawner, true);
            Debug.Log("Removed the old PlayerWeaponSpawner script.");
        }

        // Add the new loadout script
        NetworkPlayerLoadout loadout = prefab.GetComponent<NetworkPlayerLoadout>();
        if (loadout == null) 
        {
            loadout = prefab.AddComponent<NetworkPlayerLoadout>();
            Debug.Log("Added the new NetworkPlayerLoadout script.");
        }

        // Reassign the prefabs so the user doesn't have to manually drag them!
        if (rifle != null) loadout.riflePrefab = rifle;
        if (shotgun != null) loadout.shotgunPrefab = shotgun;
        if (pistol != null) loadout.pistolPrefab = pistol;

        EditorUtility.SetDirty(prefab);
        PrefabUtility.SavePrefabAsset(prefab);
        Debug.Log("<color=green>SUCCESS:</color> The VR Prefab has been successfully upgraded to the new NetworkPlayerLoadout system!");
    }
}
