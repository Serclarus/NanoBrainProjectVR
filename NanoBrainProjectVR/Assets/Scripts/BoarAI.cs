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

    [Header("Detection Settings")]
    public Transform playerTransform;
    [Tooltip("How close the player needs to be before the boar runs away.")]
    public float detectionRadius = 15f;
    [Tooltip("How far the boar tries to run when escaping.")]
    public float fleeDistance = 30f;

    [Header("Health Integration")]
    public AnimalHealth healthScript;
    [Tooltip("Health percentage (0.0 to 1.0) at which the boar gets injured and slows down.")]
    [Range(0f, 1f)]
    public float injuryThreshold = 0.3f;

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

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();

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

        ChangeState(BoarState.Wander);
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

        UpdateAnimator();
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
        // If we ran far enough away, go back to wandering
        else if (dist > detectionRadius * 1.5f && currentState == BoarState.Flee && !agent.pathPending && agent.remainingDistance < 2f)
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

    private void UpdateFlee()
    {
        fleeRecalculateTimer -= Time.deltaTime;

        // Continuously update the flee target away from the player, but zig-zag randomly!
        if (playerTransform != null && fleeRecalculateTimer <= 0f)
        {
            fleeRecalculateTimer = Random.Range(1.5f, 3.0f); // Change direction every 1.5 to 3 seconds

            // Calculate direction AWAY from player
            Vector3 runDir = (transform.position - playerTransform.position).normalized;
            
            // Add randomness to the direction (zig-zag left or right up to 60 degrees)
            float randomAngle = Random.Range(-60f, 60f);
            runDir = Quaternion.Euler(0, randomAngle, 0) * runDir;

            Vector3 targetDest = transform.position + runDir * fleeDistance;

            // Find a valid point on the NavMesh
            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetDest, out hit, 10f, NavMesh.AllAreas))
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
            
            // Pick a random point nearby
            Vector3 randomDir = Random.insideUnitSphere * 10f;
            randomDir += transform.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDir, out hit, 10f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
        else if (newState == BoarState.Flee)
        {
            agent.isStopped = false;
            // Use injured speed if health is low, otherwise normal run speed
            agent.speed = isInjured ? injuredRunSpeed : normalRunSpeed;
            fleeRecalculateTimer = 0f; // Force immediate path calculation!
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
}
