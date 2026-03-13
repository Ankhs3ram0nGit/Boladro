using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public class EncounterTrigger : MonoBehaviour
{
    [Tooltip("Tilemap used to detect tile type for encounter modifiers.")]
    public Tilemap encounterTilemap;

    [Tooltip("How many movement steps are protected from new encounters after a battle resolves.")]
    [Min(0)]
    public int postBattleGraceSteps = 3;

    [Tooltip("Use local system hour for day/night. If disabled, use manualTimeOfDay.")]
    public bool useSystemClockForDayNight = true;

    [Tooltip("Manual day/night value when system clock mode is off.")]
    public SpawnTimeOfDay manualTimeOfDay = SpawnTimeOfDay.Day;

    [Tooltip("Hour (0-23) when nighttime starts.")]
    [Range(0, 23)]
    public int nightStartsAtHour = 18;

    [Tooltip("Hour (0-23) when daytime starts.")]
    [Range(0, 23)]
    public int dayStartsAtHour = 6;

    [Tooltip("Fallback modifier if tile type key isn't mapped.")]
    public float defaultTileModifier = 1f;

    [Tooltip("Distance moved before an encounter roll is attempted, even if still in same cell.")]
    [Min(0.05f)]
    public float movementStepDistance = 0.5f;

    [Tooltip("Per tile-type encounter multipliers.")]
    public List<TileEncounterModifier> tileModifiers = new List<TileEncounterModifier>
    {
        new TileEncounterModifier { tileType = "Tall Grass", modifier = 1.0f },
        new TileEncounterModifier { tileType = "Dense Forest", modifier = 0.8f },
        new TileEncounterModifier { tileType = "Cave Floor", modifier = 0.6f },
        new TileEncounterModifier { tileType = "Ancient Ruins", modifier = 0.9f },
        new TileEncounterModifier { tileType = "Graveyard", modifier = 0.7f },
        new TileEncounterModifier { tileType = "Water", modifier = 0.3f },
        new TileEncounterModifier { tileType = "Road", modifier = 0.0f },
        new TileEncounterModifier { tileType = "Safe Zone", modifier = 0.0f },
    };

    private readonly Dictionary<Vector3Int, float> perTileLastEncounterTime = new Dictionary<Vector3Int, float>();
    private readonly Dictionary<string, float> modifierLookup = new Dictionary<string, float>();
    private bool hasLastCell;
    private Vector3Int lastCell;
    private bool hasLastStepPosition;
    private Vector3 lastStepPosition;
    private int graceStepsRemaining;
    private int stepsSincePurge;

    void Awake()
    {
        if (encounterTilemap == null)
        {
            GameObject g = GameObject.Find("Ground");
            if (g != null) encounterTilemap = g.GetComponent<Tilemap>();
        }

        RebuildModifierLookup();

        if (GetComponent<SpawnToBattleBridge>() == null)
        {
            gameObject.AddComponent<SpawnToBattleBridge>();
        }
    }

    void OnEnable()
    {
        SpawnManager.Instance.OnBattleResolved += HandleBattleResolved;
    }

    void OnDisable()
    {
        if (SpawnManager.HasInstance)
        {
            SpawnManager.Instance.OnBattleResolved -= HandleBattleResolved;
        }
    }

    void Update()
    {
        Vector3Int cell = GetCurrentCell();
        Vector3 pos = transform.position;
        if (!hasLastCell)
        {
            lastCell = cell;
            hasLastCell = true;
            lastStepPosition = pos;
            hasLastStepPosition = true;
            return;
        }

        bool changedCell = cell != lastCell;
        bool movedStep = false;
        if (hasLastStepPosition)
        {
            float dist = Vector3.Distance(pos, lastStepPosition);
            movedStep = dist >= Mathf.Max(0.05f, movementStepDistance);
        }
        else
        {
            hasLastStepPosition = true;
            lastStepPosition = pos;
        }

        if (!changedCell && !movedStep) return;

        lastCell = cell;
        if (movedStep) lastStepPosition = pos;
        OnPlayerStep(cell);
    }

    void OnValidate()
    {
        if (postBattleGraceSteps < 0) postBattleGraceSteps = 0;
        if (defaultTileModifier < 0f) defaultTileModifier = 0f;
        if (movementStepDistance < 0.05f) movementStepDistance = 0.05f;
        RebuildModifierLookup();
    }

    void RebuildModifierLookup()
    {
        modifierLookup.Clear();
        if (tileModifiers == null) return;
        for (int i = 0; i < tileModifiers.Count; i++)
        {
            TileEncounterModifier m = tileModifiers[i];
            if (m == null || string.IsNullOrWhiteSpace(m.tileType)) continue;
            modifierLookup[m.tileType.Trim().ToLowerInvariant()] = m.modifier;
        }
    }

    Vector3Int GetCurrentCell()
    {
        if (encounterTilemap != null)
        {
            return encounterTilemap.WorldToCell(transform.position);
        }

        Vector3 p = transform.position;
        return new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), 0);
    }

    void OnPlayerStep(Vector3Int cell)
    {
        if (!SpawnManager.HasInstance) return;
        SpawnManager manager = SpawnManager.Instance;
        if (!manager.EncountersEnabled) return;
        if (manager.ActiveZone == null) return;

        if (graceStepsRemaining > 0)
        {
            graceStepsRemaining--;
            return;
        }

        AreaSpawnConfig config = manager.ActiveZone.GetEffectiveConfig();
        if (config == null) return;

        if (!TileCooldownExpired(cell, config.respawnCooldownSeconds)) return;

        string tileType = ResolveTileType(cell);
        float tileModifier = GetTileModifier(tileType);
        if (tileModifier <= 0f) return;

        CreatureEncounterData data;
        bool triggered = manager.TryRequestEncounter(cell, tileModifier, tileType, GetTimeOfDay(), out data);
        if (triggered)
        {
            perTileLastEncounterTime[cell] = Time.time;
        }

        stepsSincePurge++;
        if (stepsSincePurge >= 20)
        {
            stepsSincePurge = 0;
            PurgeOldCooldowns(60f);
        }
    }

    bool TileCooldownExpired(Vector3Int cell, float cooldown)
    {
        if (cooldown <= 0f) return true;
        if (!perTileLastEncounterTime.TryGetValue(cell, out float lastTime)) return true;
        return Time.time - lastTime >= cooldown;
    }

    void PurgeOldCooldowns(float maxAge)
    {
        if (perTileLastEncounterTime.Count == 0) return;

        List<Vector3Int> remove = null;
        foreach (KeyValuePair<Vector3Int, float> kv in perTileLastEncounterTime)
        {
            if (Time.time - kv.Value > maxAge)
            {
                if (remove == null) remove = new List<Vector3Int>();
                remove.Add(kv.Key);
            }
        }

        if (remove == null) return;
        for (int i = 0; i < remove.Count; i++)
        {
            perTileLastEncounterTime.Remove(remove[i]);
        }
    }

    string ResolveTileType(Vector3Int cell)
    {
        if (encounterTilemap == null) return "Tall Grass";

        TileBase tile = encounterTilemap.GetTile(cell);
        if (tile == null) return "Safe Zone";

        string n = tile.name != null ? tile.name.ToLowerInvariant() : "";
        if (n.Contains("road") || n.Contains("path") || n.Contains("stone")) return "Road";
        if (n.Contains("water") || n.Contains("river") || n.Contains("pond")) return "Water";
        if (n.Contains("grave")) return "Graveyard";
        if (n.Contains("ruin")) return "Ancient Ruins";
        if (n.Contains("cave")) return "Cave Floor";
        if (n.Contains("forest")) return "Dense Forest";
        if (n.Contains("grass")) return "Tall Grass";
        return "Tall Grass";
    }

    float GetTileModifier(string tileType)
    {
        if (string.IsNullOrWhiteSpace(tileType)) return defaultTileModifier;
        string key = tileType.Trim().ToLowerInvariant();
        if (modifierLookup.TryGetValue(key, out float v)) return v;
        return defaultTileModifier;
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

    void HandleBattleResolved()
    {
        graceStepsRemaining = postBattleGraceSteps;
    }
}
