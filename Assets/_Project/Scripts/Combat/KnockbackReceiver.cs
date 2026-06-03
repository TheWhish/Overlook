using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]
public class KnockbackReceiver : MonoBehaviour
{
    [Header("Knockback")]
    [SerializeField, Min(0f)] private float distance = 0.14f;
    [SerializeField, Min(0.01f)] private float duration = 0.14f;
    [SerializeField, Min(0f)] private float easeOutPower = 1.8f;
    [SerializeField] private bool stopVelocityOnEnd = true;

    private Rigidbody2D rb;
    private Coroutine knockbackRoutine;

    public bool IsKnockbackActive => knockbackRoutine != null;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnDisable()
    {
        CancelKnockback(stopVelocity: true);
    }

    public void PlayKnockback(DamageInfo damageInfo)
    {
        if (distance <= 0f || duration <= 0f)
        {
            return;
        }

        Vector2 direction = GetKnockbackDirection(damageInfo);

        if (knockbackRoutine != null)
        {
            StopCoroutine(knockbackRoutine);
        }

        knockbackRoutine = StartCoroutine(KnockbackRoutine(direction));
    }

    public void CancelKnockback(bool stopVelocity)
    {
        if (knockbackRoutine != null)
        {
            StopCoroutine(knockbackRoutine);
            knockbackRoutine = null;
        }

        if (stopVelocity && rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private IEnumerator KnockbackRoutine(Vector2 direction)
    {
        float elapsedTime = 0f;
        float baseSpeed = distance / duration;

        while (elapsedTime < duration)
        {
            float normalizedTime = Mathf.Clamp01(elapsedTime / duration);
            float speedMultiplier = Mathf.Pow(1f - normalizedTime, easeOutPower);

            rb.linearVelocity = direction * (baseSpeed * speedMultiplier);

            elapsedTime += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (stopVelocityOnEnd)
        {
            rb.linearVelocity = Vector2.zero;
        }

        knockbackRoutine = null;
    }

    private Vector2 GetKnockbackDirection(DamageInfo damageInfo)
    {
        if (damageInfo.HitDirection.sqrMagnitude > 0.0001f)
        {
            return damageInfo.HitDirection.normalized;
        }

        if (damageInfo.Source != null)
        {
            Vector2 fromSource = (Vector2)transform.position - (Vector2)damageInfo.Source.transform.position;

            if (fromSource.sqrMagnitude > 0.0001f)
            {
                return fromSource.normalized;
            }
        }

        return Vector2.right;
    }

    private void OnValidate()
    {
        distance = Mathf.Max(0f, distance);
        duration = Mathf.Max(0.01f, duration);
        easeOutPower = Mathf.Max(0f, easeOutPower);
    }
}
