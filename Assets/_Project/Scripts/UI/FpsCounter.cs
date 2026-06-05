using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class FpsCounter : MonoBehaviour
{
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;
    [SerializeField] private string prefix = "FPS ";

    private TMP_Text fpsText;
    private float elapsedTime;
    private int frameCount;

    private void Awake()
    {
        fpsText = GetComponent<TMP_Text>();
        UpdateText(0);
    }

    private void Update()
    {
        elapsedTime += Time.unscaledDeltaTime;
        frameCount++;

        if (elapsedTime < refreshInterval)
        {
            return;
        }

        int fps = Mathf.RoundToInt(frameCount / elapsedTime);
        UpdateText(fps);

        elapsedTime = 0f;
        frameCount = 0;
    }

    private void UpdateText(int fps)
    {
        if (fpsText != null)
        {
            fpsText.text = $"{prefix}{fps}";
        }
    }

    private void OnValidate()
    {
        refreshInterval = Mathf.Max(0.05f, refreshInterval);
    }
}
