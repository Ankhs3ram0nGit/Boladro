using UnityEngine;

public enum MoveFlag
{
    None,
    DoubleDamageIfStatused,
    GuaranteeDodge,
    DragonBonus
}

[CreateAssetMenu(fileName = "MoveDefinition", menuName = "Creatures/Move Definition")]
public class MoveDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique move key, e.g. claw_strike.")]
    public string moveID = "claw_strike";

    [Tooltip("Display name used in battle UI.")]
    public string displayName = "Claw Strike";

    [Header("Combat")]
    [Tooltip("Element type of this move.")]
    public CreatureType moveType = CreatureType.Normal;

    [Tooltip("Base damage. Use 0 for status-only moves.")]
    public int baseDamage = 5;

    [Tooltip("Damage dealt on critical hit. Use 0 for status-only moves.")]
    public int critDamage = 7;

    [Tooltip("Hit chance from 0.0 to 1.0.")]
    [Range(0f, 1f)]
    public float accuracy = 1f;

    [Tooltip("Maximum PP for this move.")]
    [Min(1)]
    public int maxPP = 20;

    [Tooltip("Optional status applied on hit. None means no status.")]
    public StatusEffectType statusEffect = StatusEffectType.None;

    [Tooltip("Chance to apply status on hit (0.0 to 1.0).")]
    [Range(0f, 1f)]
    public float statusChance = 0f;

    [Tooltip("Turns the status lasts. Use -1 for until cured.")]
    public int statusDuration = 0;

    [Tooltip("If true, this move is status-only and should deal no damage.")]
    public bool isStatusMove = false;

    [Tooltip("Special behavior hook used by battle logic.")]
    public MoveFlag specialFlag = MoveFlag.None;

    [Tooltip("True if this move uses physical damage interactions.")]
    public bool isPhysical = true;

    public int AccuracyPercent => Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(accuracy) * 100f), 1, 100);
}
