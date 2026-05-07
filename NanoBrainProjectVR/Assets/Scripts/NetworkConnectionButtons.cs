using UnityEngine;
using XRMultiplayer;

public class NetworkConnectionButtons : MonoBehaviour
{
    [Header("Network Connection Actions")]
    [Tooltip("Call this from a UI Button or XR Simple Interactable to Host a game using Unity Relay (Works on LAN & Internet)")]
    public void HostRelayLobby()
    {
        if (XRINetworkGameManager.Instance != null)
        {
            Debug.Log("Hosting Relay Lobby...");
            _ = XRINetworkGameManager.Instance.CreateNewLobby();
        }
    }

    [Tooltip("Call this from a UI Button or XR Simple Interactable to Join the first available game using Unity Relay")]
    public void JoinRelayLobby()
    {
        if (XRINetworkGameManager.Instance != null)
        {
            Debug.Log("Joining Relay Lobby...");
            _ = XRINetworkGameManager.Instance.QuickJoinLobby();
        }
    }

    [Tooltip("Call this to host a strictly offline LAN server. Uses local IP address.")]
    public void HostStrictLAN()
    {
        if (XRINetworkGameManager.Instance != null)
        {
            Debug.Log("Hosting Local LAN Server...");
            XRINetworkGameManager.Instance.HostLocalConnection();
        }
    }

    [Tooltip("Call this to join a strictly offline LAN server. (Requires typing the Host's IP address in UnityTransport first!)")]
    public void JoinStrictLAN()
    {
        if (XRINetworkGameManager.Instance != null)
        {
            Debug.Log("Joining Local LAN Server...");
            XRINetworkGameManager.Instance.JoinLocalConnection();
        }
    }
}
