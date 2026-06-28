using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Lives on EACH ghost. Owns that ghost's GhostProximity (detection) and its
/// GhostMatchDifficulty asset. Does NOT listen for input itself — the single shared
/// FrequencyMatchController owns the F key and decides which ghost (if any) it applies
/// to, picking the closest valid candidate. This avoids the race condition that occurs
/// when multiple ghosts each independently listen for the same keypress.
///
/// IMPORTANT: there is only ONE FrequencyMatchGame in the scene (shared by all ghosts),
/// so its OnGreenMatch/OnYellowMatch/OnRedMatch events fire identically regardless of
/// WHICH ghost was actually being matched — they're only useful for global reactions
/// (e.g. a generic UI flash). For ghost-specific behavior (this ghost's reveal, this
/// ghost's unique reward, etc.) wire up the events below instead — each ghost instance
/// has its own, so different ghosts can do completely different things on the same grade.
/// </summary>
[RequireComponent(typeof(GhostProximity))]
public class GhostController : MonoBehaviour
{
    [Header("Identity")]
    public string ghostName = "Wisp";

    [Header("Difficulty")]
    [Tooltip("Drag this ghost's difficulty asset here (Create > Ghosts > Match Difficulty)")]
    public GhostMatchDifficulty difficulty;

    [Header("Shared Remote (same object for every ghost in the scene)")]
    public FrequencyMatchController remoteController;

    [Header("Per-Ghost Capture Events (THIS ghost only)")]
    [Tooltip("Fired the instant THIS ghost's target frequency is captured.")]
    public UnityEvent<float> OnThisGhostTargetCaptured;
    [Tooltip("Fired the instant THIS ghost's player-guess frequency is captured (Match pressed or timeout).")]
    public UnityEvent<float> OnThisGhostPlayerCaptured;

    [Header("Per-Ghost Result Events (THIS ghost only — not shared with other ghosts)")]
    [Tooltip("Fired when THIS specific ghost's match resolves green, after this ghost's difficulty.eventFireDelay.")]
    public UnityEvent<float> OnThisGhostGreen;
    [Tooltip("Fired when THIS specific ghost's match resolves yellow, after this ghost's difficulty.eventFireDelay.")]
    public UnityEvent<float> OnThisGhostYellow;
    [Tooltip("Fired when THIS specific ghost's match resolves red (or times out), after this ghost's difficulty.eventFireDelay.")]
    public UnityEvent<float> OnThisGhostRed;

    GhostProximity proximity;
    bool isRevealed;

    /// <summary>Read-only access for the shared controller to query live distance state each frame.</summary>
    public GhostProximity Proximity => proximity;

    /// <summary>True once this ghost has been successfully caught — excluded from future F-press candidacy.</summary>
    public bool IsRevealed => isRevealed;

    void Awake()
    {
        proximity = GetComponent<GhostProximity>();
        proximity.ApplyDifficulty(difficulty);
    }

    void OnEnable()
    {
        if (remoteController != null)
            remoteController.RegisterGhost(this);
    }

    void OnDisable()
    {
        if (remoteController == null) return;

        if (remoteController.IsOpenFor(this))
            remoteController.ReleaseFromGhost(this);

        remoteController.UnregisterGhost(this);
    }

    /// <summary>Called by FrequencyMatchController the instant an attempt is actively engaged (target captured).</summary>
    public void NotifyMinigameOpened()
    {
        proximity.SetMinigameActive(true);
    }

    /// <summary>Called by FrequencyMatchController when this ghost's match resolves green.</summary>
    public void NotifyCaught()
    {
        isRevealed = true;
        proximity.SetMinigameActive(true); // stays silent forever — ghost is caught
        // TODO: trigger your actual reveal VFX/model-visibility here.
        Debug.Log($"[{ghostName}] Caught! Revealing.");
    }

    /// <summary>Called by FrequencyMatchController when the remote closes without a successful match.</summary>
    public void NotifyRemoteClosed()
    {
        proximity.SetMinigameActive(false); // resume audio, still in range
    }

    /// <summary>Called by FrequencyMatchController the instant THIS ghost's target frequency is captured.</summary>
    public void FireTargetCaptured(float value)
    {
        OnThisGhostTargetCaptured?.Invoke(value);
    }

    /// <summary>Called by FrequencyMatchController the instant THIS ghost's player-guess frequency is captured.</summary>
    public void FirePlayerCaptured(float value)
    {
        OnThisGhostPlayerCaptured?.Invoke(value);
    }

    /// <summary>Called by FrequencyMatchController (after this ghost's own difficulty.eventFireDelay) to fire THIS ghost's specific result event.</summary>
    public void FireResultEvent(FrequencyMatchGame.MatchGrade grade, float delta)
    {
        switch (grade)
        {
            case FrequencyMatchGame.MatchGrade.Green:
                OnThisGhostGreen?.Invoke(delta);
                break;
            case FrequencyMatchGame.MatchGrade.Yellow:
                OnThisGhostYellow?.Invoke(delta);
                break;
            case FrequencyMatchGame.MatchGrade.Red:
                OnThisGhostRed?.Invoke(delta);
                break;
        }
    }
}