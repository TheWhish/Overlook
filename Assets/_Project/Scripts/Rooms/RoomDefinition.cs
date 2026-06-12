using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RoomDefinition : MonoBehaviour
{
    [SerializeField] private string roomId;
    [SerializeField] private bool isStartRoom;
    [SerializeField, Min(0f)] private float enemySpawnRadius = 0.12f;
    [SerializeField] private Collider2D roomBounds;
    [SerializeField] private Transform playerSpawn;
    [SerializeField] private Transform[] enemySpawnPoints = new Transform[0];
    [SerializeField] private RoomExit[] exits = new RoomExit[0];

    private readonly List<Transform> discoveredEnemySpawns = new List<Transform>();
    private readonly List<RoomExit> discoveredExits = new List<RoomExit>();
    private readonly List<Health> spawnedEnemyHealth = new List<Health>();

    private Transform runtimeEnemiesRoot;
    private bool enemiesSpawned;

    public string RoomId => string.IsNullOrWhiteSpace(roomId) ? gameObject.name.Replace("(Clone)", string.Empty).Trim() : roomId;
    public bool IsStartRoom => isStartRoom;
    public int EnemySpawnCount => enemySpawnPoints != null ? enemySpawnPoints.Length : 0;
    public int ExitCount => exits != null ? exits.Length : 0;
    public bool HasAliveSpawnedEnemies
    {
        get
        {
            RemoveFinishedEnemies();
            return spawnedEnemyHealth.Count > 0;
        }
    }

    public void SetRuntimeRoomId(string runtimeRoomId)
    {
        if (!string.IsNullOrWhiteSpace(runtimeRoomId))
        {
            roomId = runtimeRoomId;
        }
    }

    private void Awake()
    {
        DiscoverMarkers();
    }

    public void PrepareRuntime()
    {
        DiscoverMarkers();
        ConfigureExits();
    }

    public RoomExit GetExit(RoomDirection direction)
    {
        if (exits == null)
        {
            return null;
        }

        for (int i = 0; i < exits.Length; i++)
        {
            RoomExit roomExit = exits[i];

            if (roomExit != null && roomExit.Direction == direction)
            {
                return roomExit;
            }
        }

        return null;
    }

    public Vector3 GetPlayerStartPosition()
    {
        return playerSpawn != null ? playerSpawn.position : transform.position;
    }

    public Vector3 GetEntryPosition(RoomDirection entryDirection, float entrySpawnPadding, Collider2D playerCollider)
    {
        RoomExit entryExit = GetExit(entryDirection);

        if (entryExit == null)
        {
            return GetPlayerStartPosition();
        }

        Vector3 entryPosition = entryExit.SpawnPosition;
        Vector2 inwardDirection = RoomDirectionUtility.ToEntryInwardVector(entryDirection);
        float clearance = GetEntryClearance(entryExit, entryDirection, playerCollider, entrySpawnPadding);
        return entryPosition + new Vector3(inwardDirection.x, inwardDirection.y, 0f) * clearance;
    }

    private float GetEntryClearance(
        RoomExit entryExit,
        RoomDirection entryDirection,
        Collider2D playerCollider,
        float entrySpawnPadding)
    {
        if (entryDirection == RoomDirection.None)
        {
            return 0f;
        }

        float exitHalfExtent = entryExit != null ? entryExit.GetHalfExtentAlong(entryDirection) : 0f;
        float playerHalfExtent = GetPlayerHalfExtentAlong(playerCollider, entryDirection);
        return exitHalfExtent + playerHalfExtent + Mathf.Max(0f, entrySpawnPadding);
    }

    private static float GetPlayerHalfExtentAlong(Collider2D playerCollider, RoomDirection entryDirection)
    {
        if (playerCollider == null)
        {
            return 0f;
        }

        Bounds bounds = playerCollider.bounds;

        switch (entryDirection)
        {
            case RoomDirection.Left:
            case RoomDirection.Right:
                return bounds.extents.x;
            case RoomDirection.Top:
            case RoomDirection.Bottom:
                return bounds.extents.y;
            default:
                return 0f;
        }
    }

    public Vector3 GetCameraCenter()
    {
        if (roomBounds != null)
        {
            return roomBounds.bounds.center;
        }

        if (TryGetRendererCenter(out Vector3 rendererCenter))
        {
            return rendererCenter;
        }

        return transform.position;
    }

    public int SpawnEnemiesOnce(IReadOnlyList<GameObject> defaultEnemyPrefabs, float defaultSpawnRadius, RoomDirection entryDirection)
    {
        if (enemiesSpawned)
        {
            return 0;
        }

        enemiesSpawned = true;

        if (enemySpawnPoints == null || enemySpawnPoints.Length == 0)
        {
            return 0;
        }

        Transform enemyRoot = GetRuntimeEnemiesRoot();
        float fallbackRadius = defaultSpawnRadius >= 0f ? defaultSpawnRadius : enemySpawnRadius;
        int spawnedCount = 0;

        for (int i = 0; i < enemySpawnPoints.Length; i++)
        {
            Transform spawnPoint = enemySpawnPoints[i];

            if (spawnPoint == null)
            {
                continue;
            }

            RoomEnemySpawnPoint spawnConfig = spawnPoint.GetComponent<RoomEnemySpawnPoint>();

            if (IsSpawnPointDisabledForEntry(spawnPoint, spawnConfig, entryDirection))
            {
                continue;
            }

            GameObject enemyPrefab = spawnConfig != null && spawnConfig.EnemyPrefab != null
                ? spawnConfig.EnemyPrefab
                : GetRandomDefaultEnemyPrefab(defaultEnemyPrefabs);

            if (enemyPrefab == null)
            {
                continue;
            }

            int spawnCount = spawnConfig != null ? spawnConfig.SpawnCount : 1;
            float spawnRadius = spawnConfig != null ? spawnConfig.GetSpawnRadius(fallbackRadius) : fallbackRadius;

            for (int j = 0; j < spawnCount; j++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
                Vector3 spawnPosition = spawnPoint.position + new Vector3(randomOffset.x, randomOffset.y, 0f);
                GameObject enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity, enemyRoot);
                enemy.name = enemyPrefab.name;

                Health enemyHealth = enemy.GetComponent<Health>();

                if (enemyHealth != null)
                {
                    spawnedEnemyHealth.Add(enemyHealth);
                }

                spawnedCount++;
            }
        }

        return spawnedCount;
    }

    private static GameObject GetRandomDefaultEnemyPrefab(IReadOnlyList<GameObject> defaultEnemyPrefabs)
    {
        if (defaultEnemyPrefabs == null || defaultEnemyPrefabs.Count == 0)
        {
            return null;
        }

        int startIndex = Random.Range(0, defaultEnemyPrefabs.Count);

        for (int i = 0; i < defaultEnemyPrefabs.Count; i++)
        {
            GameObject enemyPrefab = defaultEnemyPrefabs[(startIndex + i) % defaultEnemyPrefabs.Count];

            if (enemyPrefab != null)
            {
                return enemyPrefab;
            }
        }

        return null;
    }

    private void RemoveFinishedEnemies()
    {
        for (int i = spawnedEnemyHealth.Count - 1; i >= 0; i--)
        {
            Health enemyHealth = spawnedEnemyHealth[i];

            if (enemyHealth == null || enemyHealth.IsDead)
            {
                spawnedEnemyHealth.RemoveAt(i);
            }
        }
    }

    private bool IsSpawnPointDisabledForEntry(Transform spawnPoint, RoomEnemySpawnPoint spawnConfig, RoomDirection entryDirection)
    {
        if (spawnPoint == null || entryDirection == RoomDirection.None)
        {
            return false;
        }

        if (spawnConfig != null)
        {
            return spawnConfig.IsDisabledForEntry(entryDirection, spawnPoint.name);
        }

        return RoomEnemySpawnPoint.IsMarkerDisabledForEntry(spawnPoint.name, entryDirection);
    }

    private Transform GetRuntimeEnemiesRoot()
    {
        if (runtimeEnemiesRoot != null)
        {
            return runtimeEnemiesRoot;
        }

        GameObject root = new GameObject("RuntimeEnemies");
        runtimeEnemiesRoot = root.transform;
        runtimeEnemiesRoot.SetParent(transform, false);
        return runtimeEnemiesRoot;
    }

    private void DiscoverMarkers()
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            roomId = gameObject.name.Replace("(Clone)", string.Empty).Trim();
        }

        isStartRoom |= RoomId == "Room_Start";

        discoveredEnemySpawns.Clear();
        discoveredExits.Clear();

        Transform[] children = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            string childName = child.name;

            if (roomBounds == null && childName == "Bounds")
            {
                roomBounds = child.GetComponent<Collider2D>();
                continue;
            }

            if (playerSpawn == null && childName == "PlayerSpawn")
            {
                playerSpawn = child;
                continue;
            }

            if (childName.StartsWith("EnemySpawn"))
            {
                discoveredEnemySpawns.Add(child);
                continue;
            }

            if (childName.StartsWith("Exit"))
            {
                Collider2D exitCollider = child.GetComponent<Collider2D>();

                if (exitCollider == null)
                {
                    continue;
                }

                RoomExit roomExit = child.GetComponent<RoomExit>();

                if (roomExit == null)
                {
                    roomExit = child.gameObject.AddComponent<RoomExit>();
                }

                discoveredExits.Add(roomExit);
            }
        }

        enemySpawnPoints = discoveredEnemySpawns.ToArray();
        exits = discoveredExits.ToArray();
        ConfigureExits();
    }

    private bool TryGetRendererCenter(out Vector3 center)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        Bounds combinedBounds = default;
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(renderer.bounds);
        }

        center = hasBounds ? combinedBounds.center : transform.position;
        return hasBounds;
    }

    private void ConfigureExits()
    {
        if (exits == null)
        {
            return;
        }

        for (int i = 0; i < exits.Length; i++)
        {
            if (exits[i] != null)
            {
                exits[i].Configure(this);
            }
        }
    }

    private void OnValidate()
    {
        enemySpawnRadius = Mathf.Max(0f, enemySpawnRadius);
    }
}
