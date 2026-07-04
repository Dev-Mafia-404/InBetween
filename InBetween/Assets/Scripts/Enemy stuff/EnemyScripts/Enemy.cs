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
    [Tooltip("When it can SEE you but can't physically reach you (you climbed onto furniture), it plants itself at the closest reachable spot and stares instead of drifting off.")]
    public bool holdAndStareWhenUnreachable = true;
    [Tooltip("How high above the nearest floor you must be for it to count as 'climbed onto something' (so it stares instead of trying to catch).")]
    public float climbHeightThreshold = 0.5f;
    [Tooltip("When approaching an out-of-reach you, it stops staring-distance away from the closest floor spot beneath you.")]
    public float stareApproachDistance = 1.5f;
    [Tooltip("How far it searches for a floor spot beneath/near you when you're off the navmesh.")]
    public float playerNavSampleRadius = 2f;

    [Header("Facing / Turning")]
    [Tooltip("How fast it turns to face its movement direction while walking/running (degrees/sec). Higher = snappier turns.")]
    public float moveTurnSpeed = 400f;
    [Tooltip("How fast it turns to face you while standing and staring (degrees/sec). Higher = snaps to you faster. NOTE: raise this on the component in your scene — the serialized value won't auto-update.")]
    public float stareTurnSpeed = 720f;
    [Tooltip("While planted and watching you on furniture, it holds (and keeps facing you) through brief sight flickers for this long before deciding you've really gone.")]
    public float watchHoldTime = 2f;
    [Tooltip("How fast it turns while SEARCHING (degrees/sec). Keep this near its search move speed so slow walking doesn't look like it's gliding/sliding. Lower = gentler turns.")]
    public float searchTurnSpeed = 160f;

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
    [Tooltip("Only if the Enemy is present, has a clear look at you, AND is within this distance the instant you enter does hiding fail (it catches you). Otherwise you hide and STAY hidden until you leave. Lower = more lenient. 1-6 is the useful range; ~4 is a good start.")]
    public float hideBlownRange = 4f;

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
    [Tooltip("Plays the materialization SFX when it appears.")]
    public AudioSource appearAudioSource;
    public AudioClip appearClip;
    [Tooltip("Source used for footstep one-shots. Ideally a 3D (spatial) AudioSource on the creature.")]
    public AudioSource footstepAudioSource;
    [Tooltip("Footstep clips — one is picked at random per step. Leave empty to disable footsteps.")]
    public AudioClip[] footstepClips;
    [Tooltip("Distance travelled between footstep sounds. Smaller = more frequent steps.")]
    public float footstepStride = 1.6f;
    [Tooltip("Source used for growls/vocals on state changes.")]
    public AudioSource voiceAudioSource;
    [Tooltip("Optional one-shot when it manifests.")]
    public AudioClip appearVoiceClip;
    public AudioClip huntStartClip;
    public AudioClip chaseStartClip;
    public AudioClip searchStartClip;
    [Tooltip("Plays when it catches the player.")]
    public AudioClip caughtClip;
    [Tooltip("Source for the LOOPING per-state ambience (growl/breathing). One AudioSource, loop is handled in code.")]
    public AudioSource stateLoopSource;
    [Tooltip("Looping clip while hunting. Leave empty for none.")]
    public AudioClip huntLoopClip;
    [Tooltip("Looping clip while chasing. Leave empty for none.")]
    public AudioClip chaseLoopClip;
    [Tooltip("Looping clip while searching. Leave empty for none.")]
    public AudioClip searchLoopClip;

    [Header("Animation (optional)")]
    [Tooltip("The creature's Animator. Leave empty to skip all animation calls.")]
    public Animator animator;
    [Tooltip("Float parameter driven by move speed (for an idle/walk/run blend). Blank to skip.")]
    public string speedParam = "Speed";
    [Tooltip("Int parameter for the state: 0 Idle, 1 Hunting, 2 Chasing, 3 Searching, 4 Watching. Blank to skip.")]
    public string stateParam = "State";

    [Header("Torch Response")]
    [Tooltip("The Animator STATE name to play when torched. Played DIRECTLY by name (CrossFade), so it does NOT rely on any trigger/transition setup — just make a state with this exact name holding your torched clip.")]
    public string torchedStateName = "Torched";
    [Tooltip("How long the torched animation plays before it starts to fade out and leave.")]
    public float torchedPlayTime = 1.5f;

    [Header("Direct Animation States (played by name — no transition wiring needed)")]
    [Tooltip("Animator state name for the watching/staring clip. Played directly when it plants and stares at you on furniture.")]
    public string watchingStateName = "Watching";

    [Header("Visibility")]
    [Tooltip("The creature's visual GameObject (your animating mesh child). Toggled off while gone, on once it manifests. Leave empty for no toggling.")]
    public GameObject enemyVisual;
    [Tooltip("Keep the visual hidden through the whole appear window (shown only once it starts hunting).")]
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

    bool hasLOS;
    Vector3 sightFrom, sightTo;
    bool sightBlocked, hasSightData;

    float huntTimer, huntDuration;
    float keepChasingTimer;
    bool inLostSightGrace;

    float searchTimer, searchPauseTimer, searchLogTimer;
    Vector3 searchCenter;
    bool reachedSearchCenter;

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

    // facing / audio / anim
    bool isWatching;
    float watchLostTimer;
    float footstepDistance;
    int lastAnimState = -1;
    NavMeshPath reusablePath;
    float searchStuckTimer;
    AudioClip currentLoopClip;
    float withdrawPreDelayTimer;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.enabled = false;
        reusablePath = new NavMeshPath();

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
        if (agent != null)
        {
            agent.angularSpeed = moveTurnSpeed; // snappier turns while moving
            agent.updateRotation = true;        // face movement direction by default
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
        UpdateFootsteps();
        UpdateAnimator();
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
        SetStateLoop(null);
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

        ClearWatching();
        PlayVoice(appearVoiceClip);
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
        if (agent != null) agent.angularSpeed = moveTurnSpeed;
        ClearWatching();
        SetState(State.Hunting);
        Report("Hunting — closing in.");
        Log($"Hunting for up to {huntDuration:F1}s.");
        PlayVoice(huntStartClip);
        SetStateLoop(huntLoopClip);
        OnHuntingStarted?.Invoke();
    }

    void TickHunting()
    {
        if (player == null) return;

        // While concealed it can't sense you, so it heads to the STALE last-known spot only.
        if (!playerConcealed) lastKnownPlayerPosition = player.position;

        MoveTowards(lastKnownPlayerPosition);
        TryOpenDoorIfBlocked();

        // If jammed against geometry while hunting, go to search early.
        if (agent.enabled && agent.velocity.sqrMagnitude < 0.04f && !agent.pathPending)
        {
            searchStuckTimer += Time.deltaTime;
            if (searchStuckTimer >= 0.5f)
            {
                searchStuckTimer = 0f;
                Log("Stuck during hunting — can't reach target. Switching to search.");
                BeginSearching(lastKnownPlayerPosition, "jammed against geometry while approaching");
                return;
            }
        }
        else
        {
            searchStuckTimer = 0f;
        }

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
        if (agent != null) agent.angularSpeed = moveTurnSpeed;
        inLostSightGrace = false;
        ClearWatching();
        SetState(State.Chasing);
        Report(fromSearch ? "Spotted you again — chase on!" : "Spotted you — chase on!");
        Log($"Chasing (LOS acquired, speed={agent.speed:F1}).");
        PlayVoice(chaseStartClip);
        SetStateLoop(chaseLoopClip);
        OnChaseStarted?.Invoke();
    }

    void TickChasing()
    {
        if (player == null) return;

        UpdateSightCheck();

        // Planted and watching an out-of-reach player (you're on furniture): hold through brief
        // sight flickers so it stays locked on instead of jittering off to the side.
        if (isWatching)
        {
            if (hasLOS)
            {
                watchLostTimer = 0f;
                lastKnownPlayerPosition = player.position;
            }
            else
            {
                watchLostTimer += Time.deltaTime;
                if (watchLostTimer >= watchHoldTime)
                {
                    ClearWatching();
                    BeginSearching(lastKnownPlayerPosition, "you were up on something, then dropped out of sight");
                    return;
                }
            }
            PursueOrStare(); // keeps staring, or resumes the chase if you came back down
            return;
        }

        if (hasLOS)
        {
            if (inLostSightGrace)
            {
                inLostSightGrace = false;
                Report("Back in sight — still chasing!");
            }
            lastKnownPlayerPosition = player.position;
            PursueOrStare();
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

        ClearWatching(); // back to moving/searching → face travel direction again
        MoveTowards(lastKnownPlayerPosition);
        TryOpenDoorIfBlocked();

        // If it's trying to reach the last-known spot but is jammed against geometry, skip ahead
        // to searching instead of standing there with the chase animation gliding into a wall.
        if (agent.enabled && agent.velocity.sqrMagnitude < 0.04f && !agent.pathPending)
        {
            searchStuckTimer += Time.deltaTime;
            if (searchStuckTimer >= 0.5f)
            {
                searchStuckTimer = 0f;
                Log("Stuck during grace chase — can't reach last known spot. Switching to search.");
                BeginSearching(lastKnownPlayerPosition, "jammed against geometry while heading to your last spot");
                return;
            }
        }
        else
        {
            searchStuckTimer = 0f;
        }

        keepChasingTimer -= Time.deltaTime;
        if (keepChasingTimer <= 0f)
            BeginSearching(lastKnownPlayerPosition, "lost sight and the grace chase ran out");
    }

    // ---------------------------------------------------------------
    // Searching (dynamic wander)
    // ---------------------------------------------------------------

    void BeginSearching(Vector3 position, string reasonForContext)
    {
        // If you're tucked in a hiding spot, look right around that spot.
        if (playerConcealed && currentHidingSpot != null)
            position = currentHidingSpot.transform.position;

        // Make sure the last-known spot is actually on the navmesh, so it can get there.
        if (NavMesh.SamplePosition(position, out NavMeshHit sc, 3f, NavMesh.AllAreas))
            searchCenter = sc.position;
        else
            searchCenter = position;

        searchTimer = phaseManager != null ? phaseManager.SearchTime : 15f;
        searchPauseTimer = 0f;
        searchLogTimer = 0f;
        reachedSearchCenter = false;
        inLostSightGrace = false;

        SetSpeed(searchSpeed);
        if (agent != null) agent.angularSpeed = searchTurnSpeed; // gentler turns so slow walk doesn't glide
        ClearWatching();
        MoveTowards(searchCenter);
        SetState(State.Searching);
        Report(playerConcealed ? "Searching… (it can't sense you)" : "Searching your last known area.");
        Log($"Searching for {searchTimer:F1}s. Context: {reasonForContext}. Concealed={playerConcealed}.");
        PlayVoice(searchStartClip);
        SetStateLoop(searchLoopClip);
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
            BeginWithdrawal(phaseManager != null ? phaseManager.CooldownAfterEscape : 180f, true, walkAway: true);
            return;
        }

        bool arrived = agent.enabled && agent.isOnNavMesh && !agent.pathPending &&
                       agent.remainingDistance <= searchArriveDistance;

        // First, actually get to your last known spot before wandering off it.
        if (!reachedSearchCenter)
        {
            if (arrived || !CanReach(searchCenter))
            {
                reachedSearchCenter = true;
                searchPauseTimer = Random.Range(searchPauseMin, searchPauseMax) * 1.5f; // pause & look here first
            }
            TryOpenDoorIfBlocked();
            return;
        }

        // Then wander nearby: move to a point, pause to 'look', pick another.
        if (arrived)
        {
            searchStuckTimer = 0f;
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
        else if (agent.enabled && agent.velocity.sqrMagnitude < 0.04f)
        {
            // Trying to move but not actually moving = jammed against geometry. Re-route so it
            // doesn't stand there gliding its walk animation into a wall.
            searchStuckTimer += Time.deltaTime;
            if (searchStuckTimer >= 0.7f)
            {
                searchStuckTimer = 0f;
                if (RandomSearchPoint(out Vector3 point)) MoveTowards(point);
            }
        }
        else
        {
            searchStuckTimer = 0f;
        }

        TryOpenDoorIfBlocked();
    }

    void HandleNoiseEvent(Vector3 noisePosition, float reach)
    {
        if (playerConcealed) return; // hidden = it can't hear you either
        if (currentState != State.Searching) return;

        if (Vector3.Distance(noisePosition, transform.position) <= reach)
        {
            Report("Heard something — checking it out.");
            Log($"Noise heard at {noisePosition} (reach={reach:F0}). Redirecting search (timer not reset).");
            searchCenter = noisePosition;
            searchPauseTimer = 0f;
            reachedSearchCenter = false; // go to the noise first, then poke around it
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

    void BeginWithdrawal(float nextCooldown, bool startCooldownAfter, bool walkAway = false)
    {
        disappearTimer = disappearTime;
        pendingCooldown = nextCooldown;
        pendingStartCooldown = startCooldownAfter;
        ClearWatching();
        SetStateLoop(null);

        // walkAway = drift off instead of freezing in place (so it doesn't look stuck at a wall
        // while the fade plays). Used when it gives up the search. Torch/catch stand still so their
        // trigger animation reads clearly.
        if (walkAway && agent.enabled && agent.isOnNavMesh && player != null)
        {
            Vector3 away = transform.position - player.position; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            Vector3 dest = transform.position + away.normalized * 6f;
            if (NavMesh.SamplePosition(dest, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                MoveTowards(hit.position);
        }
        else if (agent.enabled)
        {
            agent.isStopped = true;
        }

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
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, stareTurnSpeed * Time.deltaTime);
    }

    void SetAgentAutoRotate(bool on)
    {
        if (agent != null && agent.updateRotation != on) agent.updateRotation = on;
    }

    /// <summary>Stop watching-mode and hand rotation back to the agent (face travel direction).</summary>
    void ClearWatching()
    {
        if (isWatching) lastAnimState = -1; // force UpdateAnimator to re-set State on the next frame
        isWatching = false;
        SetAgentAutoRotate(true);
    }

    /// <summary>
    /// While it can see you: chase to catch if you're on the ground, otherwise approach the
    /// closest floor spot beneath you and plant + stare. Never routes through a wall to reach a
    /// closer-but-wrong-side floor point.
    /// </summary>
    void PursueOrStare()
    {
        if (!holdAndStareWhenUnreachable)
        {
            ClearWatching();
            MoveTowards(player.position);
            TryOpenDoorIfBlocked();
            return;
        }

        if (TryResolveChaseTarget(out Vector3 target, out bool grounded))
        {
            if (grounded)
            {
                // You're on the floor — chase to catch, no staring.
                ClearWatching();
                MoveTowards(target);
                TryOpenDoorIfBlocked();
                return;
            }

            // You're up on something. Approach the floor beneath you, then plant and stare.
            // Once staring, KEEP staring (hysteresis) so it doesn't jitter between the two.
            float d = Vector3.Distance(transform.position, target);
            bool closeEnough = d <= stareApproachDistance;
            bool cantGetCloser = !CanReach(target);

            if (isWatching || closeEnough || cantGetCloser)
                HoldAndStare();
            else
            {
                ClearWatching();
                MoveTowards(target);
                TryOpenDoorIfBlocked();
            }
        }
        else
        {
            HoldAndStare(); // no valid same-side floor spot (behind a wall / off-mesh)
        }
    }

    bool CanReach(Vector3 dest)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        if (!agent.CalculatePath(dest, reusablePath)) return false;
        return reusablePath.status == NavMeshPathStatus.PathComplete;
    }

    bool TryResolveChaseTarget(out Vector3 target, out bool grounded)
    {
        target = transform.position;
        grounded = false;
        if (player == null) return false;

        if (!NavMesh.SamplePosition(player.position, out NavMeshHit hit, playerNavSampleRadius, NavMesh.AllAreas))
            return false;

        Vector3 nav = hit.position;

        // The floor spot must be able to actually see you — stops it walking around into the next
        // room (e.g. the bathroom) to reach a closer-but-wrong-side navmesh point.
        if (!RayReachesPlayer(nav + Vector3.up * 0.3f, player.position + Vector3.up * 0.5f))
            return false;

        float heightAbove = player.position.y - nav.y;
        Vector3 flat = nav - player.position; flat.y = 0f;
        grounded = flat.magnitude < 0.75f && heightAbove < climbHeightThreshold;

        target = grounded ? player.position : nav;
        return true;
    }

    void HoldAndStare()
    {
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero; // plant firmly, no slow drift
        }
        SetAgentAutoRotate(false);

        if (!isWatching)
        {
            isWatching = true;
            watchLostTimer = 0f;
            SnapFacePlayer(); // instant lock-on the moment it plants — no slow turn

            // Force-play the watching animation directly by name (same pattern as Torched).
            // Set State to -1 so no "Any State → X" transition can override it.
            if (animator != null && animator.isActiveAndEnabled)
            {
                if (!string.IsNullOrEmpty(stateParam))
                {
                    animator.SetInteger(stateParam, -1);
                    lastAnimState = -1;
                }
                if (!string.IsNullOrEmpty(watchingStateName))
                    animator.CrossFade(watchingStateName, 0.15f, 0, 0f);
            }

            Report("It plants itself and watches you.");
        }
        else
        {
            FacePlayer(); // smooth-track if you move around up there
        }
    }

    void SnapFacePlayer()
    {
        if (player == null) return;
        Vector3 flat = player.position - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.LookRotation(flat.normalized);
    }

    void PlayVoice(AudioClip clip)
    {
        if (voiceAudioSource != null && clip != null) voiceAudioSource.PlayOneShot(clip);
    }

    void UpdateFootsteps()
    {
        if (footstepAudioSource == null || footstepClips == null || footstepClips.Length == 0) return;
        if (agent == null || !agent.enabled) { footstepDistance = 0f; return; }

        float v = agent.velocity.magnitude;
        if (v < 0.2f) return; // basically standing still

        footstepDistance += v * Time.deltaTime;
        if (footstepDistance >= footstepStride)
        {
            footstepDistance = 0f;
            AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
            if (clip != null) footstepAudioSource.PlayOneShot(clip);
        }
    }

    int CurrentAnimState()
    {
        if (isWatching) return 4;
        switch (currentState)
        {
            case State.Hunting: return 1;
            case State.Chasing: return 2;
            case State.Searching: return 3;
            default: return 0;
        }
    }

    void UpdateAnimator()
    {
        if (animator == null || !animator.isActiveAndEnabled) return;

        // While watching, the CrossFade owns the Animator entirely — don't touch any parameters.
        if (isWatching) return;

        if (!string.IsNullOrEmpty(speedParam))
            animator.SetFloat(speedParam, (agent != null && agent.enabled) ? agent.velocity.magnitude : 0f);

        // Don't drive the State int during Spawning/Withdrawn — those states own the
        // Animator via CrossFade. (Watching also skips via the early return above.)
        if (!string.IsNullOrEmpty(stateParam) &&
            currentState != State.Spawning && currentState != State.Withdrawn)
        {
            int s = CurrentAnimState();
            if (s != lastAnimState)
            {
                lastAnimState = s;
                animator.SetInteger(stateParam, s);
            }
        }
    }

    void SetStateLoop(AudioClip clip)
    {
        if (stateLoopSource == null || currentLoopClip == clip) return;
        currentLoopClip = clip;
        if (clip == null) { stateLoopSource.Stop(); return; }
        stateLoopSource.clip = clip;
        stateLoopSource.loop = true;
        stateLoopSource.Play();
    }

    void UpdateSightCheck()
    {
        if (player == null) { hasLOS = false; return; }

        Vector3 eye = transform.position + Vector3.up * eyeHeight;

        // Hidden in a spot: normally can't be sensed — BUT the enemy can DISCOVER you if it gets
        // within hideBlownRange with a clear line of sight. hideBlownRange is the perception dial:
        //   <=3  practically blind (must be right on top of you)   4-6 perceptive   6+ hard to hide.
        // Hidden in a spot = simply can't be sensed. You stay hidden until you leave cover.
        if (playerConcealed)
        {
            if (hasLOS) Log("Sight lost — you're hidden.");
            hasLOS = false;
            sightFrom = eye;
            sightTo = player.position + Vector3.up * 1.0f;
            sightBlocked = true;
            hasSightData = true;
            UpdateSightDebugLine();
            return;
        }

        bool sensed = RawSightToPlayer(out Vector3 seenPoint);

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
    /// Raw multi-sample perception, ignoring concealment. Casts rays up the player's body (and a
    /// couple of lateral ones up close), bailing on the first clear one. A ray is clear if it
    /// reaches the player OR the first thing it hits IS the player — so the player's own collider
    /// never blocks it and point-blank always registers.
    /// </summary>
    bool RawSightToPlayer(out Vector3 seenPoint)
    {
        seenPoint = player != null ? player.position + Vector3.up * 1.0f : transform.position;
        if (player == null) return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > maxSightRange) return false;

        Vector3 p = player.position;

        if (RayReachesPlayer(eye, p + Vector3.up * 1.0f)) { seenPoint = p + Vector3.up * 1.0f; return true; }
        if (RayReachesPlayer(eye, p + Vector3.up * 1.7f)) { seenPoint = p + Vector3.up * 1.7f; return true; }
        if (RayReachesPlayer(eye, p + Vector3.up * 0.3f)) { seenPoint = p + Vector3.up * 0.3f; return true; }

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

        // Simple rule: it catches you ONLY if it's present, has a CLEAN look at you the instant you
        // enter (one straight line to your chest — partial/edge visibility does NOT count), and is
        // within hideBlownRange. Not present / didn't clearly see you / too far  =  you hide, and stay
        // hidden until you leave (even during cooldown or before it has manifested).
        bool blown = IsActivelyPresent && player != null &&
                     Vector3.Distance(transform.position, player.position) <= hideBlownRange &&
                     HasCleanLineToPlayer();

        playerConcealed = !blown;

        if (blown)
        {
            Report("You hid — but it's right on you, no good.");
            Log($"Hiding BLOWN — present, sees you now, within {hideBlownRange}m.");
        }
        else
        {
            // Push the last-known position OUTSIDE the keep-out zone so the enemy doesn't path
            // through the trigger trying to reach where you were standing a moment ago.
            if (currentHidingSpot != null)
            {
                Vector3 spotCenter = currentHidingSpot.transform.position;
                float r = currentHidingSpot.KeepOutRadius;
                Vector3 awayDir = (lastKnownPlayerPosition - spotCenter);
                awayDir.y = 0f;
                if (awayDir.sqrMagnitude < 0.01f) awayDir = (transform.position - spotCenter);
                awayDir.y = 0f;
                if (awayDir.sqrMagnitude < 0.01f) awayDir = Vector3.forward;
                awayDir.Normalize();
                Vector3 edgePoint = spotCenter + awayDir * (r + 1f);
                if (NavMesh.SamplePosition(edgePoint, out NavMeshHit edgeHit, r + 2f, NavMesh.AllAreas))
                    lastKnownPlayerPosition = edgeHit.position;
            }

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

    /// <summary>Strict line of sight for the hiding check ONLY. Returns true only if the line from the
    /// enemy's eye to the player's chest hits nothing except possibly the player itself. If a wall,
    /// doorframe, or furniture is in the way, it counts as "didn't see you."
    /// This is deliberately MORE lenient than combat perception (single ray, chest only), so partial
    /// cover lets you hide.</summary>
    bool HasCleanLineToPlayer()
    {
        if (player == null) return false;
        Vector3 from = transform.position + Vector3.up * eyeHeight;
        Vector3 to = player.position + Vector3.up * 1.0f;
        Vector3 dir = to - from;
        float d = dir.magnitude;
        if (d < 0.001f) return true;
        dir /= d;

        // Exclude the player's own layer so the player's collider doesn't count as an obstruction.
        // If a wall or furniture is hit first, it returns false (didn't see you).
        int playerLayer = player.gameObject.layer;
        LayerMask mask = sightBlockingLayers & ~(1 << playerLayer);

        return !Physics.Raycast(from, dir, d, mask, QueryTriggerInteraction.Ignore);
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
        PlayVoice(caughtClip);
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
        Log("Torch used — playing torched animation, then withdrawing.");
        BeginTorchedWithdrawal();
    }

    /// <summary>
    /// Torch-only exit: force-plays the torched animation state DIRECTLY by name (no trigger or
    /// transition wiring needed), holds it for torchedPlayTime while standing still, THEN fades out
    /// and leaves. Just make an Animator state whose name matches torchedStateName holding your clip.
    /// </summary>
    void BeginTorchedWithdrawal()
    {
        ClearWatching();
        SetStateLoop(null);
        if (agent.enabled) agent.isStopped = true;

        if (animator != null && animator.isActiveAndEnabled)
        {
            // Set State to -1 (matches no transition condition) so the Animator's "Any State → X"
            // transitions can't fire and override the CrossFade. This is why it was snapping back
            // to Chasing — State was still 2 from the chase, so "Any State → Chasing (State==2)"
            // kept winning over the CrossFade.
            if (!string.IsNullOrEmpty(stateParam))
            {
                animator.SetInteger(stateParam, -1);
                lastAnimState = -1;
            }

            if (!string.IsNullOrEmpty(torchedStateName))
                animator.CrossFade(torchedStateName, 0.1f, 0, 0f);
        }

        pendingCooldown = phaseManager != null ? phaseManager.CooldownAfterTorch : 90f;
        pendingStartCooldown = true;
        disappearTimer = Mathf.Max(0.1f, torchedPlayTime); // show the torched clip, THEN vanish
        SetState(State.Withdrawn);
        Log($"Torched — playing '{torchedStateName}' for {torchedPlayTime:F1}s, then leaving.");
        OnWithdrawalStarted?.Invoke();
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
        if (enemyVisual != null) enemyVisual.SetActive(visible);
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
            case State.Chasing: stateText.text = isWatching ? "Watching you" : "Chasing"; break;
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