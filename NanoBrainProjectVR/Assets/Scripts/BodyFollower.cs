using UnityEngine;

public class BodyFollower : MonoBehaviour
{
    [Tooltip("Assign the Main Camera (the VR Headset) here")]
    public Transform head;

    [Tooltip("How far down from the headset should the belt sit? (Usually ~0.6m)")]
    public float bodyHeightOffset = 0.6f;

    [Tooltip("How fast the belt rotates to face the direction you are looking. High values are snappy, low values drag smoothly.")]
    public float rotationSmoothness = 5f;

    private void Start()
    {
        if (head == null)
        {
            if (Camera.main != null) head = Camera.main.transform;
            else Debug.LogWarning("BodyFollower: No head transform assigned and no Main Camera found!");
        }
    }

    private void Update()
    {
        if (head == null) return;

        // 1. Position follows X and Z of the head exactly, but Y is forced to the chest/hip height.
        Vector3 targetPosition = new Vector3(head.position.x, head.position.y - bodyHeightOffset, head.position.z);
        transform.position = targetPosition;

        // 2. We only want the yaw (left/right rotation), not the pitch (looking up/down).
        Vector3 headForward = head.forward;
        headForward.y = 0; // Flatten the vector to the floor

        if (headForward.sqrMagnitude > 0.01f) // Prevent error if looking straight UP/DOWN
        {
            Quaternion targetRotation = Quaternion.LookRotation(headForward);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothness);
        }
    }
}
