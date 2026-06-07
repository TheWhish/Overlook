using UnityEngine;
using UnityEngine.EventSystems;

public static class GameplayInputGate
{
    private static bool pauseBlocksGameplayInput;
    private static bool sceneTransitionBlocksGameplayInput;
    private static bool playerDeathBlocksGameplayInput;
    private static bool waitForPrimaryPointerRelease;
    private static int blockedUntilFrame;
    private static float blockedUntilRealtime;

    public static bool IsPauseBlockingGameplayInput => pauseBlocksGameplayInput;

    public static bool IsGameplayInputBlocked
    {
        get
        {
            if (pauseBlocksGameplayInput || sceneTransitionBlocksGameplayInput || playerDeathBlocksGameplayInput || Time.timeScale <= 0f)
            {
                return true;
            }

            if (Time.frameCount <= blockedUntilFrame || Time.unscaledTime < blockedUntilRealtime)
            {
                return true;
            }

            if (waitForPrimaryPointerRelease)
            {
                if (IsPrimaryPointerPressed())
                {
                    return true;
                }

                waitForPrimaryPointerRelease = false;
            }

            return false;
        }
    }

    public static void SetPauseBlocked(bool blocked)
    {
        pauseBlocksGameplayInput = blocked;

        if (blocked)
        {
            BlockFor(0f, waitUntilPrimaryPointerReleased: true);
        }
    }

    public static void SetSceneTransitionBlocked(bool blocked)
    {
        sceneTransitionBlocksGameplayInput = blocked;
    }

    public static void SetPlayerDeathBlocked(bool blocked)
    {
        playerDeathBlocksGameplayInput = blocked;
    }

    public static void BlockFor(float seconds, bool waitUntilPrimaryPointerReleased)
    {
        blockedUntilFrame = Mathf.Max(blockedUntilFrame, Time.frameCount + 1);
        blockedUntilRealtime = Mathf.Max(blockedUntilRealtime, Time.unscaledTime + Mathf.Max(0f, seconds));
        waitForPrimaryPointerRelease |= waitUntilPrimaryPointerReleased;
    }

    public static void Clear()
    {
        pauseBlocksGameplayInput = false;
        sceneTransitionBlocksGameplayInput = false;
        playerDeathBlocksGameplayInput = false;
        waitForPrimaryPointerRelease = false;
        blockedUntilFrame = 0;
        blockedUntilRealtime = 0f;
    }

    public static void ClearPauseAndPointerBlocks()
    {
        pauseBlocksGameplayInput = false;
        waitForPrimaryPointerRelease = false;
        blockedUntilFrame = 0;
        blockedUntilRealtime = 0f;
    }

    public static bool IsPointerOverUi()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        if (EventSystem.current.IsPointerOverGameObject())
        {
            return true;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            if (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(i).fingerId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPrimaryPointerPressed()
    {
        if (Input.GetMouseButton(0))
        {
            return true;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            TouchPhase phase = Input.GetTouch(i).phase;
            if (phase == TouchPhase.Began || phase == TouchPhase.Moved || phase == TouchPhase.Stationary)
            {
                return true;
            }
        }

        return false;
    }
}
