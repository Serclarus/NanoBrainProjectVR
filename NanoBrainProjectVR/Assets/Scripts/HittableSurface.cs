using UnityEngine;

// We moved the enum here to keep everything centralized
public enum SurfaceType
{
    Default,
    Metal,
    Concrete,
    Dirt,
    Flesh,
    Wood
}

// This script merged both what a surface is, and what happens when it gets shot.
public class HittableSurface : MonoBehaviour
{
    [System.Serializable]
    public struct ScoreColorMapping
    {
        [Tooltip("The color painted on the score map texture for this zone")]
        public Color zoneColor;
        [Tooltip("Points awarded for hitting this zone")]
        public int points;
        [Tooltip("Optional label for debugging (e.g. 'X', '10', '9')")]
        public string label;
    }

    [Header("Surface Look & Feel")]
    [Tooltip("What kind of particle effect should spawn when shot?")]
    public SurfaceType surfaceType = SurfaceType.Default;

    [Header("Gameplay & Scoring")]
    [Tooltip("If 0, this acts as a plain wall. If greater than 0, it acts as a scoring target!")]
    public int pointsAwarded = 0;

    [Header("Score Map Scoring (Optional)")]
    [Tooltip("A texture where each scoring zone is painted a distinct flat color. " +
             "Must have Read/Write enabled in the texture import settings! " +
             "The target must use a MeshCollider for UV lookup to work.")]
    public Texture2D scoreMap;

    [Tooltip("Map each color on the score map to a point value. " +
             "Colors are matched with a tolerance so you don't need pixel-perfect painting.")]
    public ScoreColorMapping[] colorMappings;

    [Tooltip("How close the sampled color needs to be to a mapping color to count as a match (0-1). " +
             "Lower = stricter matching. 0.15 works well for distinct colors.")]
    [Range(0.01f, 0.5f)]
    public float colorMatchTolerance = 0.15f;
    
    // You can add health variables here later when we make the Boars

    /// <summary>
    /// Legacy overload for backward compatibility (no hit position).
    /// Uses the flat pointsAwarded value.
    /// </summary>
    public void OnHit()
    {
        if (pointsAwarded > 0)
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(pointsAwarded);
            }
        }
    }

    /// <summary>
    /// Score map overload. If a scoreMap and colorMappings are configured,
    /// it samples the texture at the bullet's UV coordinate and looks up the score.
    /// Otherwise falls back to the flat pointsAwarded value.
    /// Requires the target to have a MeshCollider for textureCoord to work.
    /// </summary>
    public void OnHit(RaycastHit hit)
    {
        // If no score map is set up, fall back to the flat value
        if (scoreMap == null || colorMappings == null || colorMappings.Length == 0)
        {
            OnHit();
            return;
        }

        // Get the UV coordinate where the bullet hit
        Vector2 uv = hit.textureCoord;

        // Sample the score map texture at that UV
        int pixelX = Mathf.FloorToInt(uv.x * scoreMap.width);
        int pixelY = Mathf.FloorToInt(uv.y * scoreMap.height);
        pixelX = Mathf.Clamp(pixelX, 0, scoreMap.width - 1);
        pixelY = Mathf.Clamp(pixelY, 0, scoreMap.height - 1);

        Color sampledColor = scoreMap.GetPixel(pixelX, pixelY);

        // Find the closest matching color in our mappings
        int awardedPoints = 0;
        string zoneLabel = "Miss";

        for (int i = 0; i < colorMappings.Length; i++)
        {
            // Compare RGB channels (ignore alpha)
            float diff = Mathf.Abs(sampledColor.r - colorMappings[i].zoneColor.r)
                       + Mathf.Abs(sampledColor.g - colorMappings[i].zoneColor.g)
                       + Mathf.Abs(sampledColor.b - colorMappings[i].zoneColor.b);

            if (diff <= colorMatchTolerance)
            {
                awardedPoints = colorMappings[i].points;
                zoneLabel = string.IsNullOrEmpty(colorMappings[i].label) 
                    ? awardedPoints.ToString() 
                    : colorMappings[i].label;
                break;
            }
        }

        if (awardedPoints > 0 && ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddScore(awardedPoints);
            Debug.Log($"Target Hit! Zone: {zoneLabel} | Points: +{awardedPoints}");
        }
    }
}
