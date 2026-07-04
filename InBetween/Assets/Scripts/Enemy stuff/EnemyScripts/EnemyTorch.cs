using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The torch's functional logic. Press the use key while facing the Enemy (in range, with a
/// charge, and not on its short use-cooldown) to repel it. On a successful use the torch
/// light flashes to the "soul charge" colour for a moment, one charge is spent, and
/// OnTorchUsedSuccessfully fires — wire that to Enemy.NotifyTorchUsed().
///
/// Charges are granted externally via AddCharge() (wire that to your soul-resolution event).
/// </summary>
public class EnemyTorch : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The player's camera or head transform — used for facing direction.")]
    public Transform playerView;
    [Tooltip("The Enemy's transform to check facing against. Can be set at runtime via SetTarget().")]
    public Transform enemyTarget;
    [Tooltip("The torch's Light. Its colour flashes when a charge is spent. Optional.")]
    public Light torchLight;

    [Header("Input")]
    public KeyCode useKey = KeyCode.T;

    [Header("Aim")]
    [Tooltip("How wide (degrees) the aim cone is. The Enemy must be within this of your view centre.")]
    public float aimConeAngle = 35f;
    [Tooltip("Furthest distance the torch can affect the Enemy.")]
    public float maxRange = 25f;

    [Header("Charges")]
    public int startingCharges = 0;
    public int maxCharges = 3;
    public int CurrentCharges { get; private set; }

    [Header("Use Cooldown")]
    [Tooltip("Seconds you must wait between torch uses.")]
    public float useCooldown = 3f;

    [Header("Light Flash")]
    [Tooltip("Normal torch colour.")]
    public Color defaultColor = new Color(1f, 0.85f, 0.6f);
    [Tooltip("Colour the torch flashes to when a charge is spent.")]
    public Color soulChargeColor = new Color(0.5f, 0.8f, 1f);
    [Tooltip("How long the soul-charge colour stays before fading back (seconds).")]
    public float flashDuration = 1f;

    [Header("UI (optional)")]
    [Tooltip("TMP text showing current charge count. Leave empty if not using.")]
    public TMP_Text chargeText;
    [Tooltip("Format string — {0} is the current charge count.")]
    public string chargeTextFormat = "Torch: {0}";

    [Header("Debug")]
    public bool debugLogging = true;

    [Header("Events")]
    [Tooltip("Fired on a successful torch use. Wire this to Enemy.NotifyTorchUsed().")]
    public UnityEvent OnTorchUsedSuccessfully;
    [Tooltip("Fired when a use attempt fails (no charge, on cooldown, out of range, not aimed).")]
    public UnityEvent OnTorchUseFailed;
    [Tooltip("Fired whenever charge count changes, passing the new count.")]
    public UnityEvent<int> OnChargeCountChanged;

    float cooldownRemaining;
    Coroutine flashRoutine;

    void Start()
    {
        CurrentCharges = Mathf.Clamp(startingCharges, 0, maxCharges);
        if (torchLight != null) torchLight.color = defaultColor;
        RefreshDisplay();
    }

    void Update()
    {
        if (cooldownRemaining > 0f)
            cooldownRemaining -= Time.deltaTime;

        if (Input.GetKeyDown(useKey))
            TryUseTorch();
    }

    /// <summary>Set or change which transform the torch checks against.</summary>
    public void SetTarget(Transform target) => enemyTarget = target;

    void TryUseTorch()
    {
        if (cooldownRemaining > 0f)
        {
            Log($"FAILED — on cooldown ({cooldownRemaining:F1}s left).");
            OnTorchUseFailed?.Invoke();
            return;
        }
        if (CurrentCharges <= 0)
        {
            Log("FAILED — no charges. Wire AddCharge() to your soul-resolution event.");
            OnTorchUseFailed?.Invoke();
            return;
        }
        if (enemyTarget == null)
        {
            Log("FAILED — enemyTarget is null. Assign it or call SetTarget().");
            OnTorchUseFailed?.Invoke();
            return;
        }
        if (playerView == null)
        {
            Log("FAILED — playerView is null. Assign the player camera/head transform.");
            OnTorchUseFailed?.Invoke();
            return;
        }

        Vector3 toTarget = enemyTarget.position - playerView.position;
        float distance = toTarget.magnitude;

        if (distance > maxRange)
        {
            Log($"FAILED — out of range. Distance={distance:F1}, max={maxRange}.");
            OnTorchUseFailed?.Invoke();
            return;
        }

        float angle = Vector3.Angle(playerView.forward, toTarget.normalized);
        if (angle > aimConeAngle)
        {
            Log($"FAILED — not aimed at it. Angle={angle:F1}\u00b0, cone={aimConeAngle}\u00b0.");
            OnTorchUseFailed?.Invoke();
            return;
        }

        // Success.
        CurrentCharges--;
        cooldownRemaining = useCooldown;
        RefreshDisplay();
        FlashLight();
        Log($"SUCCESS — distance={distance:F1}, angle={angle:F1}\u00b0. Charges left: {CurrentCharges}.");
        OnChargeCountChanged?.Invoke(CurrentCharges);
        OnTorchUsedSuccessfully?.Invoke();
    }

    void FlashLight()
    {
        if (torchLight == null) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        torchLight.color = soulChargeColor;
        yield return new WaitForSeconds(flashDuration);
        torchLight.color = defaultColor;
        flashRoutine = null;
    }

    void Log(string message)
    {
        if (debugLogging)
            Debug.Log($"[EnemyTorch] {message}");
    }

    /// <summary>Grant one charge, capped at maxCharges. Wire this to your soul-resolution event.</summary>
    public void AddCharge()
    {
        CurrentCharges = Mathf.Min(CurrentCharges + 1, maxCharges);
        RefreshDisplay();
        OnChargeCountChanged?.Invoke(CurrentCharges);
    }

    /// <summary>Grant several charges at once, still capped at maxCharges.</summary>
    public void AddCharges(int amount)
    {
        CurrentCharges = Mathf.Min(CurrentCharges + amount, maxCharges);
        RefreshDisplay();
        OnChargeCountChanged?.Invoke(CurrentCharges);
    }

    void RefreshDisplay()
    {
        if (chargeText != null)
            chargeText.text = string.Format(chargeTextFormat, CurrentCharges);
    }
}
