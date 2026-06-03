using UnityEngine;

[DisallowMultipleComponent]
public class PlayerCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Follow")]
    [SerializeField] private Vector2 targetOffset = new Vector2(0f, 0.14f);
    [SerializeField, Min(0f)] private float smoothTime = 0.2f;
    [SerializeField, Min(0f)] private float maxSpeed = 10f;

    [Header("Dead Zone")]
    [SerializeField] private Vector2 deadZoneSize = new Vector2(0.25f, 0.2f);

    [Header("Pixel Art")]
    [SerializeField] private bool pixelSnap = false;
    [SerializeField, Min(1)] private int pixelsPerUnit = 100;

    private Vector3 velocity;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 currentPosition = transform.position;
        Vector3 targetPosition = GetTargetFollowPosition();

        Vector3 desiredPosition = CalculateDesiredPosition(currentPosition, targetPosition);

        Vector3 smoothedPosition = Vector3.SmoothDamp(
            currentPosition,
            desiredPosition,
            ref velocity,
            smoothTime,
            maxSpeed
        );

        smoothedPosition.z = currentPosition.z;

        if (pixelSnap)
        {
            smoothedPosition = SnapToPixelGrid(smoothedPosition);
        }

        transform.position = smoothedPosition;
    }

    private Vector3 GetTargetFollowPosition()
    {
        if (target == null)
        {
            return transform.position;
        }

        return target.position + (Vector3)targetOffset;
    }

    private Vector3 CalculateDesiredPosition(Vector3 cameraPosition, Vector3 targetPosition)
    {
        Vector3 desiredPosition = cameraPosition;

        float halfDeadZoneWidth = deadZoneSize.x * 0.5f;
        float halfDeadZoneHeight = deadZoneSize.y * 0.5f;

        float deltaX = targetPosition.x - cameraPosition.x;
        float deltaY = targetPosition.y - cameraPosition.y;

        if (Mathf.Abs(deltaX) > halfDeadZoneWidth)
        {
            desiredPosition.x = targetPosition.x - Mathf.Sign(deltaX) * halfDeadZoneWidth;
        }

        if (Mathf.Abs(deltaY) > halfDeadZoneHeight)
        {
            desiredPosition.y = targetPosition.y - Mathf.Sign(deltaY) * halfDeadZoneHeight;
        }

        return desiredPosition;
    }

    private Vector3 SnapToPixelGrid(Vector3 position)
    {
        float unitsPerPixel = 1f / pixelsPerUnit;

        position.x = Mathf.Round(position.x / unitsPerPixel) * unitsPerPixel;
        position.y = Mathf.Round(position.y / unitsPerPixel) * unitsPerPixel;

        return position;
    }

    private void OnDrawGizmosSelected()
    {
        if (target == null)
        {
            DrawCameraDeadZone(transform.position);
            return;
        }

        Vector3 targetPivotPosition = target.position;
        Vector3 targetFollowPosition = GetTargetFollowPosition();

        DrawCameraDeadZone(transform.position);
        DrawTargetFollowPreview(targetPivotPosition, targetFollowPosition);
    }

    private void DrawCameraDeadZone(Vector3 cameraPosition)
    {
        Gizmos.color = Color.yellow;

        Vector3 deadZoneCenter = cameraPosition;
        deadZoneCenter.z = 0f;

        Gizmos.DrawWireCube(deadZoneCenter, deadZoneSize);
    }

    private void DrawTargetFollowPreview(Vector3 pivotPosition, Vector3 followPosition)
    {
        pivotPosition.z = 0f;
        followPosition.z = 0f;

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(pivotPosition, 0.015f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(followPosition, 0.015f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(pivotPosition, followPosition);
    }
}
