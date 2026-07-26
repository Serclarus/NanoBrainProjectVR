using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class RandomAudioPitch : MonoBehaviour
{
    [Tooltip("Minimum and Maximum Pitch range to pick from randomly")]
    public Vector2 pitchRange = new Vector2(0.85f, 1.15f);
    
    [Tooltip("If true, optimizes 3D audio settings so effects remain audible at a distance in VR")]
    public bool optimizeForVR = true;

    private AudioSource audioSource;
    private static Dictionary<AudioClip, float> lastPlayedTime = new Dictionary<AudioClip, float>();

    private void Awake()
    {
        // Cache the reference to the AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource != null && optimizeForVR)
        {
            // Give hit effects higher priority (64 vs default 128) so VR audio engines don't cull them
            audioSource.priority = 64;
            // Increase minimum distance so sound doesn't drop off too rapidly across rooms/ranges
            audioSource.minDistance = Mathf.Max(audioSource.minDistance, 5f);
            audioSource.maxDistance = Mathf.Max(audioSource.maxDistance, 50f);
            // Use 80% 3D spatial blend so positional audio works while keeping 20% 2D presence
            audioSource.spatialBlend = 0.8f;
        }
    }

    private void OnEnable()
    {
        // OnEnable is perfect for Object Pooling because it runs every time the 
        // effect is spawned (reactivated) by the HitEffectPoolManager.
        if (audioSource != null && audioSource.clip != null)
        {
            // Prevent VR audio voice exhaustion & phase cancellation when multiple shotgun pellets hit the same target in the same frame
            if (lastPlayedTime.TryGetValue(audioSource.clip, out float lastTime))
            {
                if (Time.unscaledTime - lastTime < 0.02f)
                {
                    return; // Skip duplicate overlapping hit sound in the exact same frame/millisecond
                }
            }
            lastPlayedTime[audioSource.clip] = Time.unscaledTime;

            // Randomize the pitch
            audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
            
            // Explicitly play it!
            audioSource.Play();
        }
    }
}
