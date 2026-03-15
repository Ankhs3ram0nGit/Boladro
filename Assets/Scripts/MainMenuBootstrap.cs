using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-900)]
public class MainMenuBootstrap : MonoBehaviour
{
    private const string RuntimeMenuSceneName = "__RuntimeMainMenuScene";
    private const string PrimaryMenuTexturePath = "Assets/UI Soundpack/Menu Image FR.png";
    private const string FallbackMenuTexturePath = "Assets/ChatGPT Image Mar 12, 2026, 08_41_45 PM.png";
    private const string MenuPanelFramePath = "Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_Frame01a.png";
    private const string MenuButtonNormalPath = "Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_Button01a_1.png";
    private const string MenuButtonHighlightPath = "Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_Button01a_2.png";
    private const string MenuButtonPressedPath = "Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_Button01a_3.png";

    private static MainMenuBootstrap instance;
    private static bool sessionStarted;
    public static bool IsMenuOpen => instance != null && instance.menuActive && !sessionStarted;

    private string gameplayScenePath;
    private string gameplaySceneName;
    private int gameplaySceneBuildIndex = -1;
    private bool menuActive;

    private Scene runtimeMenuScene;
    private Canvas menuCanvas;
    private RectTransform playSubmenuRoot;
    private Text noticeText;
    private readonly List<GameObject> runtimeObjects = new List<GameObject>(32);
    private readonly List<EventSystemState> suspendedEventSystems = new List<EventSystemState>(4);

    private Camera menuCamera;
    private Button settingsButton;
    private Button creditsButton;
    private Button exitButton;
    private Sprite menuButtonNormal;
    private Sprite menuButtonHighlight;
    private Sprite menuButtonPressed;
    private Sprite menuPanelFrame;
    private Texture2D menuBackgroundTexture;
    private Font menuFont;

    [Header("Background Framing")]
    [Range(1f, 1.3f)] public float backgroundZoom = 1.02f;

    private struct EventSystemState
    {
        public EventSystem eventSystem;
        public bool wasEnabled;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        sessionStarted = false;
    }

#if UNITY_EDITOR
    [InitializeOnEnterPlayMode]
    private static void ResetStaticsOnEnterPlayMode(EnterPlayModeOptions options)
    {
        instance = null;
        sessionStarted = false;
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstanceBeforeSceneLoad()
    {
        EnsureInstanceInternal();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceAfterSceneLoad()
    {
        EnsureInstanceInternal();
    }

    private static void EnsureInstanceInternal()
    {
        if (sessionStarted && instance == null) sessionStarted = false;
        if (sessionStarted) return;
        if (instance != null) return;

        MainMenuBootstrap existing = FindFirstObjectByType<MainMenuBootstrap>();
        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject go = new GameObject("__MainMenuBootstrap");
        instance = go.AddComponent<MainMenuBootstrap>();
    }

    private void Awake()
    {
        if (sessionStarted)
        {
            Destroy(gameObject);
            return;
        }
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        Scene activeScene = SceneManager.GetActiveScene();
        CaptureGameplaySceneReferenceFromScene(activeScene);
        EnsureRuntimeMenuScene();
        try
        {
            EnsureMenuFont();
            BuildMenuUi();
            menuActive = true;
        }
        catch (Exception ex)
        {
            Debug.LogError("MainMenuBootstrap: Failed to build menu UI. " + ex.Message);
            try
            {
                EnsureMenuFont();
                BuildEmergencyMenuUi();
            }
            catch (Exception emergencyEx)
            {
                Debug.LogError("MainMenuBootstrap: Emergency menu failed. " + emergencyEx.Message);
            }
            menuActive = true;
        }
        StartCoroutine(EnterMenuStateRoutine());
    }

    private IEnumerator EnterMenuStateRoutine()
    {
        yield return null;
        ApplyMenuPauseState(true);
        EnsureMenuCamera();

        List<Scene> loadedScenes = new List<Scene>(SceneManager.sceneCount);
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (!s.IsValid() || !s.isLoaded) continue;
            loadedScenes.Add(s);
        }

        Scene fallbackGameplayScene = default;
        for (int i = 0; i < loadedScenes.Count; i++)
        {
            Scene scene = loadedScenes[i];
            if (string.Equals(scene.name, RuntimeMenuSceneName, StringComparison.Ordinal)) continue;

            bool isRuntimeScene = scene.buildIndex < 0 && string.IsNullOrEmpty(scene.path);
            if (isRuntimeScene) continue;

            if (!fallbackGameplayScene.IsValid())
            {
                fallbackGameplayScene = scene;
            }
        }

        if (fallbackGameplayScene.IsValid())
        {
            CaptureGameplaySceneReferenceFromScene(fallbackGameplayScene);
        }

        for (int i = 0; i < loadedScenes.Count; i++)
        {
            Scene scene = loadedScenes[i];
            if (string.Equals(scene.name, RuntimeMenuSceneName, StringComparison.Ordinal)) continue;
            bool isRuntimeScene = scene.buildIndex < 0 && string.IsNullOrEmpty(scene.path);
            if (isRuntimeScene) continue;

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null)
            {
                yield return unload;
            }
            else
            {
                Debug.LogWarning("MainMenuBootstrap: scene unload request returned null for " + scene.name);
            }
        }
    }

