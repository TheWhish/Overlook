using UnityEngine;

[DisallowMultipleComponent]
public abstract class EnemyAttack : MonoBehaviour
{
    public abstract float AttackRange { get; }
    public abstract bool IsReady { get; }
    public abstract bool IsAttacking { get; }
    public abstract float RemainingCooldown { get; }

    public abstract bool IsTargetInRange(Transform target);
    public abstract bool IsTargetInRange(Collider2D targetCollider);
    public abstract bool IsTargetInAttackArea(Collider2D targetCollider);
    public abstract bool TryAttack(Transform target);
    public abstract bool TryAttack(Collider2D targetCollider);
    public abstract void InterruptAttack(float cooldownMultiplier = 1f);
}
