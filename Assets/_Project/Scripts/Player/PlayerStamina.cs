using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerStamina : MonoBehaviour
{
    [Header("Stamina")]
    [SerializeField, Min(1f)] private float maxStamina = 100f;
    [SerializeField, Min(0f)] private float recoveryPerSecond = 18f;
    [SerializeField, Min(0f)] private float recoveryDelay = 1.1f;

    private float currentStamina;
    private float lastDrainTime;
    private bool isInitialized;

    public float CurrentStamina
    {
        get
        {
            EnsureInitialized();
            return currentStamina;
        }
    }

    public float MaxStamina => maxStamina;
    public float NormalizedStamina
    {
        get
        {
            EnsureInitialized();
            return maxStamina > 0f ? currentStamina / maxStamina : 0f;
        }
    }

    public bool HasStamina
    {
        get
        {
            EnsureInitialized();
            return currentStamina > 0f;
        }
    }

    public event Action<float> StaminaChanged;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        EnsureInitialized();
        NotifyStaminaChanged();
    }

    private void Update()
    {
        RecoverStamina();
    }

    public bool TrySpend(float amount)
    {
        EnsureInitialized();

        if (amount <= 0f)
        {
            return true;
        }

        if (currentStamina < amount)
        {
            return false;
        }

        currentStamina -= amount;
        lastDrainTime = Time.time;

        NotifyStaminaChanged();

        return true;
    }

    private void RecoverStamina()
    {
        if (currentStamina >= maxStamina)
        {
            return;
        }

        if (Time.time < lastDrainTime + recoveryDelay)
        {
            return;
        }

        currentStamina = Mathf.Min(
            currentStamina + recoveryPerSecond * Time.deltaTime,
            maxStamina
        );

        NotifyStaminaChanged();
    }

    private void OnValidate()
    {
        maxStamina = Mathf.Max(1f, maxStamina);
        recoveryPerSecond = Mathf.Max(0f, recoveryPerSecond);
        recoveryDelay = Mathf.Max(0f, recoveryDelay);

        if (!Application.isPlaying)
        {
            currentStamina = maxStamina;
            isInitialized = true;
        }
    }

    private void EnsureInitialized()
    {
        if (isInitialized)
        {
            return;
        }

        currentStamina = maxStamina;
        isInitialized = true;
    }

    private void NotifyStaminaChanged()
    {
        StaminaChanged?.Invoke(NormalizedStamina);
    }
}
