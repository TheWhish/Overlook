using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(Light2D))]
public sealed class LightFlicker : MonoBehaviour
{
    [SerializeField, Range(0f, 0.5f)] private float intensityAmount = 0.06f;
    [SerializeField, Range(0f, 0.5f)] private float radiusAmount = 0.03f;
    [SerializeField, Min(0.01f)] private float speed = 1.4f;
    [SerializeField] private float seed;

    private Light2D light2D;
    private float baseIntensity;
    private float baseRadius;

    private void Awake()
    {
        CacheLight();
        CaptureBaseValues();
    }

    private void OnEnable()
    {
        CacheLight();
        CaptureBaseValues();
    }

    private void Update()
    {
        if (light2D == null)
        {
            return;
        }

        float noise = Mathf.PerlinNoise(seed, Time.unscaledTime * speed) - 0.5f;
        light2D.intensity = Mathf.Max(0f, baseIntensity + noise * intensityAmount);
        light2D.pointLightOuterRadius = Mathf.Max(0f, baseRadius + noise * radiusAmount);
    }

    private void CacheLight()
    {
        if (light2D == null)
        {
            light2D = GetComponent<Light2D>();
        }
    }

    private void CaptureBaseValues()
    {
        if (light2D == null)
        {
            return;
        }

        baseIntensity = light2D.intensity;
        baseRadius = light2D.pointLightOuterRadius;
    }

    private void Reset()
    {
        seed = Random.value * 1000f;
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0.01f, speed);
    }
}
