using UnityEngine;
using Unity.Netcode;

public class NetworkHeadSync : NetworkBehaviour
{
    private Transform mainCamera;

    void Start()
    {
        // Find the local physical VR headset
        if (Camera.main != null)
        {
            mainCamera = Camera.main.transform;
        }
    }

    void Update()
    {
        // Only the owner of the avatar should read their local physical headset
        // and move this network anchor!
        if (IsOwner && mainCamera != null)
        {
            transform.position = mainCamera.position;
            transform.rotation = mainCamera.rotation;
        }
    }
}
