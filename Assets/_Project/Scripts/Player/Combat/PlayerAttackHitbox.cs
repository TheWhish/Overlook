using System.Collections.Generic;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[AddComponentMenu("Overlook/Player/Player Sector Attack Hitbox")]
public class PlayerAttackHitbox : MonoBehaviour
{
    private const float MinimumDirectionLength = 0.0001f;

    [Header("Hit Detection")]
    [SerializeField] private LayerMask targetLayers;
    [SerializeField, Min(0f)] private float damage = 10f;
    [SerializeField, Min(1)] private int maxOverlapCount = 24;
    [SerializeField] private bool includeTriggerColliders = true;
    [SerializeField] private bool disableLegacyCollider = true;

    [Header("Attack Origin")]
    [SerializeField] private Vector2 rightOriginOffset = new Vector2(0.02f, 0.09f);
    [SerializeField] private Vector2 leftOriginOffset = new Vector2(-0.02f, 0.09f);

    [Header("Full Sector")]
    [SerializeField, Min(0f)] private float innerRadius = 0.015f;
    [SerializeField, Min(0.01f)] private float outerRadius = 0.28f;
    [SerializeField, Range(1f, 180f)] private float sectorAngle = 100f;
    [SerializeField, Range(-360f, 360f)] private float rightSectorCenterAngle = -45f;
    [SerializeField] private bool mirrorRightSectorToLeft = true;
    [SerializeField, Range(-360f, 360f)] private float leftSectorCenterAngle = -135f;
    [SerializeField, Min(0f)] private float radiusPadding = 0.01f;
    [SerializeField, Range(0f, 30f)] private float anglePadding = 2f;

    [Header("Moving Wave")]
    [SerializeField] private bool useMovingWave = true;
    [SerializeField, Range(1f, 180f)] private float waveAngle = 28f;
    [SerializeField, Range(-180f, 180f)] private float sweepStartOffset = -50f;
    [SerializeField, Range(-180f, 180f)] private float sweepEndOffset = 50f;
    [SerializeField] private bool mirrorSweepDirectionForLeft = true;
    [SerializeField] private bool overrideActiveDuration = true;
    [SerializeField, Min(0.01f)] private float activeDurationOverride = 0.28f;
    [SerializeField, Min(0.01f)] private float minimumActiveDuration = 0.18f;
    [SerializeField, Min(0f)] private float waveStartDelay = 0.035f;
    [SerializeField, Min(0f)] private float waveEndHoldTime = 0.025f;
    [SerializeField] private bool canHitDuringStartDelay;
    [SerializeField] private bool canHitDuringEndHold;
    [SerializeField, Min(0.01f)] private float fallbackSweepDuration = 0.28f;
    [SerializeField] private AnimationCurve sweepCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Target Sampling")]
    [SerializeField] private bool testColliderCenter = true;
    [SerializeField] private bool testClosestPoint = true;
    [SerializeField] private bool testBoundsCardinalPoints = true;

