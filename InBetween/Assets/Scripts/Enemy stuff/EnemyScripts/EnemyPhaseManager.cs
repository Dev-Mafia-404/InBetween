using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Tracks how many souls have been resolved and works out the Enemy's current phase from
/// that ratio (so it scales correctly whether there are 5 or 8 total souls). Call
/// ReportSoulResolved() from your ghost-matching system whenever a soul passes on.
///
/// Every phase value is set DIRECTLY here (no multipliers). Enemy.cs reads these live, so
/// nothing goes stale across a phase transition.
/// </summary>
public class EnemyPhaseManager : MonoBehaviour
{
    public enum Phase { Dormant, Aware, Aggressive, Desperate, Dissolved }

    [Header("Soul Tracking")]
    [Tooltip("Total souls in this playthrough (5-8). Set before play starts.")]
    public int totalSouls = 6;
    [Tooltip("How many souls are resolved so far. Read-only at runtime, shown for debugging.")]
    public int soulsResolved = 0;

    [Header("When Phases Start (fraction of souls resolved)")]
    [Range(0f, 1f)] public float startAggressiveAt = 0.4f;
    [Range(0f, 1f)] public float startDesperateAt = 0.75f;

    // ---------------------------------------------------------------------
    // Each phase's tuning is a plain, direct value. No multiplying by anything.
    // ---------------------------------------------------------------------

    [Header("AWARE Phase (early — after 1st soul)")]
    [Tooltip("Move speed while chasing (units/second).")]
    public float awareChaseSpeed = 5.5f;
    [Tooltip("How long it searches before giving up (seconds).")]
    public float awareSearchTime = 15f;
    [Tooltip("Wait before it can appear again after you escape (seconds).")]
    public float awareCooldownAfterEscape = 180f;
    [Tooltip("Wait before it can appear again after you torch it (seconds).")]
    public float awareCooldownAfterTorch = 120f;
    [Tooltip("Can it open doors in this phase?")]
    public bool awareCanOpenDoors = false;

    [Header("AGGRESSIVE Phase (mid)")]
    public float aggressiveChaseSpeed = 6.4f;
    public float aggressiveSearchTime = 22f;
    public float aggressiveCooldownAfterEscape = 150f;
    public float aggressiveCooldownAfterTorch = 90f;
    public bool aggressiveCanOpenDoors = true;

    [Header("DESPERATE Phase (final stretch)")]
    public float desperateChaseSpeed = 7f;
    public float desperateSearchTime = 30f;
    public float desperateCooldownAfterEscape = 90f;
    public float desperateCooldownAfterTorch = 60f;
    public bool desperateCanOpenDoors = true;

    [Header("Global Limit")]
    [Tooltip("Hard minimum seconds between any two appearances, no matter the phase.")]
    public float minTimeBetweenSpawns = 45f;

    [Header("Debug")]
    public bool debugLogging = true;

    [Header("Events")]
    [Tooltip("Fires once, the moment the first soul resolves — hook your awakening sequence (audio/ambient shift) here.")]
    public UnityEvent OnAwakened;
    [Tooltip("Fires every time the phase changes, passing the new phase.")]
    public UnityEvent<Phase> OnPhaseChanged;
    [Tooltip("Fires once, when the final soul resolves — hook Enemy.ForceWithdraw(false) here.")]
    public UnityEvent OnAllSoulsResolved;

    public Phase CurrentPhase { get; private set; } = Phase.Dormant;

    /// <summary>Call this from your ghost match system whenever a soul resolves.</summary>
    [ContextMenu("DEBUG: Report Soul Resolved")]
    public void ReportSoulResolved()
    {
        int previousCount = soulsResolved;
        bool wasFirst = soulsResolved == 0;
        soulsResolved = Mathf.Min(soulsResolved + 1, totalSouls);

        Log($"ReportSoulResolved: {previousCount} -> {soulsResolved} (of {totalSouls}).");

        if (wasFirst)
        {
            CurrentPhase = Phase.Aware;
            Log("First soul resolved — phase set to Aware. Firing OnAwakened + OnPhaseChanged.");
            OnAwakened?.Invoke();
            OnPhaseChanged?.Invoke(CurrentPhase);
        }
        else
        {
            RecalculatePhase();
        }

        if (soulsResolved >= totalSouls)
        {
            CurrentPhase = Phase.Dissolved;
            Log("All souls resolved — phase set to Dissolved. Firing OnPhaseChanged + OnAllSoulsResolved.");
            OnPhaseChanged?.Invoke(CurrentPhase);
            OnAllSoulsResolved?.Invoke();
        }
    }

    void RecalculatePhase()
    {
        if (CurrentPhase == Phase.Dormant || CurrentPhase == Phase.Dissolved) return;

        float ratio = totalSouls > 0 ? (float)soulsResolved / totalSouls : 0f;
        Phase newPhase = ratio >= startDesperateAt ? Phase.Desperate
                        : ratio >= startAggressiveAt ? Phase.Aggressive
                        : Phase.Aware;

        if (newPhase != CurrentPhase)
        {
            Log($"Phase changed: {CurrentPhase} -> {newPhase} (ratio={ratio:F2}).");
            CurrentPhase = newPhase;
            OnPhaseChanged?.Invoke(CurrentPhase);
        }
    }

    void Log(string message)
    {
        if (debugLogging)
            Debug.Log($"[EnemyPhaseManager] {message}");
    }

    // ---------------------------------------------------------------------
    // Live values Enemy.cs reads. All direct — never multiplied.
    // ---------------------------------------------------------------------

    public float ChaseSpeed => CurrentPhase switch
    {
        Phase.Aware => awareChaseSpeed,
        Phase.Aggressive => aggressiveChaseSpeed,
        Phase.Desperate => desperateChaseSpeed,
        _ => awareChaseSpeed
    };

    public float SearchTime => CurrentPhase switch
    {
        Phase.Aware => awareSearchTime,
        Phase.Aggressive => aggressiveSearchTime,
        Phase.Desperate => desperateSearchTime,
        _ => awareSearchTime
    };

    public float CooldownAfterEscape => CurrentPhase switch
    {
        Phase.Aware => awareCooldownAfterEscape,
        Phase.Aggressive => aggressiveCooldownAfterEscape,
        Phase.Desperate => desperateCooldownAfterEscape,
        _ => awareCooldownAfterEscape
    };

    public float CooldownAfterTorch => CurrentPhase switch
    {
        Phase.Aware => awareCooldownAfterTorch,
        Phase.Aggressive => aggressiveCooldownAfterTorch,
        Phase.Desperate => desperateCooldownAfterTorch,
        _ => awareCooldownAfterTorch
    };

    public bool CanOpenDoors => CurrentPhase switch
    {
        Phase.Aware => awareCanOpenDoors,
        Phase.Aggressive => aggressiveCanOpenDoors,
        Phase.Desperate => desperateCanOpenDoors,
        _ => false
    };
}
