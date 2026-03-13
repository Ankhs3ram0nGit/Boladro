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
    Dark
}

public enum StatusEffectType
{
    Burn,
    Wet,
    Frozen,
    Anxious,
    Paralysed,
    Poisoned
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
    public bool isPhysical;
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
    public int speed = 5;
    public bool autoInitWhelpling = true;

    public List<AttackData> attacks = new List<AttackData>();
    public List<StatusEffect> statusEffects = new List<StatusEffect>();

    void Awake()
    {
        if (autoInitWhelpling)
        {
            InitWhelpling(level);
        }
    }

    public void InitWhelpling(int lvl)
    {
        creatureName = "Whelpling";
        types = new CreatureType[] { CreatureType.Dragon, CreatureType.Water, CreatureType.Fire };
        level = Mathf.Max(1, lvl);
        maxHP = 25 + (level - 1) * 4;
        currentHP = Mathf.Clamp(currentHP <= 0 ? maxHP : currentHP, 1, maxHP);
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
            isPhysical = true
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
            isPhysical = false
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
            isPhysical = false
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
            isPhysical = false
        });
    }

    public bool HasStatus(StatusEffectType type)
    {
        for (int i = 0; i < statusEffects.Count; i++)
        {
            if (statusEffects[i].type == type) return true;
        }
        return false;
    }

    public StatusEffect GetStatus(StatusEffectType type)
    {
        for (int i = 0; i < statusEffects.Count; i++)
        {
            if (statusEffects[i].type == type) return statusEffects[i];
        }
        return null;
    }

    public void AddOrRefreshStatus(StatusEffectType type, int turns)
    {
        StatusEffect s = GetStatus(type);
        if (s == null)
        {
            statusEffects.Add(new StatusEffect { type = type, turns = turns });
        }
        else
        {
            s.turns = Mathf.Max(s.turns, turns);
        }
    }

    public void TickStatus(StatusEffectType type)
    {
        StatusEffect s = GetStatus(type);
        if (s == null) return;
        s.turns -= 1;
        if (s.turns <= 0)
        {
            statusEffects.Remove(s);
        }
    }
}
