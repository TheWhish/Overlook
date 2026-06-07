using UnityEngine;

[DisallowMultipleComponent]
public sealed class RoomEnemySpawnPoint : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField, Min(1)] private int spawnCount = 1;
    [SerializeField] private bool overrideSpawnRadius;
    [SerializeField, Min(0f)] private float spawnRadius = 0.12f;
    [SerializeField] private RoomDirection disableWhenEnteringFrom = RoomDirection.None;

    public GameObject EnemyPrefab => enemyPrefab;
    public int SpawnCount => Mathf.Max(1, spawnCount);

    public bool IsDisabledForEntry(RoomDirection entryDirection, string markerName)
    {
        if (entryDirection == RoomDirection.None)
        {
            return false;
        }

        RoomDirection disabledDirection = disableWhenEnteringFrom;

        if (disabledDirection == RoomDirection.None)
        {
            TryInferDisabledEntryDirection(markerName, out disabledDirection);
        }

        return disabledDirection == entryDirection;
    }

    public float GetSpawnRadius(float defaultRadius)
    {
        return overrideSpawnRadius ? spawnRadius : defaultRadius;
    }

    public static bool TryInferDisabledEntryDirection(string markerName, out RoomDirection direction)
    {
        direction = RoomDirection.None;

        if (string.IsNullOrWhiteSpace(markerName) || !markerName.StartsWith("EnemySpawn"))
        {
            return false;
        }

        string markerSuffix = markerName.Substring("EnemySpawn".Length).TrimStart('_', '-', ' ');

        if (string.IsNullOrWhiteSpace(markerSuffix))
        {
            return false;
        }

        int separatorIndex = markerSuffix.IndexOfAny(new[] { '_', '-', ' ' });
        string firstToken = separatorIndex >= 0 ? markerSuffix.Substring(0, separatorIndex) : markerSuffix;

        return TryParseDirectionToken(firstToken, out direction);
    }

    private static bool TryParseDirectionToken(string token, out RoomDirection direction)
    {
        switch (token.ToLowerInvariant())
        {
            case "left":
                direction = RoomDirection.Left;
                return true;
            case "right":
                direction = RoomDirection.Right;
                return true;
            case "top":
            case "up":
            case "north":
                direction = RoomDirection.Top;
                return true;
            case "bottom":
            case "down":
            case "south":
                direction = RoomDirection.Bottom;
                return true;
            default:
                direction = RoomDirection.None;
                return false;
        }
    }

    private void OnValidate()
    {
        spawnCount = Mathf.Max(1, spawnCount);
        spawnRadius = Mathf.Max(0f, spawnRadius);
    }
}
