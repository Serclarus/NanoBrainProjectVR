using UnityEngine;
using UnityEditor;

public class FixWeaponPrefab
{
    [MenuItem("Tools/Fix Weapon Spawning Prefab")]
    public static void FixPrefab()
    {
        string prefabPath = "Assets/VRMPAssets/Prefabs/PrefabVariants/XR Origin Hands (XR Rig) MP Template Variant.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            Debug.LogError("Could not find the XR Origin Hands MP Variant prefab at " + prefabPath);
            return;
        }

        // Add NetworkPlayerLoadout to the root object
        NetworkPlayerLoadout loadout = prefab.GetComponent<NetworkPlayerLoadout>();
        if (loadout == null)
        {
            loadout = prefab.AddComponent<NetworkPlayerLoadout>();
        }

        // Find the old PlayerWeaponSpawner and copy the prefabs over!
        PlayerWeaponSpawner[] oldSpawners = prefab.GetComponentsInChildren<PlayerWeaponSpawner>(true);
        foreach (var spawner in oldSpawners)
        {
            if (spawner.shotgunPrefab != null) loadout.shotgunPrefab = spawner.shotgunPrefab;
            if (spawner.riflePrefab != null) loadout.riflePrefab = spawner.riflePrefab;
            if (spawner.pistolPrefab != null) loadout.pistolPrefab = spawner.pistolPrefab;
            
            // Delete the old spawner
            Object.DestroyImmediate(spawner, true);
        }

        EditorUtility.SetDirty(prefab);
        PrefabUtility.SavePrefabAsset(prefab);
        
        Debug.Log("<color=green>SUCCESS:</color> Fixed the XR Origin Hands MP Variant prefab! Weapons transferred and old script deleted!");
    }
}
