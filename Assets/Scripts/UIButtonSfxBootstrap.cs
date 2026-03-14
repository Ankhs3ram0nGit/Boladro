using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-500)]
public class UIButtonSfxBootstrap : MonoBehaviour
{
    private const string HoverSfxPath = "Assets/UI Soundpack/MP3/Abstract1.mp3";
    private const string ClickSfxPath = "Assets/UI Soundpack/MP3/Retro12.mp3";

    [Range(0f, 1f)] public float hoverVolume = 0.7f;
    [Range(0f, 1f)] public float clickVolume = 0.85f;
    [Min(0.1f)] public float refreshInterval = 0.8f;
    public AudioClip hoverSfx;
    public AudioClip clickSfx;

    private static UIButtonSfxBootstrap instance;
    private AudioSource sfxSource;
    private float nextRefreshTime;
    private readonly HashSet<int> hookedButtons = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureInstance()
    {
        if (instance != null) return;
        UIButtonSfxBootstrap existing = FindFirstObjectByType<UIButtonSfxBootstrap>();
        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject go = new GameObject("__UIButtonSfxBootstrap");
        instance = go.AddComponent<UIButtonSfxBootstrap>();
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
        EnsureAudioAssets();
        RefreshButtonHooks();
    }

    void Update()
    {
        if (Time.unscaledTime < nextRefreshTime) return;
        nextRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, refreshInterval);
        RefreshButtonHooks();
    }

    void EnsureAudioSource()
    {
        if (sfxSource != null) return;
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
    }

    void EnsureAudioAssets()
    {
#if UNITY_EDITOR
        if (hoverSfx == null)
        {
            hoverSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(HoverSfxPath);
        }
        if (clickSfx == null)
        {
            clickSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(ClickSfxPath);
        }
#endif
    }

    void RefreshButtonHooks()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (buttons == null || buttons.Length == 0) return;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (b == null) continue;
            int id = b.GetInstanceID();
            if (!hookedButtons.Add(id)) continue;
            if (b.GetComponent<UIButtonSfxReceiver>() == null)
            {
                b.gameObject.AddComponent<UIButtonSfxReceiver>();
            }
        }
    }

    public static void PlayHover()
    {
        if (instance == null) return;
        instance.EnsureAudioSource();
        instance.EnsureAudioAssets();
        if (instance.sfxSource == null || instance.hoverSfx == null) return;
        instance.sfxSource.PlayOneShot(instance.hoverSfx, Mathf.Clamp01(instance.hoverVolume));
    }

    public static void PlayClick()
    {
        if (instance == null) return;
        instance.EnsureAudioSource();
        instance.EnsureAudioAssets();
        if (instance.sfxSource == null || instance.clickSfx == null) return;
        instance.sfxSource.PlayOneShot(instance.clickSfx, Mathf.Clamp01(instance.clickVolume));
    }
}

public sealed class UIButtonSfxReceiver : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        UIButtonSfxBootstrap.PlayHover();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        UIButtonSfxBootstrap.PlayClick();
    }
}
