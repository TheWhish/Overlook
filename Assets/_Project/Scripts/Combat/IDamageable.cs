public interface IDamageable
{
    bool CanTakeDamage { get; }

    void TakeDamage(DamageInfo damageInfo);
}
