using System.Collections.Generic;
using UnityEngine;

public static class SpawnTable
{
    public static CreatureEncounterData Resolve(AreaSpawnConfig config, SpawnTimeOfDay time)
    {
        if (config == null) return null;

        bool rareRoll = RollForRareEvent(config.rareEventChance);
        List<CreatureSpawnEntry> chosenPool = rareRoll ? config.rarePool : config.mainPool;

        CreatureSpawnEntry entry = WeightedSelect(chosenPool, time);
        bool usedRare = rareRoll;

        if (entry == null)
        {
            entry = WeightedSelect(config.mainPool, time);
            usedRare = false;
        }

        if (entry == null)
        {
            // Emergency fallback: ignore time filters.
            entry = WeightedSelectIgnoreTime(config.mainPool);
            usedRare = false;
        }

        if (entry == null) return null;

        return new CreatureEncounterData
        {
            creatureID = entry.creatureID,
            resolvedLevel = ResolveLevel(entry),
            rarityTier = entry.rarityTier,
            isRareEvent = usedRare
        };
    }

    private static bool RollForRareEvent(float rareEventChance)
    {
        if (rareEventChance <= 0f) return false;
        return Random.value < rareEventChance;
    }

    private static CreatureSpawnEntry WeightedSelect(List<CreatureSpawnEntry> pool, SpawnTimeOfDay time)
    {
        if (pool == null || pool.Count == 0) return null;

        float totalWeight = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            CreatureSpawnEntry e = pool[i];
            if (e == null || !PassesTimeFilter(e, time) || e.weight <= 0) continue;
            totalWeight += e.weight;
        }

        if (totalWeight <= 0f) return null;

        float roll = Random.Range(0f, totalWeight);
        for (int i = 0; i < pool.Count; i++)
        {
            CreatureSpawnEntry e = pool[i];
            if (e == null || !PassesTimeFilter(e, time) || e.weight <= 0) continue;
            roll -= e.weight;
            if (roll <= 0f)
            {
                return e;
            }
        }

        return pool[pool.Count - 1];
    }

    private static CreatureSpawnEntry WeightedSelectIgnoreTime(List<CreatureSpawnEntry> pool)
    {
        if (pool == null || pool.Count == 0) return null;

        float totalWeight = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            CreatureSpawnEntry e = pool[i];
            if (e == null || e.weight <= 0) continue;
            totalWeight += e.weight;
        }
        if (totalWeight <= 0f) return null;

        float roll = Random.Range(0f, totalWeight);
        for (int i = 0; i < pool.Count; i++)
        {
            CreatureSpawnEntry e = pool[i];
            if (e == null || e.weight <= 0) continue;
            roll -= e.weight;
            if (roll <= 0f) return e;
        }

        return pool[pool.Count - 1];
    }

    private static int ResolveLevel(CreatureSpawnEntry entry)
    {
        int min = Mathf.Max(1, entry.levelMin);
        int max = Mathf.Max(min, entry.levelMax);
        return Random.Range(min, max + 1);
    }

    private static bool PassesTimeFilter(CreatureSpawnEntry entry, SpawnTimeOfDay time)
    {
        if (entry == null) return false;
        if (entry.dayOnly && time == SpawnTimeOfDay.Night) return false;
        if (entry.nightOnly && time == SpawnTimeOfDay.Day) return false;
        return true;
    }
}
