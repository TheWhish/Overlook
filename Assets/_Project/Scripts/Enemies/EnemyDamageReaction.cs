using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Animator))]
[DisallowMultipleComponent]
public class EnemyDamageReaction : MonoBehaviour
{
    private const string HurtTriggerParameter = "Hurt";
    private const string DeathTriggerParameter = "Death";
    private const string IsDeadParameter = "IsDead";
    private const string HorizontalParameter = "Horizontal";

    [Header("Hurt")]
    [SerializeField, Min(0f)] private float hurtLockDuration = 0.18f;
    [SerializeField] private bool stopMovementOnHurt = true;
    [SerializeField] private bool interruptAttackOnHurt = true;
    [SerializeField, Min(0f)] private float attackCooldownOnHurtMultiplier = 1f;
    [SerializeField] private bool applyKnockbackOnHurt = true;
    [SerializeField, Min(0f)] private float fullReactionCooldown = 0.8f;

    [Header("Death")]
    [SerializeField, Min(0f)] private float destroyDelay = 1f;
    [SerializeField] private bool disableCollidersOnDeath = true;
    [SerializeField] private bool disableBehaviourOnDeath = true;

    private Health health;
    private Animator animator;
    private EnemyMotor motor;
    private EnemyAttack enemyAttack;
    private KnockbackReceiver knockbackReceiver;
    private Rigidbody2D rb;
    private Collider2D[] colliders;
    private Behaviour[] behaviours;
    private Coroutine hurtRoutine;
    private Coroutine deathRoutine;
    private float lastDamageTime = -999f;
    private float nextFullReactionTime;

    private int hurtTriggerHash;
    private int deathTriggerHash;
    private int isDeadHash;
    private int horizontalHash;

    private bool hasHurtTrigger;
    private bool hasDeathTrigger;
    private bool hasIsDeadParameter;
    private bool hasHorizontalParameter;

    public bool IsReactingToDamage => hurtRoutine != null;
    public bool IsDead => health != null && health.IsDead;
    public bool BlocksMovement => IsReactingToDamage || IsDead;
    public float LastDamageTime => lastDamageTime;

    private void Awake()
    {
        health = GetComponent<Health>();
        animator = GetComponent<Animator>();
        motor = GetComponent<EnemyMotor>();
        enemyAttack = GetComponent<EnemyAttack>();
        knockbackReceiver = GetComponent<KnockbackReceiver>();
        rb = GetComponent<Rigidbody2D>();
        colliders = GetComponents<Collider2D>();
        behaviours = GetComponents<Behaviour>();

        hurtTriggerHash = Animator.StringToHash(HurtTriggerParameter);
        deathTriggerHash = Animator.StringToHash(DeathTriggerParameter);
        isDeadHash = Animator.StringToHash(IsDeadParameter);
        horizontalHash = Animator.StringToHash(HorizontalParameter);

        CacheAnimatorParameters();
    }

    private void OnEnable()
    {
        health.Damaged += HandleDamaged;
        health.Died += HandleDied;
    }

