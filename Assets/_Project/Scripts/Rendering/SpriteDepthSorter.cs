using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public class SpriteDepthSorter : MonoBehaviour
{
    private enum DepthSortMode
    {
        SortingOrderFromY = 0,
        UnityTransparencySort = 1
    }

    [SerializeField] private DepthSortMode depthSortMode = DepthSortMode.SortingOrderFromY;
    [SerializeField] private int baseSortingOrder = 10000;
    [SerializeField, Min(1)] private int unitsToOrder = 100;
    [SerializeField] private int sortingOrderOffset;
    [SerializeField] private Transform sortPoint;
    [SerializeField] private int unitySortingOrder;

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

        if (depthSortMode == DepthSortMode.UnityTransparencySort)
        {
            ApplySortingOrder(unitySortingOrder);
            return;
        }

        Vector3 pointPosition = sortPoint != null
            ? sortPoint.position
            : transform.position;

        int sortingOrder = baseSortingOrder
            - Mathf.RoundToInt(pointPosition.y * unitsToOrder)
            + sortingOrderOffset;

        ApplySortingOrder(sortingOrder);
    }

    private void OnValidate()
    {
        unitsToOrder = Mathf.Max(1, unitsToOrder);
    }

    private void ApplySortingOrder(int sortingOrder)
    {
        if (sortingOrder == lastSortingOrder)
        {
            return;
        }

        spriteRenderer.sortingOrder = sortingOrder;
        lastSortingOrder = sortingOrder;
    }
}
