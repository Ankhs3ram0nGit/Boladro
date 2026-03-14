using UnityEngine;

public struct ExperienceGainResult
{
    public int previousLevel;
    public int newLevel;
    public int previousExperience;
    public int newExperience;
    public int experienceGranted;

    public int levelsGained => Mathf.Max(0, newLevel - previousLevel);
    public bool leveledUp => newLevel > previousLevel;
}

public static class CreatureExperienceSystem
{
    public const int MaxLevel = 100;
    public const int StandardTotalExperienceToMaxLevel = 1000000;
    public const float LegendaryCurveMultiplier = 1.5f;

    public static float ResolveCurveMultiplier(CreatureDefinition definition)
    {
        float manual = definition != null ? Mathf.Max(0.1f, definition.experienceCurveMultiplier) : 1f;
        bool legendary = definition != null && definition.rarityTier == CreatureRarity.Legendary;
        return manual * (legendary ? LegendaryCurveMultiplier : 1f);
    }

    public static int GetTotalXpForLevel(int level, CreatureDefinition definition)
    {
        int clampedLevel = Mathf.Clamp(level, 1, MaxLevel);
        float normalized = (clampedLevel - 1f) / (MaxLevel - 1f);
        float scaledMax = StandardTotalExperienceToMaxLevel * ResolveCurveMultiplier(definition);
        return Mathf.RoundToInt(scaledMax * normalized * normalized * normalized);
    }

    public static int GetLevelFromTotalXp(int totalExperience, CreatureDefinition definition)
    {
        int xp = Mathf.Max(0, totalExperience);
        int lo = 1;
        int hi = MaxLevel;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (GetTotalXpForLevel(mid, definition) <= xp)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return Mathf.Clamp(lo, 1, MaxLevel);
    }

    public static int GetXpToNextLevel(int level, CreatureDefinition definition)
    {
        int lv = Mathf.Clamp(level, 1, MaxLevel);
        if (lv >= MaxLevel) return 0;
        return Mathf.Max(1, GetTotalXpForLevel(lv + 1, definition) - GetTotalXpForLevel(lv, definition));
    }

    public static float GetLevelProgress01(CreatureInstance instance, CreatureDefinition definition)
    {
        if (instance == null) return 0f;
        int level = Mathf.Clamp(instance.level, 1, MaxLevel);
        if (level >= MaxLevel) return 1f;

        int floor = GetTotalXpForLevel(level, definition);
        int ceil = GetTotalXpForLevel(level + 1, definition);
        int xp = Mathf.Max(floor, instance.totalExperience);
        int span = Mathf.Max(1, ceil - floor);
        return Mathf.Clamp01((float)(xp - floor) / span);
    }

    public static void EnsureExperienceBaseline(CreatureInstance instance, CreatureDefinition definition)
    {
        if (instance == null) return;
        instance.level = Mathf.Clamp(instance.level, 1, MaxLevel);

        int floor = GetTotalXpForLevel(instance.level, definition);
        if (instance.totalExperience < floor)
        {
            instance.totalExperience = floor;
        }

        int cap = GetTotalXpForLevel(MaxLevel, definition);
        if (instance.totalExperience > cap)
        {
            instance.totalExperience = cap;
        }

        int levelFromXp = GetLevelFromTotalXp(instance.totalExperience, definition);
        if (levelFromXp > instance.level)
        {
            instance.level = levelFromXp;
        }
    }

    public static ExperienceGainResult AddExperience(CreatureInstance instance, CreatureDefinition definition, int amount)
    {
        ExperienceGainResult result = new ExperienceGainResult();
        if (instance == null || amount <= 0)
        {
            return result;
        }

        EnsureExperienceBaseline(instance, definition);

        int beforeLevel = Mathf.Clamp(instance.level, 1, MaxLevel);
        int beforeXp = Mathf.Max(0, instance.totalExperience);
        int maxXp = GetTotalXpForLevel(MaxLevel, definition);
        int granted = Mathf.Clamp(amount, 0, Mathf.Max(0, maxXp - beforeXp));

        int afterXp = beforeXp + granted;
        int afterLevel = GetLevelFromTotalXp(afterXp, definition);

        instance.totalExperience = afterXp;
        instance.level = Mathf.Clamp(afterLevel, 1, MaxLevel);

        if (definition != null)
        {
            int oldMax = CreatureInstanceFactory.ComputeMaxHP(definition, instance.soulTraits, beforeLevel);
            int newMax = CreatureInstanceFactory.ComputeMaxHP(definition, instance.soulTraits, instance.level);
            int gain = Mathf.Max(0, newMax - oldMax);
            bool wasFainted = instance.currentHP <= 0;
            int nextHp = wasFainted ? 0 : instance.currentHP + gain;
            instance.currentHP = Mathf.Clamp(nextHp, 0, Mathf.Max(1, newMax));
        }

        result.previousLevel = beforeLevel;
        result.newLevel = instance.level;
        result.previousExperience = beforeXp;
        result.newExperience = afterXp;
        result.experienceGranted = granted;
        return result;
    }
}
