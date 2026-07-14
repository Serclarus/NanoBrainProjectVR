using UnityEngine;
using Unity.Netcode;

/// <summary>
/// This script fixes the "In-Scene XROrigin Ownership" bug.
/// When the VR Client connects, the PC Server automatically transfers ownership 
/// of this XROrigin to the VR Client, allowing them to control it and spawn weapons!
/// </summary>
public class AssignRigOwnership : NetworkBehaviour
{
    // Obsolete: We now rely on NetworkManager's PlayerPrefab auto-spawning and destroy scene objects.
    public override void OnNetworkSpawn() { }
}