    [Header("Debug Gizmos")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawOnlyWhenSelected = false;
    [SerializeField] private bool drawBothDirectionsInEditMode = true;
    [SerializeField] private bool drawFullSector = true;
    [SerializeField] private bool drawSweepLimits = true;
    [SerializeField] private bool drawActiveWave = true;
    [SerializeField] private bool drawOverlapRadius = true;
#if UNITY_EDITOR
    [SerializeField] private bool drawTimingInfo = true;
#endif
    [SerializeField] private bool drawCandidatePoints = true;
    [SerializeField] private bool drawHitPoints = true;
#if UNITY_EDITOR
    [SerializeField] private bool drawLabels = true;
#endif
    [SerializeField, Range(8, 64)] private int gizmoSegments = 32;
    [SerializeField] private Color fullSectorColor = new Color(1f, 0.85f, 0.15f, 0.55f);
    [SerializeField] private Color activeWaveColor = new Color(0.15f, 1f, 0.75f, 0.9f);
    [SerializeField] private Color sweepLimitColor = new Color(1f, 0.35f, 0.15f, 0.8f);
    [SerializeField] private Color candidatePointColor = new Color(0.2f, 0.55f, 1f, 0.85f);
    [SerializeField] private Color hitPointColor = new Color(1f, 0.1f, 0.1f, 0.95f);

    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
    private readonly List<Vector2> debugCandidatePoints = new List<Vector2>();
    private readonly List<Vector2> debugHitPoints = new List<Vector2>();
    private readonly HashSet<DestructibleBarrel> breakGroupBarrels = new HashSet<DestructibleBarrel>();
    private readonly List<int> breakGroupIds = new List<int>();
    private readonly StringBuilder breakGroupKeyBuilder = new StringBuilder(64);

    private ContactFilter2D targetFilter;
    private Collider2D[] hitColliders;
    private Collider2D[] breakGroupColliders;
    private Collider2D legacyCollider;
    private static int nextHitGroupId;
    private int currentHitGroupId;
    private float facingSign = 1f;
    private float activeDuration;
    private float activeElapsedTime;
    private string currentBreakGroupKey;
    private bool isActive;
    private bool warnedAboutFullOverlapBuffer;

    private void Awake()
    {
        legacyCollider = GetComponent<Collider2D>();

        if (legacyCollider != null && disableLegacyCollider)
        {
            legacyCollider.enabled = false;
        }

        ConfigureFilter();
        EnsureOverlapBuffer();
        DisableHitbox();
    }

    private void Update()
    {
        if (!isActive)
        {
            return;
        }

        activeElapsedTime += Time.deltaTime;
        ApplyDamageInCurrentWave();
    }

    public void SetFacingDirection(float horizontalDirection)
    {
        facingSign = horizontalDirection < 0f ? -1f : 1f;
    }

    public float EnableHitbox()
    {
        return EnableHitbox(fallbackSweepDuration);
    }

    public float EnableHitbox(float requestedActiveDuration)
    {
        ConfigureFilter();
        EnsureOverlapBuffer();
        ClearDebugPoints();
        hitTargets.Clear();
        currentHitGroupId = CreateHitGroupId();
        activeDuration = ResolveActiveDuration(requestedActiveDuration);
        activeElapsedTime = 0f;
        currentBreakGroupKey = null;
        warnedAboutFullOverlapBuffer = false;
        isActive = true;

        if (legacyCollider != null && disableLegacyCollider)
        {
            legacyCollider.enabled = false;
        }

        ApplyDamageInCurrentWave();
        return activeDuration;
    }

    public void DisableHitbox()
    {
        isActive = false;
        activeElapsedTime = 0f;
        activeDuration = 0f;
        currentHitGroupId = 0;
        currentBreakGroupKey = null;
        hitTargets.Clear();
        ClearBreakGroupPreview();
        ClearDebugPoints();

        if (legacyCollider != null && disableLegacyCollider)
        {
            legacyCollider.enabled = false;
        }
    }

    private void ApplyDamageInCurrentWave()
    {
        Vector2 origin = GetAttackOrigin(facingSign);
        int hitCount = Physics2D.OverlapCircle(origin, outerRadius + radiusPadding, targetFilter, hitColliders);

        debugCandidatePoints.Clear();

        if (hitCount >= hitColliders.Length && !warnedAboutFullOverlapBuffer)
        {
            warnedAboutFullOverlapBuffer = true;
            Debug.LogWarning(
                $"[PlayerAttackHitbox] '{name}' filled Max Overlap Count ({hitColliders.Length}). Increase it if attack targets are missed.",
                this);
        }

        bool canApplyDamage = IsDamageWindowOpen();

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = hitColliders[i];

            if (hitCollider == null)
            {
                continue;
            }

            IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();

            if (damageable == null || !damageable.CanTakeDamage || hitTargets.Contains(damageable))
            {
                continue;
            }

            if (!TryGetHitPoint(origin, hitCollider, out Vector2 hitPoint))
            {
                continue;
            }

            if (!canApplyDamage)
            {
                continue;
            }

            debugHitPoints.Add(hitPoint);
            hitTargets.Add(damageable);

            Vector2 hitDirection = hitPoint - origin;

            if (hitDirection.sqrMagnitude <= MinimumDirectionLength)
            {
                hitDirection = GetDirectionFromAngle(GetSectorCenterAngle(facingSign));
            }

            string hitGroupKey = currentBreakGroupKey;

            if (damageable is DestructibleBarrel && hitGroupKey == null)
            {
                currentBreakGroupKey = BuildCurrentBreakGroupKey();
                hitGroupKey = currentBreakGroupKey;
            }

            DamageInfo damageInfo = new DamageInfo(
                damage,
                transform.root.gameObject,
                hitPoint,
                hitDirection,
                currentHitGroupId,
                GetCurrentHitGroupEndTime(),
                hitGroupKey);
            damageable.TakeDamage(damageInfo);
        }
    }

