using UnityEngine;

public class SnapToGround : MonoBehaviour
{
    [Tooltip("How far down to search for the floor")]
    public float searchDistance = 10f;
    
    [Tooltip("Which layers count as the floor? (Set to your Terrain/Environment layer)")]
    public LayerMask groundLayer = Physics.DefaultRaycastLayers;

    private void Awake()
    {
        Snap();
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Snap();
    }

    public void Snap()
    {
        // Start the raycast slightly above the player's current position to ensure we don't start inside the floor
        Vector3 startPos = transform.position + Vector3.up * 2f;
        
        if (Physics.Raycast(startPos, Vector3.down, out RaycastHit hit, searchDistance + 2f, groundLayer))
        {
            // Move the XR Origin so its exact bottom pivot is resting perfectly on the hit point
            transform.position = hit.point;
        }
    }
}
