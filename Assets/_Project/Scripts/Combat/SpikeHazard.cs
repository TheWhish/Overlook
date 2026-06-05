using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public sealed class SpikeHazard : MonoBehaviour
{
    [SerializeField, Min(0f)] private float damage = 15f;
    [SerializeField, Min(0f)] private float damageInterval = 0.45f;
    [SerializeField] private LayerMask targetLayers = 1 << 6;
    [SerializeField] private string[] activeSpriteNames = { "peaks_1", "peaks_2" };

    private readonly List<IDamageable> overlappingTargets = new List<IDamageable>();
    private readonly Dictionary<IDamageable, float> nextDamageTimes = new Dictionary<IDamageable, float>();

    private SpriteRenderer spriteRenderer;
    private Collider2D triggerCollider;

    private bool IsActiveFrame
    {
        get
        {
            Sprite currentSprite = spriteRenderer != null ? spriteRenderer.sprite : null;

            if (currentSprite == null || activeSpriteNames == null)
            {
                return false;
            }

            for (int i = 0; i < activeSpriteNames.Length; i++)
            {
                if (currentSprite.name == activeSpriteNames[i])
                {
                    return true;
                }
            }

            return false;
        }
    }

    private void Awake()
    {
        CacheComponents();
    }

    private void Reset()
    {
        CacheComponents();

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void Update()
    {
        if (!IsActiveFrame)
        {
            return;
        }

        for (int i = overlappingTargets.Count - 1; i >= 0; i--)
        {
            IDamageable target = overlappingTargets[i];

            if (target == null || !target.CanTakeDamage)
            {
                overlappingTargets.RemoveAt(i);
                if (target != null)
                {
                    nextDamageTimes.Remove(target);
                }
                continue;
            }

            if (Time.time < GetNextDamageTime(target))
            {
                continue;
            }

            DamageTarget(target);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsInTargetLayers(other.gameObject.layer))
        {
            return;
        }

        IDamageable target = other.GetComponentInParent<IDamageable>();

        if (target == null || overlappingTargets.Contains(target))
        {
            return;
        }

        overlappingTargets.Add(target);
        nextDamageTimes[target] = 0f;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        IDamageable target = other.GetComponentInParent<IDamageable>();

        if (target == null)
        {
            return;
        }

        overlappingTargets.Remove(target);
        nextDamageTimes.Remove(target);
    }

    private void OnDisable()
    {
        overlappingTargets.Clear();
        nextDamageTimes.Clear();
    }

    private void DamageTarget(IDamageable target)
    {
        Transform targetTransform = target is Component targetComponent
            ? targetComponent.transform
            : null;

        Vector2 hitPoint = targetTransform != null
            ? (Vector2)targetTransform.position
            : (Vector2)transform.position;

        Vector2 hitDirection = targetTransform != null
            ? (Vector2)(targetTransform.position - transform.position)
            : Vector2.zero;

        target.TakeDamage(new DamageInfo(damage, gameObject, hitPoint, hitDirection));
        nextDamageTimes[target] = Time.time + damageInterval;
    }

    private float GetNextDamageTime(IDamageable target)
    {
        return nextDamageTimes.TryGetValue(target, out float nextDamageTime)
            ? nextDamageTime
            : 0f;
    }

    private bool IsInTargetLayers(int layer)
    {
        return (targetLayers.value & (1 << layer)) != 0;
    }

    private void CacheComponents()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        triggerCollider = GetComponent<Collider2D>();
    }

    private void OnValidate()
    {
        damage = Mathf.Max(0f, damage);
        damageInterval = Mathf.Max(0f, damageInterval);

        if (activeSpriteNames == null || activeSpriteNames.Length == 0)
        {
            activeSpriteNames = new[] { "peaks_1", "peaks_2" };
        }

        CacheComponents();

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }
}
