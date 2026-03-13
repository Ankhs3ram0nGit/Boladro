using System;
using UnityEngine;

public enum CreatureRarity
{
    Common,
    Uncommon,
    Rare,
    Elite,
    Legendary
}

public enum SpawnTimeOfDay
{
    Day,
    Night
}

[Serializable]
public class CreatureSpawnEntry
{
    [Tooltip("Unique creature ID used by encounter/battle systems.")]
    public string creatureID = "whelpling";

    [Tooltip("Relative weight used for weighted random selection. Minimum 1.")]
    [Min(1)]
    public int weight = 1;

    [Tooltip("Minimum level this creature can spawn at in this area.")]
    [Min(1)]
    public int levelMin = 1;

    [Tooltip("Maximum level this creature can spawn at in this area.")]
    [Min(1)]
    public int levelMax = 1;

    [Tooltip("Rarity tier used by UI/music/reward systems.")]
    public CreatureRarity rarityTier = CreatureRarity.Common;

    [Tooltip("If true, this creature only appears at night.")]
    public bool nightOnly;

    [Tooltip("If true, this creature only appears during daytime.")]
    public bool dayOnly;

    [Tooltip("Optional bonus modifier for future trait systems.")]
    [Range(0f, 1f)]
    public float levelVarianceBonus;
}

[Serializable]
public class CreatureEncounterData
{
    public string creatureID;
    public int resolvedLevel;
    public CreatureRarity rarityTier;
    public bool isRareEvent;
    public string zoneID;
    public string areaName;
}

[Serializable]
public class TileEncounterModifier
{
    [Tooltip("Tile key. Example: Tall Grass, Dense Forest, Cave Floor, Ancient Ruins, Graveyard, Water, Road, Safe Zone")]
    public string tileType = "Tall Grass";

    [Tooltip("Encounter rate multiplier for this tile type.")]
    public float modifier = 1f;
}
