using UnityEngine;

public class Camera360Rotate : MonoBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 30f; // Degrees per second
    public bool rotateClockwise = true;
    public bool isRotating = true;

    void Update()
    {
        if (!isRotating) return;

        float direction = rotateClockwise ? 1f : -1f;

        transform.Rotate(
            Vector3.up,
            rotationSpeed * direction * Time.deltaTime,
            Space.World
        );
    }

    // Optional controls
    public void StartRotation()
    {
        isRotating = true;
    }

    public void StopRotation()
    {
        isRotating = false;
    }

    public void ToggleDirection()
    {
        rotateClockwise = !rotateClockwise;
    }
}