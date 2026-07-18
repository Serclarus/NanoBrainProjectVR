using UnityEngine;
using UnityEngine.XR;

public class PCDashboardToggle : MonoBehaviour
{
    void Start()
    {
        // XRSettings.isDeviceActive returns true ONLY if a physical VR headset is plugged in
        // and currently active (running Oculus/SteamVR/OpenXR).
        if (XRSettings.isDeviceActive)
        {
            Debug.Log("<color=green>[PCDashboardToggle]</color> VR Headset detected! Disabling the PC Operator Dashboard UI.");
            gameObject.SetActive(false);
        }
        else
        {
            Debug.Log("<color=cyan>[PCDashboardToggle]</color> No VR Headset detected. Activating PC Operator Dashboard UI.");
            gameObject.SetActive(true);
        }
    }
}
