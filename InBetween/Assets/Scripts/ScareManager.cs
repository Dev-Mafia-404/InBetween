using System.Collections.Generic;
using UnityEngine;

public class ScareManager : MonoBehaviour
{
    [Header("Scare Tag")]
    [SerializeField] private string scareTag = "Scare";

    private readonly List<GameObject> scareObjects = new();

    private void Awake()
    {
        scareObjects.Clear();

        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();

        foreach (Transform t in allTransforms)
        {
            // Ignore assets/prefabs that aren't in the scene
            if (!t.gameObject.scene.IsValid())
                continue;

            if (t.CompareTag(scareTag))
            {
                GameObject root = t.root.gameObject;

                if (!scareObjects.Contains(root))
                    scareObjects.Add(root);
            }
        }
    }

    public void StartScares()
    {
        SetScaresActive(true);
    }

    public void StopScares()
    {
        SetScaresActive(false);
    }

    private void SetScaresActive(bool active)
    {
        foreach (GameObject scare in scareObjects)
        {
            if (scare != null)
                scare.SetActive(active);
        }
    }
}