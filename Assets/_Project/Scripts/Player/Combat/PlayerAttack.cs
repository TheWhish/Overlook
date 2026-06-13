using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerStamina))]
[RequireComponent(typeof(Animator))]
[DisallowMultipleComponent]
public class PlayerAttack : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");

    private const string DefaultAttackTriggerParameter = "Attack";
    private const float PostHitFacingLockTime = 0.12f;

    [Header("Input")]
    [SerializeField] private KeyCode attackKey = KeyCode.Mouse0;

    [Header("Attack")]
    [SerializeField, Min(0f)] private float staminaCost = 18f;
    [SerializeField, Min(0f)] private float attackCooldown = 0.12f;

    [Header("Animation")]
    [SerializeField] private string attackTriggerParameter = DefaultAttackTriggerParameter;

    [Header("Hitbox Timing")]
    [SerializeField, Min(0f)] private float hitboxDelay = 0.16f;
    [SerializeField, Min(0.01f)] private float hitboxActiveTime = 0.28f;

    [Header("Hitbox")]
    [SerializeField] private PlayerAttackHitbox attackHitbox;

    private PlayerStamina stamina;
    private Animator animator;
    private PlayerController playerController;
    private PlayerHurtReaction hurtReaction;
    private Camera mainCamera;

    private Coroutine attackRoutine;
    private float lockedHorizontalDirection = 1f;
    private float facingLockUntil;
    private float nextAttackAllowedTime;
    private int attackTriggerHash;
    private bool hasAttackTrigger;

    public bool IsFacingLocked => Time.time < facingLockUntil;
    public float LockedHorizontalDirection => lockedHorizontalDirection;

    private void Awake()
    {
        stamina = GetComponent<PlayerStamina>();
        animator = GetComponent<Animator>();
        playerController = GetComponent<PlayerController>();
        hurtReaction = GetComponent<PlayerHurtReaction>();
        mainCamera = Camera.main;

        CacheAnimatorParameters();

        if (attackHitbox != null)
        {
            attackHitbox.DisableHitbox();
        }
    }

    private void OnDisable()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (attackHitbox != null)
        {
            attackHitbox.DisableHitbox();
        }

        ResetAttackTrigger();
        facingLockUntil = 0f;
    }

    private void Update()
    {
        if (ShouldIgnoreAttackInput())
        {
            return;
        }

        if (Input.GetKeyDown(attackKey))
        {
            TryAttack();
        }
    }

    private void TryAttack()
    {
        if (ShouldIgnoreAttackInput())
        {
            return;
        }

        if (attackRoutine != null)
        {
            return;
        }

        if (Time.time < nextAttackAllowedTime)
        {
            return;
        }

        if (hurtReaction != null && hurtReaction.IsHurting)
        {
            return;
        }

        if (!stamina.HasStamina)
        {
            return;
        }

        if (!stamina.TrySpend(staminaCost))
        {
            return;
        }

        float attackHorizontalDirection = GetAttackHorizontalDirection();
        lockedHorizontalDirection = attackHorizontalDirection;

        attackRoutine = StartCoroutine(AttackRoutine(attackHorizontalDirection));
    }

    private IEnumerator AttackRoutine(float attackHorizontalDirection)
    {
        float attackStartTime = Time.time;

        TriggerAttackAnimation(attackHorizontalDirection);
        UpdateHitboxPosition(attackHorizontalDirection);

        if (hitboxDelay > 0f)
        {
            yield return new WaitForSeconds(hitboxDelay);
        }

        float currentHitboxActiveTime = hitboxActiveTime;

        if (attackHitbox != null)
        {
            currentHitboxActiveTime = attackHitbox.EnableHitbox(hitboxActiveTime);
        }

        facingLockUntil = attackStartTime + hitboxDelay + currentHitboxActiveTime + PostHitFacingLockTime;

        yield return new WaitForSeconds(currentHitboxActiveTime);

        if (attackHitbox != null)
        {
            attackHitbox.DisableHitbox();
        }

        attackRoutine = null;
        nextAttackAllowedTime = Time.time + attackCooldown;
    }

    private void TriggerAttackAnimation(float attackHorizontalDirection)
    {
        if (animator == null || !hasAttackTrigger)
        {
            return;
        }

        if (playerController != null)
        {
            animator.SetFloat(SpeedHash, playerController.HasMovementInput ? 1f : 0f);
            animator.SetBool(IsRunningHash, playerController.IsRunning);
        }

        animator.SetFloat(HorizontalHash, attackHorizontalDirection);
        animator.ResetTrigger(attackTriggerHash);
        animator.SetTrigger(attackTriggerHash);
    }

    private void ResetAttackTrigger()
    {
        if (animator != null && hasAttackTrigger)
        {
            animator.ResetTrigger(attackTriggerHash);
        }
    }

    private void CacheAnimatorParameters()
    {
        attackTriggerHash = Animator.StringToHash(attackTriggerParameter);
        hasAttackTrigger = false;

        if (animator == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == attackTriggerHash && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                hasAttackTrigger = true;
                return;
            }
        }

        Debug.LogWarning(
            $"Animator on {name} has no trigger parameter '{attackTriggerParameter}'. Attack gameplay will work, but no attack animation trigger will be sent.",
            this);
    }

    private float GetAttackHorizontalDirection()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            float currentHorizontal = animator != null
                ? animator.GetFloat(HorizontalHash)
                : 1f;

            return currentHorizontal < 0f ? -1f : 1f;
        }

        Vector3 playerScreenPosition = mainCamera.WorldToScreenPoint(transform.position);
        return Input.mousePosition.x >= playerScreenPosition.x ? 1f : -1f;
    }

    private void UpdateHitboxPosition(float attackHorizontalDirection)
    {
        if (attackHitbox == null)
        {
            return;
        }

        attackHitbox.SetFacingDirection(attackHorizontalDirection);
    }

    private bool ShouldIgnoreAttackInput()
    {
        return GameplayInputGate.IsGameplayInputBlocked
            || GameplayInputGate.IsPointerOverUi();
    }

    public void CancelAttack()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (attackHitbox != null)
        {
            attackHitbox.DisableHitbox();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
            CacheAnimatorParameters();
        }

        ResetAttackTrigger();
        facingLockUntil = 0f;
    }

    private void OnValidate()
    {
        staminaCost = Mathf.Max(0f, staminaCost);
        attackCooldown = Mathf.Max(0f, attackCooldown);
        hitboxDelay = Mathf.Max(0f, hitboxDelay);
        hitboxActiveTime = Mathf.Max(0.01f, hitboxActiveTime);

        if (string.IsNullOrWhiteSpace(attackTriggerParameter))
        {
            attackTriggerParameter = DefaultAttackTriggerParameter;
        }
    }
}
