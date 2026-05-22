using System;
using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    [Header("Stamina")]
    [SerializeField, Min(1f)] private float maxStamina = 100f;
    [SerializeField, Min(0f)] private float drainPerSecond = 25f;
    [SerializeField, Min(0f)] private float recoveryPerSecond = 18f;
    [SerializeField, Min(0f)] private float recoveryDelay = 0.5f;

    private float currentStamina;
    private float lastDrainTime;

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float NormalizedStamina => currentStamina / maxStamina;
    public bool HasStamina => currentStamina > 0f;

    public event Action<float> StaminaChanged;

    private void Awake()
    {
        currentStamina = maxStamina;
        StaminaChanged?.Invoke(NormalizedStamina);
    }

    private void Update()
    {
        RecoverStamina();
    }

    public bool TrySpend(float amount)
    {
        if (currentStamina <= 0f)
        {
            return false;
        }

        currentStamina = Mathf.Max(currentStamina - amount, 0f);
        lastDrainTime = Time.time;

        StaminaChanged?.Invoke(NormalizedStamina);

        return currentStamina > 0f;
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

        StaminaChanged?.Invoke(NormalizedStamina);
    }
}