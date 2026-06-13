using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(PlayerStamina))]
[DisallowMultipleComponent]
public class PlayerController : MonoBehaviour
{
    private const float BlockingNormalDotThreshold = -0.01f;
    private const float MovementEpsilon = 0.000001f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");

    [Header("Movement")]
    [SerializeField, Min(0f)] private float walkSpeed = 0.5f;
    [SerializeField, Min(0f)] private float runSpeed = 0.75f;

    [Header("Collision")]
    [SerializeField] private LayerMask movementCollisionLayers = ~0;
    [SerializeField, Min(0f)] private float collisionSkinWidth = 0.0015f;
    [SerializeField, Range(1, 4)] private int collisionSlideIterations = 3;
    [SerializeField, Min(0.000001f)] private float minimumMoveDistance = 0.00001f;

    [Header("Stamina")]
    [SerializeField, Min(0f)] private float runStaminaCostPerSecond = 25f;

    private readonly RaycastHit2D[] movementCastHits = new RaycastHit2D[16];

    private Rigidbody2D rb;
    private BoxCollider2D bodyCollider;
    private Animator animator;
    private PlayerStamina stamina;
    private PlayerAttack playerAttack;
    private PlayerHurtReaction hurtReaction;
    private KnockbackReceiver knockbackReceiver;
    private ContactFilter2D movementContactFilter;

    private Vector2 moveInput;
    private float currentSpeed;
    private float lastHorizontalDirection = 1f;
    private bool isRunning;
    private bool warnedAboutFullMovementCastBuffer;

    public bool HasMovementInput => moveInput.sqrMagnitude > 0.01f;
    public bool IsRunning => isRunning;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<BoxCollider2D>();
        animator = GetComponent<Animator>();
        stamina = GetComponent<PlayerStamina>();
        playerAttack = GetComponent<PlayerAttack>();
        hurtReaction = GetComponent<PlayerHurtReaction>();
        knockbackReceiver = GetComponent<KnockbackReceiver>();

