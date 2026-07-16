using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Attached to the Player Avatar prefab. 
/// If this is the local VR player, it finds its own Main Camera and writes its position/rotation to NetworkVariables.
/// This acts as a dedicated network transform specifically for the nested VR camera.
/// </summary>
public class VRCameraNetworkSender : NetworkBehaviour
{
    public NetworkVariable<Vector3> vrHeadPosition = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<Quaternion> vrHeadRotation = new NetworkVariable<Quaternion>(Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private Transform localHeadTransform;
    private NetworkPlayerLoadout loadout;

    private void Awake()
    {
        loadout = GetComponent<NetworkPlayerLoadout>();
    }

    private void Update()
    {
        // Only the owner sends their camera data.
        // We also verify this is actually the VR user (not the PC operator).
        if (IsOwner && loadout != null && loadout.isVRUser.Value)
        {
            if (localHeadTransform == null)
            {
                // Try to find the camera the same way the old system did
                localHeadTransform = transform.Find("Camera Offset/Main Camera") ??
                                     transform.Find("Main Camera") ??
                                     transform.Find("Head") ??
                                     transform.Find("Camera Offset/Head") ??
                                     GetComponentInChildren<Camera>()?.transform;
            }

            if (localHeadTransform != null)
            {
                vrHeadPosition.Value = localHeadTransform.position;
                vrHeadRotation.Value = localHeadTransform.rotation;
            }
        }
    }
}
