using UnityEngine;

public enum SoulTraitType
{
    VitalitySpark,
    StrikeEssence,
    WardEssence,
    GaleEssence,
    FocusEssence,
    SoulDepth
}

[CreateAssetMenu(fileName = "SoulTraitGrowthProfile", menuName = "Creatures/Soul Trait Growth Profile")]
public class SoulTraitGrowthProfile : ScriptableObject
{
    [Header("Growth Per Soul Trait")]
    [Tooltip("HP growth contribution from Vitality Spark.")]
    public float vitalitySparkGrowth = 1.5f;

    [Tooltip("Attack growth contribution from Strike Essence.")]
    public float strikeEssenceGrowth = 1.5f;

    [Tooltip("Defense growth contribution from Ward Essence.")]
    public float wardEssenceGrowth = 1.5f;

    [Tooltip("Speed growth contribution from Gale Essence.")]
    public float galeEssenceGrowth = 1.5f;

    [Tooltip("Accuracy/crit growth contribution from Focus Essence.")]
    public float focusEssenceGrowth = 1.5f;

    [Tooltip("Small composite growth contribution from Soul Depth.")]
    public float soulDepthGrowth = 0.5f;

    public float GetGrowthFor(SoulTraitType trait)
    {
        switch (trait)
        {
            case SoulTraitType.VitalitySpark:
                return vitalitySparkGrowth;
            case SoulTraitType.StrikeEssence:
                return strikeEssenceGrowth;
            case SoulTraitType.WardEssence:
                return wardEssenceGrowth;
            case SoulTraitType.GaleEssence:
                return galeEssenceGrowth;
            case SoulTraitType.FocusEssence:
                return focusEssenceGrowth;
            case SoulTraitType.SoulDepth:
                return soulDepthGrowth;
            default:
                return 0f;
        }
    }
}
