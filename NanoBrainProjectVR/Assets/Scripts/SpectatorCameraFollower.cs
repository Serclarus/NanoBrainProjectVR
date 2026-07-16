using UnityEngine;

/// <summary>
/// A standalone script that searches for the "SpectatorCamera" and syncs it to the VR player's networked head transform.
/// This runs on the PC build to ensure the PC operator's view matches the VR player's view seamlessly.
/// </summary>
public class SpectatorCameraFollower : MonoBehaviour
{
    private Transform spectatorCamera;
    private NetworkPlayerLoadout vrLoadout;

    private void LateUpdate()
    {
        // 1. Find the detached SpectatorCamera if we haven't already
        if (spectatorCamera == null)
        {
            GameObject specObj = GameObject.FindGameObjectWithTag("SpectatorCamera");
            if (specObj != null) spectatorCamera = specObj.transform;
            else return; // If it doesn't exist, we do nothing.
        }

        // 2. Find the VR player's loadout to read their network variables
        if (vrLoadout == null)
        {
            var allLoadouts = FindObjectsByType<NetworkPlayerLoadout>(FindObjectsSortMode.None);
            foreach (var loadout in allLoadouts)
            {
                // The VR player is the one where isVRUser is true
                if (loadout.isVRUser.Value)
                {
                    vrLoadout = loadout;
                    break;
                }
            }
        }

        // 3. Sync the SpectatorCamera to the VR player's networked head position
        // Since TrackedPoseDriver runs in Update and BeforeRender, LateUpdate is the perfect
        // time to catch the finalized network values for this frame and apply them to the PC camera.
        if (vrLoadout != null)
        {
            spectatorCamera.position = vrLoadout.vrHeadPosition.Value;
            spectatorCamera.rotation = vrLoadout.vrHeadRotation.Value;
        }
    }
}
