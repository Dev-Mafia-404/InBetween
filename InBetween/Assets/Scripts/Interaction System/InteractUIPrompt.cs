using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class InteractUIPrompt : MonoBehaviour
{
  [SerializeField] private PlayerInteractor interactor;
  [SerializeField] private Canvas worldSpaceCanvas;
  [SerializeField] private TMP_Text label;
  [SerializeField] private Vector3 worldOffset = new(0f, 1f, 0f);

  private IInteractable last;

  // Global override. If left at (0,0,0) it will still be used unless you add extra logic.
  // Keeping your original type/visibility; you may want to make the setter public.
  public static Vector3 WorldOffset { get; internal set; }

  void Awake() => Hide();

  void LateUpdate()
  {
    var it = interactor ? interactor.Focused : null;

    // If focus changed, reset state
    if (!ReferenceEquals(it, last))
    {
      last = it;
      if (it == null) Hide();
    }

    if (it == null) return;

    // If the underlying Unity object got destroyed this frame, accessing Transform can throw.
    Transform t;
    try
    {
      t = it.Transform;
      if (!t) { Hide(); return; }
    }
    catch (MissingReferenceException)
    {
      Hide();
      return;
    }

    if (label) label.text = $"{it.DisplayText}  [{KeyCode.E}]";

    if (worldSpaceCanvas)
    {
      worldSpaceCanvas.gameObject.SetActive(true);

      // Use the global static override:
      var offsetToUse = WorldOffset;

      // If you'd rather fall back to the serialized `worldOffset` when the override isn't set,
      // you'd need a sentinel strategy (e.g., nullable, bool flag, etc.).
      if (offsetToUse == default)
        offsetToUse = worldOffset;

      worldSpaceCanvas.transform.position = t.position + offsetToUse;

      var cam = Camera.main;
      if (cam) worldSpaceCanvas.transform.forward = cam.transform.forward;
    }
  }

  void Hide()
  {
    if (label) label.text = null;
    if (worldSpaceCanvas) worldSpaceCanvas.gameObject.SetActive(false);
  }
}