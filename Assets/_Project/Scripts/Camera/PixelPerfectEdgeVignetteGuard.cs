using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class PixelPerfectEdgeVignetteGuard : MonoBehaviour
{
    private const int GradientResolution = 256;

    [Header("Reference Resolution")]
    [SerializeField, Min(1)] private int referenceWidth = 384;
    [SerializeField, Min(1)] private int referenceHeight = 216;

    [Header("Resolution Preview")]
    [SerializeField] private ResolutionMode resolutionMode = ResolutionMode.Automatic;

    [Header("Side Mask")]
    [FormerlySerializedAs("guardedAlpha")]
    [SerializeField, Range(0f, 1f)] private float darkness = 0.9f;
    [FormerlySerializedAs("extraCoverReferencePixels")]
    [SerializeField, Min(0f)] private float extraCover;
    [SerializeField, Min(0f)] private float coreShadowWidth = 10f;
    [FormerlySerializedAs("fadeWidthReferencePixels")]
    [SerializeField, Min(0f)] private float shadowWidth = 18f;
    [SerializeField] private Color sideColor = Color.black;

    [Header("Fade")]
    [SerializeField, Min(0f)] private float transitionSpeed = 10f;

    [Header("Canvas")]
    [SerializeField] private int overlaySortingOrder = -1000;

    [Header("Debug")]
    [SerializeField] private bool logResolutionChanges;

    private Canvas canvas;
    private Image leftImage;
    private Image rightImage;
    private float lastPlateauWidth = -1f;
    private float lastFadeWidth = -1f;
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private bool lastHorizontalMismatch;

    private void OnEnable()
    {
        BuildOverlay();
        ApplyTargetValues(immediate: true);
    }

    private void Awake()
    {
        if (canvas == null)
        {
            BuildOverlay();
        }
    }

    private void OnDisable()
    {
        DestroyOverlay();
    }

    private void OnDestroy()
    {
        DestroyOverlay();
    }

    private void Update()
    {
        if (canvas == null)
        {
            BuildOverlay();
        }

        ApplyTargetValues(immediate: !Application.isPlaying || transitionSpeed <= 0f);
    }

    private void BuildOverlay()
    {
        if (canvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("PixelPerfectEdgeVignetteCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = overlaySortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        leftImage = CreateSideImage(canvasObject.transform, "LeftSideMask");
        rightImage = CreateSideImage(canvasObject.transform, "RightSideMask");
    }

    private static Image CreateSideImage(Transform parent, string objectName)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.type = Image.Type.Simple;
        image.color = Color.clear;
        return image;
    }

    private void ApplyTargetValues(bool immediate)
    {
        bool horizontalMismatch = HasHorizontalMismatch();
        float plateauWidth = GetPlateauWidthScreenPixels(horizontalMismatch);
        float fadeWidth = GetShadowWidthScreenPixels();

        EnsureSprites(plateauWidth, fadeWidth);
        ConfigureLayout(plateauWidth + fadeWidth);

        if (logResolutionChanges && HasScreenSizeChanged(horizontalMismatch))
        {
            Debug.Log(
                $"PixelPerfectEdgeVignetteGuard: {Screen.width}x{Screen.height}, reference {referenceWidth}x{referenceHeight}, horizontal mismatch: {horizontalMismatch}.",
                this);
        }

        float targetAlpha = horizontalMismatch ? darkness : 0f;
        SetImageAlpha(leftImage, targetAlpha, immediate);
        SetImageAlpha(rightImage, targetAlpha, immediate);
    }

    private void EnsureSprites(float plateauWidth, float fadeWidth)
    {
        if (Mathf.Approximately(lastPlateauWidth, plateauWidth)
            && Mathf.Approximately(lastFadeWidth, fadeWidth)
            && leftImage != null
            && leftImage.sprite != null
            && rightImage != null
            && rightImage.sprite != null)
        {
            return;
        }

        lastPlateauWidth = plateauWidth;
        lastFadeWidth = fadeWidth;

        RefreshSideSprite(leftImage, CreateGradientSprite(isLeftSide: true, plateauWidth, fadeWidth));
        RefreshSideSprite(rightImage, CreateGradientSprite(isLeftSide: false, plateauWidth, fadeWidth));
    }

    private static void RefreshSideSprite(Image image, Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        DestroyGeneratedSprite(image);
        image.sprite = sprite;
    }

    private static Sprite CreateGradientSprite(bool isLeftSide, float plateauWidth, float fadeWidth)
    {
        float totalWidth = Mathf.Max(1f, plateauWidth + fadeWidth);
        float opaqueRatio = Mathf.Clamp01(plateauWidth / totalWidth);
        float fadeRatio = Mathf.Clamp01(fadeWidth / totalWidth);

        Texture2D texture = new Texture2D(GradientResolution, 1, TextureFormat.RGBA32, false)
        {
            name = isLeftSide ? "PixelPerfectLeftSideMask" : "PixelPerfectRightSideMask",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int x = 0; x < GradientResolution; x++)
        {
            float t = x / (float)(GradientResolution - 1);
            float alpha = GetGradientAlpha(isLeftSide, t, opaqueRatio, fadeRatio);
            texture.SetPixel(x, 0, new Color(1f, 1f, 1f, alpha));
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, GradientResolution, 1f), new Vector2(0.5f, 0.5f), 100f);
    }

    private static float GetGradientAlpha(bool isLeftSide, float t, float opaqueRatio, float fadeRatio)
    {
        if (isLeftSide)
        {
            if (t <= opaqueRatio)
            {
                return 1f;
            }

            float fadeStart = opaqueRatio;
            float fadeLength = Mathf.Max(0.0001f, fadeRatio);
            float fadeT = Mathf.Clamp01((t - fadeStart) / fadeLength);
            return 1f - EvaluateShadowFade(fadeT);
        }

        if (t >= 1f - opaqueRatio)
        {
            return 1f;
        }

        float fadeLengthRight = Mathf.Max(0.0001f, fadeRatio);
        float fadeTRight = Mathf.Clamp01(t / fadeLengthRight);
        return 1f - EvaluateShadowFade(1f - fadeTRight);
    }

    private static float EvaluateShadowFade(float t)
    {
        float shaped = Mathf.Pow(Mathf.Clamp01(t), 1.55f);
        return shaped * shaped * (3f - 2f * shaped);
    }

    private void ConfigureLayout(float totalWidth)
    {
        ConfigureSideRect(leftImage, anchorX: 0f, pivotX: 0f, totalWidth: totalWidth);
        ConfigureSideRect(rightImage, anchorX: 1f, pivotX: 1f, totalWidth: totalWidth);
    }

    private static void ConfigureSideRect(Image image, float anchorX, float pivotX, float totalWidth)
    {
        if (image == null)
        {
            return;
        }

        RectTransform rectTransform = image.rectTransform;
        rectTransform.anchorMin = new Vector2(anchorX, 0f);
        rectTransform.anchorMax = new Vector2(anchorX, 1f);
        rectTransform.pivot = new Vector2(pivotX, 0.5f);
        rectTransform.sizeDelta = new Vector2(totalWidth, 0f);
        rectTransform.anchoredPosition = Vector2.zero;
    }

    private void SetImageAlpha(Image image, float targetAlpha, bool immediate)
    {
        if (image == null)
        {
            return;
        }

        Color color = image.color;
        color.r = sideColor.r;
        color.g = sideColor.g;
        color.b = sideColor.b;
        color.a = immediate
            ? Mathf.Clamp01(targetAlpha)
            : Mathf.MoveTowards(color.a, Mathf.Clamp01(targetAlpha), transitionSpeed * Time.unscaledDeltaTime);
        image.color = color;
    }

    private bool HasHorizontalMismatch()
    {
        switch (resolutionMode)
        {
            case ResolutionMode.ForceMatching:
                return false;
            case ResolutionMode.ForceGuarded:
                return true;
            default:
                return referenceWidth > 0
                    && Screen.width > 0
                    && Screen.width % referenceWidth != 0;
        }
    }

    private float GetPlateauWidthScreenPixels(bool horizontalMismatch)
    {
        float mismatchWidth = horizontalMismatch && referenceWidth > 0 && Screen.width > 0
            ? (Screen.width % referenceWidth) * 0.5f
            : 0f;

        float scale = GetReferenceScale();
        float fixedDarkWidth = horizontalMismatch ? coreShadowWidth * scale : 0f;
        return mismatchWidth + extraCover * scale + fixedDarkWidth;
    }

    private float GetShadowWidthScreenPixels()
    {
        return shadowWidth * GetReferenceScale();
    }

    private float GetReferenceScale()
    {
        if (referenceWidth <= 0 || referenceHeight <= 0 || Screen.width <= 0 || Screen.height <= 0)
        {
            return 1f;
        }

        return Mathf.Max(1f, Mathf.Min(
            Screen.width / (float)referenceWidth,
            Screen.height / (float)referenceHeight));
    }

    private bool HasScreenSizeChanged(bool horizontalMismatch)
    {
        if (lastScreenWidth == Screen.width
            && lastScreenHeight == Screen.height
            && lastHorizontalMismatch == horizontalMismatch)
        {
            return false;
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastHorizontalMismatch = horizontalMismatch;
        return true;
    }

    private static void DestroyGeneratedSprite(Image image)
    {
        if (image == null || image.sprite == null)
        {
            return;
        }

        Texture texture = image.sprite.texture;
        DestroyUnityObject(image.sprite);
        DestroyUnityObject(texture);
    }

    private void DestroyOverlay()
    {
        DestroyGeneratedSprite(leftImage);
        DestroyGeneratedSprite(rightImage);

        leftImage = null;
        rightImage = null;
        lastPlateauWidth = -1f;
        lastFadeWidth = -1f;

        if (canvas != null)
        {
            DestroyUnityObject(canvas.gameObject);
            canvas = null;
        }
    }

    private static void DestroyUnityObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private void OnValidate()
    {
        referenceWidth = Mathf.Max(1, referenceWidth);
        referenceHeight = Mathf.Max(1, referenceHeight);
        extraCover = Mathf.Max(0f, extraCover);
        coreShadowWidth = Mathf.Max(0f, coreShadowWidth);
        shadowWidth = Mathf.Max(0f, shadowWidth);
        transitionSpeed = Mathf.Max(0f, transitionSpeed);

        if (canvas != null)
        {
            canvas.sortingOrder = overlaySortingOrder;
        }

        lastPlateauWidth = -1f;
        lastFadeWidth = -1f;
        ApplyTargetValues(immediate: true);
    }

    private enum ResolutionMode
    {
        Automatic,
        ForceMatching,
        ForceGuarded
    }
}
