using UnityEngine;
using System.Collections;

public enum CreatureAggressionMode
{
    Neutral,
    Aggressive,
    Passive
}

public class WildCreatureAI : MonoBehaviour
{
    [Tooltip("Aggressive = attack on sight. Neutral = only attack if provoked. Passive = flee when provoked.")]
    public CreatureAggressionMode aggressionMode = CreatureAggressionMode.Neutral;

    public float roamRadius = 5f;
    public float detectionRadius = 5f;
    public float chaseSpeed = 2.5f;
    public float wanderSpeed = 1.5f;
    [Tooltip("1.0 = current speed, 0.33 = one third speed.")]
    public float movementSpeedMultiplier = 0.33333334f;
    public float attackRange = 0.8f;
    public float attackCooldown = 1.0f;
    public int contactDamage = 1;
    public bool spriteFacesRight = true;
    public float hopTilesPerMove = 2f;
    public float minHopTilesPerMove = 1f;
    public float maxHopTilesPerMove = 2f;
    public float hopCrouchTime = 0.14f;
    public float hopAirTime = 0.5f;
    public float hopLandTime = 0.12f;
    public float hopPauseTime = 0.10f;
    public float hopArcHeight = 0.18f;
    public float minWanderIdleTime = 0.6f;
    public float maxWanderIdleTime = 1.6f;
    public float wanderIdleChance = 0.35f;
    public float minWanderHopGap = 0.12f;
    public float maxWanderHopGap = 0.45f;

    private Vector2 spawnPos;
    private Vector2 wanderTarget;
    private float nextWanderTime;
    private float nextWanderHopTime;
    private float idleUntilTime;
    private float nextAttackTime;
    private Transform player;
    private PlayerHealth playerHealth;
    private CreatureHealth creatureHealth;
    private bool inBattle;
    private SpriteRenderer sr;
    private Vector3 baseScale = Vector3.one;
    private bool hopping;
    private Grid grid;
    private Vector2 lastMoveDir = Vector2.right;
    private bool provokedByPlayer;

    public bool IsHopping => hopping;

    void Awake()
    {
        hopTilesPerMove = 2f;
        if (GetComponent<CreatureGroundShadow>() == null)
        {
            gameObject.AddComponent<CreatureGroundShadow>();
        }
        if (maxHopTilesPerMove < minHopTilesPerMove) maxHopTilesPerMove = minHopTilesPerMove;
        spawnPos = transform.position;
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) baseScale = sr.transform.localScale;
        grid = FindAnyObjectByType<Grid>();
        creatureHealth = GetComponent<CreatureHealth>();
        if (creatureHealth != null)
        {
            creatureHealth.OnDamaged += HandleDamaged;
        }

