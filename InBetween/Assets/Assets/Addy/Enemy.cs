using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>
/// The Enemy's main state machine. Requires a NavMeshAgent. Reads its per-phase tuning
/// (chase speed, search time, cooldowns, door permission) live from EnemyPhaseManager, finds
/// spawn points via EnemySpawner, and listens to an EnemyNoiseSource while Searching.
///
/// All speeds/timers are set DIRECTLY (no multipliers). Two optional TMP fields give a live
/// readout: one shows the current stage, the other shows the latest event ("Spotted you",
/// "Lost sight", "Searching", "Couldn't find you — leaving", ...). Detailed reasoning goes to
/// the Console so you can always tell WHY it did what it did.
///
/// Hiding spots (EnemyHidingSpot) are optional; the system runs fine with none in the scene.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : MonoBehaviour
{
    public enum State { Cooldown, Spawning, Hunting, Chasing, Searching, Withdrawn, Frozen }

    [Header("References")]
    public Transform player;
    public EnemyPhaseManager phaseManager;
    public EnemySpawner spawner;
    public EnemyNoiseSource noiseSource;

    [Header("Speeds (units per second — set directly)")]
    [Tooltip("Speed while hunting (the initial approach before it sees you).")]
    public float huntSpeed = 3.5f;
    [Tooltip("Speed while searching (poking around your last known area).")]
    public float searchSpeed = 3f;
    // Chase speed comes from the phase manager (per-phase).

    [Header("Appear / Disappear")]
    [Tooltip("How long the manifestation takes before it starts hunting.")]
    public float appearTime = 1.5f;
    [Tooltip("How long the fade-out takes before it fully leaves.")]
    public float disappearTime = 1.5f;

    [Header("Hunting")]
    [Tooltip("Min/max time it hunts toward your last position before giving up and searching (even if it never sees you).")]
    public float huntTimeMin = 3f;
    public float huntTimeMax = 8f;

    [Header("Perception")]
    [Tooltip("How often (seconds) it checks whether it can sense you. 0.2 = 5x/second, very cheap.")]
    public float sightCheckInterval = 0.2f;
    [Tooltip("Enemy eye height for sight rays.")]
    public float eyeHeight = 1.6f;
    [Tooltip("Furthest distance it can sense you at all.")]
    public float maxSightRange = 35f;
    [Tooltip("Inside this distance it senses you very reliably (extra sample rays fire). Handles point-blank and standing on furniture. Still blocked by real walls.")]
    public float proximitySenseRadius = 5f;
    [Tooltip("Layers that BLOCK sight (walls, doors, big furniture). The player is handled automatically — you do NOT need to exclude the player's layer from this.")]
    public LayerMask sightBlockingLayers = ~0;
    [Tooltip("After losing sight it keeps pushing toward where you were for this long before Searching. Randomized in this range.")]
    public float keepChasingAfterLostMin = 2.5f;
    public float keepChasingAfterLostMax = 4f;
    [Tooltip("When it can SEE you but can't physically reach you (you climbed onto furniture), it holds position and keeps facing you instead of drifting off to search.")]
    public bool holdAndStareWhenUnreachable = true;

    [Header("Searching (dynamic — it moves around, doesn't just stand)")]
    [Tooltip("How far from your last known spot it wanders while searching.")]
    public float searchWanderRadius = 12f;
    [Tooltip("How close it must get to a search point to count as 'arrived'.")]
    public float searchArriveDistance = 1f;
    [Tooltip("How long it pauses to 'look around' at each search point before moving on.")]
    public float searchPauseMin = 0.5f;
    public float searchPauseMax = 1.6f;

    [Header("Doors")]
    [Tooltip("Layer(s) your Door colliders are on. Do NOT include the Enemy's own layer.")]
    public LayerMask doorLayers = 0;
    [Tooltip("How far ahead it checks for a closed door to push open.")]
    public float doorReach = 2f;

    [Header("Hiding Spots (optional)")]
    [Tooltip("If on, a loud noise (sprint / slammed door) while hidden gives the player away and blows their cover.")]
    public bool loudNoiseRevealsHiddenPlayer = true;
    [Tooltip("Noise reach at or above this counts as 'loud' enough to blow cover.")]
    public float loudNoiseThreshold = 12f;
    [Tooltip("Hiding only FAILS if the Enemy is present, actually sees you right now, AND is within this distance when you enter. Bigger = harder to hide in its face. Smaller = easier to slip into cover.")]
    public float hideBlownRange = 8f;

    [Header("Catch")]
    [Tooltip("If on, touching the player (trigger) fires OnPlayerCaught. Needs a trigger Collider on this object.")]
    public bool catchOnTouch = true;

    [Header("Status Text (optional)")]
    [Tooltip("TMP showing the current stage (Hunting / Chasing / Searching / cooldown countdown).")]
    public TMPro.TMP_Text stateText;
    [Tooltip("TMP showing the latest event ('Spotted you', 'Lost sight', 'Searching', ...).")]
    public TMPro.TMP_Text eventText;
    [Tooltip("Countdown format while waiting to reappear. {0} = seconds.")]
    public string cooldownFormat = "Next: {0}s";
    [Tooltip("Countdown format while manifesting. {0} = seconds.")]
    public string manifestingFormat = "Manifesting… {0}s";
    [Tooltip("Blank the state text while dormant / withdrawn / frozen instead of showing the state name.")]
    public bool blankStateWhenInactive = true;

    [Header("Debug")]
    [Tooltip("Logs every transition and the reason behind it. Turn off for shipping.")]
    public bool debugLogging = true;
    [Tooltip("Draws the sight line in the Scene view (green = sees you, red = blocked).")]
    public bool drawSightGizmo = true;
    [Tooltip("Optional: a LineRenderer to show the sight line in the Game view too.")]
    public LineRenderer sightDebugLine;
    public Color sightClearColor = Color.green;
    public Color sightBlockedColor = Color.red;

    [Header("Audio (optional)")]
    public AudioSource appearAudioSource;
    public AudioClip appearClip;

    [Header("Visibility (placeholder until your materialization shader exists)")]
    [Tooltip("Renderers to hide while gone and show once manifested. Auto-filled from children if empty.")]
    public Renderer[] bodyRenderers;
    [Tooltip("Keep the body hidden for the whole appear window (shown only once it starts hunting).")]
    public bool hideWhileAppearing = true;

    [Header("Read-Only State")]
    public State currentState = State.Cooldown;
    public float cooldownTimeRemaining;

    [Header("Events")]
    public UnityEvent OnSpawnStarted;
    public UnityEvent OnHuntingStarted;
    public UnityEvent OnChaseStarted;
    public UnityEvent OnSearchingStarted;
    public UnityEvent OnWithdrawalStarted;
    public UnityEvent OnCooldownStarted;
    [Tooltip("Fires when the player is caught. Hook your game-over sequence here.")]
    public UnityEvent OnPlayerCaught;

    // --- internals ---
    NavMeshAgent agent;
    Vector3 lastKnownPlayerPosition;
    Vector3 lastPlayerPosForDir;
    Vector3 playerMoveDirCache;

    float sightTimer;
    bool hasLOS;
    Vector3 sightFrom, sightTo;
    bool sightBlocked, hasSightData;

    float huntTimer, huntDuration;
    float keepChasingTimer;
    bool inLostSightGrace;

    float searchTimer, searchPauseTimer, searchLogTimer;
    Vector3 searchCenter;

    float appearTimer, disappearTimer;
    float pendingCooldown;
    bool pendingStartCooldown = true;

    float lastWithdrawTime = -999f;
    bool spawnRetryQueued;
    float spawnRetryTimer;
    const float SPAWN_RETRY_INTERVAL = 10f;

    bool hasInitialized;

    // hiding spot
    EnemyHidingSpot currentHidingSpot;
    bool playerConcealed;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.enabled = false;

        if (bodyRenderers == null || bodyRenderers.Length == 0)
            bodyRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);

        SetVisible(false);
    }

    void Start()
    {
        if (hasInitialized) return;
        hasInitialized = true;
        if (player != null)
        {
            lastKnownPlayerPosition = player.position;
            lastPlayerPosForDir = player.position;
        }
        SetState(State.Cooldown);
        cooldownTimeRemaining = float.MaxValue; // dormant until the phase manager wakes it
    }

    void OnEnable()
    {
        if (noiseSource != null) noiseSource.OnNoiseMade += HandleNoiseEvent;
        if (phaseManager != null) phaseManager.OnAwakened.AddListener(HandleAwakened);
    }

    void OnDisable()
    {
        if (noiseSource != null) noiseSource.OnNoiseMade -= HandleNoiseEvent;
        if (phaseManager != null) phaseManager.OnAwakened.RemoveListener(HandleAwakened);
    }

    void HandleAwakened()
    {
        hasInitialized = true;
        Log("Awakened — first soul resolved. Starting initial cooldown before the first possible appearance.");
        StartCooldown(phaseManager.CooldownAfterEscape);
    }

    void Update()
    {
        switch (currentState)
        {
            case State.Cooldown: TickCooldown(); break;
            case State.Spawning: TickSpawning(); break;
            case State.Hunting: TickHunting(); break;
            case State.Chasing: TickChasing(); break;
            case State.Searching: TickSearching(); break;
            case State.Withdrawn: TickWithdrawn(); break;
        }
        UpdateStateText();
    }

    void LateUpdate()
    {
        if (player != null)
        {
            Vector3 delta = player.position - lastPlayerPosForDir;
            if (delta.sqrMagnitude > 0.0001f) playerMoveDirCache = delta.normalized;
            lastPlayerPosForDir = player.position;
        }

        if (spawnRetryQueued)
        {
            spawnRetryTimer -= Time.deltaTime;
            if (spawnRetryTimer <= 0f) AttemptSpawn();
        }
    }

    // ---------------------------------------------------------------
    // Cooldown
    // ---------------------------------------------------------------

    void StartCooldown(float duration)
    {
        float floor = phaseManager != null ? phaseManager.minTimeBetweenSpawns : 45f;
        float sinceWithdraw = Time.time - lastWithdrawTime;
        float remainingFloor = Mathf.Max(0f, floor - sinceWithdraw);

        cooldownTimeRemaining = Mathf.Max(duration, remainingFloor);
        SetState(State.Cooldown);
        Log($"Cooldown started: {cooldownTimeRemaining:F1}s (phase={(phaseManager != null ? phaseManager.CurrentPhase.ToString() : "none")}).");
        OnCooldownStarted?.Invoke();
    }

    void TickCooldown()
    {
        if (phaseManager == null) return;
        if (phaseManager.CurrentPhase == EnemyPhaseManager.Phase.Dormant) return;
        if (phaseManager.CurrentPhase == EnemyPhaseManager.Phase.Dissolved) return;
        if (cooldownTimeRemaining >= float.MaxValue) return; // intentionally dormant

        cooldownTimeRemaining -= Time.deltaTime;
        if (cooldownTimeRemaining <= 0f) AttemptSpawn();
    }

    void AttemptSpawn()
    {
        if (spawner == null) { Log("Spawn FAILED — spawner reference is null."); QueueSpawnRetry(); return; }
        if (player == null) { Log("Spawn FAILED — player reference is null."); QueueSpawnRetry(); return; }

        if (spawner.TryFindSpawnPoint(player, playerMoveDirCache, out Vector3 spawnPoint))
            BeginSpawnAt(spawnPoint);
        else
        {
            Log($"Spawn FAILED — no valid point (check NavMesh coverage / distances). Retrying in {SPAWN_RETRY_INTERVAL}s.");
            QueueSpawnRetry();
        }
    }

    void QueueSpawnRetry()
    {
        spawnRetryQueued = true;
        spawnRetryTimer = SPAWN_RETRY_INTERVAL;
    }

    // ---------------------------------------------------------------
    // Spawning
    // ---------------------------------------------------------------

    void BeginSpawnAt(Vector3 position)
    {
        spawnRetryQueued = false;
        agent.enabled = true;

        if (!agent.Warp(position))
        {
            Log($"Warp FAILED at {position} — not on NavMesh. Retrying.");
            agent.enabled = false;
            QueueSpawnRetry();
            return;
        }

        appearTimer = appearTime;
        SetState(State.Spawning);
        Report("Manifesting nearby…");
        Log($"Manifesting at {position} over {appearTime}s.");

        SetVisible(!hideWhileAppearing);

        if (appearAudioSource != null && appearClip != null)
        {
            appearAudioSource.Stop();
            appearAudioSource.clip = appearClip;
            appearAudioSource.Play();
        }

        OnSpawnStarted?.Invoke();
    }

    void TickSpawning()
    {
        appearTimer -= Time.deltaTime;
        if (appearTimer <= 0f) BeginHunting();
    }

    // ---------------------------------------------------------------
    // Hunting
    // ---------------------------------------------------------------

    void BeginHunting()
    {
        if (player != null)
            lastKnownPlayerPosition = (playerConcealed && currentHidingSpot != null)
                ? currentHidingSpot.transform.position
                : player.position;
        huntDuration = Random.Range(huntTimeMin, huntTimeMax);
        huntTimer = 0f;

        SetVisible(true);
        SetSpeed(huntSpeed);
        SetState(State.Hunting);
        Report("Hunting — closing in.");
        Log($"Hunting for up to {huntDuration:F1}s.");
        OnHuntingStarted?.Invoke();
    }

    void TickHunting()
    {
        if (player == null) return;

        // While concealed it can't sense you, so it heads to the STALE last-known spot only.
        if (!playerConcealed) lastKnownPlayerPosition = player.position;

        MoveTowards(lastKnownPlayerPosition);
        TryOpenDoorIfBlocked();

        UpdateSightCheck();
        if (hasLOS) { BeginChasing(fromSearch: false); return; }

        huntTimer += Time.deltaTime;
        if (huntTimer >= huntDuration)
        {
            Log("Hunt window elapsed without sighting — switching to Searching.");
            BeginSearching(lastKnownPlayerPosition, "reached your area but never saw you");
        }
    }

    // ---------------------------------------------------------------
    // Chasing
    // ---------------------------------------------------------------

    void BeginChasing(bool fromSearch)
    {
        SetSpeed(phaseManager != null ? phaseManager.ChaseSpeed : 6f);
        inLostSightGrace = false;
        SetState(State.Chasing);
        Report(fromSearch ? "Spotted you again — chase on!" : "Spotted you — chase on!");
        Log($"Chasing (LOS acquired, speed={agent.speed:F1}).");
        OnChaseStarted?.Invoke();
    }

    void TickChasing()
    {
        if (player == null) return;

        UpdateSightCheck();

        if (hasLOS)
        {
            if (inLostSightGrace)
            {
                inLostSightGrace = false;
                Report("Back in sight — still chasing!");
            }
            lastKnownPlayerPosition = player.position;
            MoveTowards(lastKnownPlayerPosition);
            TryOpenDoorIfBlocked();

            // You can be seen but not reached (climbed onto furniture): hold and keep staring.
            if (holdAndStareWhenUnreachable && agent.enabled && agent.isOnNavMesh &&
                !agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete)
                FacePlayer();
            return;
        }

        // Lost sight — keep pushing to where you were for a short grace window.
        if (!inLostSightGrace)
        {
            inLostSightGrace = true;
            keepChasingTimer = Random.Range(keepChasingAfterLostMin, keepChasingAfterLostMax);
            Report("Lost sight — heading to where you were.");
            Log($"LOS broken. Pushing to last known spot for {keepChasingTimer:F1}s before searching.");
        }

        MoveTowards(lastKnownPlayerPosition);
        TryOpenDoorIfBlocked();

        keepChasingTimer -= Time.deltaTime;
        if (keepChasingTimer <= 0f)
            BeginSearching(lastKnownPlayerPosition, "lost sight and the grace chase ran out");
    }

    // ---------------------------------------------------------------
    // Searching (dynamic wander)
    // ---------------------------------------------------------------

    void BeginSearching(Vector3 position, string reasonForContext)
    {
        searchCenter = position;
        searchTimer = phaseManager != null ? phaseManager.SearchTime : 15f;
        searchPauseTimer = 0f; // 0 = immediately start wandering after reaching centre
        searchLogTimer = 0f;
        inLostSightGrace = false;

        SetSpeed(searchSpeed);
        MoveTowards(searchCenter);
        SetState(State.Searching);
        Report(playerConcealed ? "Searching… (it can't sense you)" : "Searching your last known area.");
        Log($"Searching for {searchTimer:F1}s. Context: {reasonForContext}. Concealed={playerConcealed}.");
        OnSearchingStarted?.Invoke();
    }

    void TickSearching()
    {
        UpdateSightCheck();
        if (hasLOS) { BeginChasing(fromSearch: true); return; }

        searchTimer -= Time.deltaTime;

        searchLogTimer -= Time.deltaTime;
        if (searchLogTimer <= 0f)
        {
            searchLogTimer = 2f;
            Log($"Searching… {searchTimer:F1}s left.");
        }

        if (searchTimer <= 0f)
        {
            string why = playerConcealed
                ? "search time ran out while you stayed hidden and quiet"
                : "search time ran out without reacquiring sight or hearing you";
            Report("Couldn't find you — leaving.");
            Log($"WITHDRAWING. Reason: {why}.");
            BeginWithdrawal(phaseManager != null ? phaseManager.CooldownAfterEscape : 180f, true);
            return;
        }

        // Dynamic wander: move to a point, pause briefly to 'look', then pick another.
        if (agent.enabled && agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= searchArriveDistance)
        {
            searchPauseTimer -= Time.deltaTime;
            if (searchPauseTimer <= 0f)
            {
                if (RandomSearchPoint(out Vector3 point))
                {
                    MoveTowards(point);
                    searchPauseTimer = Random.Range(searchPauseMin, searchPauseMax);
                }
            }
        }

        TryOpenDoorIfBlocked();
    }

    void HandleNoiseEvent(Vector3 noisePosition, float reach)
    {
        // Loud noise can blow a hidden player's cover even while concealed.
        if (playerConcealed)
        {
            if (loudNoiseRevealsHiddenPlayer && reach >= loudNoiseThreshold &&
                Vector3.Distance(noisePosition, transform.position) <= reach)
            {
                Log($"Hidden player gave themselves away with a loud noise (reach={reach:F0}). Cover blown.");
                BlowConcealment("you made too much noise while hidden");
                // fall through so it reacts to the noise below
            }
            else
            {
                return; // quiet + hidden = safe
            }
        }

        if (currentState != State.Searching) return;

        if (Vector3.Distance(noisePosition, transform.position) <= reach)
        {
            Report("Heard something — checking it out.");
            Log($"Noise heard at {noisePosition} (reach={reach:F0}). Redirecting search (timer not reset).");
            searchCenter = noisePosition;
            searchPauseTimer = 0f;
            MoveTowards(noisePosition);
        }
    }

    bool RandomSearchPoint(out Vector3 result)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            Vector3 raw = searchCenter + Random.insideUnitSphere * searchWanderRadius;
            raw.y = searchCenter.y;
            if (NavMesh.SamplePosition(raw, out NavMeshHit hit, searchWanderRadius, NavMesh.AllAreas))
            {
                if (playerConcealed && currentHidingSpot != null &&
                    Vector3.Distance(hit.position, currentHidingSpot.transform.position) < currentHidingSpot.KeepOutRadius)
                    continue; // don't wander into the hiding spot
                result = hit.position;
                return true;
            }
        }
        result = Vector3.zero;
        return false;
    }

    // ---------------------------------------------------------------
    // Withdrawal
    // ---------------------------------------------------------------

    void BeginWithdrawal(float nextCooldown, bool startCooldownAfter)
    {
        disappearTimer = disappearTime;
        pendingCooldown = nextCooldown;
        pendingStartCooldown = startCooldownAfter;
        if (agent.enabled) agent.isStopped = true;
        SetState(State.Withdrawn);
        Log($"Withdrawing. Will {(startCooldownAfter ? $"cool down for {nextCooldown:F0}s" : "stay dormant")} after {disappearTime}s fade.");
        OnWithdrawalStarted?.Invoke();
    }

    void TickWithdrawn()
    {
        disappearTimer -= Time.deltaTime;
        if (disappearTimer <= 0f) CompleteWithdrawal();
    }

    void CompleteWithdrawal()
    {
        agent.enabled = false;
        SetVisible(false);
        lastWithdrawTime = Time.time;
        hasLOS = false; // never let a stale sight value survive a despawn

        if (pendingStartCooldown)
            StartCooldown(pendingCooldown);
        else
        {
            SetState(State.Cooldown);
            cooldownTimeRemaining = float.MaxValue;
            Log("Dormant — will not reappear until something restarts it (final dissolution).");
        }
    }

    // ---------------------------------------------------------------
    // Movement / sight
    // ---------------------------------------------------------------

    void MoveTowards(Vector3 position)
    {
        if (!agent.enabled || !agent.isOnNavMesh) return;

        if (playerConcealed && currentHidingSpot != null)
            position = KeepOutOfHidingSpot(position);

        agent.isStopped = false;
        agent.SetDestination(position);
    }

    Vector3 KeepOutOfHidingSpot(Vector3 dest)
    {
        Vector3 center = currentHidingSpot.transform.position;
        float r = currentHidingSpot.KeepOutRadius;

        Vector3 flat = dest - center; flat.y = 0f;
        if (flat.magnitude >= r) return dest;

        Vector3 dir = flat.sqrMagnitude > 0.001f ? flat.normalized : (transform.position - center);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward; else dir.Normalize();

        Vector3 edge = center + dir * r;
        if (NavMesh.SamplePosition(edge, out NavMeshHit hit, r, NavMesh.AllAreas))
            return hit.position;
        return edge;
    }

    void SetSpeed(float speed)
    {
        if (agent.enabled) agent.speed = speed;
    }

    void FacePlayer()
    {
        if (player == null) return;
        Vector3 flat = player.position - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.001f) return;
        Quaternion target = Quaternion.LookRotation(flat.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 360f * Time.deltaTime);
    }

    void UpdateSightCheck()
    {
        sightTimer += Time.deltaTime;
        if (sightTimer < sightCheckInterval) return;
        sightTimer = 0f;

        if (player == null) { hasLOS = false; return; }

        Vector3 eye = transform.position + Vector3.up * eyeHeight;

        // Concealed in a hiding spot = cannot be sensed, full stop.
        if (playerConcealed)
        {
            if (hasLOS) Log("Sight lost — you slipped into a hiding spot unseen.");
            hasLOS = false;
            sightFrom = eye;
            sightTo = player.position + Vector3.up * 1.0f;
            sightBlocked = true;
            hasSightData = true;
            UpdateSightDebugLine();
            return;
        }

        bool sensed = CanSensePlayer(out Vector3 seenPoint);

        sightFrom = eye;
        sightTo = sensed ? seenPoint : player.position + Vector3.up * 1.0f;
        sightBlocked = !sensed;
        hasSightData = true;

        bool previous = hasLOS;
        hasLOS = sensed;
        if (previous != hasLOS)
            Log($"Sight {(hasLOS ? "GAINED" : "LOST")}.");

        UpdateSightDebugLine();
    }

    /// <summary>
    /// Multi-sample perception. Casts a handful of rays at points up the player's body and,
    /// when close, a couple of lateral points too. Bails early on the first clear ray. A ray
    /// counts as clear if it reaches the player OR the first thing it hits IS the player — so
    /// the player's own collider never blocks the check, and point-blank always registers.
    /// </summary>
    bool CanSensePlayer(out Vector3 seenPoint)
    {
        seenPoint = player.position + Vector3.up * 1.0f;
        if (player == null || playerConcealed) return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > maxSightRange) return false;

        Vector3 p = player.position;

        // Chest, head, low — most-likely-visible first.
        if (RayReachesPlayer(eye, p + Vector3.up * 1.0f)) { seenPoint = p + Vector3.up * 1.0f; return true; }
        if (RayReachesPlayer(eye, p + Vector3.up * 1.7f)) { seenPoint = p + Vector3.up * 1.7f; return true; }
        if (RayReachesPlayer(eye, p + Vector3.up * 0.3f)) { seenPoint = p + Vector3.up * 0.3f; return true; }

        // Up close, throw a couple of lateral rays for odd angles / thin cover / low furniture.
        if (dist <= proximitySenseRadius)
        {
            Vector3 side = transform.right * 0.35f;
            if (RayReachesPlayer(eye, p + Vector3.up * 1.0f + side)) { seenPoint = p + Vector3.up * 1.0f + side; return true; }
            if (RayReachesPlayer(eye, p + Vector3.up * 1.0f - side)) { seenPoint = p + Vector3.up * 1.0f - side; return true; }
        }

        return false;
    }

    /// <summary>True if the ray from 'from' to 'to' is unobstructed, OR the first hit is the player.</summary>
    bool RayReachesPlayer(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        float d = dir.magnitude;
        if (d < 0.001f) return true;
        dir /= d;

        if (Physics.Raycast(from, dir, out RaycastHit hit, d, sightBlockingLayers, QueryTriggerInteraction.Ignore))
            return hit.transform == player || hit.transform.IsChildOf(player); // player = clear; wall = blocked

        return true; // nothing in the way
    }

    // ---------------------------------------------------------------
    // Doors
    // ---------------------------------------------------------------

    void TryOpenDoorIfBlocked()
    {
        if (phaseManager == null || !phaseManager.CanOpenDoors) return;
        if (!agent.enabled || !agent.isOnNavMesh) return;

        Vector3 moveDir = agent.desiredVelocity;
        if (moveDir.sqrMagnitude < 0.01f) return;
        moveDir.Normalize();

        Vector3 origin = transform.position + Vector3.up * (eyeHeight * 0.6f);
        if (Physics.Raycast(origin, moveDir, out RaycastHit hit, doorReach, doorLayers, QueryTriggerInteraction.Ignore))
        {
            Door door = hit.collider.GetComponentInParent<Door>();
            if (door != null && !door.IsOpen)
            {
                door.OpenForEnemy(transform.position);
                Log($"Pushed a door open: {door.name}.");
            }
        }
    }

    // ---------------------------------------------------------------
    // Hiding spots (called by EnemyHidingSpot)
    // ---------------------------------------------------------------

    /// <summary>True only when the Enemy is materialized and actively sensing.</summary>
    bool IsActivelyPresent =>
        currentState == State.Hunting || currentState == State.Chasing || currentState == State.Searching;

    /// <summary>Called by an EnemyHidingSpot when the player enters its trigger.</summary>
    public void PlayerEnteredHidingSpot(EnemyHidingSpot spot)
    {
        currentHidingSpot = spot;

        // Cover only fails if it's actually here, genuinely sees you right now (fresh check,
        // not a stale cached value), and is close enough to have tracked you in.
        bool watchingYou = false;
        if (IsActivelyPresent && player != null)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            watchingYou = dist <= hideBlownRange && CanSensePlayer(out _);
        }

        if (watchingYou)
        {
            playerConcealed = false;
            Report("You hid — but it's right on you, no good.");
            Log($"Hiding BLOWN — it's present, sees you now, and within {hideBlownRange}m.");
        }
        else
        {
            playerConcealed = true;
            Report("You slip into cover, unseen.");
            Log($"Concealed — {(IsActivelyPresent ? "it can't see you / too far" : "it isn't even around")}. It can't sense you now.");
        }
    }

    /// <summary>Called by an EnemyHidingSpot when the player leaves its trigger.</summary>
    public void PlayerLeftHidingSpot(EnemyHidingSpot spot)
    {
        if (spot != currentHidingSpot) return;
        if (playerConcealed) Log("You left cover — it can sense you again.");
        currentHidingSpot = null;
        playerConcealed = false;
    }

    void BlowConcealment(string reason)
    {
        playerConcealed = false;
        Report("Your cover is blown!");
        Log($"Concealment blown — {reason}.");
    }

    // ---------------------------------------------------------------
    // Catch / torch
    // ---------------------------------------------------------------

    void OnTriggerEnter(Collider other)
    {
        if (!catchOnTouch || player == null) return;
        if (other.transform != player && !other.transform.IsChildOf(player)) return;
        if (currentState == State.Cooldown || currentState == State.Withdrawn || currentState == State.Frozen) return;

        Report("CAUGHT!");
        Log("Player CAUGHT — firing OnPlayerCaught, then withdrawing.");
        OnPlayerCaught?.Invoke();
        BeginWithdrawal(phaseManager != null ? phaseManager.CooldownAfterEscape : 180f, true);
    }

    /// <summary>Wire EnemyTorch.OnTorchUsedSuccessfully to this.</summary>
    public void NotifyTorchUsed()
    {
        if (currentState == State.Cooldown || currentState == State.Withdrawn || currentState == State.Frozen)
        {
            Log($"Torch used but ignored — state is {currentState} (only affects an active Enemy).");
            return;
        }
        Report("Repelled by the torch!");
        Log("Torch used successfully — withdrawing.");
        BeginWithdrawal(phaseManager != null ? phaseManager.CooldownAfterTorch : 90f, true);
    }

    // ---------------------------------------------------------------
    // Scripted control
    // ---------------------------------------------------------------

    /// <summary>Bypasses cooldown/scoring. Materializes at the given point and starts hunting.</summary>
    public void ForceSpawn(Transform spawnPoint)
    {
        if (spawnPoint == null) { Log("ForceSpawn FAILED — argument was null."); return; }
        Log($"ForceSpawn — materializing at {spawnPoint.position}.");
        BeginSpawnAt(spawnPoint.position);
    }

    [ContextMenu("DEBUG: Force Spawn Near Player")]
    public void DebugForceSpawnNearPlayer()
    {
        if (player == null) { Log("DEBUG ForceSpawn FAILED — player is null."); return; }
        Vector3 p = player.position + player.forward * 5f;
        if (NavMesh.SamplePosition(p, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            BeginSpawnAt(hit.position);
        else
            Log($"DEBUG ForceSpawn FAILED — no NavMesh near {p}.");
    }

    /// <summary>Immediately withdraws. Pass false for the final-soul dissolution (stays gone).</summary>
    public void ForceWithdraw(bool startCooldown)
    {
        if (currentState == State.Cooldown || currentState == State.Withdrawn) return;
        Log($"ForceWithdraw (startCooldown={startCooldown}).");
        BeginWithdrawal(phaseManager != null ? phaseManager.CooldownAfterEscape : 180f, startCooldown);
    }

    // ---------------------------------------------------------------
    // Dialogue interrupt
    // ---------------------------------------------------------------

    bool wasOnCooldownWhenFrozen;

    /// <summary>Wire your dialogue-start event here.</summary>
    public void OnDialogueStarted()
    {
        if (currentState == State.Frozen) return;

        wasOnCooldownWhenFrozen = currentState == State.Cooldown;
        Log($"Dialogue started — freezing. Was on cooldown: {wasOnCooldownWhenFrozen} (state was {currentState}).");

        if (!wasOnCooldownWhenFrozen)
        {
            if (agent.enabled) { agent.isStopped = true; agent.enabled = false; }
            SetVisible(false);
            hasLOS = false;
        }
        SetState(State.Frozen);
    }

    /// <summary>Wire your dialogue-end event here.</summary>
    public void OnDialogueEnded()
    {
        if (currentState != State.Frozen) return;

        if (wasOnCooldownWhenFrozen)
        {
            Log($"Dialogue ended — resuming cooldown at {cooldownTimeRemaining:F1}s.");
            SetState(State.Cooldown);
        }
        else
        {
            Log("Dialogue ended — was active, starting a fresh cooldown.");
            StartCooldown(phaseManager != null ? phaseManager.CooldownAfterEscape : 180f);
        }
    }

    // ---------------------------------------------------------------
    // Helpers / display
    // ---------------------------------------------------------------

    void SetState(State s) => currentState = s;

    void Log(string message)
    {
        if (debugLogging) Debug.Log($"[Enemy] {message}");
    }

    /// <summary>Sets the event TMP and logs the same line.</summary>
    void Report(string message)
    {
        if (eventText != null) eventText.text = message;
        Log(message);
    }

    void SetVisible(bool visible)
    {
        if (bodyRenderers == null) return;
        for (int i = 0; i < bodyRenderers.Length; i++)
            if (bodyRenderers[i] != null) bodyRenderers[i].enabled = visible;
    }

    void UpdateStateText()
    {
        if (stateText == null) return;

        switch (currentState)
        {
            case State.Cooldown:
                stateText.text = cooldownTimeRemaining >= float.MaxValue
                    ? (blankStateWhenInactive ? "" : "Dormant")
                    : string.Format(cooldownFormat, Mathf.CeilToInt(cooldownTimeRemaining));
                break;
            case State.Spawning:
                stateText.text = string.Format(manifestingFormat, Mathf.CeilToInt(appearTimer));
                break;
            case State.Hunting: stateText.text = "Hunting"; break;
            case State.Chasing: stateText.text = "Chasing"; break;
            case State.Searching: stateText.text = playerConcealed ? "Searching (you're hidden)" : "Searching"; break;
            case State.Withdrawn:
            case State.Frozen:
                stateText.text = blankStateWhenInactive ? "" : currentState.ToString();
                break;
        }
    }

    void OnDrawGizmos()
    {
        if (!drawSightGizmo || !hasSightData) return;
        Gizmos.color = sightBlocked ? sightBlockedColor : sightClearColor;
        Gizmos.DrawLine(sightFrom, sightTo);
        Gizmos.DrawWireSphere(sightFrom, 0.15f);
        Gizmos.DrawWireSphere(sightTo, 0.15f);
    }

    void UpdateSightDebugLine()
    {
        if (sightDebugLine == null) return;
        sightDebugLine.positionCount = 2;
        sightDebugLine.SetPosition(0, sightFrom);
        sightDebugLine.SetPosition(1, sightTo);
        Color c = sightBlocked ? sightBlockedColor : sightClearColor;
        sightDebugLine.startColor = c;
        sightDebugLine.endColor = c;
    }
}