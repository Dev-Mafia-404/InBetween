using System.Collections;
using UnityEngine;
using StarterAssets;

public class GameObjectSpawnManager : MonoBehaviour
{
    [Header("Player Reference")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("If empty, will try to find player by tag 'Player'")]
    [SerializeField] private string playerTag = "Player";

    [Header("Spawn Settings")]
    [Tooltip("Delay in seconds before spawning (used by delayed spawn methods)")]
    [Min(0f)][SerializeField] private float spawnDelaySeconds = 0f;

    private Coroutine _delayedSpawnRoutine;

    private void Start()
    {
        TryAcquirePlayerTransformIfNeeded();
    }

    // ============================================================================
    // PUBLIC SPAWN METHODS - Called from Events
    // ============================================================================

    /// <summary>
    /// Moves object to player position immediately and activates it
    /// </summary>
    public void SpawnAtPlayer(GameObject targetObject)
    {
        if (!ValidateSpawn(targetObject)) return;
        MoveAndActivateObject(targetObject, playerTransform.position);
    }

    /// <summary>
    /// Moves object to player position after delay and activates it
    /// </summary>
    public void SpawnAtPlayerDelayed(GameObject targetObject)
    {
        if (!ValidateSpawn(targetObject)) return;

        // Stop any existing delayed spawn routine
        if (_delayedSpawnRoutine != null)
        {
            StopCoroutine(_delayedSpawnRoutine);
        }

        _delayedSpawnRoutine = StartCoroutine(DelayedSpawnRoutine(targetObject));
    }

    /// <summary>
    /// Cancels any pending delayed spawns
    /// </summary>
    public void CancelDelayedSpawn()
    {
        if (_delayedSpawnRoutine != null)
        {
            StopCoroutine(_delayedSpawnRoutine);
            _delayedSpawnRoutine = null;
        }
    }

    // ============================================================================
    // PRIVATE SPAWN LOGIC
    // ============================================================================

    private bool ValidateSpawn(GameObject targetObject)
    {
        if (playerTransform == null)
        {
            TryAcquirePlayerTransformIfNeeded();
            if (playerTransform == null)
            {
                Debug.LogError($"[GameObjectSpawnManager] Player reference not found on '{name}'");
                return false;
            }
        }

        if (targetObject == null)
        {
            Debug.LogError($"[GameObjectSpawnManager] Null object provided to spawn on '{name}'");
            return false;
        }

        return true;
    }

    private void MoveAndActivateObject(GameObject targetObject, Vector3 spawnPosition)
    {
        targetObject.transform.position = spawnPosition;

        // Activate if inactive
        if (!targetObject.activeSelf)
        {
            targetObject.SetActive(true);
        }

        Debug.Log($"[GameObjectSpawnManager] Moved '{targetObject.name}' to player position ({spawnPosition}) on '{name}'");
    }

    // ============================================================================
    // COROUTINES
    // ============================================================================

    private IEnumerator DelayedSpawnRoutine(GameObject targetObject)
    {
        yield return new WaitForSeconds(spawnDelaySeconds);
        MoveAndActivateObject(targetObject, playerTransform.position);
        _delayedSpawnRoutine = null;
    }

    // ============================================================================
    // HELPERS
    // ============================================================================

    private void TryAcquirePlayerTransformIfNeeded()
    {
        if (playerTransform != null) return;

        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
        else
        {
            Debug.LogWarning($"[GameObjectSpawnManager] Could not find player by tag '{playerTag}' on '{name}'");
        }
    }
}