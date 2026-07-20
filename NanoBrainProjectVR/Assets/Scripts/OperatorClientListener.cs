using Unity.Netcode;
using UnityEngine;

public class OperatorClientListener : NetworkBehaviour
{


    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Register listeners for the Custom Messaging commands from the PC Operator
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("OperatorCommand_RefillMags", OnRefillMagsReceived);
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("OperatorCommand_LoadShotgun", OnLoadShotgunReceived);
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("OperatorCommand_TogglePause", OnTogglePauseReceived);
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("OperatorCommand_FadeAndChangeScene", OnFadeAndChangeSceneReceived);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null)
        {
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("OperatorCommand_RefillMags");
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("OperatorCommand_LoadShotgun");
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("OperatorCommand_TogglePause");
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("OperatorCommand_FadeAndChangeScene");
        }
    }

    private void OnRefillMagsReceived(ulong senderId, FastBufferReader messagePayload)
    {
        Debug.Log($"<color=green>[OperatorClientListener]</color> Refill Mags command received!");

        // Find all WeaponControllers to see if a weapon is being held and has a magazine
        var weapons = FindObjectsOfType<WeaponController>();
        foreach (var weapon in weapons)
        {
            var grab = weapon.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            
            // If the player is currently holding this weapon, and it has a magazine inserted in its socket
            if (grab != null && grab.isSelected && weapon.magazineSocket != null && weapon.magazineSocket.hasSelection)
            {
                var magInteractable = weapon.magazineSocket.firstInteractableSelected;
                if (magInteractable != null)
                {
                    global::Magazine networkedMag = magInteractable.transform.GetComponent<global::Magazine>();
                    if (networkedMag != null)
                    {
                        networkedMag.Refill();
                        Debug.Log($"<color=green>[OperatorClientListener]</color> Refilled networked magazine in held weapon: {grab.gameObject.name}");
                    }
                }
            }
        }
    }

    private void OnLoadShotgunReceived(ulong senderId, FastBufferReader messagePayload)
    {
        Debug.Log($"<color=green>[OperatorClientListener]</color> Load Shotgun command received!");

        var shotguns = FindObjectsOfType<ShotgunController>();
        foreach (var sg in shotguns)
        {
            var grab = sg.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            
            // If the player is currently holding the shotgun
            if (grab != null && grab.isSelected)
            {
                sg.currentAmmo.Value = sg.maxAmmoCapacity;
                sg.chamberState.Value = ShotgunController.ChamberState.LiveRound;
                Debug.Log($"<color=green>[OperatorClientListener]</color> Fully loaded held shotgun: {sg.gameObject.name}");
            }
        }
    }

    private void OnTogglePauseReceived(ulong senderId, FastBufferReader messagePayload)
    {
        Debug.Log($"<color=green>[OperatorClientListener]</color> Toggle Pause command received!");

        if (Time.timeScale == 0f)
        {
            Time.timeScale = 1f;
            if (VRSceneFader.Instance != null) VRSceneFader.Instance.PausedVignette(false);
            Debug.Log($"<color=green>[OperatorClientListener]</color> Game Resumed.");
        }
        else
        {
            Time.timeScale = 0f;
            if (VRSceneFader.Instance != null) VRSceneFader.Instance.PausedVignette(true);
            Debug.Log($"<color=green>[OperatorClientListener]</color> Game Paused.");
        }
    }

    private void OnFadeAndChangeSceneReceived(ulong senderId, FastBufferReader messagePayload)
    {
        messagePayload.ReadValueSafe(out Unity.Collections.FixedString32Bytes sceneNameBytes);
        string sceneToLoad = sceneNameBytes.ToString();
        sceneToLoad = sceneToLoad.Trim('\0', ' ');

        Debug.Log($"<color=cyan>[OperatorClientListener]</color> Operator requested graceful scene change to: {sceneToLoad}");

        if (VRSceneFader.Instance != null)
        {
            VRSceneFader.Instance.FadeToScene(sceneToLoad);
        }
        else
        {
            // Fallback if no fader exists
            Debug.LogWarning($"<color=cyan>[OperatorClientListener]</color> VRSceneFader not found! Requesting raw network load...");
            
            FastBufferWriter writer = new FastBufferWriter(32, Unity.Collections.Allocator.Temp);
            using (writer)
            {
                writer.WriteValueSafe(sceneNameBytes);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("GlobalRequest_SceneChange", NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
            }
        }
    }
}
