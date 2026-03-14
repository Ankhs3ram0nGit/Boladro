using System;
using System.Collections.Generic;
using UnityEngine;

public enum CreatureType
{
    Normal,
    Fire,
    Water,
    Lightning,
    Earth,
    Nature,
    Ice,
    Dragon,
    Light,
    Dark,
    None
}

public enum StatusEffectType
{
    Burn,
    Wet,
    Frozen,
    Anxious,
    Paralysed,
    Poisoned,
    None,
    Blind,
    Terrified
}

[Serializable]
public class AttackData
{
    public string name;
    public int maxPP;
    public int currentPP;
    public int baseDamage;
    public int critDamage;
    public int accuracy;
    public CreatureType type;
    public float statusChance;
    public StatusEffectType? statusToApply;
    public int statusDuration;
    public bool isPhysical;
    public MoveFlag specialFlag;
}

[Serializable]
public class StatusEffect
{
    public StatusEffectType type;
    public int turns;
}

public class CreatureCombatant : MonoBehaviour
{
    public string creatureName = "Whelpling";
    public CreatureType[] types = new CreatureType[] { CreatureType.Dragon, CreatureType.Water, CreatureType.Fire };
    public int level = 1;
    public int maxHP = 25;
    public int currentHP = 25;
    public int attack = 5;
    public int defense = 5;
    public int speed = 5;
    public bool autoInitWhelpling = true;

    public List<AttackData> attacks = new List<AttackData>();
    public List<StatusEffect> statusEffects = new List<StatusEffect>();

    [SerializeField] private CreatureDefinition definition;
    [SerializeField] private CreatureInstance instance;

    public CreatureDefinition Definition => definition;
    public CreatureInstance Instance => instance;

    void Awake()
    {
        if (autoInitWhelpling)
        {
            InitWhelpling(level);
        }
    }

    public void InitWhelpling(int lvl)
    {
        definition = null;
        instance = null;
        creatureName = "Whelpling";
        types = new CreatureType[] { CreatureType.Dragon, CreatureType.Water, CreatureType.Fire };
        level = Mathf.Max(1, lvl);
        maxHP = 25 + (level - 1) * 4;
        currentHP = Mathf.Clamp(currentHP <= 0 ? maxHP : currentHP, 1, maxHP);
        attack = 5 + Mathf.FloorToInt(level * 0.8f);
        defense = 5 + Mathf.FloorToInt(level * 0.8f);
        speed = 5 + Mathf.FloorToInt(level * 0.2f);

        attacks.Clear();

        attacks.Add(new AttackData
        {
            name = "Claw Strike",
            maxPP = 20,
            currentPP = 20,
            baseDamage = 5,
            critDamage = 7,
            accuracy = 100,
            type = CreatureType.Normal,
            statusChance = 0f,
            statusToApply = null,
            statusDuration = 0,
            isPhysical = true,
            specialFlag = MoveFlag.None
        });

        attacks.Add(new AttackData
        {
            name = "Ember Spit",
            maxPP = 5,
            currentPP = 5,
            baseDamage = 15,
            critDamage = 20,
            accuracy = 90,
            type = CreatureType.Fire,
            statusChance = 0.05f,
            statusToApply = StatusEffectType.Burn,
            statusDuration = 3,
            isPhysical = false,
            specialFlag = MoveFlag.None
        });

        attacks.Add(new AttackData
        {
            name = "Torrent Splash",
            maxPP = 10,
            currentPP = 10,
            baseDamage = 10,
            critDamage = 13,
            accuracy = 95,
            type = CreatureType.Water,
            statusChance = 0.30f,
            statusToApply = StatusEffectType.Wet,
            statusDuration = 3,
            isPhysical = false,
            specialFlag = MoveFlag.None
        });

        attacks.Add(new AttackData
        {
            name = "Ancient Croak",
            maxPP = 10,
            currentPP = 10,
            baseDamage = 0,
            critDamage = 0,
            accuracy = 10,
            type = CreatureType.Dragon,
            statusChance = 1f,
            statusToApply = StatusEffectType.Anxious,
            statusDuration = 1,
            isPhysical = false,
            specialFlag = MoveFlag.None
        });
    }

