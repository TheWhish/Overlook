using UnityEngine;

[DisallowMultipleComponent]
public class StaminaBarView : DelayedResourceBarView
{
    [SerializeField] private PlayerStamina playerStamina;

    private void OnEnable()
    {
        if (playerStamina == null)
        {
            return;
        }

        playerStamina.StaminaChanged += HandleStaminaChanged;
        RefreshImmediate();
    }

    private void Start()
    {
        RefreshImmediate();
    }

    private void OnDisable()
    {
        if (playerStamina == null)
        {
            return;
        }

        playerStamina.StaminaChanged -= HandleStaminaChanged;
    }

    private void HandleStaminaChanged(float normalizedValue)
    {
        SetNormalizedTarget(normalizedValue);
    }

    private void RefreshImmediate()
    {
        if (playerStamina == null)
        {
            return;
        }

        SetNormalizedValueImmediate(playerStamina.NormalizedStamina);
    }
}
