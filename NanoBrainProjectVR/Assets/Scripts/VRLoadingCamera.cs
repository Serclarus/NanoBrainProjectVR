using UnityEngine;
using Unity.Netcode;
using TMPro;

public class VRLoadingCamera : MonoBehaviour
{
    [Tooltip("The camera used to show the loading screen in the VR headset.")]
    public Camera loadingCamera;

    [Tooltip("Optional: Text element to update with the connection status.")]
    public TMP_Text statusText;

    private void Start()
    {
        if (loadingCamera == null)
            loadingCamera = GetComponent<Camera>();

        // Ensure this camera renders to both eyes in VR
        if (loadingCamera != null)
        {
            loadingCamera.stereoTargetEye = StereoTargetEyeMask.Both;
        }

        if (statusText != null)
        {
            statusText.text = "Waiting for Connection...";
        }
    }

    private void Update()
    {
        // If the NetworkManager doesn't exist yet, do nothing
        if (NetworkManager.Singleton == null) return;

        // If we are actively trying to connect, update the text
        if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsConnectedClient)
        {
            if (statusText != null)
                statusText.text = "Connecting to Server...";
        }

        // The moment we successfully connect (as Host or Client), destroy this loading camera!
        if (NetworkManager.Singleton.IsConnectedClient || NetworkManager.Singleton.IsServer)
        {
            Debug.Log("<color=green>[VRLoadingCamera]</color> Connection successful! Destroying loading camera to make way for the player.");
            Destroy(gameObject);
        }
    }
}
