using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PlayerHide : Interactable
{
    [Header("References")]
    [SerializeField] private GameObject player;

    [SerializeField] private Transform insidePoint;
    [SerializeField] private Transform outsidePoint;

    [Header("Doors")]
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;

    [Header("Left Door Rotations")]
    [SerializeField] private Vector3 leftDoorOpenRotation;
    [SerializeField] private Vector3 leftDoorClosedRotation;

    [Header("Right Door Rotations")]
    [SerializeField] private Vector3 rightDoorOpenRotation;
    [SerializeField] private Vector3 rightDoorClosedRotation;

    [Header("Animation")]
    [SerializeField] private float animationDuration = 0.5f;

    [Header("Events")]
    [SerializeField] private UnityEvent OnHideEnter;
    [SerializeField] private UnityEvent OnHideExit;

    private CharacterController playerController;
    private Rigidbody playerRigidbody;

    private bool playerInside;
    private bool animating;
    

 
 
    private void Start()
    {
        if (player != null)
        {
            playerController = player.GetComponent<CharacterController>();
            playerRigidbody = player.GetComponent<Rigidbody>();
        }
    }

    public override void OnInteract(PlayerInteractor interactor)
    {
        base.OnInteract(interactor);

        if (animating)
            return;

        StartCoroutine(HideRoutine());
    }

    private IEnumerator HideRoutine()
    {
        animating = true;

        // Open doors
        yield return AnimateDoors(leftDoorOpenRotation, rightDoorOpenRotation);

        // Teleport player
        Transform target = playerInside ? outsidePoint : insidePoint;
        TeleportPlayer(target.position, target.rotation);

        if (!playerInside)
        {
            playerInside = true;
            OnHideEnter?.Invoke();
        }
        else
        {
            playerInside = false;
            OnHideExit?.Invoke();
        }

        // Close doors
        yield return AnimateDoors(leftDoorClosedRotation, rightDoorClosedRotation);

        animating = false;
    }

    //public new void OnFocusEnter(PlayerInteractor interactor)
    //{
    //    if (Sillouet != null)
    //        Sillouet.enabled = true;

 
    //}

    //public new void OnFocusExit(PlayerInteractor interactor)
    //{
    //    if (Sillouet != null)
    //        Sillouet.enabled = false;

      
    //}
   

private void TeleportPlayer(Vector3 position, Quaternion rotation)
    {
        if (playerController != null)
            playerController.enabled = false;

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        player.transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();

        if (playerRigidbody != null)
        {
            playerRigidbody.position = position;
            playerRigidbody.rotation = rotation;
        }

        if (playerController != null)
            playerController.enabled = true;
    }

    private IEnumerator AnimateDoors(Vector3 leftTargetRotation, Vector3 rightTargetRotation)
    {
        Quaternion leftStart = leftDoor.localRotation;
        Quaternion rightStart = rightDoor.localRotation;

        Quaternion leftTarget = Quaternion.Euler(leftTargetRotation);
        Quaternion rightTarget = Quaternion.Euler(rightTargetRotation);

        float timer = 0f;

        while (timer < animationDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / animationDuration);

            leftDoor.localRotation = Quaternion.Slerp(leftStart, leftTarget, t);
            rightDoor.localRotation = Quaternion.Slerp(rightStart, rightTarget, t);

            yield return null;
        }

        leftDoor.localRotation = leftTarget;
        rightDoor.localRotation = rightTarget;
    }
}