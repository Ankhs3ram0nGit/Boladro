using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-900)]
public class WhelplingAnimationBootstrap : MonoBehaviour
{
    private static WhelplingAnimationBootstrap instance;
    private float nextScanAt;
    public bool periodicRefreshInPlayMode = false;
    public bool periodicRefreshInEditMode = true;
    [Min(0.1f)] public float refreshIntervalSeconds = 1.0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RuntimeBoot()
    {
        EnsureInstance();
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void EditorBoot()
    {
        EditorApplication.delayCall += EnsureInstance;
    }
#endif

    private static void EnsureInstance()
    {
        if (instance != null) return;
        instance = FindAnyObjectByType<WhelplingAnimationBootstrap>();
        if (instance != null) return;
        GameObject go = new GameObject("__WhelplingAnimationBootstrap");
        go.hideFlags = HideFlags.HideAndDontSave;
        instance = go.AddComponent<WhelplingAnimationBootstrap>();
    }

    private void OnEnable()
    {
        nextScanAt = 0f;
        ApplyToWhelplings();
    }

    private void Update()
    {
        bool shouldRefresh = Application.isPlaying ? periodicRefreshInPlayMode : periodicRefreshInEditMode;
        if (!shouldRefresh) return;
        if (Time.realtimeSinceStartup < nextScanAt) return;
        nextScanAt = Time.realtimeSinceStartup + Mathf.Max(0.1f, refreshIntervalSeconds);
        ApplyToWhelplings();
    }

    private static void ApplyToWhelplings()
    {
        SpriteRenderer[] srs = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        for (int i = 0; i < srs.Length; i++)
        {
            SpriteRenderer sr = srs[i];
            if (sr == null) continue;
            if (!LooksLikeWhelpling(sr)) continue;
            if (sr.GetComponent<WhelplingBounceAnimator>() != null) continue;
            sr.gameObject.AddComponent<WhelplingBounceAnimator>();
        }
    }

    private static bool LooksLikeWhelpling(SpriteRenderer sr)
    {
        string n = sr.gameObject.name.ToLowerInvariant();
        if (n.Contains("frog") || n.Contains("whelpling")) return true;

        CreatureCombatant combatant = sr.GetComponent<CreatureCombatant>();
        if (combatant != null && !string.IsNullOrEmpty(combatant.creatureName))
        {
            if (combatant.creatureName.ToLowerInvariant().Contains("whelpling")) return true;
        }

        if (sr.GetComponent<WildCreatureAI>() != null) return true;
        if (sr.GetComponent<Follower>() != null) return true;
        return false;
    }
}
