using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The "brain" of the frequency match minigame. All tuning values come directly from
/// whichever GhostMatchDifficulty is active — nothing is duplicated as Inspector fields
/// here, since ApplyDifficulty is always called before any session begins.
/// </summary>
public class FrequencyMatchGame : MonoBehaviour
{
    public enum MatchGrade { None, Green, Yellow, Red }
    public enum CapturePhase { Inactive, TargetHaywire, PlayerControl, Resolved, TimedOut }

    [Header("Runtime State (read-only, watch during play for debugging)")]
    public CapturePhase phase = CapturePhase.Inactive;
    public float targetValue;
    public float playerValue;
    public MatchGrade lastGrade = MatchGrade.None;
    public float timeRemaining;

    [Header("Global Events (fire for ANY ghost — use GhostController's per-ghost events for ghost-specific logic)")]
    public UnityEvent<float> OnTargetCaptured;
    public UnityEvent<float> OnPlayerCaptured;
    public UnityEvent<float> OnGreenMatch;
    public UnityEvent<float> OnYellowMatch;
    public UnityEvent<float> OnRedMatch;

    // All tuning read directly from the active difficulty profile — not copied into
    // public fields here, because those copies were the source of the Inspector bloat.
    GhostMatchDifficulty profile;

    float liveValue;
    float jitterTimer;
    float jitterFrom, jitterTo;
    bool haywireEligible;

    // Shorthand readers — keeps call sites clean while reading from one source of truth.
    float MinFreq => profile != null ? profile.minFrequency : 0f;
    float MaxFreq => profile != null ? profile.maxFrequency : 100f;

    public float CurrentTargetValue => liveValue;
    public bool CanCaptureTarget => phase == CapturePhase.TargetHaywire && haywireEligible;
    public bool CanAdjustPlayer => phase == CapturePhase.PlayerControl;
    public bool CanMatch => phase == CapturePhase.PlayerControl;

    public float Normalize(float rawValue)
    {
        float min = MinFreq, max = MaxFreq;
        return max > min ? Mathf.InverseLerp(min, max, rawValue) : 0f;
    }

    /// <summary>Set the active difficulty profile. Always call this before OpenRemote.</summary>
    public void ApplyDifficulty(GhostMatchDifficulty newProfile)
    {
        if (newProfile == null)
        {
            Debug.LogWarning("[FrequencyMatchGame] ApplyDifficulty called with null profile — assign a GhostMatchDifficulty to the ghost's GhostController.");
            return;
        }
        profile = newProfile;
    }

    /// <summary>Call every time the remote opens for a ghost (or switches to a new closest one). Resets state to Inactive.</summary>
    public void OpenRemote()
    {
        phase = CapturePhase.Inactive;
        targetValue = MinFreq;
        playerValue = MinFreq;
        liveValue = MinFreq;
        lastGrade = MatchGrade.None;
        haywireEligible = false;
    }

    /// <summary>
    /// Feed this every frame while phase == Inactive or TargetHaywire (before target capture).
    /// proximity01: 0 = out of range, 1 = at close-range edge. eligible: true once in close range.
    /// </summary>
    public void DriveTargetFromProximity(float proximity01, bool eligible, bool outOfRange)
    {
        if (phase != CapturePhase.Inactive && phase != CapturePhase.TargetHaywire) return;

        if (outOfRange)
        {
            phase = CapturePhase.Inactive;
            liveValue = MinFreq;
            haywireEligible = false;
            return;
        }

        haywireEligible = eligible;
        phase = CapturePhase.TargetHaywire;

        if (eligible)
        {
            float jitterSpeed = profile != null ? profile.targetJitterSpeed : 6f;
            jitterTimer += Time.deltaTime * jitterSpeed;
            if (jitterTimer >= 1f)
            {
                jitterFrom = liveValue;
                jitterTo = Random.Range(MinFreq, MaxFreq);
                jitterTimer = 0f;
            }
            liveValue = Mathf.SmoothStep(jitterFrom, jitterTo, Mathf.Clamp01(jitterTimer));
        }
        else
        {
            liveValue = Mathf.Lerp(MinFreq, MaxFreq, proximity01);
            jitterFrom = jitterTo = liveValue;
            jitterTimer = 0f;
        }
    }

    /// <summary>Call on Capture input. Only succeeds if CanCaptureTarget is true (close range reached).</summary>
    public void CaptureTarget()
    {
        if (!CanCaptureTarget) return;

        targetValue = liveValue;
        OnTargetCaptured?.Invoke(targetValue);

        phase = CapturePhase.PlayerControl;
        playerValue = MinFreq;
        timeRemaining = profile != null ? profile.timerSeconds : 10f;
    }

    /// <summary>Call from mouse scroll delta. Positive = up = increase.</summary>
    public void AdjustPlayerValue(float scrollDelta)
    {
        if (!CanAdjustPlayer) return;
        float sensitivity = profile != null ? profile.scrollSensitivity : 30f;
        playerValue = Mathf.Clamp(playerValue + scrollDelta * sensitivity, MinFreq, MaxFreq);
    }

    /// <summary>Call once per frame while CanAdjustPlayer, to tick the timer if enabled. Returns true if timeout just occurred.</summary>
    public bool TickTimer(float deltaTime)
    {
        bool timerEnabled = profile != null && profile.useTimer;
        if (!CanAdjustPlayer || !timerEnabled) return false;

        timeRemaining -= deltaTime;
        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            OnPlayerCaptured?.Invoke(playerValue);
            lastGrade = MatchGrade.Red;
            phase = CapturePhase.TimedOut;
            Debug.Log($"[FrequencyMatch] TIMEOUT — RED. Target={targetValue:F1} Player={playerValue:F1}");
            OnRedMatch?.Invoke(Mathf.Abs(targetValue - playerValue));
            return true;
        }
        return false;
    }

    /// <summary>Call when the player presses the Match button.</summary>
    public void ResolveMatch()
    {
        if (phase != CapturePhase.PlayerControl) return;

        OnPlayerCaptured?.Invoke(playerValue);
        phase = CapturePhase.Resolved;

        float delta = Mathf.Abs(targetValue - playerValue);
        float green = profile != null ? profile.greenThreshold : 5f;
        float yellow = profile != null ? profile.yellowThreshold : 10f;

        if (delta <= green)
        {
            lastGrade = MatchGrade.Green;
            Debug.Log($"[FrequencyMatch] GREEN. Target={targetValue:F1} Player={playerValue:F1} Delta={delta:F1}");
            OnGreenMatch?.Invoke(delta);
        }
        else if (delta <= yellow)
        {
            lastGrade = MatchGrade.Yellow;
            Debug.Log($"[FrequencyMatch] YELLOW. Target={targetValue:F1} Player={playerValue:F1} Delta={delta:F1}");
            OnYellowMatch?.Invoke(delta);
        }
        else
        {
            lastGrade = MatchGrade.Red;
            Debug.Log($"[FrequencyMatch] RED. Target={targetValue:F1} Player={playerValue:F1} Delta={delta:F1}");
            OnRedMatch?.Invoke(delta);
        }
    }

    /// <summary>Drops back to live target-tracking — re-entering close range starts a fresh haywire target.</summary>
    public void RevertToTracking()
    {
        if (phase != CapturePhase.PlayerControl) return;
        phase = CapturePhase.Inactive;
        haywireEligible = false;
    }

    /// <summary>Full reset — call when the remote closes or the attempt is abandoned.</summary>
    public void CancelAttempt()
    {
        phase = CapturePhase.Inactive;
        liveValue = MinFreq;
        haywireEligible = false;
    }
}