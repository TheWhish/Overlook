using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(Volume))]
public sealed class PixelPerfectVignetteGuard : MonoBehaviour
{
    [Header("Reference Resolution")]
    [SerializeField, Min(1)] private int referenceWidth = 384;
    [SerializeField, Min(1)] private int referenceHeight = 216;

    [Header("Vignette")]
    [SerializeField, Range(0f, 1f)] private float matchingResolutionIntensity = 0.2f;
    [SerializeField, Range(0f, 1f)] private float guardedResolutionIntensity = 0.36f;
    [SerializeField, Min(0f)] private float transitionSpeed = 8f;
    [SerializeField] private bool updateSmoothness;
    [SerializeField, Range(0.01f, 1f)] private float matchingResolutionSmoothness = 0.68f;
    [SerializeField, Range(0.01f, 1f)] private float guardedResolutionSmoothness = 0.78f;

    [Header("Debug")]
    [SerializeField] private bool logResolutionChanges;

    private Volume volume;
    private Vignette vignette;
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private bool lastResolutionMatchesReference;

    private void Awake()
    {
        volume = GetComponent<Volume>();
        CacheVignette();
        ApplyTargetValues(immediate: true);
    }

    private void Update()
    {
        if (vignette == null && !CacheVignette())
        {
            return;
        }

        ApplyTargetValues(immediate: transitionSpeed <= 0f);
    }

    private bool CacheVignette()
    {
        if (volume == null)
        {
            volume = GetComponent<Volume>();
        }

        if (volume == null || volume.profile == null)
        {
            return false;
        }

        return volume.profile.TryGet(out vignette);
    }

    private void ApplyTargetValues(bool immediate)
    {
        if (vignette == null)
        {
            return;
        }

        bool resolutionMatchesReference = DoesResolutionMatchReference();

        if (logResolutionChanges && HasScreenSizeChanged(resolutionMatchesReference))
        {
            Debug.Log(
                $"PixelPerfectVignetteGuard: {Screen.width}x{Screen.height}, reference {referenceWidth}x{referenceHeight}, exact multiple: {resolutionMatchesReference}.",
                this);
        }

        float targetIntensity = resolutionMatchesReference
            ? matchingResolutionIntensity
            : guardedResolutionIntensity;

        vignette.intensity.overrideState = true;
        vignette.intensity.value = immediate
            ? targetIntensity
            : Mathf.MoveTowards(vignette.intensity.value, targetIntensity, transitionSpeed * Time.unscaledDeltaTime);

        if (!updateSmoothness)
        {
            return;
        }

        float targetSmoothness = resolutionMatchesReference
            ? matchingResolutionSmoothness
            : guardedResolutionSmoothness;

        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = immediate
            ? targetSmoothness
            : Mathf.MoveTowards(vignette.smoothness.value, targetSmoothness, transitionSpeed * Time.unscaledDeltaTime);
    }

    private bool DoesResolutionMatchReference()
    {
        return referenceWidth > 0
            && referenceHeight > 0
            && Screen.width > 0
            && Screen.height > 0
            && Screen.width % referenceWidth == 0
            && Screen.height % referenceHeight == 0;
    }

    private bool HasScreenSizeChanged(bool resolutionMatchesReference)
    {
        if (lastScreenWidth == Screen.width
            && lastScreenHeight == Screen.height
            && lastResolutionMatchesReference == resolutionMatchesReference)
        {
            return false;
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastResolutionMatchesReference = resolutionMatchesReference;
        return true;
    }

    private void OnValidate()
    {
        referenceWidth = Mathf.Max(1, referenceWidth);
        referenceHeight = Mathf.Max(1, referenceHeight);
        transitionSpeed = Mathf.Max(0f, transitionSpeed);
    }
}
