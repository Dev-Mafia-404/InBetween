using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class Door : Interactable
{
    [Header("Door Settings")]
    [SerializeField] private float rotationSpeed = 0.5f;

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

    private void Start()
    {
        // Remember the rotation you placed in the Inspector.
        closedRotation = transform.localRotation;
        targetRotation = closedRotation;

        
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

    private void OpenDoor(PlayerInteractor interactor)
    {
        if (insidePoint == null || outsidePoint == null)
        {
            Debug.LogError($"{name}: InsidePoint or OutsidePoint is missing!");
            return;
        }

        float insideDistance =
            Vector3.SqrMagnitude(interactor.transform.position - insidePoint.position);

        float outsideDistance =
            Vector3.SqrMagnitude(interactor.transform.position - outsidePoint.position);

      

        float targetAngle;

        if (insideDistance < outsideDistance)
        {
           
            targetAngle = insideOpenAngle;
        }
        else
        {
            
            targetAngle = outsideOpenAngle;
        }

        

        RotateTo(targetAngle);

        isOpen = true;
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