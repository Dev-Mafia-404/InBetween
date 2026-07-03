using UnityEngine;

/// <summary>
/// Attach to the player. Broadcasts noise the Enemy listens for while Searching. Movement
/// noise (walk/sprint) is detected automatically each frame from how far the player moved;
/// sprint detection uses the legacy Input Manager by default, or you can drive it from your
/// own movement controller via SetSprinting(bool) (recommended — avoids double key-reading).
/// </summary>
public class EnemyNoiseSource : MonoBehaviour
{
    [Header("Sprint Detection")]
    [Tooltip("If your movement script already tracks sprint, call SetSprinting(bool) and turn this off.")]
    public bool readSprintKeyHere = true;
    public KeyCode sprintKey = KeyCode.LeftShift;

    [Header("How Far Noise Reaches")]
    [Tooltip("Noise reach while sprinting.")]
    public float sprintNoiseReach = 18f;
    [Tooltip("Noise reach while walking.")]
    public float walkNoiseReach = 8f;
    [Tooltip("Below this move speed the player counts as still (no noise).")]
    public float stillSpeedThreshold = 0.1f;

    [Header("Event Noise Reach")]
    public float interactNoiseReach = 10f;
    public float doorNoiseReach = 14f;

    Vector3 lastPosition;
    bool externallySetSprinting;
    bool useExternalSprintFlag;

    public delegate void NoiseEvent(Vector3 position, float reach);
    /// <summary>Fired whenever the player makes noise. The Enemy subscribes during Searching.</summary>
    public event NoiseEvent OnNoiseMade;

    void Start()
    {
        lastPosition = transform.position;
    }

    void Update()
    {
        float speed = (transform.position - lastPosition).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPosition = transform.position;

        if (speed < stillSpeedThreshold)
            return; // standing still — no movement noise

        bool sprinting = useExternalSprintFlag ? externallySetSprinting
                        : readSprintKeyHere && Input.GetKey(sprintKey);

        float reach = sprinting ? sprintNoiseReach : walkNoiseReach;
        OnNoiseMade?.Invoke(transform.position, reach);
    }

    /// <summary>Call from your movement controller if it already tracks sprint state.</summary>
    public void SetSprinting(bool sprinting)
    {
        useExternalSprintFlag = true;
        externallySetSprinting = sprinting;
    }

    /// <summary>Call when the player interacts with any object.</summary>
    public void ReportInteraction()
    {
        OnNoiseMade?.Invoke(transform.position, interactNoiseReach);
    }

    /// <summary>Call when the player opens or closes a door.</summary>
    public void ReportDoorUse()
    {
        OnNoiseMade?.Invoke(transform.position, doorNoiseReach);
    }

    /// <summary>Call with any custom noise event (e.g. knocking something over) at a specific reach.</summary>
    public void ReportCustomNoise(float reach)
    {
        OnNoiseMade?.Invoke(transform.position, reach);
    }
}
