using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class RoomAwarenessZone : MonoBehaviour
{
    [SerializeField] private string zoneId;

    private Collider2D zoneCollider;

    public string ZoneId => string.IsNullOrWhiteSpace(zoneId) ? name : zoneId;

    public bool ContainsPoint(Vector2 point)
    {
        EnsureTriggerCollider();
        return zoneCollider != null && zoneCollider.OverlapPoint(point);
    }

    public Vector2 ClosestPoint(Vector2 point)
    {
        EnsureTriggerCollider();
        return zoneCollider != null ? zoneCollider.ClosestPoint(point) : point;
    }

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        RoomAwarenessMember member = other.GetComponentInParent<RoomAwarenessMember>();

        if (member != null)
        {
            member.EnterZone(this);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        RoomAwarenessMember member = other.GetComponentInParent<RoomAwarenessMember>();

        if (member != null)
        {
            member.ExitZone(this);
        }
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    private void EnsureTriggerCollider()
    {
        if (zoneCollider == null)
        {
            zoneCollider = GetComponent<Collider2D>();
        }

        if (zoneCollider != null)
        {
            zoneCollider.isTrigger = true;
        }
    }
}
