using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
    [SerializeField, Min(0f)] private float resumeInputLockDuration = 0.12f;

    private bool isPaused;

    private void Awake()
    {
        ResolveReferences();
        SetPaused(false, force: true);
    }

    private void OnEnable()
    {
        ResolveReferences();
        AddButtonListeners();
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
