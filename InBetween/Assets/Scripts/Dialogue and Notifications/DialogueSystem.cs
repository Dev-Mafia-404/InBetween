using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.Events;
using StarterAssets;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class DialogueSystem : MonoBehaviour
{
    [System.Serializable]
    public class DialogueEntry
    {
        [TextArea] public string text;
        public AudioClip audioClip;
        [Min(0f)] public float displayTime;

        [Header("Choice (optional)")]
        [Tooltip("If true, after this entry's text/audio the player will be presented with two choices.")]
        public bool useChoice;

        [TextArea] public string choice1Label;
        [TextArea] public string choice1Response;
        public AudioClip choice1ResponseAudio;
        [Min(0f)] public float choice1ResponseDisplayTime;

        [TextArea] public string choice2Label;
        [TextArea] public string choice2Response;
        public AudioClip choice2ResponseAudio;
        [Min(0f)] public float choice2ResponseDisplayTime;
    }

    [Header("Dialogue Data")]
    [SerializeField] private DialogueEntry[] dialogueEntries;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private AudioSource audioSource;

    [Header("Choice UI")]
    [Tooltip("TMP text for choice 1 button. Its GameObject will be toggled active/inactive.")]
    [SerializeField] private TextMeshProUGUI choice1Text;
    [Tooltip("TMP text for choice 2 button. Its GameObject will be toggled active/inactive.")]
    [SerializeField] private TextMeshProUGUI choice2Text;
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color unselectedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [Tooltip("If true, scrolling past the last option wraps to the first.")]
    [SerializeField] private bool wrapChoiceScroll = true;

    [Header("Choice Input")]
    [SerializeField] private KeyCode choiceUpKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode choiceDownKey = KeyCode.DownArrow;
    [SerializeField] private KeyCode choiceConfirmKey = KeyCode.Space;
    [Tooltip("If true, left mouse click also confirms the current choice.")]
    [SerializeField] private bool confirmWithLeftClick = true;

    [Header("Choice Audio")]
    [SerializeField] private AudioClip choiceToggleSFX;

    [Header("Trigger Settings")]
    [SerializeField] private string playerTag = "Player";

    [Header("Lifecycle")]
    [Tooltip("If true, the dialogue can be retriggered and this GameObject is NOT destroyed.")]
    [SerializeField] private bool isLoopable = false;

    [Header("Other Behaviors")]
    [Tooltip("If true the system will wait for the longer of (entry.displayTime, audioClip.length).")]
    [SerializeField] private bool waitForFullAudio = true;
    [SerializeField] private bool clearTextOnFinish = true;

    [Header("Skip Settings")]
    [Tooltip("Enable skip functionality for dialogue. Skip is auto-disabled while waiting on a choice.")]
    [SerializeField] private bool enableSkip = true;
    [Tooltip("Key to press to skip the current dialogue.")]
    [SerializeField] private KeyCode skipKey = KeyCode.Backspace;

    [Header("Player Control")]
    [Tooltip("Freeze (disable) FPS_Controller while dialogue is running.")]
    [SerializeField] private bool freezeMovement = true;
    [Tooltip("Optional explicit reference. If empty it will be fetched by tag.")]
    [SerializeField] private FPS_Controller playerController;

    [Header("Timing")]
    [Tooltip("Enable a delay before showing any dialogue.")]
    [SerializeField] private bool useStartDelay = false;
    [Tooltip("Seconds to wait before the first dialogue appears when useStartDelay is enabled.")]
    [Min(0f)][SerializeField] private float startDelaySeconds = 0f;

    [Header("Selection")]
    [Tooltip("If enabled, picks a single random entry from the array and plays only that one.")]
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
    private bool _awaitingChoice;
    private int _selectedChoice; // 0 or 1

    public bool IsRunning => _isRunning;
    public bool IsLoopable => isLoopable;

    public void StartDialogue(FPS_Controller controllerOverride = null)
    {
        if (_isRunning) return;

        if (controllerOverride != null)
            playerController = controllerOverride;

        if (dialogueEntries == null || dialogueEntries.Length == 0)
        {
            Debug.LogWarning($"[DialogueSystem] No dialogue entries assigned on '{name}'.");
            return;
        }
        if (dialogueText == null)
        {
            Debug.LogWarning($"[DialogueSystem] Missing TextMeshProUGUI reference on '{name}'.");
            return;
        }
        if (popupRoot == null)
        {
            Debug.LogWarning($"[DialogueSystem] Missing popupRoot reference on '{name}'.");
            return;
        }
        if (audioSource == null)
        {
            Debug.LogWarning($"[DialogueSystem] Missing AudioSource (will continue without audio) on '{name}'.");
        }

        TryAcquirePlayerControllerIfNeeded();

        if (freezeMovement)
            TryFreezeMovement();

        if (popupRoot != null)
            popupRoot.SetActive(true);

        HideChoiceButtons();

        _isRunning = true;
        _currentIndex = 0;

        if (onStarted != null)
            onStarted.Invoke();

        _runRoutine = StartCoroutine(RunDialogueSequence());
    }

    public void FinishDialogue()
    {
        if (_isRunning)
            _isRunning = false;

        _awaitingChoice = false;
        HideChoiceButtons();

        if (objectsToActivate != null)
        {
            for (int i = 0; i < objectsToActivate.Length; i++)
            {
                var go = objectsToActivate[i];
                if (go != null)
                    go.SetActive(true);
            }
        }

        if (freezeMovement)
            TryUnfreezeMovement();

        if (clearTextOnFinish && dialogueText != null)
            dialogueText.text = string.Empty;

        if (popupRoot != null)
            popupRoot.SetActive(false);

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
        FinishDialogue();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        var controller = other.GetComponentInParent<FPS_Controller>();
        if (controller != null)
            playerController = controller;

        StartDialogue(playerController);
    }

    private void Update()
    {
        // Skip is disabled while waiting on a choice to avoid conflicting with confirm input.
        if (_isRunning && !_awaitingChoice && enableSkip && Input.GetKeyDown(skipKey))
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

        if (_isRunning && freezeMovement)
            TryUnfreezeMovement();

        HideChoiceButtons();

        if (popupRoot != null)
            popupRoot.SetActive(false);
    }

    // Dialogue Coroutine 
    private IEnumerator RunDialogueSequence()
    {
        if (useStartDelay && startDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(startDelaySeconds);
            if (!_isRunning) yield break;
        }

        bool playOnlyOnce = false;
        if (randomizeSingleEntry)
        {
            _currentIndex = Random.Range(0, dialogueEntries.Length);
            playOnlyOnce = true;
        }

        while (_currentIndex >= 0 && _currentIndex < dialogueEntries.Length)
        {
            DialogueEntry entry = dialogueEntries[_currentIndex];
            if (entry == null)
            {
                Debug.LogWarning($"[DialogueSystem] Null DialogueEntry at index {_currentIndex} on '{name}'. Skipping.");
                _currentIndex++;
                continue;
            }

            // Play the entry's own line
            yield return PlayLine(entry.text, entry.audioClip, entry.displayTime);
            if (!_isRunning) yield break;

            // If this entry has a choice, present it and then play the chosen response
            if (entry.useChoice)
            {
                int picked = 0;
                yield return WaitForChoice(entry, r => picked = r);
                if (!_isRunning) yield break;

                if (picked == 0)
                    yield return PlayLine(entry.choice1Response, entry.choice1ResponseAudio, entry.choice1ResponseDisplayTime);
                else
                    yield return PlayLine(entry.choice2Response, entry.choice2ResponseAudio, entry.choice2ResponseDisplayTime);

                if (!_isRunning) yield break;
            }

            if (playOnlyOnce)
            {
                _currentIndex = dialogueEntries.Length;
            }
            else
            {
                _currentIndex++;
            }
        }

        _runRoutine = null;
        _isRunning = false;
        FinishDialogue();
    }

    // Plays a single line of text with optional audio + wait.
    private IEnumerator PlayLine(string text, AudioClip clip, float displayTime)
    {
        if (dialogueText != null)
            dialogueText.text = text ?? string.Empty;

        float waitTime = Mathf.Max(0f, displayTime);

        if (audioSource != null && clip != null)
        {
            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();

            if (waitForFullAudio)
                waitTime = Mathf.Max(waitTime, clip.length);
        }

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
    }

    // Choice handling 
    private IEnumerator WaitForChoice(DialogueEntry entry, System.Action<int> onPicked)
    {
        _awaitingChoice = true;
        _selectedChoice = 0;

        ShowChoiceButtons(entry.choice1Label, entry.choice2Label);
        RefreshChoiceHighlight();

        while (_isRunning)
        {
            // Scroll wheel
            float scroll = Input.mouseScrollDelta.y;
            if (scroll > 0f)
            {
                MoveSelection(-1);
            }
            else if (scroll < 0f)
            {
                MoveSelection(1);
            }

            // Arrow keys
            if (Input.GetKeyDown(choiceUpKey))
                MoveSelection(-1);
            else if (Input.GetKeyDown(choiceDownKey))
                MoveSelection(1);

            // Confirm
            bool confirmed = Input.GetKeyDown(choiceConfirmKey)
                             || (confirmWithLeftClick && Input.GetMouseButtonDown(0));
            if (confirmed)
            {
                onPicked?.Invoke(_selectedChoice);
                break;
            }

            yield return null;
        }

        HideChoiceButtons();
        _awaitingChoice = false;
    }

    private void MoveSelection(int delta)
    {
        int next = _selectedChoice + delta;
        if (wrapChoiceScroll)
        {
            if (next < 0) next = 1;
            else if (next > 1) next = 0;
        }
        else
        {
            next = Mathf.Clamp(next, 0, 1);
        }

        if (next != _selectedChoice)
        {
            _selectedChoice = next;

            // Play toggle SFX
            if (audioSource != null && choiceToggleSFX != null)
            {
                audioSource.PlayOneShot(choiceToggleSFX);
            }

            RefreshChoiceHighlight();
        }
    }

    private void ShowChoiceButtons(string label1, string label2)
    {
        if (choice1Text != null)
        {
            choice1Text.text = label1 ?? string.Empty;
            choice1Text.gameObject.SetActive(true);
        }
        if (choice2Text != null)
        {
            choice2Text.text = label2 ?? string.Empty;
            choice2Text.gameObject.SetActive(true);
        }
    }

    private void HideChoiceButtons()
    {
        if (choice1Text != null)
            choice1Text.gameObject.SetActive(false);
        if (choice2Text != null)
            choice2Text.gameObject.SetActive(false);
    }

    private void RefreshChoiceHighlight()
    {
        if (choice1Text != null)
            choice1Text.color = (_selectedChoice == 0) ? selectedColor : unselectedColor;
        if (choice2Text != null)
            choice2Text.color = (_selectedChoice == 1) ? selectedColor : unselectedColor;
    }

    // Movement Freeze Helpers
    private void TryAcquirePlayerControllerIfNeeded()
    {
        if (playerController != null) return;

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
        playerController.SuppressAudio = true;
        _movementFrozenByUs = true;
    }

    private void TryUnfreezeMovement()
    {
        if (!_movementFrozenByUs) return;
        if (playerController == null) return;

        playerController.enabled = true;
        playerController.SuppressAudio = false;
        _movementFrozenByUs = false;
    }
}