using System;
using System.Collections.Generic;
using UnityEngine;

public static class CreatureRegistry
{
    private static Dictionary<string, CreatureDefinition> registry;
    private static Dictionary<string, string> aliasToCanonical;
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized && registry != null) return;

        registry = new Dictionary<string, CreatureDefinition>(StringComparer.OrdinalIgnoreCase);
        aliasToCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        CreatureDefinition[] all = Resources.LoadAll<CreatureDefinition>("Creatures/Definitions");
        for (int i = 0; i < all.Length; i++)
        {
            CreatureDefinition def = all[i];
            if (def == null) continue;

            string key = NormalizeKey(def.creatureID, keepUnderscore: true);
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogWarning("[CreatureRegistry] Definition missing creatureID on asset: " + def.name);
                continue;
            }

            if (registry.ContainsKey(key))
            {
                Debug.LogWarning("[CreatureRegistry] Duplicate creatureID '" + key + "' found. Keeping first, ignoring " + def.name);
                continue;
            }

            registry[key] = def;
            aliasToCanonical[key] = key;

            string displayAlias = NormalizeKey(def.displayName, keepUnderscore: true);
            if (!string.IsNullOrWhiteSpace(displayAlias))
            {
                aliasToCanonical[displayAlias] = key;
            }

            string compact = NormalizeKey(def.creatureID, keepUnderscore: false);
            if (!string.IsNullOrWhiteSpace(compact))
            {
                aliasToCanonical[compact] = key;
            }
        }

        // Explicit alias support for doc IDs to current canonical IDs.
        AddAlias("solnox", "solnox_the_eternal");
        AddAlias("solnoxeternal", "solnox_the_eternal");
        AddAlias("solnox_the_eternal", "solnox_the_eternal");
        AddAlias("solnoxtheeternal", "solnox_the_eternal");

        AddAlias("zypheron", "zypheron_the_unyielding");
        AddAlias("zypheronunyielding", "zypheron_the_unyielding");
        AddAlias("zypheron_the_unyielding", "zypheron_the_unyielding");
        AddAlias("zypherontheunyielding", "zypheron_the_unyielding");

        initialized = true;
        Debug.Log("[CreatureRegistry] initialized with " + registry.Count + " definitions.");
    }

    public static CreatureDefinition Get(string creatureID)
    {
        Initialize();
        if (TryGet(creatureID, out CreatureDefinition def)) return def;
        Debug.LogWarning("[CreatureRegistry] Missing CreatureDefinition for ID '" + creatureID + "'.");
        return null;
    }

    public static bool TryGet(string creatureID, out CreatureDefinition def)
    {
        Initialize();
        def = null;
        if (registry == null || registry.Count == 0) return false;
        string canonical = CanonicalizeCreatureID(creatureID);
        if (string.IsNullOrWhiteSpace(canonical)) return false;
        return registry.TryGetValue(canonical, out def);
    }

    public static IEnumerable<CreatureDefinition> GetAll()
    {
        Initialize();
        return registry.Values;
    }

    public static string CanonicalizeCreatureID(string creatureID)
    {
        Initialize();
        string normalizedUnderscore = NormalizeKey(creatureID, keepUnderscore: true);
        string normalizedCompact = NormalizeKey(creatureID, keepUnderscore: false);

        if (!string.IsNullOrWhiteSpace(normalizedUnderscore))
        {
            if (aliasToCanonical.TryGetValue(normalizedUnderscore, out string mappedA)) return mappedA;
            if (registry.ContainsKey(normalizedUnderscore)) return normalizedUnderscore;
        }

        if (!string.IsNullOrWhiteSpace(normalizedCompact))
        {
            if (aliasToCanonical.TryGetValue(normalizedCompact, out string mappedB)) return mappedB;
        }

        return normalizedUnderscore;
    }

    public static string NormalizeKey(string raw, bool keepUnderscore = true)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        string k = raw.Trim().ToLowerInvariant();
        k = k.Replace("-", "_");
        k = k.Replace(" ", keepUnderscore ? "_" : string.Empty);
        if (!keepUnderscore) k = k.Replace("_", string.Empty);
        return k;
    }

    private static void AddAlias(string alias, string canonical)
    {
        string a = NormalizeKey(alias, keepUnderscore: true);
        string c = NormalizeKey(canonical, keepUnderscore: true);
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(c)) return;
        aliasToCanonical[a] = c;
        aliasToCanonical[NormalizeKey(a, keepUnderscore: false)] = c;
    }
}
