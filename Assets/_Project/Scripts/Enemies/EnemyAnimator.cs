using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(EnemyMotor))]
[DisallowMultipleComponent]
public class EnemyAnimator : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");

    [Header("Direction")]
    [SerializeField, Min(0f)] private float horizontalDeadZone = 0.01f;

    private Animator animator;
    private EnemyMotor motor;
    private EnemyDamageReaction damageReaction;
    private float lastHorizontalDirection = 1f;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        motor = GetComponent<EnemyMotor>();
        damageReaction = GetComponent<EnemyDamageReaction>();
    }

    private void Update()
    {
        if (damageReaction != null && (damageReaction.IsReactingToDamage || damageReaction.IsDead))
        {
            return;
        }

        Vector2 direction = motor.AnimationDirection;

        animator.SetFloat(SpeedHash, motor.ShouldPlayMovementAnimation ? 1f : 0f);

        if (Mathf.Abs(direction.x) > horizontalDeadZone)
        {
            lastHorizontalDirection = Mathf.Sign(direction.x);
            animator.SetFloat(HorizontalHash, lastHorizontalDirection);
        }
    }
}
