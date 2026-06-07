using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(GraphicRaycaster))]
public sealed class SceneTransition : MonoBehaviour
{
    private const float FadeOutDuration = 0.52f;
    private const float FadeInDuration = 0.78f;
    private const float SceneReadyHoldDuration = 0.10f;
    private const int OverlaySortingOrder = short.MaxValue;

    private static SceneTransition instance;

    private CanvasGroup canvasGroup;
    private Image fadeImage;
    private Coroutine activeRoutine;
    private bool isTransitioning;
    private bool isLoadingScene;

    public static bool IsTransitioning => instance != null && instance.isTransitioning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SceneTransition transition = GetOrCreate();
        transition.ShowBlackImmediately();
        transition.StartInitialFadeIn();
    }

    public static void LoadScene(int buildIndex)
    {
        if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogWarning($"SceneTransition could not load scene build index {buildIndex}.");
            return;
        }

        GetOrCreate().StartSceneTransition(sceneName: null, buildIndex, useBuildIndex: true, FadeOutDuration);
    }

    public static void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("SceneTransition could not load an empty scene name.");
            return;
        }

        GetOrCreate().StartSceneTransition(sceneName, buildIndex: -1, useBuildIndex: false, FadeOutDuration);
    }

    public static void LoadScene(string sceneName, float fadeOutDuration)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("SceneTransition could not load an empty scene name.");
            return;
        }

        GetOrCreate().StartSceneTransition(sceneName, buildIndex: -1, useBuildIndex: false, fadeOutDuration);
    }

    private static SceneTransition GetOrCreate()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindAnyObjectByType<SceneTransition>();

        if (instance != null)
        {
            return instance;
        }

        GameObject transitionObject = new("SceneTransition");
        instance = transitionObject.AddComponent<SceneTransition>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            GameplayInputGate.SetSceneTransitionBlocked(false);
            isLoadingScene = false;
            instance = null;
        }
    }

    private void BuildOverlay()
    {
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OverlaySortingOrder;

        CanvasScaler canvasScaler = GetComponent<CanvasScaler>();
        if (canvasScaler == null)
        {
            canvasScaler = gameObject.AddComponent<CanvasScaler>();
        }

        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0f;

        fadeImage = GetComponentInChildren<Image>(true);
        if (fadeImage == null)
        {
            GameObject fadeObject = new("BlackFade", typeof(RectTransform), typeof(Image));
            fadeObject.transform.SetParent(transform, worldPositionStays: false);

            RectTransform rectTransform = fadeObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            fadeImage = fadeObject.GetComponent<Image>();
        }

        fadeImage.color = Color.black;
        fadeImage.raycastTarget = true;
    }

    private void StartInitialFadeIn()
    {
        StopActiveRoutine();
        activeRoutine = StartCoroutine(InitialFadeInRoutine());
    }

    private IEnumerator InitialFadeInRoutine()
    {
        isTransitioning = true;
        GameplayInputGate.SetSceneTransitionBlocked(true);
        SetOverlayBlocking(true);

        SetAlpha(1f);
        yield return null;
        yield return FadeTo(0f, FadeInDuration);

        FinishTransition();
    }

    private void StartSceneTransition(string sceneName, int buildIndex, bool useBuildIndex, float fadeOutDuration)
    {
        if (isLoadingScene)
        {
            return;
        }

        if (isTransitioning && activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }

        activeRoutine = StartCoroutine(SceneTransitionRoutine(sceneName, buildIndex, useBuildIndex, fadeOutDuration));
    }

    private IEnumerator SceneTransitionRoutine(string sceneName, int buildIndex, bool useBuildIndex, float fadeOutDuration)
    {
        isTransitioning = true;
        isLoadingScene = true;
        GameplayInputGate.SetSceneTransitionBlocked(true);
        SetOverlayBlocking(true);

        yield return FadeTo(1f, Mathf.Max(0f, fadeOutDuration));

        AsyncOperation loadOperation = useBuildIndex
            ? SceneManager.LoadSceneAsync(buildIndex)
            : SceneManager.LoadSceneAsync(sceneName);

        if (loadOperation == null)
        {
            Debug.LogWarning("SceneTransition could not start scene loading.");
            yield return FadeTo(0f, FadeInDuration);
            FinishTransition();
            yield break;
        }

        loadOperation.allowSceneActivation = false;

        while (loadOperation.progress < 0.9f)
        {
            yield return null;
        }

        loadOperation.allowSceneActivation = true;

        while (!loadOperation.isDone)
        {
            yield return null;
        }

        SetAlpha(1f);
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return new WaitForSecondsRealtime(SceneReadyHoldDuration);
        yield return FadeTo(0f, FadeInDuration);

        FinishTransition();
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float startAlpha = canvasGroup.alpha;

        if (duration <= 0f)
        {
            SetAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = EaseInOut(progress);
            SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, easedProgress));
            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    private void ShowBlackImmediately()
    {
        StopActiveRoutine();
        SetOverlayBlocking(true);
        SetAlpha(1f);
    }

    private void FinishTransition()
    {
        activeRoutine = null;
        isTransitioning = false;
        isLoadingScene = false;
        GameplayInputGate.SetSceneTransitionBlocked(false);
        SetOverlayBlocking(false);
        SetAlpha(0f);
    }

    private void StopActiveRoutine()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }
    }

    private void SetOverlayBlocking(bool blocksInput)
    {
        canvasGroup.blocksRaycasts = blocksInput;
    }

    private void SetAlpha(float alpha)
    {
        canvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    private static float EaseInOut(float progress)
    {
        return progress * progress * progress * (progress * (6f * progress - 15f) + 10f);
    }
}
