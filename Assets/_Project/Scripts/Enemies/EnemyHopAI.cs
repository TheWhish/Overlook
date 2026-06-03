using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyMotor))]
[RequireComponent(typeof(EnemyTargetSensor))]
[DisallowMultipleComponent]
public class EnemyHopAI : MonoBehaviour
{
    private static readonly float[] DirectionFallbackAngles =
    {
        35f,
        -35f,
        70f,
        -70f,
        110f,
        -110f,
        180f
    };

    [Header("Hop")]
    [SerializeField, Min(0.01f)] private float hopDuration = 1f;
    [SerializeField, Min(0f)] private float hopDistance = 0.35f;
    [SerializeField, Range(0f, 1f)] private float linearMotionBlend = 0.18f;

    [Header("Idle")]
    [SerializeField, Min(0f)] private float idleDurationMin = 0.4f;
    [SerializeField, Min(0f)] private float idleDurationMax = 1.2f;

    [Header("Aggression")]
    [SerializeField, Min(0f)] private float aggressionAfterDamageTime = 1.25f;
    [SerializeField, Min(0f)] private float aggressiveIdleDurationMax = 0.08f;
    [SerializeField, Min(0.1f)] private float aggressiveHopDurationMultiplier = 0.85f;
    [SerializeField, Min(0f)] private float aggressiveHopDistanceMultiplier = 1.15f;
    [SerializeField, Range(0f, 1f)] private float approachSideStepBlend = 0.2f;
    [SerializeField, Range(0f, 1f)] private float aggressiveApproachSideStepBlend = 0.45f;
    [SerializeField, Range(0f, 1f)] private float enrageHealthPercent = 0.35f;
    [SerializeField, Min(0f)] private float enrageIdleDurationMax = 0.12f;
    [SerializeField, Min(0.1f)] private float enrageHopDurationMultiplier = 0.8f;
    [SerializeField, Min(0f)] private float enrageHopDistanceMultiplier = 1.2f;

    [Header("Direction")]
    [SerializeField] private bool allowDiagonalMovement = true;

    [Header("Targeting")]
    [SerializeField] private bool chaseTarget = true;
    [SerializeField, Min(0f)] private float attackPauseTime = 0.15f;
    [SerializeField] private bool retreatWhileAttackOnCooldown = true;
    [SerializeField, Range(0f, 1f)] private float retreatSideStepBlend = 0.35f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask obstacleLayers = 1 << 9;
    [SerializeField, Min(0f)] private float obstacleProbeRadius = 0.06f;
    [SerializeField, Min(0f)] private float obstacleProbePadding = 0.08f;
    [SerializeField, Range(1, 8)] private int directionFallbackAttempts = 7;
    [SerializeField, Min(0f)] private float blockedIdleDuration = 0.12f;
    [SerializeField, Min(0f)] private float obstacleEscapeProbeRadius = 0.18f;
    [SerializeField, Min(0f)] private float obstacleEscapeDistance = 0.18f;
    [SerializeField, Min(0.1f)] private float obstacleEscapeDurationMultiplier = 0.65f;

    [Header("Adaptive Pressure")]
    [SerializeField, Range(0f, 1f)] private float playerLowHealthPercent = 0.35f;
    [SerializeField, Min(0.1f)] private float playerLowHealthIdleMultiplier = 0.55f;
    [SerializeField, Min(0.1f)] private float playerLowHealthHopDurationMultiplier = 0.9f;
    [SerializeField, Min(0f)] private float playerLowHealthHopDistanceMultiplier = 1.1f;
    [SerializeField, Range(0f, 1f)] private float selfCriticalHealthPercent = 0.22f;
    [SerializeField, Range(0f, 1f)] private float selfCriticalRetreatChance = 0.45f;
    [SerializeField, Range(0f, 1f)] private float selfCriticalSideStepBlend = 0.72f;

    private EnemyMotor motor;
    private EnemyTargetSensor targetSensor;
    private EnemyMeleeAttack meleeAttack;
    private EnemyDamageReaction damageReaction;
    private Health health;
    private Collider2D bodyCollider;
    private Coroutine hopRoutine;

