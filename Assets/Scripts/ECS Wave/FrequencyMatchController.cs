using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The "conductor" — ONE of these exists in the scene, shared by every ghost.
///
/// Ownership model: this is the ONLY place that reads the F key. Ghosts register
/// themselves here on enable/disable; every frame while open, the controller picks
/// whichever registered, non-revealed ghost is currently closest and renders ITS data.
/// This is what makes multiple simultaneous ghosts work correctly — there is exactly
/// one input check and one "closest wins" decision per frame, instead of every ghost
/// independently reacting to the same keypress (which caused the old race-condition bug).
///
/// Leaving a ghost's close range while mid-attempt (or a different ghost becoming closer)
/// resets that attempt back to live distance-tracking — target re-rolls fresh next time
/// close range is reached, nothing is remembered across the gap.
/// </summary>
public class FrequencyMatchController : MonoBehaviour
{
    [Header("Core References")]
    public FrequencyMatchGame game;
    public FrequencyWaveUI targetWave;
    public FrequencyWaveUI playerWave;

    [Header("Buttons (visible always once open, interactable only when valid)")]
    public Button captureButton;
    public Button matchButton;
    public KeyCode captureKey = KeyCode.Return;
    public KeyCode openRemoteKey = KeyCode.F;

    [Header("Root")]
    public GameObject remoteRoot;

    [Header("Number Readouts")]
    public TMP_Text targetValueText;
    public TMP_Text playerValueText;
    public string unitSuffix = "Hz";

    [Header("Match Status Text")]
    public TypewriterText matchStatusTypewriter;
    public string matchingMessage = "Matching.....";
    public string greenMessage = "Match Successful!";
    public string yellowMessage = "Partial Match";
    public string redMessage = "Match Failed";
    public string timeoutMessage = "The ghost slipped away...";
    public float matchingHoldSeconds = 1f;
    public float resultHoldBeforeCloseSeconds = 1.2f;

    [Header("Timer Display (optional)")]
    public TMP_Text timerText;

    [Header("Cursor")]
    public bool manageCursor = true;

    [Header("Scroll Input")]
    public string scrollAxisName = "Mouse ScrollWheel";

    readonly List<GhostController> registeredGhosts = new List<GhostController>();

    bool isOpen;
    GhostController currentGhost;       // whichever ghost is closest THIS frame
    bool resolving;                     // true while Matching/result sequence plays — blocks switching/input
    bool playerHasScrolled;
    string lastAppliedDifficultyGhostName;

    void Awake()
    {
        if (captureButton != null) captureButton.onClick.AddListener(OnCaptureButtonPressed);
        if (matchButton != null) matchButton.onClick.AddListener(OnMatchButtonPressed);
    }

    void OnDestroy()
    {
        if (captureButton != null) captureButton.onClick.RemoveListener(OnCaptureButtonPressed);
        if (matchButton != null) matchButton.onClick.RemoveListener(OnMatchButtonPressed);
    }

    void Start()
    {
        CloseRemoteInternal();
    }

    // ---------------------------------------------------------------
    // Ghost registry
    // ---------------------------------------------------------------

    public void RegisterGhost(GhostController ghost)
    {
        if (!registeredGhosts.Contains(ghost))
            registeredGhosts.Add(ghost);
    }

    public void UnregisterGhost(GhostController ghost)
    {
        registeredGhosts.Remove(ghost);
        if (currentGhost == ghost)
            currentGhost = null;
    }

    public bool IsOpenFor(GhostController ghost) => isOpen && currentGhost == ghost;

    /// <summary>A ghost is being disabled/destroyed mid-attempt; release cleanly if it was the active one.</summary>
    public void ReleaseFromGhost(GhostController ghost)
    {
        if (currentGhost != ghost) return;
        currentGhost = null;
    }

    // ---------------------------------------------------------------
    // Single, centralized input check — the fix for the multi-ghost race condition.
    // ---------------------------------------------------------------