        FaceTarget2D faceTarget = GetComponent<FaceTarget2D>();
        if (faceTarget != null)
        {
            // Wild movement controls facing. Prevent target-facing script override.
            faceTarget.enabled = false;
        }
    }

    void Start()
    {
        FindPlayer();
        PickNewWanderTarget();
    }

    void OnEnable()
    {
        provokedByPlayer = false;
        spawnPos = transform.position;
        idleUntilTime = 0f;
        nextWanderHopTime = 0f;
        PickNewWanderTarget();
    }

    void OnDestroy()
    {
        if (creatureHealth != null)
        {
            creatureHealth.OnDamaged -= HandleDamaged;
        }
    }

    void Update()
    {
        if (inBattle) return;
        if (hopping)
        {
            FaceMove(lastMoveDir);
            return;
        }

        if (player == null) FindPlayer();

        // During engaged battles, overworld creatures ignore the player completely.
        if (BattleSystem.IsEngagedBattleActive)
        {
            Wander();
            return;
        }

        if (player != null)
        {
            float dist = Vector2.Distance(transform.position, player.position);
            if (ShouldFleePlayer(dist))
            {
                FleeFromPlayer(dist);
                return;
            }

            if (ShouldChasePlayer(dist))
            {
                ChasePlayer(dist);
                return;
            }
        }

        Wander();
    }

    void FindPlayer()
    {
        GameObject go = GameObject.Find("Player");
        if (go != null)
        {
            player = go.transform;
            playerHealth = go.GetComponent<PlayerHealth>();
        }
    }

    void ChasePlayer(float dist)
    {
        Vector2 current = transform.position;
        Vector2 goal = player.position;
        Vector2 delta = goal - current;
        FaceMove(delta);

        float desired = Mathf.Max(0f, dist - attackRange * 0.8f);
        float step = Mathf.Min(desired, GetRandomHopDistanceWorld());
        if (step >= 0.05f)
        {
            StartCoroutine(HopTowards(SnapDirection(delta), step, false));
        }

        if (dist <= attackRange && Time.time >= nextAttackTime)
        {
            if (BattleSystem.IsEngagedBattleActive) return;
            if (!CanDealContactDamage()) return;
            nextAttackTime = Time.time + attackCooldown;
            if (playerHealth != null) playerHealth.TakeDamage(contactDamage);
        }
    }

    void FleeFromPlayer(float dist)
    {
        if (player == null) return;

        Vector2 delta = (Vector2)transform.position - (Vector2)player.position;
        FaceMove(delta);

        float desired = Mathf.Max(0.15f, detectionRadius - dist + 0.1f);
        float step = Mathf.Min(desired, GetRandomHopDistanceWorld());
        if (step >= 0.05f)
        {
            StartCoroutine(HopTowards(SnapDirection(delta), step, false));
        }
    }

    void Wander()
    {
        if (Time.time < idleUntilTime) return;
        if (Time.time < nextWanderHopTime) return;

        if (Time.time >= nextWanderTime || Vector2.Distance(transform.position, wanderTarget) < 0.1f)
        {
            PickNewWanderTarget();
        }

        Vector2 current = transform.position;
        Vector2 delta = wanderTarget - current;
        FaceMove(delta);

        float step = Mathf.Min(delta.magnitude, GetRandomHopDistanceWorld());
        if (step >= 0.05f)
        {
            StartCoroutine(HopTowards(SnapDirection(delta), step, true));
        }
        else
        {
            PickNewWanderTarget();
        }
    }

    void PickNewWanderTarget()
    {
        Vector2 offset = Random.insideUnitCircle * roamRadius;
        wanderTarget = spawnPos + offset;
        nextWanderTime = Time.time + Random.Range(1.5f, 3.5f);
    }

    void FaceMove(Vector2 delta)
    {
        if (sr == null) return;
        if (delta.sqrMagnitude <= 0.000001f) return;
        lastMoveDir = delta.normalized;

        if (Mathf.Abs(lastMoveDir.x) <= 0.0001f) return;

        bool movingLeft = lastMoveDir.x < 0f;
        sr.flipX = spriteFacesRight ? movingLeft : !movingLeft;
    }

    public void EnterBattle()
    {
        inBattle = true;
        StopAllCoroutines();
        hopping = false;
        nextWanderHopTime = Time.time + 9999f;
        idleUntilTime = Time.time + 9999f;
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        if (sr != null) sr.transform.localScale = baseScale;
    }

    public void ExitBattle()
    {
        inBattle = false;
        idleUntilTime = Time.time;
        nextWanderHopTime = Time.time;
    }

    public bool IsAlive()
    {
        return creatureHealth == null || creatureHealth.currentHealth > 0;
    }

    public bool IsInBattle()
    {
        return inBattle;
    }

    public void ForceStop()
    {
        StopAllCoroutines();
        hopping = false;
        if (sr != null) sr.transform.localScale = baseScale;
    }

    // For future weapon hits from player scripts.
    public void NotifyDamagedByPlayer()
    {
        provokedByPlayer = true;
    }

    IEnumerator HopTowards(Vector2 dir, float step, bool isWanderHop)
    {
        hopping = true;
        Vector3 startScale = baseScale;
        float speedScale = 1f / Mathf.Max(0.01f, movementSpeedMultiplier);
        float crouchTime = hopCrouchTime * speedScale;
        float airTime = hopAirTime * speedScale;
        float landTime = hopLandTime * speedScale;
        float pauseTime = hopPauseTime * speedScale;

        yield return AnimateScale(startScale, new Vector3(startScale.x * 1.08f, startScale.y * 0.86f, startScale.z), crouchTime);

        Vector3 startPos = transform.position;
        Vector3 endPos = new Vector3(startPos.x + dir.x * step, startPos.y + dir.y * step, startPos.z);
        Vector3 stretch = new Vector3(startScale.x * 0.92f, startScale.y * 1.10f, startScale.z);
        Vector3 prevPos = startPos;

        float t = 0f;
        while (t < airTime)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / airTime);
            float eased = Mathf.SmoothStep(0f, 1f, u);
            Vector3 flat = Vector3.Lerp(startPos, endPos, eased);
            float arc = 4f * u * (1f - u) * hopArcHeight;
            Vector3 curPos = new Vector3(flat.x, flat.y + arc, flat.z);
            transform.position = curPos;
            Vector3 frameDelta = curPos - prevPos;
            if (frameDelta.sqrMagnitude > 0.0000001f)
            {
                FaceMove(new Vector2(frameDelta.x, frameDelta.y));
            }
            prevPos = curPos;
            if (sr != null) sr.transform.localScale = Vector3.Lerp(startScale, stretch, Mathf.Sin(u * Mathf.PI));
            yield return null;
        }
        transform.position = endPos;
        FaceMove(new Vector2(endPos.x - startPos.x, endPos.y - startPos.y));

        yield return AnimateScale(sr != null ? sr.transform.localScale : startScale, new Vector3(startScale.x * 1.05f, startScale.y * 0.90f, startScale.z), landTime);
        yield return AnimateScale(sr != null ? sr.transform.localScale : startScale, startScale, pauseTime);
        hopping = false;

        if (isWanderHop)
        {
            nextWanderHopTime = Time.time + Random.Range(minWanderHopGap, maxWanderHopGap);
            if (Random.value < wanderIdleChance)
            {
                idleUntilTime = Time.time + Random.Range(minWanderIdleTime, maxWanderIdleTime);
            }
        }
    }

    IEnumerator AnimateScale(Vector3 from, Vector3 to, float duration)
    {
        if (sr == null || duration <= 0f) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            sr.transform.localScale = Vector3.Lerp(from, to, u);
            yield return null;
        }
        sr.transform.localScale = to;
    }

    float GetHopDistanceWorld(float tilesPerMove)
    {
        float tile = 1f;
        if (grid == null) grid = FindAnyObjectByType<Grid>();
        if (grid != null)
        {
            tile = Mathf.Abs(grid.cellSize.x * grid.transform.lossyScale.x);
            if (tile < 0.0001f) tile = 1f;
        }
        return Mathf.Max(0.01f, tilesPerMove) * tile;
    }

    float GetRandomHopDistanceWorld()
    {
        float minTiles = Mathf.Max(0.1f, minHopTilesPerMove);
        float maxTiles = Mathf.Max(minTiles, maxHopTilesPerMove);
        float tiles = Random.Range(minTiles, maxTiles);
        return GetHopDistanceWorld(tiles);
    }

    Vector2 SnapDirection(Vector2 raw)
    {
        if (raw.sqrMagnitude <= 0.0001f) return Vector2.right;
        return raw.normalized;
    }

    bool ShouldChasePlayer(float dist)
    {
        if (dist > detectionRadius) return false;

        switch (aggressionMode)
        {
            case CreatureAggressionMode.Aggressive:
                return true;
            case CreatureAggressionMode.Neutral:
                return provokedByPlayer;
            case CreatureAggressionMode.Passive:
                return false;
            default:
                return false;
        }
    }

    bool ShouldFleePlayer(float dist)
    {
        if (aggressionMode != CreatureAggressionMode.Passive) return false;
        if (!provokedByPlayer) return false;
        return dist <= detectionRadius * 1.25f;
    }

    bool CanDealContactDamage()
    {
        switch (aggressionMode)
        {
            case CreatureAggressionMode.Aggressive:
                return true;
            case CreatureAggressionMode.Neutral:
                return provokedByPlayer;
            case CreatureAggressionMode.Passive:
                return false;
            default:
                return false;
        }
    }

    void HandleDamaged(CreatureHealth _, int __)
    {
        if (aggressionMode == CreatureAggressionMode.Aggressive) return;
        provokedByPlayer = true;
    }
}
