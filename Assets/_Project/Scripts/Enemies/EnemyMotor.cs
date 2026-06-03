using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]
public class EnemyMotor : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 0.35f;

    private Rigidbody2D rb;
    private KnockbackReceiver knockbackReceiver;
    private Vector2 desiredDirection;
    private Vector2 lastMoveDirection = Vector2.right;
    private float currentSpeed;

    public Vector2 DesiredDirection => desiredDirection;
    public Vector2 LastMoveDirection => lastMoveDirection;
    public Vector2 AnimationDirection => lastMoveDirection;
    public float MoveSpeed => currentSpeed;
    public bool HasMovementInput => desiredDirection.sqrMagnitude > 0.0001f;
    public bool ShouldPlayMovementAnimation => HasMovementInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        knockbackReceiver = GetComponent<KnockbackReceiver>();
        currentSpeed = moveSpeed;
    }

    private void FixedUpdate()
    {
        if (knockbackReceiver != null && knockbackReceiver.IsKnockbackActive)
        {
            return;
        }

        rb.linearVelocity = desiredDirection * currentSpeed;
    }

    public void SetMoveDirection(Vector2 direction)
    {
        SetMoveVelocity(direction, moveSpeed);
    }

    public void SetMoveVelocity(Vector2 direction, float speed)
    {
        desiredDirection = direction.sqrMagnitude > 1f
            ? direction.normalized
            : direction;

        currentSpeed = Mathf.Max(speed, 0f);

        if (desiredDirection.sqrMagnitude > 0.0001f)
        {
            lastMoveDirection = desiredDirection;
        }
    }

    public void Stop()
    {
        desiredDirection = Vector2.zero;
        currentSpeed = 0f;
    }

    public void FaceDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        lastMoveDirection = direction.normalized;
    }
}
