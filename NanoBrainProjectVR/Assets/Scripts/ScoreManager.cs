using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    private int localPlayerScore = 0;

    [Header("UI References")]
    [Tooltip("Optional: Drag a TextMeshProUGUI element here to display score")]
    public TMP_Text scoreTextUI;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
            
        UpdateScoreUI();
    }

    public void AddScore(int points)
    {
        localPlayerScore += points;
        Debug.Log($"Score Added: +{points} | Total Score: {localPlayerScore}");
        UpdateScoreUI();
    }

    private void UpdateScoreUI()
    {
        if (scoreTextUI != null)
        {
            scoreTextUI.text = "Score: " + localPlayerScore.ToString();
        }
    }
}
