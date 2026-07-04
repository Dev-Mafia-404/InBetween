using UnityEngine;

public class LookAwaySwap : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public GameObject originalObject;
    public GameObject swappedObject;
    public AudioSource scareAudio;

    [Header("Timing")]
    [Tooltip("Delay before the scare sound plays.")]
    public float audioDelay = 1.5f;

    [Tooltip("Delay after the sound before the object can swap.")]
    public float swapDelayAfterAudio = 0.5f;

    [Header("View Settings")]
    [Tooltip("How far the player must look away before the swap happens.")]
    [Range(90f, 180f)]
    public float requiredLookAwayAngle = 130f;

    private bool eventStarted = false;
    private bool audioPlayed = false;

    private float triggerTime;
    private float audioPlayTime;

    private void Start()
    {
        if (swappedObject != null)
            swappedObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (eventStarted) return;

        eventStarted = true;
        triggerTime = Time.time;

        Debug.Log("HORROR EVENT STARTED");
    }

    private void Update()
    {
        if (!eventStarted) return;

        // Play scare sound after delay
        if (!audioPlayed && Time.time >= triggerTime + audioDelay)
        {
            if (scareAudio != null)
                scareAudio.Play();

            audioPlayed = true;
            audioPlayTime = Time.time;
        }

        // Wait until audio has played
        if (!audioPlayed) return;

        // Check if player has looked away
        Vector3 toObject =
            (originalObject.transform.position - playerCamera.transform.position).normalized;

        float angle =
            Vector3.Angle(playerCamera.transform.forward, toObject);

        // Swap only when player has looked away enough
        // and the swap delay has passed
        if (angle > requiredLookAwayAngle &&
            Time.time >= audioPlayTime + swapDelayAfterAudio)
        {
            if (originalObject != null)
                originalObject.SetActive(false);

            if (swappedObject != null)
                swappedObject.SetActive(true);

            Debug.Log("OBJECT SWAPPED");

            eventStarted = false;
            enabled = false; // One-time use
        }
    }
}