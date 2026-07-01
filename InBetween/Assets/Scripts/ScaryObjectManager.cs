using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PaintingScare : MonoBehaviour
{
    public float enableAfter = 10f;
    public float disableAfter = 20f;

    private List<GameObject> scareObjects = new();

    private void Awake()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child != transform && child.CompareTag("Scare"))
            {
                scareObjects.Add(child.gameObject);
            }
        }
    }

    private void Start()
    {
        StartCoroutine(ScareRoutine());
    }

    private IEnumerator ScareRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(enableAfter);

            foreach (GameObject obj in scareObjects)
            {
                if (obj != null)
                    obj.SetActive(true);
            }

            yield return new WaitForSeconds(disableAfter);

            foreach (GameObject obj in scareObjects)
            {
                if (obj != null)
                    obj.SetActive(false);
            }
        }
    }
}