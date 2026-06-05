using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class UIButtonPressOffset : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private RectTransform target;
    [SerializeField] private Vector2 pressedOffset = new(0f, -1f);
    [SerializeField] private bool resetOnPointerExit = true;

    private Vector2 restingPosition;
    private bool hasRestingPosition;

    private void Awake()
    {
        if (target == null)
        {
            target = GetComponentInChildren<UnityEngine.UI.Text>(true)?.rectTransform;
        }

        CaptureRestingPosition();
    }

    private void OnEnable()
    {
        CaptureRestingPosition();
        ResetOffset();
    }

    private void OnDisable()
    {
        ResetOffset();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (target == null)
        {
            return;
        }

        CaptureRestingPosition();
        target.anchoredPosition = restingPosition + pressedOffset;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ResetOffset();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (resetOnPointerExit)
        {
            ResetOffset();
        }
    }

    private void CaptureRestingPosition()
    {
        if (target == null || hasRestingPosition)
        {
            return;
        }

        restingPosition = target.anchoredPosition;
        hasRestingPosition = true;
    }

    private void ResetOffset()
    {
        if (target == null || !hasRestingPosition)
        {
            return;
        }

        target.anchoredPosition = restingPosition;
    }
}