    private string BuildCurrentBreakGroupKey()
    {
        ClearBreakGroupPreview();

        Vector2 origin = GetAttackOrigin(facingSign);
        int hitCount = Physics2D.OverlapCircle(origin, outerRadius + radiusPadding, targetFilter, breakGroupColliders);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = breakGroupColliders[i];

            if (hitCollider == null)
            {
                continue;
            }

            DestructibleBarrel barrel = hitCollider.GetComponentInParent<DestructibleBarrel>();

            if (barrel == null || !barrel.CanTakeDamage || breakGroupBarrels.Contains(barrel))
            {
                continue;
            }

            if (!TryGetHitPoint(origin, hitCollider, requireCurrentWave: false, trackDebugPoint: false, out _))
            {
                continue;
            }

            breakGroupBarrels.Add(barrel);
            breakGroupIds.Add(barrel.GetEntityId().GetHashCode());
        }

        if (breakGroupIds.Count == 0)
        {
            return null;
        }

        breakGroupIds.Sort();
        return BuildBreakGroupKey();
    }

    private string BuildBreakGroupKey()
    {
        breakGroupKeyBuilder.Length = 0;

        for (int i = 0; i < breakGroupIds.Count; i++)
        {
            if (i > 0)
            {
                breakGroupKeyBuilder.Append('|');
            }

            breakGroupKeyBuilder.Append(breakGroupIds[i]);
        }

        return breakGroupKeyBuilder.ToString();
    }

    private bool TryGetHitPoint(Vector2 origin, Collider2D hitCollider, out Vector2 hitPoint)
    {
        return TryGetHitPoint(origin, hitCollider, requireCurrentWave: true, trackDebugPoint: true, out hitPoint);
    }

    private bool TryGetHitPoint(Vector2 origin, Collider2D hitCollider, bool requireCurrentWave, bool trackDebugPoint, out Vector2 hitPoint)
    {
        Bounds bounds = hitCollider.bounds;

        if (testClosestPoint)
        {
            Vector2 closestPoint = hitCollider.ClosestPoint(origin);

            if (IsPointInsideAttack(origin, closestPoint, requireCurrentWave, trackDebugPoint))
            {
                hitPoint = closestPoint;
                return true;
            }
        }

        if (testColliderCenter)
        {
            Vector2 centerPoint = bounds.center;

            if (IsPointInsideAttack(origin, centerPoint, requireCurrentWave, trackDebugPoint))
            {
                hitPoint = centerPoint;
                return true;
            }
        }

        if (testBoundsCardinalPoints)
        {
            Vector2 center = bounds.center;
            Vector2 min = bounds.min;
            Vector2 max = bounds.max;

            if (TryUseCandidatePoint(origin, new Vector2(min.x, center.y), requireCurrentWave, trackDebugPoint, out hitPoint)
                || TryUseCandidatePoint(origin, new Vector2(max.x, center.y), requireCurrentWave, trackDebugPoint, out hitPoint)
                || TryUseCandidatePoint(origin, new Vector2(center.x, min.y), requireCurrentWave, trackDebugPoint, out hitPoint)
                || TryUseCandidatePoint(origin, new Vector2(center.x, max.y), requireCurrentWave, trackDebugPoint, out hitPoint))
            {
                return true;
            }
        }

        hitPoint = default;
        return false;
    }

    private bool TryUseCandidatePoint(Vector2 origin, Vector2 candidatePoint, bool requireCurrentWave, bool trackDebugPoint, out Vector2 hitPoint)
    {
        if (IsPointInsideAttack(origin, candidatePoint, requireCurrentWave, trackDebugPoint))
        {
            hitPoint = candidatePoint;
            return true;
        }

        hitPoint = default;
        return false;
    }

    private bool IsPointInsideAttack(Vector2 origin, Vector2 point, bool requireCurrentWave, bool trackDebugPoint)
    {
        if (trackDebugPoint)
        {
            debugCandidatePoints.Add(point);
        }

        Vector2 toPoint = point - origin;
        float distance = toPoint.magnitude;

        if (distance < Mathf.Max(0f, innerRadius - radiusPadding)
            || distance > outerRadius + radiusPadding
            || distance <= MinimumDirectionLength)
        {
            return false;
        }

        float pointAngle = Mathf.Atan2(toPoint.y, toPoint.x) * Mathf.Rad2Deg;
        float sectorDelta = Mathf.Abs(Mathf.DeltaAngle(GetSectorCenterAngle(facingSign), pointAngle));

        if (sectorDelta > sectorAngle * 0.5f + anglePadding)
        {
            return false;
        }

        if (!useMovingWave || !requireCurrentWave)
        {
            return true;
        }

        float waveDelta = Mathf.Abs(Mathf.DeltaAngle(GetCurrentWaveCenterAngle(), pointAngle));
        return waveDelta <= waveAngle * 0.5f + anglePadding;
    }

    private Vector2 GetAttackOrigin(float directionSign)
    {
        Vector2 offset = directionSign < 0f ? leftOriginOffset : rightOriginOffset;
        return transform.TransformPoint(offset);
    }

    private float GetSectorCenterAngle(float directionSign)
    {
        if (directionSign >= 0f)
        {
            return NormalizeAngle(rightSectorCenterAngle);
        }

        if (!mirrorRightSectorToLeft)
        {
            return NormalizeAngle(leftSectorCenterAngle);
        }

        return NormalizeAngle(180f - rightSectorCenterAngle);
    }

    private float GetCurrentWaveCenterAngle()
    {
        float normalizedTime = GetWaveNormalizedTime();
        float easedTime = sweepCurve != null && sweepCurve.length > 0
            ? Mathf.Clamp01(sweepCurve.Evaluate(normalizedTime))
            : normalizedTime;
        float startOffset = sweepStartOffset;
        float endOffset = sweepEndOffset;

        if (facingSign < 0f && mirrorSweepDirectionForLeft)
        {
            startOffset = -sweepStartOffset;
            endOffset = -sweepEndOffset;
        }

        return NormalizeAngle(GetSectorCenterAngle(facingSign) + Mathf.Lerp(startOffset, endOffset, easedTime));
    }

    private float ResolveActiveDuration(float requestedActiveDuration)
    {
        float duration = overrideActiveDuration
            ? activeDurationOverride
            : requestedActiveDuration;

        if (duration <= 0f)
        {
            duration = fallbackSweepDuration;
        }

        float minimumDurationForTimeline = waveStartDelay + waveEndHoldTime + 0.01f;
        return Mathf.Max(0.01f, minimumActiveDuration, minimumDurationForTimeline, duration);
    }

    private float GetSweepMoveDuration()
    {
        return Mathf.Max(0.01f, activeDuration - waveStartDelay - waveEndHoldTime);
    }

    private float GetCurrentHitGroupEndTime()
    {
        return Time.time + Mathf.Max(0f, activeDuration - activeElapsedTime);
    }

    private float GetWaveNormalizedTime()
    {
        if (activeDuration <= 0f)
        {
            return 1f;
        }

        if (activeElapsedTime <= waveStartDelay)
        {
            return 0f;
        }

        return Mathf.Clamp01((activeElapsedTime - waveStartDelay) / GetSweepMoveDuration());
    }

    private bool IsDamageWindowOpen()
    {
        if (!canHitDuringStartDelay && activeElapsedTime < waveStartDelay)
        {
            return false;
        }

        if (!canHitDuringEndHold && activeElapsedTime > activeDuration - waveEndHoldTime)
        {
            return false;
        }

        return true;
    }

    private float GetWaveCenterAngleForPreview(float directionSign, float normalizedTime)
    {
        float previousFacingSign = facingSign;
        float previousElapsedTime = activeElapsedTime;
        float previousDuration = activeDuration;

        facingSign = directionSign < 0f ? -1f : 1f;
        activeDuration = 1f;
        activeElapsedTime = Mathf.Clamp01(normalizedTime);
        float angle = GetCurrentWaveCenterAngle();

        facingSign = previousFacingSign;
        activeElapsedTime = previousElapsedTime;
        activeDuration = previousDuration;

        return angle;
    }

    private void ConfigureFilter()
    {
        targetFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = includeTriggerColliders
        };

        targetFilter.SetLayerMask(targetLayers);
    }

    private void EnsureOverlapBuffer()
    {
        int requiredSize = Mathf.Max(1, maxOverlapCount);

        if (hitColliders == null || hitColliders.Length != requiredSize)
        {
            hitColliders = new Collider2D[requiredSize];
        }

        if (breakGroupColliders == null || breakGroupColliders.Length != requiredSize)
        {
            breakGroupColliders = new Collider2D[requiredSize];
        }
    }

    private void ClearDebugPoints()
    {
        debugCandidatePoints.Clear();
        debugHitPoints.Clear();
    }

    private void ClearBreakGroupPreview()
    {
        breakGroupBarrels.Clear();
        breakGroupIds.Clear();
        breakGroupKeyBuilder.Length = 0;
    }

    private static int CreateHitGroupId()
    {
        unchecked
        {
            nextHitGroupId++;

            if (nextHitGroupId == 0)
            {
                nextHitGroupId++;
            }

            return nextHitGroupId;
        }
    }

    private static Vector2 GetDirectionFromAngle(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private static float NormalizeAngle(float angle)
    {
        return Mathf.Repeat(angle + 180f, 360f) - 180f;
    }

    private void OnValidate()
    {
        damage = Mathf.Max(0f, damage);
        maxOverlapCount = Mathf.Max(1, maxOverlapCount);
        innerRadius = Mathf.Max(0f, innerRadius);
        outerRadius = Mathf.Max(innerRadius + 0.001f, outerRadius);
        radiusPadding = Mathf.Max(0f, radiusPadding);
        waveAngle = Mathf.Min(waveAngle, sectorAngle);
        activeDurationOverride = Mathf.Max(0.01f, activeDurationOverride);
        minimumActiveDuration = Mathf.Max(0.01f, minimumActiveDuration);
        waveStartDelay = Mathf.Max(0f, waveStartDelay);
        waveEndHoldTime = Mathf.Max(0f, waveEndHoldTime);
        fallbackSweepDuration = Mathf.Max(0.01f, fallbackSweepDuration);
        gizmoSegments = Mathf.Clamp(gizmoSegments, 8, 64);

        if (sweepCurve == null || sweepCurve.length == 0)
        {
            sweepCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        if (Application.isPlaying)
        {
            ConfigureFilter();
            EnsureOverlapBuffer();
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos || drawOnlyWhenSelected)
        {
            return;
        }

        DrawAttackGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        DrawAttackGizmos();
    }

    private void DrawAttackGizmos()
    {
        if (Application.isPlaying)
        {
            DrawDirectionGizmos(facingSign);
        }
        else if (drawBothDirectionsInEditMode)
        {
            DrawDirectionGizmos(1f);
            DrawDirectionGizmos(-1f);
        }
        else
        {
            DrawDirectionGizmos(facingSign);
        }

        DrawDebugPoints();
    }

    private void DrawDirectionGizmos(float directionSign)
    {
        Vector2 origin = GetAttackOrigin(directionSign);
        float sectorCenterAngle = GetSectorCenterAngle(directionSign);

        if (drawFullSector)
        {
            Gizmos.color = fullSectorColor;
            DrawSectorGizmo(origin, sectorCenterAngle, sectorAngle, innerRadius, outerRadius);
        }

        if (drawOverlapRadius)
        {
            Gizmos.color = new Color(fullSectorColor.r, fullSectorColor.g, fullSectorColor.b, 0.18f);
            Gizmos.DrawWireSphere(origin, outerRadius + radiusPadding);
        }

        if (drawSweepLimits)
        {
            DrawSweepLimitGizmos(origin, directionSign, sectorCenterAngle);
        }

        if (drawActiveWave)
        {
            float waveCenterAngle = Application.isPlaying && isActive && Mathf.Sign(directionSign) == Mathf.Sign(facingSign)
                ? GetCurrentWaveCenterAngle()
                : GetWaveCenterAngleForPreview(directionSign, 0.5f);

            Gizmos.color = activeWaveColor;
            DrawSectorGizmo(origin, waveCenterAngle, waveAngle, innerRadius, outerRadius);
        }

#if UNITY_EDITOR
        if (drawLabels)
        {
            Handles.color = fullSectorColor;
            string label = directionSign < 0f ? "Attack sector L" : "Attack sector R";

            if (drawTimingInfo && Application.isPlaying && isActive && Mathf.Sign(directionSign) == Mathf.Sign(facingSign))
            {
                label += $" | {activeElapsedTime:0.00}/{activeDuration:0.00}s | wave {GetWaveNormalizedTime():P0}";
            }

            Handles.Label(origin + Vector2.up * (outerRadius + 0.035f), label);
        }
#endif
    }

    private void DrawSweepLimitGizmos(Vector2 origin, float directionSign, float sectorCenterAngle)
    {
        float startOffset = sweepStartOffset;
        float endOffset = sweepEndOffset;

        if (directionSign < 0f && mirrorSweepDirectionForLeft)
        {
            startOffset = -sweepStartOffset;
            endOffset = -sweepEndOffset;
        }

        Vector2 startDirection = GetDirectionFromAngle(sectorCenterAngle + startOffset);
        Vector2 endDirection = GetDirectionFromAngle(sectorCenterAngle + endOffset);

        Gizmos.color = sweepLimitColor;
        Gizmos.DrawLine(origin + startDirection * innerRadius, origin + startDirection * outerRadius);
        Gizmos.DrawLine(origin + endDirection * innerRadius, origin + endDirection * outerRadius);
    }

    private void DrawDebugPoints()
    {
        if (drawCandidatePoints)
        {
            Gizmos.color = candidatePointColor;

            for (int i = 0; i < debugCandidatePoints.Count; i++)
            {
                Gizmos.DrawWireSphere(debugCandidatePoints[i], 0.008f);
            }
        }

        if (drawHitPoints)
        {
            Gizmos.color = hitPointColor;

            for (int i = 0; i < debugHitPoints.Count; i++)
            {
                Gizmos.DrawSphere(debugHitPoints[i], 0.01f);
                Gizmos.DrawLine(GetAttackOrigin(facingSign), debugHitPoints[i]);
            }
        }
    }

    private void DrawSectorGizmo(Vector2 origin, float centerAngle, float angle, float minRadius, float maxRadius)
    {
        if (maxRadius <= 0f || angle <= 0f)
        {
            return;
        }

        int segments = Mathf.Max(2, gizmoSegments);
        float halfAngle = angle * 0.5f;
        Vector2 previousOuter = origin + GetDirectionFromAngle(centerAngle - halfAngle) * maxRadius;
        Vector2 previousInner = origin + GetDirectionFromAngle(centerAngle - halfAngle) * minRadius;

        for (int i = 1; i <= segments; i++)
        {
            float currentAngle = centerAngle - halfAngle + angle * i / segments;
            Vector2 direction = GetDirectionFromAngle(currentAngle);
            Vector2 currentOuter = origin + direction * maxRadius;
            Vector2 currentInner = origin + direction * minRadius;

            Gizmos.DrawLine(previousOuter, currentOuter);

            if (minRadius > 0f)
            {
                Gizmos.DrawLine(previousInner, currentInner);
            }

            previousOuter = currentOuter;
            previousInner = currentInner;
        }

        Vector2 leftDirection = GetDirectionFromAngle(centerAngle - halfAngle);
        Vector2 rightDirection = GetDirectionFromAngle(centerAngle + halfAngle);
        Gizmos.DrawLine(origin + leftDirection * minRadius, origin + leftDirection * maxRadius);
        Gizmos.DrawLine(origin + rightDirection * minRadius, origin + rightDirection * maxRadius);
        Gizmos.DrawWireSphere(origin, 0.01f);
    }
}
