using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class PlayerHolsterSystem : MonoBehaviour
{
    [Tooltip("Drag your Main Camera here")]
    public Transform cameraTransform;

    [Header("Holster Sockets")]
    public XRSocketInteractor leftShoulderSocket;
    public XRSocketInteractor rightShoulderSocket;
    public XRSocketInteractor rightBeltSocket;

    [Header("Holster Offsets")]
    public Vector3 leftShoulderOffset = new Vector3(-0.2f, -0.2f, -0.1f);
    public Vector3 rightShoulderOffset = new Vector3(0.2f, -0.2f, -0.1f);
    public Vector3 rightBeltOffset = new Vector3(0.25f, -0.6f, 0.1f);

    private void Start()
    {
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void Update()
    {
        if (cameraTransform == null) return;

        // Calculate a perfectly flat body rotation (so holsters don't tilt up when you look at the sky)
        Vector3 forwardFlat = cameraTransform.forward;
        forwardFlat.y = 0;
        if (forwardFlat.sqrMagnitude > 0.001f)
        {
            forwardFlat.Normalize();
        }
        else
        {
            forwardFlat = Vector3.forward;
        }
        
        Quaternion bodyRotation = Quaternion.LookRotation(forwardFlat);

        // Update Shoulder positions (shoulders tilt slightly with the head)
        if (leftShoulderSocket != null)
        {
            leftShoulderSocket.transform.position = cameraTransform.TransformPoint(leftShoulderOffset);
            leftShoulderSocket.transform.rotation = cameraTransform.rotation;
        }

        if (rightShoulderSocket != null)
        {
            rightShoulderSocket.transform.position = cameraTransform.TransformPoint(rightShoulderOffset);
            rightShoulderSocket.transform.rotation = cameraTransform.rotation;
        }

        // Update Belt position (belt stays perfectly flat on your waist)
        if (rightBeltSocket != null)
        {
            rightBeltSocket.transform.position = cameraTransform.position + (bodyRotation * rightBeltOffset);
            rightBeltSocket.transform.rotation = bodyRotation;
        }
    }
}
