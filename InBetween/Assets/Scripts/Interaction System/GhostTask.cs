using TMPro;
using UnityEngine;
using UnityEngine.Events;
using System.Collections;

[DisallowMultipleComponent]
public class GhostTask : Interactable
{
    [Header("Ghost Task Setup")]
    [SerializeField] private string ghostID = "Ghost_001";

    [Header("Player Reference")]
    [SerializeField] private Transform playerTransform;

    [Header("Dialogue Objects")]
    [SerializeField] private GameObject greetingDialogue;
    [SerializeField] private GameObject taskIncompleteDialogue;
    [SerializeField] private GameObject taskCompleteDialogue;

    [Header("Ongoing Task Display")]
    [SerializeField] private TextMeshProUGUI ongoingTaskTMP;
    [SerializeField] private string ongoingTaskText = "Task in progress...";
    [SerializeField] private float typewriterSpeed = 0.05f;
    [SerializeField] private float delayBeforeTypewriter = 1f;

    [Header("Disappear Settings")]
    [SerializeField] private bool useDisappearDelay = true;
    [SerializeField] private float disappearDelay = 3f;
    [SerializeField] private bool useReverseTypewriterOnClear = true;
    [SerializeField] private float reverseTypewriterSpeed = 0.03f;

    [Header("Task Status")]
    [SerializeField] private bool isTaskComplete = false;

    [Header("Event Fire Delay")]
    [SerializeField] private bool useEventFireDelay = false;
    [SerializeField] private float eventFireDelay = 1f;

    [Header("Events")]
    [SerializeField] private UnityEvent onTaskComplete;
    [SerializeField] private UnityEvent onTaskIncomplete;

    private bool hasGreeted = false;
    private Coroutine typewriterCoroutine;
    private Coroutine reverseTypewriterCoroutine;
    private Coroutine disappearCoroutine;
    private Coroutine delayCoroutine;

    public override void OnInteract(PlayerInteractor interactor)
    {
        if (!CanInteract) return;

        if (playerTransform == null)
        {
            Debug.LogError($"[GhostTask - {ghostID}] Player transform not assigned in inspector!");
            return;
        }

        // First interaction - spawn greeting only
        if (!hasGreeted)
        {
            SpawnDialogueAtPlayer(greetingDialogue);
            hasGreeted = true;
            return;
        }

        // Subsequent interactions - check task status
        if (isTaskComplete)
        {
            SpawnDialogueAtPlayer(taskCompleteDialogue);
            FireEventWithDelay(onTaskComplete);
        }
        else
        {
            SpawnDialogueAtPlayer(taskIncompleteDialogue);
            FireEventWithDelay(onTaskIncomplete);
        }
    }

    private void SpawnDialogueAtPlayer(GameObject dialogue)
    {
        if (dialogue == null)
        {
            Debug.LogWarning($"[GhostTask - {ghostID}] Missing dialogue object!");
            return;
        }

        // Move actual object to player position
        dialogue.transform.position = playerTransform.position;

        // Activate if inactive
        if (!dialogue.activeInHierarchy)
            dialogue.SetActive(true);

        Debug.Log($"[GhostTask - {ghostID}] Dialogue spawned at player location!");
    }

    /// <summary>
    /// Display typewriter effect with the default delay before starting.
    /// Uses the ongoingTaskText from the inspector.
    /// </summary>
    public void StartDisplayTypewriterWithDelay()
    {
        if (ongoingTaskTMP == null)
        {
            Debug.LogWarning($"[GhostTask - {ghostID}] Ongoing Task TMP not assigned!");
            return;
        }

        // Stop any existing delay coroutine
        if (delayCoroutine != null)
            StopCoroutine(delayCoroutine);

        delayCoroutine = StartCoroutine(DisplayTypewriterWithDelayCoroutine());
    }

    /// <summary>
    /// Display typewriter effect with custom delay before starting.
    /// Uses the ongoingTaskText from the inspector.
    /// </summary>
    /// <param name="customDelay">Custom delay in seconds before typewriter starts</param>
    public void StartDisplayTypewriterWithCustomDelay(float customDelay)
    {
        if (ongoingTaskTMP == null)
        {
            Debug.LogWarning($"[GhostTask - {ghostID}] Ongoing Task TMP not assigned!");
            return;
        }

        // Stop any existing delay coroutine
        if (delayCoroutine != null)
            StopCoroutine(delayCoroutine);

        delayCoroutine = StartCoroutine(DisplayTypewriterWithDelayCoroutine(customDelay));
    }

    /// <summary>
    /// Start typewriter effect immediately.
    /// No delay before typewriter starts. Uses the ongoingTaskText from the inspector.
    /// </summary>
    public void StartTypewriterImmediate()
    {
        if (ongoingTaskTMP == null)
        {
            Debug.LogWarning($"[GhostTask - {ghostID}] Ongoing Task TMP not assigned!");
            return;
        }

        // Stop any existing delay coroutine
        if (delayCoroutine != null)
        {
            StopCoroutine(delayCoroutine);
            delayCoroutine = null;
        }

        StartTypewriter();
    }

    private IEnumerator DisplayTypewriterWithDelayCoroutine()
    {
        yield return new WaitForSeconds(delayBeforeTypewriter);
        StartTypewriter();
        delayCoroutine = null;
    }

    private IEnumerator DisplayTypewriterWithDelayCoroutine(float customDelay)
    {
        yield return new WaitForSeconds(customDelay);
        StartTypewriter();
        delayCoroutine = null;
    }

