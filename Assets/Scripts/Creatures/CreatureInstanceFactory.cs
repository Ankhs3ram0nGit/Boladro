using System;
using UnityEngine;

public static class CreatureInstanceFactory
{
    public static CreatureInstance CreateWild(CreatureDefinition def, int level)
    {
        if (def == null) return null;

        CreatureInstance instance = new CreatureInstance();
        instance.creatureUID = Guid.NewGuid().ToString();
        instance.definitionID = CreatureRegistry.CanonicalizeCreatureID(def.creatureID);
        instance.ownerID = string.Empty;
        instance.ownershipState = OwnershipState.Wild;
        instance.nickname = string.Empty;
        instance.level = Mathf.Max(1, level);
        instance.soulTraits = RollSoulTraits();
        instance.totalBattles = 0;
        instance.isShiny = false;
        instance.familiarityTier = FamiliarityTier.None;
        instance.captureTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        instance.currentPP = BuildStartingPP(def);
        instance.currentHP = ComputeMaxHP(def, instance.soulTraits, instance.level);
        return instance;
    }

    public static CreatureInstance CreateFromSave(CreatureInstanceSaveData data)
    {
        CreatureInstance instance = new CreatureInstance();
        instance.creatureUID = string.IsNullOrWhiteSpace(data.creatureUID) ? Guid.NewGuid().ToString() : data.creatureUID;
        instance.definitionID = CreatureRegistry.CanonicalizeCreatureID(data.definitionID);
        instance.ownerID = data.ownerID ?? string.Empty;
        instance.ownershipState = data.ownershipState;
        instance.nickname = data.nickname;
        instance.level = Mathf.Max(1, data.level);
        instance.currentHP = Mathf.Max(0, data.currentHP);
        instance.currentPP = data.currentPP != null ? (int[])data.currentPP.Clone() : new int[4];
        if (instance.currentPP.Length < 4)
        {
            Array.Resize(ref instance.currentPP, 4);
        }
        instance.soulTraits = ClampSoulTraits(data.soulTraits);
        instance.totalBattles = Mathf.Max(0, data.totalBattles);
        instance.isShiny = data.isShiny;
        instance.capturedInZoneID = data.capturedInZoneID;
        instance.captureTimestamp = data.captureTimestamp;
        instance.familiarityTier = data.familiarityTier;
        return instance;
    }

    public static int ComputeMaxHP(CreatureDefinition def, SoulTraitValues soulTraits, int level)
    {
        if (def == null) return Mathf.Max(1, level * 4);
        int lvl = Mathf.Max(1, level);
        float baseGrowth = def.baseHP + (def.hpPerLevel * lvl);
        float trait = ComputeTraitGrowth(def, soulTraits, SoulTraitType.VitalitySpark, lvl);
        return Mathf.Max(1, Mathf.RoundToInt(baseGrowth + trait));
    }

    public static int ComputeAttack(CreatureDefinition def, SoulTraitValues soulTraits, int level)
    {
        if (def == null) return Mathf.Max(1, 5 + level);
        int lvl = Mathf.Max(1, level);
        float baseGrowth = def.baseAttack + (def.atkPerLevel * lvl);
        float trait = ComputeTraitGrowth(def, soulTraits, SoulTraitType.StrikeEssence, lvl);
        return Mathf.Max(1, Mathf.RoundToInt(baseGrowth + trait));
    }

    public static int ComputeDefense(CreatureDefinition def, SoulTraitValues soulTraits, int level)
    {
        if (def == null) return Mathf.Max(1, 5 + level);
        int lvl = Mathf.Max(1, level);
        float baseGrowth = def.baseDefense + (def.defPerLevel * lvl);
        float trait = ComputeTraitGrowth(def, soulTraits, SoulTraitType.WardEssence, lvl);
        return Mathf.Max(1, Mathf.RoundToInt(baseGrowth + trait));
    }

    public static int ComputeSpeed(CreatureDefinition def, SoulTraitValues soulTraits, int level)
    {
        if (def == null) return Mathf.Max(1, 5 + level);
        int lvl = Mathf.Max(1, level);
        float baseGrowth = def.baseSpeed + (def.spdPerLevel * lvl);
        float trait = ComputeTraitGrowth(def, soulTraits, SoulTraitType.GaleEssence, lvl);
        return Mathf.Max(1, Mathf.RoundToInt(baseGrowth + trait));
    }

    public static float ComputeAccuracyModifier(CreatureDefinition def, SoulTraitValues soulTraits, int level)
    {
        if (def == null) return 1f;
        int lvl = Mathf.Max(1, level);
        float growth = ComputeTraitGrowth(def, soulTraits, SoulTraitType.FocusEssence, lvl);
        return 1f + Mathf.Clamp(growth * 0.005f, 0f, 0.40f);
    }

    public static float ComputeCritModifier(CreatureDefinition def, SoulTraitValues soulTraits, int level)
    {
        if (def == null) return 1f;
        int lvl = Mathf.Max(1, level);
        float growth = ComputeTraitGrowth(def, soulTraits, SoulTraitType.FocusEssence, lvl);
        return 1f + Mathf.Clamp(growth * 0.004f, 0f, 0.30f);
    }

    public static void RefillPP(CreatureDefinition def, CreatureInstance instance)
    {
        if (def == null || instance == null) return;
        instance.currentPP = BuildStartingPP(def);
    }

    private static int[] BuildStartingPP(CreatureDefinition def)
    {
        int[] pp = new int[4];
        for (int i = 0; i < 4; i++)
        {
            MoveDefinition move = def.GetMoveForSlot(i);
            pp[i] = move != null ? Mathf.Max(0, move.maxPP) : 0;
        }
        return pp;
    }

    private static SoulTraitValues RollSoulTraits()
    {
        SoulTraitValues v = new SoulTraitValues();
        v.vitalitySpark = UnityEngine.Random.Range(1, 32);
        v.strikeEssence = UnityEngine.Random.Range(1, 32);
        v.wardEssence = UnityEngine.Random.Range(1, 32);
        v.galeEssence = UnityEngine.Random.Range(1, 32);
        v.focusEssence = UnityEngine.Random.Range(1, 32);
        v.soulDepth = UnityEngine.Random.Range(1, 32);
        return v;
    }

    private static SoulTraitValues ClampSoulTraits(SoulTraitValues v)
    {
        v.vitalitySpark = Mathf.Clamp(v.vitalitySpark, 1, 31);
        v.strikeEssence = Mathf.Clamp(v.strikeEssence, 1, 31);
        v.wardEssence = Mathf.Clamp(v.wardEssence, 1, 31);
        v.galeEssence = Mathf.Clamp(v.galeEssence, 1, 31);
        v.focusEssence = Mathf.Clamp(v.focusEssence, 1, 31);
        v.soulDepth = Mathf.Clamp(v.soulDepth, 1, 31);
        return v;
    }

    private static float ComputeTraitGrowth(CreatureDefinition def, SoulTraitValues soulTraits, SoulTraitType primaryTrait, int level)
    {
        if (def == null || def.soulTraitGrowthProfile == null) return 0f;

        float primary = (soulTraits.Get(primaryTrait) / 31f) * level * def.soulTraitGrowthProfile.GetGrowthFor(primaryTrait);
        float soulDepth = (soulTraits.Get(SoulTraitType.SoulDepth) / 31f) * level * def.soulTraitGrowthProfile.GetGrowthFor(SoulTraitType.SoulDepth);
        return primary + soulDepth;
    }
}
