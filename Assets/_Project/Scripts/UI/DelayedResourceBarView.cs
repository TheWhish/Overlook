using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public abstract class DelayedResourceBarView : MonoBehaviour
{
    [FormerlySerializedAs("fillImage")]
    [SerializeField] private Image currentFillImage;
    [SerializeField] private Image delayedFillImage;

    [Header("Delayed Fill")]
    [SerializeField, Min(0f)] private float delayedStartDelay = 0.35f;
    [SerializeField, Min(0f)] private float delayedCatchUpSpeed = 1.8f;
    [SerializeField, Min(0f)] private float delayedFadeSpeed = 8f;

    private const float FillEpsilon = 0.0001f;

    private float targetFillAmount = 1f;
    private float delayedDelayTimer;
    private float delayedAlpha;
    private bool delayedFillActive;

    protected virtual void Update()
    {
        if (!delayedFillActive || delayedFillImage == null)
        {
            return;
        }

        float deltaTime = Time.unscaledDeltaTime;

        if (delayedDelayTimer > 0f)
        {
            delayedDelayTimer -= deltaTime;
            return;
        }

        if (Mathf.Abs(delayedFillImage.fillAmount - targetFillAmount) > FillEpsilon)
        {
            float nextFillAmount = delayedCatchUpSpeed <= 0f
                ? targetFillAmount
                : Mathf.MoveTowards(delayedFillImage.fillAmount, targetFillAmount, delayedCatchUpSpeed * deltaTime);

            if (Mathf.Abs(nextFillAmount - targetFillAmount) <= FillEpsilon)
            {
                nextFillAmount = targetFillAmount;
            }

            SetFillAmount(delayedFillImage, nextFillAmount);
            return;
        }

        delayedAlpha = delayedFadeSpeed <= 0f
            ? 0f
            : Mathf.MoveTowards(delayedAlpha, 0f, delayedFadeSpeed * deltaTime);

        SetDelayedAlpha(delayedAlpha);

        if (delayedAlpha <= 0f)
        {
            delayedFillActive = false;
            SetFillAmount(delayedFillImage, targetFillAmount);
        }
    }

    protected void SetNormalizedValueImmediate(float normalizedValue)
    {
        targetFillAmount = Mathf.Clamp01(normalizedValue);

        SetFillAmount(currentFillImage, targetFillAmount);

        if (delayedFillImage != null)
        {
            SetFillAmount(delayedFillImage, targetFillAmount);
            SetDelayedAlpha(0f);
        }

        delayedAlpha = 0f;
        delayedDelayTimer = 0f;
        delayedFillActive = false;
    }

    protected void SetNormalizedTarget(float normalizedValue)
    {
        float previousFillAmount = targetFillAmount;
        targetFillAmount = Mathf.Clamp01(normalizedValue);

        SetFillAmount(currentFillImage, targetFillAmount);

        if (delayedFillImage == null)
        {
            return;
        }

        if (targetFillAmount < previousFillAmount)
        {
            SetFillAmount(delayedFillImage, Mathf.Max(delayedFillImage.fillAmount, previousFillAmount));
            delayedDelayTimer = delayedStartDelay;
            delayedAlpha = 1f;
            delayedFillActive = true;
            SetDelayedAlpha(delayedAlpha);
            return;
        }

        if (delayedFillActive && delayedFillImage.fillAmount > targetFillAmount + FillEpsilon)
        {
            delayedAlpha = 1f;
            SetDelayedAlpha(delayedAlpha);
            return;
        }

        SetFillAmount(delayedFillImage, targetFillAmount);
        delayedDelayTimer = 0f;
        delayedAlpha = 0f;
        delayedFillActive = false;
        SetDelayedAlpha(delayedAlpha);
    }

    private static void SetFillAmount(Image image, float normalizedValue)
    {
        if (image == null)
        {
            return;
        }

        float clampedValue = Mathf.Clamp01(normalizedValue);

        if (Mathf.Abs(image.fillAmount - clampedValue) <= FillEpsilon)
        {
            return;
        }

        image.fillAmount = clampedValue;
    }

    private void SetDelayedAlpha(float alpha)
    {
        if (delayedFillImage == null)
        {
            return;
        }

        Color color = delayedFillImage.color;
        color.a = Mathf.Clamp01(alpha);
        delayedFillImage.color = color;
    }
}
