using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;
using System.Collections;
using UnityEngine.UI;

[RequireComponent(typeof(VideoPlayer))]
public class VideoPlayerController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool playOnStart = true;

    [Header("Events")]
    public UnityEvent onVideoFinished;
    public UnityEvent onVideoStarted;
    public GameObject RawImage;

    private VideoPlayer videoPlayer;
    private bool finished;

    // Tracks whether the intro has already been played this session.
    private static bool hasPlayedIntro = false;

    private void Awake()
    {
        RawImage.SetActive(true);
        onVideoStarted?.Invoke();
        videoPlayer = GetComponent<VideoPlayer>();

        videoPlayer.playOnAwake = false;

        videoPlayer.prepareCompleted += OnVideoPrepared;
        videoPlayer.loopPointReached += OnVideoFinished;
    }

    private void Start()
    {
        // Skip the intro if it has already been played this session.
        if (hasPlayedIntro)
        {
            onVideoFinished?.Invoke();
            return;
        }

        if (playOnStart)
            StartCoroutine(PrepareAndPlay());
    }

    public void PlayVideo()
    {
        finished = false;
        StartCoroutine(PrepareAndPlay());
    }

    private IEnumerator PrepareAndPlay()
    {
        videoPlayer.Stop();

        videoPlayer.Prepare();

        while (!videoPlayer.isPrepared)
            yield return null;

        hasPlayedIntro = true;
        videoPlayer.Play();
    }

    private void OnVideoPrepared(VideoPlayer source)
    {
    }

    private void OnVideoFinished(VideoPlayer source)
    {
        if (finished)
            return;

        finished = true;
        onVideoFinished?.Invoke();
    }

    private void OnDisable()
    {
        videoPlayer.prepareCompleted -= OnVideoPrepared;
        videoPlayer.loopPointReached -= OnVideoFinished;
    }
}