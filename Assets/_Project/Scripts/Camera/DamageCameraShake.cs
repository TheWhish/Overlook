using UnityEngine;

[DisallowMultipleComponent]
public class DamageCameraShake : MonoBehaviour
{
    [Header("Player Damage")]
    [SerializeField] private bool shakeOnPlayerDamage = true;
    [SerializeField, Min(0f)] private float playerShakeAmplitude = 0.032f;
    [SerializeField, Min(0f)] private float playerShakeDuration = 0.18f;
    [SerializeField, Min(1f)] private float playerShakeFrequency = 22f;

    [Header("Enemy Damage")]
    [SerializeField] private bool shakeOnEnemyDamage = true;
    [SerializeField, Min(0f)] private float enemyShakeAmplitude = 0.026f;
    [SerializeField, Min(0f)] private float enemyShakeDuration = 0.16f;
    [SerializeField, Min(1f)] private float enemyShakeFrequency = 20f;
    [SerializeField, Min(0f)] private float enemyShakeCooldown = 0.07f;

    [Header("Layers")]
    [SerializeField] private LayerMask playerLayers = 1 << 6;
    [SerializeField] private LayerMask enemyLayers = 1 << 8;

    private Vector3 basePosition;
    private Vector3 currentOffset;
    private float nextEnemyShakeTime;
    private float shakeTimeRemaining;
    private float shakeDuration;
    private float shakeAmplitude;
    private float shakeFrequency = 22f;
    private float shakeSeedX;
    private float shakeSeedY;

    private void OnEnable()
    {
        CaptureBasePosition();
        Health.AnyDamaged += HandleAnyDamaged;
    }

    private void OnDisable()
    {
        Health.AnyDamaged -= HandleAnyDamaged;
        transform.position = basePosition;
        currentOffset = Vector3.zero;
    }

    private void HandleAnyDamaged(Health damagedHealth, DamageInfo damageInfo)
    {
        if (damagedHealth == null)
        {
            return;
        }

        int damagedLayer = damagedHealth.gameObject.layer;

        if (shakeOnPlayerDamage && IsInLayerMask(damagedLayer, playerLayers))
        {
            PlayShake(playerShakeAmplitude, playerShakeDuration, playerShakeFrequency);
            return;
        }

        if (!shakeOnEnemyDamage || !IsInLayerMask(damagedLayer, enemyLayers) || Time.time < nextEnemyShakeTime)
        {
            return;
        }

        nextEnemyShakeTime = Time.time + enemyShakeCooldown;
        PlayShake(enemyShakeAmplitude, enemyShakeDuration, enemyShakeFrequency);
    }

    private void LateUpdate()
    {
        if (transform.position != basePosition + currentOffset)
        {
            basePosition = transform.position - currentOffset;
        }

        currentOffset = GetShakeOffset();
        transform.position = basePosition + currentOffset;
    }

    private void CaptureBasePosition()
    {
        basePosition = transform.position;
        currentOffset = Vector3.zero;
    }

    private void PlayShake(float amplitude, float duration, float frequency)
    {
        if (amplitude <= 0f || duration <= 0f)
        {
            return;
        }

        shakeAmplitude = Mathf.Max(shakeAmplitude, amplitude);
        shakeDuration = Mathf.Max(shakeDuration, duration);
        shakeTimeRemaining = Mathf.Max(shakeTimeRemaining, duration);
        shakeFrequency = Mathf.Max(1f, frequency);
        shakeSeedX = Random.value * 1000f;
        shakeSeedY = Random.value * 1000f;
    }

    private Vector3 GetShakeOffset()
    {
        if (shakeTimeRemaining <= 0f || shakeDuration <= 0f)
        {
            shakeTimeRemaining = 0f;
            shakeDuration = 0f;
            shakeAmplitude = 0f;
            return Vector3.zero;
        }

        shakeTimeRemaining = Mathf.Max(0f, shakeTimeRemaining - Time.deltaTime);

        float normalizedTime = Mathf.Clamp01(shakeTimeRemaining / shakeDuration);
        float envelope = Mathf.SmoothStep(0f, 1f, normalizedTime);
        float sampleTime = Time.time * shakeFrequency;
        float offsetX = Mathf.PerlinNoise(shakeSeedX, sampleTime) * 2f - 1f;
        float offsetY = Mathf.PerlinNoise(shakeSeedY, sampleTime) * 2f - 1f;

        if (shakeTimeRemaining <= 0f)
        {
            shakeDuration = 0f;
            shakeAmplitude = 0f;
        }

        return new Vector3(offsetX, offsetY, 0f) * shakeAmplitude * envelope;
    }

    private static bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    private void OnValidate()
    {
        playerShakeAmplitude = Mathf.Max(0f, playerShakeAmplitude);
        playerShakeDuration = Mathf.Max(0f, playerShakeDuration);
        playerShakeFrequency = Mathf.Max(1f, playerShakeFrequency);
        enemyShakeAmplitude = Mathf.Max(0f, enemyShakeAmplitude);
        enemyShakeDuration = Mathf.Max(0f, enemyShakeDuration);
        enemyShakeFrequency = Mathf.Max(1f, enemyShakeFrequency);
        enemyShakeCooldown = Mathf.Max(0f, enemyShakeCooldown);
    }
}