    private void StartTypewriter()
    {
        // Stop any existing typewriter coroutine
        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);

        // Stop any existing disappear coroutine
        if (disappearCoroutine != null)
        {
            StopCoroutine(disappearCoroutine);
            disappearCoroutine = null;
        }

        typewriterCoroutine = StartCoroutine(TypewriterEffect());
    }

    private IEnumerator TypewriterEffect()
    {
        ongoingTaskTMP.text = "";

        foreach (char character in ongoingTaskText)
        {
            ongoingTaskTMP.text += character;
            yield return new WaitForSeconds(typewriterSpeed);
        }

        Debug.Log($"[GhostTask - {ghostID}] Typewriter effect completed!");
        typewriterCoroutine = null;

        // Start disappear delay after typewriter completes
        if (useDisappearDelay && disappearDelay > 0f)
        {
            disappearCoroutine = StartCoroutine(DisappearAfterDelay());
        }
    }

    private IEnumerator DisappearAfterDelay()
    {
        yield return new WaitForSeconds(disappearDelay);

        if (ongoingTaskTMP != null)
        {
            ongoingTaskTMP.text = "";
            Debug.Log($"[GhostTask - {ghostID}] Text disappeared after delay!");
        }

        disappearCoroutine = null;
    }

    /// <summary>
    /// Clears the task display with reverse typewriter effect.
    /// Call this whenever you want to clear the text.
    /// </summary>
    public void ClearTaskDisplay()
    {
        if (ongoingTaskTMP == null)
            return;

        // Stop any ongoing delay coroutine
        if (delayCoroutine != null)
        {
            StopCoroutine(delayCoroutine);
            delayCoroutine = null;
        }

        // Stop any ongoing typewriter coroutine
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        // Stop any ongoing disappear coroutine
        if (disappearCoroutine != null)
        {
            StopCoroutine(disappearCoroutine);
            disappearCoroutine = null;
        }

        // Stop any existing reverse typewriter
        if (reverseTypewriterCoroutine != null)
            StopCoroutine(reverseTypewriterCoroutine);

        // Use reverse typewriter or instant clear based on settings
        if (useReverseTypewriterOnClear && ongoingTaskTMP.text.Length > 0)
        {
            reverseTypewriterCoroutine = StartCoroutine(ReverseTypewriterEffect());
        }
        else
        {
            ongoingTaskTMP.text = "";
            Debug.Log($"[GhostTask - {ghostID}] Task display cleared instantly!");
        }
    }

    /// <summary>
    /// Clears the task display with custom disappear delay then reverse typewriter.
    /// </summary>
    /// <param name="customDisappearDelay">Custom delay before reverse typewriter starts</param>
    public void ClearTaskDisplayWithDelay(float customDisappearDelay)
    {
        if (ongoingTaskTMP == null)
            return;

        // Stop any ongoing delay coroutine
        if (delayCoroutine != null)
        {
            StopCoroutine(delayCoroutine);
            delayCoroutine = null;
        }

        // Stop any existing reverse typewriter
        if (reverseTypewriterCoroutine != null)
            StopCoroutine(reverseTypewriterCoroutine);

        reverseTypewriterCoroutine = StartCoroutine(ClearWithDelayCoroutine(customDisappearDelay));
    }

    private IEnumerator ClearWithDelayCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Stop any ongoing typewriter coroutine
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        if (useReverseTypewriterOnClear && ongoingTaskTMP != null && ongoingTaskTMP.text.Length > 0)
        {
            yield return StartCoroutine(ReverseTypewriterEffect());
        }
        else if (ongoingTaskTMP != null)
        {
            ongoingTaskTMP.text = "";
        }

        reverseTypewriterCoroutine = null;
    }

    private IEnumerator ReverseTypewriterEffect()
    {
        while (ongoingTaskTMP.text.Length > 0)
        {
            ongoingTaskTMP.text = ongoingTaskTMP.text.Substring(0, ongoingTaskTMP.text.Length - 1);
            yield return new WaitForSeconds(reverseTypewriterSpeed);
        }

        Debug.Log($"[GhostTask - {ghostID}] Reverse typewriter effect completed!");
        reverseTypewriterCoroutine = null;
    }

    private void FireEventWithDelay(UnityEvent unityEvent)
    {
        if (useEventFireDelay)
        {
            Invoke(nameof(InvokeEvent), eventFireDelay);
            _pendingEvent = unityEvent;
        }
        else
        {
            unityEvent?.Invoke();
        }
    }

    private UnityEvent _pendingEvent;

    private void InvokeEvent()
    {
        _pendingEvent?.Invoke();
        _pendingEvent = null;
    }

    /// <summary>
    /// Call this method via UnityEvent to mark the task as complete
    /// </summary>
    public void CompleteTask()
    {
        isTaskComplete = true;
        Debug.Log($"[GhostTask - {ghostID}] Task marked as complete!");
    }

    /// <summary>
    /// Call this method via UnityEvent to reset the task
    /// </summary>
    public void ResetTask()
    {
        isTaskComplete = false;
        Debug.Log($"[GhostTask - {ghostID}] Task reset!");
    }

    public void SetTaskComplete(bool value)
    {
        isTaskComplete = value;
        Debug.Log($"[GhostTask - {ghostID}] Task set to: {value}");
    }

    public string GetGhostID() => ghostID;
    public bool IsTaskComplete() => isTaskComplete;
}