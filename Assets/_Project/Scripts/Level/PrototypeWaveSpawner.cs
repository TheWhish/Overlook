using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PrototypeWaveSpawner : MonoBehaviour
{
    private static PrototypeWaveSpawner activeSpawner;

    [Header("Enemy")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private bool useSceneEnemyAsTemplate = true;
    [SerializeField] private bool disableExistingSceneEnemiesOnStart = true;

    [Header("Waves")]
    [SerializeField, Min(0f)] private float firstWaveDelay = 3f;
    [SerializeField, Min(0f)] private float delayBetweenWaves = 3f;
    [SerializeField, Min(1)] private int firstWaveEnemyCount = 1;
    [SerializeField, Min(1)] private int enemiesAddedPerWave = 1;

    [Header("Spawn")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Vector2 fallbackSpawnAreaSize = new Vector2(1.5f, 1f);
    [SerializeField, Min(0f)] private float spawnJitter = 0.15f;
    [SerializeField] private LayerMask spawnBlockedLayers = (1 << 6) | (1 << 8) | (1 << 9);
    [SerializeField, Min(0f)] private float spawnCheckRadius = 0.14f;
    [SerializeField, Min(1)] private int spawnPositionAttempts = 12;

    [Header("Player")]
    [SerializeField] private Health playerHealth;
    [SerializeField] private string playerTag = "Player";

    [Header("Debug")]
    [SerializeField] private bool logWaves = true;
    [SerializeField] private bool logGameOver = true;

    private readonly HashSet<Health> aliveEnemies = new HashSet<Health>();
    private readonly List<Transform> reusableSpawnPoints = new List<Transform>();
    private Coroutine waveRoutine;
    private int currentWaveNumber;
    private bool isGameOver;

    private void Awake()
    {
        ResolvePlayerHealth();
        ResolveEnemyTemplate();
    }

    private void OnEnable()
    {
        if (activeSpawner != null && activeSpawner != this)
        {
            Debug.LogWarning($"Another {nameof(PrototypeWaveSpawner)} is already active. Disabling duplicate '{name}'.", this);
            enabled = false;
            return;
        }

        activeSpawner = this;

        if (playerHealth != null)
        {
            playerHealth.Died += HandlePlayerDied;
        }

        waveRoutine = StartCoroutine(WaveRoutine());
    }

    private void OnDisable()
    {
        if (activeSpawner == this)
        {
            activeSpawner = null;
        }

        if (waveRoutine != null)
        {
            StopCoroutine(waveRoutine);
            waveRoutine = null;
        }

        if (playerHealth != null)
        {
            playerHealth.Died -= HandlePlayerDied;
        }

        foreach (Health enemyHealth in aliveEnemies)
        {
            if (enemyHealth != null)
            {
                enemyHealth.Died -= HandleEnemyDied;
            }
        }

        aliveEnemies.Clear();
    }

    private IEnumerator WaveRoutine()
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning($"{nameof(PrototypeWaveSpawner)} has no enemy prefab.", this);
            yield break;
        }

        PrepareSceneEnemies();

        yield return new WaitForSeconds(firstWaveDelay);

        int enemyCount = firstWaveEnemyCount;

        while (!isGameOver)
        {
            currentWaveNumber++;
            SpawnWave(enemyCount);

            if (logWaves)
            {
                Debug.Log($"Wave {currentWaveNumber} started. Enemies: {enemyCount}", this);
            }

            yield return new WaitUntil(() => isGameOver || !HasTrackedAliveEnemies());

            if (isGameOver)
            {
                yield break;
            }

            if (logWaves)
            {
                Debug.Log($"Wave {currentWaveNumber} cleared.", this);
            }

            enemyCount += enemiesAddedPerWave;
            yield return new WaitForSeconds(delayBetweenWaves);
        }
    }

    private void SpawnWave(int enemyCount)
    {
        RebuildSpawnPointPool();

        for (int i = 0; i < enemyCount; i++)
        {
            Vector3 spawnPosition = GetSpawnPosition(i);
            GameObject enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
            enemy.name = $"{enemyPrefab.name}_Wave{currentWaveNumber}_{i + 1}";
            enemy.SetActive(true);

            Health enemyHealth = enemy.GetComponent<Health>();

            if (enemyHealth == null)
            {
                Debug.LogWarning($"Spawned enemy '{enemy.name}' has no Health component.", enemy);
                continue;
            }

            enemyHealth.RestoreToFull();
            enemyHealth.Died += HandleEnemyDied;
            aliveEnemies.Add(enemyHealth);
        }
    }

    private Vector3 GetSpawnPosition(int enemyIndex)
    {
        Vector3 preferredPosition;

        if (reusableSpawnPoints.Count > 0)
        {
            int spawnPointIndex = Random.Range(0, reusableSpawnPoints.Count);
            Transform spawnPoint = reusableSpawnPoints[spawnPointIndex];
            reusableSpawnPoints.RemoveAt(spawnPointIndex);

            if (spawnPoint != null)
            {
                preferredPosition = spawnPoint.position + GetJitter(enemyIndex);
                return GetClearSpawnPosition(preferredPosition);
            }
        }

        Vector2 areaOffset = new Vector2(
            Random.Range(-fallbackSpawnAreaSize.x * 0.5f, fallbackSpawnAreaSize.x * 0.5f),
            Random.Range(-fallbackSpawnAreaSize.y * 0.5f, fallbackSpawnAreaSize.y * 0.5f));

        preferredPosition = transform.position + (Vector3)areaOffset + GetJitter(enemyIndex);
        return GetClearSpawnPosition(preferredPosition);
    }

    private void RebuildSpawnPointPool()
    {
        reusableSpawnPoints.Clear();

        if (spawnPoints == null)
        {
            return;
        }

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] != null)
            {
                reusableSpawnPoints.Add(spawnPoints[i]);
            }
        }
    }

    private Vector3 GetJitter(int enemyIndex)
    {
        if (spawnJitter <= 0f)
        {
            return Vector3.zero;
        }

        float angle = enemyIndex * 137.5f * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * spawnJitter;
    }

    private Vector3 GetClearSpawnPosition(Vector3 preferredPosition)
    {
        if (IsSpawnPositionClear(preferredPosition))
        {
            return preferredPosition;
        }

        for (int i = 0; i < spawnPositionAttempts; i++)
        {
            float searchRadius = spawnCheckRadius + spawnJitter + 0.08f * (i + 1);
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * searchRadius;
            Vector3 candidatePosition = preferredPosition + offset;

            if (IsSpawnPositionClear(candidatePosition))
            {
                return candidatePosition;
            }
        }

        Debug.LogWarning($"No clear spawn position found near {preferredPosition}. Using preferred point.", this);
        return preferredPosition;
    }

    private bool IsSpawnPositionClear(Vector3 position)
    {
        if (spawnBlockedLayers.value == 0 || spawnCheckRadius <= 0f)
        {
            return true;
        }

        Collider2D blockingCollider = Physics2D.OverlapCircle(position, spawnCheckRadius, spawnBlockedLayers);
        return blockingCollider == null;
    }

    private void HandleEnemyDied(DamageInfo damageInfo)
    {
        Health deadEnemy = null;

        foreach (Health enemyHealth in aliveEnemies)
        {
            if (enemyHealth != null && enemyHealth.IsDead)
            {
                deadEnemy = enemyHealth;
                break;
            }
        }

        if (deadEnemy == null)
        {
            return;
        }

        deadEnemy.Died -= HandleEnemyDied;
        aliveEnemies.Remove(deadEnemy);
    }

    private bool HasTrackedAliveEnemies()
    {
        aliveEnemies.RemoveWhere(enemyHealth =>
            enemyHealth == null
            || enemyHealth.IsDead
            || !enemyHealth.gameObject.activeInHierarchy);

        return aliveEnemies.Count > 0;
    }

    private void HandlePlayerDied(DamageInfo damageInfo)
    {
        isGameOver = true;

        if (logGameOver)
        {
            Debug.Log("Game over", this);
        }
    }

    private void ResolvePlayerHealth()
    {
        if (playerHealth != null)
        {
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);

        if (player != null)
        {
            playerHealth = player.GetComponent<Health>();
        }
    }

    private void ResolveEnemyTemplate()
    {
        if (enemyPrefab != null || !useSceneEnemyAsTemplate)
        {
            return;
        }

        EnemyDamageReaction sceneEnemy = FindAnyObjectByType<EnemyDamageReaction>();

        if (sceneEnemy == null)
        {
            return;
        }

        enemyPrefab = sceneEnemy.gameObject;
        sceneEnemy.gameObject.SetActive(false);
    }

    private void PrepareSceneEnemies()
    {
        if (!disableExistingSceneEnemiesOnStart)
        {
            return;
        }

        EnemyDamageReaction[] sceneEnemies = FindObjectsByType<EnemyDamageReaction>(FindObjectsInactive.Exclude);

        for (int i = 0; i < sceneEnemies.Length; i++)
        {
            EnemyDamageReaction sceneEnemy = sceneEnemies[i];

            if (sceneEnemy == null)
            {
                continue;
            }

            sceneEnemy.gameObject.SetActive(false);
        }
    }

    private void OnValidate()
    {
        firstWaveDelay = Mathf.Max(0f, firstWaveDelay);
        delayBetweenWaves = Mathf.Max(0f, delayBetweenWaves);
        firstWaveEnemyCount = Mathf.Max(1, firstWaveEnemyCount);
        enemiesAddedPerWave = Mathf.Max(1, enemiesAddedPerWave);
        fallbackSpawnAreaSize.x = Mathf.Max(0f, fallbackSpawnAreaSize.x);
        fallbackSpawnAreaSize.y = Mathf.Max(0f, fallbackSpawnAreaSize.y);
        spawnJitter = Mathf.Max(0f, spawnJitter);
        spawnCheckRadius = Mathf.Max(0f, spawnCheckRadius);
        spawnPositionAttempts = Mathf.Max(1, spawnPositionAttempts);
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnCheckRadius <= 0f)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 1f, 0.45f, 0.55f);

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] != null)
                {
                    Gizmos.DrawWireSphere(spawnPoints[i].position, spawnCheckRadius);
                }
            }

            return;
        }

        Gizmos.DrawWireSphere(transform.position, spawnCheckRadius);
    }
}
