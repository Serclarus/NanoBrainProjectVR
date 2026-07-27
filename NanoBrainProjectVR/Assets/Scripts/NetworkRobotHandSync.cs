using System.Collections;
using Unity.Netcode;
using UnityEngine;
using MikeNspired.XRIStarterKit;

/// <summary>
/// Dedicated synchronization script that replicates the LeftHand_Robot and RightHand_Robot
/// world transforms and HandAnimator finger curl shapes (trigger/grip) from the VR player
/// to all remote clients and the PC build.
/// </summary>
public class NetworkRobotHandSync : NetworkBehaviour
{
    private HandAnimator m_LeftHandAnimator;
    private HandAnimator m_RightHandAnimator;

    public NetworkVariable<Vector3> leftHandPos = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<Quaternion> leftHandRot = new NetworkVariable<Quaternion>(Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> leftTrigger = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> leftGrip = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public NetworkVariable<Vector3> rightHandPos = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<Quaternion> rightHandRot = new NetworkVariable<Quaternion>(Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> rightTrigger = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<float> rightGrip = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    [SerializeField] private float lerpSpeed = 25f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Find Left and Right HandAnimator components on this avatar
        var animators = GetComponentsInChildren<HandAnimator>(true);
        foreach (var anim in animators)
        {
            if (anim.handType == LeftRight.Left)
                m_LeftHandAnimator = anim;
            else if (anim.handType == LeftRight.Right)
                m_RightHandAnimator = anim;
        }

        if (IsOwner)
        {
            if (m_LeftHandAnimator != null)
            {
                leftHandPos.Value = m_LeftHandAnimator.transform.position;
                leftHandRot.Value = m_LeftHandAnimator.transform.rotation;
            }
            if (m_RightHandAnimator != null)
            {
                rightHandPos.Value = m_RightHandAnimator.transform.position;
                rightHandRot.Value = m_RightHandAnimator.transform.rotation;
            }
        }
        else
        {
            // Start posing coroutines on remote clients so fingers continuously animate to match values
            if (m_LeftHandAnimator != null)
            {
                m_LeftHandAnimator.StartAnimationPosing();
                m_LeftHandAnimator.StartSecondaryPosing();
            }
            if (m_RightHandAnimator != null)
            {
                m_RightHandAnimator.StartAnimationPosing();
                m_RightHandAnimator.StartSecondaryPosing();
            }
        }
    }

    private void Update()
    {
        if (!IsSpawned) return;

        if (IsOwner)
        {
            // Read and send local VR hand positions & finger animation values
            if (m_LeftHandAnimator != null)
            {
                if (Vector3.Distance(leftHandPos.Value, m_LeftHandAnimator.transform.position) > 0.001f)
                    leftHandPos.Value = m_LeftHandAnimator.transform.position;
                if (Quaternion.Angle(leftHandRot.Value, m_LeftHandAnimator.transform.rotation) > 0.5f)
                    leftHandRot.Value = m_LeftHandAnimator.transform.rotation;

                if (Mathf.Abs(leftTrigger.Value - m_LeftHandAnimator.triggerAnimationValue) > 0.02f)
                    leftTrigger.Value = m_LeftHandAnimator.triggerAnimationValue;
                if (Mathf.Abs(leftGrip.Value - m_LeftHandAnimator.gripAnimationValue) > 0.02f)
                    leftGrip.Value = m_LeftHandAnimator.gripAnimationValue;
            }

            if (m_RightHandAnimator != null)
            {
                if (Vector3.Distance(rightHandPos.Value, m_RightHandAnimator.transform.position) > 0.001f)
                    rightHandPos.Value = m_RightHandAnimator.transform.position;
                if (Quaternion.Angle(rightHandRot.Value, m_RightHandAnimator.transform.rotation) > 0.5f)
                    rightHandRot.Value = m_RightHandAnimator.transform.rotation;

                if (Mathf.Abs(rightTrigger.Value - m_RightHandAnimator.triggerAnimationValue) > 0.02f)
                    rightTrigger.Value = m_RightHandAnimator.triggerAnimationValue;
                if (Mathf.Abs(rightGrip.Value - m_RightHandAnimator.gripAnimationValue) > 0.02f)
                    rightGrip.Value = m_RightHandAnimator.gripAnimationValue;
            }
        }
        else
        {
            // Remote player / PC build: apply network transform and finger values
            if (m_LeftHandAnimator != null)
            {
                if (!m_LeftHandAnimator.gameObject.activeSelf)
                    m_LeftHandAnimator.gameObject.SetActive(true);

                m_LeftHandAnimator.transform.position = Vector3.Lerp(m_LeftHandAnimator.transform.position, leftHandPos.Value, Time.deltaTime * lerpSpeed);
                m_LeftHandAnimator.transform.rotation = Quaternion.Slerp(m_LeftHandAnimator.transform.rotation, leftHandRot.Value, Time.deltaTime * lerpSpeed);

                m_LeftHandAnimator.triggerAnimationValue = leftTrigger.Value;
                m_LeftHandAnimator.gripAnimationValue = leftGrip.Value;
            }

            if (m_RightHandAnimator != null)
            {
                if (!m_RightHandAnimator.gameObject.activeSelf)
                    m_RightHandAnimator.gameObject.SetActive(true);

                m_RightHandAnimator.transform.position = Vector3.Lerp(m_RightHandAnimator.transform.position, rightHandPos.Value, Time.deltaTime * lerpSpeed);
                m_RightHandAnimator.transform.rotation = Quaternion.Slerp(m_RightHandAnimator.transform.rotation, rightHandRot.Value, Time.deltaTime * lerpSpeed);

                m_RightHandAnimator.triggerAnimationValue = rightTrigger.Value;
                m_RightHandAnimator.gripAnimationValue = rightGrip.Value;
            }
        }
    }
}
