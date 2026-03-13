using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class OverworldCreatureSpawner : MonoBehaviour
{
    private static readonly string[] DebugAllStageCreatureIds =
    {
        "whelpling",
        "ashcub",
        "strikeling",
        "emberclaw",
        "frostcharge",
        "voidmane",
        "galecrown",
        "solnox_the_eternal",
        "zypheron_the_unyielding"
    };

    [Tooltip("How often the spawner checks if new wild creatures should be added.")]
    [Min(0.1f)]
    public float spawnCheckInterval = 0.5f;

    [Tooltip("Minimum distance from player when placing a new wild creature.")]
    [Min(0f)]
    public float minSpawnDistanceFromPlayer = 4f;

    [Tooltip("Maximum distance from player when placing a new wild creature.")]
    [Min(1f)]
    public float maxSpawnDistanceFromPlayer = 24f;

    [Tooltip("How many random position attempts per spawn check.")]
    [Min(1)]
    public int spawnPositionAttempts = 24;

    [Tooltip("World-space clearance radius used to avoid spawning inside colliders.")]
    [Min(0f)]
    public float spawnClearanceRadius = 0f;

    [Tooltip("Optional explicit template. If empty, one is discovered in scene.")]
    public WildCreatureAI fallbackTemplate;

    [Tooltip("Use local system hour for day/night filtering.")]
    public bool useSystemClockForDayNight = true;

    [Tooltip("Manual day/night when system clock mode is disabled.")]
    public SpawnTimeOfDay manualTimeOfDay = SpawnTimeOfDay.Day;

    [Range(0, 23)]
    public int nightStartsAtHour = 18;

    [Range(0, 23)]
    public int dayStartsAtHour = 6;

    [Tooltip("If enabled, cleans up very distant wild creatures.")]
    public bool despawnFarCreatures;

    [Min(5f)]
    public float farDespawnDistance = 40f;

    [Tooltip("Deprecated: when false, AreaSpawnConfig remains the authoritative spawn source.")]
    public bool enforceProgressiveStageOdds = false;

    private readonly List<WildCreatureAI> activeWilds = new List<WildCreatureAI>();
    private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    private readonly HashSet<string> missingSpriteCache = new HashSet<string>();

    private WildCreatureAI discoveredTemplate;
    private Transform player;
    private float nextCheckTime;
    private float nextSpawnAllowedTime;
    private int preparedConfigInstanceId = int.MinValue;

    void OnEnable()
    {
        TryFindPlayer();
        RefreshActiveWildList();
        SyncManagerCount();
    }

    void Update()
    {
        if (!SpawnManager.HasInstance) return;
        if (Time.time < nextCheckTime) return;
        nextCheckTime = Time.time + Mathf.Max(0.1f, spawnCheckInterval);

        EnsureActiveZoneReady();
        TryFindPlayer();
        RefreshActiveWildList();
        SyncManagerCount();
        TryDespawnFarCreatures();
        TrySpawnIntoActiveZone();
    }

    void TrySpawnIntoActiveZone()
    {
        SpawnZone zone = SpawnManager.Instance.ActiveZone;
        if (zone == null || !zone.isActive) return;

        AreaSpawnConfig config = zone.GetEffectiveConfig();
        if (config == null) return;

        if (!CanSpawnAtCurrentTime(config)) return;
        if (Time.time < nextSpawnAllowedTime) return;

        int cap = Mathf.Max(1, config.maxActiveCreatures);
        if (activeWilds.Count >= cap) return;

        if (!TryPickSpawnData(config, out CreatureEncounterData data)) return;
        if (!TryGetSpawnPoint(zone, out Vector3 spawnPos)) return;
        if (!TryCreateWildCreature(data, spawnPos, zone.zoneID))
        {
            nextSpawnAllowedTime = Time.time + 0.35f;
            return;
        }

        float cooldown = Mathf.Max(0f, config.respawnCooldownSeconds);
        nextSpawnAllowedTime = Time.time + Mathf.Max(0.15f, cooldown);
    }

    void EnsureProgressiveSpawnOdds(AreaSpawnConfig config)
    {
        if (config == null) return;
        int configId = config.GetInstanceID();
        if (preparedConfigInstanceId == configId) return;
        preparedConfigInstanceId = configId;

        if (config.mainPool == null) config.mainPool = new List<CreatureSpawnEntry>();
        if (config.rarePool == null) config.rarePool = new List<CreatureSpawnEntry>();
        config.mainPool.Clear();
        config.rarePool.Clear();
        config.rareEventChance = 0f;

        // Stage 1: equal odds between Whelpling, Ashcub, Strikeling.
        AddSpawn(config.mainPool, "whelpling", 30, 2, 6, CreatureRarity.Common);
        AddSpawn(config.mainPool, "ashcub", 30, 2, 6, CreatureRarity.Common);
        AddSpawn(config.mainPool, "strikeling", 30, 2, 6, CreatureRarity.Common);

        // Stage 2: rarer than stage 1.
        AddSpawn(config.mainPool, "emberclaw", 10, 7, 11, CreatureRarity.Rare);
        AddSpawn(config.mainPool, "frostcharge", 10, 7, 11, CreatureRarity.Rare);

        // Stage 3: rarer than stage 2.
        AddSpawn(config.mainPool, "voidmane", 4, 12, 16, CreatureRarity.Elite);
        AddSpawn(config.mainPool, "galecrown", 4, 12, 16, CreatureRarity.Elite);

        // Stage 4: rarest wild spawns.
        AddSpawn(config.mainPool, "solnox_the_eternal", 1, 17, 22, CreatureRarity.Legendary);
        AddSpawn(config.mainPool, "zypheron_the_unyielding", 1, 17, 22, CreatureRarity.Legendary);
    }

    static void AddSpawn(List<CreatureSpawnEntry> pool, string id, int weight, int minLevel, int maxLevel, CreatureRarity rarity)
    {
        if (pool == null) return;
        CreatureSpawnEntry e = new CreatureSpawnEntry
        {
            creatureID = id,
            weight = Mathf.Max(1, weight),
            levelMin = Mathf.Max(1, minLevel),
            levelMax = Mathf.Max(Mathf.Max(1, minLevel), maxLevel),
            rarityTier = rarity
        };
        pool.Add(e);
    }

    void EnsureActiveZoneReady()
    {
        SpawnManager manager = SpawnManager.Instance;
        SpawnZone active = manager.ActiveZone;
        if (active != null && active.isActive && active.gameObject.activeInHierarchy)
        {
            return;
        }

        SpawnZone[] zones = FindObjectsByType<SpawnZone>(FindObjectsSortMode.None);
        SpawnZone candidate = null;
        for (int i = 0; i < zones.Length; i++)
        {
            SpawnZone z = zones[i];
            if (z == null) continue;
            if (!z.gameObject.activeInHierarchy) continue;
            if (candidate == null) candidate = z;
            if (z.isActive)
            {
                candidate = z;
                break;
            }
        }

        if (candidate == null)
        {
            candidate = CreateRecoveryZone();
        }

        if (candidate == null) return;
        candidate.isActive = true;
        manager.RegisterZone(candidate);
        manager.EnterZone(candidate);
    }

    SpawnZone CreateRecoveryZone()
    {
        GameObject zoneObj = new GameObject("SpawnZone_Recovery");
        BoxCollider2D col = zoneObj.GetComponent<BoxCollider2D>();
        if (col == null) col = zoneObj.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        SpawnZone zone = zoneObj.AddComponent<SpawnZone>();

        Tilemap ground = null;
        GameObject g = GameObject.Find("Ground");
        if (g != null) ground = g.GetComponent<Tilemap>();

        if (ground != null)
        {
            Bounds b = ground.localBounds;
            Vector3 center = ground.transform.TransformPoint(b.center);
            Vector3 size = Vector3.Scale(b.size, ground.transform.lossyScale);
            zoneObj.transform.position = center;
            col.size = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
            col.offset = Vector2.zero;
        }
        else
        {
            zoneObj.transform.position = player != null ? player.position : Vector3.zero;
            col.size = new Vector2(80f, 80f);
            col.offset = Vector2.zero;
        }

        zone.zoneID = "recovery_zone";
        zone.config = Resources.Load<AreaSpawnConfig>("SpawnConfigs/VerdantCrossing_OpenMeadow");
        if (zone.config == null)
        {
            zone.config = BuildFallbackConfig();
        }
        zone.isActive = true;
        return zone;
    }

    AreaSpawnConfig BuildFallbackConfig()
    {
        AreaSpawnConfig cfg = ScriptableObject.CreateInstance<AreaSpawnConfig>();
        cfg.areaName = "Recovery Zone";
        cfg.baseEncounterRate = 0.10f;
        cfg.maxActiveCreatures = 8;
        cfg.respawnCooldownSeconds = 3f;
        cfg.rareEventChance = 0.02f;
        cfg.allowDaySpawns = true;
        cfg.allowNightSpawns = true;
        cfg.mainPool = new List<CreatureSpawnEntry>
        {
            new CreatureSpawnEntry
            {
                creatureID = "whelpling",
                weight = 100,
                levelMin = 2,
                levelMax = 5,
                rarityTier = CreatureRarity.Common
            }
        };
        cfg.rarePool = new List<CreatureSpawnEntry>();
        return cfg;
    }

    bool TryPickSpawnData(AreaSpawnConfig config, out CreatureEncounterData data)
    {
        data = null;
        SpawnTimeOfDay tod = GetTimeOfDay();
        for (int i = 0; i < 6; i++)
        {
            CreatureEncounterData rolled = SpawnTable.Resolve(config, tod);
            if (rolled == null) continue;
            data = rolled;
            return true;
        }
        return false;
    }

    bool TryGetSpawnPoint(SpawnZone zone, out Vector3 spawnPos)
    {
        spawnPos = Vector3.zero;
        Collider2D zoneCollider = zone.GetComponent<Collider2D>();
        if (zoneCollider == null) return false;

        Bounds bounds = zoneCollider.bounds;
        Vector2 playerPos = player != null ? (Vector2)player.position : Vector2.zero;

        int attempts = Mathf.Max(1, spawnPositionAttempts);
        for (int i = 0; i < attempts; i++)
        {
            if (TryPoint(zoneCollider, bounds, playerPos, true, out spawnPos)) return true;
        }

        // Fallback pass to avoid "nothing spawning" if strict distance rules reject all points.
        for (int i = 0; i < attempts; i++)
        {
            if (TryPoint(zoneCollider, bounds, playerPos, false, out spawnPos)) return true;
        }

        return false;
    }

    bool TryPoint(Collider2D zoneCollider, Bounds bounds, Vector2 playerPos, bool enforceDistanceBounds, out Vector3 spawnPos)
    {
        spawnPos = Vector3.zero;
        float x = Random.Range(bounds.min.x, bounds.max.x);
        float y = Random.Range(bounds.min.y, bounds.max.y);
        Vector2 candidate = new Vector2(x, y);

        if (!zoneCollider.OverlapPoint(candidate)) return false;

        if (player != null)
        {
            float dist = Vector2.Distance(playerPos, candidate);
            if (dist < minSpawnDistanceFromPlayer) return false;
            if (enforceDistanceBounds && dist > Mathf.Max(minSpawnDistanceFromPlayer, maxSpawnDistanceFromPlayer))
            {
                return false;
            }
        }

        if (IsBlocked(candidate, zoneCollider)) return false;

        spawnPos = new Vector3(candidate.x, candidate.y, 0f);
        return true;
    }

    bool IsBlocked(Vector2 point, Collider2D zoneCollider)
    {
        if (spawnClearanceRadius <= 0.001f) return false;
        Collider2D[] hits = Physics2D.OverlapCircleAll(point, spawnClearanceRadius);
        if (hits == null || hits.Length == 0) return false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null) continue;
            if (!hit.gameObject.activeInHierarchy) continue;
            if (hit.isTrigger) continue;
            if (hit == zoneCollider) continue;
            if (hit.GetComponentInParent<WildCreatureAI>() != null) continue;
            if (hit.GetComponentInParent<PlayerMover>() != null) continue;
            return true;
        }
        return false;
    }

    bool TryCreateWildCreature(CreatureEncounterData data, Vector3 spawnPos, string zoneID)
    {
        WildCreatureAI template = ResolveTemplate();
        if (template == null) return false;

        GameObject newObj = Instantiate(template.gameObject, spawnPos, Quaternion.identity);
        if (newObj == null) return false;
        newObj.SetActive(true);

        newObj.name = "Wild_" + FormatCreatureName(data.creatureID);
        EncounterSpawnMarker encounterMarker = newObj.GetComponent<EncounterSpawnMarker>();
        if (encounterMarker != null) Destroy(encounterMarker);

        WorldSpawnMarker marker = newObj.GetComponent<WorldSpawnMarker>();
        if (marker == null) marker = newObj.AddComponent<WorldSpawnMarker>();
        string canonicalID = CreatureRegistry.CanonicalizeCreatureID(data.creatureID);
        marker.creatureID = string.IsNullOrWhiteSpace(canonicalID) ? data.creatureID : canonicalID;
        marker.zoneID = zoneID;
        marker.level = Mathf.Max(1, data.resolvedLevel);

        WildCreatureAI ai = newObj.GetComponent<WildCreatureAI>();
        if (ai == null) ai = newObj.AddComponent<WildCreatureAI>();
        ai.ExitBattle();

        CreatureCombatant combatant = newObj.GetComponent<CreatureCombatant>();
        if (combatant == null) combatant = newObj.AddComponent<CreatureCombatant>();

        int level = Mathf.Max(1, data.resolvedLevel);
        if (!CreatureRegistry.TryGet(data.creatureID, out CreatureDefinition def) || def == null)
        {
            Debug.LogWarning("[Spawner] No CreatureDefinition found for ID '" + data.creatureID + "'. Spawn skipped.");
            Destroy(newObj);
            return false;
        }

        CreatureInstance instance = CreatureInstanceFactory.CreateWild(def, level);
        if (instance != null)
        {
            instance.capturedInZoneID = zoneID;
        }
        combatant.InitFromDefinition(def, instance);
        ai.ApplyDefinitionSettings(def);

        CreatureHealth health = newObj.GetComponent<CreatureHealth>();
        if (health == null) health = newObj.AddComponent<CreatureHealth>();
        health.level = combatant.level;
        health.maxHealth = Mathf.Max(1, combatant.maxHP);
        health.currentHealth = Mathf.Clamp(combatant.currentHP, 0, health.maxHealth);

        SpriteRenderer sr = newObj.GetComponent<SpriteRenderer>();
        Sprite sprite = def.sprite != null ? def.sprite : TryLoadSpriteForCreature(data.creatureID);
        if (sprite != null && sr != null)
        {
            DisableSpriteOverrideComponents(newObj);
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Simple;
            ApplyOverworldDefinitionScale(newObj.transform, def, sprite);
        }

        WhelplingBounceAnimator bounce = newObj.GetComponent<WhelplingBounceAnimator>();
        if (bounce != null)
        {
            bounce.RefreshDefaultSprite();
        }

        activeWilds.Add(ai);
        SyncManagerCount();
        return true;
    }

    public int SpawnDebugOneOfEachAroundPlayer()
    {
        TryFindPlayer();
        if (player == null) return 0;

        float minSpacing = Mathf.Max(2.0f, spawnClearanceRadius * 2f + 0.75f);
        float baseRadius = Mathf.Max(minSpawnDistanceFromPlayer + 1.5f, 4.5f);
        Vector3 center = player.position;

        List<Vector2> usedPositions = new List<Vector2>(DebugAllStageCreatureIds.Length);
        int spawned = 0;
        for (int i = 0; i < DebugAllStageCreatureIds.Length; i++)
        {
            string id = DebugAllStageCreatureIds[i];
            Vector3 spawnPos = ResolveDebugSpawnPoint(center, i, DebugAllStageCreatureIds.Length, baseRadius, minSpacing, usedPositions);
            int level = ResolveDebugSpawnLevel(id);

            CreatureEncounterData data = new CreatureEncounterData
            {
                creatureID = id,
                resolvedLevel = level,
                rarityTier = CreatureRarity.Legendary,
                isRareEvent = true,
                zoneID = "debug_manual",
                areaName = "Debug Spawn"
            };

            if (TryCreateWildCreature(data, spawnPos, "debug_manual"))
            {
                usedPositions.Add(spawnPos);
                spawned++;
            }
        }

        return spawned;
    }

    Vector3 ResolveDebugSpawnPoint(Vector3 center, int index, int total, float baseRadius, float minSpacing, List<Vector2> usedPositions)
    {
        int perRing = 6;
        int ring = index / perRing;
        int slot = index % perRing;
        int ringCount = Mathf.Min(perRing, total - ring * perRing);

        float angleStep = (Mathf.PI * 2f) / Mathf.Max(1, ringCount);
        float angle = slot * angleStep;
        float radius = baseRadius + (ring * (minSpacing * 0.95f));

        for (int tries = 0; tries < 14; tries++)
        {
            float jitter = UnityEngine.Random.Range(-0.22f, 0.22f);
            float testAngle = angle + jitter;
            float testRadius = radius + UnityEngine.Random.Range(-0.35f, 0.35f);
            Vector2 offset = new Vector2(Mathf.Cos(testAngle), Mathf.Sin(testAngle)) * testRadius;
            Vector2 candidate = (Vector2)center + offset;

            bool tooClose = false;
            for (int i = 0; i < usedPositions.Count; i++)
            {
                if (Vector2.Distance(usedPositions[i], candidate) < minSpacing)
                {
                    tooClose = true;
                    break;
                }
            }
            if (!tooClose)
            {
                return new Vector3(candidate.x, candidate.y, 0f);
            }
        }

        Vector2 fallbackOffset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        Vector2 fallback = (Vector2)center + fallbackOffset;
        return new Vector3(fallback.x, fallback.y, 0f);
    }

    static int ResolveDebugSpawnLevel(string creatureID)
    {
        string key = NormalizeKey(creatureID);
        if (key.Contains("solnox") || key.Contains("zypheron")) return 20;
        if (key.Contains("voidmane") || key.Contains("galecrown")) return 14;
        if (key.Contains("emberclaw") || key.Contains("frostcharge")) return 9;
        return 4;
    }

    void TryDespawnFarCreatures()
    {
        if (!despawnFarCreatures || player == null) return;
        float maxDist = Mathf.Max(5f, farDespawnDistance);
        float sqr = maxDist * maxDist;

        for (int i = activeWilds.Count - 1; i >= 0; i--)
        {
            WildCreatureAI ai = activeWilds[i];
            if (ai == null)
            {
                activeWilds.RemoveAt(i);
                continue;
            }

            if (ai.IsInBattle()) continue;
            if (((Vector2)(ai.transform.position - player.position)).sqrMagnitude > sqr)
            {
                Destroy(ai.gameObject);
                activeWilds.RemoveAt(i);
            }
        }
    }

    void RefreshActiveWildList()
    {
        for (int i = activeWilds.Count - 1; i >= 0; i--)
        {
            WildCreatureAI ai = activeWilds[i];
            if (ai == null || !ai.gameObject.activeInHierarchy || !ai.IsAlive())
            {
                activeWilds.RemoveAt(i);
            }
        }

        WorldSpawnMarker[] markers = FindObjectsByType<WorldSpawnMarker>(FindObjectsSortMode.None);
        for (int i = 0; i < markers.Length; i++)
        {
            WorldSpawnMarker marker = markers[i];
            if (marker == null || !marker.gameObject.activeInHierarchy) continue;

            WildCreatureAI ai = marker.GetComponent<WildCreatureAI>();
            if (ai == null || !ai.IsAlive()) continue;
            if (!activeWilds.Contains(ai))
            {
                activeWilds.Add(ai);
            }
        }
    }

    void TryFindPlayer()
    {
        if (player != null) return;
        GameObject go = GameObject.Find("Player");
        if (go != null) player = go.transform;
    }

    void SyncManagerCount()
    {
        if (!SpawnManager.HasInstance) return;
        SpawnManager.Instance.SyncActiveCreatureCount(activeWilds.Count);
    }

    bool CanSpawnAtCurrentTime(AreaSpawnConfig config)
    {
        SpawnTimeOfDay tod = GetTimeOfDay();
        if (tod == SpawnTimeOfDay.Day && !config.allowDaySpawns) return false;
        if (tod == SpawnTimeOfDay.Night && !config.allowNightSpawns) return false;
        return true;
    }

    SpawnTimeOfDay GetTimeOfDay()
    {
        if (!useSystemClockForDayNight) return manualTimeOfDay;

        int hour = System.DateTime.Now.Hour;
        bool isNight = (nightStartsAtHour <= dayStartsAtHour)
            ? (hour >= nightStartsAtHour && hour < dayStartsAtHour)
            : (hour >= nightStartsAtHour || hour < dayStartsAtHour);
        return isNight ? SpawnTimeOfDay.Night : SpawnTimeOfDay.Day;
    }

    WildCreatureAI ResolveTemplate()
    {
        if (fallbackTemplate != null) return fallbackTemplate;
        if (discoveredTemplate != null) return discoveredTemplate;

        WildCreatureAI[] all = FindObjectsByType<WildCreatureAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            WildCreatureAI ai = all[i];
            if (ai == null) continue;
            if (ai.GetComponent<WorldSpawnMarker>() != null) continue;
            if (ai.GetComponent<EncounterSpawnMarker>() != null) continue;
            if (ai.GetComponent<Follower>() != null) continue;
            discoveredTemplate = ai;
            return discoveredTemplate;
        }

        // Last-resort runtime template so spawning still works even without an authored wild prefab.
        GameObject runtimeTemplate = new GameObject("RuntimeWildTemplate");
        runtimeTemplate.SetActive(false);
        SpriteRenderer sr = runtimeTemplate.AddComponent<SpriteRenderer>();
        runtimeTemplate.AddComponent<TopDownSorter>();
        runtimeTemplate.AddComponent<CreatureHealth>();
        runtimeTemplate.AddComponent<CreatureCombatant>().autoInitWhelpling = false;
        runtimeTemplate.AddComponent<WildCreatureAI>();
        runtimeTemplate.AddComponent<WhelplingBounceAnimator>();

        Sprite fallbackSprite = TryLoadSpriteForCreature("whelpling");
        if (fallbackSprite != null)
        {
            sr.sprite = fallbackSprite;
            SpriteFromTexture sft = runtimeTemplate.AddComponent<SpriteFromTexture>();
            sft.texture = fallbackSprite.texture;
            sft.pixelsPerUnit = fallbackSprite.pixelsPerUnit;
        }

        runtimeTemplate.transform.SetParent(transform, false);
        discoveredTemplate = runtimeTemplate.GetComponent<WildCreatureAI>();
        return discoveredTemplate;
    }

    Sprite TryLoadSpriteForCreature(string creatureID)
    {
        if (string.IsNullOrWhiteSpace(creatureID)) return null;
        string key = NormalizeKey(creatureID);

        if (spriteCache.TryGetValue(key, out Sprite cached))
        {
            return cached;
        }
        if (missingSpriteCache.Contains(key)) return null;

        Sprite sprite = TryLoadFromResources(creatureID);
#if UNITY_EDITOR
        if (sprite == null)
        {
            sprite = TryLoadFromEditorAssets(creatureID);
        }
#endif
        if (sprite != null)
        {
            spriteCache[key] = sprite;
            return sprite;
        }

        missingSpriteCache.Add(key);
        return null;
    }

    Sprite TryLoadFromResources(string creatureID)
    {
        string[] candidates = BuildNameCandidates(creatureID);
        for (int i = 0; i < candidates.Length; i++)
        {
            string c = candidates[i];
            Sprite s = Resources.Load<Sprite>("Creatures/" + c);
            if (s != null) return s;
        }

        Sprite[] all = Resources.LoadAll<Sprite>("Creatures");
        if (all == null || all.Length == 0) return null;

        string wanted = NormalizeKey(creatureID);
        for (int i = 0; i < all.Length; i++)
        {
            Sprite s = all[i];
            if (s == null) continue;
            if (NormalizeKey(s.name) == wanted) return s;
        }

        return null;
    }