    void Update()
    {
        if (Input.GetKeyDown(openRemoteKey) && !resolving)
            ToggleRemote();

        if (!isOpen || resolving) return;

        UpdateClosestGhost();

        if (currentGhost == null)
        {
            ShowNoSignal();
            return;
        }

        DriveCurrentGhost();
        RefreshButtonInteractable();

        if (Input.GetKeyDown(captureKey))
        {
            if (game.CanCaptureTarget) OnCaptureButtonPressed();
            else if (game.CanMatch) OnMatchButtonPressed();
        }
    }

    void ToggleRemote()
    {
        if (isOpen)
            CloseRemote();
        else
            OpenRemote();
    }

    // ---------------------------------------------------------------
    // Closest-ghost selection — runs every frame while open.
    // ---------------------------------------------------------------

    void UpdateClosestGhost()
    {
        GhostController best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < registeredGhosts.Count; i++)
        {
            var g = registeredGhosts[i];
            if (g == null || g.IsRevealed) continue;
            var prox = g.Proximity;
            if (prox == null || prox.player == null) continue;
            if (prox.currentState == GhostProximity.ProximityState.OutOfRange) continue;

            float dist = Vector3.Distance(prox.player.position, prox.transform.position);

            // Strict less-than means an already-selected ghost keeps the slot unless
            // something else becomes GENUINELY closer — avoids flicker between two
            // ghosts sitting at near-identical distance.
            if (dist < bestDist)
            {
                bestDist = dist;
                best = g;
            }
        }

