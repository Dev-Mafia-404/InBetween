using UnityEngine;

public class ScareManager : MonoBehaviour
{
    [Header("Scare Objects")]
    [SerializeField] private GameObject[] scareObjects;

    private void Awake()
    {
        SetScares(false);
    }

    public void StartScares()
    {
        SetScares(true);
    }

    public void StopScares()
    {
        SetScares(false);
    }

    private void SetScares(bool active)
    {
        foreach (GameObject scare in scareObjects)
        {
            if (scare != null)
                scare.SetActive(active);
        }
    }
}