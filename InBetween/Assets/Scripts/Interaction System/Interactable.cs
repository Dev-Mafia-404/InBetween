using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class Interactable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private string ToolTip = "Interact";
    [Space] [SerializeField] private bool canInteract = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onInteract;
    [SerializeField] private Vector3 personaloffset = Vector3.zero;
    
    private Outline outline;

    public Transform Transform => transform;
    public string DisplayText => ToolTip;
    public bool CanInteract => canInteract;

    void Awake()
    {
        outline = GetComponent<Outline>();
        if (outline != null)
            outline.enabled = false;
    }

    public virtual void OnInteract(PlayerInteractor interactor)
    {
        if (!CanInteract) return;

        Debug.Log($"Interacted with {name}");
        onInteract?.Invoke();
    }

    public void OnFocusEnter(PlayerInteractor interactor)
    {
        if (outline != null)
            outline.enabled = true;
        
        // Update the UI prompt with this object's offset
        InteractUIPrompt.WorldOffset = personaloffset;
    }

    public void OnFocusExit(PlayerInteractor interactor)
    {
        if (outline != null)
            outline.enabled = false;
        
        // Reset the UI prompt offset
        InteractUIPrompt.WorldOffset = Vector3.zero;
    }
}