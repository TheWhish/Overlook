using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyMotor))]
[RequireComponent(typeof(EnemyTargetSensor))]
[DisallowMultipleComponent]
public class EnemyHopAI : MonoBehaviour
{
    private static readonly float[] TargetCandidateAngles =
    {
        0f,
        18f,
        -18f,
        38f,
        -38f,
        62f,
        -62f,
        90f,
        -90f,
        125f,
        -125f,
        180f
    };

    private static readonly float[] WanderCandidateAngles =
    {
        0f,
        45f,
        -45f,
        90f,
        -90f,
        135f,
        -135f,
        180f
    };

    private static readonly float[] DistanceMultipliers =
    {
        1f,
        0.75f,
        0.5f,
        0.32f
    };

    [Header("Hop")]
    [SerializeField, Min(0.01f)] private float hopDuration = 1f;
    [SerializeField, Min(0f)] private float hopDistance = 0.35f;
    [SerializeField, Range(0f, 1f)] private float linearMotionBlend = 0.18f;

    [Header("Idle")]
    [SerializeField, Min(0f)] private float idleDurationMin = 0.4f;
    [SerializeField, Min(0f)] private float idleDurationMax = 1.2f;
    [SerializeField, Min(0f)] private float engagedIdleDurationMin = 0.06f;
    [SerializeField, Min(0f)] private float engagedIdleDurationMax = 0.22f;

    [Header("Combat")]
    [SerializeField, Min(0f)] private float attackPauseTime = 0.08f;
    [SerializeField, Range(0f, 1f)] private float dodgeChance = 0.22f;
    [SerializeField, Range(0f, 1f)] private float lowHealthDodgeChance = 0.42f;
    [SerializeField, Range(0f, 1f)] private float sideStepChance = 0.5f;
    [SerializeField, Range(0f, 1f)] private float attackReadyApproachBias = 0.75f;
    [SerializeField, Range(0f, 1f)] private float cooldownRetreatBias = 0.45f;
    [SerializeField, Min(0f)] private float preferredAttackDistanceMultiplier = 0.72f;
    [SerializeField, Min(0f)] private float tooCloseDistanceMultiplier = 0.42f;

    [Header("Pressure")]
    [SerializeField, Min(0f)] private float aggressionAfterDamageTime = 1.35f;
    [SerializeField, Min(0.1f)] private float aggressiveHopDurationMultiplier = 0.9f;
    [SerializeField, Min(0f)] private float aggressiveHopDistanceMultiplier = 1.05f;
    [SerializeField, Range(0f, 1f)] private float enrageHealthPercent = 0.35f;
    [SerializeField, Min(0.1f)] private float enrageHopDurationMultiplier = 0.88f;
    [SerializeField, Min(0f)] private float enrageHopDistanceMultiplier = 1.05f;
    [SerializeField, Range(0f, 1f)] private float playerLowHealthPercent = 0.35f;
    [SerializeField, Min(0.1f)] private float playerLowHealthIdleMultiplier = 0.65f;
    [SerializeField, Min(0.1f)] private float playerLowHealthHopDurationMultiplier = 0.95f;
    [SerializeField, Min(0f)] private float playerLowHealthHopDistanceMultiplier = 1f;
    [SerializeField, Range(0f, 1f)] private float selfCriticalHealthPercent = 0.22f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask obstacleLayers = 1 << 9;
    [SerializeField, Min(0f)] private float obstacleProbeRadius = 0.07f;
    [SerializeField, Min(0f)] private float obstacleProbePadding = 0.08f;
    [SerializeField, Min(0f)] private float endpointClearanceRadius = 0.08f;
    [SerializeField, Min(0f)] private float blockedIdleDuration = 0.08f;
    [SerializeField, Min(0f)] private float obstacleEscapeProbeRadius = 0.2f;
    [SerializeField, Min(0f)] private float obstacleEscapeDistance = 0.2f;

    [Header("Room Awareness")]
    [SerializeField] private bool keepPassiveMovementInsideZone = true;
    [SerializeField] private bool keepCombatMovementInsideZone = true;
    [SerializeField, Min(0f)] private float zoneProbePadding = 0.04f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos = true;
    [SerializeField] private bool drawRuntimeDebugLines;