        ConfigureMovementContactFilter();
        currentSpeed = walkSpeed;
    }

    private void Update()
    {
        if (GameplayInputGate.IsGameplayInputBlocked)
        {
            StopMovement();
            return;
        }

        if (IsKnockbackActive())
        {
            StopMovement(stopRigidbody: false);
            return;
        }

        if (IsMovementBlocked())
        {
            StopMovement();
            return;
        }

        ReadMovementInput();
        UpdateMovementSpeed();
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        if (GameplayInputGate.IsGameplayInputBlocked)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            return;
        }

        if (IsKnockbackActive())
        {
            return;
        }

        if (IsMovementBlocked())
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            return;
        }

        Move();
    }

    private void ReadMovementInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        moveInput = new Vector2(horizontal, vertical);

        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }
    }

    private void UpdateMovementSpeed()
    {
        bool wantsToRun = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        isRunning = false;

        if (HasMovementInput && wantsToRun && stamina.HasStamina)
        {
            float staminaCost = runStaminaCostPerSecond * Time.deltaTime;

            if (stamina.TrySpend(staminaCost))
            {
                isRunning = true;
            }
        }

        currentSpeed = isRunning ? runSpeed : walkSpeed;
    }

    private void Move()
    {
        if (rb == null)
        {
            return;
        }

        if (!HasMovementInput || currentSpeed <= 0f)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 desiredDelta = moveInput * (currentSpeed * Time.fixedDeltaTime);

        if (desiredDelta.sqrMagnitude <= minimumMoveDistance * minimumMoveDistance)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 resolvedDelta = ResolveMovementDelta(desiredDelta);
        rb.linearVelocity = Vector2.zero;

        if (resolvedDelta.sqrMagnitude <= minimumMoveDistance * minimumMoveDistance)
        {
            return;
        }

        rb.MovePosition(rb.position + resolvedDelta);
    }

    private Vector2 ResolveMovementDelta(Vector2 desiredDelta)
    {
        if (bodyCollider == null || movementCollisionLayers.value == 0)
        {
            return desiredDelta;
        }

        Vector2 resolvedDelta = Vector2.zero;
        Vector2 remainingDelta = desiredDelta;
        int iterations = Mathf.Max(1, collisionSlideIterations);
        float minimumMoveDistanceSqr = minimumMoveDistance * minimumMoveDistance;

        for (int i = 0; i < iterations; i++)
        {
            if (remainingDelta.sqrMagnitude <= minimumMoveDistanceSqr)
            {
                break;
            }

            if (!TryFindBlockingMovementHit(remainingDelta, out RaycastHit2D hit))
            {
                resolvedDelta += remainingDelta;
                break;
            }

            float remainingDistance = remainingDelta.magnitude;
            Vector2 direction = remainingDelta / remainingDistance;
            float moveDistance = Mathf.Clamp(hit.distance - collisionSkinWidth, 0f, remainingDistance);
            Vector2 moveDelta = direction * moveDistance;
            resolvedDelta += moveDelta;

            float leftoverDistance = remainingDistance - moveDistance;
            if (leftoverDistance <= minimumMoveDistance)
            {
                break;
            }

            Vector2 leftoverDelta = direction * leftoverDistance;
            remainingDelta = GetSlideDelta(leftoverDelta, hit.normal);
        }

        return resolvedDelta;
    }

    private bool TryFindBlockingMovementHit(Vector2 delta, out RaycastHit2D bestHit)
    {
        bestHit = default;

        float distance = delta.magnitude;
        if (distance <= MovementEpsilon)
        {
            return false;
        }

        Vector2 direction = delta / distance;
        float castDistance = distance + collisionSkinWidth;
        int hitCount = bodyCollider.Cast(direction, movementContactFilter, movementCastHits, castDistance);

        if (hitCount >= movementCastHits.Length && !warnedAboutFullMovementCastBuffer)
        {
            warnedAboutFullMovementCastBuffer = true;
            Debug.LogWarning(
                $"[PlayerController] '{name}' filled movement cast buffer ({movementCastHits.Length}). Increase the buffer if movement blockers are missed.",
                this);
        }

        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = movementCastHits[i];

            if (!IsBlockingMovementHit(hit, direction))
            {
                continue;
            }

            float hitDistance = Mathf.Max(0f, hit.distance);
            if (hitDistance < bestDistance)
            {
                bestDistance = hitDistance;
                bestHit = hit;
            }
        }

        return bestHit.collider != null;
    }

    private bool IsBlockingMovementHit(RaycastHit2D hit, Vector2 direction)
    {
        Collider2D hitCollider = hit.collider;

        if (hitCollider == null || hitCollider == bodyCollider || hitCollider.isTrigger)
        {
            return false;
        }

        if (hitCollider.attachedRigidbody != null && hitCollider.attachedRigidbody == rb)
        {
            return false;
        }

        Transform hitTransform = hitCollider.transform;
        if (hitTransform == transform || hitTransform.IsChildOf(transform))
        {
            return false;
        }

        if (hit.normal.sqrMagnitude <= MovementEpsilon)
        {
            return true;
        }

        float normalDot = Vector2.Dot(hit.normal.normalized, direction);
        return normalDot < BlockingNormalDotThreshold;
    }

    private static Vector2 GetSlideDelta(Vector2 delta, Vector2 normal)
    {
        if (normal.sqrMagnitude <= MovementEpsilon)
        {
            return Vector2.zero;
        }

        normal.Normalize();
        float intoSurface = Vector2.Dot(delta, normal);

        if (intoSurface >= 0f)
        {
            return delta;
        }

        return delta - normal * intoSurface;
    }

    private bool IsMovementBlocked()
    {
        return hurtReaction != null && hurtReaction.BlocksMovement;
    }

    private bool IsKnockbackActive()
    {
        return knockbackReceiver != null && knockbackReceiver.IsKnockbackActive;
    }

    private void UpdateAnimation()
    {
        if (hurtReaction != null && hurtReaction.IsHurting)
        {
            SetMovementAnimationStopped();
            return;
        }

        float movementAmount = moveInput.sqrMagnitude;

        animator.SetFloat(SpeedHash, movementAmount);
        animator.SetBool(IsRunningHash, isRunning);

        if (playerAttack != null && playerAttack.IsFacingLocked)
        {
            lastHorizontalDirection = playerAttack.LockedHorizontalDirection;
            animator.SetFloat(HorizontalHash, lastHorizontalDirection);
            return;
        }

        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            lastHorizontalDirection = Mathf.Sign(moveInput.x);
            animator.SetFloat(HorizontalHash, lastHorizontalDirection);
        }
    }

    private void SetMovementAnimationStopped()
    {
        animator.SetFloat(SpeedHash, 0f);
        animator.SetBool(IsRunningHash, false);
    }

    private void StopMovement(bool stopRigidbody = true)
    {
        moveInput = Vector2.zero;
        isRunning = false;
        currentSpeed = 0f;

        if (stopRigidbody && rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        SetMovementAnimationStopped();
    }

    private void ConfigureMovementContactFilter()
    {
        movementContactFilter = new ContactFilter2D();
        movementContactFilter.SetLayerMask(movementCollisionLayers);
        movementContactFilter.useTriggers = false;
    }

    private void OnValidate()
    {
        walkSpeed = Mathf.Max(0f, walkSpeed);
        runSpeed = Mathf.Max(walkSpeed, runSpeed);
        collisionSkinWidth = Mathf.Max(0f, collisionSkinWidth);
        collisionSlideIterations = Mathf.Clamp(collisionSlideIterations, 1, 4);
        minimumMoveDistance = Mathf.Max(0.000001f, minimumMoveDistance);
        runStaminaCostPerSecond = Mathf.Max(0f, runStaminaCostPerSecond);
    }
}
