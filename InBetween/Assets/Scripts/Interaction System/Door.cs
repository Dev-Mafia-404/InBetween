using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class Door : Interactable
{
    [Header("Player Reference")]
    [SerializeField] private Transform playerReference;

    [Header("Door Settings")]
    [SerializeField] private float rotationSpeed = 0.5f;
    [SerializeField] private float interactionDistance = 2f;

    [Header("Door Open Points")]
    [SerializeField] private Transform insidePoint;
    [SerializeField] private float insideOpenAngle = 0f;

    [SerializeField] private Transform outsidePoint;
    [SerializeField] private float outsideOpenAngle = 180f;

    [SerializeField] private bool isOpen;

    [Header("Door Events")]
    [SerializeField] private UnityEvent onDoorToggled;

    private Quaternion startRotation;
    private Quaternion targetRotation;
    private Quaternion closedRotation;

    private bool isRotating;
    private float rotationTimer;

    private Collider doorCollider;
    private Collider playerCollider;

    private void Start()
    {
        // Remember the rotation you placed in the Inspector.
        closedRotation = transform.localRotation;
        targetRotation = closedRotation;

        // Cache colliders
        doorCollider = GetComponent<Collider>();
        if (playerReference != null)
            playerCollider = playerReference.GetComponent<Collider>();

        if (playerReference == null)
            Debug.LogWarning($"{name}: Player Reference is not assigned in the Inspector!");
    }

    private void Update()
    {
        if (!isRotating)
            return;

        rotationTimer += Time.deltaTime;

        float t = Mathf.Clamp01(rotationTimer / rotationSpeed);

        transform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);

        if (t >= 1f)
        {
            transform.localRotation = targetRotation;
            isRotating = false;
        }
    }

    public override void OnInteract(PlayerInteractor interactor)
    {
        if (!CanInteract)
            return;

        Debug.Log($"Interacted with {name}");

        if (isOpen)
            CloseDoor();
        else
            OpenDoor(interactor);

        onDoorToggled?.Invoke();

        base.OnInteract(interactor);
    }

    public bool IsPlayerInRange()
    {
        if (playerReference == null)
            return false;

        float distance = GetClosestDistance(playerReference.position);
        return distance <= interactionDistance;
    }

    private float GetClosestDistance(Vector3 playerPos)
    {
        // If both colliders exist, calculate distance to closest points
        if (doorCollider != null && playerCollider != null)
        {
            Vector3 doorClosestPoint = doorCollider.ClosestPoint(playerPos);
            Vector3 playerClosestPoint = playerCollider.ClosestPoint(transform.position);
            return Vector3.Distance(doorClosestPoint, playerClosestPoint);
        }

        // Fallback: simple distance between transforms
        return Vector3.Distance(playerReference.position, transform.position);
    }

    private void OpenDoor(PlayerInteractor interactor)
    {
        OpenAwayFrom(playerReference.position);
    }

    /// <summary>
    /// Opens the door swinging away from whoever is approaching. Used by both the player
    /// (via OnInteract) and the Enemy (via OpenForEnemy). Safe to call when already open.
    /// </summary>
    private void OpenAwayFrom(Vector3 approacherPosition)
    {
        if (insidePoint == null || outsidePoint == null)
        {
            Debug.LogError($"{name}: InsidePoint or OutsidePoint is missing!");
            return;
        }

        float insideDistance = Vector3.SqrMagnitude(approacherPosition - insidePoint.position);
        float outsideDistance = Vector3.SqrMagnitude(approacherPosition - outsidePoint.position);

        float targetAngle = insideDistance < outsideDistance ? insideOpenAngle : outsideOpenAngle;

        RotateTo(targetAngle);
        isOpen = true;
    }

    /// <summary>
    /// Called by the Enemy when it needs to push through a closed door. Swings the door open
    /// away from the Enemy and fires the same toggle event a player interaction would.
    /// Does nothing if already open.
    /// </summary>
    public void OpenForEnemy(Vector3 enemyPosition)
    {
        if (isOpen) return;
        OpenAwayFrom(enemyPosition);
        onDoorToggled?.Invoke();
    }

    private void CloseDoor()
    {
        startRotation = transform.localRotation;
        targetRotation = closedRotation;

        rotationTimer = 0f;
        isRotating = true;

        isOpen = false;
    }

    private void RotateTo(float yAngle)
    {
        startRotation = transform.localRotation;

        targetRotation = Quaternion.Euler(
            closedRotation.eulerAngles.x,
            yAngle,
            closedRotation.eulerAngles.z);

        rotationTimer = 0f;
        isRotating = true;
    }

    public bool IsOpen => isOpen;
}