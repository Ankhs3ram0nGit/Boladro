using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<SpawnManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("SpawnManager");
                    _instance = go.AddComponent<SpawnManager>();
                }
            }
            return _instance;
        }
    }

    public static bool HasInstance => _instance != null;
    private static SpawnManager _instance;

    [Tooltip("Global master toggle for random encounters.")]
    public bool EncountersEnabled = true;

    [Tooltip("Global encounter multiplier. 0 disables all encounters, 2 doubles chance.")]
    public float GlobalEncounterRateMultiplier = 1f;

    [Tooltip("Toggle debug overlay with this key.")]
    public KeyCode debugToggleKey = KeyCode.F9;
    [Tooltip("Toggle wild creature spawning on/off.")]
    public KeyCode toggleWildSpawningKey = KeyCode.P;
    [Tooltip("Spawn one of every creature stage around the player.")]
    public KeyCode spawnAllStagesKey = KeyCode.O;

    [Tooltip("Start with debug overlay visible.")]
    public bool showDebugOverlay;

    public event Action<CreatureEncounterData> OnEncounterResolved;
    public event Action OnBattleResolved;

    private readonly List<SpawnZone> zoneStack = new List<SpawnZone>();
    private int activeCreatureCount;
    private CreatureEncounterData lastEncounter;
    private float lastEncounterTime = -999f;
    private string lastTileType = "None";
    private float nextZoneRecoveryTime;
    private OverworldCreatureSpawner overworldSpawner;
    private bool wildSpawningPaused;

    public int ActiveCreatureCount => activeCreatureCount;
    public SpawnZone ActiveZone => GetTopActiveZone();

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        EncountersEnabled = false;

        EnsureOverworldSpawnerPresent();
        EnsureZoneReady();
    }

    void Update()
    {
        if (Input.GetKeyDown(debugToggleKey))
        {
            showDebugOverlay = !showDebugOverlay;
        }
        if (Input.GetKeyDown(toggleWildSpawningKey))
        {
            ToggleWildSpawningPause();
        }
        if (Input.GetKeyDown(spawnAllStagesKey))
        {
            SpawnDebugOneOfEachStage();
        }

        if (Time.time >= nextZoneRecoveryTime)
        {
            nextZoneRecoveryTime = Time.time + 1.0f;
            EnsureZoneReady();
        }
    }

    public void RegisterZone(SpawnZone zone)
    {
        if (zone == null) return;
        if (!zoneStack.Contains(zone))
        {
            zoneStack.Add(zone);
        }
    }

    public void DeregisterZone(SpawnZone zone)
    {
        if (zone == null) return;
        zoneStack.Remove(zone);
    }

    public void EnterZone(SpawnZone zone)
    {
        if (zone == null) return;
        zoneStack.Remove(zone);
        zoneStack.Add(zone);
    }

    public void ExitZone(SpawnZone zone)
    {
        if (zone == null) return;
        zoneStack.Remove(zone);
    }

    public bool TryRequestEncounter(Vector3Int tilePosition, float tileModifier, string tileType, SpawnTimeOfDay timeOfDay, out CreatureEncounterData data)
    {
        data = null;
        if (!EncountersEnabled) return false;

        ReconcileActiveCreatureCount();

        SpawnZone zone = GetTopActiveZone();
        if (zone == null || !zone.isActive) return false;

        AreaSpawnConfig config = zone.GetEffectiveConfig();
        if (config == null) return false;

        if (activeCreatureCount >= Mathf.Max(1, config.maxActiveCreatures))
        {
            return false;
        }

        if (timeOfDay == SpawnTimeOfDay.Day && !config.allowDaySpawns) return false;
        if (timeOfDay == SpawnTimeOfDay.Night && !config.allowNightSpawns) return false;

        float rate = Mathf.Clamp01(config.baseEncounterRate * Mathf.Max(0f, tileModifier) * Mathf.Max(0f, GlobalEncounterRateMultiplier));
        if (UnityEngine.Random.value >= rate)
        {
            return false;
        }

        data = SpawnTable.Resolve(config, timeOfDay);
        if (data == null) return false;

        data.zoneID = zone.zoneID;
        data.areaName = config.areaName;

        activeCreatureCount++;
        lastEncounter = data;
        lastEncounterTime = Time.time;
        lastTileType = tileType;
        OnEncounterResolved?.Invoke(data);
        return true;
    }

    public void ForceEncounter(string creatureID, int level)
    {
        CreatureEncounterData data = new CreatureEncounterData
        {
            creatureID = creatureID,
            resolvedLevel = Mathf.Max(1, level),
            rarityTier = CreatureRarity.Rare,
            isRareEvent = true,
            zoneID = ActiveZone != null ? ActiveZone.zoneID : "forced",
            areaName = ActiveZone != null && ActiveZone.config != null ? ActiveZone.config.areaName : "Forced"
        };

        activeCreatureCount++;
        lastEncounter = data;
        lastEncounterTime = Time.time;
        lastTileType = "Forced";
        OnEncounterResolved?.Invoke(data);
    }

    public void NotifyBattleResolved()
    {
        activeCreatureCount = Mathf.Max(0, activeCreatureCount - 1);
        OnBattleResolved?.Invoke();
    }

    public void SyncActiveCreatureCount(int count)
    {
        activeCreatureCount = Mathf.Max(0, count);
    }

    private void ReconcileActiveCreatureCount()
    {
        // Keep cap logic resilient if a previous encounter failed to instantiate
        // or was otherwise removed unexpectedly.
        int liveSpawned = FindObjectsByType<EncounterSpawnMarker>(FindObjectsSortMode.None).Length;
        if (liveSpawned < activeCreatureCount)
        {
            activeCreatureCount = liveSpawned;
        }
    }

    private SpawnZone GetTopActiveZone()
    {
        if (zoneStack.Count == 0)
        {
            EnsureZoneReady();
        }

        for (int i = zoneStack.Count - 1; i >= 0; i--)
        {
            SpawnZone z = zoneStack[i];
            if (z == null) continue;
            if (!z.isActive) continue;
            if (!z.gameObject.activeInHierarchy) continue;
            return z;
        }

        EnsureZoneReady();
        for (int i = zoneStack.Count - 1; i >= 0; i--)
        {
            SpawnZone z = zoneStack[i];
            if (z == null) continue;
            if (!z.isActive) continue;
            if (!z.gameObject.activeInHierarchy) continue;
            return z;
        }

        return null;
    }

    private void EnsureOverworldSpawnerPresent()
    {
        OverworldCreatureSpawner spawner = GetComponent<OverworldCreatureSpawner>();
        if (spawner == null)
        {
            spawner = gameObject.AddComponent<OverworldCreatureSpawner>();
        }
        overworldSpawner = spawner;
        if (overworldSpawner != null) overworldSpawner.enabled = !wildSpawningPaused;
    }

    private void ToggleWildSpawningPause()
    {
        EnsureOverworldSpawnerPresent();
        if (overworldSpawner == null) return;

        wildSpawningPaused = !wildSpawningPaused;
        overworldSpawner.enabled = !wildSpawningPaused;
        EncountersEnabled = !wildSpawningPaused;

        Debug.Log("SpawnManager Debug: Wild spawning " + (wildSpawningPaused ? "DISABLED" : "ENABLED") + " (P)");
    }

    private void SpawnDebugOneOfEachStage()
    {
        EnsureOverworldSpawnerPresent();
        if (overworldSpawner == null) return;

        int spawned = overworldSpawner.SpawnDebugOneOfEachAroundPlayer();
        Debug.Log("SpawnManager Debug: Spawned " + spawned + " staged creatures around player (O).");
    }

    private void EnsureZoneReady()
    {
        SpawnZone active = GetRegisteredActiveZone();
        if (active != null) return;

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
            candidate = CreateRuntimeZone();
        }

        if (candidate == null) return;

        if (candidate.config == null)
        {
            candidate.config = LoadOrBuildFallbackConfig();
        }

        candidate.isActive = true;
        RegisterZone(candidate);
        EnterZone(candidate);
    }

    private SpawnZone GetRegisteredActiveZone()
    {
        for (int i = zoneStack.Count - 1; i >= 0; i--)
        {
            SpawnZone z = zoneStack[i];
            if (z == null) continue;
            if (!z.gameObject.activeInHierarchy) continue;
            if (!z.isActive) continue;
            return z;
        }
        return null;
    }

    private SpawnZone CreateRuntimeZone()
    {
        GameObject zoneObj = new GameObject("SpawnZone_AutoRuntime");
        BoxCollider2D col = zoneObj.GetComponent<BoxCollider2D>();
        if (col == null) col = zoneObj.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        SpawnZone zone = zoneObj.AddComponent<SpawnZone>();

        Tilemap ground = null;
        GameObject groundGo = GameObject.Find("Ground");
        if (groundGo != null) ground = groundGo.GetComponent<Tilemap>();

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
            zoneObj.transform.position = Vector3.zero;
            col.size = new Vector2(120f, 120f);
            col.offset = Vector2.zero;
        }

        zone.zoneID = "auto_runtime_zone";
        zone.isActive = true;
        zone.config = LoadOrBuildFallbackConfig();
        return zone;
    }

    private AreaSpawnConfig LoadOrBuildFallbackConfig()
    {
        AreaSpawnConfig loaded = Resources.Load<AreaSpawnConfig>("SpawnConfigs/VerdantCrossing_OpenMeadow");
        if (loaded != null) return loaded;

        AreaSpawnConfig cfg = ScriptableObject.CreateInstance<AreaSpawnConfig>();
        cfg.areaName = "Auto Runtime Zone";
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
                weight = 60,
                levelMin = 2,
                levelMax = 5,
                rarityTier = CreatureRarity.Common
            },
            new CreatureSpawnEntry
            {
                creatureID = "meadow_hopper",
                weight = 40,
                levelMin = 1,
                levelMax = 1,
                rarityTier = CreatureRarity.Common
            },
            new CreatureSpawnEntry
            {
                creatureID = "leaf_sprite",
                weight = 30,
                levelMin = 1,
                levelMax = 1,
                rarityTier = CreatureRarity.Common
            }
        };
        cfg.rarePool = new List<CreatureSpawnEntry>
        {
            new CreatureSpawnEntry
            {
                creatureID = "ashcub",
                weight = 50,
                levelMin = 5,
                levelMax = 8,
                rarityTier = CreatureRarity.Legendary
            },
            new CreatureSpawnEntry
            {
                creatureID = "strikeling",
                weight = 50,
                levelMin = 5,
                levelMax = 8,
                rarityTier = CreatureRarity.Legendary
            }
        };
        return cfg;
    }

    void OnGUI()
    {
        if (!showDebugOverlay) return;

        SpawnZone zone = GetTopActiveZone();
        string zoneName = zone != null && zone.config != null ? zone.config.areaName : "None";
        string encounterName = lastEncounter != null ? lastEncounter.creatureID + " Lv" + lastEncounter.resolvedLevel : "None";
        float since = lastEncounterTime > -900f ? Time.time - lastEncounterTime : -1f;

        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.Box(new Rect(12, 12, 360, 134), GUIContent.none);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(20, 20, 344, 118));
        GUILayout.Label("Spawn Debug");
        GUILayout.Label("Zone: " + zoneName);
        GUILayout.Label("Active Creatures: " + activeCreatureCount);
        GUILayout.Label("Encounter Multiplier: " + GlobalEncounterRateMultiplier.ToString("0.00"));
        GUILayout.Label("Wild Spawning: " + (wildSpawningPaused ? "Paused (P)" : "Enabled (P)"));
        GUILayout.Label("Last Tile Type: " + lastTileType);
        GUILayout.Label("Last Encounter: " + encounterName);
        GUILayout.Label("Last Encounter Ago: " + (since < 0f ? "-" : since.ToString("0.0s")));
        GUILayout.EndArea();
    }
}
