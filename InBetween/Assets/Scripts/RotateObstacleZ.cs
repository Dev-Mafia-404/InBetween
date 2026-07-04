using UnityEngine;

public class RotateObstacleZ : MonoBehaviour
{
    [Tooltip("Degrees Per Second")]
    public float rotationSpeed = 180f;

    [Tooltip("Starting Rotation on Z Axis")]
    public float rotationOffset = 0f;

    void Start()
    {
        Vector3 currentRotation = transform.localEulerAngles;

        transform.localRotation = Quaternion.Euler(
            currentRotation.x,
            currentRotation.y,
            currentRotation.z + rotationOffset
        );
    }

    void Update()
    {
        transform.Rotate(
            0f,
            0f,
            rotationSpeed * Time.deltaTime,
            Space.Self
        );
    }
}