using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetMover : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("How far the target moves along its local Z axis each phase.")]
    public float zMoveDistance = 4.5f;
    [Tooltip("How long it takes to move to the next phase position (seconds).")]
    public float moveDuration = 3f;
    
    private Vector3 initialPosition;

    [Header("Audio Settings")]
    [Tooltip("AudioSource to play the movement sound.")]
    public AudioSource audioSource;
    [Tooltip("Sound to play while the target is moving.")]
    public AudioClip movingSound;

    private void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        
        initialPosition = transform.position;
    }

    /// <summary>
    /// Starts moving the target to the position for the specified phase index (0, 1, 2...).
    /// </summary>
    public void MoveToPhase(int phaseIndex, System.Action onComplete = null)
    {
        Vector3 targetPosition = initialPosition + (transform.forward * (zMoveDistance * phaseIndex));
        StartCoroutine(MoveRoutine(targetPosition, onComplete));
    }

    /// <summary>
    /// Resets the target instantly to the position for the specified phase index.
    /// </summary>
    public void ResetToPhase(int phaseIndex)
    {
        StopAllCoroutines();
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        
        transform.position = initialPosition + (transform.forward * (zMoveDistance * phaseIndex));
    }

    private IEnumerator MoveRoutine(Vector3 targetPosition, System.Action onComplete)
    {
        Vector3 startPosition = transform.position;
        float elapsed = 0f;

        // Play moving sound
        if (audioSource != null && movingSound != null)
        {
            audioSource.clip = movingSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            // Optional: Smooth step for smoother start and stop
            t = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        transform.position = targetPosition;

        // Stop moving sound
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        onComplete?.Invoke();
    }
}
