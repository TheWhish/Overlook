using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerStamina))]
[RequireComponent(typeof(Animator))]
[DisallowMultipleComponent]
public class PlayerAttack : MonoBehaviour
{
    private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");

    private const float AttackIdleDuration = 0.67f;
    private const float AttackWalkDuration = 0.50f;
    private const float AttackRunDuration = 0.67f;
    private const float PostAttackFacingLockTime = 0.12f;

    [Header("Input")]
    [SerializeField] private KeyCode attackKey = KeyCode.Mouse0;

    [Header("Attack")]
    [SerializeField, Min(0f)] private float staminaCost = 18f;

    [Header("Hitbox Timing")]
    [SerializeField, Min(0f)] private float hitboxDelay = 0.08f;
    [SerializeField, Min(0f)] private float hitboxActiveTime = 0.10f;

    [Header("Hitbox")]
    [SerializeField] private PlayerAttackHitbox attackHitbox;
    [SerializeField] private Vector2 rightHitboxOffset = new Vector2(0.18f, 0.08f);
    [SerializeField] private Vector2 leftHitboxOffset = new Vector2(-0.18f, 0.08f);

    private PlayerStamina stamina;
    private Animator animator;
    private PlayerController playerController;
    private PlayerHurtReaction hurtReaction;
    private Camera mainCamera;

    private Coroutine attackRoutine;
    private float lockedHorizontalDirection = 1f;
    private float facingLockUntil;

    public bool IsAttacking => attackRoutine != null;
    public bool IsFacingLocked => Time.time < facingLockUntil;
    public float LockedHorizontalDirection => lockedHorizontalDirection;

    private void Awake()
    {
        stamina = GetComponent<PlayerStamina>();
        animator = GetComponent<Animator>();
        playerController = GetComponent<PlayerController>();
        hurtReaction = GetComponent<PlayerHurtReaction>();
        mainCamera = Camera.main;

        if (attackHitbox != null)
        {
            attackHitbox.DisableHitbox();
        }

        animator.SetBool(IsAttackingHash, false);
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

        if (animator != null)
        {
            animator.SetBool(IsAttackingHash, false);
        }

        facingLockUntil = 0f;
    }

    private void Update()
    {
        if (Input.GetKeyDown(attackKey))
        {
            TryAttack();
        }
    }

    private void TryAttack()
    {
        if (attackRoutine != null)
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
        float currentAttackDuration = GetAttackDuration();

        facingLockUntil = Time.time + currentAttackDuration + PostAttackFacingLockTime;

        if (playerController != null)
        {
            animator.SetFloat(SpeedHash, playerController.HasMovementInput ? 1f : 0f);
            animator.SetBool(IsRunningHash, playerController.IsRunning);
        }

        animator.SetFloat(HorizontalHash, attackHorizontalDirection);
        animator.SetBool(IsAttackingHash, true);

        UpdateHitboxPosition(attackHorizontalDirection);

        yield return new WaitForSeconds(hitboxDelay);

        if (attackHitbox != null)
        {
            attackHitbox.EnableHitbox();
        }

        yield return new WaitForSeconds(hitboxActiveTime);

        if (attackHitbox != null)
        {
            attackHitbox.DisableHitbox();
        }

        float remainingAttackTime = currentAttackDuration - hitboxDelay - hitboxActiveTime;

        if (remainingAttackTime > 0f)
        {
            yield return new WaitForSeconds(remainingAttackTime);
        }

        animator.SetBool(IsAttackingHash, false);

        attackRoutine = null;
    }

    private float GetAttackDuration()
    {
        if (playerController == null || !playerController.HasMovementInput)
        {
            return AttackIdleDuration;
        }

        if (playerController.IsRunning)
        {
            return AttackRunDuration;
        }

        return AttackWalkDuration;
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

        Vector2 offset = attackHorizontalDirection > 0f
            ? rightHitboxOffset
            : leftHitboxOffset;

        attackHitbox.transform.localPosition = offset;
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

        animator.SetBool(IsAttackingHash, false);
        facingLockUntil = 0f;
    }

    private void OnValidate()
    {
        staminaCost = Mathf.Max(0f, staminaCost);
        hitboxDelay = Mathf.Max(0f, hitboxDelay);
        hitboxActiveTime = Mathf.Max(0f, hitboxActiveTime);
    }
}
