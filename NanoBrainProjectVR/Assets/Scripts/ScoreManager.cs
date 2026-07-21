using UnityEngine;
using TMPro;
using Unity.Netcode;

public class ScoreManager : NetworkBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public float currentMultiplier = 1.0f;

    [Header("Networked Scores")]
    public NetworkVariable<int> hostScore = new NetworkVariable<int>(0);
    public NetworkVariable<int> clientScore = new NetworkVariable<int>(0);
    
    // Fallback offline score
    private int offlineScore = 0;
    
    // Local High Score (Persisted across sessions)
    private int localHighScore = 0;

    [Header("UI References")]
    public TMP_Text player1ScoreText;
    public TMP_Text player2ScoreText;
    public TMP_Text highScoreText;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
            
        localHighScore = PlayerPrefs.GetInt("OfflineHighScore", 0);
        UpdateUI();
    }

    public override void OnNetworkSpawn()
    {
        // When connecting to the server, the host resets the scores to 0
        if (IsServer)
        {
            hostScore.Value = 0;
            clientScore.Value = 0;
        }

        // Listen for score changes so the UI updates automatically
        hostScore.OnValueChanged += (oldVal, newVal) => OnNetworkScoreChanged();
        clientScore.OnValueChanged += (oldVal, newVal) => OnNetworkScoreChanged();
        
        UpdateUI();
    }

    public void AddScore(int points, float customMultiplier = -1f)
    {
        float multiplierToUse = customMultiplier >= 0f ? customMultiplier : currentMultiplier;
        int finalPoints = Mathf.RoundToInt(points * multiplierToUse);

        if (!IsSpawned)
        {
            // If playing offline, just add to the offline score
            offlineScore += finalPoints;
            CheckHighScore(offlineScore);
            UpdateUI();
            return;
        }

        // If online, send the points to the server with our unique Client ID
        AddScoreServerRpc(finalPoints, NetworkManager.Singleton.LocalClientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void AddScoreServerRpc(int points, ulong clientId)
    {
        if (clientId == 0) // Client 0 is always the Host (Player 1)
        {
            hostScore.Value += points;
        }
        else // Client 1 is the joined Player 2
        {
            clientScore.Value += points;
        }
    }

    private void OnNetworkScoreChanged()
    {
        // Find out what the LOCAL player's score is right now
        int myCurrentScore = 0;
        
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
        {
            if (NetworkManager.Singleton.LocalClientId == 0) 
            {
                myCurrentScore = hostScore.Value;
            }
            else 
            {
                myCurrentScore = clientScore.Value;
            }
        }

        CheckHighScore(myCurrentScore);
        UpdateUI();
    }

    private void CheckHighScore(int currentScore)
    {
        if (currentScore > localHighScore)
        {
            localHighScore = currentScore;
            PlayerPrefs.SetInt("OfflineHighScore", localHighScore);
            PlayerPrefs.Save();
        }
    }

    [ContextMenu("Reset High Score")]
    public void ResetHighScore()
    {
        localHighScore = 0;
        PlayerPrefs.SetInt("OfflineHighScore", 0);
        PlayerPrefs.Save();
        UpdateUI();
        Debug.Log("[ScoreManager] High Score has been reset to 0!");
    }

    [ContextMenu("Reset Current Score")]
    public void ResetCurrentScore()
    {
        // Reset local offline score
        offlineScore = 0;

        if (IsSpawned)
        {
            // If online, tell the server to reset everyone's score
            ResetScoreServerRpc();
        }

        UpdateUI();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ResetScoreServerRpc()
    {
        hostScore.Value = 0;
        clientScore.Value = 0;
    }

    private void UpdateUI()
    {
        if (player1ScoreText != null)
        {
            player1ScoreText.text = IsSpawned ? hostScore.Value.ToString() : offlineScore.ToString();
        }

        if (player2ScoreText != null)
        {
            player2ScoreText.text = IsSpawned ? clientScore.Value.ToString() : "0";
        }

        if (highScoreText != null)
        {
            highScoreText.text = localHighScore.ToString();
        }
    }
}
