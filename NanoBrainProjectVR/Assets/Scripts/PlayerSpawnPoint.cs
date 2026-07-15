using UnityEngine;

public class PlayerSpawnPoint : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("If multiple spawn points exist, you can assign an index to group or sequence them.")]
    public int spawnIndex = 0;

    private void OnDrawGizmos()
    {
        // Draw a blue capsule representing the player spawn location in the Unity Editor
        Gizmos.color = Color.blue;
        Vector3 center = transform.position + Vector3.up * 1f;
        Gizmos.DrawWireCube(center, new Vector3(0.6f, 2.0f, 0.6f));
        
        // Draw a forward direction indicator
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position + Vector3.up * 1.5f, transform.forward * 0.5f);
    }
}
