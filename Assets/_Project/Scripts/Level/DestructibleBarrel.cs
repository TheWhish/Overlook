using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public sealed class DestructibleBarrel : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 10f;
    [SerializeField] private bool resetHealthOnEnable = true;
    [SerializeField] private bool logDamage;

    [Header("Break Chance")]
    [SerializeField] private bool useBreakChance = true;
    [SerializeField, Range(0f, 1f)] private float firstHitBreakChance = 0.5f;
    [SerializeField, Range(0f, 1f)] private float secondHitBreakChance = 0.75f;
    [SerializeField, Range(0f, 1f)] private float thirdHitBreakChance = 1f;
    [FormerlySerializedAs("fallbackBreakGroupResolveDelay")]
    [SerializeField, Min(0f)] private float resolvedBreakGroupCacheDuration = 0.5f;
    [SerializeField] private bool logBreakRolls;

    [Header("Break")]
    [SerializeField] private bool hideSpriteOnBreak = true;
    [SerializeField] private bool disableCollidersOnBreak = true;
    [SerializeField] private bool destroyObjectAfterBreak;
    [SerializeField, Min(0f)] private float destroyDelay = 1f;
    [FormerlySerializedAs("shakeCameraOnBreak")]
    [SerializeField] private bool shakeCameraOnHit = true;
    [SerializeField] private bool shakeBeforeBreak = true;
    [SerializeField, Min(0f)] private float breakShakeDuration = 0.12f;
    [SerializeField, Min(0f)] private float breakShakeAmplitude = 0.018f;
    [SerializeField, Min(1f)] private float breakShakeFrequency = 48f;
    [SerializeField] private bool snapBreakShakeToPixelGrid = true;
    [SerializeField, Min(0f)] private float fragmentSpawnDelay;

    [Header("Sprite Fragment Particles")]
    [SerializeField] private bool spawnFragments = true;
    [SerializeField, Range(1, 64)] private int fragmentCount = 8;
    [SerializeField, Range(2, 4)] private int fragmentPixelSize = 2;
    [SerializeField] private bool forcePointFilter = true;
    [SerializeField, Range(0f, 1f)] private float opaqueAlphaThreshold = 0.2f;
    [SerializeField, Min(1)] private int opaqueSearchAttempts = 12;
    [SerializeField] private Vector2 fragmentSpawnAreaSize = new Vector2(0.16f, 0.16f);
    [SerializeField, Min(0f)] private float spawnRadius = 0.006f;
    [SerializeField, Range(0f, 180f)] private float spreadAngle = 70f;
    [SerializeField, Range(0f, 1f)] private float hitDirectionInfluence = 0.22f;
    [SerializeField, Range(0f, 1f)] private float outwardScatterInfluence = 0.55f;
    [SerializeField, Range(0f, 1f)] private float randomDirectionBlend = 0.18f;
    [SerializeField] private Vector2 speedRange = new Vector2(0.06f, 0.16f);
    [FormerlySerializedAs("airTimeRange")]
    [SerializeField] private Vector2 scatterDurationRange = new Vector2(0.1f, 0.18f);
    [FormerlySerializedAs("groundedLifetimeRange")]
    [SerializeField] private Vector2 settledLifetimeRange = new Vector2(0.12f, 0.28f);
    [FormerlySerializedAs("groundedFadeDurationRange")]
    [SerializeField] private Vector2 settledFadeDurationRange = new Vector2(0.55f, 0.75f);
    [SerializeField] private Vector2 angularSpeedRange = new Vector2(15f, 45f);
    [SerializeField, Min(0f)] private float velocityDamping = 12f;
    [SerializeField] private bool snapFragmentPositionsToPixelGrid = true;
    [SerializeField] private Material fragmentMaterial;
    [SerializeField] private bool inheritSpriteColor = true;
    [SerializeField] private Color fragmentColor = Color.white;
    [SerializeField] private string fragmentSortingLayerName = "Ground";
    [SerializeField] private int fragmentSortingOrderOffset = 2;

    private SpriteRenderer spriteRenderer;
    private Collider2D[] colliders;
    private float currentHealth;
    private bool broken;
    private Coroutine activeShakeRoutine;
    private Vector3 shakeBaseLocalPosition;

    private static readonly Dictionary<PendingHitKey, ResolvedBreakGroup> resolvedBreakGroups = new Dictionary<PendingHitKey, ResolvedBreakGroup>();
    private static readonly List<PendingHitKey> expiredBreakGroupKeys = new List<PendingHitKey>();
    private static string lastBreakChanceGroupKey;
    private static int failedBreakAttemptsInCurrentGroup;

    private readonly struct PendingHitKey : System.IEquatable<PendingHitKey>
    {
        private readonly int sourceId;
        private readonly int hitGroupId;
        private readonly int fallbackFrame;

        public PendingHitKey(int sourceId, int hitGroupId, int fallbackFrame)
        {
            this.sourceId = sourceId;
            this.hitGroupId = hitGroupId;
            this.fallbackFrame = fallbackFrame;
        }

        public bool Equals(PendingHitKey other)
        {
            return sourceId == other.sourceId
                && hitGroupId == other.hitGroupId
                && fallbackFrame == other.fallbackFrame;
        }

        public override bool Equals(object obj)
        {
            return obj is PendingHitKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + sourceId;
                hash = hash * 31 + hitGroupId;
                hash = hash * 31 + fallbackFrame;
                return hash;
            }
        }
    }

    private sealed class ResolvedBreakGroup
    {
        public ResolvedBreakGroup(bool shouldBreak, float removeAfterTime)
        {
            ShouldBreak = shouldBreak;
            RemoveAfterTime = removeAfterTime;
        }

        public bool ShouldBreak { get; }
        public float RemoveAfterTime { get; }
    }

    public bool CanTakeDamage => !broken && isActiveAndEnabled;

    private void Awake()
    {
        CacheComponents();
        ResetState();
    }

    private void OnEnable()
    {
        CacheComponents();
        shakeBaseLocalPosition = transform.localPosition;

        if (resetHealthOnEnable)
        {
            ResetState();
        }
    }

    private void OnDisable()
    {
        StopActiveShake(restorePosition: true);
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (!CanTakeDamage)
        {
            return;
        }

        float damageAmount = Mathf.Max(0f, damageInfo.Amount);

        if (damageAmount <= 0f)
        {
            return;
        }

        PlayBarrelShake();
        PlayHitCameraShake(damageInfo);

        if (useBreakChance)
        {
            QueueBreakRoll(damageInfo);
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damageAmount);

        if (logDamage)
        {
            Debug.Log($"[DestructibleBarrel] {name} took {damageAmount:0.#} damage. Health: {currentHealth:0.#}/{maxHealth:0.#}.", this);
        }

        if (currentHealth <= 0f)
        {
            Break(damageInfo);
        }
    }

    private void QueueBreakRoll(DamageInfo damageInfo)
    {
        PendingHitKey hitKey = CreatePendingHitKey(damageInfo);
        ResolvedBreakGroup breakGroup = GetOrCreateResolvedBreakGroup(hitKey, damageInfo);

        if (breakGroup.ShouldBreak)
        {
            Break(damageInfo);
        }
    }

    private ResolvedBreakGroup GetOrCreateResolvedBreakGroup(PendingHitKey hitKey, DamageInfo damageInfo)
    {
        PruneResolvedBreakGroups();

        if (resolvedBreakGroups.TryGetValue(hitKey, out ResolvedBreakGroup breakGroup))
        {
            return breakGroup;
        }

        string breakGroupKey = GetBreakGroupKey(damageInfo);
        int failedAttempts = breakGroupKey == lastBreakChanceGroupKey
            ? failedBreakAttemptsInCurrentGroup
            : 0;
        float breakChance = GetBreakChance(failedAttempts);
        bool shouldBreak = UnityEngine.Random.value <= breakChance;

        if (logBreakRolls)
        {
            Debug.Log(
                $"[DestructibleBarrel] Group roll '{breakGroupKey}' attempt {failedAttempts + 1}: {breakChance:P0}. Result: {(shouldBreak ? "break" : "hold")}.",
                this);
        }

        if (!shouldBreak)
        {
            lastBreakChanceGroupKey = breakGroupKey;
            failedBreakAttemptsInCurrentGroup = failedAttempts + 1;
        }
        else
        {
            ResetBreakChanceStreak();
        }

        breakGroup = new ResolvedBreakGroup(shouldBreak, GetResolvedBreakGroupRemoveTime(damageInfo));
        resolvedBreakGroups.Add(hitKey, breakGroup);
        return breakGroup;
    }

    public void Break()
    {
        Break(new DamageInfo(maxHealth, gameObject, transform.position, Vector2.zero));
    }

    private void Break(DamageInfo damageInfo)
    {
        if (broken)
        {
            return;
        }

        broken = true;

        if (disableCollidersOnBreak)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }
        }

        if (activeShakeRoutine == null)
        {
            PlayBarrelShake();
        }

        StartCoroutine(BreakRoutine(damageInfo));
    }

    private void PlayHitCameraShake(DamageInfo damageInfo)
    {
        if (shakeCameraOnHit)
        {
            DamageCameraShake.PlayEnemyDamageShakeOnMainCamera(damageInfo);
        }
    }

    private static PendingHitKey CreatePendingHitKey(DamageInfo damageInfo)
    {
        int sourceId = damageInfo.Source != null ? damageInfo.Source.GetEntityId().GetHashCode() : 0;
        int fallbackFrame = damageInfo.HitGroupId != 0 ? 0 : Time.frameCount;
        return new PendingHitKey(sourceId, damageInfo.HitGroupId, fallbackFrame);
    }

    private string GetBreakGroupKey(DamageInfo damageInfo)
    {
        return !string.IsNullOrEmpty(damageInfo.HitGroupKey)
            ? damageInfo.HitGroupKey
            : GetEntityId().GetHashCode().ToString();
    }

    private float GetResolvedBreakGroupRemoveTime(DamageInfo damageInfo)
    {
        float groupEndTime = Mathf.Max(Time.time, damageInfo.HitGroupEndTime);
        return groupEndTime + resolvedBreakGroupCacheDuration;
    }

    private static void PruneResolvedBreakGroups()
    {
        if (resolvedBreakGroups.Count == 0)
        {
            return;
        }

        expiredBreakGroupKeys.Clear();

        foreach (KeyValuePair<PendingHitKey, ResolvedBreakGroup> entry in resolvedBreakGroups)
        {
            if (Time.time >= entry.Value.RemoveAfterTime)
            {
                expiredBreakGroupKeys.Add(entry.Key);
            }
        }

        for (int i = 0; i < expiredBreakGroupKeys.Count; i++)
        {
            resolvedBreakGroups.Remove(expiredBreakGroupKeys[i]);
        }
    }

    private static void ResetBreakChanceStreak()
    {
        lastBreakChanceGroupKey = null;
        failedBreakAttemptsInCurrentGroup = 0;
    }

    private float GetBreakChance(int failedAttempts)
    {
        if (failedAttempts <= 0)
        {
            return firstHitBreakChance;
        }

        if (failedAttempts == 1)
        {
            return secondHitBreakChance;
        }

        return thirdHitBreakChance;
    }

    private IEnumerator BreakRoutine(DamageInfo damageInfo)
    {
        if (fragmentSpawnDelay > 0f)
        {
            yield return new WaitForSeconds(fragmentSpawnDelay);
        }

        if (spawnFragments)
        {
            SpawnFragments(damageInfo);
        }

        if (hideSpriteOnBreak && spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        if (destroyObjectAfterBreak)
        {
            Destroy(gameObject, destroyDelay);
            yield break;
        }

        enabled = false;
    }

    private void PlayBarrelShake()
    {
        if (!CanShakeBarrel())
        {
            return;
        }

        if (activeShakeRoutine == null)
        {
            shakeBaseLocalPosition = transform.localPosition;
        }

        StopActiveShake(restorePosition: true);
        activeShakeRoutine = StartCoroutine(ShakeTransform(shakeBaseLocalPosition, clearActiveRoutine: true));
    }

    private void StopActiveShake(bool restorePosition)
    {
        if (activeShakeRoutine != null)
        {
            StopCoroutine(activeShakeRoutine);
            activeShakeRoutine = null;
        }

        if (restorePosition)
        {
            transform.localPosition = shakeBaseLocalPosition;
        }
    }

    private bool CanShakeBarrel()
    {
        return shakeBeforeBreak && breakShakeDuration > 0f && breakShakeAmplitude > 0f && spriteRenderer != null;
    }

    private IEnumerator ShakeTransform(Vector3 startLocalPosition, bool clearActiveRoutine)
    {
        float elapsedTime = 0f;
        float seedX = UnityEngine.Random.value * 1000f;
        float seedY = UnityEngine.Random.value * 1000f;
        float worldPixelSize = GetWorldPixelSize();

        while (elapsedTime < breakShakeDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / breakShakeDuration);
            float envelope = 1f - Mathf.SmoothStep(0f, 1f, normalizedTime);
            float sampleTime = Time.time * breakShakeFrequency;
            float offsetX = (Mathf.PerlinNoise(seedX, sampleTime) * 2f - 1f) * breakShakeAmplitude * envelope;
            float offsetY = (Mathf.PerlinNoise(seedY, sampleTime) * 2f - 1f) * breakShakeAmplitude * envelope;
            Vector3 offset = new Vector3(offsetX, offsetY, 0f);

            if (snapBreakShakeToPixelGrid && worldPixelSize > 0f)
            {
                offset.x = Mathf.Round(offset.x / worldPixelSize) * worldPixelSize;
                offset.y = Mathf.Round(offset.y / worldPixelSize) * worldPixelSize;
            }

            transform.localPosition = startLocalPosition + offset;
            yield return null;
        }

        transform.localPosition = startLocalPosition;

        if (clearActiveRoutine)
        {
            activeShakeRoutine = null;
        }
    }

    private void SpawnFragments(DamageInfo damageInfo)
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null || fragmentCount <= 0)
        {
            return;
        }

        Sprite sourceSprite = spriteRenderer.sprite;
        Texture2D sourceTexture = sourceSprite.texture;

        if (forcePointFilter && sourceTexture != null)
        {
            sourceTexture.filterMode = FilterMode.Point;
        }

        Vector3 origin = spriteRenderer.bounds.center;
        Vector2 baseDirection = damageInfo.HitDirection.sqrMagnitude > 0.0001f
            ? damageInfo.HitDirection.normalized
            : Random.insideUnitCircle.normalized;

        if (baseDirection.sqrMagnitude <= 0.0001f)
        {
            baseDirection = Vector2.up;
        }

        Color color = inheritSpriteColor && spriteRenderer != null
            ? spriteRenderer.color * fragmentColor
            : fragmentColor;
        string sortingLayer = !string.IsNullOrWhiteSpace(fragmentSortingLayerName)
            ? fragmentSortingLayerName
            : spriteRenderer.sortingLayerName;
        int sortingOrder = spriteRenderer.sortingOrder + fragmentSortingOrderOffset;

        for (int i = 0; i < fragmentCount; i++)
        {
            Sprite fragmentSprite = CreateFragmentSprite(sourceSprite);

            if (fragmentSprite == null)
            {
                continue;
            }

            Vector2 areaOffset = GetFragmentSpawnOffset();
            Vector2 offset = areaOffset + Random.insideUnitCircle * spawnRadius;
            GameObject fragmentObject = new GameObject($"{name}_Fragment");
            fragmentObject.transform.position = origin + new Vector3(offset.x, offset.y, 0f);
            fragmentObject.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            PixelSpriteFragmentParticle fragment = fragmentObject.AddComponent<PixelSpriteFragmentParticle>();
            float angleOffset = Random.Range(spreadAngle * -0.5f, spreadAngle * 0.5f);
            Vector2 direction = CreateTopDownFragmentDirection(baseDirection, areaOffset, angleOffset);

            if (randomDirectionBlend > 0f)
            {
                Vector2 randomDirection = Random.insideUnitCircle.normalized;

                if (randomDirection.sqrMagnitude <= 0.0001f)
                {
                    randomDirection = direction.sqrMagnitude > 0.0001f ? direction : Vector2.right;
                }

                direction = Vector2.Lerp(direction, randomDirection, randomDirectionBlend).normalized;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.right;
            }

            float speed = Random.Range(speedRange.x, speedRange.y);
            float scatterDuration = Random.Range(scatterDurationRange.x, scatterDurationRange.y);
            float settledLifetime = Random.Range(settledLifetimeRange.x, settledLifetimeRange.y);
            float settledFadeDuration = Random.Range(settledFadeDurationRange.x, settledFadeDurationRange.y);
            float angularSpeed = Random.Range(angularSpeedRange.x, angularSpeedRange.y) * (Random.value < 0.5f ? -1f : 1f);

            fragment.Initialize(
                fragmentSprite,
                ownsSprite: true,
                fragmentMaterial != null ? fragmentMaterial : spriteRenderer.sharedMaterial,
                sortingLayer,
                sortingOrder,
                color,
                direction * speed,
                angularSpeed,
                velocityDamping,
                scatterDuration,
                settledLifetime,
                settledFadeDuration,
                snapFragmentPositionsToPixelGrid);
        }
    }

    private Vector2 GetFragmentSpawnOffset()
    {
        return new Vector2(
            Random.Range(fragmentSpawnAreaSize.x * -0.5f, fragmentSpawnAreaSize.x * 0.5f),
            Random.Range(fragmentSpawnAreaSize.y * -0.5f, fragmentSpawnAreaSize.y * 0.5f));
    }

    private Vector2 CreateTopDownFragmentDirection(Vector2 hitDirection, Vector2 areaOffset, float angleOffset)
    {
        Vector2 randomFloorDirection = Random.insideUnitCircle.normalized;

        if (randomFloorDirection.sqrMagnitude <= 0.0001f)
        {
            randomFloorDirection = Vector2.right;
        }

        Vector2 outwardDirection = areaOffset.sqrMagnitude > 0.0001f
            ? areaOffset.normalized
            : randomFloorDirection;
        Vector2 hitPushDirection = Rotate(hitDirection, angleOffset).normalized;
        Vector2 direction = Vector2.Lerp(randomFloorDirection, outwardDirection, outwardScatterInfluence).normalized;
        return Vector2.Lerp(direction, hitPushDirection, hitDirectionInfluence).normalized;
    }

    private Sprite CreateFragmentSprite(Sprite sourceSprite)
    {
        Texture2D texture = sourceSprite.texture;

        if (texture == null)
        {
            return null;
        }

        Rect sourceRect = sourceSprite.rect;
        int size = Mathf.Clamp(fragmentPixelSize, 1, Mathf.FloorToInt(Mathf.Min(sourceRect.width, sourceRect.height)));
        Rect fragmentRect = PickFragmentRect(texture, sourceRect, size);

        return Sprite.Create(
            texture,
            fragmentRect,
            new Vector2(0.5f, 0.5f),
            sourceSprite.pixelsPerUnit,
            0,
            SpriteMeshType.FullRect);
    }

    private Rect PickFragmentRect(Texture2D texture, Rect sourceRect, int size)
    {
        Rect fallback = GetRandomFragmentRect(sourceRect, size);

        if (!texture.isReadable || opaqueAlphaThreshold <= 0f)
        {
            return fallback;
        }

        for (int i = 0; i < opaqueSearchAttempts; i++)
        {
            Rect candidate = GetRandomFragmentRect(sourceRect, size);

            if (HasOpaquePixel(texture, candidate))
            {
                return candidate;
            }
        }

        return fallback;
    }

    private Rect GetRandomFragmentRect(Rect sourceRect, int size)
    {
        int minX = Mathf.RoundToInt(sourceRect.xMin);
        int minY = Mathf.RoundToInt(sourceRect.yMin);
        int maxX = Mathf.Max(minX, Mathf.RoundToInt(sourceRect.xMax) - size);
        int maxY = Mathf.Max(minY, Mathf.RoundToInt(sourceRect.yMax) - size);
        int x = Random.Range(minX, maxX + 1);
        int y = Random.Range(minY, maxY + 1);
        return new Rect(x, y, size, size);
    }

    private bool HasOpaquePixel(Texture2D texture, Rect rect)
    {
        int minX = Mathf.RoundToInt(rect.xMin);
        int minY = Mathf.RoundToInt(rect.yMin);
        int maxX = Mathf.RoundToInt(rect.xMax);
        int maxY = Mathf.RoundToInt(rect.yMax);

        for (int y = minY; y < maxY; y++)
        {
            for (int x = minX; x < maxX; x++)
            {
                if (texture.GetPixel(x, y).a >= opaqueAlphaThreshold)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void ResetState()
    {
        if (activeShakeRoutine != null)
        {
            StopActiveShake(restorePosition: true);
        }

        broken = false;
        currentHealth = maxHealth;
        enabled = true;
        shakeBaseLocalPosition = transform.localPosition;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        if (colliders == null)
        {
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = true;
            }
        }
    }

    private void CacheComponents()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        colliders = GetComponents<Collider2D>();
    }

    private static Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos);
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        secondHitBreakChance = Mathf.Max(firstHitBreakChance, secondHitBreakChance);
        thirdHitBreakChance = Mathf.Max(secondHitBreakChance, thirdHitBreakChance);
        resolvedBreakGroupCacheDuration = Mathf.Max(0f, resolvedBreakGroupCacheDuration);
        destroyDelay = Mathf.Max(0f, destroyDelay);
        fragmentCount = Mathf.Max(1, fragmentCount);
        fragmentPixelSize = Mathf.Clamp(fragmentPixelSize, 2, 4);
        opaqueSearchAttempts = Mathf.Max(1, opaqueSearchAttempts);
        fragmentSpawnAreaSize.x = Mathf.Max(0f, fragmentSpawnAreaSize.x);
        fragmentSpawnAreaSize.y = Mathf.Max(0f, fragmentSpawnAreaSize.y);
        spawnRadius = Mathf.Max(0f, spawnRadius);
        speedRange = ValidateRange(speedRange, 0f);
        scatterDurationRange = ValidateRange(scatterDurationRange, 0.01f);
        settledLifetimeRange = ValidateRange(settledLifetimeRange, 0f);
        settledFadeDurationRange = ValidateRange(settledFadeDurationRange, 0.01f);
        angularSpeedRange = ValidateRange(angularSpeedRange, 0f);
        velocityDamping = Mathf.Max(0f, velocityDamping);
        breakShakeDuration = Mathf.Max(0f, breakShakeDuration);
        breakShakeAmplitude = Mathf.Max(0f, breakShakeAmplitude);
        breakShakeFrequency = Mathf.Max(1f, breakShakeFrequency);
        fragmentSpawnDelay = Mathf.Max(0f, fragmentSpawnDelay);
    }

    private static Vector2 ValidateRange(Vector2 range, float minimum)
    {
        range.x = Mathf.Max(minimum, range.x);
        range.y = Mathf.Max(range.x, range.y);
        return range;
    }

    private float GetWorldPixelSize()
    {
        return spriteRenderer != null && spriteRenderer.sprite != null && spriteRenderer.sprite.pixelsPerUnit > 0f
            ? 1f / spriteRenderer.sprite.pixelsPerUnit
            : 0f;
    }
}
