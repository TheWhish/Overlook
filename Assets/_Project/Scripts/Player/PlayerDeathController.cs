using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Animator))]
[DisallowMultipleComponent]
public sealed class PlayerDeathController : MonoBehaviour
{
    private const string DefaultDeathTriggerParameter = "Death";
    private const string DefaultIsDeadParameter = "IsDead";
    private const string DefaultSpeedParameter = "Speed";
    private const string DefaultIsRunningParameter = "IsRunning";

    [Header("Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Animation")]
    [SerializeField] private string deathTriggerParameter = DefaultDeathTriggerParameter;
    [SerializeField] private string isDeadParameter = DefaultIsDeadParameter;
    [SerializeField, Min(0f)] private float deathAnimationDuration = 1.2f;

    [Header("Fade")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.95f;
    [SerializeField, Min(0f)] private float fadeCompletesAfterAnimationEnd = 0.08f;

    private Health health;
    private Animator animator;
    private Rigidbody2D body;
    private PlayerAttack playerAttack;
    private PlayerHurtReaction hurtReaction;

    private int deathTriggerHash;
    private int isDeadHash;
    private int speedHash;
    private int isRunningHash;

    private bool hasDeathTrigger;
    private bool hasIsDeadParameter;
    private bool hasSpeedParameter;
    private bool hasIsRunningParameter;
    private bool isDying;
    private bool disabledHurtReactionForDeath;

    private void Awake()
    {
        health = GetComponent<Health>();
        animator = GetComponent<Animator>();
        body = GetComponent<Rigidbody2D>();
        playerAttack = GetComponent<PlayerAttack>();
        hurtReaction = GetComponent<PlayerHurtReaction>();

        CacheAnimatorParameters();
    }

    private void OnEnable()
    {
        health.Died += HandleDied;
        ResetDeathStateIfAlive();
    }

    private void OnDisable()
    {
        health.Died -= HandleDied;
    }

    private void HandleDied(DamageInfo damageInfo)
    {
        if (isDying)
        {
            return;
        }

        isDying = true;
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        FreezePlayer();
        PlayDeathAnimation();

        float fadeStartDelay = Mathf.Max(
            0f,
            deathAnimationDuration + fadeCompletesAfterAnimationEnd - fadeOutDuration);

        if (fadeStartDelay > 0f)
        {
            yield return new WaitForSeconds(fadeStartDelay);
        }

        SceneTransition.LoadScene(mainMenuSceneName, fadeOutDuration);
        GameplayInputGate.SetPlayerDeathBlocked(false);
    }

    private void FreezePlayer()
    {
        GameplayInputGate.SetPlayerDeathBlocked(true);

        if (playerAttack != null)
        {
            playerAttack.CancelAttack();
        }

        if (hurtReaction != null)
        {
            disabledHurtReactionForDeath = hurtReaction.enabled;
            hurtReaction.enabled = false;
        }

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

        if (hasSpeedParameter)
        {
            animator.SetFloat(speedHash, 0f);
        }

        if (hasIsRunningParameter)
        {
            animator.SetBool(isRunningHash, false);
        }
    }

    private void PlayDeathAnimation()
    {
        if (hasIsDeadParameter)
        {
            animator.SetBool(isDeadHash, true);
        }

        if (hasDeathTrigger)
        {
            animator.ResetTrigger(deathTriggerHash);
            animator.SetTrigger(deathTriggerHash);
            return;
        }

        Debug.LogWarning($"Animator on {name} has no '{deathTriggerParameter}' trigger parameter.", this);
    }

    public void ResetDeathState()
    {
        isDying = false;
        GameplayInputGate.SetPlayerDeathBlocked(false);

        if (playerAttack != null)
        {
            playerAttack.CancelAttack();
        }

        if (disabledHurtReactionForDeath && hurtReaction != null)
        {
            hurtReaction.enabled = true;
        }

        disabledHurtReactionForDeath = false;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

        if (hasDeathTrigger)
        {
            animator.ResetTrigger(deathTriggerHash);
        }

        if (hasIsDeadParameter)
        {
            animator.SetBool(isDeadHash, false);
        }

        if (hasSpeedParameter)
        {
            animator.SetFloat(speedHash, 0f);
        }

        if (hasIsRunningParameter)
        {
            animator.SetBool(isRunningHash, false);
        }
    }

    private void ResetDeathStateIfAlive()
    {
        if (health != null && health.IsDead)
        {
            return;
        }

        ResetDeathState();
    }

    private void CacheAnimatorParameters()
    {
        deathTriggerHash = Animator.StringToHash(deathTriggerParameter);
        isDeadHash = Animator.StringToHash(isDeadParameter);
        speedHash = Animator.StringToHash(DefaultSpeedParameter);
        isRunningHash = Animator.StringToHash(DefaultIsRunningParameter);

        hasDeathTrigger = false;
        hasIsDeadParameter = false;
        hasSpeedParameter = false;
        hasIsRunningParameter = false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == deathTriggerHash && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                hasDeathTrigger = true;
            }
            else if (parameter.nameHash == isDeadHash && parameter.type == AnimatorControllerParameterType.Bool)
            {
                hasIsDeadParameter = true;
            }
            else if (parameter.nameHash == speedHash && parameter.type == AnimatorControllerParameterType.Float)
            {
                hasSpeedParameter = true;
            }
            else if (parameter.nameHash == isRunningHash && parameter.type == AnimatorControllerParameterType.Bool)
            {
                hasIsRunningParameter = true;
            }
        }
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            mainMenuSceneName = "MainMenu";
        }

        if (string.IsNullOrWhiteSpace(deathTriggerParameter))
        {
            deathTriggerParameter = DefaultDeathTriggerParameter;
        }

        if (string.IsNullOrWhiteSpace(isDeadParameter))
        {
            isDeadParameter = DefaultIsDeadParameter;
        }

        deathAnimationDuration = Mathf.Max(0f, deathAnimationDuration);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        fadeCompletesAfterAnimationEnd = Mathf.Max(0f, fadeCompletesAfterAnimationEnd);
    }
}
