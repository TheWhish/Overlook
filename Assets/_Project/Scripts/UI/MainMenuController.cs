using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private string fallbackSceneName = "Game";

    private void Awake()
    {
        ResolveButtons();
    }

    private void OnEnable()
    {
        ResolveButtons();
        AddButtonListeners();
    }

    private void OnDisable()
    {
        RemoveButtonListeners();
    }

    public void Play()
    {
        GameplayInputGate.Clear();

        int currentBuildIndex = SceneManager.GetActiveScene().buildIndex;
        int nextBuildIndex = currentBuildIndex + 1;

        if (currentBuildIndex >= 0 && nextBuildIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneTransition.LoadScene(nextBuildIndex);
            return;
        }

        if (!string.IsNullOrEmpty(fallbackSceneName))
        {
            SceneTransition.LoadScene(fallbackSceneName);
            return;
        }

        Debug.LogWarning("MainMenuController could not find a scene to load.");
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void AddButtonListeners()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveListener(Play);
            playButton.onClick.AddListener(Play);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(Quit);
            quitButton.onClick.AddListener(Quit);
        }
    }

    private void RemoveButtonListeners()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveListener(Play);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(Quit);
        }
    }

    private void ResolveButtons()
    {
        playButton ??= FindButton("ButtonPlay");
        quitButton ??= FindButton("ButtonQuit");
    }

    private Button FindButton(string buttonName)
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button.name == buttonName)
            {
                return button;
            }
        }

        return null;
    }
}