    private void OnDisable()
    {
        health.Damaged -= HandleDamaged;
        health.Died -= HandleDied;

        if (hurtRoutine != null)
        {
            StopCoroutine(hurtRoutine);
            hurtRoutine = null;
        }

        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }
    }

    private void HandleDamaged(DamageInfo damageInfo)
    {
        lastDamageTime = Time.time;

        if (health.IsDead)
        {
            return;
        }

        if (Time.time < nextFullReactionTime)
        {
            SetHorizontalFromDamage(damageInfo);
            return;
        }

        nextFullReactionTime = Time.time + fullReactionCooldown;

        if (hurtRoutine != null)
        {
            StopCoroutine(hurtRoutine);
        }

        hurtRoutine = StartCoroutine(HurtRoutine(damageInfo));
    }

    public bool WasDamagedRecently(float duration)
    {
        return Time.time - lastDamageTime <= duration;
    }

    private void HandleDied(DamageInfo damageInfo)
    {
        if (deathRoutine != null)
        {
            return;
        }

        deathRoutine = StartCoroutine(DeathRoutine(damageInfo));
    }

    private IEnumerator HurtRoutine(DamageInfo damageInfo)
    {
        if (stopMovementOnHurt && motor != null)
        {
            motor.Stop();
        }

        if (interruptAttackOnHurt && enemyAttack != null)
        {
            enemyAttack.InterruptAttack(attackCooldownOnHurtMultiplier);
        }

        StopPhysicsVelocity();

        if (applyKnockbackOnHurt && knockbackReceiver != null)
        {
            knockbackReceiver.PlayKnockback(damageInfo);
        }

        SetHorizontalFromDamage(damageInfo);

        if (hasHurtTrigger)
        {
            animator.ResetTrigger(hurtTriggerHash);
            animator.SetTrigger(hurtTriggerHash);
        }
        else
        {
            Debug.LogWarning($"Animator on {name} has no '{HurtTriggerParameter}' trigger parameter.", this);
        }

        if (hurtLockDuration > 0f)
        {
            yield return new WaitForSeconds(hurtLockDuration);
        }

        hurtRoutine = null;
    }

    private IEnumerator DeathRoutine(DamageInfo damageInfo)
    {
        if (hurtRoutine != null)
        {
            StopCoroutine(hurtRoutine);
            hurtRoutine = null;
        }

        if (motor != null)
        {
            motor.Stop();
        }

        StopPhysicsVelocity();

        SetHorizontalFromDamage(damageInfo);

        if (hasIsDeadParameter)
        {
            animator.SetBool(isDeadHash, true);
        }

        if (hasDeathTrigger)
        {
            if (hasHurtTrigger)
            {
                animator.ResetTrigger(hurtTriggerHash);
            }

            animator.ResetTrigger(deathTriggerHash);
            animator.SetTrigger(deathTriggerHash);
        }
        else
        {
            Debug.LogWarning($"Animator on {name} has no '{DeathTriggerParameter}' trigger parameter.", this);
        }

        if (disableCollidersOnDeath)
        {
            SetCollidersEnabled(false);
        }

        if (disableBehaviourOnDeath)
        {
            DisableEnemyBehaviours();
        }

        if (destroyDelay > 0f)
        {
            yield return new WaitForSeconds(destroyDelay);
        }

        Destroy(gameObject);
    }

    private void SetHorizontalFromDamage(DamageInfo damageInfo)
    {
        if (!hasHorizontalParameter)
        {
            return;
        }

        float horizontalDirection = GetHorizontalDirection(damageInfo);
        animator.SetFloat(horizontalHash, horizontalDirection);
    }

    private float GetHorizontalDirection(DamageInfo damageInfo)
    {
        if (damageInfo.Source != null)
        {
            return damageInfo.Source.transform.position.x < transform.position.x
                ? -1f
                : 1f;
        }

        if (damageInfo.HitDirection.sqrMagnitude > 0.0001f)
        {
            return damageInfo.HitDirection.x < 0f ? -1f : 1f;
        }

        float currentHorizontal = hasHorizontalParameter
            ? animator.GetFloat(horizontalHash)
            : 1f;

        return currentHorizontal < 0f ? -1f : 1f;
    }

    private void SetCollidersEnabled(bool isEnabled)
    {
        foreach (Collider2D enemyCollider in colliders)
        {
            if (enemyCollider != null)
            {
                enemyCollider.enabled = isEnabled;
            }
        }
    }

    private void StopPhysicsVelocity()
    {
        if (rb == null)
        {
            return;
        }

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    private void DisableEnemyBehaviours()
    {
        foreach (Behaviour behaviour in behaviours)
        {
            if (behaviour == null || behaviour == this || behaviour == animator)
            {
                continue;
            }

            if (behaviour is EnemyMotor
                || behaviour is EnemyAnimator
                || behaviour is EnemyHopAI
                || behaviour is EnemyWanderAI
                || behaviour is EnemyTargetSensor
                || behaviour is EnemyAttack
                || behaviour is KnockbackReceiver)
            {
                behaviour.enabled = false;
            }
        }
    }

    private void OnValidate()
    {
        hurtLockDuration = Mathf.Max(0f, hurtLockDuration);
        destroyDelay = Mathf.Max(0f, destroyDelay);
        attackCooldownOnHurtMultiplier = Mathf.Max(0f, attackCooldownOnHurtMultiplier);
        fullReactionCooldown = Mathf.Max(0f, fullReactionCooldown);
    }

    private void CacheAnimatorParameters()
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == hurtTriggerHash && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                hasHurtTrigger = true;
            }
            else if (parameter.nameHash == deathTriggerHash && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                hasDeathTrigger = true;
            }
            else if (parameter.nameHash == isDeadHash && parameter.type == AnimatorControllerParameterType.Bool)
            {
                hasIsDeadParameter = true;
            }
            else if (parameter.nameHash == horizontalHash && parameter.type == AnimatorControllerParameterType.Float)
            {
                hasHorizontalParameter = true;
            }
        }
    }
}
