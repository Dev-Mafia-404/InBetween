using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// VideoPlayerManager: Plays videos on camera near plane through Unity Events.
/// Supports immediate playback, delayed playback, and player movement control.
/// Videos can be wired through the Inspector and called via Unity Events.
/// </summary>
public class VideoPlayerManager : MonoBehaviour
{
    [System.Serializable]
    public class VideoPlaybackEvent : UnityEvent<VideoClip> { }

    [Header("Video Setup")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private Canvas videoCanvas;
    [SerializeField] private RawImage videoDisplay;
    [Tooltip("Drag your video clips here in the Inspector")]
    [SerializeField] private VideoClip[] availableVideos;

    [Header("Player Reference")]
    [SerializeField] private GameObject playerObject;
    [SerializeField] private MonoBehaviour playerMovementScript;
    [SerializeField] private string disableMethodName = "DisableMovement";
    [SerializeField] private string enableMethodName = "EnableMovement";

    [Header("Events")]
    [SerializeField] private UnityEvent onVideoStarted;
    [SerializeField] private UnityEvent onVideoEnded;

    private Coroutine currentPlaybackCoroutine;
    private RenderTexture renderTexture;
    private bool isVideoPlaying;

    private void Awake()
    {
        InitializeVideoPlayer();
    }

    private void InitializeVideoPlayer()
    {
        // Create RenderTexture if it doesn't exist
        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(1920, 1080, 24);
            renderTexture.Create();
        }

        // Setup VideoPlayer
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (videoPlayer != null)
        {
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = renderTexture;
            videoPlayer.playOnAwake = false;
            videoPlayer.loopPointReached += OnVideoEnded;
        }

        // Setup Canvas/RawImage for display
        if (videoDisplay != null)
        {
            videoDisplay.texture = renderTexture;
        }

        Debug.Log("[VideoPlayerManager] Initialized successfully.");
    }

    /// <summary>
    /// Play a video immediately by VideoClip reference.
    /// </summary>
    public void PlayVideoImmediate(VideoClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[VideoPlayerManager] Attempted to play null VideoClip!");
            return;
        }

        StopCurrentPlayback();
        currentPlaybackCoroutine = StartCoroutine(PlayVideoCoroutine(clip, 0f));
    }

    /// <summary>
    /// Play a video by index from the availableVideos array.
    /// </summary>
    public void PlayVideoByIndex(int index)
    {
        if (index >= 0 && index < availableVideos.Length)
        {
            PlayVideoImmediate(availableVideos[index]);
        }
        else
        {
            Debug.LogWarning($"[VideoPlayerManager] Invalid video index: {index}");
        }
    }

    /// <summary>
    /// Play a video with a delay (in seconds).
    /// </summary>
    public void PlayVideoWithDelay(VideoClip clip, float delaySeconds)
    {
        if (clip == null)
        {
            Debug.LogWarning("[VideoPlayerManager] Attempted to play null VideoClip!");
            return;
        }

        StopCurrentPlayback();
        currentPlaybackCoroutine = StartCoroutine(PlayVideoCoroutine(clip, delaySeconds));
    }

    /// <summary>
    /// Play a video by index with delay.
    /// </summary>
    public void PlayVideoByIndexWithDelay(int index, float delaySeconds)
    {
        if (index >= 0 && index < availableVideos.Length)
        {
            PlayVideoWithDelay(availableVideos[index], delaySeconds);
        }
        else
        {
            Debug.LogWarning($"[VideoPlayerManager] Invalid video index: {index}");
        }
    }

    private IEnumerator PlayVideoCoroutine(VideoClip clip, float delaySeconds)
    {
        // Wait for delay if specified
        if (delaySeconds > 0f)
        {
            yield return new WaitForSeconds(delaySeconds);
        }

        // Disable player movement
        DisablePlayerMovement();

        // Set up and play video
        videoPlayer.clip = clip;
        videoPlayer.Play();
        isVideoPlaying = true;

        // Show canvas if hidden
        if (videoCanvas != null)
            videoCanvas.enabled = true;

        onVideoStarted?.Invoke();
        Debug.Log($"[VideoPlayerManager] Playing video: {clip.name}");

        // Wait for video to finish
        while (videoPlayer.isPlaying)
        {
            yield return null;
        }
    }

    private void OnVideoEnded(VideoPlayer source)
    {
        isVideoPlaying = false;
        EnablePlayerMovement();
        onVideoEnded?.Invoke();

        // Hide canvas (optional)
        if (videoCanvas != null)
            videoCanvas.enabled = false;

        Debug.Log("[VideoPlayerManager] Video playback ended.");
    }

    /// <summary>
    /// Stop the current video playback.
    /// </summary>
    public void StopVideo()
    {
        if (currentPlaybackCoroutine != null)
        {
            StopCoroutine(currentPlaybackCoroutine);
            currentPlaybackCoroutine = null;
        }

        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }

        EnablePlayerMovement();
        isVideoPlaying = false;

        if (videoCanvas != null)
            videoCanvas.enabled = false;

        Debug.Log("[VideoPlayerManager] Video stopped.");
    }

    private void StopCurrentPlayback()
    {
        if (currentPlaybackCoroutine != null)
        {
            StopCoroutine(currentPlaybackCoroutine);
            currentPlaybackCoroutine = null;
        }

        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }
    }

    private void DisablePlayerMovement()
    {
        if (playerMovementScript != null)
        {
            playerMovementScript.SendMessage(disableMethodName, SendMessageOptions.DontRequireReceiver);
        }

        if (playerObject != null)
        {
            // Alternative: Disable CharacterController or Rigidbody
            var cc = playerObject.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            var rb = playerObject.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }

        Debug.Log("[VideoPlayerManager] Player movement disabled.");
    }

    private void EnablePlayerMovement()
    {
        if (playerMovementScript != null)
        {
            playerMovementScript.SendMessage(enableMethodName, SendMessageOptions.DontRequireReceiver);
        }

        if (playerObject != null)
        {
            // Alternative: Enable CharacterController or Rigidbody
            var cc = playerObject.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;

            var rb = playerObject.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;
        }

        Debug.Log("[VideoPlayerManager] Player movement enabled.");
    }

    public bool IsVideoPlaying => isVideoPlaying;

    public void SetPlayerMovementScript(MonoBehaviour script)
    {
        playerMovementScript = script;
    }

    public void SetDisableMethod(string methodName)
    {
        disableMethodName = methodName;
    }

    public void SetEnableMethod(string methodName)
    {
        enableMethodName = methodName;
    }

    private void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}