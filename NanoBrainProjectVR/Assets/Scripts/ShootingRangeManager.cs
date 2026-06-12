using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ShootingRangeManager : MonoBehaviour
{
    public static ShootingRangeManager Instance { get; private set; }

    [Header("Global Settings")]
    public bool isShootingAllowed = true;
    [Tooltip("Duration of the shooting range game in seconds.")]
    public float gameDuration = 60f;

    [Header("References")]
    [Tooltip("The target movers that will be controlled by this manager.")]
    public TargetMover[] targetMovers;
    
    [Tooltip("Optional: Text element to display the timer.")]
    public TMP_Text statusText;

    [Header("Audio Settings")]
    public AudioSource audioSource;
    [Tooltip("Sound played for each of the last 3 seconds of the game.")]
    public AudioClip countdownTickSound;
    [Tooltip("Sound played when the game starts/ends.")]
    public AudioClip phaseChangeSound;

    private bool isGameActive = false;
    private float currentTimer = 0f;
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
        if (isGameActive) return;

        isGameActive = true;
        isShootingAllowed = true;
        currentTimer = gameDuration;
        lastTickSecond = Mathf.CeilToInt(currentTimer);

        // Tell all targets to start random movement
        foreach (var mover in targetMovers)
        {
            if (mover != null) mover.StartRandomMovement();
        }

        PlayStateChangeSound();
        UpdateUI();
    }

    private void Update()
    {
        if (!isGameActive) return;

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
            EndGame();
        }
        else
        {
            UpdateUI();
        }
    }

    private void EndGame()
    {
        isGameActive = false;
        isShootingAllowed = false;
        PlayStateChangeSound();
        UpdateUI();

        // Tell all targets to stop and reset
        foreach (var mover in targetMovers)
        {
            if (mover != null) mover.StopAndReset();
        }
    }

    private void PlayCountdownTick()
    {
        if (audioSource != null && countdownTickSound != null)
        {
            audioSource.PlayOneShot(countdownTickSound);
        }
    }

    private void PlayStateChangeSound()
    {
        if (audioSource != null && phaseChangeSound != null)
        {
            audioSource.PlayOneShot(phaseChangeSound);
        }
    }

    private void UpdateUI()
    {
        if (statusText != null)
        {
            float time = Mathf.Max(0, currentTimer);
            int minutes = Mathf.FloorToInt(time / 60F);
            int seconds = Mathf.FloorToInt(time - minutes * 60);
            statusText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }
}
