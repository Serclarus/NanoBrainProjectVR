using UnityEngine;

/// <summary>
/// Attach this to your XR Origin or VR Rig.
/// It ensures that your player body, hands, and holsters survive scene transitions,
/// and destroys any duplicate VR Rigs baked into the new scenes you load into.
/// </summary>
public class XROriginPersist : MonoBehaviour
{
    private static XROriginPersist instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // Teleport the surviving XR Origin to exactly where this new (duplicate) one was placed in the scene!
            instance.transform.position = transform.position;
            instance.transform.rotation = transform.rotation;

            Debug.Log($"<color=orange>[XROriginPersist]</color> Teleported surviving XR Origin to {transform.position}. Destroying duplicate {gameObject.name}.");
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log($"<color=green>[XROriginPersist]</color> {gameObject.name} initialized and set to persist across scenes!");
    }
}
