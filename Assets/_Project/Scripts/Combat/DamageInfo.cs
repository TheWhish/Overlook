using UnityEngine;

public readonly struct DamageInfo
{
    public DamageInfo(
        float amount,
        GameObject source,
        Vector2 hitPoint,
        Vector2 hitDirection,
        int hitGroupId = 0,
        float hitGroupEndTime = 0f,
        string hitGroupKey = null)
    {
        Amount = amount;
        Source = source;
        HitPoint = hitPoint;
        HitDirection = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : Vector2.zero;
        HitGroupId = hitGroupId;
        HitGroupEndTime = hitGroupEndTime;
        HitGroupKey = hitGroupKey;
    }

    public float Amount { get; }
    public GameObject Source { get; }
    public Vector2 HitPoint { get; }
    public Vector2 HitDirection { get; }
    public int HitGroupId { get; }
    public float HitGroupEndTime { get; }
    public string HitGroupKey { get; }
}
