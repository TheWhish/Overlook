using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class RoomCounterView : MonoBehaviour
{
    [SerializeField] private string prefix = "ROOMS ";

    private TMP_Text counterText;

    private void Awake()
    {
        counterText = GetComponent<TMP_Text>();
        Refresh();
    }

    private void OnEnable()
    {
        RoomFlowController.CompletedRoomCountChanged += UpdateText;
        Refresh();
    }

    private void OnDisable()
    {
        RoomFlowController.CompletedRoomCountChanged -= UpdateText;
    }

    private void Refresh()
    {
        int completedRoomCount = RoomFlowController.Instance != null
            ? RoomFlowController.Instance.CompletedRoomCount
            : 0;

        UpdateText(completedRoomCount);
    }

    private void UpdateText(int completedRoomCount)
    {
        if (counterText != null)
        {
            counterText.text = $"{prefix}{completedRoomCount}";
        }
    }
}
