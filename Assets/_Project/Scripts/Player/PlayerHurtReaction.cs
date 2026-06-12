using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Animator))]
[DisallowMultipleComponent]
public class PlayerHurtReaction : MonoBehaviour
{
    private const float DefaultHurtDuration = 0.5f;
    private const string HurtTriggerParameter = "Hurt";
    private const string HorizontalParameter = "Horizontal";

    [Header("Hurt")]
    [SerializeField, Min(0f)] private float hurtDuration = DefaultHurtDuration;
    [SerializeField] private bool interruptAttack = true;
    [SerializeField] private bool applyKnockback = true;

    private Health health;
    private Animator animator;
    private PlayerAttack playerAttack;
    private KnockbackReceiver knockbackReceiver;
    private Coroutine hurtRoutine;
    private bool isBlockingIncomingDamage;

    private int hurtTriggerHash;
    private int horizontalHash;
    private bool hasHurtTrigger;
    private bool hasHorizontalParameter;

    public bool IsHurting => hurtRoutine != null;
    public bool BlocksMovement => IsHurting;

    private void Awake()
    {
        health = GetComponent<Health>();
        animator = GetComponent<Animator>();
        playerAttack = GetComponent<PlayerAttack>();
        knockbackReceiver = GetComponent<KnockbackReceiver>();

        hurtTriggerHash = Animator.StringToHash(HurtTriggerParameter);
        horizontalHash = Animator.StringToHash(HorizontalParameter);

        CacheAnimatorParameters();
    }

    private void OnEnable()
    {
        health.Damaged += HandleDamaged;
    }

    private void OnDisable()
    {
        health.Damaged -= HandleDamaged;
        ResetReaction();
    }

    public void ResetReaction()
    {
        if (hurtRoutine != null)
        {
            StopCoroutine(hurtRoutine);
            hurtRoutine = null;
        }

        SetIncomingDamageBlocked(false);
    }

    private void HandleDamaged(DamageInfo damageInfo)
    {
        if (health.IsDead)
        {
            return;
        }

        if (hurtRoutine != null)
        {
            return;
        }

        hurtRoutine = StartCoroutine(HurtRoutine(damageInfo));
    }

    private IEnumerator HurtRoutine(DamageInfo damageInfo)
    {
        SetIncomingDamageBlocked(true);

        if (interruptAttack && playerAttack != null)
        {
            playerAttack.CancelAttack();
        }

        if (applyKnockback && knockbackReceiver != null)
        {
            knockbackReceiver.PlayKnockback(damageInfo);
        }

        float hurtHorizontalDirection = GetHurtHorizontalDirection(damageInfo);

        if (hasHorizontalParameter)
        {
            animator.SetFloat(horizontalHash, hurtHorizontalDirection);
        }

        if (hasHurtTrigger)
        {
            animator.ResetTrigger(hurtTriggerHash);
            animator.SetTrigger(hurtTriggerHash);
        }
        else
        {
            Debug.LogWarning($"Animator on {name} has no '{HurtTriggerParameter}' trigger parameter.", this);
        }

        if (hurtDuration > 0f)
        {
            yield return new WaitForSeconds(hurtDuration);
        }

        SetIncomingDamageBlocked(false);
        hurtRoutine = null;
    }

    private void SetIncomingDamageBlocked(bool blocked)
    {
        if (isBlockingIncomingDamage == blocked)
        {
            return;
        }

        isBlockingIncomingDamage = blocked;
        if (health != null)
        {
            health.SetDamageBlocked(blocked);
        }
    }

    private float GetHurtHorizontalDirection(DamageInfo damageInfo)
    {
        if (damageInfo.Source != null)
        {
            return damageInfo.Source.transform.position.x < transform.position.x
                ? -1f
                : 1f;
        }

        float currentHorizontal = hasHorizontalParameter
            ? animator.GetFloat(horizontalHash)
            : 1f;

        return currentHorizontal < 0f ? -1f : 1f;
    }

    private void CacheAnimatorParameters()
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == hurtTriggerHash && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                hasHurtTrigger = true;
            }
            else if (parameter.nameHash == horizontalHash && parameter.type == AnimatorControllerParameterType.Float)
            {
                hasHorizontalParameter = true;
            }
        }
    }
}
