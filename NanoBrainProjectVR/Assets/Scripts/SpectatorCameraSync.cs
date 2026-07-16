using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Attached to the unparented SpectatorCamera on the PC build.
/// It searches the network for an active VRCameraNetworkSender and smoothly moves the camera to match.
/// </summary>
public class SpectatorCameraSync : MonoBehaviour
{
    private VRCameraNetworkSender vrTarget;

    private void LateUpdate()
    {
        // 1. If we don't have a target, search for one
        if (vrTarget == null)
        {
            FindVRTarget();
            if (vrTarget == null) return; // Still no target, do nothing
        }

        // 2. If the target was destroyed (e.g. scene load or disconnect), clear it and wait
        if (!vrTarget.IsSpawned)
        {
            vrTarget = null;
            return;
        }

        // 3. Sync position and rotation exactly
        transform.position = vrTarget.vrHeadPosition.Value;
        transform.rotation = vrTarget.vrHeadRotation.Value;
    }

    private void FindVRTarget()
    {
        var allSenders = FindObjectsByType<VRCameraNetworkSender>(FindObjectsSortMode.None);
        foreach (var sender in allSenders)
        {
            // We want to find the sender that belongs to the VR user.
            var loadout = sender.GetComponent<NetworkPlayerLoadout>();
            if (loadout != null && sender.IsSpawned && loadout.isVRUser.Value)
            {
                vrTarget = sender;
                Debug.Log($"<color=cyan>[SpectatorCameraSync]</color> Found VR target to spectate: Client {sender.OwnerClientId}");
                break;
            }
        }
    }
}
