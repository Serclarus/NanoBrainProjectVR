using UnityEngine;
using Unity.Netcode;

public class CustomNetworkAvatar : NetworkBehaviour
{
    [Header("Visuals to Move")]
    public Transform headVisual;
    public Transform leftHandVisual;
    public Transform rightHandVisual;

    [Header("Tracking Sources (Auto-found)")]
    private Transform localHead;
    private Transform localLeftHand;
    private Transform localRightHand;

    [Header("Visibility")]
    [Tooltip("Drag your Head/Helmet visual objects here. They will be turned invisible for YOU so they don't block your camera, but other players will still see them!")]
    public GameObject[] hideForLocalPlayer;

    public override void OnNetworkSpawn()
    {
        // Only track the local camera if this is OUR body
        if (IsOwner)
        {
            foreach (var obj in hideForLocalPlayer)
            {
                if (obj != null) obj.SetActive(false);
            }
            if (Camera.main != null)
                localHead = Camera.main.transform;

            GameObject left = GameObject.Find("Left Controller");
            if (left != null) localLeftHand = left.transform;

            GameObject right = GameObject.Find("Right Controller");
            if (right != null) localRightHand = right.transform;
        }
    }

    private void Update()
    {
        // Only the owner moves these transforms. 
        // A ClientNetworkTransform on this object will sync them to other players!
        if (!IsOwner) return;

        if (localHead != null && headVisual != null)
        {
            headVisual.position = localHead.position;
            headVisual.rotation = localHead.rotation;
        }

        if (localLeftHand != null && leftHandVisual != null)
        {
            leftHandVisual.position = localLeftHand.position;
            leftHandVisual.rotation = localLeftHand.rotation;
        }

        if (localRightHand != null && rightHandVisual != null)
        {
            rightHandVisual.position = localRightHand.position;
            rightHandVisual.rotation = localRightHand.rotation;
        }
    }
}
