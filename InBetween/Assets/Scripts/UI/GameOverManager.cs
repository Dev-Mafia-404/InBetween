using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject gameOverCanvas;

    [Header("Scene Settings")]
    [SerializeField] private int retrySceneIndex;
    [SerializeField] private int mainMenuSceneIndex;

    [Header("Buttons")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private Button settingsButton;

    private void Awake()
    {
        if (retryButton != null)
            retryButton.onClick.AddListener(Retry);

        if (menuButton != null)
            menuButton.onClick.AddListener(LoadMainMenu);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(Settings);

        if (gameOverCanvas != null)
            gameOverCanvas.SetActive(false);
    }

    /// <summary>
    /// Call this from a UnityEvent when the player dies.
    /// </summary>
    public void ShowGameOver()
    {
        if (gameOverCanvas != null)
            gameOverCanvas.SetActive(true);

        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Retry()
    {
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync(retrySceneIndex);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync(mainMenuSceneIndex);
    }

    public void Settings()
    {
        Debug.Log("Opening Settings");
    }

    private void OnDisable()
    {
        if (retryButton != null)
            retryButton.onClick.RemoveListener(Retry);

        if (menuButton != null)
            menuButton.onClick.RemoveListener(LoadMainMenu);

        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(Settings);
    }
}