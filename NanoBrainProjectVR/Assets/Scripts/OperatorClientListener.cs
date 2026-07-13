using Unity.Netcode;
using UnityEngine;

public class OperatorClientListener : NetworkBehaviour
{
    private GameObject pauseOverlay;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Register listeners for the Custom Messaging commands from the PC Operator
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("OperatorCommand_RefillMags", OnRefillMagsReceived);
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("OperatorCommand_LoadShotgun", OnLoadShotgunReceived);
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("OperatorCommand_TogglePause", OnTogglePauseReceived);
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

        // Find all MagazineAttachPoints to see if a weapon is being held and has a magazine
        var attachPoints = FindObjectsOfType<MikeNspired.XRIStarterKit.MagazineAttachPoint>();
        foreach (var point in attachPoints)
        {
            var grab = point.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            
            // If the player is currently holding this weapon, and it has a magazine inserted
            if (grab != null && grab.isSelected && point.Magazine != null)
            {
                point.Magazine.Refill();
                Debug.Log($"<color=green>[OperatorClientListener]</color> Refilled magazine in held weapon: {grab.gameObject.name}");
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
            if (pauseOverlay != null) pauseOverlay.SetActive(false);
            Debug.Log($"<color=green>[OperatorClientListener]</color> Game Resumed.");
        }
        else
        {
            Time.timeScale = 0f;
            if (pauseOverlay == null)
            {
                CreatePauseOverlay();
            }
            pauseOverlay.SetActive(true);
            Debug.Log($"<color=green>[OperatorClientListener]</color> Game Paused.");
        }
    }

    private void CreatePauseOverlay()
    {
        if (Camera.main == null) return;

        pauseOverlay = new GameObject("PauseOverlay");
        pauseOverlay.transform.SetParent(Camera.main.transform, false);
        pauseOverlay.transform.localPosition = new Vector3(0, 0, 0.5f); // 0.5 meters directly in front of the face
        
        Canvas canvas = pauseOverlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        RectTransform rt = pauseOverlay.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(2, 2);
        rt.localScale = new Vector3(0.001f, 0.001f, 0.001f);
        
        // 50% black vignette background
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(pauseOverlay.transform, false);
        UnityEngine.UI.Image img = bg.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0, 0, 0, 0.5f);
        bg.GetComponent<RectTransform>().sizeDelta = new Vector2(3000, 3000); // Massive enough to cover entire FOV
        
        // "PAUSED" Text
        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(pauseOverlay.transform, false);
        UnityEngine.UI.Text txt = txtObj.AddComponent<UnityEngine.UI.Text>();
        txt.text = "PAUSED";
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = 80;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(1000, 200);
    }
}
