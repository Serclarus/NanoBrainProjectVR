using UnityEngine;
using Unity.Netcode;

public class MovingTarget : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("How far the target moves in each direction from its starting point")]
    public float moveDistance = 5f;
    
    [Tooltip("How fast the target moves")]
    public float moveSpeed = 2f;
    
    [Tooltip("The axis to move along. (0, 0, 1) is the Z axis!")]
    public Vector3 moveAxis = new Vector3(0, 0, 1);

    private Vector3 startPosition;
    private float internalTime = 0f;
    private KnockdownTarget knockdownTarget;

    private void Start()
    {
        // Remember exactly where we placed the target in the editor
        startPosition = transform.position;
        knockdownTarget = GetComponent<KnockdownTarget>();
    }

    private void Update()
    {
        // Safely check if the target is down
        bool targetIsDown = false;
        if (knockdownTarget != null)
        {
            try
            {
                if (knockdownTarget.IsSpawned)
                {
                    if (knockdownTarget.isDown.Value) targetIsDown = true;
                }
                else
                {
                    if (knockdownTarget.isLocallyDown) targetIsDown = true;
                }
            }
            catch { }
        }

        // If the target is knocked down, pause the timer so it freezes in place!
        if (targetIsDown)
        {
            return;
        }

        // Advance our own timer
        internalTime += Time.deltaTime;

        // Mathf.Sin smoothly ramps up and down between -1 and 1
        float offset = Mathf.Sin(internalTime * moveSpeed) * moveDistance;
        
        // Apply the offset to the starting position along the chosen axis
        transform.position = startPosition + (moveAxis.normalized * offset);
    }
}