#if UNITY_EDITOR
    Sprite TryLoadFromEditorAssets(string creatureID)
    {
        string wanted = NormalizeKey(creatureID);
        string[] roots = { "Assets/Creatures", "Assets/Resources/Creatures" };
        for (int r = 0; r < roots.Length; r++)
        {
            string root = roots[r];
            if (!AssetDatabase.IsValidFolder(root)) continue;

            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { root });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path)) continue;

                string fileName = Path.GetFileNameWithoutExtension(path);
                if (NormalizeKey(fileName) != wanted) continue;

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) return sprite;
            }
        }
        return null;
    }
#endif

    static string FormatCreatureName(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "Creature";
        string trimmed = id.Trim().Replace("_", " ");
        if (trimmed.Length <= 1) return trimmed.ToUpperInvariant();
        return char.ToUpperInvariant(trimmed[0]) + trimmed.Substring(1);
    }

    static string NormalizeKey(string value)
    {
        return CreatureRegistry.NormalizeKey(value, keepUnderscore: false);
    }

    static string[] BuildNameCandidates(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return new string[0];
        string trimmed = id.Trim();
        string lower = trimmed.ToLowerInvariant();
        string upperFirst = char.ToUpperInvariant(trimmed[0]) + trimmed.Substring(1);
        string noUnderscores = trimmed.Replace("_", " ");
        return new[] { trimmed, lower, upperFirst, noUnderscores };
    }

    void DisableSpriteOverrideComponents(GameObject go)
    {
        if (go == null) return;

        SpriteFromTexture sft = go.GetComponent<SpriteFromTexture>();
        if (sft != null) sft.enabled = false;

        SpriteFromAtlas sfa = go.GetComponent<SpriteFromAtlas>();
        if (sfa != null) sfa.enabled = false;
    }

    void ApplyOverworldDefinitionScale(Transform target, CreatureDefinition def, Sprite sprite)
    {
        if (target == null || def == null || sprite == null) return;

        // Sprite world size already derives from import PPU when localScale = 1.
        // Keep scale tied to definition multiplier only, so PPU drives the baseline.
        float scale = Mathf.Max(0.05f, def.overworldSizeMultiplier);
        target.localScale = new Vector3(scale, scale, 1f);
    }

}
