using System;
using UnityEngine;

[DisallowMultipleComponent]
public class Health : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField] private bool logDamage;

    private float currentHealth;
    private int damageBlockCount;
    private bool isDead;
    private bool isInitialized;

    public event Action<float, float> HealthChanged;
    public event Action<DamageInfo> Damaged;
    public event Action<DamageInfo> Died;

    public static event Action<Health, DamageInfo> AnyDamaged;

    public float MaxHealth => maxHealth;
    public float CurrentHealth
    {
        get
        {
            EnsureInitialized();
            return currentHealth;
        }
    }

    public float NormalizedHealth
    {
        get
        {
            EnsureInitialized();
            return maxHealth > 0f ? currentHealth / maxHealth : 0f;
        }
    }

    public bool IsDead => isDead;
    public bool CanTakeDamage => !isDead && damageBlockCount <= 0 && isActiveAndEnabled;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        EnsureInitialized();
        NotifyHealthChanged();
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        EnsureInitialized();

        if (!CanTakeDamage)
        {
            return;
        }

        float damageAmount = Mathf.Max(0f, damageInfo.Amount);

        if (damageAmount <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damageAmount);

        Damaged?.Invoke(damageInfo);
        AnyDamaged?.Invoke(this, damageInfo);
        NotifyHealthChanged();

        if (logDamage)
        {
            LogDamage($"{name} took {damageAmount:0.#} damage. Health: {currentHealth:0.#}/{maxHealth:0.#}");
        }

        if (currentHealth <= 0f)
        {
            Die(damageInfo);
        }
    }

    public void RestoreToFull()
    {
        currentHealth = maxHealth;
        damageBlockCount = 0;
        isDead = false;
        NotifyHealthChanged();
    }

    public void SetDamageBlocked(bool blocked)
    {
        if (blocked)
        {
            damageBlockCount++;
            return;
        }

        damageBlockCount = Mathf.Max(0, damageBlockCount - 1);
    }

    private void Die(DamageInfo damageInfo)
    {
        isDead = true;
        Died?.Invoke(damageInfo);

        if (logDamage)
        {
            LogDamage($"{name} died.");
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void LogDamage(string message)
    {
        Debug.Log(message, this);
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);

        if (!Application.isPlaying)
        {
            currentHealth = maxHealth;
            isInitialized = true;
        }
    }

    private void EnsureInitialized()
    {
        if (isInitialized)
        {
            return;
        }

        currentHealth = maxHealth;
        damageBlockCount = 0;
        isDead = false;
        isInitialized = true;
    }

    private void NotifyHealthChanged()
    {
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
