using UnityEngine;

[System.Serializable]
public struct RecoilTier
{
    [Tooltip("Min and Max upward kick (Pitch)")]
    public Vector2 pitchRange;
    [Tooltip("Side to side wobble per shot (Yaw)")]
    public Vector2 yawRange;
    [Tooltip("Rotational twist per shot (Roll)")]
    public Vector2 rollRange;
    [Tooltip("How far the gun kicks back per shot")]
    public float backwardKick;
}

[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Weapons/Weapon Data")]
public class WeaponDataSO : ScriptableObject
{
    [Header("Fire Mode")]
    [Tooltip("If true, holding the trigger will fire continuously. If false, one shot per trigger pull.")]
    public bool fullAuto = true;
    [Tooltip("Rounds per minute for full-auto fire")]
    public float fireRate = 600f;
    public float range = 100f;

    [Header("Audio Settings")]
    [Tooltip("The audio clip played when the weapon fires")]
    public AudioClip shootSound;
    [Tooltip("Sound to play when pulling the trigger but there's no ammo chambered")]
    public AudioClip dryFireSound;
    [Tooltip("Slightly randomize the pitch of each shot so it doesn't sound repetitive")]
    public Vector2 soundPitchRange = new Vector2(0.95f, 1.05f);
    [Tooltip("Volume of the gunshot")]
    [Range(0f, 1f)] public float shootVolume = 1f;

    [Header("Controller Haptics")]
    [Tooltip("How strong the controller vibrates (0.0 to 1.0)")]
    [Range(0f, 1f)] public float hapticIntensity = 0.5f;
    [Tooltip("How long the controller vibrates in seconds")]
    public float hapticDuration = 0.1f;

    [Header("Shell Ejection")]
    [Tooltip("The empty shell prefab to instantiate")]
    public GameObject shellPrefab;
    [Tooltip("Force applied to the ejected shell")]
    public float shellEjectionForce = 3f;
    [Tooltip("Spin applied to the ejected shell")]
    public float shellTorque = 1f;

    [Header("Recoil - Per-Tier Settings (based on consecutive shots)")]
    [Tooltip("Recoil for shots 1-3 (initial burst)")]
    public RecoilTier tier1_Shots1to3 = new RecoilTier
    {
        pitchRange = new Vector2(1.0f, 2.0f),
        yawRange = new Vector2(-0.3f, 0.3f),
        rollRange = new Vector2(-0.2f, 0.2f),
        backwardKick = 0.01f
    };

    [Tooltip("Recoil for shots 4-8 (building up)")]
    public RecoilTier tier2_Shots4to8 = new RecoilTier
    {
        pitchRange = new Vector2(2.0f, 4.0f),
        yawRange = new Vector2(-1.0f, 1.0f),
        rollRange = new Vector2(-0.5f, 0.5f),
        backwardKick = 0.025f
    };

    [Tooltip("Recoil for shots 9-17 (sustained fire)")]
    public RecoilTier tier3_Shots9to17 = new RecoilTier
    {
        pitchRange = new Vector2(3.0f, 6.0f),
        yawRange = new Vector2(-2.0f, 2.0f),
        rollRange = new Vector2(-0.8f, 0.8f),
        backwardKick = 0.04f
    };

    [Tooltip("Recoil for shots 18-30 (full spray)")]
    public RecoilTier tier4_Shots18plus = new RecoilTier
    {
        pitchRange = new Vector2(4.0f, 8.0f),
        yawRange = new Vector2(-3.0f, 3.0f),
        rollRange = new Vector2(-1.0f, 1.0f),
        backwardKick = 0.05f
    };

    [Header("Recoil - Feel (Dynamics)")]
    [Tooltip("How fast the gun snaps to the peak of the recoil. High values feel punchy and sharp.")]
    public float snappiness = 20f;
    [Tooltip("How slow the gun recovers back to rest position. Low values feel heavy.")]
    public float returnSpeed = 8f;

    [Header("Two-Handed Feel")]
    [Tooltip("Multiplier for recoil when holding the weapon with both hands")]
    public float twoHandRecoilModifier = 0.4f;
}
