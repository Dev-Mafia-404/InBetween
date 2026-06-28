using UnityEngine;

/// <summary>
/// Tracks distance between player and a ghost. Drives a looping audio cue's volume
/// based on proximity, AND exposes a continuous 0-100 "proximity frequency" value
/// that rises as the player gets closer (used to drive the target wave's number/visual
/// before it's captured). PC-only: no vibration/haptics.
/// </summary>
public class GhostProximity : MonoBehaviour
{
    public enum ProximityState { OutOfRange, Approaching, CloseRange }

    [Header("References")]
    public Transform player;
    [Tooltip("If left empty, this GameObject's transform is used as the ghost position.")]
    public Transform ghost;

    [Header("Audio")]
    public AudioSource proximityAudio;
    public float volumeFalloffPower = 1.5f;
    public float volumeLerpSpeed = 2f;

    [Header("State (read-only, for debugging)")]
    public ProximityState currentState = ProximityState.OutOfRange;
    [Range(0f, 1f)] public float proximity01; // 0 = at maxDetectionRange edge, 1 = at closeRangeDistance edge

    // Always set via ApplyDifficulty from the owning ghost's GhostMatchDifficulty —
    // not public Inspector fields because editing them here directly would just get
    // overwritten on Awake, same reasoning as FrequencyWaveUI's tuning fields.
    float maxDetectionRange = 15f;
    float closeRangeDistance = 4f;
    float minVolume = 0.3f;
    float maxVolume = 1f;

    /// <summary>True while the minigame's capture/match flow is actively running for this ghost.</summary>
    public bool MinigameActive { get; private set; }

    Transform GhostTransform => ghost != null ? ghost : transform;

    /// <summary>0-100 value representing how "hot" the signal is based on raw distance alone (no haywire/jitter applied).</summary>
    public float ProximityFrequency01to100 => proximity01 * 100f;

    public void ApplyDifficulty(GhostMatchDifficulty profile)
    {
        if (profile == null) return;
        maxDetectionRange = profile.maxDetectionRange;
        closeRangeDistance = profile.closeRangeDistance;
        minVolume = profile.minVolume;
        maxVolume = profile.maxVolume;
    }

    void Update()
    {
        UpdateProximity();
        HandleAudio();
    }

    void UpdateProximity()
    {
        if (player == null) return;

        float dist = Vector3.Distance(player.position, GhostTransform.position);

        if (dist > maxDetectionRange)
        {
            currentState = ProximityState.OutOfRange;
            proximity01 = 0f;
        }
        else if (dist <= closeRangeDistance)
        {
            currentState = ProximityState.CloseRange;
            proximity01 = 1f;
        }
        else
        {
            currentState = ProximityState.Approaching;
            float t = 1f - Mathf.InverseLerp(closeRangeDistance, maxDetectionRange, dist);
            proximity01 = Mathf.Clamp01(t);
        }
    }

    void HandleAudio()
    {
        if (proximityAudio == null) return;

        bool shouldBeSilent = MinigameActive || currentState == ProximityState.OutOfRange;

        float shapedProximity = Mathf.Pow(proximity01, volumeFalloffPower);
        float targetVolume = shouldBeSilent ? 0f : Mathf.Lerp(minVolume, maxVolume, shapedProximity);

        proximityAudio.volume = Mathf.MoveTowards(proximityAudio.volume, targetVolume, Time.deltaTime * volumeLerpSpeed);

        if (!shouldBeSilent && !proximityAudio.isPlaying)
            proximityAudio.Play();
        else if (shouldBeSilent && proximityAudio.volume <= 0.001f && proximityAudio.isPlaying)
            proximityAudio.Stop();
    }

    /// <summary>Call when the capture/match flow is actively engaged (haywire captured, attempting match) so audio mutes correctly.</summary>
    public void SetMinigameActive(bool active)
    {
        MinigameActive = active;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.25f);
        Gizmos.DrawWireSphere(GhostTransform.position, maxDetectionRange);
        Gizmos.color = new Color(0f, 1f, 0.3f, 0.4f);
        Gizmos.DrawWireSphere(GhostTransform.position, closeRangeDistance);
    }
#endif
}