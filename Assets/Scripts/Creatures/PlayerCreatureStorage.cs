using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerCreatureStorage : MonoBehaviour
{
    [Header("Storage Capacity")]
    [Min(5)] public int slotsPerRow = 5;
    [Min(5)] public int rowsPerPage = 5;
    [Min(30)] public int pageCount = 30;

    [Header("References")]
    [SerializeField] private PlayerCreatureParty party;

    [SerializeField] private List<CreatureInstance> storedCreatures = new List<CreatureInstance>();

    public IReadOnlyList<CreatureInstance> StoredCreatures => storedCreatures;
    public int Capacity => Mathf.Max(1, slotsPerRow) * Mathf.Max(1, rowsPerPage) * Mathf.Max(1, pageCount);
    public int Count => storedCreatures != null ? storedCreatures.Count : 0;

    public event Action StorageChanged;

    void Awake()
    {
        EnsureInitialized();
    }

    public void EnsureInitialized(PlayerCreatureParty explicitParty = null)
    {
        slotsPerRow = Mathf.Max(5, slotsPerRow);
        rowsPerPage = Mathf.Max(5, rowsPerPage);
        pageCount = Mathf.Max(30, pageCount);

        if (explicitParty != null)
        {
            party = explicitParty;
        }
        if (party == null)
        {
            party = GetComponent<PlayerCreatureParty>();
        }
        if (party == null)
        {
            party = gameObject.AddComponent<PlayerCreatureParty>();
        }

        if (storedCreatures == null)
        {
            storedCreatures = new List<CreatureInstance>();
        }

        int cap = Capacity;
        if (storedCreatures.Count > cap)
        {
            storedCreatures.RemoveRange(cap, storedCreatures.Count - cap);
            StorageChanged?.Invoke();
        }
    }

    public bool HasSpace()
    {
        return Count < Capacity;
    }

    public CreatureInstance GetAt(int index)
    {
        if (storedCreatures == null) return null;
        if (index < 0 || index >= storedCreatures.Count) return null;
        return storedCreatures[index];
    }

    public bool TryStoreCreature(CreatureInstance instance)
    {
        EnsureInitialized();
        if (instance == null) return false;
        if (!HasSpace()) return false;

        if (!NormalizeCapturedInstance(instance)) return false;
        storedCreatures.Add(instance);
        StorageChanged?.Invoke();
        return true;
    }

    public bool TryAddCapturedCreature(CreatureInstance instance, out bool addedToParty)
    {
        EnsureInitialized();
        addedToParty = false;
        if (instance == null) return false;
        if (!NormalizeCapturedInstance(instance)) return false;

        if (party != null && party.HasSpaceInParty())
        {
            if (party.TryAddCapturedCreature(instance))
            {
                addedToParty = true;
                return true;
            }
        }

        return TryStoreCreature(instance);
    }

    bool NormalizeCapturedInstance(CreatureInstance instance)
    {
        if (instance == null) return false;
        CreatureRegistry.Initialize();

        string canonical = CreatureRegistry.CanonicalizeCreatureID(instance.definitionID);
        CreatureDefinition def = CreatureRegistry.Get(canonical);
        if (def == null) return false;

        instance.definitionID = def.creatureID;
        instance.ownerID = "player";
        instance.ownershipState = OwnershipState.Captured;
        instance.level = Mathf.Clamp(instance.level, 1, CreatureExperienceSystem.MaxLevel);
        if (string.IsNullOrWhiteSpace(instance.creatureUID))
        {
            instance.creatureUID = Guid.NewGuid().ToString("N");
        }

        int maxHp = Mathf.Max(1, CreatureInstanceFactory.ComputeMaxHP(def, instance.soulTraits, instance.level));
        instance.currentHP = Mathf.Clamp(instance.currentHP, 0, maxHp);
        if (instance.currentPP == null || instance.currentPP.Length < 4)
        {
            int[] pp = new int[4];
            if (instance.currentPP != null)
            {
                int copy = Mathf.Min(4, instance.currentPP.Length);
                for (int i = 0; i < copy; i++) pp[i] = instance.currentPP[i];
            }
            instance.currentPP = pp;
        }

        return true;
    }
}
