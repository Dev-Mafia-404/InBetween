using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private KeyCode Pausekey = KeyCode.Escape;
    [SerializeField] private GameObject PauseMenuCanvas;
    [SerializeField] private int Sceneindex = 0;

    [Header("Buttons")]
    [SerializeField] private Button ResumeButton;
    [SerializeField] private Button MenuButton;
    [SerializeField] private Button SettingsButton;

    private bool isopen = false;

    void Awake()
    {
        // Hook up button click listeners
        if (ResumeButton != null)
            ResumeButton.onClick.AddListener(Resume);

        if (MenuButton != null)
            MenuButton.onClick.AddListener(LoadScene);

        if (SettingsButton != null)
            SettingsButton.onClick.AddListener(Settings);

        // Deactivate pause menu on start
        if (PauseMenuCanvas != null)
            PauseMenuCanvas.SetActive(false);
    }

    void Update()
    {
        if (PauseMenuCanvas != null && Input.GetKeyDown(Pausekey))
        {
            if (isopen)
                Resume();
            else
                Pause();
        }
    }

    public void Resume()
    {
        PauseMenuCanvas.SetActive(false);
        Time.timeScale = 1;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        isopen = false;
    }

    public void Pause()
    {
        PauseMenuCanvas.SetActive(true);
        Time.timeScale = 0;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        isopen = true;
    }

    public void LoadScene()
    {
        Time.timeScale = 1; // Resume time before loading scene
        SceneManager.LoadSceneAsync(Sceneindex);
    }

    public void Settings()
    {
        Debug.Log("Opening Settings");
    }

    void OnDisable()
    {
        // Clean up listeners to avoid duplicate registrations
        if (ResumeButton != null)
            ResumeButton.onClick.RemoveListener(Resume);

        if (MenuButton != null)
            MenuButton.onClick.RemoveListener(LoadScene);

        if (SettingsButton != null)
            SettingsButton.onClick.RemoveListener(Settings);
    }
}

