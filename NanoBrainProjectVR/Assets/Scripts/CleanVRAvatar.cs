using UnityEngine;
using Unity.Netcode;
using Unity.XR.CoreUtils;

public class CleanVRAvatar : NetworkBehaviour
{
    [Header("Network Avatar Transforms")]
    [Tooltip("The Head object of this networked avatar")]
    public Transform avatarHead;
    
    [Tooltip("The Left Hand object of this networked avatar")]
    public Transform avatarLeftHand;
    
    [Tooltip("The Right Hand object of this networked avatar")]
    public Transform avatarRightHand;

    [Header("Visibility")]
    [Tooltip("Meshes to hide for the local player so they don't block vision (e.g. Head/Body models)")]
    public Renderer[] hideForLocalPlayer;

    // References to the actual physical VR rig
    private Transform localHead;
    private Transform localLeftHand;
    private Transform localRightHand;
    private Transform localOrigin;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            // Hide meshes so we don't see our own head model floating in front of us
            foreach (var rend in hideForLocalPlayer)
            {
                if (rend != null)
                {
                    rend.enabled = false;
                }
            }

            // Find the local XR Origin
            XROrigin xrOrigin = FindFirstObjectByType<XROrigin>();
            if (xrOrigin != null)
            {
                localOrigin = xrOrigin.transform;
                localHead = xrOrigin.Camera.transform;

                // Find the hands. In XRI 3.0, these are usually children of the Camera Offset.
                // We'll search by standard naming conventions, or fallback to finding XRControllers.
                Transform cameraOffset = xrOrigin.CameraFloorOffsetObject.transform;
                
                foreach (Transform child in cameraOffset)
                {
                    string name = child.name.ToLower();
                    if (name.Contains("left")) localLeftHand = child;
                    if (name.Contains("right")) localRightHand = child;
                }

                if (localLeftHand == null || localRightHand == null)
                {
                    Debug.LogWarning("CleanVRAvatar: Could not automatically detect Left or Right hand transforms on the XR Origin.");
                }
            }
            else
            {
                Debug.LogError("CleanVRAvatar: Could not find an XROrigin in the scene!");
            }
        }
    }

    private void Update()
    {
        // Only the Owner reads from their physical headset.
        // ClientNetworkTransform handles broadcasting this position to all other players.
        if (!IsOwner) return;

        if (localOrigin != null)
        {
            // Sync the root of the avatar to the XR Origin root
            transform.position = localOrigin.position;
            transform.rotation = localOrigin.rotation;
        }

        if (avatarHead != null && localHead != null)
        {
            avatarHead.position = localHead.position;
            avatarHead.rotation = localHead.rotation;
        }

        if (avatarLeftHand != null && localLeftHand != null)
        {
            avatarLeftHand.position = localLeftHand.position;
            avatarLeftHand.rotation = localLeftHand.rotation;
        }

        if (avatarRightHand != null && localRightHand != null)
        {
            avatarRightHand.position = localRightHand.position;
            avatarRightHand.rotation = localRightHand.rotation;
        }
    }
}
