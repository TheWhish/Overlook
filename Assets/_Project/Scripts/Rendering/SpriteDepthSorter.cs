using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public class SpriteDepthSorter : MonoBehaviour
{
    [SerializeField] private int baseSortingOrder = 10000;
    [SerializeField, Min(1)] private int unitsToOrder = 100;
    [SerializeField] private int sortingOrderOffset;
    [SerializeField] private Transform sortPoint;

    private SpriteRenderer spriteRenderer;
    private int lastSortingOrder = int.MinValue;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Vector3 pointPosition = sortPoint != null
            ? sortPoint.position
            : transform.position;

        int sortingOrder = baseSortingOrder
            - Mathf.RoundToInt(pointPosition.y * unitsToOrder)
            + sortingOrderOffset;

        if (sortingOrder == lastSortingOrder)
        {
            return;
        }

        spriteRenderer.sortingOrder = sortingOrder;
        lastSortingOrder = sortingOrder;
    }

    private void OnValidate()
    {
        unitsToOrder = Mathf.Max(1, unitsToOrder);
    }
}
