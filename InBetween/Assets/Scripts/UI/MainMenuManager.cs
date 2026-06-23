using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuManager : MonoBehaviour
{

    [Header("Scene Index")]
    [SerializeField] private int Sceneindex = 0;

    [Header("Buttons")]
    [SerializeField] private Button PlayButton;
    [SerializeField] private Button QuitButton;
    [SerializeField] private Button SettingsButton;


    void Awake()
    {
        // Hook up button click listeners
        if (PlayButton != null)
            PlayButton.onClick.AddListener(LoadScene);

        if (QuitButton != null)
            QuitButton.onClick.AddListener(Quit);

        if (SettingsButton != null)
            SettingsButton.onClick.AddListener(Settings);
       
    }

   public void LoadScene()
    {
        SceneManager.LoadSceneAsync(Sceneindex);
    }

    void Settings() 
    {
        Debug.Log("opening Settings");
    }

     public  void Quit()
    {
        Application.Quit();

#if UNITY_EDITOR
        // Exit play mode in editor
        EditorApplication.isPlaying = false;
#endif
    }

    void OnDisable()
    {
        // Clean up listeners to avoid duplicate registrations
        if (PlayButton != null)
            PlayButton.onClick.RemoveListener(LoadScene);

        if (QuitButton != null)
            QuitButton.onClick.RemoveListener(Quit);

        if (SettingsButton != null)
            SettingsButton.onClick.RemoveListener(Settings);
    }
}
