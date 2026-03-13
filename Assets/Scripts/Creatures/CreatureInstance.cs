using System;
using UnityEngine;

public enum OwnershipState
{
    Wild,
    Captured
}

public enum FamiliarityTier
{
    None,
    Bronze,
    Silver,
    Gold
}

[Serializable]
public struct SoulTraitValues
{
    [Range(1, 31)] public int vitalitySpark;
    [Range(1, 31)] public int strikeEssence;
    [Range(1, 31)] public int wardEssence;
    [Range(1, 31)] public int galeEssence;
    [Range(1, 31)] public int focusEssence;
    [Range(1, 31)] public int soulDepth;

    public int Get(SoulTraitType trait)
    {
        switch (trait)
        {
            case SoulTraitType.VitalitySpark:
                return vitalitySpark;
            case SoulTraitType.StrikeEssence:
                return strikeEssence;
            case SoulTraitType.WardEssence:
                return wardEssence;
            case SoulTraitType.GaleEssence:
                return galeEssence;
            case SoulTraitType.FocusEssence:
                return focusEssence;
            case SoulTraitType.SoulDepth:
                return soulDepth;
            default:
                return 1;
        }
    }
}

[Serializable]
public class CreatureInstance
{
    public string creatureUID;
    public string definitionID;
    public string ownerID;
    public OwnershipState ownershipState = OwnershipState.Wild;
    public string nickname;
    public int level = 1;
    public int currentHP = 1;
    public int[] currentPP = new int[4];
    public SoulTraitValues soulTraits;
    public int totalBattles;
    public bool isShiny;
    public string capturedInZoneID;
    public long captureTimestamp;
    public FamiliarityTier familiarityTier = FamiliarityTier.None;

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(nickname)) return nickname.Trim();
            CreatureDefinition def = CreatureRegistry.Get(definitionID);
            if (def != null && !string.IsNullOrWhiteSpace(def.displayName)) return def.displayName;
            return string.IsNullOrWhiteSpace(definitionID) ? "Creature" : definitionID;
        }
    }
}

[Serializable]
public struct CreatureInstanceSaveData
{
    public string creatureUID;
    public string definitionID;
    public string ownerID;
    public OwnershipState ownershipState;
    public string nickname;
    public int level;
    public int currentHP;
    public int[] currentPP;
    public SoulTraitValues soulTraits;
    public int totalBattles;
    public bool isShiny;
    public string capturedInZoneID;
    public long captureTimestamp;
    public FamiliarityTier familiarityTier;
}