        if (best != currentGhost)
            SwitchToGhost(best);
    }

    /// <summary>Switching to a new closest ghost (or to none) is a clean reset — nothing carries over.</summary>
    void SwitchToGhost(GhostController newGhost)
    {
        currentGhost = newGhost;
        playerHasScrolled = false;

        if (currentGhost == null)
        {
            ShowNoSignal();
            return;
        }

        ApplyDifficultyIfNeeded(currentGhost);
        game.OpenRemote(); // fresh session for the newly-selected ghost
        ResetVisualsToBlank();
    }

    void ApplyDifficultyIfNeeded(GhostController ghost)
    {
        if (lastAppliedDifficultyGhostName == ghost.ghostName && ghost.difficulty != null) return;

        var difficulty = ghost.difficulty;
        if (difficulty == null) return;

        game.ApplyDifficulty(difficulty);
        targetWave.ApplyGhostTuning(difficulty);
        playerWave.ApplyGhostTuning(difficulty);

        lastAppliedDifficultyGhostName = ghost.ghostName;
    }

    void ResetVisualsToBlank()
    {
        targetWave.SetFlatline();
        targetWave.SetGradeColor(targetWave.neutralColor, instant: true);
        playerWave.SetFlatline();
        playerWave.SetGradeColor(playerWave.neutralColor, instant: true);
        if (targetValueText != null) targetValueText.text = "";
        if (playerValueText != null) playerValueText.text = "";
        if (matchStatusTypewriter != null) matchStatusTypewriter.SetInstant("");
        if (timerText != null) timerText.text = "";
    }

    void ShowNoSignal()
    {
        ResetVisualsToBlank();
        if (captureButton != null) captureButton.interactable = false;
        if (matchButton != null) matchButton.interactable = false;
    }

    // ---------------------------------------------------------------
    // Open / close
    // ---------------------------------------------------------------

    void OpenRemote()
    {
        isOpen = true;
        currentGhost = null;
        lastAppliedDifficultyGhostName = null;
        playerHasScrolled = false;

        if (remoteRoot != null) remoteRoot.SetActive(true);
        SetCursorVisible(true);

        ResetVisualsToBlank();
        UpdateClosestGhost(); // pick immediately so it doesn't sit blank for a frame
    }

    void CloseRemote()
    {
        currentGhost = null;
        CloseRemoteInternal();
    }

    /// <summary>Used after a match resolves (any grade).</summary>
    void CloseRemoteAfterResolution(bool wasGreen)
    {
        if (currentGhost != null)
        {
            if (wasGreen)
                currentGhost.NotifyCaught();
            else
                currentGhost.NotifyRemoteClosed();
        }
        currentGhost = null;
        CloseRemoteInternal();
    }

    void CloseRemoteInternal()
    {
        isOpen = false;
        resolving = false;
        if (remoteRoot != null) remoteRoot.SetActive(false);
        SetCursorVisible(false);
    }

    void SetCursorVisible(bool visible)
    {
        if (!manageCursor) return;
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
    }

    // ---------------------------------------------------------------
    // Per-frame drive of whichever ghost is currently selected
    // ---------------------------------------------------------------

    void DriveCurrentGhost()
    {
        var proximity = currentGhost.Proximity;

        switch (game.phase)
        {
            case FrequencyMatchGame.CapturePhase.Inactive:
            case FrequencyMatchGame.CapturePhase.TargetHaywire:
                DriveTargetPhase(proximity);
                break;

            case FrequencyMatchGame.CapturePhase.PlayerControl:
                DrivePlayerPhase(proximity);
                break;
        }
    }

    void DriveTargetPhase(GhostProximity proximity)
    {
        bool outOfRange = proximity.currentState == GhostProximity.ProximityState.OutOfRange;
        bool closeRange = proximity.currentState == GhostProximity.ProximityState.CloseRange;

        game.DriveTargetFromProximity(proximity.proximity01, closeRange, outOfRange);

        if (outOfRange)
        {
            targetWave.SetFlatline();
            if (targetValueText != null) targetValueText.text = "";
            return;
        }

        if (closeRange)
            targetWave.SetHaywire();
        else
            targetWave.SetTracking();

        targetWave.SetLiveValue(game.CurrentTargetValue);
        if (targetValueText != null)
            targetValueText.text = $"{game.CurrentTargetValue:F1}{unitSuffix}";
    }

    void DrivePlayerPhase(GhostProximity proximity)
    {
        // Leaving close range mid-attempt: reset this ghost's attempt entirely and drop
        // back to live target-tracking. Nothing is remembered across the gap — re-entering
        // close range starts a brand new haywire target.
        if (proximity.currentState != GhostProximity.ProximityState.CloseRange)
        {
            currentGhost.NotifyRemoteClosed(); // resume its proximity audio, attempt is over
            game.RevertToTracking();
            playerWave.SetFlatline();
            playerHasScrolled = false;
            if (playerValueText != null) playerValueText.text = "";
            return;
        }

        float scroll = Input.GetAxis(scrollAxisName);
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            if (!playerHasScrolled)
            {
                playerHasScrolled = true;
                playerWave.SetLocked();
            }
            game.AdjustPlayerValue(scroll);
        }

        if (playerHasScrolled)
        {
            playerWave.SetValueDirect(game.playerValue);
            if (playerValueText != null)
                playerValueText.text = $"{game.playerValue:F1}{unitSuffix}";
        }

        bool timedOut = game.TickTimer(Time.deltaTime);
        if (timerText != null && game.timeRemaining > 0f)
            timerText.text = Mathf.CeilToInt(game.timeRemaining).ToString();

        if (timedOut)
            HandleTimeout();
    }

    void HandleTimeout()
    {
        targetWave.SetFlatline();
        targetWave.SetGradeColor(targetWave.redColor, instant: true);
        playerWave.SetFlatline();
        playerWave.SetGradeColor(playerWave.redColor, instant: true);
        if (targetValueText != null) targetValueText.text = "";
        if (playerValueText != null) playerValueText.text = "";
        if (timerText != null) timerText.text = "";
        RefreshButtonInteractable();

        if (matchStatusTypewriter != null)
            matchStatusTypewriter.SetInstant(timeoutMessage);

        currentGhost?.FirePlayerCaptured(game.playerValue);

        float delta = Mathf.Abs(game.targetValue - game.playerValue);
        if (currentGhost != null)
            StartCoroutine(FireDelayedGhostEvent(currentGhost, FrequencyMatchGame.MatchGrade.Red, delta));

        currentGhost?.NotifyRemoteClosed();
    }

    void RefreshButtonInteractable()
    {
        if (captureButton != null)
            captureButton.interactable = game.CanCaptureTarget;
        if (matchButton != null)
            matchButton.interactable = game.CanMatch;
    }

    // ---------------------------------------------------------------

    void OnCaptureButtonPressed()
    {
        if (!isOpen || resolving || currentGhost == null) return;
        if (!game.CanCaptureTarget) return;

        currentGhost.NotifyMinigameOpened();
        game.CaptureTarget();
        currentGhost.FireTargetCaptured(game.targetValue);

        targetWave.SetLocked();
        if (targetValueText != null)
            targetValueText.text = $"{game.targetValue:F1}{unitSuffix}";

        playerWave.SetFlatline();
        playerHasScrolled = false;
        if (playerValueText != null) playerValueText.text = "";

        RefreshButtonInteractable();
    }

    void OnMatchButtonPressed()
    {
        if (!isOpen || resolving || currentGhost == null) return;
        if (!game.CanMatch) return;

        resolving = true;
        if (matchButton != null) matchButton.interactable = false;
        if (captureButton != null) captureButton.interactable = false;

        if (matchStatusTypewriter != null)
            matchStatusTypewriter.Type(matchingMessage, () => StartCoroutine(ResolveAfterDelay()));
        else
            StartCoroutine(ResolveAfterDelay());
    }

    System.Collections.IEnumerator ResolveAfterDelay()
    {
        yield return new WaitForSeconds(matchingHoldSeconds);

        game.ResolveMatch();
        currentGhost?.FirePlayerCaptured(game.playerValue);
        ShowResultMessage(game.lastGrade);

        // Capture references NOW — currentGhost gets nulled out by CloseRemoteAfterResolution
        // below, but the per-ghost event fires on its own independent timer and may need to
        // run after that close has already happened.
        var resolvedGhost = currentGhost;
        var resolvedGrade = game.lastGrade;
        float resolvedDelta = Mathf.Abs(game.targetValue - game.playerValue);

        if (resolvedGhost != null)
            StartCoroutine(FireDelayedGhostEvent(resolvedGhost, resolvedGrade, resolvedDelta));

        yield return new WaitForSeconds(resultHoldBeforeCloseSeconds);

        bool wasGreen = resolvedGrade == FrequencyMatchGame.MatchGrade.Green;
        CloseRemoteAfterResolution(wasGreen);
    }

    /// <summary>Fires a specific ghost's per-ghost result event after THAT ghost's own difficulty.eventFireDelay.</summary>
    System.Collections.IEnumerator FireDelayedGhostEvent(GhostController ghost, FrequencyMatchGame.MatchGrade grade, float delta)
    {
        float delay = ghost != null && ghost.difficulty != null ? ghost.difficulty.eventFireDelay : 0f;
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (ghost != null) // ghost could have been destroyed/despawned during the delay
            ghost.FireResultEvent(grade, delta);
    }

    void ShowResultMessage(FrequencyMatchGame.MatchGrade grade)
    {
        string msg = grade switch
        {
            FrequencyMatchGame.MatchGrade.Green => greenMessage,
            FrequencyMatchGame.MatchGrade.Yellow => yellowMessage,
            FrequencyMatchGame.MatchGrade.Red => redMessage,
            _ => ""
        };

        Color c = grade switch
        {
            FrequencyMatchGame.MatchGrade.Green => targetWave.greenColor,
            FrequencyMatchGame.MatchGrade.Yellow => targetWave.yellowColor,
            FrequencyMatchGame.MatchGrade.Red => targetWave.redColor,
            _ => Color.white
        };

        playerWave.SetGradeColor(c);
        targetWave.SetGradeColor(c);

        if (matchStatusTypewriter != null)
            matchStatusTypewriter.SetInstant(msg);
    }
}