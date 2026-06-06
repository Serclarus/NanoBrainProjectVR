using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using TMPro;

public class TrainingGroundsManager : MonoBehaviour
{
    [Header("Level Settings")]
    public int totalTargetsToWin = 5;
    
    [Header("UI Configuration")]
    [Tooltip("The parent canvas object for the victory screen")]
    public GameObject victoryCanvas;
    public TMP_Text timeText;
    public TMP_Text accuracyText;
    
    private int targetsKnockedDown = 0;
    private int totalProjectilesFired = 0;
    private int totalProjectilesHit = 0;
    
    private float timer = 0f;
    private bool isLevelActive = false;

    private void Start()
    {
        if (victoryCanvas != null) victoryCanvas.SetActive(false);
        StartLevel();
    }

    public void StartLevel()
    {
        isLevelActive = true;
        timer = 0f;
        targetsKnockedDown = 0;
        totalProjectilesFired = 0;
        totalProjectilesHit = 0;
    }

    private void Update()
    {
        if (isLevelActive)
        {
            timer += Time.deltaTime;
        }
    }

    private void OnEnable()
    {
        KnockdownTarget.OnTargetKnockedDown += HandleTargetKnockedDown;
        WeaponController.OnProjectilesFired += HandleProjectilesFired;
        ShotgunController.OnProjectilesFired += HandleProjectilesFired;
        TargetDamageZone.OnTargetHit += HandleTargetHit;
    }

    private void OnDisable()
    {
        KnockdownTarget.OnTargetKnockedDown -= HandleTargetKnockedDown;
        WeaponController.OnProjectilesFired -= HandleProjectilesFired;
        ShotgunController.OnProjectilesFired -= HandleProjectilesFired;
        TargetDamageZone.OnTargetHit -= HandleTargetHit;
    }

    private void HandleProjectilesFired(int count)
    {
        if (isLevelActive) totalProjectilesFired += count;
    }

    private void HandleTargetHit()
    {
        if (isLevelActive) totalProjectilesHit++;
    }

    private void HandleTargetKnockedDown()
    {
        Debug.Log($"[TrainingGrounds] A target was knocked down! Level Active: {isLevelActive}");
        if (!isLevelActive) return;

        targetsKnockedDown++;
        Debug.Log($"[TrainingGrounds] Targets Knocked Down: {targetsKnockedDown} / {totalTargetsToWin}");

        if (targetsKnockedDown >= totalTargetsToWin)
        {
            Debug.Log("[TrainingGrounds] All targets hit! Completing Level!");
            CompleteLevel();
        }
    }

    private void CompleteLevel()
    {
        isLevelActive = false;

        // 1. Freeze Player Locomotion (XRI 3.0 removes LocomotionSystem)
        var locomotionProviders = FindObjectsOfType<LocomotionProvider>();
        foreach (var provider in locomotionProviders)
        {
            provider.enabled = false;
        }

        // 2. Calculate Stats
        float accuracy = 0f;
        if (totalProjectilesFired > 0)
        {
            // Clamp hit count to fired count in case of extreme shotgun overlap edge-cases
            int safeHits = Mathf.Min(totalProjectilesHit, totalProjectilesFired);
            accuracy = ((float)safeHits / totalProjectilesFired) * 100f;
        }

        // 3. Show Victory UI
        if (victoryCanvas != null)
        {
            victoryCanvas.SetActive(true);
            
            // Position it 0.7m in front of the player (arms reach)
            if (Camera.main != null)
            {
                Transform head = Camera.main.transform;
                // Place it in front of the face, but keep it level with the horizon so it's not tilted weirdly
                Vector3 spawnPos = head.position + (head.forward * 0.7f);
                spawnPos.y = head.position.y; 
                
                victoryCanvas.transform.position = spawnPos;
                
                // Look at player
                victoryCanvas.transform.LookAt(head);
                // Flip it so text isn't backwards
                victoryCanvas.transform.Rotate(0, 180, 0); 
            }

            if (timeText != null)
            {
                int minutes = Mathf.FloorToInt(timer / 60F);
                int seconds = Mathf.FloorToInt(timer - minutes * 60);
                timeText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            }

            if (accuracyText != null)
            {
                accuracyText.text = string.Format("%{0:0}", accuracy);
            }
        }
    }

    // --- UI Button Methods ---
    
    public void RestartLevel()
    {
        // Re-enable locomotion before reloading so the player isn't stuck
        var locomotionProviders = FindObjectsOfType<LocomotionProvider>(true);
        foreach (var provider in locomotionProviders)
        {
            provider.enabled = true;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    public void ReturnToHub(string hubSceneName)
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(hubSceneName);
    }
}
