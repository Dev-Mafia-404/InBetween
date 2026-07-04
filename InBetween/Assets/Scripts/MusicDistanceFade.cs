using UnityEngine;

/// <summary>
/// Fades an AudioSource's volume based on the player's position along a path
/// defined by three points: A, B, and C.
///
///   A ---------------- B ---------------- C
///   |--- 100% volume ---|--- fades to 0% --|
///
/// - Between A and B: volume stays at 100%.
/// - Between B and C: volume fades linearly from 100% to 0%.
/// - Before A: volume is clamped to 100%.
/// - After C: volume is clamped to 0%.
///
/// The player's position is projected onto the line A->C, so this works
/// even if the player strays slightly off the direct line (e.g. side to side
/// in a corridor), as long as the corridor mainly runs from A to C.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MusicDistanceFade : MonoBehaviour
{
    [Header("Spawn Points")]
    [Tooltip("Start of the corridor. Volume is 100% at and before this point.")]
    public Transform pointA;

    [Tooltip("End of the full-volume zone. Fading begins after this point.")]
    public Transform pointB;

    [Tooltip("End of the corridor. Volume reaches 0% at and after this point.")]
    public Transform pointC;

    [Header("Audio")]
    [Tooltip("Audio source to fade. If left empty, uses the AudioSource on this GameObject.")]
    public AudioSource audioSource;

    [Tooltip("The maximum volume used for the 100% level (usually 1).")]
    [Range(0f, 1f)]
    public float maxVolume = 1f;

    [Header("Player")]
    [Tooltip("The player transform to track. If left empty, will try to find an object tagged 'Player'.")]
    public Transform player;

    [Header("Fade Curve (optional)")]
    [Tooltip("Optional curve to shape the fade between B and C. Leave default (linear 0-1) for a straight linear fade.")]
    public AnimationCurve fadeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }
    }

    private void Update()
    {
        if (player == null || pointA == null || pointB == null || pointC == null || audioSource == null)
            return;

        audioSource.volume = CalculateVolume(player.position);
    }

    /// <summary>
    /// Calculates the target volume (0-1, scaled by maxVolume) for a given world position.
    /// </summary>
    private float CalculateVolume(Vector3 worldPosition)
    {
        // Direction and total length of the corridor (A -> C).
        Vector3 abVector = pointC.position - pointA.position;
        float totalLength = abVector.magnitude;

        if (totalLength <= 0.0001f)
            return maxVolume; // Avoid divide-by-zero if points overlap.

        Vector3 direction = abVector / totalLength;

        // Project the player's position onto the A->C line to get distance along it.
        Vector3 toPlayer = worldPosition - pointA.position;
        float distanceAlongPath = Vector3.Dot(toPlayer, direction);

        // Distance from A to B, projected the same way, to know where the fade starts.
        float distanceAB = Vector3.Dot(pointB.position - pointA.position, direction);

        // Before or at B: full volume.
        if (distanceAlongPath <= distanceAB)
            return maxVolume;

        // At or after C: silence.
        if (distanceAlongPath >= totalLength)
            return 0f;

        // Between B and C: fade using the curve.
        float fadeZoneLength = totalLength - distanceAB;
        float t = (distanceAlongPath - distanceAB) / fadeZoneLength; // 0 at B, 1 at C
        float curveValue = fadeCurve.Evaluate(t);

        return curveValue * maxVolume;
    }

    // Draws the corridor and points in the Scene view for easy setup.
    private void OnDrawGizmosSelected()
    {
        if (pointA != null && pointB != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(pointA.position, pointB.position);
        }

        if (pointB != null && pointC != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pointB.position, pointC.position);
        }

        if (pointA != null) Gizmos.DrawSphere(pointA.position, 0.2f);
        if (pointB != null) Gizmos.DrawSphere(pointB.position, 0.2f);
        if (pointC != null) Gizmos.DrawSphere(pointC.position, 0.2f);
    }
}