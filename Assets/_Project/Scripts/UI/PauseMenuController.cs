using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button cameraTransitionButton;
    [SerializeField] private Image cameraTransitionStateImage;
    [SerializeField] private Sprite cameraTransitionEnabledSprite;
    [SerializeField] private Sprite cameraTransitionDisabledSprite;
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
    [SerializeField, Min(0f)] private float resumeInputLockDuration = 0.12f;

    private bool isPaused;
    private bool cachedCameraTransitionsEnabled = true;

    private void Awake()
    {
        ResolveReferences();
        SetPaused(false, force: true);
    }

    private void OnEnable()
    {
        ResolveReferences();
        AddButtonListeners();
        RefreshCameraTransitionButton();
    }

    private void OnDisable()
    {
        RemoveButtonListeners();
        ClearPauseState();
    }

    private void OnDestroy()
    {
        ClearPauseState();
    }

    private void Update()
    {
        if (SceneTransition.IsTransitioning)
        {
            return;
        }

        if (Input.GetKeyDown(pauseKey))
        {
            SetPaused(!isPaused);
        }
    }

    public void Resume()
    {
        SetPaused(false);
    }

    public void ReturnToMainMenu()
    {
        PrepareForSceneLoad();
        SceneTransition.LoadScene(mainMenuSceneName);
    }

    public void ToggleCameraTransition()
    {
        bool enabled = !GetCameraTransitionsEnabled();
        cachedCameraTransitionsEnabled = enabled;

        if (RoomFlowController.Instance != null)
        {
            RoomFlowController.Instance.SetCameraTransitionsEnabled(enabled);
        }

        RefreshCameraTransitionButton();
        ClearSelectedUiObject();
    }

    private void SetPaused(bool paused, bool force = false)
    {
        if (!force && isPaused == paused)
        {
            return;
        }

        isPaused = paused;
        GameplayInputGate.SetPauseBlocked(paused);

        if (pauseMenu != null)
        {
            pauseMenu.SetActive(paused);
        }

        if (paused)
        {
            RefreshCameraTransitionButton();
        }

        Time.timeScale = paused ? 0f : 1f;

        if (!paused && !force)
        {
            GameplayInputGate.BlockFor(resumeInputLockDuration, waitUntilPrimaryPointerReleased: true);
            ClearSelectedUiObject();
        }
    }

    private void AddButtonListeners()
    {
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(Resume);
            resumeButton.onClick.AddListener(Resume);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        }

        if (cameraTransitionButton != null)
        {
            cameraTransitionButton.onClick.RemoveListener(ToggleCameraTransition);
            cameraTransitionButton.onClick.AddListener(ToggleCameraTransition);
        }
    }

    private void RemoveButtonListeners()
    {
        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(Resume);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
        }

        if (cameraTransitionButton != null)
        {
            cameraTransitionButton.onClick.RemoveListener(ToggleCameraTransition);
        }
    }

    private void ResolveReferences()
    {
        if (pauseMenu == null)
        {
            Transform pauseMenuTransform = transform.Find("PauseMenu");
            pauseMenu = pauseMenuTransform != null ? pauseMenuTransform.gameObject : null;
        }

        if (pauseMenu == null)
        {
            return;
        }

        resumeButton ??= FindButton("ButtonResume");
        mainMenuButton ??= FindButton("ButtonQuit");
        cameraTransitionButton ??= FindButton("ButtonCameraTransition");

        if (cameraTransitionStateImage == null && cameraTransitionButton != null)
        {
            cameraTransitionStateImage = FindButtonStateImage(cameraTransitionButton);
        }
    }

    private Button FindButton(string buttonName)
    {
        foreach (Button button in pauseMenu.GetComponentsInChildren<Button>(true))
        {
            if (button.name == buttonName)
            {
                return button;
            }
        }

        return null;
    }

    private static Image FindButtonStateImage(Button button)
    {
        if (button == null)
        {
            return null;
        }

        foreach (Image image in button.GetComponentsInChildren<Image>(true))
        {
            if (image.name == "Icon")
            {
                return image;
            }
        }

        if (button.targetGraphic is Image targetImage)
        {
            return targetImage;
        }

        return button.GetComponent<Image>();
    }

    private bool GetCameraTransitionsEnabled()
    {
        if (RoomFlowController.Instance != null)
        {
            cachedCameraTransitionsEnabled = RoomFlowController.Instance.CameraTransitionsEnabled;
        }

        return cachedCameraTransitionsEnabled;
    }

    private void RefreshCameraTransitionButton()
    {
        bool enabled = GetCameraTransitionsEnabled();

        if (cameraTransitionStateImage == null)
        {
            return;
        }

        Sprite stateSprite = enabled
            ? cameraTransitionEnabledSprite
            : cameraTransitionDisabledSprite;

        if (stateSprite != null)
        {
            cameraTransitionStateImage.sprite = stateSprite;
        }
    }

    private void ClearPauseState()
    {
        isPaused = false;
        GameplayInputGate.ClearPauseAndPointerBlocks();

        if (pauseMenu != null)
        {
            pauseMenu.SetActive(false);
        }

        Time.timeScale = 1f;
        ClearSelectedUiObject();
    }

    private void PrepareForSceneLoad()
    {
        isPaused = false;
        GameplayInputGate.ClearPauseAndPointerBlocks();
        Time.timeScale = 1f;
        ClearSelectedUiObject();
    }

    private static void ClearSelectedUiObject()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void OnValidate()
    {
        resumeInputLockDuration = Mathf.Max(0f, resumeInputLockDuration);
    }
}
