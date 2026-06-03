using UnityEngine;

[RequireComponent(typeof(EnemyMotor))]
[DisallowMultipleComponent]
public class EnemyWanderAI : MonoBehaviour
{
    [Header("Wander")]
    [SerializeField, Min(0f)] private float moveDurationMin = 0.8f;
    [SerializeField, Min(0f)] private float moveDurationMax = 1.8f;
    [SerializeField, Min(0f)] private float idleDurationMin = 0.4f;
    [SerializeField, Min(0f)] private float idleDurationMax = 1.2f;

    [Header("Direction")]
    [SerializeField] private bool allowDiagonalMovement = true;

    private EnemyMotor motor;
    private float nextDecisionTime;
    private bool isMoving;

    private void Awake()
    {
        motor = GetComponent<EnemyMotor>();
    }

    private void OnEnable()
    {
        PickIdle();
    }

    private void Update()
    {
        if (Time.time < nextDecisionTime)
        {
            return;
        }

        if (isMoving)
        {
            PickIdle();
            return;
        }

        PickMove();
    }

    private void PickIdle()
    {
        isMoving = false;
        motor.Stop();
        nextDecisionTime = Time.time + Random.Range(idleDurationMin, idleDurationMax);
    }

    private void PickMove()
    {
        isMoving = true;
        motor.SetMoveDirection(GetRandomDirection());
        nextDecisionTime = Time.time + Random.Range(moveDurationMin, moveDurationMax);
    }

    private Vector2 GetRandomDirection()
    {
        if (allowDiagonalMovement)
        {
            Vector2 direction = Random.insideUnitCircle;
            return direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector2.right;
        }

        return Random.value < 0.5f
            ? new Vector2(Random.value < 0.5f ? -1f : 1f, 0f)
            : new Vector2(0f, Random.value < 0.5f ? -1f : 1f);
    }
}
