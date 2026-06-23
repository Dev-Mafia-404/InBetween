using UnityEngine;

/// <summary>
/// Defines how hard the frequency match minigame is for a given ghost type.
/// Create one asset per ghost (Right-click in Project → Create → Ghosts → Match Difficulty)
/// and assign it to that ghost's GhostController.
/// </summary>
[CreateAssetMenu(fileName = "NewGhostDifficulty", menuName = "Ghosts/Match Difficulty")]
public class GhostMatchDifficulty : ScriptableObject
{
    [Header("Identity (for your own reference)")]
    public string ghostTypeName = "Wisp";

    [Header("Frequency Range (this ghost's signal band — e.g. 200-400Hz vs 50-90Hz)")]
    [Tooltip("Lowest possible frequency value this ghost can roll.")]
    public float minFrequency = 0f;
    [Tooltip("Highest possible frequency value this ghost can roll.")]
    public float maxFrequency = 100f;

    [Header("Match Tolerance — fixed absolute Hz delta, independent of range size")]
    [Tooltip("Delta <= this = green (perfect)")]
    public float greenThreshold = 5f;
    [Tooltip("Delta <= this = yellow (close). Above = red.")]
    public float yellowThreshold = 10f;

    [Header("Target Wave — Haywire Phase (only active in close range, pre-capture)")]
    [Tooltip("How fast the target's number jumps around while haywire (jumps/sec).")]
    public float targetJitterSpeed = 6f;
    [Tooltip("How wild the target's amplitude/frequency swings are while haywire (0=calm, 1=full chaos).")]
    [Range(0f, 1f)] public float targetHaywireIntensity = 0.8f;

    [Header("Wave Visual Feel (applies once locked/steady, and to player wave)")]
    public float waveBaseFrequency = 3f;
    public float waveScrollSpeed = 4f;

    [Header("Player Wave — Scroll Control")]
    [Tooltip("How much frequency changes (in Hz) per scroll-wheel notch. Input.GetAxis returns ~0.1 per notch — scale this relative to your frequency range size.")]
    public float scrollSensitivity = 30f;

    [Header("Timer (harder ghosts only)")]
    public bool useTimer = false;
    [Tooltip("Seconds the player has, from target capture, to complete the match before it auto-fails.")]
    public float timerSeconds = 10f;

    [Header("Per-Ghost Event Timing")]
    [Tooltip("Delay (seconds) between this ghost's match resolving and its OnThisGhostGreen/Yellow/Red event actually firing.")]
    public float eventFireDelay = 0f;

    [Header("Physical Detection Range (world-space distance, NOT frequency)")]
    public float maxDetectionRange = 15f;
    [Tooltip("Within this distance, target wave goes haywire and becomes capturable.")]
    public float closeRangeDistance = 4f;

    [Header("Proximity Audio")]
    [Range(0f, 1f)] public float minVolume = 0.3f;
    [Range(0f, 1f)] public float maxVolume = 1f;
}