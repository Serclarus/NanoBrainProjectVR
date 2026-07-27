using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using MikeNspired.XRIStarterKit;

public struct HandPoseData : INetworkSerializable, IEquatable<HandPoseData>
{
    public Quaternion r0, r1, r2, r3, r4, r5, r6, r7, r8, r9,
                      r10, r11, r12, r13, r14, r15, r16, r17, r18, r19;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref r0);
        serializer.SerializeValue(ref r1);
        serializer.SerializeValue(ref r2);
        serializer.SerializeValue(ref r3);
        serializer.SerializeValue(ref r4);
        serializer.SerializeValue(ref r5);
        serializer.SerializeValue(ref r6);
        serializer.SerializeValue(ref r7);
        serializer.SerializeValue(ref r8);
        serializer.SerializeValue(ref r9);
        serializer.SerializeValue(ref r10);
        serializer.SerializeValue(ref r11);
        serializer.SerializeValue(ref r12);
        serializer.SerializeValue(ref r13);
        serializer.SerializeValue(ref r14);
        serializer.SerializeValue(ref r15);
        serializer.SerializeValue(ref r16);
        serializer.SerializeValue(ref r17);
        serializer.SerializeValue(ref r18);
        serializer.SerializeValue(ref r19);
    }

    public bool Equals(HandPoseData other)
    {
        return r0 == other.r0 && r1 == other.r1 && r2 == other.r2 && r3 == other.r3 &&
               r4 == other.r4 && r5 == other.r5 && r6 == other.r6 && r7 == other.r7 &&
               r8 == other.r8 && r9 == other.r9 && r10 == other.r10 && r11 == other.r11 &&
               r12 == other.r12 && r13 == other.r13 && r14 == other.r14 && r15 == other.r15 &&
               r16 == other.r16 && r17 == other.r17 && r18 == other.r18 && r19 == other.r19;
    }
}

/// <summary>
/// Dedicated synchronization script that replicates the LeftHand_Robot and RightHand_Robot
/// world transforms and exact finger bone rotations from the VR player to all remote clients
/// and the PC build.
/// </summary>
public class NetworkRobotHandSync : NetworkBehaviour
{
    private HandAnimator m_LeftHandAnimator;
    private HandAnimator m_RightHandAnimator;

