using UnityEngine;

public readonly struct DamageInfo
{
    public DamageInfo(float amount, GameObject source, Vector2 hitPoint, Vector2 hitDirection)
    {
        Amount = amount;
        Source = source;
        HitPoint = hitPoint;
        HitDirection = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : Vector2.zero;
    }

    public float Amount { get; }
    public GameObject Source { get; }
    public Vector2 HitPoint { get; }
    public Vector2 HitDirection { get; }
}
