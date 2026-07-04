using UnityEngine;
using System.Collections;

public class MoveOnZAxis : MonoBehaviour
{
    [Header("Points")]
    public Transform initialPoint;
    public Transform finalPoint;

    [Header("Movement")]
    public float duration = 3f;

    [Header("Offsets")]
    public float startOffset = 0f;
    public float loopOffset = 0f;

    [Header("Looping")]
    public bool loop = true;
    public bool pingPong = true;

    [Header("Easing")]
    public AnimationCurve movementCurve =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    private void Start()
    {
        StartCoroutine(MoveRoutine());
    }

    private IEnumerator MoveRoutine()
    {
        if (startOffset > 0)
            yield return new WaitForSeconds(startOffset);

        bool forward = true;

        while (true)
        {
            Vector3 startPos = forward
                ? initialPoint.position
                : finalPoint.position;

            Vector3 endPos = forward
                ? finalPoint.position
                : initialPoint.position;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                float curveT = movementCurve.Evaluate(t);

                transform.position = Vector3.Lerp(
                    startPos,
                    endPos,
                    curveT
                );

                yield return null;
            }

            transform.position = endPos;

            if (!loop)
                break;

            if (loopOffset > 0)
                yield return new WaitForSeconds(loopOffset);

            if (pingPong)
                forward = !forward;
        }
    }
}