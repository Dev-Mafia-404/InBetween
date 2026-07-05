using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// VideoPlayerManager: Plays videos on camera near plane through Unity Events.
/// Supports immediate playback, delayed playback, and player movement control.
/// Individual events per video index for fine-grained control.
/// </summary>
public class VideoPlayerScript : MonoBehaviour
{
    [System.Serializable]
    public class VideoEvents
    {
        public UnityEvent onStarted;
        public UnityEvent onEnded;
    }

    [Header("Video Setup")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private Canvas videoCanvas;
    [SerializeField] private RawImage videoDisplay;
    [Tooltip("Drag your video clips here in the Inspector")]
    [SerializeField] private VideoClip[] availableVideos;

    [Header("Per-Video Events")]
    [SerializeField] private VideoEvents[] videoEvents;

    [Header("Player Reference")]
    [SerializeField] private GameObject playerObject;
    [SerializeField] private MonoBehaviour playerMovementScript;
    [SerializeField] private string disableMethodName = "DisableMovement";
    [SerializeField] private string enableMethodName = "EnableMovement";

    [Header("Global Events")]
    [SerializeField] private UnityEvent onAnyVideoStarted;
    [SerializeField] private UnityEvent onAnyVideoEnded;

    private Coroutine currentPlaybackCoroutine;
    private RenderTexture renderTexture;
    private bool isVideoPlaying;
    private int currentVideoIndex = -1;

    private void Awake()
    {
        InitializeVideoPlayer();
    }

    private void OnValidate()
    {
        // Ensure videoEvents array matches availableVideos length
        if (availableVideos != null)
        {
            if (videoEvents == null || videoEvents.Length != availableVideos.Length)
            {
                System.Array.Resize(ref videoEvents, availableVideos.Length);

                // Initialize new events
                for (int i = 0; i < videoEvents.Length; i++)
                {
                    if (videoEvents[i] == null)
                    {
                        videoEvents[i] = new VideoEvents();
                    }
                }
            }
        }
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

        // Initialize video events array if needed
        if (videoEvents == null || videoEvents.Length != availableVideos.Length)
        {
            System.Array.Resize(ref videoEvents, availableVideos.Length);
            for (int i = 0; i < videoEvents.Length; i++)
            {
                if (videoEvents[i] == null)
                {
                    videoEvents[i] = new VideoEvents();
                }
            }
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

        int index = System.Array.IndexOf(availableVideos, clip);
        PlayVideoByIndex(index);
    }

    /// <summary>
    /// Play a video by index from the availableVideos array.
    /// </summary>
    public void PlayVideoByIndex(int index)
    {
        if (index >= 0 && index < availableVideos.Length)
        {
            StopCurrentPlayback();
            currentPlaybackCoroutine = StartCoroutine(PlayVideoCoroutine(availableVideos[index], index, 0f));
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

        int index = System.Array.IndexOf(availableVideos, clip);
        PlayVideoByIndexWithDelay(index, delaySeconds);
    }

    /// <summary>
    /// Play a video by index with delay.
    /// </summary>
    public void PlayVideoByIndexWithDelay(int index, float delaySeconds)
    {
        if (index >= 0 && index < availableVideos.Length)
        {
            StopCurrentPlayback();
            currentPlaybackCoroutine = StartCoroutine(PlayVideoCoroutine(availableVideos[index], index, delaySeconds));
        }
        else
        {
            Debug.LogWarning($"[VideoPlayerManager] Invalid video index: {index}");
        }
    }

    private IEnumerator PlayVideoCoroutine(VideoClip clip, int index, float delaySeconds)
    {
        // Wait for delay if specified
        if (delaySeconds > 0f)
        {
            yield return new WaitForSeconds(delaySeconds);
        }

        // Disable player movement
        DisablePlayerMovement();

        // Set up and play video
        currentVideoIndex = index;
        videoPlayer.clip = clip;
        videoPlayer.Play();
        isVideoPlaying = true;

        // Show canvas if hidden
        if (videoCanvas != null)
            videoCanvas.enabled = true;

        // Invoke index-specific event
        if (index >= 0 && index < videoEvents.Length)
        {
            videoEvents[index].onStarted?.Invoke();
        }

        // Invoke global event
        onAnyVideoStarted?.Invoke();

        Debug.Log($"[VideoPlayerManager] Playing video [{index}]: {clip.name}");

        // Wait for video to finish
        while (videoPlayer.isPlaying)
        {
            yield return null;
        }
    }

    private void OnVideoEnded(VideoPlayer source)
    {
        isVideoPlaying = false;
        int endedIndex = currentVideoIndex;
        EnablePlayerMovement();

        // Invoke index-specific event
        if (endedIndex >= 0 && endedIndex < videoEvents.Length)
        {
            videoEvents[endedIndex].onEnded?.Invoke();
        }

        // Invoke global event
        onAnyVideoEnded?.Invoke();

        // Hide canvas (optional)
        if (videoCanvas != null)
            videoCanvas.enabled = false;

        currentVideoIndex = -1;
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
        currentVideoIndex = -1;

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
            var cc = playerObject.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = true;

            var rb = playerObject.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;
        }

        Debug.Log("[VideoPlayerManager] Player movement enabled.");
    }

    public bool IsVideoPlaying => isVideoPlaying;
    public int CurrentVideoIndex => currentVideoIndex;

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