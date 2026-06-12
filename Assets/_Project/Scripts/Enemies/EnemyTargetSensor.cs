using UnityEngine;

[DisallowMultipleComponent]
public class EnemyTargetSensor : MonoBehaviour
{
    private const int MaxTargets = 8;

    [Header("Target")]
    [SerializeField] private LayerMask targetLayers;
    [SerializeField, Min(0f)] private float detectionRange = 1.2f;
    [SerializeField, Min(0.02f)] private float scanInterval = 0.15f;
    [SerializeField, Min(0f)] private float targetMemoryTime = 0.6f;

    [Header("Visibility")]
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask lineOfSightObstacleLayers = 1 << 9;
    [SerializeField, Min(0f)] private float lineOfSightPadding = 0.02f;
    [SerializeField] private bool useAwarenessZones = true;

    private readonly Collider2D[] targetBuffer = new Collider2D[MaxTargets];
    private ContactFilter2D targetFilter;
    private Transform currentTarget;
    private Collider2D currentTargetCollider;
    private Collider2D ownCollider;
    private RoomAwarenessMember awarenessMember;
    private Vector2 lastKnownTargetPosition;
    private float lastTargetSeenTime = -999f;
    private float nextScanTime;
    private bool hasEngagedTarget;

    public Transform CurrentTarget => currentTarget;
    public Collider2D CurrentTargetCollider => currentTargetCollider;
    public bool HasTarget => currentTarget != null;
    public bool HasKnownTarget => HasTarget || Time.time - lastTargetSeenTime <= targetMemoryTime;
    public bool HasEngagedTarget => hasEngagedTarget;
    public Vector2 LastKnownTargetPosition => lastKnownTargetPosition;
    public Vector2 KnownTargetPoint => HasTarget ? GetCurrentTargetPoint() : lastKnownTargetPosition;
    public float DetectionRange => detectionRange;

    private void Awake()
    {
        ownCollider = GetComponent<Collider2D>();
        awarenessMember = GetComponent<RoomAwarenessMember>();

        if (awarenessMember == null)
        {
            awarenessMember = gameObject.AddComponent<RoomAwarenessMember>();
        }

        ConfigureFilter();
    }

    private void OnEnable()
    {
        nextScanTime = 0f;
    }

    private void Update()
    {
        if (Time.time < nextScanTime)
        {
            return;
        }

        nextScanTime = Time.time + scanInterval;
        ScanForTarget();
    }

    public Vector2 GetDirectionToTarget()
    {
        if (currentTarget == null)
        {
            return Vector2.zero;
        }

        Vector2 toTarget = GetCurrentTargetPoint() - (Vector2)transform.position;
        return toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.zero;
    }

    public float GetSqrDistanceToTarget()
    {
        if (currentTarget == null)
        {
            return float.PositiveInfinity;
        }

        return (GetCurrentTargetPoint() - (Vector2)transform.position).sqrMagnitude;
    }

    public Vector2 GetDirectionToKnownTarget()
    {
        if (currentTarget != null)
        {
            return GetDirectionToTarget();
        }

        if (!HasKnownTarget)
        {
            return Vector2.zero;
        }

        Vector2 toLastKnownTarget = lastKnownTargetPosition - (Vector2)transform.position;
        return toLastKnownTarget.sqrMagnitude > 0.0001f ? toLastKnownTarget.normalized : Vector2.zero;
    }

    private void ScanForTarget()
    {
        if (awarenessMember != null)
        {
            awarenessMember.RefreshCurrentZone();
        }

        Vector2 origin = transform.position;
        int hitCount = Physics2D.OverlapCircle(origin, detectionRange, targetFilter, targetBuffer);

        Transform bestTarget = null;
        Collider2D bestTargetCollider = null;
        float bestSqrDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = targetBuffer[i];
            targetBuffer[i] = null;

            if (hit == null)
            {
                continue;
            }

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();

            if (damageable == null)
            {
                continue;
            }

            Health targetHealth = hit.GetComponentInParent<Health>();

            if (targetHealth != null && targetHealth.IsDead)
            {
                continue;
            }

            if (!CanPerceiveTarget(hit))
            {
                continue;
            }

            Vector2 closestPoint = hit.ClosestPoint(origin);
            float sqrDistance = (closestPoint - origin).sqrMagnitude;

            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                bestTarget = hit.transform;
                bestTargetCollider = hit;
            }
        }

        if (bestTarget != null)
        {
            currentTarget = bestTarget;
            currentTargetCollider = bestTargetCollider;
            lastKnownTargetPosition = GetTargetPoint(bestTargetCollider, bestTarget);
            lastTargetSeenTime = Time.time;
            hasEngagedTarget = true;
            return;
        }

        currentTarget = null;
        currentTargetCollider = null;
    }

    private Vector2 GetCurrentTargetPoint()
    {
        return GetTargetPoint(currentTargetCollider, currentTarget);
    }

    private static Vector2 GetTargetPoint(Collider2D targetCollider, Transform targetTransform)
    {
        if (targetCollider != null)
        {
            return targetCollider.bounds.center;
        }

        return targetTransform != null
            ? targetTransform.position
            : Vector2.zero;
    }

    private bool CanPerceiveTarget(Collider2D targetCollider)
    {
        if (targetCollider == null)
        {
            return false;
        }

        if (useAwarenessZones && !SharesAwarenessZone(targetCollider))
        {
            return false;
        }

        return !requireLineOfSight || !IsLineOfSightBlocked(targetCollider);
    }

    private bool SharesAwarenessZone(Collider2D targetCollider)
    {
        if (awarenessMember == null || awarenessMember.CurrentZone == null)
        {
            return true;
        }

        if (hasEngagedTarget)
        {
            return true;
        }

        RoomAwarenessMember targetMember = targetCollider.GetComponentInParent<RoomAwarenessMember>();

        if (targetMember == null)
        {
            Transform targetRoot = targetCollider.attachedRigidbody != null
                ? targetCollider.attachedRigidbody.transform
                : targetCollider.transform.root;

            targetMember = targetRoot.gameObject.AddComponent<RoomAwarenessMember>();
        }

        if (targetMember != null)
        {
            targetMember.RefreshCurrentZone();
        }

        return targetMember != null && awarenessMember.SharesZoneWith(targetMember);
    }

    private bool IsLineOfSightBlocked(Collider2D targetCollider)
    {
        if (lineOfSightObstacleLayers.value == 0)
        {
            return false;
        }

        Vector2 origin = GetSensorPoint();
        Vector2 targetPoint = GetTargetPoint(targetCollider, targetCollider.transform);
        Vector2 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;

        if (distance <= 0.0001f)
        {
            return false;
        }

        float castDistance = Mathf.Max(0f, distance - lineOfSightPadding);
        RaycastHit2D hit = Physics2D.Linecast(origin, origin + toTarget.normalized * castDistance, lineOfSightObstacleLayers);
        return hit.collider != null;
    }

    private Vector2 GetSensorPoint()
    {
        return ownCollider != null
            ? ownCollider.bounds.center
            : transform.position;
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

    private void OnValidate()
    {
        detectionRange = Mathf.Max(0f, detectionRange);
        scanInterval = Mathf.Max(0.02f, scanInterval);
        targetMemoryTime = Mathf.Max(0f, targetMemoryTime);
        lineOfSightPadding = Mathf.Max(0f, lineOfSightPadding);
        ConfigureFilter();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
