using UnityEngine;

/// <summary>
/// OPTIONAL. Put this on a trigger volume (a Collider with Is Trigger on). When the player
/// steps inside WITHOUT the Enemy currently seeing them, they become concealed: the Enemy
/// can't see or hear them, so a search here fizzles out and it withdraws. If the Enemy saw
/// the player enter (or already had eyes on them), the hiding spot is "blown" and offers no
/// protection — it will walk right in.
///
/// The whole Enemy system works fine with zero hiding spots in the scene; add these only
/// where you want a guaranteed safe nook.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EnemyHidingSpot : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Enemy this spot talks to. Left empty = auto-found at Start.")]
    public Enemy enemy;

    [Header("Detection")]
    [Tooltip("Tag used to recognise the player entering the volume.")]
    public string playerTag = "Player";

    [Header("Enemy Avoidance")]
    [Tooltip("While the player is concealed here, the Enemy roams but won't path closer than this to the spot's centre.")]
    public float keepEnemyOutRadius = 3f;

    [Header("Debug")]
    public bool debugLogging = true;

    public float KeepOutRadius => keepEnemyOutRadius;

    void Start()
    {
        if (enemy == null)
            enemy = FindObjectOfType<Enemy>();

        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;   
            Debug.LogWarning($"[EnemyHidingSpot] '{name}' collider is not set to Is Trigger — it won't detect the player.");
        }
    }

    bool IsPlayer(Collider other)
    {
        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag)) return true;
        if (enemy != null && enemy.player != null)
            return other.transform == enemy.player || other.transform.IsChildOf(enemy.player);
        return false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (enemy == null || !IsPlayer(other)) return;
        if (debugLogging) Debug.Log($"[EnemyHidingSpot] Player entered '{name}'.");
        enemy.PlayerEnteredHidingSpot(this);
    }

    void OnTriggerExit(Collider other)
    {
        if (enemy == null || !IsPlayer(other)) return;
        if (debugLogging) Debug.Log($"[EnemyHidingSpot] Player left '{name}'.");
        enemy.PlayerLeftHidingSpot(this);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, keepEnemyOutRadius);
    }
}
