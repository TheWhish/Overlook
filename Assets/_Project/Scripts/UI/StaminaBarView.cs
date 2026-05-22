using UnityEngine;
using UnityEngine.UI;

public class StaminaBarView : MonoBehaviour
{
    [SerializeField] private PlayerStamina playerStamina;
    [SerializeField] private Image fillImage;

    private void OnEnable()
    {
        if (playerStamina == null)
        {
            return;
        }

        playerStamina.StaminaChanged += UpdateView;
        UpdateView(playerStamina.NormalizedStamina);
    }

    private void OnDisable()
    {
        if (playerStamina == null)
        {
            return;
        }

        playerStamina.StaminaChanged -= UpdateView;
    }

    private void UpdateView(float normalizedValue)
    {
        if (fillImage == null)
        {
            return;
        }

        fillImage.fillAmount = normalizedValue;
    }
}