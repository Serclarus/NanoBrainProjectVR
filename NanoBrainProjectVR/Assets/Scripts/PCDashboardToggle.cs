using UnityEngine;
using UnityEngine.XR;

public class PCDashboardToggle : MonoBehaviour
{
    [Tooltip("If true, playing in the Unity Editor will act like a VR headset is plugged in, turning this UI OFF.")]
    public bool forceVRInEditor = true;

    void Start()
    {
        // On Android (Quest), we are ALWAYS VR.
        bool isAndroid = (Application.platform == RuntimePlatform.Android);
        
        // In the Unity Editor, we act as VR if forceVRInEditor is true.
        bool isForcedEditorVR = (Application.isEditor && forceVRInEditor);
        
        // On PC, we check if a physical headset is actually running.
        bool isRealVR = XRSettings.isDeviceActive;

        if (isAndroid || isForcedEditorVR || isRealVR)
        {
            Debug.Log("<color=green>[PCDashboardToggle]</color> VR Mode active! Disabling the PC Operator Dashboard UI.");
            gameObject.SetActive(false);
        }
        else
        {
            Debug.Log("<color=cyan>[PCDashboardToggle]</color> PC Operator Mode active. Activating PC Operator Dashboard UI.");
            gameObject.SetActive(true);
        }
    }
}
