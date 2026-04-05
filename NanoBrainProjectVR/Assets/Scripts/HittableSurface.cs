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
    [Header("Surface Look & Feel")]
    [Tooltip("What kind of particle effect should spawn when shot?")]
    public SurfaceType surfaceType = SurfaceType.Default;

    [Header("Gameplay & Scoring")]
    [Tooltip("If 0, this acts as a plain wall. If greater than 0, it acts as a scoring target!")]
    public int pointsAwarded = 0;
    
    // You can add health variables here later when we make the Boars
    
    public void OnHit()
    {
        // Only award points if it is meant to be a scoring target
        if (pointsAwarded > 0)
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(pointsAwarded);
            }
        }
    }
}
