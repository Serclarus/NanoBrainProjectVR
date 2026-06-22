using UnityEngine;

public class VisualSlopeAligner : MonoBehaviour
{
    [Tooltip("How fast the mesh adapts to the slope")]
    public float tiltSpeed = 8f;
    [Tooltip("How far down to cast the ray to find the ground")]
    public float rayDistance = 3f;
    [Tooltip("Offset up from the pivot to start the raycast")]
    public float rayHeightOffset = 1.0f;
    [Tooltip("Layers to consider as ground (Default: Everything)")]
    public LayerMask groundMask = ~0;

    // Use RaycastNonAlloc to completely eliminate GC Allocation Spikes (VR Hourglass freezes)!
    private static RaycastHit[] hitBuffer = new RaycastHit[10];

    private void Update()
    {
        // Safety check: This script MUST be a child of the NavMeshAgent root!
        if (transform.parent == null) return;

        Vector3 rayStart = transform.parent.position + Vector3.up * rayHeightOffset;

        // Draw a cyan debug line in the Unity Scene view so you can visually confirm it's shooting down
        Debug.DrawRay(rayStart, Vector3.down * rayDistance, Color.cyan);

        // Highly optimized raycast that generates ZERO garbage data
        int hitCount = Physics.RaycastNonAlloc(rayStart, Vector3.down, hitBuffer, rayDistance, groundMask, QueryTriggerInteraction.Ignore);
        
        bool foundGround = false;
        Vector3 groundNormal = Vector3.up;
        float closestDist = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];
            
            // We perfectly ignore the collider of this child mesh AND the parent capsule collider!
            if (!hit.collider.transform.IsChildOf(transform.parent) && hit.collider.gameObject != transform.parent.gameObject)
            {
                if (hit.distance < closestDist)
                {
                    closestDist = hit.distance;
                    groundNormal = hit.normal;
                    foundGround = true;
                    // Draw a green line showing the Normal it found!
                    Debug.DrawRay(hit.point, hit.normal * 2f, Color.green);
                }
            }
        }

        if (foundGround)
        {
            // The magic: Calculate a rotation that faces the exact same direction as the Parent Brain, but tilts to match the hill!
            Vector3 forwardDirection = transform.parent.forward;
            forwardDirection.y = 0; // Keep the heading purely horizontal to avoid weird spin glitches
            
            if (forwardDirection.sqrMagnitude < 0.01f) forwardDirection = transform.parent.forward;

            // Project the heading onto the slope to get the true "Nose" direction (pitched up or down)
            Vector3 noseDirection = Vector3.ProjectOnPlane(forwardDirection, groundNormal).normalized;

            // By forcing the Up vector to be Vector3.up (instead of groundNormal), 
            // Unity mathematically eliminates all sideways roll, leaving ONLY the forward/backward pitch!
            Quaternion targetRotation = Quaternion.LookRotation(noseDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * tiltSpeed);
        }
        else
        {
            // Fallback: If the boar jumps off a cliff, smoothly blend back to match the parent perfectly
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.identity, Time.deltaTime * tiltSpeed);
        }
    }
}
