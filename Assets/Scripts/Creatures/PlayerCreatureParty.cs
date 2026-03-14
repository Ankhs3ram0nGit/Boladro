using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerCreatureParty : MonoBehaviour
{
    [Serializable]
    public class PartySlot
    {
        public string creatureID;
        [Range(1, 99)] public int level = 5;
    }

    [Header("Party")]
    [SerializeField] private List<PartySlot> configuredParty = new List<PartySlot>();
    [SerializeField, Range(0, 5)] private int activePartyIndex = 0;

    [Header("Testing")]
    [Tooltip("Seeds a 6-creature party if none is configured.")]
    public bool seedSixUniqueCreaturesForTesting = true;
    [Range(1, 6)] public int testPartySize = 6;
    [Range(1, 99)] public int testBaseLevel = 5;
    [Tooltip("Preferred IDs for test seeding. Missing IDs are ignored.")]
    public List<string> preferredTestCreatureIDs = new List<string>
    {
        "whelpling",
        "strikeling",
        "ashcub",
        "emberclaw",
        "frostcharge",
        "galecrown"
    };

    private readonly List<CreatureInstance> activeCreatures = new List<CreatureInstance>();
    public IReadOnlyList<CreatureInstance> ActiveCreatures => activeCreatures;
    public int ActivePartyIndex => Mathf.Clamp(activePartyIndex, 0, Mathf.Max(0, activeCreatures.Count - 1));

    public event Action PartyChanged;

    void Awake()
    {
        InitializeParty();
    }

    void OnEnable()
    {
        if (activeCreatures.Count == 0)
        {
            InitializeParty();
        }
    }

    [ContextMenu("Rebuild Party Now")]
    public void InitializeParty()
    {
        CreatureRegistry.Initialize();

        if (seedSixUniqueCreaturesForTesting && (configuredParty == null || configuredParty.Count == 0))
        {
            SeedTestPartyInternal();
        }

        RebuildActiveCreatures();
    }

    [ContextMenu("Seed 6 Unique Test Creatures")]
    public void SeedTestPartyForDebug()
    {
        SeedTestPartyInternal();
        RebuildActiveCreatures();
    }

    public void SetActivePartyIndex(int index)
    {
        int clamped = Mathf.Clamp(index, 0, Mathf.Max(0, activeCreatures.Count - 1));
        if (clamped == activePartyIndex) return;
        activePartyIndex = clamped;
        PartyChanged?.Invoke();
    }

    public bool HasAnyAliveCreatures()
    {
        for (int i = 0; i < activeCreatures.Count; i++)
        {
            CreatureInstance c = activeCreatures[i];
            if (c != null && c.currentHP > 0) return true;
        }
        return false;
    }

    public int FindFirstAlivePartyIndex()
    {
        for (int i = 0; i < activeCreatures.Count; i++)
        {
            CreatureInstance c = activeCreatures[i];
            if (c != null && c.currentHP > 0) return i;
        }
        return -1;
    }

    public int FindNextAlivePartyIndex(int startExclusive)
    {
        int count = activeCreatures.Count;
        if (count <= 0) return -1;

        int start = Mathf.Clamp(startExclusive, 0, count - 1);
        for (int step = 1; step <= count; step++)
        {
            int idx = (start + step) % count;
            CreatureInstance c = activeCreatures[idx];
            if (c != null && c.currentHP > 0) return idx;
        }

        return -1;
    }

    public bool TrySetActiveToFirstAlive()
    {
        int idx = FindFirstAlivePartyIndex();
        if (idx < 0) return false;
        SetActivePartyIndex(idx);
        return true;
    }

    public bool TrySetActiveToNextAlive()
    {
        if (activeCreatures.Count <= 0) return false;
        int idx = FindNextAlivePartyIndex(ActivePartyIndex);
        if (idx < 0) return false;
        SetActivePartyIndex(idx);
        return true;
    }

    public void ReviveAllCreaturesToFull()
    {
        if (activeCreatures == null || activeCreatures.Count == 0) return;

        bool changed = false;
        for (int i = 0; i < activeCreatures.Count; i++)
        {
            CreatureInstance inst = activeCreatures[i];
            if (inst == null) continue;

            CreatureDefinition def = CreatureRegistry.Get(inst.definitionID);
            if (def == null) continue;

            int level = Mathf.Max(1, inst.level);
            int maxHp = Mathf.Max(1, CreatureInstanceFactory.ComputeMaxHP(def, inst.soulTraits, level));
            if (inst.currentHP != maxHp)
            {
                inst.currentHP = maxHp;
                changed = true;
            }

            int[] previousPp = inst.currentPP != null ? (int[])inst.currentPP.Clone() : null;
            CreatureInstanceFactory.RefillPP(def, inst);
            if (previousPp == null || inst.currentPP == null || inst.currentPP.Length != previousPp.Length)
            {
                changed = true;
            }
            else
            {
                for (int pp = 0; pp < inst.currentPP.Length; pp++)
                {
                    if (inst.currentPP[pp] != previousPp[pp])
                    {
                        changed = true;
                        break;
                    }
                }
            }
        }

        if (activeCreatures.Count > 0)
        {
            activePartyIndex = Mathf.Clamp(activePartyIndex, 0, activeCreatures.Count - 1);
            if (activeCreatures[activePartyIndex] == null || activeCreatures[activePartyIndex].currentHP <= 0)
            {
                int firstAlive = FindFirstAlivePartyIndex();
                if (firstAlive >= 0 && firstAlive != activePartyIndex)
                {
                    activePartyIndex = firstAlive;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            PartyChanged?.Invoke();
        }
    }

    private void SeedTestPartyInternal()
    {
        CreatureRegistry.Initialize();

        if (configuredParty == null)
        {
            configuredParty = new List<PartySlot>();
        }
        else
        {
            configuredParty.Clear();
        }

        int targetCount = Mathf.Clamp(testPartySize, 1, 6);
        HashSet<string> added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (preferredTestCreatureIDs != null)
        {
            for (int i = 0; i < preferredTestCreatureIDs.Count && configuredParty.Count < targetCount; i++)
            {
                string canonical = CreatureRegistry.CanonicalizeCreatureID(preferredTestCreatureIDs[i]);
                if (string.IsNullOrWhiteSpace(canonical)) continue;
                if (added.Contains(canonical)) continue;
                if (!CreatureRegistry.TryGet(canonical, out CreatureDefinition def) || def == null) continue;

                configuredParty.Add(new PartySlot
                {
                    creatureID = def.creatureID,
                    level = Mathf.Clamp(testBaseLevel + configuredParty.Count, 1, 99)
                });
                added.Add(canonical);
            }
        }

        List<CreatureDefinition> allDefs = CreatureRegistry.GetAll()
            .Where(d => d != null)
            .OrderBy(d => d.creatureID, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int i = 0; i < allDefs.Count && configuredParty.Count < targetCount; i++)
        {
            CreatureDefinition def = allDefs[i];
            string canonical = CreatureRegistry.CanonicalizeCreatureID(def.creatureID);
            if (string.IsNullOrWhiteSpace(canonical) || added.Contains(canonical)) continue;

            configuredParty.Add(new PartySlot
            {
                creatureID = def.creatureID,
                level = Mathf.Clamp(testBaseLevel + configuredParty.Count, 1, 99)
            });
            added.Add(canonical);
        }
    }

    private void RebuildActiveCreatures()
    {
        CreatureRegistry.Initialize();
        activeCreatures.Clear();

        if (configuredParty == null)
        {
            configuredParty = new List<PartySlot>();
        }

        int maxCount = Mathf.Min(6, configuredParty.Count);
        for (int i = 0; i < maxCount; i++)
        {
            PartySlot slot = configuredParty[i];
            if (slot == null) continue;

            string canonical = CreatureRegistry.CanonicalizeCreatureID(slot.creatureID);
            if (!CreatureRegistry.TryGet(canonical, out CreatureDefinition def) || def == null) continue;

            int level = Mathf.Clamp(slot.level, 1, 99);
            CreatureInstance instance = CreatureInstanceFactory.CreateWild(def, level);
            instance.definitionID = def.creatureID;
            instance.ownerID = "player";
            instance.ownershipState = OwnershipState.Captured;
            if (string.IsNullOrWhiteSpace(instance.creatureUID))
            {
                instance.creatureUID = Guid.NewGuid().ToString("N");
            }

            activeCreatures.Add(instance);
        }

        activePartyIndex = Mathf.Clamp(activePartyIndex, 0, Mathf.Max(0, activeCreatures.Count - 1));

        PartyChanged?.Invoke();
    }
}
