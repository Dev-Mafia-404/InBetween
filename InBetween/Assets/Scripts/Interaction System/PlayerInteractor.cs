using UnityEngine;

[DisallowMultipleComponent]
public class PlayerInteractor : MonoBehaviour
{
  [Header("Detection")]
  [SerializeField] private float radius = 2f;
  [SerializeField] private LayerMask interactableLayers = ~0;

  [Header("Input")]
  [SerializeField] private KeyCode interactKey = KeyCode.E;

  [Header("Debug")]
  [SerializeField] private bool drawGizmos = true;

  private readonly Collider[] hits = new Collider[32];
  private IInteractable focused;
  public IInteractable Focused => focused;

  //private HoldParentInteractable held;

  void Update()
  {
    SetFocus(FindClosestInteractable());

    if (focused != null && focused.CanInteract && Input.GetKeyDown(interactKey))
    {
      focused.OnInteract(this);
    }
  }

  IInteractable FindClosestInteractable()
  {
    var count = Physics.OverlapSphereNonAlloc(transform.position, radius, hits, interactableLayers, QueryTriggerInteraction.Collide);
    IInteractable best = null;
    var bestSqr = float.PositiveInfinity;

    for (var i = 0; i < count; i++)
    {
      var c = hits[i];
      if (!c) continue;

      if (!c.TryGetComponent<IInteractable>(out var it) && !c.GetComponentInParent<IInteractable>(out it))
        continue;

      if (!it.CanInteract) continue;

      var d = (it.Transform.position - transform.position).sqrMagnitude;
      if (d < bestSqr) { bestSqr = d; best = it; }
    }

    return best;
  }

  void SetFocus(IInteractable next)
  {
    if (ReferenceEquals(focused, next)) return;

    // Call OnFocusExit on the previous interactable
    if (focused != null)
    {
      focused.OnFocusExit(this);
    }

    // Set the new focus
    focused = next;

    // Call OnFocusEnter on the new interactable
    if (focused != null)
    {
      focused.OnFocusEnter(this);
    }
  }

  void OnDisable() => SetFocus(null);

  void OnDrawGizmosSelected()
  {
    if (!drawGizmos) return;
    Gizmos.color = Color.cyan;
    Gizmos.DrawWireSphere(transform.position, radius);
  }
}

static class ComponentExt
{
  public static bool GetComponentInParent<T>(this Component c, out T result) where T : class
  {
    result = c.GetComponentInParent(typeof(T)) as T;
    return result != null;
  }
}