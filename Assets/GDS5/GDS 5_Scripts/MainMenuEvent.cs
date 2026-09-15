using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class MainMenuEvent : MonoBehaviour
{
    private const string GameplaySceneName = "Mechanic test 1";
    private const string StartButtonName = "StartGameButton";
    private const string QuitButtonName = "QuitGameButton";

    private Button _startButton;
    private Button _quitButton;

    private void OnEnable()
    {
        RestoreMainMenuState();

        var document = GetComponent<UIDocument>();
        var root = document.rootVisualElement;

        _startButton = root.Q<Button>(StartButtonName);
        _quitButton = root.Q<Button>(QuitButtonName);

        if (_startButton != null)
        {
            _startButton.clicked += StartGame;
        }
        else
        {
            Debug.LogError($"Button '{StartButtonName}' was not found.", this);
        }

        if (_quitButton != null)
        {
            _quitButton.clicked += QuitGame;
        }
        else
        {
            Debug.LogError($"Button '{QuitButtonName}' was not found.", this);
        }
    }

    private void OnDisable()
    {
        if (_startButton != null)
        {
            _startButton.clicked -= StartGame;
        }

        if (_quitButton != null)
        {
            _quitButton.clicked -= QuitGame;
        }
    }

    private void StartGame()
    {
        Debug.Log("UI BUTTON CLICKED: MAIN MENU - START GAME", this);
        SceneManager.LoadScene(GameplaySceneName);
    }

    // Every main-menu entry clears global gameplay state left by a paused or completed challenge.
    private void RestoreMainMenuState()
    {
        Time.timeScale = 1f;
        UnityEngine.Cursor.visible = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        Debug.Log("Main menu input restored: cursor visible, cursor unlocked, time scale normal.", this);
    }

    private void QuitGame()
    {
        Debug.Log("UI BUTTON CLICKED: MAIN MENU - QUIT", this);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
// MainMenuEvent restores menu input on every scene entry and connects the start and quit buttons.
