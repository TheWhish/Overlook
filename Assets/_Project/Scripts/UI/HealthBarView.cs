using UnityEngine;

[DisallowMultipleComponent]
public class HealthBarView : DelayedResourceBarView
{
    [SerializeField] private Health health;

    private void OnEnable()
    {
        if (health == null)
        {
            return;
        }

        health.HealthChanged += HandleHealthChanged;
        RefreshImmediate();
    }

    private void Start()
    {
        RefreshImmediate();
    }

    private void OnDisable()
    {
        if (health == null)
        {
            return;
        }

        health.HealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        float newFillAmount = maxHealth > 0f
            ? Mathf.Clamp01(currentHealth / maxHealth)
            : 0f;

        SetNormalizedTarget(newFillAmount);
    }

    private void RefreshImmediate()
    {
        if (health == null)
        {
            return;
        }

        SetNormalizedValueImmediate(health.NormalizedHealth);
    }
}
