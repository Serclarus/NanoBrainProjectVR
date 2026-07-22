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

            // CRITICAL FIX: The duplicate XR Origin has InputActionManager component(s) that reference the global/shared InputActionAssets.
            // If we just Destroy(gameObject), the duplicate's OnDisable() will call DisableInput() and disable the global input actions,
            // which completely kills tracking/grabbing for the surviving instance.
            // To prevent this, we set the duplicate's actionAssets list to empty before destroying it.
            var duplicateManagers = GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>(true);
            foreach (var manager in duplicateManagers)
            {
                try
                {
                    manager.actionAssets = new System.Collections.Generic.List<UnityEngine.InputSystem.InputActionAsset>();
                    Debug.Log($"<color=orange>[XROriginPersist]</color> Cleared InputActionAssets from duplicate manager '{manager.name}' to prevent disabling input globally.");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"<color=red>[XROriginPersist]</color> Failed to clear actionAssets on duplicate: {e.Message}");
                }
            }

            Debug.Log($"<color=orange>[XROriginPersist]</color> Teleported surviving XR Origin to {transform.position}. Destroying duplicate {gameObject.name}.");
            Destroy(gameObject);

            // Double check: ensure the surviving instance's InputActionManagers are enabled and running
            var survivingManagers = instance.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>(true);
            foreach (var manager in survivingManagers)
            {
                try
                {
                    manager.EnableInput();
                    Debug.Log($"<color=green>[XROriginPersist]</color> Explicitly re-enabled input on surviving manager '{manager.name}'");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"<color=red>[XROriginPersist]</color> Failed to enable input on surviving manager: {e.Message}");
                }
            }

            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log($"<color=green>[XROriginPersist]</color> {gameObject.name} initialized and set to persist across scenes!");
    }
}
