using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyCircleAttack : EnemyAttack
{
    private enum AttackAreaShape
    {
        Circle,
        Sector
    }

    private const int MaxHitColliders = 12;
    private const string DefaultAttackTriggerParameter = "Attack";
    private const string DefaultHorizontalParameter = "Horizontal";

    [Header("Attack")]
    [SerializeField, Min(0f)] private float damage = 10f;
    [SerializeField, Min(0f)] private float attackRange = 0.36f;
    [SerializeField, Min(0f)] private float attackCooldown = 0.65f;
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private bool requireTargetInsideHitboxToStart = true;
    [SerializeField, Min(0f)] private float attackStartPadding = 0.02f;

    [Header("Expanding Hitbox")]
    [SerializeField] private Vector2 attackOriginOffset = new Vector2(0f, 0.04f);
    [SerializeField] private AttackAreaShape attackAreaShape = AttackAreaShape.Circle;
    [SerializeField, Min(0f)] private float startRadius = 0.07f;
    [SerializeField, Min(0f)] private float endRadius = 0.38f;
    [SerializeField, Range(1f, 180f)] private float sectorAngle = 165f;
    [SerializeField, Range(-180f, 180f)] private float rightSectorRotationOffset = 0f;
    [SerializeField, Range(-180f, 180f)] private float leftSectorRotationOffset = 0f;
    [SerializeField, Min(0f)] private float windupTime = 0.2f;
    [SerializeField, Min(0.01f)] private float expansionDuration = 0.55f;
    [SerializeField, Min(0f)] private float recoveryTime = 0.15f;

    [Header("Animator")]
    [SerializeField] private string attackTriggerParameter = DefaultAttackTriggerParameter;
    [SerializeField] private string horizontalParameter = DefaultHorizontalParameter;

    private readonly Collider2D[] hitColliders = new Collider2D[MaxHitColliders];
    private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

    private ContactFilter2D targetFilter;
    private Animator animator;
    private EnemyMotor motor;
    private EnemyDamageReaction damageReaction;
    private Coroutine attackRoutine;
    private float nextAttackTime;
    private float currentAttackRadius;
    private Vector2 attackFacingDirection = Vector2.right;
    private Vector2 attackSectorDirection = Vector2.right;

    private int attackTriggerHash;
    private int horizontalHash;
    private bool hasAttackTrigger;
    private bool hasHorizontalParameter;
    private bool warnedAboutMissingAttackTrigger;

    public override float AttackRange => attackRange;
    public override bool IsReady => attackRoutine == null && Time.time >= nextAttackTime;
    public override bool IsAttacking => attackRoutine != null;
    public float CurrentAttackRadius => currentAttackRadius;
    public override float RemainingCooldown => Mathf.Max(0f, nextAttackTime - Time.time);

    private void Awake()
    {
        animator = GetComponent<Animator>();
        motor = GetComponent<EnemyMotor>();
        damageReaction = GetComponent<EnemyDamageReaction>();

        ConfigureFilter();
        CacheAnimatorParameters();
    }

    private void OnDisable()
    {
        ClearAttackState(stopRoutine: true);
    }

    public override bool IsTargetInRange(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        if (target.TryGetComponent(out Collider2D targetCollider))
        {
            return IsTargetInRange(targetCollider);
        }

        Vector2 origin = GetAttackOrigin();
        float sqrRange = attackRange * attackRange;
        float sqrDistance = ((Vector2)target.position - origin).sqrMagnitude;

        return sqrDistance <= sqrRange;
    }

    public override bool IsTargetInRange(Collider2D targetCollider)
    {
        if (targetCollider == null)
        {
            return false;
        }

        Vector2 origin = GetAttackOrigin();
        Vector2 closestPoint = targetCollider.ClosestPoint(origin);
        float sqrRange = attackRange * attackRange;

        return (closestPoint - origin).sqrMagnitude <= sqrRange;
    }

    public override bool IsTargetInAttackArea(Collider2D targetCollider)
    {
        if (targetCollider == null || !IsTargetInRange(targetCollider))
        {
            return false;
        }

        return IsColliderInsideAttackArea(
            GetAttackOrigin(),
            targetCollider,
            GetAttackAreaDirection(targetCollider),
            endRadius + attackStartPadding);
    }

    public override bool TryAttack(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        if (target.TryGetComponent(out Collider2D targetCollider))
        {
            return TryAttack(targetCollider);
        }

        if (!IsReady || !IsTargetInRange(target) || !IsInTargetLayer(target.gameObject))
        {
            return false;
        }

        FacePoint(target.position);
        attackRoutine = StartCoroutine(AttackRoutine());

        return true;
    }

    public override bool TryAttack(Collider2D targetCollider)
    {
        if (!IsReady
            || targetCollider == null
            || !IsInTargetLayer(targetCollider.gameObject)
            || !IsTargetInRange(targetCollider)
            || (requireTargetInsideHitboxToStart && !IsTargetInAttackArea(targetCollider)))
        {
            return false;
        }

        FacePoint(GetTargetPoint(targetCollider));
        attackRoutine = StartCoroutine(AttackRoutine());

        return true;
    }

    public override void InterruptAttack(float cooldownMultiplier = 1f)
    {
        ClearAttackState(stopRoutine: true);
        nextAttackTime = Time.time + attackCooldown * Mathf.Max(0f, cooldownMultiplier);
    }

    private IEnumerator AttackRoutine()
    {
        damagedTargets.Clear();
        currentAttackRadius = 0f;

        if (motor != null)
        {
            motor.Stop();
        }

        TriggerAttackAnimation();

        float elapsedTime = 0f;
        while (elapsedTime < windupTime)
        {
            if (ShouldInterruptAttack())
            {
                FinishAttack(interrupted: true);
                yield break;
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        elapsedTime = 0f;
        while (elapsedTime < expansionDuration)
        {
            if (ShouldInterruptAttack())
            {
                FinishAttack(interrupted: true);
                yield break;
            }

            float normalizedTime = Mathf.Clamp01(elapsedTime / expansionDuration);
            float easedTime = Mathf.SmoothStep(0f, 1f, normalizedTime);

            currentAttackRadius = Mathf.Lerp(startRadius, endRadius, easedTime);
            ApplyDamageInCurrentArea();

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        currentAttackRadius = endRadius;
        ApplyDamageInCurrentArea();
        currentAttackRadius = 0f;

        elapsedTime = 0f;
        while (elapsedTime < recoveryTime)
        {
            if (ShouldInterruptAttack())
            {
                FinishAttack(interrupted: true);
                yield break;
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        FinishAttack(interrupted: false);
    }

    private void ApplyDamageInCurrentArea()
    {
        Vector2 origin = GetAttackOrigin();
        int hitCount = Physics2D.OverlapCircle(origin, currentAttackRadius, targetFilter, hitColliders);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = hitColliders[i];
            hitColliders[i] = null;

            if (hitCollider == null
                || !IsColliderInsideAttackArea(origin, hitCollider, attackSectorDirection, currentAttackRadius))
            {
                continue;
            }

            IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();

            if (damageable == null || !damageable.CanTakeDamage || damagedTargets.Contains(damageable))
            {
                continue;
            }

            Vector2 hitPoint = hitCollider.ClosestPoint(origin);
            Vector2 hitDirection = (Vector2)hitCollider.transform.position - (Vector2)transform.position;
            DamageInfo damageInfo = new DamageInfo(damage, gameObject, hitPoint, hitDirection);

            damageable.TakeDamage(damageInfo);
            damagedTargets.Add(damageable);
        }
    }

    private bool IsColliderInsideAttackArea(Vector2 origin, Collider2D hitCollider, Vector2 areaDirection, float radius)
    {
        return attackAreaShape == AttackAreaShape.Circle
            ? IsColliderInsideCircle(origin, hitCollider, radius)
            : IsColliderInsideSector(origin, hitCollider, areaDirection, radius);
    }

    private static bool IsColliderInsideCircle(Vector2 origin, Collider2D hitCollider, float radius)
    {
        if (hitCollider == null || radius <= 0f)
        {
            return false;
        }

        Vector2 closestPoint = hitCollider.ClosestPoint(origin);
        return (closestPoint - origin).sqrMagnitude <= radius * radius;
    }

    private bool IsColliderInsideSector(Vector2 origin, Collider2D hitCollider, Vector2 sectorDirection, float radius)
    {
        if (radius <= 0f)
        {
            return false;
        }

        Bounds bounds = hitCollider.bounds;
        Vector2 center = bounds.center;

        if (IsPointInsideSector(origin, center, sectorDirection, radius))
        {
            return true;
        }

        Vector2 closestPoint = hitCollider.ClosestPoint(origin);

        if (IsPointInsideSector(origin, closestPoint, sectorDirection, radius))
        {
            return true;
        }

        Vector2 min = bounds.min;
        Vector2 max = bounds.max;

        return IsPointInsideSector(origin, new Vector2(min.x, min.y), sectorDirection, radius)
            || IsPointInsideSector(origin, new Vector2(min.x, max.y), sectorDirection, radius)
            || IsPointInsideSector(origin, new Vector2(max.x, min.y), sectorDirection, radius)
            || IsPointInsideSector(origin, new Vector2(max.x, max.y), sectorDirection, radius);
    }

    private bool IsPointInsideSector(Vector2 origin, Vector2 point, Vector2 sectorDirection, float radius)
    {
        Vector2 toPoint = point - origin;

        if (toPoint.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        if (toPoint.sqrMagnitude > radius * radius)
        {
            return false;
        }

        float halfAngle = sectorAngle * 0.5f;
        float angleDotThreshold = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
        float directionDot = Vector2.Dot(sectorDirection.normalized, toPoint.normalized);

        return directionDot >= angleDotThreshold;
    }

    private void FacePoint(Vector2 point)
    {
        Vector2 facingDirection = GetFacingDirectionToPoint(point);

        attackFacingDirection = facingDirection;
        attackSectorDirection = GetSectorDirection(attackFacingDirection);

        if (motor != null)
        {
            motor.FaceDirection(attackFacingDirection);
        }
    }

    private void TriggerAttackAnimation()
    {
        if (animator == null)
        {
            return;
        }

        if (hasHorizontalParameter)
        {
            animator.SetFloat(horizontalHash, attackFacingDirection.x < 0f ? -1f : 1f);
        }

        if (hasAttackTrigger)
        {
            animator.ResetTrigger(attackTriggerHash);
            animator.SetTrigger(attackTriggerHash);
        }
        else if (!warnedAboutMissingAttackTrigger)
        {
            Debug.LogWarning($"Animator on {name} has no '{attackTriggerParameter}' trigger parameter.", this);
            warnedAboutMissingAttackTrigger = true;
        }
    }

    private void FinishAttack(bool interrupted)
    {
        ClearAttackState(stopRoutine: false);

        nextAttackTime = Time.time + (interrupted ? attackCooldown * 0.5f : attackCooldown);
    }

    private void ClearAttackState(bool stopRoutine)
    {
        if (stopRoutine && attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
        }

        currentAttackRadius = 0f;
        damagedTargets.Clear();
        attackRoutine = null;
    }

    private bool ShouldInterruptAttack()
    {
        return damageReaction != null && damageReaction.BlocksMovement;
    }

    private Vector2 GetAttackOrigin()
    {
        return (Vector2)transform.position + attackOriginOffset;
    }

    private bool IsInTargetLayer(GameObject target)
    {
        return (targetLayers.value & (1 << target.layer)) != 0;
    }

    private Vector2 GetFacingDirectionToPoint(Vector2 point)
    {
        Vector2 toPoint = point - (Vector2)transform.position;
        float horizontalDirection = toPoint.x < 0f ? -1f : 1f;

        return new Vector2(horizontalDirection, 0f);
    }

    private static Vector2 GetTargetPoint(Collider2D targetCollider)
    {
        return targetCollider != null
            ? targetCollider.bounds.center
            : Vector2.zero;
    }

    private Vector2 GetAttackAreaDirection(Collider2D targetCollider)
    {
        Vector2 targetPoint = GetTargetPoint(targetCollider);
        Vector2 facingDirection = GetFacingDirectionToPoint(targetPoint);

        return GetSectorDirection(facingDirection);
    }

    private void ConfigureFilter()
    {
        targetFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true
        };

        targetFilter.SetLayerMask(targetLayers);
    }

    private void CacheAnimatorParameters()
    {
        hasAttackTrigger = false;
        hasHorizontalParameter = false;
        warnedAboutMissingAttackTrigger = false;

        if (animator == null)
        {
            return;
        }

        attackTriggerHash = Animator.StringToHash(attackTriggerParameter);
        horizontalHash = Animator.StringToHash(horizontalParameter);

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == attackTriggerHash && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                hasAttackTrigger = true;
            }
            else if (parameter.nameHash == horizontalHash && parameter.type == AnimatorControllerParameterType.Float)
            {
                hasHorizontalParameter = true;
            }
        }
    }

    private void OnValidate()
    {
        damage = Mathf.Max(0f, damage);
        attackRange = Mathf.Max(0f, attackRange);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        attackStartPadding = Mathf.Max(0f, attackStartPadding);
        startRadius = Mathf.Max(0f, startRadius);
        endRadius = Mathf.Max(startRadius, endRadius);
        windupTime = Mathf.Max(0f, windupTime);
        expansionDuration = Mathf.Max(0.01f, expansionDuration);
        recoveryTime = Mathf.Max(0f, recoveryTime);

        if (Application.isPlaying)
        {
            ConfigureFilter();
            CacheAnimatorParameters();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 origin = GetAttackOrigin();
        Vector2 facing = Application.isPlaying
            ? attackSectorDirection
            : GetSectorDirection(Vector2.right);

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(origin, attackRange);

        Gizmos.color = new Color(1f, 0.25f, 0.15f, 0.65f);
        DrawAttackAreaGizmo(origin, facing, endRadius);

        if (Application.isPlaying && currentAttackRadius > 0f)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.75f, 0.8f);
            DrawAttackAreaGizmo(origin, facing, currentAttackRadius);
        }
    }

    private void DrawAttackAreaGizmo(Vector2 origin, Vector2 facing, float radius)
    {
        if (attackAreaShape == AttackAreaShape.Circle)
        {
            Gizmos.DrawWireSphere(origin, radius);
            return;
        }

        DrawSectorGizmo(origin, facing, radius);
    }

    private void DrawSectorGizmo(Vector2 origin, Vector2 facing, float radius)
    {
        if (radius <= 0f)
        {
            return;
        }

        const int SegmentCount = 24;
        float halfAngle = sectorAngle * 0.5f;
        float baseAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
        Vector2 previousPoint = origin;

        for (int i = 0; i <= SegmentCount; i++)
        {
            float angle = baseAngle - halfAngle + sectorAngle * i / SegmentCount;
            Vector2 direction = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad));
            Vector2 point = origin + direction * radius;

            if (i > 0)
            {
                Gizmos.DrawLine(previousPoint, point);
            }

            Gizmos.DrawLine(origin, point);
            previousPoint = point;
        }
    }

    private Vector2 GetSectorDirection(Vector2 baseDirection)
    {
        if (baseDirection.sqrMagnitude <= 0.0001f)
        {
            baseDirection = Vector2.right;
        }

        float rotationOffset = baseDirection.x < 0f
            ? leftSectorRotationOffset
            : rightSectorRotationOffset;
        float angle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg + rotationOffset;

        return new Vector2(
            Mathf.Cos(angle * Mathf.Deg2Rad),
            Mathf.Sin(angle * Mathf.Deg2Rad));
    }
}
