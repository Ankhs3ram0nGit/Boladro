using System.Collections.Generic;
using UnityEngine;

public enum WildBehaviour
{
    Aggressive,
    Neutral,
    Passive
}

public enum EvolutionTrigger
{
    LevelThreshold,
    LevelPlusCondition,
    SpecialItem
}

public enum EvolutionRelic
{
    None,
    EmbershardCrystal,
    TideglassOrb,
    StorecoreGem,
    Rootstone,
    Frostbloom,
    Voidheart,
    Dawncrystal,
    AncientRiftstone
}

[CreateAssetMenu(fileName = "CreatureDefinition", menuName = "Creatures/Creature Definition")]
public class CreatureDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique creature key used by spawners and markers, e.g. whelpling.")]
    public string creatureID = "whelpling";

    [Tooltip("Display name shown in UI.")]
    public string displayName = "Whelpling";

    [Tooltip("Flavor text.")]
    [TextArea]
    public string description = "A curious creature.";

    [Tooltip("Rarity tier.")]
    public CreatureRarity rarityTier = CreatureRarity.Common;

    [Tooltip("Primary type (always required).")]
    public CreatureType primaryType = CreatureType.Normal;

    [Tooltip("Optional second type.")]
    public CreatureType secondaryType = CreatureType.None;

    [Tooltip("Optional third type.")]
    public CreatureType tertiaryType = CreatureType.None;

    [Tooltip("Optional fourth type; only valid for legendary creatures.")]
    public CreatureType quaternaryType = CreatureType.None;

    [Tooltip("Evolution stage number.")]
    [Range(1, 4)]
    public int evolutionStage = 1;

    [Header("Visuals")]
    [Tooltip("Primary sprite used for world and battle.")]
    public Sprite sprite;

    [Tooltip("Optional custom ground shadow sprite.")]
    public Sprite shadowSprite;

    [Tooltip("Default facing direction in world.")]
    public Vector2 facingDirection = Vector2.right;

    [Tooltip("Optional idle animation clip.")]
    public AnimationClip idleAnimationClip;

    [Header("Sizing")]
    [Tooltip("Overworld scale multiplier. Final world size = sprite native world size from PPU * this multiplier.")]
    [Min(0.05f)]
    public float overworldSizeMultiplier = 1f;

    [Tooltip("Optional battle sprite scale multiplier.")]
    [Min(0.05f)]
    public float battleSizeMultiplier = 1f;

    [Header("Movement & Behaviour")]
    [Tooltip("Wild behavior profile.")]
    public WildBehaviour wildBehaviour = WildBehaviour.Neutral;

    [Tooltip("Base overworld movement speed profile.")]
    [Min(0.1f)]
    public float moveSpeed = 2.5f;

    [Tooltip("Wander radius around spawn point.")]
    [Min(0.1f)]
    public float wanderRadius = 3f;

    [Tooltip("Flee speed multiplier for passive creatures.")]
    [Min(1f)]
    public float fleeSpeedMultiplier = 1.5f;

    [Tooltip("Distance at which creature reacts to player.")]
    [Min(0.1f)]
    public float detectionRadius = 4f;

    [Tooltip("Aggro chase radius for aggressive creatures.")]
    [Min(0.1f)]
    public float aggroRadius = 2.5f;

    [Header("Base Stats")]
    [Min(1)] public int baseHP = 25;
    [Min(1)] public int baseAttack = 5;
    [Min(1)] public int baseDefense = 5;
    [Min(1)] public int baseSpeed = 5;

    public float hpPerLevel = 4f;
    public float atkPerLevel = 1f;
    public float defPerLevel = 1f;
    public float spdPerLevel = 1f;

    [Header("Moves")]
    [Tooltip("Move unlocked at level 1.")]
    public MoveDefinition moveSlot1;
    [Tooltip("Move unlocked at level 5.")]
    public MoveDefinition moveSlot2;
    [Tooltip("Move unlocked at level 10.")]
    public MoveDefinition moveSlot3;
    [Tooltip("Move unlocked at level 15.")]
    public MoveDefinition moveSlot4;

    [Header("Soul Traits")]
    [Tooltip("Growth profile used for IV-style stat scaling.")]
    public SoulTraitGrowthProfile soulTraitGrowthProfile;

    [Header("Evolution")]
    [Tooltip("Next evolution definition.")]
    public CreatureDefinition nextEvolution;

    [Tooltip("How evolution is triggered.")]
    public EvolutionTrigger evolutionTrigger = EvolutionTrigger.LevelThreshold;

    [Min(0)] public int evolutionLevel = 0;
    [Min(0)] public int evolutionBattleCount = 0;
    public EvolutionRelic evolutionItem = EvolutionRelic.None;
    public bool canDelayEvolution = true;

    [Header("Audio")]
    public AudioClip encounterCry;
    public AudioClip battleCry;
    public AudioClip faintSound;

    void OnValidate()
    {
        if (!string.IsNullOrWhiteSpace(creatureID))
        {
            creatureID = CreatureRegistry.NormalizeKey(creatureID, keepUnderscore: true);
        }

        if (quaternaryType != CreatureType.None && rarityTier != CreatureRarity.Legendary)
        {
            Debug.LogWarning("[CreatureDefinition] " + creatureID + ": quaternaryType is only valid on Legendary creatures. Resetting to None.");
            quaternaryType = CreatureType.None;
        }

        if (evolutionStage < 1) evolutionStage = 1;
        if (evolutionStage > 4) evolutionStage = 4;
        if (overworldSizeMultiplier < 0.05f) overworldSizeMultiplier = 0.05f;
        if (battleSizeMultiplier < 0.05f) battleSizeMultiplier = 0.05f;
        if (detectionRadius < 0.1f) detectionRadius = 0.1f;
        if (aggroRadius < 0.1f) aggroRadius = 0.1f;
        if (aggroRadius > detectionRadius) aggroRadius = detectionRadius;
        if (wanderRadius < 0.1f) wanderRadius = 0.1f;
        if (moveSpeed < 0.1f) moveSpeed = 0.1f;
        if (fleeSpeedMultiplier < 1f) fleeSpeedMultiplier = 1f;
        if (baseHP < 1) baseHP = 1;
        if (baseAttack < 1) baseAttack = 1;
        if (baseDefense < 1) baseDefense = 1;
        if (baseSpeed < 1) baseSpeed = 1;
    }

    public CreatureType[] GetAllTypes()
    {
        List<CreatureType> all = new List<CreatureType>(4);
        if (primaryType != CreatureType.None) all.Add(primaryType);
        if (secondaryType != CreatureType.None) all.Add(secondaryType);
        if (tertiaryType != CreatureType.None) all.Add(tertiaryType);
        if (quaternaryType != CreatureType.None && rarityTier == CreatureRarity.Legendary) all.Add(quaternaryType);
        if (all.Count == 0) all.Add(CreatureType.Normal);
        return all.ToArray();
    }

    public MoveDefinition GetMoveForSlot(int slot)
    {
        switch (slot)
        {
            case 0: return moveSlot1;
            case 1: return moveSlot2;
            case 2: return moveSlot3;
            case 3: return moveSlot4;
            default: return null;
        }
    }

    public MoveDefinition[] GetAllMoves()
    {
        List<MoveDefinition> list = new List<MoveDefinition>(4);
        MoveDefinition[] raw = { moveSlot1, moveSlot2, moveSlot3, moveSlot4 };
        for (int i = 0; i < raw.Length; i++)
        {
            if (raw[i] != null) list.Add(raw[i]);
        }
        return list.ToArray();
    }

    public static CreatureAggressionMode ToAggressionMode(WildBehaviour behaviour)
    {
        switch (behaviour)
        {
            case WildBehaviour.Aggressive:
                return CreatureAggressionMode.Aggressive;
            case WildBehaviour.Passive:
                return CreatureAggressionMode.Passive;
            default:
                return CreatureAggressionMode.Neutral;
        }
    }
}