    private void CaptureGameplaySceneReferenceFromScene(Scene active)
    {
        if (!active.IsValid())
        {
            gameplayScenePath = string.Empty;
            gameplaySceneName = "SampleScene";
            gameplaySceneBuildIndex = -1;
            return;
        }

        gameplayScenePath = active.path;
        gameplaySceneName = active.name;
        gameplaySceneBuildIndex = active.buildIndex;

        if (string.Equals(gameplaySceneName, RuntimeMenuSceneName, StringComparison.Ordinal))
        {
            gameplaySceneName = "SampleScene";
            gameplayScenePath = string.Empty;
            gameplaySceneBuildIndex = -1;
        }
    }

    private Scene ResolveCapturedGameplayScene()
    {
        if (gameplaySceneBuildIndex >= 0)
        {
            Scene byIndex = SceneManager.GetSceneByBuildIndex(gameplaySceneBuildIndex);
            if (byIndex.IsValid()) return byIndex;
        }
        if (!string.IsNullOrEmpty(gameplayScenePath))
        {
            Scene byPath = SceneManager.GetSceneByPath(gameplayScenePath);
            if (byPath.IsValid()) return byPath;
        }
        if (!string.IsNullOrEmpty(gameplaySceneName))
        {
            Scene byName = SceneManager.GetSceneByName(gameplaySceneName);
            if (byName.IsValid()) return byName;
        }
        return default;
    }

    private void EnsureRuntimeMenuScene()
    {
        runtimeMenuScene = SceneManager.GetSceneByName(RuntimeMenuSceneName);
        if (!runtimeMenuScene.IsValid())
        {
            runtimeMenuScene = SceneManager.CreateScene(RuntimeMenuSceneName);
        }

        SceneManager.MoveGameObjectToScene(gameObject, runtimeMenuScene);
        SceneManager.SetActiveScene(runtimeMenuScene);
    }

    private void EnsureMenuCamera()
    {
        if (menuCamera != null) return;

        GameObject cameraGo = new GameObject("MainMenuCamera", typeof(Camera));
        runtimeObjects.Add(cameraGo);
        SceneManager.MoveGameObjectToScene(cameraGo, runtimeMenuScene);
        cameraGo.transform.SetParent(transform, false);

        Camera cam = cameraGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.cullingMask = 0;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 10f;
        cam.depth = -1000f;
        cam.enabled = true;

        menuCamera = cam;
    }

    private static void ApplyMenuPauseState(bool pause)
    {
        Time.timeScale = pause ? 0f : 1f;
        AudioListener.pause = pause;
    }

