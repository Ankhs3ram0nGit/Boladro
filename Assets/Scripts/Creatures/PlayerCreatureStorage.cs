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
    public int Count
    {
        get
        {
            if (storedCreatures == null) return 0;
            int occupied = 0;
            for (int i = 0; i < storedCreatures.Count; i++)
            {
                if (storedCreatures[i] != null) occupied++;
            }
            return occupied;
        }
    }

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
        return FindFirstEmptyIndex() >= 0;
    }

    public CreatureInstance GetAt(int index)
    {
        if (storedCreatures == null) return null;
        if (index < 0 || index >= storedCreatures.Count) return null;
        return storedCreatures[index];
    }

    public int FindFirstEmptyIndex()
    {
        if (storedCreatures == null) return 0;
        int cap = Capacity;
        int limit = Mathf.Min(storedCreatures.Count, cap);
        for (int i = 0; i < limit; i++)
        {
            if (storedCreatures[i] == null) return i;
        }
        if (storedCreatures.Count < cap) return storedCreatures.Count;
        return -1;
    }

    public bool TrySetAt(int index, CreatureInstance instance, out CreatureInstance replaced)
    {
        replaced = null;
        EnsureInitialized();
        if (index < 0 || index >= Capacity) return false;
        if (instance != null && !NormalizeCapturedInstance(instance)) return false;

        EnsureBackingSize(index + 1);
        replaced = storedCreatures[index];
        storedCreatures[index] = instance;
        TrimTrailingNulls();
        StorageChanged?.Invoke();
        return true;
    }

    public bool TryTakeAt(int index, out CreatureInstance removed)
    {
        removed = null;
        EnsureInitialized();
        if (index < 0 || index >= Capacity) return false;
        if (index >= storedCreatures.Count) return false;

        removed = storedCreatures[index];
        storedCreatures[index] = null;
        TrimTrailingNulls();
        StorageChanged?.Invoke();
        return true;
    }

    public bool TryStoreCreature(CreatureInstance instance)
    {
        EnsureInitialized();
        if (instance == null) return false;
        if (!HasSpace()) return false;

        if (!NormalizeCapturedInstance(instance)) return false;
        int target = FindFirstEmptyIndex();
        if (target < 0) return false;
        EnsureBackingSize(target + 1);
        storedCreatures[target] = instance;
        TrimTrailingNulls();
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

    void EnsureBackingSize(int size)
    {
        if (storedCreatures == null) storedCreatures = new List<CreatureInstance>();
        int clamped = Mathf.Clamp(size, 0, Capacity);
        while (storedCreatures.Count < clamped)
        {
            storedCreatures.Add(null);
        }
    }

    void TrimTrailingNulls()
    {
        if (storedCreatures == null) return;
        for (int i = storedCreatures.Count - 1; i >= 0; i--)
        {
            if (storedCreatures[i] != null) break;
            storedCreatures.RemoveAt(i);
        }
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
