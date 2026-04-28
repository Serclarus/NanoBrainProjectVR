using UnityEngine;

public class BodyFollower : MonoBehaviour
{
    [Header("Tracking Targets")]
    [Tooltip("Assign the Main Camera (the VR Headset) here")]
    public Transform head;
    
    [Tooltip("Assign the Left Controller here (Optional but Recommended)")]
    public Transform leftHand;

    [Tooltip("Assign the Right Controller here (Optional but Recommended)")]
    public Transform rightHand;

    [Header("Positioning")]
    [Tooltip("How far down from the headset should the belt sit? (Usually ~0.6m)")]
    public float bodyHeightOffset = 0.6f;

    [Tooltip("Side offset from the body center. Positive = right hip, Negative = left hip. (e.g. 0.2 for right hip)")]
    public float sideOffset = 0f;

    [Tooltip("Forward/backward offset from the body center. Positive = in front, Negative = behind. (e.g. -0.1 for slightly behind)")]
    public float forwardOffset = 0f;

    [Header("Rotation Settings")]
    [Tooltip("How fast the belt rotates to face the direction you are looking. High values are snappy, low values drag smoothly.")]
    public float rotationSmoothness = 5f;

    [Tooltip("How much influence the Head's direction has on the chest rotation (0 to 1). Highly recommended to keep this at 1.")]
    [Range(0f, 1f)] public float headWeight = 1f;

    [Tooltip("How much influence the Hands' positions have on the chest rotation (0 to 1). If you turn sideways to aim a rifle, your hands will pull the belt sideways with you!")]
    [Range(0f, 1f)] public float handsWeight = 0.7f;

    [Tooltip("How far the body can twist left or right before being dragged (Deadzone in degrees)")]
    public float bodyTurnDeadzone = 30f;

    private float currentBodyYaw;

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
        Vector3 centerPosition = new Vector3(head.position.x, head.position.y - bodyHeightOffset, head.position.z);

        // Apply side and forward offsets relative to the body's current facing direction
        Vector3 right = transform.right * sideOffset;
        Vector3 forward = transform.forward * forwardOffset;
        transform.position = centerPosition + right + forward;

        // Keep a reference for the hand direction calculations below
        Vector3 targetPosition = centerPosition;

        // 2. Calculate the estimated "Forward" direction using a blend of Head and Hands
        Vector3 headForward = head.forward;
        headForward.y = 0; // Flatten the vector to the floor

        Vector3 combinedForward = headForward * headWeight;

        // If hands are assigned, offset the forward direction based on where the hands are physically held
        if (leftHand != null && rightHand != null)
        {
            // Vector pointing from the center of the chest to the hands
            Vector3 leftHandDir = leftHand.position - targetPosition;
            Vector3 rightHandDir = rightHand.position - targetPosition;

            // Average hand direction
            Vector3 averageHandDir = (leftHandDir + rightHandDir).normalized;
            averageHandDir.y = 0; // Flatten to floor

            // Blend the head and hand directions together mathematically
            combinedForward = (combinedForward + (averageHandDir * handsWeight)).normalized;
        }

        // 3. Apply Deadzone logic to the newly blended specific forward direction
        if (combinedForward.sqrMagnitude > 0.01f) // Prevent error if looking straight UP/DOWN
        {
            float targetYaw = Quaternion.LookRotation(combinedForward.normalized).eulerAngles.y;

            // Calculate the difference between where the calculated body is looking and where it currently is pointing
            float yawDifference = Mathf.DeltaAngle(currentBodyYaw, targetYaw);

            // If the estimated direction turns further than the deadzone, drag the body along with it
            if (Mathf.Abs(yawDifference) > bodyTurnDeadzone)
            {
                if (yawDifference > 0)
                {
                    currentBodyYaw = targetYaw - bodyTurnDeadzone;
                }
                else
                {
                    currentBodyYaw = targetYaw + bodyTurnDeadzone;
                }
            }

            // Smoothly rotate the actual transform to match the calculated body yaw
            Quaternion targetBodyRotation = Quaternion.Euler(0, currentBodyYaw, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetBodyRotation, Time.deltaTime * rotationSmoothness);
        }
    }
}
