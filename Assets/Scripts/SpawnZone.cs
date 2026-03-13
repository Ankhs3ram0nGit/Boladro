using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SpawnZone : MonoBehaviour
{
    [Tooltip("Unique zone ID for save/load and debug.")]
    public string zoneID = "zone_01";

    [Tooltip("Area spawn config used when player is inside this zone.")]
    public AreaSpawnConfig config;

    [Tooltip("Runtime toggle to enable or disable spawning in this zone.")]
    public bool isActive = true;

    private Collider2D zoneCollider;

    void Awake()
    {
        EnsureZoneCollider();
        SpawnManager.Instance.RegisterZone(this);
    }

    void OnEnable()
    {
        EnsureZoneCollider();
        SpawnManager.Instance.RegisterZone(this);
    }

    void OnDisable()
    {
        if (SpawnManager.HasInstance)
        {
            SpawnManager.Instance.DeregisterZone(this);
            SpawnManager.Instance.ExitZone(this);
        }
    }

    void OnDestroy()
    {
        if (SpawnManager.HasInstance)
        {
            SpawnManager.Instance.DeregisterZone(this);
            SpawnManager.Instance.ExitZone(this);
        }
    }

    public AreaSpawnConfig GetEffectiveConfig()
    {
        if (config == null) return null;
        if (config.weatherOverride != null) return config.weatherOverride;
        return config;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive || other == null) return;
        if (other.GetComponent<PlayerMover>() == null) return;
        SpawnManager.Instance.EnterZone(this);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other == null) return;
        if (other.GetComponent<PlayerMover>() == null) return;
        if (SpawnManager.HasInstance)
        {
            SpawnManager.Instance.ExitZone(this);
        }
    }

    void OnDrawGizmos()
    {
        Collider2D c = zoneCollider != null ? zoneCollider : GetComponent<Collider2D>();
        if (c == null) return;

        Color zoneColor = GetZoneColor();
        Color fill = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.18f);
        Color outline = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.85f);

        Gizmos.color = fill;
        Bounds b = c.bounds;
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = outline;
        Gizmos.DrawWireCube(b.center, b.size);

#if UNITY_EDITOR
        Handles.color = outline;
        string label = string.IsNullOrWhiteSpace(zoneID) ? name : zoneID;
        if (config != null && !string.IsNullOrWhiteSpace(config.areaName))
        {
            label += " (" + config.areaName + ")";
        }
        Handles.Label(b.center + Vector3.up * (b.extents.y + 0.2f), label);
#endif
    }

    Color GetZoneColor()
    {
        if (config == null || config.rarePool == null || config.rarePool.Count == 0)
        {
            return new Color(0.3f, 0.9f, 0.3f);
        }

        bool hasLegendary = false;
        bool hasEliteOrRare = false;
        for (int i = 0; i < config.rarePool.Count; i++)
        {
            CreatureSpawnEntry e = config.rarePool[i];
            if (e == null) continue;
            if (e.rarityTier == CreatureRarity.Legendary) hasLegendary = true;
            if (e.rarityTier == CreatureRarity.Elite || e.rarityTier == CreatureRarity.Rare) hasEliteOrRare = true;
        }

        if (hasLegendary) return new Color(0.95f, 0.22f, 0.22f);
        if (hasEliteOrRare) return new Color(1f, 0.55f, 0.12f);
        return new Color(0.3f, 0.9f, 0.3f);
    }

    void EnsureZoneCollider()
    {
        zoneCollider = GetComponent<Collider2D>();
        if (zoneCollider == null)
        {
            zoneCollider = gameObject.AddComponent<BoxCollider2D>();
        }
        if (zoneCollider != null) zoneCollider.isTrigger = true;
    }
}
