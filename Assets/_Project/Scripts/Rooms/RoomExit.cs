using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class RoomExit : MonoBehaviour
{
    [SerializeField] private RoomDirection direction = RoomDirection.None;
    [SerializeField] private LayerMask playerLayers = 1 << 6;

    private RoomDefinition room;
    private Collider2D triggerCollider;

    public RoomDefinition Room => room;
    public RoomDirection Direction => direction;
    public Vector3 SpawnPosition => transform.position;

    public float GetHalfExtentAlong(RoomDirection roomDirection)
    {
        EnsureColliderIsTrigger();

        if (triggerCollider == null)
        {
            return 0f;
        }

        Bounds bounds = triggerCollider.bounds;

        switch (roomDirection)
        {
            case RoomDirection.Left:
            case RoomDirection.Right:
                return bounds.extents.x;
            case RoomDirection.Top:
            case RoomDirection.Bottom:
                return bounds.extents.y;
            default:
                return 0f;
        }
    }

    public void Configure(RoomDefinition owner)
    {
        room = owner;
        EnsureColliderIsTrigger();

        if (direction == RoomDirection.None)
        {
            RoomDirectionUtility.TryParseFromName(name, out direction);
        }
    }

    private void Awake()
    {
        EnsureColliderIsTrigger();

        if (direction == RoomDirection.None)
        {
            RoomDirectionUtility.TryParseFromName(name, out direction);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryEnter(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryEnter(other);
    }

    private void TryEnter(Collider2D other)
    {
        if (direction == RoomDirection.None || room == null)
        {
            return;
        }

        if ((playerLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();

        if (player == null)
        {
            return;
        }

        RoomFlowController controller = RoomFlowController.Instance;

        if (controller != null)
        {
            controller.TryEnterExit(this, player);
        }
    }

    private void OnValidate()
    {
        if (direction == RoomDirection.None)
        {
            RoomDirectionUtility.TryParseFromName(name, out direction);
        }

        EnsureColliderIsTrigger();
    }

    private void EnsureColliderIsTrigger()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider2D>();
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }
}