    private void Awake()
    {
        motor = GetComponent<EnemyMotor>();
        targetSensor = GetComponent<EnemyTargetSensor>();
        meleeAttack = GetComponent<EnemyMeleeAttack>();
        damageReaction = GetComponent<EnemyDamageReaction>();
        health = GetComponent<Health>();
        bodyCollider = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        hopRoutine = StartCoroutine(HopLoop());
    }

    private void OnDisable()
    {
        if (hopRoutine != null)
        {
            StopCoroutine(hopRoutine);
            hopRoutine = null;
        }

        if (motor != null)
        {
            motor.Stop();
        }
    }

    private IEnumerator HopLoop()
    {
        while (enabled)
        {
            motor.Stop();

            if (!IsMovementBlocked() && TryAttackCurrentTarget())
            {
                yield return WaitAfterAttack();
                continue;
            }

            float idleDuration = GetIdleDuration();
            yield return WaitForIdle(idleDuration);

            if (IsMovementBlocked())
            {
                yield return WaitUntilMovementAllowed();
                continue;
            }

            if (TryAttackCurrentTarget())
            {
                yield return WaitAfterAttack();
                continue;
            }

            Vector2 hopDirection = GetHopDirection();
            float currentHopDuration = GetCurrentHopDuration();
            float currentHopDistance = GetCurrentHopDistance();
            bool isEscapingObstacle = false;

            hopDirection = GetSafeHopDirection(hopDirection, currentHopDistance);

            if (hopDirection.sqrMagnitude <= 0.0001f)
            {
                if (!TryGetObstacleEscapeDirection(out hopDirection))
                {
                    yield return WaitForIdle(blockedIdleDuration);
                    continue;
                }

                currentHopDistance = Mathf.Min(currentHopDistance, obstacleEscapeDistance);
                currentHopDuration *= obstacleEscapeDurationMultiplier;
                isEscapingObstacle = true;
            }

            float elapsedTime = 0f;

            while (elapsedTime < currentHopDuration)
            {
                if (IsMovementBlocked())
                {
                    break;
                }

                float normalizedTime = Mathf.Clamp01(elapsedTime / currentHopDuration);
                float hopSpeed = GetHopSpeed(normalizedTime, currentHopDuration, currentHopDistance);

                if (!isEscapingObstacle && IsPathBlocked(hopDirection, obstacleProbePadding))
                {
                    break;
                }

                motor.SetMoveVelocity(hopDirection, hopSpeed);

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            motor.Stop();

            if (IsMovementBlocked())
            {
                yield return WaitUntilMovementAllowed();
                continue;
            }

            if (TryAttackCurrentTarget())
            {
                yield return WaitAfterAttack();
            }
        }
    }

    private float GetHopSpeed(float normalizedTime, float currentHopDuration, float currentHopDistance)
    {
        float averageSpeed = currentHopDistance / currentHopDuration;
        float easedVelocityMultiplier = Mathf.Sin(normalizedTime * Mathf.PI) * (Mathf.PI * 0.5f);
        float hopVelocityMultiplier = Mathf.Lerp(easedVelocityMultiplier, 1f, linearMotionBlend);

        return averageSpeed * hopVelocityMultiplier;
    }

    private float GetIdleDuration()
    {
        if (IsAggressive())
        {
            return Random.Range(0f, aggressiveIdleDurationMax) * GetTargetPressureIdleMultiplier();
        }

        if (IsEnraged())
        {
            return Random.Range(0f, enrageIdleDurationMax) * GetTargetPressureIdleMultiplier();
        }

        return Random.Range(idleDurationMin, idleDurationMax) * GetTargetPressureIdleMultiplier();
    }

    private float GetCurrentHopDuration()
    {
        float multiplier = 1f;

        if (IsAggressive())
        {
            multiplier *= aggressiveHopDurationMultiplier;
        }

        if (IsEnraged())
        {
            multiplier *= enrageHopDurationMultiplier;
        }

        if (IsTargetVulnerable())
        {
            multiplier *= playerLowHealthHopDurationMultiplier;
        }

        return hopDuration * multiplier;
    }

    private float GetCurrentHopDistance()
    {
        float multiplier = 1f;

        if (IsAggressive())
        {
            multiplier *= aggressiveHopDistanceMultiplier;
        }

        if (IsEnraged())
        {
            multiplier *= enrageHopDistanceMultiplier;
        }

        if (IsTargetVulnerable())
        {
            multiplier *= playerLowHealthHopDistanceMultiplier;
        }

        return hopDistance * multiplier;
    }

    private Vector2 GetHopDirection()
    {
        if (!chaseTarget || targetSensor == null)
        {
            return GetRandomDirection();
        }

        if (ShouldRetreatFromTarget())
        {
            return GetRetreatDirection();
        }

        if (targetSensor.HasKnownTarget)
        {
            Vector2 directionToTarget = targetSensor.GetDirectionToKnownTarget();

            if (directionToTarget.sqrMagnitude > 0.0001f)
            {
                return GetPressureDirection(directionToTarget);
            }
        }

        return GetRandomDirection();
    }

    private bool ShouldRetreatFromTarget()
    {
        if (targetSensor == null || !targetSensor.HasTarget)
        {
            return false;
        }

        if (IsSelfCritical() && !IsTargetVulnerable() && Random.value < selfCriticalRetreatChance)
        {
            return true;
        }

        return retreatWhileAttackOnCooldown
            && meleeAttack != null
            && !meleeAttack.IsReady
            && !IsTargetVulnerable()
            && meleeAttack.IsTargetInAttackArea(targetSensor.CurrentTargetCollider);
    }

    private Vector2 GetRetreatDirection()
    {
        Vector2 awayFromTarget = -targetSensor.GetDirectionToTarget();

        if (awayFromTarget.sqrMagnitude <= 0.0001f)
        {
            return GetRandomDirection();
        }

        Vector2 sideStep = Random.value < 0.5f
            ? new Vector2(-awayFromTarget.y, awayFromTarget.x)
            : new Vector2(awayFromTarget.y, -awayFromTarget.x);

        float sideStepBlend = IsSelfCritical()
            ? selfCriticalSideStepBlend
            : retreatSideStepBlend;

        return Vector2.Lerp(awayFromTarget, sideStep, sideStepBlend).normalized;
    }

    private Vector2 GetPressureDirection(Vector2 directionToTarget)
    {
        float sideStepBlend = IsAggressive() || IsEnraged()
            ? aggressiveApproachSideStepBlend
            : approachSideStepBlend;

        if (IsTargetVulnerable())
        {
            sideStepBlend *= 0.35f;
        }
        else if (IsSelfCritical())
        {
            sideStepBlend = Mathf.Max(sideStepBlend, selfCriticalSideStepBlend);
        }

        if (sideStepBlend <= 0f)
        {
            return directionToTarget;
        }

        Vector2 sideStep = Random.value < 0.5f
            ? new Vector2(-directionToTarget.y, directionToTarget.x)
            : new Vector2(directionToTarget.y, -directionToTarget.x);

        return Vector2.Lerp(directionToTarget, sideStep, sideStepBlend).normalized;
    }

    private bool TryAttackCurrentTarget()
    {
        if (meleeAttack == null || targetSensor == null || !targetSensor.HasTarget)
        {
            return false;
        }

        motor.FaceDirection(targetSensor.GetDirectionToTarget());

        if (targetSensor.CurrentTargetCollider != null)
        {
            return meleeAttack.TryAttack(targetSensor.CurrentTargetCollider);
        }

        return meleeAttack.TryAttack(targetSensor.CurrentTarget);
    }

    private IEnumerator WaitAfterAttack()
    {
        motor.Stop();

        while (meleeAttack != null && meleeAttack.IsAttacking)
        {
            motor.Stop();
            yield return null;
        }

        if (attackPauseTime > 0f)
        {
            yield return WaitForIdle(attackPauseTime);
        }
    }

    private IEnumerator WaitForIdle(float duration)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            if (IsMovementBlocked())
            {
                yield return WaitUntilMovementAllowed();
                yield break;
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitUntilMovementAllowed()
    {
        motor.Stop();

        while (IsMovementBlocked())
        {
            motor.Stop();
            yield return null;
        }
    }

    private bool IsMovementBlocked()
    {
        return damageReaction != null && damageReaction.BlocksMovement;
    }

    private bool IsAggressive()
    {
        return damageReaction != null
            && damageReaction.WasDamagedRecently(aggressionAfterDamageTime);
    }

    private bool IsEnraged()
    {
        return health != null
            && !health.IsDead
            && health.NormalizedHealth <= enrageHealthPercent;
    }

    private bool IsSelfCritical()
    {
        return health != null
            && !health.IsDead
            && health.NormalizedHealth <= selfCriticalHealthPercent;
    }

    private bool IsTargetVulnerable()
    {
        Health targetHealth = GetTargetHealth();

        return targetHealth != null
            && !targetHealth.IsDead
            && targetHealth.NormalizedHealth <= playerLowHealthPercent;
    }

    private Health GetTargetHealth()
    {
        if (targetSensor == null || !targetSensor.HasTarget)
        {
            return null;
        }

        if (targetSensor.CurrentTargetCollider != null)
        {
            Health colliderHealth = targetSensor.CurrentTargetCollider.GetComponentInParent<Health>();

            if (colliderHealth != null)
            {
                return colliderHealth;
            }
        }

        return targetSensor.CurrentTarget.GetComponentInParent<Health>();
    }

    private float GetTargetPressureIdleMultiplier()
    {
        return IsTargetVulnerable()
            ? playerLowHealthIdleMultiplier
            : 1f;
    }

    private Vector2 GetSafeHopDirection(Vector2 desiredDirection, float distance)
    {
        if (desiredDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector2.zero;
        }

        desiredDirection.Normalize();

        if (!IsPathBlocked(desiredDirection, distance))
        {
            return desiredDirection;
        }

        if (!allowDiagonalMovement)
        {
            return GetSafeCardinalDirection(desiredDirection, distance);
        }

        int attempts = Mathf.Min(directionFallbackAttempts, DirectionFallbackAngles.Length);

        for (int i = 0; i < attempts; i++)
        {
            Vector2 candidateDirection = Rotate(desiredDirection, DirectionFallbackAngles[i]);

            if (!IsPathBlocked(candidateDirection, distance))
            {
                return candidateDirection;
            }
        }

        return Vector2.zero;
    }

    private Vector2 GetSafeCardinalDirection(Vector2 desiredDirection, float distance)
    {
        Vector2 primaryDirection = Mathf.Abs(desiredDirection.x) >= Mathf.Abs(desiredDirection.y)
            ? new Vector2(Mathf.Sign(desiredDirection.x), 0f)
            : new Vector2(0f, Mathf.Sign(desiredDirection.y));

        Vector2 secondaryDirection = primaryDirection.x != 0f
            ? new Vector2(0f, desiredDirection.y < 0f ? -1f : 1f)
            : new Vector2(desiredDirection.x < 0f ? -1f : 1f, 0f);

        if (!IsPathBlocked(primaryDirection, distance))
        {
            return primaryDirection;
        }

        if (!IsPathBlocked(secondaryDirection, distance))
        {
            return secondaryDirection;
        }

        if (!IsPathBlocked(-secondaryDirection, distance))
        {
            return -secondaryDirection;
        }

        if (!IsPathBlocked(-primaryDirection, distance))
        {
            return -primaryDirection;
        }

        return Vector2.zero;
    }

    private bool IsPathBlocked(Vector2 direction, float distance)
    {
        if (obstacleLayers.value == 0 || direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        float checkDistance = Mathf.Max(0f, distance + obstacleProbePadding);
        Vector2 origin = GetObstacleProbeOrigin();
        RaycastHit2D hit = Physics2D.CircleCast(origin, obstacleProbeRadius, direction.normalized, checkDistance, obstacleLayers);

        return hit.collider != null;
    }

    private bool TryGetObstacleEscapeDirection(out Vector2 escapeDirection)
    {
        escapeDirection = Vector2.zero;

        if (obstacleLayers.value == 0 || obstacleEscapeProbeRadius <= 0f)
        {
            return false;
        }

        Vector2 origin = GetObstacleProbeOrigin();
        Collider2D obstacle = Physics2D.OverlapCircle(origin, obstacleEscapeProbeRadius, obstacleLayers);

        if (obstacle == null)
        {
            return false;
        }

        Vector2 closestPoint = obstacle.ClosestPoint(origin);
        Vector2 awayFromObstacle = origin - closestPoint;

        if (awayFromObstacle.sqrMagnitude <= 0.0001f)
        {
            awayFromObstacle = targetSensor != null && targetSensor.HasKnownTarget
                ? -targetSensor.GetDirectionToKnownTarget()
                : GetRandomDirection();
        }

        if (awayFromObstacle.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        escapeDirection = awayFromObstacle.normalized;
        motor.FaceDirection(escapeDirection);
        return true;
    }

    private Vector2 GetObstacleProbeOrigin()
    {
        return bodyCollider != null
            ? bodyCollider.bounds.center
            : transform.position;
    }

    private static Vector2 Rotate(Vector2 direction, float angleDegrees)
    {
        float angle = angleDegrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(angle);
        float cos = Mathf.Cos(angle);

        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos).normalized;
    }

    private Vector2 GetRandomDirection()
    {
        if (allowDiagonalMovement)
        {
            Vector2 direction = Random.insideUnitCircle;
            return direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector2.right;
        }

        return Random.value < 0.5f
            ? new Vector2(Random.value < 0.5f ? -1f : 1f, 0f)
            : new Vector2(0f, Random.value < 0.5f ? -1f : 1f);
    }

    private void OnValidate()
    {
        hopDuration = Mathf.Max(0.01f, hopDuration);
        hopDistance = Mathf.Max(0f, hopDistance);
        idleDurationMin = Mathf.Max(0f, idleDurationMin);
        idleDurationMax = Mathf.Max(idleDurationMin, idleDurationMax);
        attackPauseTime = Mathf.Max(0f, attackPauseTime);
        aggressionAfterDamageTime = Mathf.Max(0f, aggressionAfterDamageTime);
        aggressiveIdleDurationMax = Mathf.Max(0f, aggressiveIdleDurationMax);
        aggressiveHopDurationMultiplier = Mathf.Max(0.1f, aggressiveHopDurationMultiplier);
        aggressiveHopDistanceMultiplier = Mathf.Max(0f, aggressiveHopDistanceMultiplier);
        enrageIdleDurationMax = Mathf.Max(0f, enrageIdleDurationMax);
        enrageHopDurationMultiplier = Mathf.Max(0.1f, enrageHopDurationMultiplier);
        enrageHopDistanceMultiplier = Mathf.Max(0f, enrageHopDistanceMultiplier);
        obstacleProbeRadius = Mathf.Max(0f, obstacleProbeRadius);
        obstacleProbePadding = Mathf.Max(0f, obstacleProbePadding);
        blockedIdleDuration = Mathf.Max(0f, blockedIdleDuration);
        obstacleEscapeProbeRadius = Mathf.Max(0f, obstacleEscapeProbeRadius);
        obstacleEscapeDistance = Mathf.Max(0f, obstacleEscapeDistance);
        obstacleEscapeDurationMultiplier = Mathf.Max(0.1f, obstacleEscapeDurationMultiplier);
        playerLowHealthHopDurationMultiplier = Mathf.Max(0.1f, playerLowHealthHopDurationMultiplier);
        playerLowHealthHopDistanceMultiplier = Mathf.Max(0f, playerLowHealthHopDistanceMultiplier);
        playerLowHealthIdleMultiplier = Mathf.Max(0.1f, playerLowHealthIdleMultiplier);
    }

    private void OnDrawGizmosSelected()
    {
        if (obstacleLayers.value == 0)
        {
            return;
        }

        Vector2 origin = Application.isPlaying
            ? GetObstacleProbeOrigin()
            : transform.position;

        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(origin, obstacleProbeRadius);

        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(origin, obstacleEscapeProbeRadius);
    }
}
