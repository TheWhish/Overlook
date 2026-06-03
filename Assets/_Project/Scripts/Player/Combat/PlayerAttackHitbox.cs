using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[DisallowMultipleComponent]
public class PlayerAttackHitbox : MonoBehaviour
{
    [Header("Hit Detection")]
    [SerializeField] private LayerMask targetLayers;
    [SerializeField, Min(0f)] private float damage = 10f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private BoxCollider2D hitboxCollider;
    private readonly HashSet<Collider2D> hitTargets = new HashSet<Collider2D>();

    private void Awake()
    {
        hitboxCollider = GetComponent<BoxCollider2D>();
        hitboxCollider.isTrigger = true;

        DisableHitbox();
    }

    public void EnableHitbox()
    {
        hitTargets.Clear();
        hitboxCollider.enabled = true;
    }

    public void DisableHitbox()
    {
        hitboxCollider.enabled = false;
        hitTargets.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsTargetLayer(other.gameObject.layer))
        {
            return;
        }

        if (hitTargets.Contains(other))
        {
            return;
        }

        hitTargets.Add(other);

        IDamageable damageable = other.GetComponentInParent<IDamageable>();

        if (damageable == null || !damageable.CanTakeDamage)
        {
            return;
        }

        Vector2 hitDirection = other.transform.position - transform.root.position;
        DamageInfo damageInfo = new DamageInfo(damage, transform.root.gameObject, other.ClosestPoint(transform.position), hitDirection);

        damageable.TakeDamage(damageInfo);
    }

    private bool IsTargetLayer(int layer)
    {
        return (targetLayers.value & (1 << layer)) != 0;
    }

    private void OnValidate()
    {
        damage = Mathf.Max(0f, damage);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
        {
            return;
        }

        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();

        if (boxCollider == null)
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        bool isEnabled = Application.isPlaying && boxCollider.enabled;

        Gizmos.color = isEnabled
            ? new Color(1f, 0f, 0f, 0.35f)
            : new Color(1f, 1f, 0f, 0.20f);

        Gizmos.DrawCube(boxCollider.offset, boxCollider.size);

        Gizmos.color = isEnabled
            ? Color.red
            : Color.yellow;

        Gizmos.DrawWireCube(boxCollider.offset, boxCollider.size);

        Gizmos.matrix = previousMatrix;
    }
}
