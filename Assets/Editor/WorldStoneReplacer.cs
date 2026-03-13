#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WorldStoneReplacer
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string PropsTexturePath = "Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Props.png";

    private static readonly string[] AllowedStoneSpriteNames =
    {
        "TX Props - Stone 02",
        "TX Props - Stone 03",
        "TX Props - Stone 04",
        "TX Props - Stone 05",
        "TX Props - Stone 06"
    };

    [MenuItem("Tools/Map/Replace World Stones (Stone 02-06)")]
    public static void ReplaceWorldStonesMenu()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        ReplaceWorldStonesInScene(activeScene, saveScene: true);
    }

    public static void ReplaceWorldStonesBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ReplaceWorldStonesInScene(scene, saveScene: true);
        EditorApplication.Exit(0);
    }

    private static void ReplaceWorldStonesInScene(Scene scene, bool saveScene)
    {
        List<Sprite> allowedSprites = LoadAllowedStoneSprites();
        if (allowedSprites.Count == 0)
        {
            Debug.LogError("[WorldStoneReplacer] Failed: no allowed stone sprites were found in TX Props.");
            return;
        }

        SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        int replaced = 0;
        int spriteIndex = 0;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null || !sr.gameObject.scene.IsValid()) continue;
            if (scene.IsValid() && sr.gameObject.scene != scene) continue;
            if (!LooksLikeWorldStone(sr)) continue;

            SpriteFromAtlas atlas = sr.GetComponent<SpriteFromAtlas>();
            if (atlas != null) atlas.enabled = false;

            sr.sprite = allowedSprites[spriteIndex % allowedSprites.Count];
            spriteIndex++;
            replaced++;
            EditorUtility.SetDirty(sr);
        }

        if (saveScene && scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log("[WorldStoneReplacer] Replaced " + replaced + " world stone sprites using Stone 02-06.");
    }

    private static List<Sprite> LoadAllowedStoneSprites()
    {
        List<Sprite> sprites = new List<Sprite>();
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(PropsTexturePath);
        if (all == null || all.Length == 0) return sprites;

        for (int i = 0; i < AllowedStoneSpriteNames.Length; i++)
        {
            string target = AllowedStoneSpriteNames[i];
            for (int j = 0; j < all.Length; j++)
            {
                Sprite s = all[j] as Sprite;
                if (s == null) continue;
                if (!string.Equals(s.name, target, System.StringComparison.OrdinalIgnoreCase)) continue;
                sprites.Add(s);
                break;
            }
        }

        return sprites;
    }

    private static bool LooksLikeWorldStone(SpriteRenderer sr)
    {
        if (sr == null) return false;
        if (sr.GetComponentInParent<Canvas>() != null) return false;

        string objectName = sr.gameObject.name.ToLowerInvariant();
        string rootName = sr.transform.root != null ? sr.transform.root.name.ToLowerInvariant() : string.Empty;
        string spriteName = sr.sprite != null ? sr.sprite.name.ToLowerInvariant() : string.Empty;

        bool isStoneLike =
            objectName.Contains("stone") || objectName.Contains("rock") ||
            rootName.Contains("stone") || rootName.Contains("rock") ||
            spriteName.Contains("stone") || spriteName.Contains("rock");

        return isStoneLike;
    }
}
#endif
