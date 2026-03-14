using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-700)]
public class MainMenuBootstrap : MonoBehaviour
{
    private const string PrimaryMenuArtPath = "Assets/UI/MainMenuArt.png";
    private const string FallbackMenuArtPath = "Assets/ChatGPT Image Mar 12, 2026, 08_41_45 PM.png";
    private const string MenuButtonNormalPath = "Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_Button01a_1.png";
    private const string MenuButtonHighlightPath = "Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_Button01a_2.png";
    private const string MenuButtonPressedPath = "Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_Button01a_3.png";
    private const string SaveFolderName = "Saves";

    private static MainMenuBootstrap instance;
    private static bool sessionStarted;

    private readonly List<BehaviourState> suspendedBehaviours = new List<BehaviourState>(64);
    private readonly List<GameObjectState> hiddenObjects = new List<GameObjectState>(16);
    private readonly List<GameObjectState> suspendedRootObjects = new List<GameObjectState>(64);
    private readonly List<GameObject> runtimeCreatedObjects = new List<GameObject>(24);
    private readonly List<Button> menuButtons = new List<Button>(16);
    private readonly List<Text> saveEntryLabels = new List<Text>(16);

    private Canvas menuCanvas;
    private RectTransform playSubmenuRoot;
    private RectTransform saveListRoot;
    private Text noticeText;
    private Sprite menuButtonNormal;
    private Sprite menuButtonHighlight;
    private Sprite menuButtonPressed;
    private bool suspendedGameplay;
    private float previousTimeScale = 1f;
    private float previousFixedDeltaTime = 0.02f;

    private struct BehaviourState
    {
        public Behaviour behaviour;
        public bool wasEnabled;
    }

    private struct GameObjectState
    {
        public GameObject target;
        public bool wasActive;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        sessionStarted = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
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
        SuspendGameplay();
        BuildMenuUi();
    }

    private void OnDestroy()
    {
        if (!sessionStarted)
        {
            ResumeGameplay();
        }
    }

    private void SuspendGameplay()
    {
        if (suspendedGameplay) return;
        suspendedGameplay = true;

        previousTimeScale = Time.timeScale;
        previousFixedDeltaTime = Time.fixedDeltaTime;
        Time.timeScale = 0f;
        Time.fixedDeltaTime = previousFixedDeltaTime;

        SuspendActiveSceneRoots();

        SuspendAllOfType<PlayerMover>();
        SuspendAllOfType<PlayerToolController>();
        SuspendAllOfType<TreeHoverSelector>();
        SuspendAllOfType<BattleSystem>();
        SuspendAllOfType<EncounterTrigger>();
        SuspendAllOfType<SpawnToBattleBridge>();
        SuspendAllOfType<InventoryUI>();
        SuspendAllOfType<InventoryHotbar>();
        SuspendAllOfType<MiniMapController>();
        SuspendAllOfType<SpawnManager>();

        HideNamedObject("HUD");
        HideNamedObject("GameOverUI");
        HideAllOfType<CreaturePartySidebarUI>();
    }

    private void ResumeGameplay()
    {
        if (!suspendedGameplay) return;
        suspendedGameplay = false;

        for (int i = 0; i < suspendedRootObjects.Count; i++)
        {
            GameObjectState state = suspendedRootObjects[i];
            if (state.target != null)
            {
                state.target.SetActive(state.wasActive);
            }
        }
        suspendedRootObjects.Clear();

        for (int i = 0; i < suspendedBehaviours.Count; i++)
        {
            BehaviourState state = suspendedBehaviours[i];
            if (state.behaviour != null)
            {
                state.behaviour.enabled = state.wasEnabled;
            }
        }
        suspendedBehaviours.Clear();

        for (int i = 0; i < hiddenObjects.Count; i++)
        {
            GameObjectState state = hiddenObjects[i];
            if (state.target != null)
            {
                state.target.SetActive(state.wasActive);
            }
        }
        hiddenObjects.Clear();

        Time.timeScale = previousTimeScale;
        Time.fixedDeltaTime = previousFixedDeltaTime;
    }

    private void SuspendActiveSceneRoots()
    {
        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid() || !active.isLoaded) return;

