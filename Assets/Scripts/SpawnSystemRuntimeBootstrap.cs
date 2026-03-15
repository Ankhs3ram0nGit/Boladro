using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;
using System;

public class SpawnSystemRuntimeBootstrap : MonoBehaviour
{
    private const string ZoneConfigResourcePath = "SpawnConfigs/VerdantCrossing_OpenMeadow";
    private const string RuntimeMenuSceneName = "__RuntimeMainMenuScene";
    private static bool sceneHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureSceneHook();
        BootstrapForScene(SceneManager.GetActiveScene());
    }

    private static void EnsureSceneHook()
    {
        if (sceneHooked) return;
        SceneManager.sceneLoaded += OnSceneLoaded;
        sceneHooked = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BootstrapForScene(scene);
    }

    private static bool ShouldSkipScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return true;
        if (string.Equals(scene.name, RuntimeMenuSceneName, StringComparison.Ordinal)) return true;
        if (MainMenuBootstrap.IsMenuOpen) return true;
        return false;
    }

    private static void BootstrapForScene(Scene scene)
    {
        if (ShouldSkipScene(scene)) return;
        BootstrapGameplayRuntime();
    }

    private static void BootstrapGameplayRuntime()
    {
        SpawnManager manager = SpawnManager.Instance;
        if (manager == null) return;
        manager.EncountersEnabled = false;
        if (manager.GlobalEncounterRateMultiplier <= 0f)
        {
            manager.GlobalEncounterRateMultiplier = 1f;
        }
        AreaSpawnConfig configuredZoneConfig = Resources.Load<AreaSpawnConfig>(ZoneConfigResourcePath);

        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            if (player.transform.localScale.x > 0.65f && player.transform.localScale.y > 0.65f)
            {
                player.transform.localScale = new Vector3(0.8f, 0.8f, player.transform.localScale.z);
            }

            PlayerCreatureParty party = player.GetComponent<PlayerCreatureParty>();
            if (party == null)
            {
                party = player.AddComponent<PlayerCreatureParty>();
            }
            if (party.ActiveCreatures == null || party.ActiveCreatures.Count == 0)
            {
                party.InitializeParty();
            }

            ActivePartyFollowerController followerController = player.GetComponent<ActivePartyFollowerController>();
            if (followerController == null)
            {
                followerController = player.AddComponent<ActivePartyFollowerController>();
            }
            followerController.enabled = true;

            EncounterTrigger trigger = player.GetComponent<EncounterTrigger>();
            if (trigger != null && trigger.encounterTilemap == null)
            {
                GameObject g = GameObject.Find("Ground");
                if (g != null) trigger.encounterTilemap = g.GetComponent<Tilemap>();
            }
            if (trigger != null) trigger.enabled = false;

            SpawnToBattleBridge bridge = player.GetComponent<SpawnToBattleBridge>();
            if (bridge != null) bridge.enabled = false;

            BattleSystem bs = player.GetComponent<BattleSystem>();
            if (bs != null) bs.enabled = true;
            BattleManager legacyBattle = player.GetComponent<BattleManager>();
            if (legacyBattle != null) legacyBattle.enabled = false;
            if (bridge != null && bridge.battleSystem == null)
            {
                bridge.battleSystem = bs;
            }

            MiniMapController miniMap = player.GetComponent<MiniMapController>();
            if (miniMap == null)
            {
                miniMap = player.AddComponent<MiniMapController>();
            }
            miniMap.enabled = true;

            PlayerGroundShadow playerShadow = player.GetComponent<PlayerGroundShadow>();
            if (playerShadow == null)
            {
                playerShadow = player.AddComponent<PlayerGroundShadow>();
            }
            playerShadow.enabled = true;
        }

        SpawnZone[] zones = FindObjectsByType<SpawnZone>(FindObjectsSortMode.None);
        SpawnZone existingZone = null;
        for (int i = 0; i < zones.Length; i++)
        {
            SpawnZone z = zones[i];
            if (z == null) continue;
            if (!z.gameObject.activeInHierarchy) continue;
            if (existingZone == null) existingZone = z;
            if (z.isActive)
            {
                existingZone = z;
                break;
            }
        }

        SpawnZone activeZone = existingZone;
        if (activeZone == null)
        {
            Tilemap ground = null;
            GameObject g = GameObject.Find("Ground");
            if (g != null) ground = g.GetComponent<Tilemap>();
            activeZone = CreateDefaultZone(ground, configuredZoneConfig);
        }
        else
        {
            // If a zone already exists, wire the authored config into it.
            if (configuredZoneConfig != null)
            {
                activeZone.config = configuredZoneConfig;
            }
            activeZone.isActive = true;
        }

        if (activeZone != null)
        {
            manager.RegisterZone(activeZone);
            SpawnManager.Instance.EnterZone(activeZone);
        }

        OverworldCreatureSpawner spawner = manager.GetComponent<OverworldCreatureSpawner>();
        if (spawner == null)
        {
            spawner = manager.gameObject.AddComponent<OverworldCreatureSpawner>();
        }
        if (spawner != null)
        {
            // Force a fresh OnEnable pass after scene transitions.
            bool wasEnabled = spawner.enabled;
            spawner.enabled = false;
            spawner.enabled = true;
            if (!wasEnabled) spawner.enabled = true;
        }

        WorldRockSpriteNormalizer rockNormalizer = manager.GetComponent<WorldRockSpriteNormalizer>();
        if (rockNormalizer == null)
        {
            rockNormalizer = manager.gameObject.AddComponent<WorldRockSpriteNormalizer>();
        }
        if (rockNormalizer != null)
        {
            // Re-apply stone normalization for freshly loaded scene renderers.
            bool wasEnabled = rockNormalizer.enabled;
            rockNormalizer.enabled = false;
            rockNormalizer.enabled = true;
            if (!wasEnabled) rockNormalizer.enabled = true;
        }
    }

    static SpawnZone CreateDefaultZone(Tilemap ground, AreaSpawnConfig configuredZoneConfig)
    {
        GameObject zoneObj = new GameObject("SpawnZone_Default");
        BoxCollider2D col = zoneObj.GetComponent<BoxCollider2D>();
        if (col == null) col = zoneObj.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        SpawnZone zone = zoneObj.AddComponent<SpawnZone>();

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
            col.size = new Vector2(50f, 40f);
        }

        zone.zoneID = "default_zone";
        zone.config = configuredZoneConfig != null ? configuredZoneConfig : BuildDefaultConfig();
        zone.isActive = true;
        return zone;
    }

    static AreaSpawnConfig BuildDefaultConfig()
    {
        AreaSpawnConfig cfg = ScriptableObject.CreateInstance<AreaSpawnConfig>();
        cfg.areaName = "Default Field";
        cfg.baseEncounterRate = 0.10f;
        cfg.maxActiveCreatures = 8;
        cfg.respawnCooldownSeconds = 3f;
        cfg.rareEventChance = 0.02f;
        cfg.allowDaySpawns = true;
        cfg.allowNightSpawns = true;
        cfg.mainPool = new System.Collections.Generic.List<CreatureSpawnEntry>
        {
            new CreatureSpawnEntry
            {
                creatureID = "whelpling",
                weight = 100,
                levelMin = 1,
                levelMax = 3,
                rarityTier = CreatureRarity.Common
            }
        };
        cfg.rarePool = new System.Collections.Generic.List<CreatureSpawnEntry>
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
}