    private void BuildMenuUi()
    {
        EnsureMenuAssetsLoaded();
        EnsureEventSystem();
        EnsureMenuCamera();

        GameObject canvasGo = new GameObject("MainMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        runtimeObjects.Add(canvasGo);
        SceneManager.MoveGameObjectToScene(canvasGo, runtimeMenuScene);
        canvasGo.transform.SetParent(transform, false);

        menuCanvas = canvasGo.GetComponent<Canvas>();
        menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        menuCanvas.sortingOrder = short.MaxValue - 32;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        StretchRect(canvasRect);

        GameObject bgHolderGo = CreateUiObject("BackgroundHolder", canvasRect);
        RectTransform bgHolder = bgHolderGo.GetComponent<RectTransform>();
        StretchRect(bgHolder);

        GameObject bgGo = CreateUiObject("Background", bgHolder);
        RawImage bg = bgGo.AddComponent<RawImage>();
        RectTransform bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 0.5f);
        bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.anchoredPosition = Vector2.zero;
        bgRect.sizeDelta = new Vector2(1920f, 1080f);
        bgRect.localScale = Vector3.one * Mathf.Max(1f, backgroundZoom);
        bg.texture = menuBackgroundTexture;
        bg.color = Color.white;

        AspectRatioFitter bgFitter = bgGo.AddComponent<AspectRatioFitter>();
        bgFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        if (menuBackgroundTexture != null && menuBackgroundTexture.height > 0)
        {
            bgFitter.aspectRatio = (float)menuBackgroundTexture.width / menuBackgroundTexture.height;
        }
        else
        {
            bgFitter.aspectRatio = 16f / 9f;
        }

        GameObject dimGo = CreateUiObject("Dim", canvasRect);
        Image dim = dimGo.AddComponent<Image>();
        RectTransform dimRect = dim.rectTransform;
        StretchRect(dimRect);
        // Keep original menu art colors untouched.
        dim.color = new Color(0f, 0f, 0f, 0f);

        GameObject leftPanelGo = CreateUiObject("MenuPanel", canvasRect);
        Image leftPanel = leftPanelGo.AddComponent<Image>();
        if (menuPanelFrame != null)
        {
            leftPanel.sprite = menuPanelFrame;
            leftPanel.type = menuPanelFrame.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            leftPanel.color = new Color(1f, 1f, 1f, 0.95f);
        }
        else
        {
            leftPanel.color = new Color(0f, 0f, 0f, 0.52f);
        }
        RectTransform panelRect = leftPanel.rectTransform;
        panelRect.anchorMin = new Vector2(0.01f, 0.10f);
        panelRect.anchorMax = new Vector2(0.24f, 0.90f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        RectTransform menuRoot = CreateUiObject("MenuButtons", leftPanel.rectTransform).GetComponent<RectTransform>();
        menuRoot.anchorMin = new Vector2(0.5f, 0.5f);
        menuRoot.anchorMax = new Vector2(0.5f, 0.5f);
        menuRoot.pivot = new Vector2(0.5f, 0.5f);
        menuRoot.sizeDelta = new Vector2(320f, 560f);
        menuRoot.anchoredPosition = Vector2.zero;

        const float buttonWidth = 301f; // 30% shorter than previous 430.

        Button playButton = CreateMenuButton(menuRoot, "Play", new Vector2(0f, 190f), new Vector2(buttonWidth, 76f), 42);
        playButton.onClick.AddListener(TogglePlaySubmenu);

        playSubmenuRoot = CreateUiObject("PlaySubmenu", menuRoot).GetComponent<RectTransform>();
        playSubmenuRoot.anchorMin = new Vector2(0.5f, 0.5f);
        playSubmenuRoot.anchorMax = new Vector2(0.5f, 0.5f);
        playSubmenuRoot.pivot = new Vector2(0.5f, 0.5f);
        playSubmenuRoot.sizeDelta = new Vector2(buttonWidth, 90f);
        playSubmenuRoot.anchoredPosition = new Vector2(0f, 100f);
        playSubmenuRoot.gameObject.SetActive(false);

        // Align directly with Play button, centered in the same frame.
        Button newGameButton = CreateMenuButton(playSubmenuRoot, "New Game", Vector2.zero, new Vector2(buttonWidth, 64f), 34);
        newGameButton.onClick.AddListener(StartNewGame);

        settingsButton = CreateMenuButton(menuRoot, "Settings", new Vector2(0f, 40f), new Vector2(buttonWidth, 76f), 42);
        settingsButton.onClick.AddListener(() => ShowNotice("Settings coming soon."));

        creditsButton = CreateMenuButton(menuRoot, "Credits", new Vector2(0f, -60f), new Vector2(buttonWidth, 76f), 42);
        creditsButton.onClick.AddListener(() => ShowNotice("Credits coming soon."));

        exitButton = CreateMenuButton(menuRoot, "Exit", new Vector2(0f, -160f), new Vector2(buttonWidth, 76f), 42);
        exitButton.onClick.AddListener(ExitGame);

        noticeText = CreateLabel(canvasRect, string.Empty, new Vector2(0f, 26f), new Vector2(1500f, 56f), 28, TextAnchor.MiddleCenter);
        noticeText.color = new Color(1f, 1f, 1f, 0.95f);
        AddOutline(noticeText.gameObject, new Color(0f, 0f, 0f, 0.85f), new Vector2(2f, -2f));
    }

    private void BuildEmergencyMenuUi()
    {
        EnsureEventSystem();
        EnsureMenuCamera();

        GameObject canvasGo = new GameObject("MainMenuCanvasEmergency", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        runtimeObjects.Add(canvasGo);
        SceneManager.MoveGameObjectToScene(canvasGo, runtimeMenuScene);
        canvasGo.transform.SetParent(transform, false);

        menuCanvas = canvasGo.GetComponent<Canvas>();
        menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        menuCanvas.sortingOrder = short.MaxValue - 16;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = canvasGo.GetComponent<RectTransform>();
        StretchRect(root);

        Image bg = CreateUiObject("EmergencyBG", root).AddComponent<Image>();
        StretchRect(bg.rectTransform);
        bg.color = new Color(0f, 0f, 0f, 0.88f);

        Button newGameButton = CreateMenuButton(root, "New Game", new Vector2(0f, 10f), new Vector2(420f, 76f), 38);
        newGameButton.onClick.AddListener(StartNewGame);

        Button exitButton = CreateMenuButton(root, "Exit", new Vector2(0f, -92f), new Vector2(420f, 76f), 38);
        exitButton.onClick.AddListener(ExitGame);

        noticeText = CreateLabel(root, "Fallback menu active.", new Vector2(0f, -200f), new Vector2(1200f, 60f), 28, TextAnchor.MiddleCenter);
    }

    private void EnsureMenuAssetsLoaded()
    {
        menuBackgroundTexture = TryLoadTexture(PrimaryMenuTexturePath);
        if (menuBackgroundTexture == null)
        {
            menuBackgroundTexture = TryLoadTexture(FallbackMenuTexturePath);
        }

#if UNITY_EDITOR
        if (menuPanelFrame == null)
        {
            menuPanelFrame = AssetDatabase.LoadAssetAtPath<Sprite>(MenuPanelFramePath);
        }
        if (menuButtonNormal == null)
        {
            menuButtonNormal = AssetDatabase.LoadAssetAtPath<Sprite>(MenuButtonNormalPath);
        }
        if (menuButtonHighlight == null)
        {
            menuButtonHighlight = AssetDatabase.LoadAssetAtPath<Sprite>(MenuButtonHighlightPath);
        }
        if (menuButtonPressed == null)
        {
            menuButtonPressed = AssetDatabase.LoadAssetAtPath<Sprite>(MenuButtonPressedPath);
        }
#endif
    }

    private void EnsureEventSystem()
    {
        EventSystem[] all = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            EventSystem es = all[i];
            if (es == null) continue;
            suspendedEventSystems.Add(new EventSystemState
            {
                eventSystem = es,
                wasEnabled = es.enabled
            });
            es.enabled = false;
        }

        GameObject esGo = new GameObject("MainMenuEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        runtimeObjects.Add(esGo);
        SceneManager.MoveGameObjectToScene(esGo, runtimeMenuScene);
    }

    private Button CreateMenuButton(RectTransform parent, string label, Vector2 anchoredPos, Vector2 size, int fontSize)
    {
        GameObject go = CreateUiObject(label + "Button", parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image image = go.AddComponent<Image>();
        Color accentYellow = new Color(0.96f, 0.83f, 0.25f, 1f);
        if (menuButtonNormal != null)
        {
            image.sprite = menuButtonNormal;
            image.type = menuButtonNormal.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            image.color = accentYellow;
        }
        else
        {
            image.color = accentYellow;
        }

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;

        SpriteState state = button.spriteState;
        state.highlightedSprite = menuButtonHighlight != null ? menuButtonHighlight : menuButtonNormal;
        state.pressedSprite = menuButtonPressed != null ? menuButtonPressed : menuButtonNormal;
        state.selectedSprite = state.highlightedSprite;
        button.spriteState = state;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;

        Text text = CreateLabel(rt, label, Vector2.zero, size, fontSize, TextAnchor.MiddleCenter);
        text.fontStyle = FontStyle.Bold;
        text.color = new Color(0.12f, 0.10f, 0.05f, 1f);
        AddOutline(text.gameObject, new Color(0f, 0f, 0f, 0.9f), new Vector2(2f, -2f));

        if (go.GetComponent<UIButtonSfxReceiver>() == null) go.AddComponent<UIButtonSfxReceiver>();
        if (go.GetComponent<MenuButtonPressAnimator>() == null) go.AddComponent<MenuButtonPressAnimator>();
        return button;
    }

    private Text CreateLabel(RectTransform parent, string content, Vector2 anchoredPos, Vector2 size, int fontSize, TextAnchor align)
    {
        GameObject go = CreateUiObject("Label", parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Text text = go.AddComponent<Text>();
        text.text = content;
        text.font = menuFont;
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = new Color(1f, 1f, 1f, 0.98f);
        return text;
    }

    private void TogglePlaySubmenu()
    {
        if (playSubmenuRoot == null) return;
        bool next = !playSubmenuRoot.gameObject.activeSelf;
        playSubmenuRoot.gameObject.SetActive(next);
        SetPrimaryMenuButtonsVisible(!next);
        if (next) ShowNotice(string.Empty);
    }

    private void SetPrimaryMenuButtonsVisible(bool visible)
    {
        if (settingsButton != null) settingsButton.gameObject.SetActive(visible);
        if (creditsButton != null) creditsButton.gameObject.SetActive(visible);
        if (exitButton != null) exitButton.gameObject.SetActive(visible);
    }

    private void StartNewGame()
    {
        if (sessionStarted) return;
        StartCoroutine(StartNewGameRoutine());
    }

    private IEnumerator StartNewGameRoutine()
    {
        sessionStarted = true;
        menuActive = false;
        ApplyMenuPauseState(false);
        if (menuCanvas != null) menuCanvas.enabled = false;

        AsyncOperation loadOp = null;
        if (gameplaySceneBuildIndex >= 0)
        {
            loadOp = SceneManager.LoadSceneAsync(gameplaySceneBuildIndex, LoadSceneMode.Single);
        }
        else if (!string.IsNullOrEmpty(gameplayScenePath))
        {
            string cleanPath = gameplayScenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                ? gameplayScenePath.Substring(0, gameplayScenePath.Length - 6)
                : gameplayScenePath;
            loadOp = SceneManager.LoadSceneAsync(cleanPath, LoadSceneMode.Single);
        }
        else
        {
            string fallbackName = string.IsNullOrEmpty(gameplaySceneName) ? "SampleScene" : gameplaySceneName;
            loadOp = SceneManager.LoadSceneAsync(fallbackName, LoadSceneMode.Single);
        }

        if (loadOp != null) yield return loadOp;

        CleanupRuntimeMenuObjects();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            ApplyMenuPauseState(false);
            instance = null;
        }
    }

    private void CleanupRuntimeMenuObjects()
    {
        for (int i = 0; i < suspendedEventSystems.Count; i++)
        {
            EventSystemState state = suspendedEventSystems[i];
            if (state.eventSystem != null)
            {
                state.eventSystem.enabled = state.wasEnabled;
            }
        }
        suspendedEventSystems.Clear();

        for (int i = 0; i < runtimeObjects.Count; i++)
        {
            GameObject go = runtimeObjects[i];
            if (go != null)
            {
                Destroy(go);
            }
        }
        runtimeObjects.Clear();
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowNotice(string message)
    {
        if (noticeText == null) return;
        noticeText.text = message ?? string.Empty;
    }

    private static void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.GetComponent<Outline>();
        if (outline == null) outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    private static void StretchRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static GameObject CreateUiObject(string name, Transform parent, params Type[] extraComponents)
    {
        List<Type> components = new List<Type>(2 + (extraComponents != null ? extraComponents.Length : 0));
        components.Add(typeof(RectTransform));
        if (extraComponents != null)
        {
            for (int i = 0; i < extraComponents.Length; i++)
            {
                if (extraComponents[i] != null) components.Add(extraComponents[i]);
            }
        }

        GameObject go = new GameObject(name, components.ToArray());
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Texture2D TryLoadTexture(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
#if UNITY_EDITOR
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null) return tex;
#endif
        return null;
    }

    private void EnsureMenuFont()
    {
        if (menuFont != null) return;

        try
        {
            menuFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        catch
        {
            // Ignore and try fallback below.
        }

        if (menuFont == null)
        {
            try
            {
                menuFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch
            {
                // Last resort below.
            }
        }

        if (menuFont == null)
        {
            Font[] anyFonts = Resources.FindObjectsOfTypeAll<Font>();
            if (anyFonts != null && anyFonts.Length > 0)
            {
                menuFont = anyFonts[0];
            }
        }
    }

}