    private EnemyMotor motor;
    private EnemyTargetSensor targetSensor;
    private EnemyAttack enemyAttack;
    private EnemyDamageReaction damageReaction;
    private Health health;
    private Collider2D bodyCollider;
    private RoomAwarenessMember awarenessMember;
    private RoomAwarenessZone cachedZone;
    private Coroutine hopRoutine;
    private Vector2 lastChosenDirection = Vector2.right;
    private Vector2 lastChosenEndPoint;
    private Vector2 lastBlockedDirection;
    private int stuckCount;

    private void Awake()
    {
        motor = GetComponent<EnemyMotor>();
        targetSensor = GetComponent<EnemyTargetSensor>();
        enemyAttack = GetComponent<EnemyAttack>();
        damageReaction = GetComponent<EnemyDamageReaction>();
        health = GetComponent<Health>();
        bodyCollider = GetComponent<Collider2D>();
        awarenessMember = GetComponent<RoomAwarenessMember>();

        if (awarenessMember == null)
        {
            awarenessMember = gameObject.AddComponent<RoomAwarenessMember>();
        }
    }

    private void OnEnable()
    {
        hopRoutine = StartCoroutine(ThinkLoop());
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

    private IEnumerator ThinkLoop()
    {
        while (enabled)
        {
            motor.Stop();

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

            yield return WaitForIdle(GetIdleDuration());

            if (IsMovementBlocked())
            {
                continue;
            }

            if (TryAttackCurrentTarget())
            {
                yield return WaitAfterAttack();
                continue;
            }

            if (TryChooseHop(out Vector2 direction, out float distance))
            {
                yield return Hop(direction, distance, GetCurrentHopDuration());
                continue;
            }

            yield return WaitForIdle(blockedIdleDuration);
        }
    }

    private bool TryChooseHop(out Vector2 direction, out float distance)
    {
        direction = Vector2.zero;
        distance = 0f;

        if (TryChooseTargetHop(out direction, out distance))
        {
            return true;
        }

        if (TryChooseEscapeHop(out direction, out distance))
        {
            return true;
        }

        return TryChooseWanderHop(out direction, out distance);
    }

    private bool TryChooseTargetHop(out Vector2 bestDirection, out float bestDistance)
    {
        bestDirection = Vector2.zero;
        bestDistance = 0f;

        if (targetSensor == null || !targetSensor.HasKnownTarget)
        {
            return false;
        }

        Vector2 origin = GetBodyCenter();
        Vector2 targetPoint = targetSensor.KnownTargetPoint;
        Vector2 toTarget = targetPoint - origin;

        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            toTarget = motor != null ? motor.LastMoveDirection : Vector2.right;
        }

        Vector2 baseDirection = toTarget.normalized;
        Vector2 sideDirection = Random.value < 0.5f
            ? new Vector2(-baseDirection.y, baseDirection.x)
            : new Vector2(baseDirection.y, -baseDirection.x);

        bool wantsDodge = ShouldDodge();
        bool attackReady = enemyAttack != null && enemyAttack.IsReady;
        float hopDistanceCurrent = GetCurrentHopDistance();
        float attackRange = enemyAttack != null ? enemyAttack.AttackRange : hopDistanceCurrent;
        float preferredDistance = Mathf.Max(attackRange * preferredAttackDistanceMultiplier, 0.05f);
        float tooCloseDistance = Mathf.Max(attackRange * tooCloseDistanceMultiplier, 0.02f);
        float currentDistance = Mathf.Sqrt(toTarget.sqrMagnitude);

        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < TargetCandidateAngles.Length; i++)
        {
            Vector2 angledDirection = Rotate(baseDirection, TargetCandidateAngles[i]);

            for (int d = 0; d < DistanceMultipliers.Length; d++)
            {
                float candidateDistance = hopDistanceCurrent * DistanceMultipliers[d];

                if (candidateDistance <= 0.001f)
                {
                    continue;
                }

                if (!IsHopUsable(angledDirection, candidateDistance, hasTarget: true))
                {
                    continue;
                }

                Vector2 endPoint = origin + angledDirection * candidateDistance;
                float endDistance = Vector2.Distance(endPoint, targetPoint);
                float distanceError = Mathf.Abs(endDistance - preferredDistance);
                float score = -distanceError * 4f;

                if (attackReady)
                {
                    score += Mathf.Max(0f, currentDistance - endDistance) * attackReadyApproachBias;
                    score += endDistance <= attackRange ? 2.5f : 0f;
                }
                else
                {
                    score += Mathf.Max(0f, endDistance - currentDistance) * cooldownRetreatBias;
                }

                if (currentDistance < tooCloseDistance || wantsDodge)
                {
                    score += Vector2.Dot(angledDirection, -baseDirection) * 1.6f;
                    score += Mathf.Abs(Vector2.Dot(angledDirection, sideDirection)) * 1.15f;
                }
                else if (Random.value < sideStepChance)
                {
                    score += Mathf.Abs(Vector2.Dot(angledDirection, sideDirection)) * 0.45f;
                }

                score += GetWallClearanceScore(endPoint);
                score -= stuckCount * Mathf.Max(0f, Vector2.Dot(angledDirection, lastBlockedDirection)) * 2f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDirection = angledDirection;
                    bestDistance = candidateDistance;
                }
            }
        }

        return bestDirection.sqrMagnitude > 0.0001f;
    }

    private bool TryChooseEscapeHop(out Vector2 direction, out float distance)
    {
        direction = Vector2.zero;
        distance = 0f;

        if (obstacleLayers.value == 0 || obstacleEscapeProbeRadius <= 0f)
        {
            return false;
        }

        Vector2 origin = GetBodyCenter();
        Collider2D obstacle = Physics2D.OverlapCircle(origin, obstacleEscapeProbeRadius, obstacleLayers);

        if (obstacle == null)
        {
            return false;
        }

        Vector2 closestPoint = obstacle.ClosestPoint(origin);
        Vector2 away = origin - closestPoint;

        if (away.sqrMagnitude <= 0.0001f)
        {
            away = targetSensor != null && targetSensor.HasKnownTarget
                ? -targetSensor.GetDirectionToKnownTarget()
                : -lastBlockedDirection;
        }

        if (away.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector2 baseDirection = away.normalized;
        float escapeDistance = Mathf.Min(GetCurrentHopDistance(), obstacleEscapeDistance);

        for (int i = 0; i < TargetCandidateAngles.Length; i++)
        {
            Vector2 candidate = Rotate(baseDirection, TargetCandidateAngles[i]);

            if (IsHopUsable(candidate, escapeDistance, hasTarget: targetSensor != null && targetSensor.HasKnownTarget))
            {
                direction = candidate;
                distance = escapeDistance;
                return true;
            }
        }

        return false;
    }

    private bool TryChooseWanderHop(out Vector2 direction, out float distance)
    {
        direction = Vector2.zero;
        distance = 0f;

        Vector2 baseDirection = motor != null && motor.LastMoveDirection.sqrMagnitude > 0.0001f
            ? motor.LastMoveDirection.normalized
            : Random.insideUnitCircle.normalized;

        if (baseDirection.sqrMagnitude <= 0.0001f)
        {
            baseDirection = Vector2.right;
        }

        float hopDistanceCurrent = GetCurrentHopDistance();

        for (int i = 0; i < WanderCandidateAngles.Length; i++)
        {
            Vector2 candidate = Rotate(baseDirection, WanderCandidateAngles[(i + Random.Range(0, WanderCandidateAngles.Length)) % WanderCandidateAngles.Length]);

            for (int d = 0; d < DistanceMultipliers.Length; d++)
            {
                float candidateDistance = hopDistanceCurrent * DistanceMultipliers[d];

                if (IsHopUsable(candidate, candidateDistance, hasTarget: false))
                {
                    direction = candidate;
                    distance = candidateDistance;
                    return true;
                }
            }
        }

        return false;
    }

    private IEnumerator Hop(Vector2 direction, float distance, float duration)
    {
        direction = direction.normalized;
        duration = Mathf.Max(0.01f, duration);

        Vector2 start = GetBodyCenter();
        lastChosenDirection = direction;
        lastChosenEndPoint = start + direction * distance;

        if (drawRuntimeDebugLines)
        {
            Debug.DrawLine(start, lastChosenEndPoint, Color.green, duration);
        }

        if (motor != null)
        {
            motor.FaceDirection(direction);
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            if (IsMovementBlocked())
            {
                break;
            }

            float remainingDistance = Mathf.Max(0f, Vector2.Distance(GetBodyCenter(), lastChosenEndPoint));

            if (remainingDistance <= 0.01f)
            {
                break;
            }

            if (IsPathBlocked(direction, Mathf.Min(remainingDistance, obstacleProbePadding + 0.03f)))
            {
                lastBlockedDirection = direction;
                stuckCount++;
                break;
            }

            float normalizedTime = Mathf.Clamp01(elapsedTime / duration);
            float speed = GetHopSpeed(normalizedTime, duration, distance);
            motor.SetMoveVelocity(direction, speed);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        motor.Stop();

        float movedDistance = Vector2.Distance(start, GetBodyCenter());
        stuckCount = movedDistance < distance * 0.22f ? Mathf.Min(stuckCount + 1, 4) : 0;

        if (TryAttackCurrentTarget())
        {
            yield return WaitAfterAttack();
        }
    }

    private bool TryAttackCurrentTarget()
    {
        if (enemyAttack == null || targetSensor == null || !targetSensor.HasTarget)
        {
            return false;
        }

        Vector2 directionToTarget = targetSensor.GetDirectionToTarget();

        if (motor != null)
        {
            motor.FaceDirection(directionToTarget);
        }

        if (targetSensor.CurrentTargetCollider != null)
        {
            return enemyAttack.TryAttack(targetSensor.CurrentTargetCollider);
        }

        return enemyAttack.TryAttack(targetSensor.CurrentTarget);
    }

    private IEnumerator WaitAfterAttack()
    {
        motor.Stop();

        while (enemyAttack != null && enemyAttack.IsAttacking)
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

            if (targetSensor != null && targetSensor.HasTarget && enemyAttack != null && enemyAttack.IsReady && TryAttackCurrentTarget())
            {
                yield return WaitAfterAttack();
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

    private float GetIdleDuration()
    {
        bool engaged = targetSensor != null && targetSensor.HasKnownTarget;
        float min = engaged ? engagedIdleDurationMin : idleDurationMin;
        float max = engaged ? engagedIdleDurationMax : idleDurationMax;
        float multiplier = IsTargetVulnerable() ? playerLowHealthIdleMultiplier : 1f;

        if (IsAggressive() || IsEnraged())
        {
            multiplier *= 0.65f;
        }

        return Random.Range(min, Mathf.Max(min, max)) * multiplier;
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

    private float GetHopSpeed(float normalizedTime, float currentHopDuration, float currentHopDistance)
    {
        float averageSpeed = currentHopDistance / currentHopDuration;
        float easedVelocityMultiplier = Mathf.Sin(normalizedTime * Mathf.PI) * (Mathf.PI * 0.5f);
        float hopVelocityMultiplier = Mathf.Lerp(easedVelocityMultiplier, 1f, linearMotionBlend);

        return averageSpeed * hopVelocityMultiplier;
    }

    private bool IsHopUsable(Vector2 direction, float distance, bool hasTarget)
    {
        if (direction.sqrMagnitude <= 0.0001f || distance <= 0f)
        {
            return false;
        }

        direction.Normalize();

        if (IsPathBlocked(direction, distance + obstacleProbePadding))
        {
            lastBlockedDirection = direction;
            return false;
        }

        Vector2 endpoint = GetBodyCenter() + direction * distance;

        if (IsEndpointBlocked(endpoint))
        {
            lastBlockedDirection = direction;
            return false;
        }

        return IsAllowedByAwarenessZone(endpoint, hasTarget);
    }

    private bool IsPathBlocked(Vector2 direction, float distance)
    {
        if (obstacleLayers.value == 0 || direction.sqrMagnitude <= 0.0001f || distance <= 0f)
        {
            return false;
        }

        Vector2 origin = GetBodyCenter();
        RaycastHit2D hit = Physics2D.CircleCast(origin, obstacleProbeRadius, direction.normalized, distance, obstacleLayers);
        return hit.collider != null;
    }

    private bool IsEndpointBlocked(Vector2 endpoint)
    {
        return obstacleLayers.value != 0
            && endpointClearanceRadius > 0f
            && Physics2D.OverlapCircle(endpoint, endpointClearanceRadius, obstacleLayers) != null;
    }

    private float GetWallClearanceScore(Vector2 point)
    {
        if (obstacleLayers.value == 0 || obstacleEscapeProbeRadius <= 0f)
        {
            return 0f;
        }

        Collider2D obstacle = Physics2D.OverlapCircle(point, obstacleEscapeProbeRadius, obstacleLayers);

        if (obstacle == null)
        {
            return 0.6f;
        }

        Vector2 closestPoint = obstacle.ClosestPoint(point);
        float distance = Vector2.Distance(point, closestPoint);
        return Mathf.Clamp01(distance / obstacleEscapeProbeRadius) - 1f;
    }

    private bool IsAllowedByAwarenessZone(Vector2 endpoint, bool hasTarget)
    {
        if (awarenessMember == null)
        {
            return true;
        }

        if (hasTarget && !keepCombatMovementInsideZone)
        {
            return true;
        }

        if (!hasTarget && !keepPassiveMovementInsideZone)
        {
            return true;
        }

        RoomAwarenessZone zone = GetCurrentZone();

        if (zone == null)
        {
            return true;
        }

        if (zone.ContainsPoint(GetBodyCenter()))
        {
            return zone.ContainsPoint(endpoint);
        }

        Vector2 closestToCurrent = zone.ClosestPoint(GetBodyCenter());
        Vector2 closestToEndpoint = zone.ClosestPoint(endpoint);
        return (closestToEndpoint - endpoint).sqrMagnitude < (closestToCurrent - (Vector2)GetBodyCenter()).sqrMagnitude + zoneProbePadding;
    }

    private RoomAwarenessZone GetCurrentZone()
    {
        awarenessMember.RefreshCurrentZone();

        if (awarenessMember.CurrentZone != null)
        {
            cachedZone = awarenessMember.CurrentZone;
        }

        return cachedZone;
    }

    private bool ShouldDodge()
    {
        if (targetSensor == null || !targetSensor.HasTarget)
        {
            return false;
        }

        float chance = IsSelfCritical() ? lowHealthDodgeChance : dodgeChance;

        if (enemyAttack != null && !enemyAttack.IsReady)
        {
            chance += cooldownRetreatBias * 0.25f;
        }

        return Random.value < Mathf.Clamp01(chance);
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

    private Vector2 GetBodyCenter()
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

    private void OnValidate()
    {
        hopDuration = Mathf.Max(0.01f, hopDuration);
        hopDistance = Mathf.Max(0f, hopDistance);
        idleDurationMin = Mathf.Max(0f, idleDurationMin);
        idleDurationMax = Mathf.Max(idleDurationMin, idleDurationMax);
        engagedIdleDurationMin = Mathf.Max(0f, engagedIdleDurationMin);
        engagedIdleDurationMax = Mathf.Max(engagedIdleDurationMin, engagedIdleDurationMax);
        attackPauseTime = Mathf.Max(0f, attackPauseTime);
        aggressionAfterDamageTime = Mathf.Max(0f, aggressionAfterDamageTime);
        aggressiveHopDurationMultiplier = Mathf.Max(0.1f, aggressiveHopDurationMultiplier);
        aggressiveHopDistanceMultiplier = Mathf.Max(0f, aggressiveHopDistanceMultiplier);
        enrageHopDurationMultiplier = Mathf.Max(0.1f, enrageHopDurationMultiplier);
        enrageHopDistanceMultiplier = Mathf.Max(0f, enrageHopDistanceMultiplier);
        playerLowHealthHopDurationMultiplier = Mathf.Max(0.1f, playerLowHealthHopDurationMultiplier);
        playerLowHealthHopDistanceMultiplier = Mathf.Max(0f, playerLowHealthHopDistanceMultiplier);
        playerLowHealthIdleMultiplier = Mathf.Max(0.1f, playerLowHealthIdleMultiplier);
        preferredAttackDistanceMultiplier = Mathf.Max(0f, preferredAttackDistanceMultiplier);
        tooCloseDistanceMultiplier = Mathf.Max(0f, tooCloseDistanceMultiplier);
        obstacleProbeRadius = Mathf.Max(0f, obstacleProbeRadius);
        obstacleProbePadding = Mathf.Max(0f, obstacleProbePadding);
        endpointClearanceRadius = Mathf.Max(0f, endpointClearanceRadius);
        blockedIdleDuration = Mathf.Max(0f, blockedIdleDuration);
        obstacleEscapeProbeRadius = Mathf.Max(0f, obstacleEscapeProbeRadius);
        obstacleEscapeDistance = Mathf.Max(0f, obstacleEscapeDistance);
        zoneProbePadding = Mathf.Max(0f, zoneProbePadding);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
        {
            return;
        }

        Vector2 origin = Application.isPlaying ? GetBodyCenter() : transform.position;

        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.65f);
        Gizmos.DrawWireSphere(origin, obstacleProbeRadius);

        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(origin, obstacleEscapeProbeRadius);

        if (!Application.isPlaying)
        {
            return;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin, lastChosenEndPoint);
        Gizmos.DrawWireSphere(lastChosenEndPoint, endpointClearanceRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, origin + lastBlockedDirection.normalized * hopDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + lastChosenDirection.normalized * hopDistance);
    }
}