        GameObject[] roots = active.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null) continue;
            suspendedRootObjects.Add(new GameObjectState
            {
                target = root,
                wasActive = root.activeSelf
            });
            if (root.activeSelf)
            {
                root.SetActive(false);
            }
        }
    }

    private void SuspendAllOfType<T>() where T : Behaviour
    {
        T[] all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            T behaviour = all[i];
            if (behaviour == null) continue;
            suspendedBehaviours.Add(new BehaviourState
            {
                behaviour = behaviour,
                wasEnabled = behaviour.enabled
            });
            behaviour.enabled = false;
        }
    }

    private void HideAllOfType<T>() where T : Component
    {
        T[] all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            T component = all[i];
            if (component == null || component.gameObject == null) continue;
            hiddenObjects.Add(new GameObjectState
            {
                target = component.gameObject,
                wasActive = component.gameObject.activeSelf
            });
            component.gameObject.SetActive(false);
        }
    }

    private void HideNamedObject(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        if (target == null) return;
        hiddenObjects.Add(new GameObjectState
        {
            target = target,
            wasActive = target.activeSelf
        });
        target.SetActive(false);
    }

    private void BuildMenuUi()
    {
        EnsureButtonSpritesLoaded();

        GameObject canvasGo = new GameObject("MainMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        runtimeCreatedObjects.Add(canvasGo);
        menuCanvas = canvasGo.GetComponent<Canvas>();
        menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        menuCanvas.sortingOrder = short.MaxValue - 32;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        EnsureEventSystemExists();

        GameObject bgGo = CreateUiObject("Background", canvasRect);
        Image bg = bgGo.AddComponent<Image>();
        RectTransform bgRect = bgGo.GetComponent<RectTransform>();
        StretchRect(bgRect);
        Sprite menuArt = TryLoadMainMenuArt();
        if (menuArt != null)
        {
            bg.sprite = menuArt;
            bg.color = Color.white;
            bg.type = Image.Type.Simple;
            bg.preserveAspect = false;
        }
        else
        {
            bg.color = new Color(0.11f, 0.19f, 0.27f, 1f);
        }

        GameObject dimGo = CreateUiObject("DimOverlay", canvasRect);
        Image dim = dimGo.AddComponent<Image>();
        StretchRect(dim.rectTransform);
        dim.color = new Color(0f, 0f, 0f, 0.26f);

        GameObject titleGo = CreateUiObject("Title", canvasRect);
        Text title = titleGo.AddComponent<Text>();
        title.text = "RIFTBORN";
        title.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        title.alignment = TextAnchor.MiddleCenter;
        title.fontStyle = FontStyle.Bold;
        title.fontSize = 128;
        title.color = new Color(1f, 0.92f, 0.42f, 1f);
        AddOutline(titleGo, new Color(0f, 0f, 0f, 0.85f), new Vector2(3f, -3f));
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(1200f, 180f);
        titleRect.anchoredPosition = new Vector2(0f, -40f);

        GameObject menuContainerGo = CreateUiObject("MenuContainer", canvasRect);
        RectTransform menuContainer = menuContainerGo.GetComponent<RectTransform>();
        menuContainer.anchorMin = new Vector2(0.20f, 0.5f);
        menuContainer.anchorMax = new Vector2(0.20f, 0.5f);
        menuContainer.pivot = new Vector2(0f, 0.5f);
        menuContainer.sizeDelta = new Vector2(520f, 560f);
        menuContainer.anchoredPosition = new Vector2(0f, -30f);

        VerticalLayoutGroup vlg = menuContainerGo.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.spacing = 14f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = false;

        ContentSizeFitter fitter = menuContainerGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Button playButton = CreateMenuButton(menuContainer, "Play", 420f, 72f);
        playButton.onClick.AddListener(TogglePlaySubmenu);

        GameObject playSubGo = CreateUiObject("PlaySubmenu", menuContainer);
        playSubmenuRoot = playSubGo.GetComponent<RectTransform>();
        VerticalLayoutGroup subLayout = playSubGo.AddComponent<VerticalLayoutGroup>();
        subLayout.childAlignment = TextAnchor.UpperLeft;
        subLayout.spacing = 8f;
        subLayout.childControlWidth = true;
        subLayout.childControlHeight = false;
        subLayout.childForceExpandHeight = false;
        subLayout.childForceExpandWidth = false;
        ContentSizeFitter subFitter = playSubGo.AddComponent<ContentSizeFitter>();
        subFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        subFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        playSubmenuRoot.gameObject.SetActive(false);

        Button newGameButton = CreateMenuButton(playSubmenuRoot, "New Game", 380f, 64f);
        newGameButton.onClick.AddListener(StartNewGame);

        GameObject savesTitleGo = CreateUiObject("SavedGamesLabel", playSubmenuRoot);
        Text savesTitle = savesTitleGo.AddComponent<Text>();
        savesTitle.text = "Saved Games";
        savesTitle.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        savesTitle.fontSize = 28;
        savesTitle.fontStyle = FontStyle.Bold;
        savesTitle.alignment = TextAnchor.MiddleLeft;
        savesTitle.color = new Color(1f, 1f, 1f, 0.95f);
        RectTransform savesTitleRect = savesTitleGo.GetComponent<RectTransform>();
        savesTitleRect.sizeDelta = new Vector2(420f, 36f);

        GameObject saveListGo = CreateUiObject("SavedGamesList", playSubmenuRoot);
        saveListRoot = saveListGo.GetComponent<RectTransform>();
        VerticalLayoutGroup saveLayout = saveListGo.AddComponent<VerticalLayoutGroup>();
        saveLayout.spacing = 6f;
        saveLayout.childAlignment = TextAnchor.UpperLeft;
        saveLayout.childControlWidth = true;
        saveLayout.childControlHeight = false;
        saveLayout.childForceExpandHeight = false;
        saveLayout.childForceExpandWidth = false;
        ContentSizeFitter saveFitter = saveListGo.AddComponent<ContentSizeFitter>();
        saveFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        saveFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        saveListRoot.sizeDelta = new Vector2(420f, 200f);

        Button settingsButton = CreateMenuButton(menuContainer, "Settings", 420f, 72f);
        settingsButton.onClick.AddListener(() => ShowNotice("Settings coming soon."));

        Button creditsButton = CreateMenuButton(menuContainer, "Credits", 420f, 72f);
        creditsButton.onClick.AddListener(() => ShowNotice("Credits coming soon."));

        Button exitButton = CreateMenuButton(menuContainer, "Exit", 420f, 72f);
        exitButton.onClick.AddListener(ExitGame);

        GameObject noticeGo = CreateUiObject("MenuNotice", canvasRect);
        noticeText = noticeGo.AddComponent<Text>();
        noticeText.text = string.Empty;
        noticeText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        noticeText.alignment = TextAnchor.MiddleCenter;
        noticeText.fontSize = 30;
        noticeText.color = new Color(1f, 1f, 1f, 0.90f);
        AddOutline(noticeGo, new Color(0f, 0f, 0f, 0.8f), new Vector2(2f, -2f));
        RectTransform noticeRect = noticeGo.GetComponent<RectTransform>();
        noticeRect.anchorMin = new Vector2(0.5f, 0f);
        noticeRect.anchorMax = new Vector2(0.5f, 0f);
        noticeRect.pivot = new Vector2(0.5f, 0f);
        noticeRect.sizeDelta = new Vector2(1400f, 64f);
        noticeRect.anchoredPosition = new Vector2(0f, 42f);
    }

    private void TogglePlaySubmenu()
    {
        if (playSubmenuRoot == null) return;
        bool nextActive = !playSubmenuRoot.gameObject.activeSelf;
        playSubmenuRoot.gameObject.SetActive(nextActive);
        if (nextActive)
        {
            RefreshSavedGamesList();
            ShowNotice(string.Empty);
        }
    }

    private void RefreshSavedGamesList()
    {
        if (saveListRoot == null) return;

        for (int i = 0; i < saveEntryLabels.Count; i++)
        {
            if (saveEntryLabels[i] != null)
            {
                Destroy(saveEntryLabels[i].gameObject);
            }
        }
        saveEntryLabels.Clear();

        string saveRoot = Path.Combine(Application.persistentDataPath, SaveFolderName);
        List<string> saveFiles = new List<string>(32);
        if (Directory.Exists(saveRoot))
        {
            AppendFiles(saveFiles, saveRoot, "*.save");
            AppendFiles(saveFiles, saveRoot, "*.sav");
            AppendFiles(saveFiles, saveRoot, "*.json");
            saveFiles.Sort(StringComparer.OrdinalIgnoreCase);
        }

        if (saveFiles.Count == 0)
        {
            CreateSaveLabel("(No saved games yet)");
            return;
        }

        for (int i = 0; i < saveFiles.Count; i++)
        {
            string file = saveFiles[i];
            string name = Path.GetFileNameWithoutExtension(file);
            DateTime modified = File.GetLastWriteTime(file);
            CreateSaveLabel(name + "  -  " + modified.ToString("yyyy-MM-dd HH:mm"));
        }
    }

    private static void AppendFiles(List<string> target, string root, string pattern)
    {
        string[] matches = Directory.GetFiles(root, pattern, SearchOption.TopDirectoryOnly);
        for (int i = 0; i < matches.Length; i++)
        {
            target.Add(matches[i]);
        }
    }

    private void CreateSaveLabel(string text)
    {
        if (saveListRoot == null) return;
        GameObject labelGo = CreateUiObject("SaveEntry", saveListRoot);
        Text label = labelGo.AddComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.fontSize = 22;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = new Color(1f, 1f, 1f, 0.92f);
        RectTransform rt = labelGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(420f, 34f);
        saveEntryLabels.Add(label);
    }

    private void StartNewGame()
    {
        sessionStarted = true;
        ResumeGameplay();
        if (menuCanvas != null)
        {
            Destroy(menuCanvas.gameObject);
        }

        for (int i = 0; i < runtimeCreatedObjects.Count; i++)
        {
            if (runtimeCreatedObjects[i] != null)
            {
                Destroy(runtimeCreatedObjects[i]);
            }
        }
        runtimeCreatedObjects.Clear();

        Destroy(gameObject);
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

    private Button CreateMenuButton(Transform parent, string label, float width, float height)
    {
        GameObject buttonGo = CreateUiObject(label + "Button", parent);
        Image buttonImage = buttonGo.AddComponent<Image>();
        buttonImage.sprite = menuButtonNormal;
        buttonImage.type = menuButtonNormal != null && menuButtonNormal.border.sqrMagnitude > 0f
            ? Image.Type.Sliced
            : Image.Type.Simple;
        buttonImage.color = Color.white;

        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = buttonImage;

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
        colors.fadeDuration = 0.07f;
        button.colors = colors;

        RectTransform rt = buttonGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);

        LayoutElement le = buttonGo.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;
        le.minHeight = height;
        le.minWidth = width;

        GameObject textGo = CreateUiObject("Label", rt);
        Text t = textGo.AddComponent<Text>();
        t.text = label;
        t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = 42;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        AddOutline(textGo, new Color(0f, 0f, 0f, 0.9f), new Vector2(2f, -2f));
        RectTransform tRt = textGo.GetComponent<RectTransform>();
        StretchRect(tRt);

        menuButtons.Add(button);
        return button;
    }

    private void EnsureButtonSpritesLoaded()
    {
#if UNITY_EDITOR
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

    private Sprite TryLoadMainMenuArt()
    {
#if UNITY_EDITOR
        Sprite s = TryLoadSpriteAtPath(PrimaryMenuArtPath);
        if (s != null) return s;
        s = TryLoadSpriteAtPath(FallbackMenuArtPath);
        if (s != null) return s;
#endif
        Sprite fromResources = Resources.Load<Sprite>("UI/MainMenuArt");
        if (fromResources != null) return fromResources;
        return null;
    }

    private static Sprite TryLoadSpriteAtPath(string path)
    {
#if UNITY_EDITOR
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null)
        {
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
#endif
        return null;
    }

    private static void StretchRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.GetComponent<Outline>();
        if (outline == null) outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    private void EnsureEventSystemExists()
    {
        if (EventSystem.current != null) return;
        GameObject es = new GameObject("MainMenuEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        runtimeCreatedObjects.Add(es);
    }
}
