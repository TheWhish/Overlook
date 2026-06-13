using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PixelSpriteFragmentParticle : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Sprite runtimeSprite;
    private Vector2 velocity;
    private float angularVelocity;
    private float damping;
    private float scatterDuration;
    private float settledLifetime;
    private float settledFadeDuration;
    private float age;
    private float settledAge;
    private bool settled;
    private bool destroyRuntimeSprite;
    private bool snapToPixelGrid;
    private float worldPixelSize;
    private Vector3 precisePosition;
    private Color startColor;

    public void Initialize(
        Sprite fragmentSprite,
        bool ownsSprite,
        Material material,
        string sortingLayerName,
        int sortingOrder,
        Color color,
        Vector2 initialVelocity,
        float angularSpeed,
        float velocityDamping,
        float scatterSeconds,
        float settledSeconds,
        float settledFadeSeconds,
        bool snapToGrid)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        runtimeSprite = fragmentSprite;
        destroyRuntimeSprite = ownsSprite;
        snapToPixelGrid = snapToGrid;
        worldPixelSize = fragmentSprite != null && fragmentSprite.pixelsPerUnit > 0f
            ? 1f / fragmentSprite.pixelsPerUnit
            : 0f;
        velocity = initialVelocity;
        angularVelocity = angularSpeed;
        damping = Mathf.Max(0f, velocityDamping);
        scatterDuration = Mathf.Max(0.01f, scatterSeconds);
        settledLifetime = Mathf.Max(0f, settledSeconds);
        settledFadeDuration = Mathf.Max(0.01f, settledFadeSeconds);
        age = 0f;
        settledAge = 0f;
        settled = false;
        startColor = color;
        precisePosition = transform.position;

        spriteRenderer.sprite = fragmentSprite;
        spriteRenderer.color = color;
        spriteRenderer.sortingLayerName = sortingLayerName;
        spriteRenderer.sortingOrder = sortingOrder;

        if (material != null)
        {
            spriteRenderer.sharedMaterial = material;
        }

        if (snapToPixelGrid)
        {
            transform.position = SnapPosition(transform.position);
            precisePosition = transform.position;
        }
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        if (settled)
        {
            UpdateSettled(deltaTime);
            return;
        }

        UpdateScatter(deltaTime);
    }

    private void UpdateScatter(float deltaTime)
    {
        age += deltaTime;

        if (damping > 0f)
        {
            velocity = Vector2.Lerp(velocity, Vector2.zero, 1f - Mathf.Exp(-damping * deltaTime));
        }

        precisePosition += new Vector3(velocity.x, velocity.y, 0f) * deltaTime;
        transform.position = snapToPixelGrid ? SnapPosition(precisePosition) : precisePosition;
        transform.Rotate(0f, 0f, angularVelocity * deltaTime);

        if (age >= scatterDuration || velocity.sqrMagnitude <= 0.0001f)
        {
            Settle();
        }
    }

    private void UpdateSettled(float deltaTime)
    {
        settledAge += deltaTime;

        if (settledAge >= settledLifetime + settledFadeDuration)
        {
            Destroy(gameObject);
            return;
        }

        if (settledAge <= settledLifetime || spriteRenderer == null)
        {
            return;
        }

        float fadeProgress = Mathf.Clamp01((settledAge - settledLifetime) / settledFadeDuration);
        Color color = startColor;
        color.a *= 1f - fadeProgress;
        spriteRenderer.color = color;
    }

    private void Settle()
    {
        settled = true;
        settledAge = 0f;
        velocity = Vector2.zero;
        angularVelocity = 0f;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = startColor;
        }
    }

    private Vector3 SnapPosition(Vector3 position)
    {
        if (worldPixelSize <= 0f)
        {
            return position;
        }

        position.x = Mathf.Round(position.x / worldPixelSize) * worldPixelSize;
        position.y = Mathf.Round(position.y / worldPixelSize) * worldPixelSize;
        return position;
    }

    private void OnDestroy()
    {
        if (destroyRuntimeSprite && runtimeSprite != null)
        {
            Destroy(runtimeSprite);
            runtimeSprite = null;
        }
    }
}
