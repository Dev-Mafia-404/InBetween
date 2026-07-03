using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Finds a good NavMesh spawn point for the Enemy: a ring of candidates around the player,
/// filtered by hard rules, then scored to pick the most natural-feeling spot (behind the
/// player, where they came from, out of sight, etc). Pure query utility — TryFindSpawnPoint
/// returns false when nothing valid exists right now and the caller (Enemy.cs) decides what
/// to do (wait and retry).
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Candidate Ring")]
    [Tooltip("How many points to test around the player per attempt.")]
    public int candidateCount = 16;
    [Tooltip("Closest a spawn can be to the player.")]
    public float minSpawnDistance = 7f;
    [Tooltip("Furthest a spawn can be from the player.")]
    public float maxSpawnDistance = 20f;
    [Tooltip("How far to search the NavMesh near each raw point for a valid position.")]
    public float navMeshSnapRadius = 1f;

    [Header("Path Check")]
    [Tooltip("Reject spawns whose walking route to the player is longer than this (stops 'close but through 5 rooms').")]
    public float maxWalkPathLength = 60f;

    [Header("Avoid Repeats")]
    [Tooltip("Penalize points within this distance of the last spawn.")]
    public float avoidLastSpawnRadius = 5f;
    public float avoidLastSpawnPenalty = 20f;

    [Header("Scoring")]
    [Tooltip("Bonus for being behind the player's facing.")]
    public float behindPlayerBonus = 30f;
    [Tooltip("Bonus for being out of the player's direct sightline.")]
    public float outOfSightBonus = 20f;
    [Tooltip("Bonus for being in the direction the player came from.")]
    public float cameFromBonus = 15f;
    [Tooltip("Bonus for landing in the sweet-spot distance band.")]
    public float sweetSpotBonus = 10f;
    public float sweetSpotMin = 9f;
    public float sweetSpotMax = 15f;

    [Header("Sightline Check")]
    [Tooltip("Layers that count as blocking the sightline check.")]
    public LayerMask sightBlockingLayers = ~0;
    [Tooltip("Eye height added to both ends of the sightline ray.")]
    public float eyeHeight = 1.6f;

    Vector3? lastSpawnPosition;

    /// <summary>
    /// Tries to find a valid spawn point. Returns true and outputs the position if found.
    /// playerMoveDirection can be Vector3.zero if the player is standing still.
    /// </summary>
    public bool TryFindSpawnPoint(Transform player, Vector3 playerMoveDirection, out Vector3 result)
    {
        result = Vector3.zero;
        if (player == null) return false;

        Vector3 playerPos = player.position;
        Vector3 playerForward = player.forward;
        bool playerIsMoving = playerMoveDirection.sqrMagnitude > 0.01f;
        Vector3 moveDir = playerIsMoving ? playerMoveDirection.normalized : Vector3.zero;

        float bestScore = float.MinValue;
        Vector3 bestPoint = Vector3.zero;
        bool foundAny = false;

        for (int i = 0; i < candidateCount; i++)
        {
            float angle = (360f / candidateCount) * i;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
            Vector3 rawPoint = playerPos + dir * distance;

            if (!NavMesh.SamplePosition(rawPoint, out NavMeshHit hit, navMeshSnapRadius, NavMesh.AllAreas))
                continue; // not on NavMesh

            Vector3 candidate = hit.position;
            float actualDistance = Vector3.Distance(candidate, playerPos);

            if (actualDistance < minSpawnDistance || actualDistance > maxSpawnDistance)
                continue; // drifted outside the band

            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.CalculatePath(candidate, playerPos, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                continue; // no complete path

            if (GetPathLength(path) > maxWalkPathLength)
                continue; // path too winding

            // Passed the hard rules — score it.
            float score = 0f;
            Vector3 toCandidate = (candidate - playerPos).normalized;

            if (Vector3.Dot(toCandidate, playerForward) < 0f)
                score += behindPlayerBonus;

            if (playerIsMoving && Vector3.Dot(toCandidate, moveDir) > 0.3f)
                score += cameFromBonus;

            if (IsOutOfSight(candidate, playerPos))
                score += outOfSightBonus;

            if (actualDistance >= sweetSpotMin && actualDistance <= sweetSpotMax)
                score += sweetSpotBonus;

            if (lastSpawnPosition.HasValue && Vector3.Distance(candidate, lastSpawnPosition.Value) < avoidLastSpawnRadius)
                score -= avoidLastSpawnPenalty;

            if (score > bestScore)
            {
                bestScore = score;
                bestPoint = candidate;
                foundAny = true;
            }
        }

        if (!foundAny) return false;

        result = bestPoint;
        lastSpawnPosition = bestPoint;
        return true;
    }

    bool IsOutOfSight(Vector3 from, Vector3 to)
    {
        Vector3 a = from + Vector3.up * eyeHeight;
        Vector3 b = to + Vector3.up * eyeHeight;
        return Physics.Linecast(a, b, sightBlockingLayers);
    }

    static float GetPathLength(NavMeshPath path)
    {
        float length = 0f;
        var corners = path.corners;
        for (int i = 1; i < corners.Length; i++)
            length += Vector3.Distance(corners[i - 1], corners[i]);
        return length;
    }
}
