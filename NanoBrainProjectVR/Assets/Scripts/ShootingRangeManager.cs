using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ShootingRangeManager : MonoBehaviour
{
    public static ShootingRangeManager Instance { get; private set; }

    [Header("Global Settings")]
    public bool isShootingAllowed = true;

    [Header("References")]
    [Tooltip("The target movers that will be controlled by this manager.")]
    public TargetMover[] targetMovers;
    
    [Tooltip("Optional: Text element to display the timer and current phase info.")]
    public TMP_Text statusText;

    [Header("Audio Settings")]
    public AudioSource audioSource;
    [Tooltip("Sound played for each of the last 3 seconds of a phase.")]
    public AudioClip countdownTickSound;
    [Tooltip("Sound played when a phase ends/begins.")]
    public AudioClip phaseChangeSound;

    [Header("Phase Configuration")]
    [Tooltip("Duration of Phase 1 in seconds")]
    public float phase1Duration = 20f;
    public float phase1Multiplier = 1.0f;

    [Tooltip("Duration of Phase 2 in seconds")]
    public float phase2Duration = 20f;
    public float phase2Multiplier = 1.2f;

    [Tooltip("Duration of Phase 3 in seconds")]
    public float phase3Duration = 20f;
    public float phase3Multiplier = 1.5f;

    private int currentPhase = 0; // 0 = inactive, 1, 2, 3 = active phases
    private float currentTimer = 0f;
    private bool isResting = false;
    private int lastTickSecond = -1;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
            
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    [ContextMenu("Start Shooting Range")]
    public void StartRange()
    {
        // Reset everything to phase 1
        currentPhase = 1;
        isResting = false;
        isShootingAllowed = true;
        currentTimer = phase1Duration;
        lastTickSecond = Mathf.CeilToInt(currentTimer);

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.currentMultiplier = phase1Multiplier;
        }

        // Reset all targets to phase 0
        foreach (var mover in targetMovers)
        {
            if (mover != null) mover.ResetToPhase(0);
        }

        PlayPhaseChangeSound();
        UpdateUI($"Phase 1 - Multiplier: {phase1Multiplier}x\nTime: {currentTimer:F1}s");
    }

    private void Update()
    {
        if (currentPhase == 0 || isResting) return;

        currentTimer -= Time.deltaTime;

        // Play countdown tick for the last 3 seconds
        int currentSecond = Mathf.CeilToInt(currentTimer);
        if (currentSecond <= 3 && currentSecond > 0 && currentSecond != lastTickSecond)
        {
            lastTickSecond = currentSecond;
            PlayCountdownTick();
        }

        if (currentTimer <= 0f)
        {
            currentTimer = 0f;
            PhaseEnded();
        }
        else
        {
            UpdateUI($"Phase {currentPhase} - Multiplier: {GetCurrentMultiplier()}x\nTime: {currentTimer:F1}s");
        }
    }

    private void PhaseEnded()
    {
        isResting = true;
        isShootingAllowed = false;
        PlayPhaseChangeSound();

        if (currentPhase == 1)
        {
            UpdateUI("Target Moving... (Hold Fire)");
            MoveTargetsToPhase(1, () => StartPhase(2, phase2Duration, phase2Multiplier));
        }
        else if (currentPhase == 2)
        {
            UpdateUI("Target Moving... (Hold Fire)");
            MoveTargetsToPhase(2, () => StartPhase(3, phase3Duration, phase3Multiplier));
        }
        else if (currentPhase == 3)
        {
            UpdateUI("Range Complete! Resetting...");
            // Reset the range after Phase 3
            currentPhase = 0;
            StartCoroutine(ResetRangeRoutine());
        }
    }

    private IEnumerator ResetRangeRoutine()
    {
        yield return new WaitForSeconds(3f);
        UpdateUI("Range Ready. Shoot to start!");
        // Reset targets
        foreach (var mover in targetMovers)
        {
            if (mover != null) mover.ResetToPhase(0);
        }
        isShootingAllowed = true;
    }

    private void StartPhase(int newPhase, float duration, float multiplier)
    {
        currentPhase = newPhase;
        currentTimer = duration;
        isResting = false;
        isShootingAllowed = true;
        lastTickSecond = Mathf.CeilToInt(currentTimer);

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.currentMultiplier = multiplier;
        }

        PlayPhaseChangeSound();
        UpdateUI($"Phase {currentPhase} - Multiplier: {multiplier}x\nTime: {currentTimer:F1}s");
    }

    private void MoveTargetsToPhase(int index, System.Action onAllComplete)
    {
        if (targetMovers == null || targetMovers.Length == 0)
        {
            onAllComplete?.Invoke();
            return;
        }

        int completedCount = 0;
        int targetCount = targetMovers.Length;

        foreach (var mover in targetMovers)
        {
            if (mover != null)
            {
                mover.MoveToPhase(index, () => {
                    completedCount++;
                    if (completedCount == targetCount)
                    {
                        onAllComplete?.Invoke();
                    }
                });
            }
            else
            {
                completedCount++;
                if (completedCount == targetCount)
                {
                    onAllComplete?.Invoke();
                }
            }
        }
    }

    private float GetCurrentMultiplier()
    {
        if (currentPhase == 1) return phase1Multiplier;
        if (currentPhase == 2) return phase2Multiplier;
        if (currentPhase == 3) return phase3Multiplier;
        return 1.0f;
    }

    private void PlayCountdownTick()
    {
        if (audioSource != null && countdownTickSound != null)
        {
            audioSource.PlayOneShot(countdownTickSound);
        }
    }

    private void PlayPhaseChangeSound()
    {
        if (audioSource != null && phaseChangeSound != null)
        {
            audioSource.PlayOneShot(phaseChangeSound);
        }
    }

    private void UpdateUI(string text)
    {
        if (statusText != null)
        {
            statusText.text = text;
        }
    }
}