    public void InitFromDefinition(CreatureDefinition def, CreatureInstance inst)
    {
        if (def == null || inst == null)
        {
            Debug.LogWarning("[CreatureCombatant] InitFromDefinition called with null data. Falling back.");
            InitWhelpling(level);
            return;
        }

        definition = def;
        instance = inst;
        autoInitWhelpling = false;

        level = Mathf.Max(1, inst.level);
        creatureName = string.IsNullOrWhiteSpace(inst.DisplayName) ? def.displayName : inst.DisplayName;
        types = def.GetAllTypes();
        statusEffects.Clear();

        CreatureStats finalStats = GetFinalStats();
        maxHP = Mathf.Max(1, finalStats.maxHP);
        attack = Mathf.Max(1, finalStats.attack);
        defense = Mathf.Max(1, finalStats.defense);
        speed = Mathf.Max(1, finalStats.speed);

        if (inst.currentHP < 0) inst.currentHP = 0;
        inst.currentHP = Mathf.Clamp(inst.currentHP, 0, maxHP);
        currentHP = inst.currentHP;

        if (inst.currentPP == null || inst.currentPP.Length < 4)
        {
            int[] resized = new int[4];
            if (inst.currentPP != null)
            {
                int copy = Mathf.Min(inst.currentPP.Length, 4);
                for (int i = 0; i < copy; i++) resized[i] = inst.currentPP[i];
            }
            inst.currentPP = resized;
        }

        attacks.Clear();
        for (int slot = 0; slot < 4; slot++)
        {
            MoveDefinition move = def.GetMoveForSlot(slot);
            if (move == null) continue;

            int maxPp = Mathf.Max(0, move.maxPP);
            int currentPp = inst.currentPP[slot];
            if (currentPp <= 0 || currentPp > maxPp)
            {
                currentPp = maxPp;
                inst.currentPP[slot] = currentPp;
            }

            AttackData attackData = new AttackData
            {
                name = string.IsNullOrWhiteSpace(move.displayName) ? move.name : move.displayName,
                maxPP = maxPp,
                currentPP = currentPp,
                baseDamage = Mathf.Max(0, move.baseDamage),
                critDamage = Mathf.Max(0, move.critDamage),
                accuracy = move.AccuracyPercent,
                type = move.moveType,
                statusChance = Mathf.Clamp01(move.statusChance),
                statusToApply = move.statusEffect == StatusEffectType.None ? (StatusEffectType?)null : move.statusEffect,
                statusDuration = move.statusDuration,
                isPhysical = move.isPhysical,
                specialFlag = move.specialFlag
            };

            if (move.isStatusMove)
            {
                attackData.baseDamage = 0;
                attackData.critDamage = 0;
            }

            attacks.Add(attackData);
        }
    }

    public CreatureStats GetFinalStats()
    {
        if (definition == null || instance == null)
        {
            CreatureStats fallback = new CreatureStats();
            fallback.maxHP = Mathf.Max(1, maxHP);
            fallback.attack = Mathf.Max(1, attack);
            fallback.defense = Mathf.Max(1, defense);
            fallback.speed = Mathf.Max(1, speed);
            fallback.accuracyModifier = 1f;
            fallback.critModifier = 1f;
            return fallback;
        }

        int lvl = Mathf.Max(1, instance.level);
        SoulTraitValues traits = instance.soulTraits;

        CreatureStats stats = new CreatureStats();
        stats.maxHP = CreatureInstanceFactory.ComputeMaxHP(definition, traits, lvl);
        stats.attack = CreatureInstanceFactory.ComputeAttack(definition, traits, lvl);
        stats.defense = CreatureInstanceFactory.ComputeDefense(definition, traits, lvl);
        stats.speed = CreatureInstanceFactory.ComputeSpeed(definition, traits, lvl);
        stats.accuracyModifier = CreatureInstanceFactory.ComputeAccuracyModifier(definition, traits, lvl);
        stats.critModifier = CreatureInstanceFactory.ComputeCritModifier(definition, traits, lvl);
        return stats;
    }

    public CreatureType[] GetResolvedTypes()
    {
        if (definition != null)
        {
            return definition.GetAllTypes();
        }

        if (types == null || types.Length == 0)
        {
            return new[] { CreatureType.Normal };
        }

        List<CreatureType> cleaned = new List<CreatureType>(types.Length);
        for (int i = 0; i < types.Length; i++)
        {
            if (types[i] == CreatureType.None) continue;
            cleaned.Add(types[i]);
        }
        if (cleaned.Count == 0) cleaned.Add(CreatureType.Normal);
        return cleaned.ToArray();
    }

    public bool HasStatus(StatusEffectType type)
    {
        if (type == StatusEffectType.None) return false;
        for (int i = 0; i < statusEffects.Count; i++)
        {
            if (statusEffects[i].type == type) return true;
        }
        return false;
    }

    public StatusEffect GetStatus(StatusEffectType type)
    {
        if (type == StatusEffectType.None) return null;
        for (int i = 0; i < statusEffects.Count; i++)
        {
            if (statusEffects[i].type == type) return statusEffects[i];
        }
        return null;
    }

    public void AddOrRefreshStatus(StatusEffectType type, int turns)
    {
        if (type == StatusEffectType.None) return;
        StatusEffect s = GetStatus(type);
        if (s == null)
        {
            statusEffects.Add(new StatusEffect { type = type, turns = turns });
        }
        else
        {
            if (s.turns < 0 || turns < 0)
            {
                s.turns = -1;
            }
            else
            {
                s.turns = Mathf.Max(s.turns, turns);
            }
        }
    }

    public void TickStatus(StatusEffectType type)
    {
        StatusEffect s = GetStatus(type);
        if (s == null) return;
        if (s.turns < 0) return;
        s.turns -= 1;
        if (s.turns <= 0)
        {
            statusEffects.Remove(s);
        }
    }

    public void SyncInstanceRuntimeState()
    {
        if (instance == null) return;
        instance.level = Mathf.Max(1, level);
        instance.currentHP = Mathf.Clamp(currentHP, 0, Mathf.Max(1, maxHP));
        if (instance.currentPP == null || instance.currentPP.Length < 4)
        {
            instance.currentPP = new int[4];
        }
        for (int i = 0; i < Mathf.Min(attacks.Count, 4); i++)
        {
            AttackData atk = attacks[i];
            if (atk == null) continue;
            instance.currentPP[i] = Mathf.Clamp(atk.currentPP, 0, atk.maxPP);
        }
    }
}
