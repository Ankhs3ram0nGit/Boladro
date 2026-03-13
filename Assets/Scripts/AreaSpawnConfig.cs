using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AreaSpawnConfig", menuName = "Spawning/Area Spawn Config")]
public class AreaSpawnConfig : ScriptableObject
{
    [Tooltip("Human-readable area name for debugging and design clarity.")]
    public string areaName = "New Area";

    [Tooltip("Base encounter chance per step before modifiers. Recommended 0.03 - 0.10.")]
    [Range(0f, 1f)]
    public float baseEncounterRate = 0.10f;

    [Tooltip("Hard cap of active creatures generated from this zone.")]
    [Min(1)]
    public int maxActiveCreatures = 10;

    [Tooltip("Minimum seconds before the same tile can trigger an encounter again.")]
    [Min(0f)]
    public float respawnCooldownSeconds = 3f;

    [Tooltip("Primary weighted creature pool for this area.")]
    public List<CreatureSpawnEntry> mainPool = new List<CreatureSpawnEntry>();

    [Tooltip("Rare event weighted pool (Elite/Legendary style encounters).")]
    public List<CreatureSpawnEntry> rarePool = new List<CreatureSpawnEntry>();

    [Tooltip("Chance that a triggered encounter rolls from rarePool instead of mainPool.")]
    [Range(0f, 1f)]
    public float rareEventChance = 0.02f;

    [Tooltip("Allow encounters during day.")]
    public bool allowDaySpawns = true;

    [Tooltip("Allow encounters at night.")]
    public bool allowNightSpawns = true;

    [Tooltip("Optional config override for weather events.")]
    public AreaSpawnConfig weatherOverride;

    private void OnValidate()
    {
        if (maxActiveCreatures < 1) maxActiveCreatures = 1;
        if (respawnCooldownSeconds < 0f) respawnCooldownSeconds = 0f;
        if (baseEncounterRate < 0f) baseEncounterRate = 0f;
        if (baseEncounterRate > 1f) baseEncounterRate = 1f;
        if (rareEventChance < 0f) rareEventChance = 0f;
        if (rareEventChance > 1f) rareEventChance = 1f;

        ValidatePool(mainPool);
        ValidatePool(rarePool);
    }

    private static void ValidatePool(List<CreatureSpawnEntry> pool)
    {
        if (pool == null) return;
        for (int i = 0; i < pool.Count; i++)
        {
            CreatureSpawnEntry e = pool[i];
            if (e == null) continue;
            if (e.weight < 1) e.weight = 1;
            if (e.levelMin < 1) e.levelMin = 1;
            if (e.levelMax < e.levelMin) e.levelMax = e.levelMin;
            if (e.levelVarianceBonus < 0f) e.levelVarianceBonus = 0f;
            if (e.levelVarianceBonus > 1f) e.levelVarianceBonus = 1f;
        }
    }
}
