using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public class MainMenuManager : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Scene")]
    [SerializeField] private int gameplaySceneIndex = 1;

    [Header("Loading Video")]
    [SerializeField] private RawImage loadingScreen;
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("Fade (Optional)")]
    [SerializeField] private CanvasGroup fadeCanvas;
    [SerializeField] private float fadeDuration = 0.35f;

    private bool videoFinished;
    private AsyncOperation loadingOperation;

    private void Awake()
    {
        if (playButton != null)
            playButton.onClick.AddListener(PlayGame);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(Settings);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        if (loadingScreen != null)
            loadingScreen.gameObject.SetActive(false);

        if (fadeCanvas != null)
            fadeCanvas.alpha = 0f;
    }

    private void PlayGame()
    {
        playButton.interactable = false;
        if (settingsButton != null) settingsButton.interactable = false;
        if (quitButton != null) quitButton.interactable = false;

        StartCoroutine(PlayIntroAndLoad());
    }

    private IEnumerator PlayIntroAndLoad()
    {
        videoFinished = false;

        loadingScreen.gameObject.SetActive(true);

        videoPlayer.Stop();
        videoPlayer.frame = 0;
        videoPlayer.loopPointReached += OnVideoFinished;
        videoPlayer.Play();

        loadingOperation = SceneManager.LoadSceneAsync(gameplaySceneIndex);
        loadingOperation.allowSceneActivation = false;

        while (!videoFinished || loadingOperation.progress < 0.9f)
            yield return null;

        if (fadeCanvas != null)
        {
            float t = 0;

            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                fadeCanvas.alpha = Mathf.Lerp(0, 1, t / fadeDuration);
                yield return null;
            }

            fadeCanvas.alpha = 1;
        }

        loadingOperation.allowSceneActivation = true;
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        videoFinished = true;
        videoPlayer.loopPointReached -= OnVideoFinished;
    }

    private void Settings()
    {
        Debug.Log("Settings");
    }

    private void QuitGame()
    {
        Application.Quit();
    }

    private void OnDisable()
    {
        if (playButton != null)
            playButton.onClick.RemoveListener(PlayGame);

        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(Settings);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(QuitGame);

        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }
}