using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class BoarAI : MonoBehaviour
{
    public enum BoarState { Idle, Wander, Flee, Dead }

    [Header("AI State")]
    public BoarState currentState = BoarState.Idle;

    [Header("Movement Speeds")]
    public float normalRunSpeed = 8f;
    public float injuredRunSpeed = 4f;
    public float wanderSpeed = 1.5f;
    [Tooltip("How far the boar will search for a random wander point. Increase this so it travels across the whole zone instead of small steps.")]
    public float wanderRadius = 30f;

    [Header("Detection Settings")]
    public Transform playerTransform;
    [Tooltip("How close the player needs to be before the boar runs away.")]
    public float detectionRadius = 15f;
    [Tooltip("How far the boar tries to run away at once")]
    public float fleeDistance = 20f;
    [Tooltip("How fast the boar walks")]
    public float walkSpeed = 2f;
    [Tooltip("How fast the boar runs when scared")]
    public float runSpeed = 6f;
    //[Tooltip("How fast the boar runs when injured (bleeding out)")]
    //public float injuredRunSpeed = 3.5f;

    private bool wasShot = false; // Tracks if the boar was damaged
    [Header("Health Integration")]
    public AnimalHealth healthScript;
    [Tooltip("Health percentage (0.0 to 1.0) at which the boar gets injured and slows down.")]
    [Range(0f, 1f)]
    public float injuryThreshold = 0.3f;

    [Header("NavMesh Areas")]
    [Tooltip("The exact name of the NavMesh area to use when wandering/hovering")]
    public string wanderAreaName = "Hover";
    [Tooltip("The exact name of the NavMesh area to use when fleeing")]
    public string fleeAreaName = "Flee";
    [Tooltip("The exact name of the NavMesh area to use to get close to the player")]
    public string closeAreaName = "CloseToPlayer";

    [Header("Animation States")]
    public Animator animator;
    [Tooltip("If true, the Boar will smoothly blend animations based on its speed using a Blend Tree instead of hard transitions.")]
    public bool useBlendTree = true;
    [Tooltip("The exact name of the Float parameter in your Animator (used if Blend Tree is enabled).")]
    public string speedAnimParam = "Speed";
    [Tooltip("The exact name of the Float parameter for turning in your Animator (used for 2D Blend Trees).")]
    public string turnAnimParam = "Turn";

    [Space(10)]
    [Tooltip("Exact name of the Idle state (used if Blend Tree is disabled)")]
    public string idleAnimName = "IdleBreathe";
    [Tooltip("Exact name of the Walk state (used if Blend Tree is disabled)")]
    public string walkAnimName = "Walk";
    [Tooltip("Exact name of the Run state (used if Blend Tree is disabled)")]
    public string runAnimName = "Run";
    [Tooltip("Exact name of the Death state/trigger")]
    public string deathAnimName = "Death";

    private NavMeshAgent agent;
    private bool isInjured = false;
    private Vector3 wanderTarget;
    private float stateTimer = 0f;
    private string currentAnim = "";
    private float lastYRotation = 0f;

    private int wanderAreaMask;
    private int fleeAreaMask;
    private int closeAreaMask;
    
    private int wanderPathCount = 0;
    
    private Collider[] cachedFleeZones;

    // Keep track of all living boars for highly performant custom avoidance!
    public static System.Collections.Generic.List<BoarAI> activeBoars = new System.Collections.Generic.List<BoarAI>();

    private void OnEnable()
    {
        activeBoars.Add(this);
    }

    private void OnDisable()
    {
        activeBoars.Remove(this);
    }

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        // Convert the string names into integer bitmasks that Unity's NavMesh system understands
        int wanderLayer = NavMesh.GetAreaFromName(wanderAreaName);
        int fleeLayer = NavMesh.GetAreaFromName(fleeAreaName);
        int closeLayer = NavMesh.GetAreaFromName(closeAreaName);
        
        wanderAreaMask = (wanderLayer != -1) ? (1 << wanderLayer) : NavMesh.AllAreas;
        fleeAreaMask = (fleeLayer != -1) ? (1 << fleeLayer) : NavMesh.AllAreas;
        closeAreaMask = (closeLayer != -1) ? (1 << closeLayer) : NavMesh.AllAreas;

        if (healthScript != null)
        {
            // Subscribe to health events
            healthScript.onDamageTaken.AddListener(OnDamaged);
            healthScript.onDeathEvent.AddListener(OnDeath);
        }

        // Try to find the player if not assigned
        if (playerTransform == null && Camera.main != null)
        {
            playerTransform = Camera.main.transform;
        }

        // Listen for gunshots globally
        WeaponController.OnProjectilesFired += OnGunshotHeard;

        // Cache flee zones to manually check overlaps (bypasses Unity's Rigidbody requirement for OnTriggerEnter)
        GameObject[] zoneObjs = GameObject.FindGameObjectsWithTag("FleeZone");
        cachedFleeZones = new Collider[zoneObjs.Length];
        for (int i = 0; i < zoneObjs.Length; i++)
        {
            cachedFleeZones[i] = zoneObjs[i].GetComponent<Collider>();
        }

        // Ensure NavMeshAgent handles rotation natively for maximum performance
        agent.updateRotation = true;

        ChangeState(BoarState.Wander);
    }

    private void OnDestroy()
    {
        WeaponController.OnProjectilesFired -= OnGunshotHeard;
    }

    private void OnGunshotHeard(int amount)
    {
        if (currentState == BoarState.Dead || currentState == BoarState.Flee || playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= detectionRadius)
        {
            ChangeState(BoarState.Flee);
        }
    }

    private void Update()
    {
        if (currentState == BoarState.Dead) return;

        CheckHealthStatus();
        CheckPlayerDistance();

        switch (currentState)
        {
            case BoarState.Idle:
                UpdateIdle();
                break;
            case BoarState.Wander:
                UpdateWander();
                break;
            case BoarState.Flee:
                UpdateFlee();
                break;
        }

        HandleBoarAvoidance();
        UpdateAnimator();
    }

    private void HandleBoarAvoidance()
    {
        if (agent == null || !agent.enabled || currentState == BoarState.Dead) return;

        Vector3 avoidanceForce = Vector3.zero;
        int count = 0;

        // Loop through the highly-optimized list of active boars (No expensive Physics overlaps!)
        foreach (var otherBoar in activeBoars)
        {
            if (otherBoar == this || otherBoar == null || otherBoar.currentState == BoarState.Dead) continue;

            Vector3 diff = transform.position - otherBoar.transform.position;
            diff.y = 0; // Only care about horizontal distance
            float distSqr = diff.sqrMagnitude;

            // If closer than 1 meter (1 squared is 1)
            if (distSqr < 1.0f && distSqr > 0.001f)
            {
                float actualDist = Mathf.Sqrt(distSqr);
                // Push harder the closer they get
                avoidanceForce += diff.normalized * (1.0f - actualDist);
                count++;
            }
        }

        if (count > 0)
        {
            // Instead of magically gliding sideways, we add the avoidance to the velocity!
            // This forces the NavMeshAgent to naturally steer and curve its path.
            agent.velocity += avoidanceForce * Time.deltaTime * 8f;
        }
    }



    private void CheckHealthStatus()
    {
        if (healthScript == null) return;

        if (!isInjured && healthScript.GetHealthPercentage() <= injuryThreshold)
        {
            isInjured = true;
            // Instantly apply the injured speed limit
            if (currentState == BoarState.Flee)
            {
                agent.speed = injuredRunSpeed;
            }
        }
    }

    private void CheckPlayerDistance()
    {
        if (playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        
        // If the player gets too close, run away!
        if (dist <= detectionRadius && currentState != BoarState.Flee)
        {
            ChangeState(BoarState.Flee);
        }
        // If we ran far enough away, go back to wandering (BUT ONLY if we were just spooked, NOT if we were shot!)
        else if (!wasShot && dist > detectionRadius * 1.5f && currentState == BoarState.Flee && !agent.pathPending && agent.remainingDistance < 2f)
        {
            ChangeState(BoarState.Wander);
        }
    }

    private void UpdateIdle()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
        {
            ChangeState(BoarState.Wander);
        }
    }

    private void UpdateWander()
    {
        // If we reached our wander destination, go idle
        if (!agent.pathPending && agent.remainingDistance < 1f)
        {
            ChangeState(BoarState.Idle);
            return;
        }
    }

    private float fleeRecalculateTimer = 0f;
    private float totalFleeTime = 0f;

    private void UpdateFlee()
    {
        totalFleeTime += Time.deltaTime;

        // Manually check if inside a FleeZone (Works even if the Boar doesn't have a Rigidbody!)
        // ALSO check if it has been fleeing for more than 12 seconds, so they don't get stuck forever
        if (totalFleeTime > 12f)
        {
            StartCoroutine(FadeOutAndDestroy());
            return;
        }

        if (cachedFleeZones != null)
        {
            foreach (var col in cachedFleeZones)
            {
                if (col != null && col.ClosestPoint(transform.position) == transform.position)
                {
                    // We are inside the FleeZone!
                    StartCoroutine(FadeOutAndDestroy());
                    return; // Stop updating
                }
            }
        }

        fleeRecalculateTimer -= Time.deltaTime;

        // Continuously update the flee target away from the player
        // If the timer expires OR we reach our destination early, immediately pick a new spot so we never stop running!
        if (playerTransform != null && (fleeRecalculateTimer <= 0f || (!agent.pathPending && agent.remainingDistance < 1f)))
        {
            fleeRecalculateTimer = Random.Range(1.5f, 3.0f); // Change direction every 1.5 to 3 seconds

            // Calculate direction AWAY from player
            Vector3 runDir = (transform.position - playerTransform.position).normalized;
            
            // Add randomness to the direction (zig-zag left or right up to 60 degrees)
            float randomAngle = Random.Range(-60f, 60f);
            runDir = Quaternion.Euler(0, randomAngle, 0) * runDir;

            Vector3 targetDest = transform.position + runDir * fleeDistance;

            // Find a valid point on the NavMesh using the Flee Area Mask!
            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetDest, out hit, 10f, fleeAreaMask))
            {
                agent.SetDestination(hit.position);
            }
        }
    }

    private void ChangeState(BoarState newState)
    {
        currentState = newState;
        
        if (newState == BoarState.Idle)
        {
            agent.isStopped = true;
            stateTimer = Random.Range(2f, 5f); // Stay idle for 2-5 seconds
        }
        else if (newState == BoarState.Wander)
        {
            agent.isStopped = false;
            agent.speed = wanderSpeed;
            
            wanderPathCount++;
            
            // Determine which mask to use for this wander path
            int currentMask = wanderAreaMask;
            
            // Every 3rd path, try to use the CloseToPlayer area!
            if (wanderPathCount >= 3)
            {
                currentMask = closeAreaMask;
                wanderPathCount = 0; // Reset counter
            }
            
            // Pick a random point within the Wander Radius
            Vector3 randomDir = Random.insideUnitSphere * wanderRadius;
            randomDir += transform.position;
            NavMeshHit hit;
            
            // Enforce that the wander point is inside the chosen Mask
            if (NavMesh.SamplePosition(randomDir, out hit, wanderRadius, currentMask))
            {
                agent.SetDestination(hit.position);
            }
            else if (currentMask == closeAreaMask)
            {
                // If it couldn't find a CloseToPlayer spot nearby, fallback to normal wandering
                if (NavMesh.SamplePosition(randomDir, out hit, wanderRadius, wanderAreaMask))
                {
                    agent.SetDestination(hit.position);
                }
            }
        }
        else if (newState == BoarState.Flee)
        {
            agent.isStopped = false;
            // Use injured speed if health is low, otherwise normal run speed
            agent.speed = isInjured ? injuredRunSpeed : normalRunSpeed;
            fleeRecalculateTimer = 0f; // Force immediate path calculation!
            totalFleeTime = 0f; // Reset flee timer
            UpdateFlee();
        }
    }

    private void PlayAnim(string animName)
    {
        if (string.IsNullOrEmpty(animName) || currentAnim == animName) return;
        currentAnim = animName;
        
        // If they use a trigger for death instead of a state name, handle it gracefully
        if (useBlendTree && animName == deathAnimName)
        {
            animator.SetTrigger(deathAnimName);
        }
        else
        {
            animator.CrossFadeInFixedTime(animName, 0.25f);
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null || currentState == BoarState.Dead) return;

        if (useBlendTree)
        {
            // Calculate how fast the boar is turning (Angular Velocity)
            float currentYRotation = transform.eulerAngles.y;
            float deltaY = Mathf.DeltaAngle(lastYRotation, currentYRotation);
            float angularVelocity = deltaY / Time.deltaTime;
            lastYRotation = currentYRotation;

            // Map the turn speed (e.g. max 120 degrees/sec) to a -1 to 1 range for the Animator
            float turnParam = Mathf.Clamp(angularVelocity / 120f, -1f, 1f);

            // Smoothly feed the physical speed and turn rate into the animator!
            animator.SetFloat(speedAnimParam, agent.velocity.magnitude);
            
            // We only set the Turn parameter if it's not empty, to avoid errors for users not using it
            if (!string.IsNullOrEmpty(turnAnimParam))
            {
                animator.SetFloat(turnAnimParam, turnParam);
            }
        }
        else
        {
            // If moving very slowly or stopped
            if (agent.velocity.magnitude < 0.1f)
            {
                PlayAnim(idleAnimName);
            }
            // If moving faster than a walk (e.g. running or limping fast)
            else if (agent.velocity.magnitude > wanderSpeed + 0.5f)
            {
                PlayAnim(runAnimName);
            }
            // If walking
            else
            {
                PlayAnim(walkAnimName);
            }
        }
    }

    public void OnDamaged()
    {
        wasShot = true;

        // If we get shot (even from far away), instantly start running away!
        if (currentState != BoarState.Flee && currentState != BoarState.Dead)
        {
            ChangeState(BoarState.Flee);
        }
    }

    public void OnDeath()
    {
        ChangeState(BoarState.Dead);
        
        agent.isStopped = true;
        agent.enabled = false; // Disable pathfinding

        if (animator != null)
        {
            StartCoroutine(DeathSequenceRoutine());
        }
        else
        {
            this.enabled = false;
        }
    }

    private System.Collections.IEnumerator DeathSequenceRoutine()
    {
        // 1. Play the falling over animation
        PlayAnim(deathAnimName);
        
        // 2. Wait for the death animation to finish (most death animations are 1 to 2 seconds long)
        yield return new WaitForSeconds(1.5f);
        
        // 3. Play the static resting/dead pose so it stays on the ground!
        PlayAnim("Resting");
        
        // 4. Disable the script so it never updates again
        this.enabled = false;
    }

    // Flee Zone Integration
    // We keep OnTriggerEnter just in case it DOES have a Rigidbody
    private void OnTriggerEnter(Collider other)
    {
        if (currentState == BoarState.Flee && other.CompareTag("FleeZone"))
        {
            StartCoroutine(FadeOutAndDestroy());
        }
    }

    private bool isFadingOut = false;

    private System.Collections.IEnumerator FadeOutAndDestroy()
    {
        if (isFadingOut) yield break;
        isFadingOut = true;
        
        agent.isStopped = true;
        agent.enabled = false;
        
        // Let the manager know it successfully fled
        BoarHuntingManager manager = FindObjectOfType<BoarHuntingManager>();
        if (manager != null)
        {
            manager.OnBoarFled(gameObject);
        }
        else
        {
            SendMessageUpwards("OnBoarFled", gameObject, SendMessageOptions.DontRequireReceiver);
        }
        
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        float fadeTime = 1.0f;
        float timer = 0f;

        // Note: Accessing .materials automatically creates unique instances for this specific boar!
        while (timer < fadeTime)
        {
            timer += Time.deltaTime;
            float progress = timer / fadeTime;

            foreach (Renderer r in renderers)
            {
                foreach (Material mat in r.materials)
                {
                    // Update alpha clipping property (Common names are _Cutoff or _AlphaClipThreshold)
                    if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", progress);
                    if (mat.HasProperty("_AlphaClipThreshold")) mat.SetFloat("_AlphaClipThreshold", progress);
                    
                    // Fallback scale down in case shader doesn't support alpha clip
                    transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, progress);
                }
            }
            yield return null;
        }

        Destroy(gameObject);
    }
}
