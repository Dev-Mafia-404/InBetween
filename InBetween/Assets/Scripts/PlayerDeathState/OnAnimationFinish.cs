using UnityEngine;
using UnityEngine.Events;

public class OnAnimationFinish : MonoBehaviour
{
    [SerializeField] AudioSource JumpscareAudioSource;
    [SerializeField] GameObject BloodUI;
    public UnityEvent OnFinish;

    public void ActivateEvent()
    {
        Debug.Log("PLAYER GOT KILLED !");
        OnFinish?.Invoke();
    }

    public void ActivateJumpscareSound()
    {
        JumpscareAudioSource.Play();
    }

    public void ActivateBloodUI()
    {
        BloodUI.SetActive(true);
    }
}
