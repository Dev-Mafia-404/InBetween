using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Unity.Cinemachine;

public class PlayerDeath : MonoBehaviour
{
    [SerializeField] private GameObject JumpscareEnemy;
    [SerializeField] private GameObject FlashlightMesh;

    [SerializeField] private CinemachineCameraOffset _offset;

    [Header("Camera Offset Lerp")]
    [SerializeField] private float startY;
    [SerializeField] private float endY;
    [SerializeField] private float lerpDuration = 1f;

    private void OnEnable()
    {
        JumpscareEnemy.SetActive(true);
        FlashlightMesh.SetActive(false);
        LerpPlayerCameraOffset();
    }
    void LerpPlayerCameraOffset()
    {
        StartCoroutine(LerpOffsetRoutine());
    }

    private IEnumerator LerpOffsetRoutine()
    {
        float elapsed = 0f;

        Vector3 offset = _offset.Offset;
        offset.y = startY;
        _offset.Offset = offset;

        while (elapsed < lerpDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / lerpDuration);

            offset = _offset.Offset;
            offset.y = Mathf.Lerp(startY, endY, t);
            _offset.Offset = offset;

            yield return null;
        }

        offset = _offset.Offset;
        offset.y = endY;
        _offset.Offset = offset;
    }

    public void EnableEnemyCameraMesh()
    {
        JumpscareEnemy.SetActive(true);
    }

}