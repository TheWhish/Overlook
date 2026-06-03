using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(PlayerStamina))]
[DisallowMultipleComponent]
public class PlayerController : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");

    [Header("Movement")]
    [SerializeField, Min(0f)] private float walkSpeed = 0.5f;
    [SerializeField, Min(0f)] private float runSpeed = 0.75f;

    [Header("Stamina")]
    [SerializeField, Min(0f)] private float runStaminaCostPerSecond = 25f;

    private Rigidbody2D rb;
    private Animator animator;
    private PlayerStamina stamina;
    private PlayerAttack playerAttack;
    private PlayerHurtReaction hurtReaction;
    private KnockbackReceiver knockbackReceiver;

    private Vector2 moveInput;
    private float currentSpeed;
    private float lastHorizontalDirection = 1f;
    private bool isRunning;

    public bool HasMovementInput => moveInput.sqrMagnitude > 0.01f;
    public bool IsRunning => isRunning;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        stamina = GetComponent<PlayerStamina>();
        playerAttack = GetComponent<PlayerAttack>();
        hurtReaction = GetComponent<PlayerHurtReaction>();
        knockbackReceiver = GetComponent<KnockbackReceiver>();

        currentSpeed = walkSpeed;
    }

    private void Update()
    {
        if (IsMovementBlocked())
        {
            moveInput = Vector2.zero;
            isRunning = false;
            currentSpeed = 0f;
            SetMovementAnimationStopped();
            return;
        }

        ReadMovementInput();
        UpdateMovementSpeed();
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        if (IsMovementBlocked())
        {
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
        rb.linearVelocity = moveInput * currentSpeed;
    }

    private bool IsMovementBlocked()
    {
        return (hurtReaction != null && hurtReaction.BlocksMovement)
            || (knockbackReceiver != null && knockbackReceiver.IsKnockbackActive);
    }

    private void UpdateAnimation()
    {
        if (hurtReaction != null && hurtReaction.IsHurting)
        {
            SetMovementAnimationStopped();
            return;
        }

        if (playerAttack != null && playerAttack.IsAttacking)
        {
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

    private void OnValidate()
    {
        walkSpeed = Mathf.Max(0f, walkSpeed);
        runSpeed = Mathf.Max(walkSpeed, runSpeed);
        runStaminaCostPerSecond = Mathf.Max(0f, runStaminaCostPerSecond);
    }
}
