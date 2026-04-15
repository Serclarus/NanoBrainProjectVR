using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class RandomAudioPitch : MonoBehaviour
{
    [Tooltip("Minimum and Maximum Pitch range to pick from randomly")]
    public Vector2 pitchRange = new Vector2(0.85f, 1.15f);
    
    private AudioSource audioSource;

    private void Awake()
    {
        // Cache the reference to the AudioSource
        audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        // OnEnable is perfect for Object Pooling because it runs every time the 
        // effect is spawned (reactivated) by the HitEffectPoolManager.
        if (audioSource != null && audioSource.clip != null)
        {
            // Randomize the pitch
            audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
            
            // Explicitly play it!
            audioSource.Play();
        }
    }
}
