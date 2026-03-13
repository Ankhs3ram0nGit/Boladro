#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CreatureDefinitionSeeder
{
    private const string DefinitionsRoot = "Assets/Resources/Creatures/Definitions";
    private const string MovesRoot = "Assets/Resources/Creatures/Moves";
    private const string ProfilesRoot = "Assets/Resources/Creatures/GrowthProfiles";
    private const string SeedVersionKey = "Boladro.CreatureDefinitionSeeder.Version";
    private const string SeedVersion = "2026-03-13-v1";

    [InitializeOnLoadMethod]
    private static void AutoSeedOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetString(SeedVersionKey, string.Empty) == SeedVersion) return;
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;

            bool ok = SeedOrUpdateAll(overwriteExisting: true, logSummary: true);
            if (ok)
            {
                EditorPrefs.SetString(SeedVersionKey, SeedVersion);
            }
        };
    }

    [MenuItem("Tools/Creatures/Seed Or Update Creature Assets")]
    public static void SeedFromMenu()
    {
        SeedOrUpdateAll(overwriteExisting: true, logSummary: true);
    }

    private static bool SeedOrUpdateAll(bool overwriteExisting, bool logSummary)
    {
        EnsureFolders();

        Dictionary<string, MoveDefinition> moves = SeedMoves(overwriteExisting);
        Dictionary<string, SoulTraitGrowthProfile> profiles = SeedGrowthProfiles(overwriteExisting);
        Dictionary<string, CreatureDefinition> defs = SeedCreatureDefinitions(overwriteExisting, moves, profiles);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (logSummary)
        {
            Debug.Log("[CreatureDefinitionSeeder] Completed. Definitions: " + defs.Count + ", Moves: " + moves.Count + ", Profiles: " + profiles.Count + ".");
        }

        return defs.Count >= 9 && moves.Count >= 10 && profiles.Count >= 3;
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Creatures");
        EnsureFolder(DefinitionsRoot);
        EnsureFolder(MovesRoot);
        EnsureFolder(ProfilesRoot);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        if (slash <= 0) return;
        string parent = path.Substring(0, slash);
        string name = path.Substring(slash + 1);
        EnsureFolder(parent);
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static Dictionary<string, MoveDefinition> SeedMoves(bool overwriteExisting)
    {
        Dictionary<string, MoveDefinition> result = new Dictionary<string, MoveDefinition>();

        AddMove(result, overwriteExisting, "claw_strike", "Claw Strike", CreatureType.Normal, 5, 7, 1.0f, 20, StatusEffectType.None, 0f, 0, false, MoveFlag.None, true);
        AddMove(result, overwriteExisting, "ember_spit", "Ember Spit", CreatureType.Fire, 15, 20, 0.9f, 5, StatusEffectType.Burn, 0.05f, 3, false, MoveFlag.None, false);
        AddMove(result, overwriteExisting, "torrent_splash", "Torrent Splash", CreatureType.Water, 10, 13, 0.95f, 10, StatusEffectType.Wet, 0.30f, 3, false, MoveFlag.None, false);
        AddMove(result, overwriteExisting, "ancient_croak", "Ancient Croak", CreatureType.Dragon, 0, 0, 0.1f, 10, StatusEffectType.Anxious, 1.0f, 1, true, MoveFlag.None, false);

        AddMove(result, overwriteExisting, "shadowclaw", "Shadowclaw", CreatureType.Dark, 6, 9, 1.0f, 20, StatusEffectType.None, 0f, 0, false, MoveFlag.None, true);
        AddMove(result, overwriteExisting, "goldvein_pulse", "Goldvein Pulse", CreatureType.Light, 12, 16, 0.9f, 10, StatusEffectType.Blind, 0.10f, 2, false, MoveFlag.None, false);
        AddMove(result, overwriteExisting, "voidstep", "Voidstep", CreatureType.Dark, 0, 0, 0.95f, 15, StatusEffectType.None, 0f, 0, true, MoveFlag.GuaranteeDodge, false);
        AddMove(result, overwriteExisting, "eclipse_roar", "Eclipse Roar", CreatureType.Light, 18, 25, 0.85f, 5, StatusEffectType.Terrified, 0.20f, 2, false, MoveFlag.None, false);
        AddMove(result, overwriteExisting, "voidrend", "Voidrend", CreatureType.Dark, 14, 18, 1.0f, 20, StatusEffectType.Blind, 0.15f, 2, false, MoveFlag.None, true);
        AddMove(result, overwriteExisting, "solarstrike", "Solarstrike", CreatureType.Light, 20, 26, 0.9f, 10, StatusEffectType.None, 0f, 0, false, MoveFlag.None, true);
        AddMove(result, overwriteExisting, "eclipse_roar_plus", "Eclipse Roar+", CreatureType.Light, 22, 30, 0.85f, 5, StatusEffectType.Terrified, 0.25f, 2, false, MoveFlag.None, false);
        AddMove(result, overwriteExisting, "abyssal_slash", "Abyssal Slash", CreatureType.Dark, 22, 30, 1.0f, 20, StatusEffectType.Blind, 0.20f, 2, false, MoveFlag.None, true);
        AddMove(result, overwriteExisting, "crowned_light", "Crowned Light", CreatureType.Light, 30, 40, 0.85f, 10, StatusEffectType.None, 0f, 0, false, MoveFlag.None, false);
        AddMove(result, overwriteExisting, "phantom_shift", "Phantom Shift", CreatureType.Dark, 0, 0, 0.95f, 15, StatusEffectType.None, 0f, 0, true, MoveFlag.GuaranteeDodge, false);
        AddMove(result, overwriteExisting, "total_eclipse", "Total Eclipse", CreatureType.Light, 28, 38, 0.80f, 5, StatusEffectType.Terrified, 0.35f, 2, false, MoveFlag.None, false);
        AddMove(result, overwriteExisting, "eternal_darkness", "Eternal Darkness", CreatureType.Dark, 35, 48, 1.0f, 20, StatusEffectType.Blind, 0.25f, 2, false, MoveFlag.None, true);
        AddMove(result, overwriteExisting, "solarburst", "Solarburst", CreatureType.Light, 45, 60, 0.85f, 10, StatusEffectType.None, 0f, 0, false, MoveFlag.None, false);
        AddMove(result, overwriteExisting, "solnox_judgement", "Solnox Judgement", CreatureType.Dragon, 55, 75, 0.80f, 5, StatusEffectType.Terrified, 0.50f, 2, false, MoveFlag.DragonBonus, false);

        AddMove(result, overwriteExisting, "sparkhorn", "Sparkhorn", CreatureType.Lightning, 7, 10, 1.0f, 20, StatusEffectType.Paralysed, 0.10f, 2, false, MoveFlag.None, true);
        AddMove(result, overwriteExisting, "frostbreath", "Frostbreath", CreatureType.Ice, 10, 14, 0.95f, 10, StatusEffectType.Wet, 0.30f, 3, false, MoveFlag.None, false);
        AddMove(result, overwriteExisting, "thunderhoof", "Thunderhoof", CreatureType.Lightning, 16, 22, 0.9f, 10, StatusEffectType.Paralysed, 0.15f, 2, false, MoveFlag.DoubleDamageIfStatused, true);
        AddMove(result, overwriteExisting, "glacial_storm", "Glacial Storm", CreatureType.Ice, 20, 28, 0.85f, 5, StatusEffectType.Wet, 0.80f, 3, false, MoveFlag.DoubleDamageIfStatused, false);
        AddMove(result, overwriteExisting, "crackling_antler", "Crackling Antler", CreatureType.Lightning, 14, 19, 1.0f, 20, StatusEffectType.Paralysed, 0.15f, 2, false, MoveFlag.None, true);
        AddMove(result, overwriteExisting, "crystal_exhale", "Crystal Exhale", CreatureType.Ice, 18, 24, 0.95f, 10, StatusEffectType.Wet, 0.40f, 3, false, MoveFlag.None, false);
        AddMove(result, overwriteExisting, "thunderhoof_plus", "Thunderhoof+", CreatureType.Lightning, 20, 28, 0.9f, 10, StatusEffectType.Paralysed, 0.15f, 2, false, MoveFlag.DoubleDamageIfStatused, true);
        AddMove(result, overwriteExisting, "glacial_storm_plus", "Glacial Storm+", CreatureType.Ice, 26, 35, 0.85f, 5, StatusEffectType.Wet, 0.80f, 3, false, MoveFlag.DoubleDamageIfStatused, false);
        AddMove(result, overwriteExisting, "stormrend", "Stormrend", CreatureType.Lightning, 22, 30, 1.0f, 20, StatusEffectType.Paralysed, 0.20f, 2, false, MoveFlag.None, true);
        AddMove(result, overwriteExisting, "blizzard_breath", "Blizzard Breath", CreatureType.Ice, 26, 35, 0.95f, 10, StatusEffectType.Wet, 0.50f, 3, false, MoveFlag.None, false);
        AddMove(result, overwriteExisting, "seismic_bolt", "Seismic Bolt", CreatureType.Lightning, 30, 42, 0.9f, 10, StatusEffectType.Paralysed, 0.20f, 2, false, MoveFlag.DoubleDamageIfStatused, true);
        AddMove(result, overwriteExisting, "frozen_tempest", "Frozen Tempest", CreatureType.Ice, 32, 44, 0.85f, 5, StatusEffectType.Wet, 1.0f, 3, false, MoveFlag.DoubleDamageIfStatused, false);
        AddMove(result, overwriteExisting, "heavens_discharge", "Heaven's Discharge", CreatureType.Lightning, 38, 52, 1.0f, 20, StatusEffectType.Paralysed, 0.25f, 2, false, MoveFlag.None, true);
        AddMove(result, overwriteExisting, "absolute_zero", "Absolute Zero", CreatureType.Ice, 40, 55, 0.95f, 10, StatusEffectType.Wet, 0.60f, 3, false, MoveFlag.None, false);
        AddMove(result, overwriteExisting, "judgement_bolt", "Judgement Bolt", CreatureType.Lightning, 48, 65, 0.9f, 10, StatusEffectType.Paralysed, 1.0f, 2, false, MoveFlag.DoubleDamageIfStatused, true);
        AddMove(result, overwriteExisting, "zypheron_wrath", "Zypheron Wrath", CreatureType.Dragon, 60, 82, 0.80f, 5, StatusEffectType.Paralysed, 1.0f, 2, false, MoveFlag.DragonBonus, false);

        return result;
    }

    private static void AddMove(Dictionary<string, MoveDefinition> map, bool overwriteExisting, string id, string name, CreatureType type, int baseDamage, int critDamage, float accuracy, int maxPP, StatusEffectType statusEffect, float statusChance, int statusDuration, bool isStatusMove, MoveFlag flag, bool isPhysical)
    {
        string path = MovesRoot + "/" + id + ".asset";
        MoveDefinition asset = AssetDatabase.LoadAssetAtPath<MoveDefinition>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<MoveDefinition>();
            AssetDatabase.CreateAsset(asset, path);
            overwriteExisting = true;
        }

        if (overwriteExisting)
        {
            asset.moveID = id;
            asset.displayName = name;
            asset.moveType = type;
            asset.baseDamage = baseDamage;
            asset.critDamage = critDamage;
            asset.accuracy = Mathf.Clamp01(accuracy);
            asset.maxPP = Mathf.Max(1, maxPP);
            asset.statusEffect = statusEffect;
            asset.statusChance = Mathf.Clamp01(statusChance);
            asset.statusDuration = statusDuration;
            asset.isStatusMove = isStatusMove;
            asset.specialFlag = flag;
            asset.isPhysical = isPhysical;
            EditorUtility.SetDirty(asset);
        }

        map[id] = asset;
    }

    private static Dictionary<string, SoulTraitGrowthProfile> SeedGrowthProfiles(bool overwriteExisting)
    {
        Dictionary<string, SoulTraitGrowthProfile> map = new Dictionary<string, SoulTraitGrowthProfile>();

        AddProfile(map, overwriteExisting, "whelpling_growth_profile", 2.5f, 1.5f, 2.0f, 1.0f, 1.5f, 0.5f);
        AddProfile(map, overwriteExisting, "solnox_growth_profile", 2.0f, 2.0f, 1.5f, 1.5f, 2.5f, 1.0f);
        AddProfile(map, overwriteExisting, "zypheron_growth_profile", 1.5f, 3.0f, 1.0f, 3.0f, 2.0f, 0.5f);

        return map;
    }

    private static void AddProfile(Dictionary<string, SoulTraitGrowthProfile> map, bool overwriteExisting, string id, float vit, float str, float ward, float gale, float focus, float soulDepth)
    {
        string path = ProfilesRoot + "/" + id + ".asset";
        SoulTraitGrowthProfile asset = AssetDatabase.LoadAssetAtPath<SoulTraitGrowthProfile>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<SoulTraitGrowthProfile>();
            AssetDatabase.CreateAsset(asset, path);
            overwriteExisting = true;
        }

        if (overwriteExisting)
        {
            asset.vitalitySparkGrowth = vit;
            asset.strikeEssenceGrowth = str;
            asset.wardEssenceGrowth = ward;
            asset.galeEssenceGrowth = gale;
            asset.focusEssenceGrowth = focus;
            asset.soulDepthGrowth = soulDepth;
            EditorUtility.SetDirty(asset);
        }

        map[id] = asset;
    }

    private static Dictionary<string, CreatureDefinition> SeedCreatureDefinitions(bool overwriteExisting, Dictionary<string, MoveDefinition> moves, Dictionary<string, SoulTraitGrowthProfile> profiles)
    {
        Dictionary<string, CreatureDefinition> map = new Dictionary<string, CreatureDefinition>();

        AddDefinition(map, overwriteExisting, "whelpling", "Whelpling", "Assets/Creatures/whelpling.png", CreatureRarity.Common, 1, CreatureType.Dragon, CreatureType.Water, CreatureType.Fire, WildBehaviour.Neutral, 2.0f, 3.0f, 1.5f, 3.5f, 2.5f, 30, 6, 8, 5, 7f, 2.5f, 3f, 1.5f, "claw_strike", "ember_spit", "torrent_splash", "ancient_croak", "whelpling_growth_profile", 1.0f, 1.0f);
        AddDefinition(map, overwriteExisting, "ashcub", "Ashcub", "Assets/Creatures/Ashcub.png", CreatureRarity.Rare, 1, CreatureType.Light, CreatureType.Dark, CreatureType.Dragon, WildBehaviour.Neutral, 2.5f, 3.5f, 1.8f, 4.0f, 2.6f, 30, 8, 6, 7, 7f, 3f, 2f, 2f, "shadowclaw", "goldvein_pulse", "voidstep", "eclipse_roar", "solnox_growth_profile", 1.0f, 1.0f);
        AddDefinition(map, overwriteExisting, "emberclaw", "Emberclaw", "Assets/Creatures/Emberclaw.png", CreatureRarity.Rare, 2, CreatureType.Light, CreatureType.Dark, CreatureType.Dragon, WildBehaviour.Neutral, 2.8f, 4.0f, 1.8f, 4.5f, 2.8f, 58, 20, 15, 20, 7f, 3f, 2f, 2f, "voidrend", "solarstrike", "voidstep", "eclipse_roar_plus", "solnox_growth_profile", 1.0f, 1.3f);
        AddDefinition(map, overwriteExisting, "voidmane", "Voidmane", "Assets/Creatures/Voidmane.png", CreatureRarity.Elite, 3, CreatureType.Light, CreatureType.Dark, CreatureType.Dragon, WildBehaviour.Aggressive, 3.0f, 4.5f, 1.6f, 5.0f, 4.2f, 94, 38, 27, 38, 7f, 3f, 2f, 2f, "abyssal_slash", "crowned_light", "phantom_shift", "total_eclipse", "solnox_growth_profile", 1.0f, 1.6f);
        AddDefinition(map, overwriteExisting, "solnox_the_eternal", "Solnox the Eternal", "Assets/Creatures/Solnox the Eternal.png", CreatureRarity.Legendary, 4, CreatureType.Light, CreatureType.Dark, CreatureType.Dragon, WildBehaviour.Aggressive, 3.2f, 5.0f, 1.5f, 6.0f, 5.0f, 130, 56, 39, 56, 7f, 3f, 2f, 2f, "eternal_darkness", "solarburst", "phantom_shift", "solnox_judgement", "solnox_growth_profile", 1.0f, 2.0f);

        AddDefinition(map, overwriteExisting, "strikeling", "Strikeling", "Assets/Creatures/Strikeling.png", CreatureRarity.Common, 1, CreatureType.Lightning, CreatureType.Ice, CreatureType.Dragon, WildBehaviour.Neutral, 3.5f, 4.0f, 2.0f, 3.5f, 2.4f, 28, 10, 5, 10, 6f, 4f, 2f, 3f, "sparkhorn", "frostbreath", "thunderhoof", "glacial_storm", "zypheron_growth_profile", 1.0f, 1.0f);
        AddDefinition(map, overwriteExisting, "frostcharge", "Frostcharge", "Assets/Creatures/Frostcharge.png", CreatureRarity.Rare, 2, CreatureType.Lightning, CreatureType.Ice, CreatureType.Dragon, WildBehaviour.Neutral, 3.8f, 4.5f, 1.9f, 4.5f, 2.8f, 54, 26, 13, 28, 6f, 4f, 2f, 3f, "crackling_antler", "crystal_exhale", "thunderhoof_plus", "glacial_storm_plus", "zypheron_growth_profile", 1.0f, 1.3f);
        AddDefinition(map, overwriteExisting, "galecrown", "Galecrown", "Assets/Creatures/Galecrown.png", CreatureRarity.Elite, 3, CreatureType.Lightning, CreatureType.Ice, CreatureType.Dragon, WildBehaviour.Aggressive, 4.0f, 5.0f, 1.8f, 5.5f, 4.5f, 88, 50, 25, 55, 6f, 4f, 2f, 3f, "stormrend", "blizzard_breath", "seismic_bolt", "frozen_tempest", "zypheron_growth_profile", 1.0f, 1.7f);
        AddDefinition(map, overwriteExisting, "zypheron_the_unyielding", "Zypheron the Unyielding", "Assets/Creatures/Zypheron the Unyielding.png", CreatureRarity.Legendary, 4, CreatureType.Lightning, CreatureType.Ice, CreatureType.Dragon, WildBehaviour.Aggressive, 4.5f, 6.0f, 1.7f, 7.0f, 6.0f, 124, 74, 37, 82, 6f, 4f, 2f, 3f, "heavens_discharge", "absolute_zero", "judgement_bolt", "zypheron_wrath", "zypheron_growth_profile", 1.0f, 2.0f);

        LinkEvolution(map, "whelpling", "emberclaw", EvolutionTrigger.LevelThreshold, 20, 0, EvolutionRelic.None);
        LinkEvolution(map, "ashcub", "emberclaw", EvolutionTrigger.LevelThreshold, 18, 0, EvolutionRelic.None);
        LinkEvolution(map, "emberclaw", "voidmane", EvolutionTrigger.LevelPlusCondition, 36, 40, EvolutionRelic.None);
        LinkEvolution(map, "voidmane", "solnox_the_eternal", EvolutionTrigger.SpecialItem, 0, 0, EvolutionRelic.AncientRiftstone);
        LinkEvolution(map, "strikeling", "frostcharge", EvolutionTrigger.LevelThreshold, 18, 0, EvolutionRelic.None);
        LinkEvolution(map, "frostcharge", "galecrown", EvolutionTrigger.LevelPlusCondition, 36, 40, EvolutionRelic.None);
        LinkEvolution(map, "galecrown", "zypheron_the_unyielding", EvolutionTrigger.SpecialItem, 0, 0, EvolutionRelic.AncientRiftstone);

        return map;

        void AddDefinition(Dictionary<string, CreatureDefinition> dMap, bool overwrite, string id, string displayName, string spritePath, CreatureRarity rarity, int stage, CreatureType primary, CreatureType secondary, CreatureType tertiary, WildBehaviour behaviour, float moveSpeed, float wanderRadius, float fleeMult, float detectionRadius, float aggroRadius, int baseHP, int baseAtk, int baseDef, int baseSpd, float hpPer, float atkPer, float defPer, float spdPer, string move1, string move2, string move3, string move4, string profileId, float worldScale, float battleScale)
        {
            string path = DefinitionsRoot + "/" + id + ".asset";
            CreatureDefinition asset = AssetDatabase.LoadAssetAtPath<CreatureDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CreatureDefinition>();
                AssetDatabase.CreateAsset(asset, path);
                overwrite = true;
            }

            if (overwrite)
            {
                asset.creatureID = id;
                asset.displayName = displayName;
                asset.description = displayName;
                asset.rarityTier = rarity;
                asset.evolutionStage = stage;
                asset.primaryType = primary;
                asset.secondaryType = secondary;
                asset.tertiaryType = tertiary;
                asset.quaternaryType = CreatureType.None;
                asset.sprite = LoadSprite(spritePath);
                asset.shadowSprite = null;
                asset.facingDirection = Vector2.right;
                asset.idleAnimationClip = null;
                asset.overworldSizeMultiplier = worldScale;
                asset.battleSizeMultiplier = battleScale;
                asset.wildBehaviour = behaviour;
                asset.moveSpeed = moveSpeed;
                asset.wanderRadius = wanderRadius;
                asset.fleeSpeedMultiplier = fleeMult;
                asset.detectionRadius = detectionRadius;
                asset.aggroRadius = Mathf.Min(aggroRadius, detectionRadius);
                asset.baseHP = baseHP;
                asset.baseAttack = baseAtk;
                asset.baseDefense = baseDef;
                asset.baseSpeed = baseSpd;
                asset.hpPerLevel = hpPer;
                asset.atkPerLevel = atkPer;
                asset.defPerLevel = defPer;
                asset.spdPerLevel = spdPer;
                asset.moveSlot1 = moves.TryGetValue(move1, out MoveDefinition m1) ? m1 : null;
                asset.moveSlot2 = moves.TryGetValue(move2, out MoveDefinition m2) ? m2 : null;
                asset.moveSlot3 = moves.TryGetValue(move3, out MoveDefinition m3) ? m3 : null;
                asset.moveSlot4 = moves.TryGetValue(move4, out MoveDefinition m4) ? m4 : null;
                asset.soulTraitGrowthProfile = profiles.TryGetValue(profileId, out SoulTraitGrowthProfile gp) ? gp : null;
                asset.nextEvolution = null;
                asset.evolutionTrigger = EvolutionTrigger.LevelThreshold;
                asset.evolutionLevel = 0;
                asset.evolutionBattleCount = 0;
                asset.evolutionItem = EvolutionRelic.None;
                asset.encounterCry = null;
                asset.battleCry = null;
                asset.faintSound = null;
                EditorUtility.SetDirty(asset);
            }

            dMap[id] = asset;
        }
    }

    private static void LinkEvolution(Dictionary<string, CreatureDefinition> map, string from, string to, EvolutionTrigger trigger, int level, int battles, EvolutionRelic relic)
    {
        if (!map.TryGetValue(from, out CreatureDefinition source)) return;
        CreatureDefinition target = null;
        if (!string.IsNullOrWhiteSpace(to))
        {
            map.TryGetValue(to, out target);
        }

        source.nextEvolution = target;
        source.evolutionTrigger = trigger;
        source.evolutionLevel = level;
        source.evolutionBattleCount = battles;
        source.evolutionItem = relic;
        EditorUtility.SetDirty(source);
    }

    private static Sprite LoadSprite(string assetPath)
    {
        Sprite direct = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (direct != null) return direct;
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < all.Length; i++)
        {
            Sprite s = all[i] as Sprite;
            if (s != null) return s;
        }
        return null;
    }
}
#endif
