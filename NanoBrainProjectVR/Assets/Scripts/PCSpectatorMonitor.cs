using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class PCSpectatorMonitor : MonoBehaviour
{
    [Header("UI Output")]
    [Tooltip("The RawImage on your PC Dashboard Canvas that will display the camera feed.")]
    public RawImage dashboardMonitor;

    private Transform targetVRHead;
    private RenderTexture videoFeed; // Hard reference to prevent Garbage Collection!
    private Camera spectatorCamera;

    void Start()
    {
        // Force the PC build to keep running and rendering even if you click away to another window!
        Application.runInBackground = true;

        spectatorCamera = GetComponent<Camera>();

        // If we are outputting to a UI screen, we need a RenderTexture
        if (dashboardMonitor != null && spectatorCamera != null)
        {
            videoFeed = new RenderTexture(1920, 1080, 24);
            videoFeed.name = "VR_Spectator_RT";
            spectatorCamera.targetTexture = videoFeed;
            dashboardMonitor.texture = videoFeed;
        }
    }

    void LateUpdate()
    {
        // If the VR player disconnected, targetVRHead will be implicitly null (destroyed). 
        // We must search for a new one.
        if (targetVRHead == null)
        {
            FindVRPlayer();
            return;
        }

        // Lock the PC camera to the VR player's exact head position and rotation
        if (spectatorCamera != null)
        {
            spectatorCamera.transform.position = targetVRHead.position;
            spectatorCamera.transform.rotation = targetVRHead.rotation;
        }
    }

    private void FindVRPlayer()
    {
        // Search through all connected players to find the VR host/client
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObj = client.PlayerObject;
            if (playerObj != null)
            {
                var playerLoadout = playerObj.GetComponent<NetworkPlayerLoadout>();
                if (playerLoadout != null && playerLoadout.isVRUser.Value)
                {
                    // Directly grab the transform the user assigned in the inspector!
                    if (playerLoadout.spectatorHeadSource != null)
                    {
                        targetVRHead = playerLoadout.spectatorHeadSource;
                        Debug.Log("<color=cyan>[PCSpectator]</color> Found VR Player's Spectator Head Source! Locking on.");
                        return;
                    }
                }
            }
        }
    }
}
