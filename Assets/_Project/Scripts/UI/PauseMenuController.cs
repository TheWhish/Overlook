using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

    private bool isPaused;

    private void Awake()
    {
        ResolveReferences();
        SetPaused(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        AddButtonListeners();
    }

    private void OnDisable()
    {
        RemoveButtonListeners();
        Time.timeScale = 1f;
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    private void Update()
    {
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
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void SetPaused(bool paused)
    {
        isPaused = paused;

        if (pauseMenu != null)
        {
            pauseMenu.SetActive(paused);
        }

        Time.timeScale = paused ? 0f : 1f;
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
}
