using Unity.Netcode;
using UnityEngine;

public class OperatorClientListener : NetworkBehaviour
{


    public NetworkVariable<Unity.Collections.FixedString32Bytes> requestedScene = new NetworkVariable<Unity.Collections.FixedString32Bytes>(
        "",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        // Listen for scene change requests from the VR Client (executed on the Server)
        if (IsServer)
        {
            requestedScene.OnValueChanged += OnSceneRequested;
        }

        if (IsOwner)
        {
            // Register listeners for the Custom Messaging commands from the PC Operator
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("OperatorCommand_RefillMags", OnRefillMagsReceived);
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("OperatorCommand_LoadShotgun", OnLoadShotgunReceived);
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("OperatorCommand_TogglePause", OnTogglePauseReceived);
        }
    }

    private void OnSceneRequested(Unity.Collections.FixedString32Bytes previousValue, Unity.Collections.FixedString32Bytes newValue)
    {
        if (IsServer && !string.IsNullOrEmpty(newValue.ToString()))
        {
            Debug.Log($"<color=magenta>[OperatorClientListener]</color> VR Client requested scene change to: {newValue.ToString()} via NetworkVariable! Executing...");
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(newValue.ToString(), UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
            // Reset it so they can request it again later if needed
            requestedScene.Value = "";
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null)
        {
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("OperatorCommand_RefillMags");
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("OperatorCommand_LoadShotgun");
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("OperatorCommand_TogglePause");
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
}