    public NetworkVariable<Vector3> leftHandPos = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<Quaternion> leftHandRot = new NetworkVariable<Quaternion>(Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<HandPoseData> leftHandPose = new NetworkVariable<HandPoseData>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public NetworkVariable<Vector3> rightHandPos = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<Quaternion> rightHandRot = new NetworkVariable<Quaternion>(Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<HandPoseData> rightHandPose = new NetworkVariable<HandPoseData>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

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
                leftHandPose.Value = PackJoints(m_LeftHandAnimator);
            }
            if (m_RightHandAnimator != null)
            {
                rightHandPos.Value = m_RightHandAnimator.transform.position;
                rightHandRot.Value = m_RightHandAnimator.transform.rotation;
                rightHandPose.Value = PackJoints(m_RightHandAnimator);
            }
        }
        else
        {
            // On remote clients / PC build, stop any local HandAnimator coroutines
            // so they don't force a default fist pose or fight our network joint synchronization.
            if (m_LeftHandAnimator != null)
            {
                m_LeftHandAnimator.StopAllCoroutines();
            }
            if (m_RightHandAnimator != null)
            {
                m_RightHandAnimator.StopAllCoroutines();
            }
        }
    }

    private void Update()
    {
        if (!IsSpawned) return;

        if (IsOwner)
        {
            // Read and send local VR hand positions & finger joint rotations
            if (m_LeftHandAnimator != null)
            {
                if (Vector3.Distance(leftHandPos.Value, m_LeftHandAnimator.transform.position) > 0.001f)
                    leftHandPos.Value = m_LeftHandAnimator.transform.position;
                if (Quaternion.Angle(leftHandRot.Value, m_LeftHandAnimator.transform.rotation) > 0.5f)
                    leftHandRot.Value = m_LeftHandAnimator.transform.rotation;

                HandPoseData leftPacked = PackJoints(m_LeftHandAnimator);
                if (!leftHandPose.Value.Equals(leftPacked))
                    leftHandPose.Value = leftPacked;
            }

            if (m_RightHandAnimator != null)
            {
                if (Vector3.Distance(rightHandPos.Value, m_RightHandAnimator.transform.position) > 0.001f)
                    rightHandPos.Value = m_RightHandAnimator.transform.position;
                if (Quaternion.Angle(rightHandRot.Value, m_RightHandAnimator.transform.rotation) > 0.5f)
                    rightHandRot.Value = m_RightHandAnimator.transform.rotation;

                HandPoseData rightPacked = PackJoints(m_RightHandAnimator);
                if (!rightHandPose.Value.Equals(rightPacked))
                    rightHandPose.Value = rightPacked;
            }
        }
        else
        {
            // Remote player / PC build: apply network transform and exact finger bone rotations
            if (m_LeftHandAnimator != null)
            {
                if (!m_LeftHandAnimator.gameObject.activeSelf)
                    m_LeftHandAnimator.gameObject.SetActive(true);

                m_LeftHandAnimator.transform.position = Vector3.Lerp(m_LeftHandAnimator.transform.position, leftHandPos.Value, Time.deltaTime * lerpSpeed);
                m_LeftHandAnimator.transform.rotation = Quaternion.Slerp(m_LeftHandAnimator.transform.rotation, leftHandRot.Value, Time.deltaTime * lerpSpeed);

                UnpackJoints(m_LeftHandAnimator, leftHandPose.Value, Time.deltaTime * lerpSpeed);
            }

            if (m_RightHandAnimator != null)
            {
                if (!m_RightHandAnimator.gameObject.activeSelf)
                    m_RightHandAnimator.gameObject.SetActive(true);

                m_RightHandAnimator.transform.position = Vector3.Lerp(m_RightHandAnimator.transform.position, rightHandPos.Value, Time.deltaTime * lerpSpeed);
                m_RightHandAnimator.transform.rotation = Quaternion.Slerp(m_RightHandAnimator.transform.rotation, rightHandRot.Value, Time.deltaTime * lerpSpeed);

                UnpackJoints(m_RightHandAnimator, rightHandPose.Value, Time.deltaTime * lerpSpeed);
            }
        }
    }

    private HandPoseData PackJoints(HandAnimator animator)
    {
        HandPoseData data = new HandPoseData
        {
            r0 = Quaternion.identity, r1 = Quaternion.identity, r2 = Quaternion.identity, r3 = Quaternion.identity, r4 = Quaternion.identity,
            r5 = Quaternion.identity, r6 = Quaternion.identity, r7 = Quaternion.identity, r8 = Quaternion.identity, r9 = Quaternion.identity,
            r10 = Quaternion.identity, r11 = Quaternion.identity, r12 = Quaternion.identity, r13 = Quaternion.identity, r14 = Quaternion.identity,
            r15 = Quaternion.identity, r16 = Quaternion.identity, r17 = Quaternion.identity, r18 = Quaternion.identity, r19 = Quaternion.identity
        };

        if (animator == null) return data;

        if (animator.currentJoints == null || animator.currentJoints.Count == 0)
            animator.SetBones();

        var joints = animator.currentJoints;
        if (joints.Count > 0 && joints[0] != null) data.r0 = joints[0].localRotation;
        if (joints.Count > 1 && joints[1] != null) data.r1 = joints[1].localRotation;
        if (joints.Count > 2 && joints[2] != null) data.r2 = joints[2].localRotation;
        if (joints.Count > 3 && joints[3] != null) data.r3 = joints[3].localRotation;
        if (joints.Count > 4 && joints[4] != null) data.r4 = joints[4].localRotation;
        if (joints.Count > 5 && joints[5] != null) data.r5 = joints[5].localRotation;
        if (joints.Count > 6 && joints[6] != null) data.r6 = joints[6].localRotation;
        if (joints.Count > 7 && joints[7] != null) data.r7 = joints[7].localRotation;
        if (joints.Count > 8 && joints[8] != null) data.r8 = joints[8].localRotation;
        if (joints.Count > 9 && joints[9] != null) data.r9 = joints[9].localRotation;
        if (joints.Count > 10 && joints[10] != null) data.r10 = joints[10].localRotation;
        if (joints.Count > 11 && joints[11] != null) data.r11 = joints[11].localRotation;
        if (joints.Count > 12 && joints[12] != null) data.r12 = joints[12].localRotation;
        if (joints.Count > 13 && joints[13] != null) data.r13 = joints[13].localRotation;
        if (joints.Count > 14 && joints[14] != null) data.r14 = joints[14].localRotation;
        if (joints.Count > 15 && joints[15] != null) data.r15 = joints[15].localRotation;
        if (joints.Count > 16 && joints[16] != null) data.r16 = joints[16].localRotation;
        if (joints.Count > 17 && joints[17] != null) data.r17 = joints[17].localRotation;
        if (joints.Count > 18 && joints[18] != null) data.r18 = joints[18].localRotation;
        if (joints.Count > 19 && joints[19] != null) data.r19 = joints[19].localRotation;

        return data;
    }

    private void UnpackJoints(HandAnimator animator, HandPoseData data, float lerp)
    {
        if (animator == null) return;

        if (animator.currentJoints == null || animator.currentJoints.Count == 0)
            animator.SetBones();

        var joints = animator.currentJoints;
        if (joints.Count > 0 && joints[0] != null)
            joints[0].localRotation = Quaternion.Slerp(joints[0].localRotation, data.r0, lerp);
        if (joints.Count > 1 && joints[1] != null)
            joints[1].localRotation = Quaternion.Slerp(joints[1].localRotation, data.r1, lerp);
        if (joints.Count > 2 && joints[2] != null)
            joints[2].localRotation = Quaternion.Slerp(joints[2].localRotation, data.r2, lerp);
        if (joints.Count > 3 && joints[3] != null)
            joints[3].localRotation = Quaternion.Slerp(joints[3].localRotation, data.r3, lerp);
        if (joints.Count > 4 && joints[4] != null)
            joints[4].localRotation = Quaternion.Slerp(joints[4].localRotation, data.r4, lerp);
        if (joints.Count > 5 && joints[5] != null)
            joints[5].localRotation = Quaternion.Slerp(joints[5].localRotation, data.r5, lerp);
        if (joints.Count > 6 && joints[6] != null)
            joints[6].localRotation = Quaternion.Slerp(joints[6].localRotation, data.r6, lerp);
        if (joints.Count > 7 && joints[7] != null)
            joints[7].localRotation = Quaternion.Slerp(joints[7].localRotation, data.r7, lerp);
        if (joints.Count > 8 && joints[8] != null)
            joints[8].localRotation = Quaternion.Slerp(joints[8].localRotation, data.r8, lerp);
        if (joints.Count > 9 && joints[9] != null)
            joints[9].localRotation = Quaternion.Slerp(joints[9].localRotation, data.r9, lerp);
        if (joints.Count > 10 && joints[10] != null)
            joints[10].localRotation = Quaternion.Slerp(joints[10].localRotation, data.r10, lerp);
        if (joints.Count > 11 && joints[11] != null)
            joints[11].localRotation = Quaternion.Slerp(joints[11].localRotation, data.r11, lerp);
        if (joints.Count > 12 && joints[12] != null)
            joints[12].localRotation = Quaternion.Slerp(joints[12].localRotation, data.r12, lerp);
        if (joints.Count > 13 && joints[13] != null)
            joints[13].localRotation = Quaternion.Slerp(joints[13].localRotation, data.r13, lerp);
        if (joints.Count > 14 && joints[14] != null)
            joints[14].localRotation = Quaternion.Slerp(joints[14].localRotation, data.r14, lerp);
        if (joints.Count > 15 && joints[15] != null)
            joints[15].localRotation = Quaternion.Slerp(joints[15].localRotation, data.r15, lerp);
        if (joints.Count > 16 && joints[16] != null)
            joints[16].localRotation = Quaternion.Slerp(joints[16].localRotation, data.r16, lerp);
        if (joints.Count > 17 && joints[17] != null)
            joints[17].localRotation = Quaternion.Slerp(joints[17].localRotation, data.r17, lerp);
        if (joints.Count > 18 && joints[18] != null)
            joints[18].localRotation = Quaternion.Slerp(joints[18].localRotation, data.r18, lerp);
        if (joints.Count > 19 && joints[19] != null)
            joints[19].localRotation = Quaternion.Slerp(joints[19].localRotation, data.r19, lerp);
    }
}
