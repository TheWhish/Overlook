using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(PlayerStamina))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float walkSpeed = 0.5f;
    [SerializeField, Min(0f)] private float runSpeed = 0.75f;

    [Header("Stamina")]
    [SerializeField, Min(0f)] private float runStaminaCostPerSecond = 25f;

    [Header("Animation Parameters")]
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string horizontalParameter = "Horizontal";
    [SerializeField] private string isRunningParameter = "IsRunning";

    private Rigidbody2D rb;
    private Animator animator;
    private PlayerStamina stamina;

    private Vector2 moveInput;
    private float currentSpeed;
    private float lastHorizontalDirection = 1f;
    private bool isRunning;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        stamina = GetComponent<PlayerStamina>();

        currentSpeed = walkSpeed;
    }

    private void Update()
    {
        ReadMovementInput();
        UpdateMovementSpeed();
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
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
        bool hasMovementInput = moveInput.sqrMagnitude > 0.01f;
        bool wantsToRun = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        isRunning = false;

        if (hasMovementInput && wantsToRun && stamina.HasStamina)
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

    private void UpdateAnimation()
    {
        float movementAmount = moveInput.sqrMagnitude;

        animator.SetFloat(speedParameter, movementAmount);
        animator.SetBool(isRunningParameter, isRunning);

        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            lastHorizontalDirection = Mathf.Sign(moveInput.x);
            animator.SetFloat(horizontalParameter, lastHorizontalDirection);
        }
    }
}