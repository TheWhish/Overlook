using UnityEngine;

public enum RoomDirection
{
    None = 0,
    Left = 1,
    Right = 2,
    Top = 3,
    Bottom = 4
}

public static class RoomDirectionUtility
{
    public static RoomDirection Opposite(RoomDirection direction)
    {
        switch (direction)
        {
            case RoomDirection.Left:
                return RoomDirection.Right;
            case RoomDirection.Right:
                return RoomDirection.Left;
            case RoomDirection.Top:
                return RoomDirection.Bottom;
            case RoomDirection.Bottom:
                return RoomDirection.Top;
            default:
                return RoomDirection.None;
        }
    }

    public static Vector2Int ToCellOffset(RoomDirection direction)
    {
        switch (direction)
        {
            case RoomDirection.Left:
                return Vector2Int.left;
            case RoomDirection.Right:
                return Vector2Int.right;
            case RoomDirection.Top:
                return Vector2Int.up;
            case RoomDirection.Bottom:
                return Vector2Int.down;
            default:
                return Vector2Int.zero;
        }
    }

    public static Vector2 ToEntryInwardVector(RoomDirection entryDirection)
    {
        Vector2Int sideOffset = ToCellOffset(entryDirection);
        return new Vector2(-sideOffset.x, -sideOffset.y);
    }

    public static bool TryParseFromName(string markerName, out RoomDirection direction)
    {
        string normalizedName = markerName.Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();

        if (normalizedName.Contains("left"))
        {
            direction = RoomDirection.Left;
            return true;
        }

        if (normalizedName.Contains("right"))
        {
            direction = RoomDirection.Right;
            return true;
        }

        if (normalizedName.Contains("top") || normalizedName.Contains("up") || normalizedName.Contains("north"))
        {
            direction = RoomDirection.Top;
            return true;
        }

        if (normalizedName.Contains("bottom") || normalizedName.Contains("down") || normalizedName.Contains("south"))
        {
            direction = RoomDirection.Bottom;
            return true;
        }

        direction = RoomDirection.None;
        return false;
    }
}
