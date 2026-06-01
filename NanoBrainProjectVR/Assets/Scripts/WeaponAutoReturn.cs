using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;
using Unity.Netcode;

public enum WeaponSlotType { Rifle, Shotgun, Pistol }

public class WeaponAutoReturn : NetworkBehaviour
{
    [Header("Weapon Configuration")]
    [Tooltip("Which socket on the player's body does this weapon belong to?")]
    public WeaponSlotType slotType;
    
    [Tooltip("How many seconds the weapon sits on the ground before returning")]
    public float returnDelay = 3.0f;

    private XRGrabInteractable grabInteractable;
    private XRSocketInteractor homeSocket;
    private Coroutine returnRoutine;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnDropped);
        }
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnDropped);
        }
    }

    public override void OnNetworkSpawn()
    {
        // When the weapon spawns on the client, find its home socket!
        if (IsOwner)
        {
            PlayerHolsterSystem holsters = FindObjectOfType<PlayerHolsterSystem>();
            if (holsters != null)
            {
                if (slotType == WeaponSlotType.Rifle) homeSocket = holsters.rightShoulderSocket;
                else if (slotType == WeaponSlotType.Shotgun) homeSocket = holsters.leftShoulderSocket;
                else if (slotType == WeaponSlotType.Pistol) homeSocket = holsters.rightBeltSocket;

                if (homeSocket != null)
                {
                    // Snap it in perfectly!
                    StartCoroutine(ForceSlotWeaponRoutine());
                }
            }
        }
    }

    private IEnumerator ForceSlotWeaponRoutine()
    {
        // Wait 1 frame so XR Toolkit can initialize the socket properly
        yield return new WaitForEndOfFrame();
        
        transform.position = homeSocket.transform.position;
        transform.rotation = homeSocket.transform.rotation;
        
        homeSocket.interactionManager.SelectEnter((IXRSelectInteractor)homeSocket, grabInteractable);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        // If someone grabs it, stop the return timer!
        if (returnRoutine != null) StopCoroutine(returnRoutine);
    }

    private void OnDropped(SelectExitEventArgs args)
    {
        if (!IsOwner) return;

        // If it was dropped on the ground (NOT put into another socket)
        if (!(args.interactorObject is XRSocketInteractor))
        {
            if (returnRoutine != null) StopCoroutine(returnRoutine);
            returnRoutine = StartCoroutine(ReturnTimer());
        }
    }

    private IEnumerator ReturnTimer()
    {
        yield return new WaitForSeconds(returnDelay);

        if (homeSocket != null)
        {
            Debug.Log($"Weapon Auto-Returned to {slotType} Holster!");
            yield return StartCoroutine(ForceSlotWeaponRoutine());
        }
    }
}
