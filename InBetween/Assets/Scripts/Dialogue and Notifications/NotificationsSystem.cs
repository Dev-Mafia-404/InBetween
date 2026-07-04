using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.Events;
using StarterAssets;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class NotificationSystem : MonoBehaviour
{
    [System.Serializable]
    public class NotificationEntry
    {
        [TextArea] public string text;
        public AudioClip audioClip;
        [Min(0f)] public float displayTime = 1f;
    }

    [Header("Notification Data")]
    [SerializeField] private NotificationEntry[] notificationEntries;

    [Header("Popup UI References")]
    [Tooltip("Root GameObject of the popup (enable/disable to show/hide).")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private AudioSource audioSource;

    [Header("Trigger Settings")]
    [SerializeField] private string playerTag = "Player";

    [Header("Lifecycle")]
    [Tooltip("If true, the notification can be retriggered and this GameObject is NOT destroyed.")]
    [SerializeField] private bool isLoopable = false;

    [Header("Behavior")]
    [Tooltip("If true the popup toggles off between entries, then re-activates for the next one.")]
    [SerializeField] private bool deactivateBetweenEntries = true;
    [Tooltip("Time to remain hidden between entries when deactivating.")]
    [SerializeField, Min(0f)] private float gapBetweenNotifications = 0.1f;
    [SerializeField] private bool clearTextOnFinish = true;

    [Header("Skip Settings")]
    [Tooltip("Enable skip functionality for notifications.")]
    [SerializeField] private bool enableSkip = true;
    [Tooltip("Key to press to skip the current notification.")]
    [SerializeField] private KeyCode skipKey = KeyCode.Backspace;

    [Header("Player Control")]
    [Tooltip("Freeze (disable) FPS_Controller while notifications are running.")]
    [SerializeField] private bool freezeMovement = false;
    [Tooltip("Optional explicit reference. If empty it will be fetched by tag.")]
    [SerializeField] private FPS_Controller playerController;

    [Header("Timing")]
    [Tooltip("Enable a delay before showing any notification.")]
    [SerializeField] private bool useStartDelay = false;
    [Tooltip("Seconds to wait before the first notification appears when useStartDelay is enabled.")]
    [Min(0f)][SerializeField] private float startDelaySeconds = 0f;

    [Header("Selection")]
    [Tooltip("If enabled, picks a single random entry from the array and shows only that one.")]
    [SerializeField] private bool randomizeSingleEntry = false;

    [Header("Activation On Finish")]
    [SerializeField] private GameObject[] objectsToActivate;

    [Header("Events")]
    [SerializeField] private UnityEvent onStarted;
    public UnityEvent OnStarted => onStarted;

    [SerializeField] private UnityEvent onCompleted;
    public UnityEvent OnCompleted => onCompleted;

    private int _currentIndex = -1;
    private Coroutine _runRoutine;
    private bool _isRunning;
    private bool _movementFrozenByUs;

    public bool IsRunning => _isRunning;
    public bool IsLoopable => isLoopable;

    public void StartNotifications(FPS_Controller controllerOverride = null)
    {
        if (_isRunning) return;

        if (controllerOverride != null)
            playerController = controllerOverride;

        if (notificationEntries == null || notificationEntries.Length == 0)
        {
            Debug.LogWarning($"[NotificationSystem] No notification entries assigned on '{name}'.");
            return;
        }
        if (popupRoot == null)
        {
            Debug.LogWarning($"[NotificationSystem] Missing Popup Root reference on '{name}'.");
            return;
        }
        if (notificationText == null)
        {
            Debug.LogWarning($"[NotificationSystem] Missing TextMeshProUGUI reference on '{name}'.");
            return;
        }
        if (audioSource == null)
        {
            Debug.LogWarning($"[NotificationSystem] Missing AudioSource (will continue without audio) on '{name}'.");
        }

        TryAcquirePlayerControllerIfNeeded();

        if (freezeMovement)
            TryFreezeMovement();

        // Ensure popup starts hidden before we show the first entry
        popupRoot.SetActive(false);

        _isRunning = true;
        _currentIndex = 0;

        // Fire OnStarted event
        if (onStarted != null)
            onStarted.Invoke();

        _runRoutine = StartCoroutine(RunNotificationSequence());
    }

    public void FinishNotifications()
    {
        if (_isRunning)
            _isRunning = false;

        // Reactivate objects
        if (objectsToActivate != null)
        {
            for (int i = 0; i < objectsToActivate.Length; i++)
            {
                var go = objectsToActivate[i];
                if (go != null)
                    go.SetActive(true);
            }
        }

        // Unfreeze movement if frozen
        if (freezeMovement)
            TryUnfreezeMovement();

        // Hide popup and clear text
        if (popupRoot != null)
            popupRoot.SetActive(false);

        if (clearTextOnFinish && notificationText != null)
            notificationText.text = string.Empty;

        // Invoke completion event (if any listeners)
        if (onCompleted != null)
            onCompleted.Invoke();

        if (isLoopable)
        {
            _currentIndex = -1;
            _runRoutine = null;
            return;
        }

        Destroy(gameObject);
    }

    public void AbortAndFinish()
    {
        if (_runRoutine != null)
        {
            StopCoroutine(_runRoutine);
            _runRoutine = null;
        }
        FinishNotifications();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        var controller = other.GetComponentInParent<FPS_Controller>();
        if (controller != null)
            playerController = controller;

        StartNotifications(playerController);
    }

    private void Update()
    {
        // Only allow skipping when notifications are running and skip is enabled
        if (_isRunning && enableSkip && Input.GetKeyDown(skipKey))
        {
            AbortAndFinish();
        }
    }

    private void OnDisable()
    {
        if (_runRoutine != null)
        {
            StopCoroutine(_runRoutine);
            _runRoutine = null;
        }

        // if disabled mid-sequence movement reactivates
        if (_isRunning && freezeMovement)
            TryUnfreezeMovement();

        if (popupRoot != null)
            popupRoot.SetActive(false);
    }

    // Notification Coroutine 
    private IEnumerator RunNotificationSequence()
    {
        // Optional start delay before showing anything
        if (useStartDelay && startDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(startDelaySeconds);
            if (!_isRunning) yield break;
        }

        // If randomize is on, pick one random entry and show only that one
        bool playOnlyOnce = false;
        if (randomizeSingleEntry)
        {
            _currentIndex = Random.Range(0, notificationEntries.Length);
            playOnlyOnce = true;
        }

        bool first = true;

        while (_currentIndex >= 0 && _currentIndex < notificationEntries.Length)
        {
            NotificationEntry entry = notificationEntries[_currentIndex];
            if (entry == null)
            {
                Debug.LogWarning($"[NotificationSystem] Null NotificationEntry at index {_currentIndex} on '{name}'. Skipping.");
                _currentIndex++;
                continue;
            }

            // Between-entries hide gap (after the very first entry)
            if (!first && deactivateBetweenEntries)
            {
                if (popupRoot != null) popupRoot.SetActive(false);
                if (notificationText != null) notificationText.text = string.Empty;

                if (gapBetweenNotifications > 0f)
                {
                    float gap = 0f;
                    while (gap < gapBetweenNotifications)
                    {
                        if (!_isRunning) yield break;
                        gap += Time.deltaTime;
                        yield return null;
                    }
                }
            }
            first = false;

            // Prepare UI for this entry
            if (notificationText != null)
                notificationText.text = entry.text ?? string.Empty;

            if (popupRoot != null)
                popupRoot.SetActive(true);

            // Play sound (SFX)
            float waitTime = Mathf.Max(0f, entry.displayTime);
            if (audioSource != null && entry.audioClip != null)
            {
                audioSource.Stop();
                audioSource.clip = entry.audioClip;
                audioSource.Play();
            }

            // Wait while showing this notification (only for displayTime)
            if (waitTime > 0f)
            {
                float elapsed = 0f;
                while (elapsed < waitTime)
                {
                    if (!_isRunning)
                        yield break;

                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            // After each entry, optionally deactivate (toggled again on next iteration)
            if (deactivateBetweenEntries && popupRoot != null)
                popupRoot.SetActive(false);

            if (playOnlyOnce)
            {
                // Stop after the single randomly chosen entry
                _currentIndex = notificationEntries.Length;
            }
            else
            {
                _currentIndex++;
            }
        }

        _runRoutine = null;
        _isRunning = false;
        FinishNotifications();
    }

    // Movement Freeze Helpers with Audio Suppression
    private void TryAcquirePlayerControllerIfNeeded()
    {
        if (playerController != null) return;

        // Try find by tag (fallback if manual start without trigger)
        if (!string.IsNullOrEmpty(playerTag))
        {
            GameObject tagged = GameObject.FindGameObjectWithTag(playerTag);
            if (tagged != null)
            {
                playerController = tagged.GetComponentInChildren<FPS_Controller>();
            }
        }
    }

    private void TryFreezeMovement()
    {
        if (playerController == null) return;
        if (!playerController.enabled) return;

        playerController.enabled = false;
        playerController.SuppressAudio = true;  // Suppress audio output (footsteps, etc)
        _movementFrozenByUs = true;
    }

    private void TryUnfreezeMovement()
    {
        if (!_movementFrozenByUs) return;
        if (playerController == null) return;

        playerController.enabled = true;
        playerController.SuppressAudio = false;  // Re-enable audio output
        _movementFrozenByUs = false;
    }
}