using UnityEngine;
using Unity.Netcode;

/// <summary>
/// This script fixes the "In-Scene XROrigin Ownership" bug.
/// When the VR Client connects, the PC Server automatically transfers ownership 
/// of this XROrigin to the VR Client, allowing them to control it and spawn weapons!
/// </summary>
public class AssignRigOwnership : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // When the server starts, subscribe to client connections
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // Ignore the PC Operator (Client 0)
        if (clientId == NetworkManager.ServerClientId) return;

        // If a VR Client joins, force this in-scene XR Origin to belong to them!
        Debug.Log($"<color=yellow>[AssignRigOwnership]</color> VR Client {clientId} joined! Transferring XR Origin ownership to them!");
        
        if (NetworkObject.IsSpawned && NetworkObject.OwnerClientId != clientId)
        {
            NetworkObject.ChangeOwnership(clientId);
            
            // Now that they own it, force NetworkPlayerLoadout to run its spawn logic!
            NetworkPlayerLoadout loadout = GetComponent<NetworkPlayerLoadout>();
            if (loadout != null)
            {
                // We must use reflection or change NetworkPlayerLoadout, but wait:
                // NetworkPlayerLoadout runs OnNetworkSpawn(), which already passed for the server.
                // We can just explicitly call the weapon spawner here!
                Debug.Log($"<color=yellow>[AssignRigOwnership]</color> Triggering weapon spawn for Client {clientId}...");
                loadout.ForceSpawnForClient(clientId);
            }
        }
    }
}
