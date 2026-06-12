using System;
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

        if (disableWhenEnteringFrom == entryDirection)
        {
            return true;
        }

        return IsMarkerDisabledForEntry(markerName, entryDirection);
    }

    public float GetSpawnRadius(float defaultRadius)
    {
        return overrideSpawnRadius ? spawnRadius : defaultRadius;
    }

    public static bool TryInferDisabledEntryDirection(string markerName, out RoomDirection direction)
    {
        direction = RoomDirection.None;

        if (!TryGetMarkerTokens(markerName, out string[] tokens))
        {
            return false;
        }

        for (int i = 0; i < tokens.Length; i++)
        {
            if (ShouldIgnoreMarkerToken(tokens[i]))
            {
                continue;
            }

            if (TryParseDirectionToken(tokens[i], out direction))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsMarkerDisabledForEntry(string markerName, RoomDirection entryDirection)
    {
        if (entryDirection == RoomDirection.None || !TryGetMarkerTokens(markerName, out string[] tokens))
        {
            return false;
        }

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];

            if (ShouldIgnoreMarkerToken(token))
            {
                continue;
            }

            if (TryParseDirectionToken(token, out RoomDirection disabledDirection) &&
                disabledDirection == entryDirection)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetMarkerTokens(string markerName, out string[] tokens)
    {
        tokens = null;

        if (string.IsNullOrWhiteSpace(markerName) ||
            !markerName.StartsWith("EnemySpawn", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        tokens = markerName.Split('_');
        return tokens.Length > 0;
    }

    private static bool ShouldIgnoreMarkerToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return true;
        }

        string normalizedToken = token.Trim();

        if (string.Equals(normalizedToken, "EnemySpawn", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (int i = 0; i < normalizedToken.Length; i++)
        {
            if (!char.IsDigit(normalizedToken[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseDirectionToken(string token, out RoomDirection direction)
    {
        switch (token.Trim().ToLowerInvariant())
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
