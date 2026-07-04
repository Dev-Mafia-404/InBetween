using UnityEngine;

public class TestVolume : MonoBehaviour
{
    public AudioSource audioSource;

    private void Update()
    {
        if (Input.GetKey(KeyCode.K))
        {
            audioSource.volume = 0f;
        }

        if (Input.GetKey(KeyCode.L))
        {
            audioSource.volume = 1f;
        }
    }
}