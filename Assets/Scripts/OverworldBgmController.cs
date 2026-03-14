using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-450)]
public class OverworldBgmController : MonoBehaviour
{
    private const string OverworldBgmPath = "Assets/UI Soundpack/16-Bit Fantasy & Adventure Music [no AI].wav";

    [Range(0f, 1f)] public float bgmVolume = 0.2f;
    [Min(0.01f)] public float fadeInDuration = 1.1f;
    [Min(0.01f)] public float fadeOutDuration = 0.8f;
    [Min(0.05f)] public float statePollInterval = 0.08f;
    public AudioClip overworldBgm;

    private static OverworldBgmController instance;
    private AudioSource musicSource;
    private float currentTargetVolume;
    private float currentFadeDuration;
    private bool initialized;
    private bool wasInBattle;
    private float nextStatePollTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureInstance()
    {
        if (instance != null) return;
        OverworldBgmController existing = FindFirstObjectByType<OverworldBgmController>();
        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject go = new GameObject("__OverworldBgmController");
        instance = go.AddComponent<OverworldBgmController>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureAudioSource();
        EnsureAudioClipLoaded();
        initialized = false;
        currentTargetVolume = 0f;
        currentFadeDuration = Mathf.Max(0.01f, fadeInDuration);
    }

    void Update()
    {
        if (musicSource == null) return;
        if (!initialized)
        {
            initialized = true;
            wasInBattle = BattleSystem.IsEngagedBattleActive;
            ApplyBattleState(wasInBattle, true);
        }

        if (Time.unscaledTime >= nextStatePollTime)
        {
            nextStatePollTime = Time.unscaledTime + Mathf.Max(0.05f, statePollInterval);
            bool inBattle = BattleSystem.IsEngagedBattleActive;
            if (inBattle != wasInBattle)
            {
                wasInBattle = inBattle;
                ApplyBattleState(inBattle, false);
            }
        }

        float dt = Time.unscaledDeltaTime;
        float duration = Mathf.Max(0.01f, currentFadeDuration);
        float step = dt / duration;
        musicSource.volume = Mathf.MoveTowards(musicSource.volume, currentTargetVolume, step * Mathf.Max(0f, bgmVolume));

        if (currentTargetVolume > 0f && !musicSource.isPlaying && overworldBgm != null)
        {
            musicSource.UnPause();
            if (!musicSource.isPlaying) musicSource.Play();
        }

        if (currentTargetVolume <= 0f && musicSource.volume <= 0.0001f && musicSource.isPlaying)
        {
            musicSource.Pause();
        }
    }

    void OnApplicationQuit()
    {
        if (musicSource != null)
        {
            musicSource.volume = 0f;
            musicSource.Stop();
        }
    }

    void EnsureAudioSource()
    {
        if (musicSource != null) return;
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = 0f;
    }

    void EnsureAudioClipLoaded()
    {
        if (overworldBgm != null) return;
#if UNITY_EDITOR
        overworldBgm = AssetDatabase.LoadAssetAtPath<AudioClip>(OverworldBgmPath);
#endif
        if (musicSource != null) musicSource.clip = overworldBgm;
    }

    void ApplyBattleState(bool inBattle, bool immediate)
    {
        EnsureAudioClipLoaded();

        if (inBattle)
        {
            currentTargetVolume = 0f;
            currentFadeDuration = immediate ? 0.01f : Mathf.Max(0.01f, fadeOutDuration);
            return;
        }

        if (overworldBgm == null) return;
        if (musicSource.clip != overworldBgm) musicSource.clip = overworldBgm;
        if (!musicSource.isPlaying)
        {
            musicSource.volume = 0f;
            musicSource.Play();
        }
        currentTargetVolume = Mathf.Clamp01(bgmVolume);
        currentFadeDuration = immediate ? 0.01f : Mathf.Max(0.01f, fadeInDuration);
    }
}

