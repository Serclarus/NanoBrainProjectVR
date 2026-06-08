using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[Tooltip("Attach this to your Vest/BodyFollower object!")]
public class PlayerHolsterSystem : MonoBehaviour
{
    [Header("Holster Sockets")]
    [Tooltip("Drag your manually positioned Left Shoulder socket here")]
    public XRSocketInteractor leftShoulderSocket;
    
    [Tooltip("Drag your manually positioned Right Shoulder socket here")]
    public XRSocketInteractor rightShoulderSocket;
    
    [Tooltip("Drag your manually positioned Right Belt socket here")]
    public XRSocketInteractor rightBeltSocket;

    [Header("Debugging")]
    public bool printDebugLogs = true;
    private float debugTimer = 0f;

    private void Update()
    {
        // We no longer forcefully calculate socket positions here! 
        // We assume the Sockets are children of your Vest (BodyFollower) and you positioned them manually.

        if (printDebugLogs)
        {
            debugTimer -= Time.deltaTime;
            if (debugTimer <= 0f)
            {
                debugTimer = 3f; // Print every 3 seconds
                LogSocketContents();
            }
        }
    }

    private void LogSocketContents()
    {
        string left = (leftShoulderSocket != null && leftShoulderSocket.hasSelection) ? leftShoulderSocket.interactablesSelected[0].transform.name : "Empty";
        string right = (rightShoulderSocket != null && rightShoulderSocket.hasSelection) ? rightShoulderSocket.interactablesSelected[0].transform.name : "Empty";
        string belt = (rightBeltSocket != null && rightBeltSocket.hasSelection) ? rightBeltSocket.interactablesSelected[0].transform.name : "Empty";

        Debug.Log($"<color=cyan>[Holster System]</color> Left Shoulder: <b>{left}</b> | Right Shoulder: <b>{right}</b> | Belt: <b>{belt}</b>");
    }
}
