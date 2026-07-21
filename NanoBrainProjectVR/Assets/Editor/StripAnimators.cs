using UnityEngine;
using UnityEditor;

public class StripAnimators
{
    public static void Run()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/BigBlit/ShootingRange/Demo/Prefabs/Targets" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            bool modified = false;

            var scriptableAnimators = prefab.GetComponentsInChildren<BigBlit.ShootingRange.ScriptableAnimator>(true);
            foreach (var sa in scriptableAnimators)
            {
                Object.DestroyImmediate(sa, true);
                modified = true;
            }

            var animators = prefab.GetComponentsInChildren<Animator>(true);
            foreach (var a in animators)
            {
                Object.DestroyImmediate(a, true);
                modified = true;
            }

            if (modified)
            {
                PrefabUtility.SavePrefabAsset(prefab);
                Debug.Log("Stripped Animators from: " + path);
            }
        }
    }
}
