using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public class SpriteDepthSorter : MonoBehaviour
{
    [SerializeField] private int baseSortingOrder = 10000;
    [SerializeField, Min(1)] private int unitsToOrder = 100;
    [SerializeField] private int sortingOrderOffset;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        spriteRenderer.sortingOrder = baseSortingOrder
            - Mathf.RoundToInt(transform.position.y * unitsToOrder)
            + sortingOrderOffset;
    }
}
