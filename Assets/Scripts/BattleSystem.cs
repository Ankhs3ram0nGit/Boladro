using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class BattleSystem : MonoBehaviour
{
    public static bool IsEngagedBattleActive { get; private set; }

    public float engageRadius = 5f;
    [Tooltip("Hard cap for nearest-creature search if no target is found in normal engage range.")]
    public float maxEngageSearchRadius = 30f;
    public GameObject battleRoot;
    public Text messageText;

    public Text playerNameText;
    public Text playerLevelText;
    public Text playerTypesText;
    public Text playerHpText;
    public Image playerHpBg;
    public Image playerHpFill;
    public Image playerXpBg;
    public Image playerXpFill;

    public Text enemyNameText;
    public Text enemyLevelText;
    public Text enemyTypesText;
    public Text enemyHpText;
    public Image enemyHpBg;
    public Image enemyHpFill;

    public Button attackButton;
    public Button swapButton;
    public Button captureButton;
    public Button runButton;

    public GameObject movePanel;
    public Button[] moveButtons = new Button[4];
    public Text[] moveTexts = new Text[4];

    public Texture2D barBgTexture;
    public Texture2D barFillTexture;
    public Texture2D bottomFrameTexture;
    public Sprite battleBackgroundSprite;
    public Texture2D battleBackgroundTexture;
    public Texture2D buttonNormalTexture;
    public Texture2D buttonPressedTexture;
    public Texture2D buttonHighlightTexture;
    public float battleUiZoom = 1f;
    public float actionNarrationDelay = 1.25f;
    public float opponentTurnDelay = 1.0f;
    public float actionPhaseDelay = 0.45f;
    [Tooltip("Failsafe radius used when standard engage search finds no valid target.")]
    public float fallbackEngageRadius = 10f;
    [Tooltip("Show live debug panel for E-to-engage diagnostics.")]
    public bool showEngageDebug = false;
    public Vector2 engageDebugPanelPosition = new Vector2(16f, 160f);
    public Vector2 engageDebugPanelSize = new Vector2(560f, 190f);
    [Header("Swap Menu")]
    [Range(0f, 1f)] public float swapOverlayOpacity = 0.72f;
    [Range(0.2f, 1f)] public float swapActiveCardOpacity = 0.5f;
    public Sprite swapCardBackgroundSprite;
    public Sprite swapCardGlassSprite;
    public Sprite swapExitIconSprite;
    public Sprite levelUpArrowSprite;

    [Header("XP Rewards")]
    [Min(1)] public int battleWinBaseXp = 40;
    [Min(0f)] public float battleWinPerEnemyLevel = 14f;
    [Min(0.1f)] public float rareBattleXpMultiplier = 1.15f;
    [Min(0.1f)] public float eliteBattleXpMultiplier = 1.30f;
    [Min(0.1f)] public float legendaryBattleXpMultiplier = 1.55f;

    [Header("Damage Scaling")]
    [Tooltip("Move damage is converted to attack power with: moveDamage * (attack / this value).")]
    [Min(0.1f)] public float damageAttackStatDivisor = 10f;
    [Tooltip("High-attack branch multiplier used when attack power >= defense.")]
    [Min(0.1f)] public float damageHighAttackBranchMultiplier = 2f;
    [Tooltip("Level ratio exponent used in level-gap scaling. 2 = quadratic.")]
    [Min(0.1f)] public float damageLevelRatioExponent = 2f;
    [Tooltip("Minimum level-gap multiplier clamp.")]
    [Min(0.01f)] public float damageLevelMultiplierMin = 0.10f;
    [Tooltip("Maximum level-gap multiplier clamp.")]
    [Min(0.1f)] public float damageLevelMultiplierMax = 4.0f;
    [Tooltip("Final minimum damage after all scaling.")]
    [Min(1)] public int minimumDamagePerHit = 1;

    [Header("Battle Audio")]
    public AudioClip encounterStartSfx;
    public AudioClip moveDamageSfx;
    public AudioClip debuffSfx;

    [Header("Encounter Transition")]
    [Min(0f)] public float encounterBlackoutFadeIn = 0.18f;
    [Min(0f)] public float encounterBlackoutHold = 0.05f;
    [Min(0f)] public float encounterBlackoutFadeOut = 0.20f;

    private PlayerMover playerMover;
    private PlayerHealth playerHealth;
    private WildCreatureAI currentEnemyAI;
    private CreatureCombatant playerCreature;
    private CreatureCombatant enemyCreature;
    private GameObject hudRoot;
    private GameObject actionMenu;
    private RectTransform arenaPanel;
    private RectTransform uiPanel;
    private Image playerSpriteImage;
    private Image enemySpriteImage;
    private Image playerShadowImage;
    private Image enemyShadowImage;
    private Image battleBackgroundImage;
    private Button backButton;
    private Image bottomFrameImage;
    private Font cachedDefaultFont;
    private readonly Dictionary<string, Sprite> creatureSpriteCache = new Dictionary<string, Sprite>();
    private PlayerCreatureParty playerParty;
    private PlayerCreatureStorage playerStorage;
    private GameObject swapMenuRoot;
    private RectTransform swapMenuCardsRoot;
    private Button swapMenuExitButton;
    private bool swapMenuOpen;
    private bool swapMenuExitAnimating;
    private bool swapSelectionForced;
    private bool swapSelectionConsumesTurn = true;
    private readonly List<SwapCardView> swapCardViews = new List<SwapCardView>();
    private readonly Dictionary<string, Sprite> swapHeadSpriteCache = new Dictionary<string, Sprite>();
    private readonly List<Sprite> generatedSwapHeadSprites = new List<Sprite>();

    private bool inBattle;
    private bool waitingForPlayerMove;
    private bool hudWasHiddenForBattle;
    private string engageDebugMessage = "Engage debug ready.";
    private int engageDebugPressCount;
    private int engageDebugNearbyCount;
    private int engageDebugAliveCount;
    private int engageDebugInBattleCount;
    private float engageDebugNearestDistance = -1f;
    private bool runtimeLayoutInitialized;
    private Vector2Int lastLayoutScreenSize;
    private bool turnResolutionInProgress;
    private readonly HashSet<Image> activeAttackAnimations = new HashSet<Image>();
    private readonly Dictionary<CreatureCombatant, int> guaranteedDodgeCharges = new Dictionary<CreatureCombatant, int>();
    private Sprite shadowEllipseSprite;
    private Sprite neutralFillSprite;
    private AudioSource battleSfxSource;
    private Canvas battleBlackoutCanvas;
    private Image battleBlackoutImage;
    private bool battleStartTransitionInProgress;
    private Vector3 playerSpriteBaseLocalPos;
    private Vector3 enemySpriteBaseLocalPos;
    private Vector3 playerSpriteBaseLocalScale = Vector3.one;
    private Vector3 enemySpriteBaseLocalScale = Vector3.one;
    private bool playerFaintedVisualLocked;
    private bool enemyFaintedVisualLocked;
    private readonly HashSet<CreatureInstance> battleParticipants = new HashSet<CreatureInstance>();
    private static readonly Color HpGreen = new Color(0.20f, 0.82f, 0.24f, 1f);
    private static readonly Color HpYellow = new Color(0.97f, 0.88f, 0.20f, 1f);
    private static readonly Color HpOrange = new Color(1.00f, 0.62f, 0.16f, 1f);
    private static readonly Color HpRed = new Color(0.90f, 0.18f, 0.18f, 1f);
    private static readonly Color TypeNormal = new Color32(0x88, 0x88, 0x88, 0xFF);
    private static readonly Color TypeFire = new Color32(0xC8, 0x4B, 0x31, 0xFF);
    private static readonly Color TypeWater = new Color32(0x1B, 0x6C, 0xA8, 0xFF);
    private static readonly Color TypeLightning = new Color32(0xD4, 0xA0, 0x17, 0xFF);
    private static readonly Color TypeEarth = new Color32(0x7A, 0x5C, 0x3A, 0xFF);
    private static readonly Color TypeNature = new Color32(0x2E, 0x7D, 0x32, 0xFF);
    private static readonly Color TypeIce = new Color32(0x5C, 0x9E, 0xBF, 0xFF);
    private static readonly Color TypeDragon = new Color32(0x6A, 0x0D, 0xAD, 0xFF);
    private static readonly Color TypeLight = new Color32(0xFF, 0xF8, 0xE7, 0xFF);
    private static readonly Color TypeDark = new Color32(0x3A, 0x3A, 0x5C, 0xFF);

    private sealed class SwapCardView
    {
        public int slotIndex;
        public Button button;
        public CanvasGroup canvasGroup;
        public RectTransform root;
        public LayoutElement layout;
        public Image background;
        public RectTransform iconRect;
        public Image icon;
        public Image glass;
        public RectTransform levelUpArrowRect;
        public Image levelUpArrow;
        public Text nameText;
        public Text levelText;
        public Text hpText;
        public Image hpFill;
        public Image xpFill;
    }

    void OnEnable()
    {
        // Recover from stale play-mode state when domain reload is disabled.
        inBattle = false;
        battleStartTransitionInProgress = false;
        waitingForPlayerMove = false;
        turnResolutionInProgress = false;
        playerFaintedVisualLocked = false;
        enemyFaintedVisualLocked = false;
        battleParticipants.Clear();
        IsEngagedBattleActive = false;
        SetBlackoutAlpha(0f);
    }

    void OnDisable()
    {
        IsEngagedBattleActive = false;
        battleStartTransitionInProgress = false;
        swapMenuOpen = false;
        battleParticipants.Clear();
        if (swapMenuRoot != null) swapMenuRoot.SetActive(false);
        SetBlackoutAlpha(0f);
    }

    void OnDestroy()
    {
        for (int i = 0; i < generatedSwapHeadSprites.Count; i++)
        {
            if (generatedSwapHeadSprites[i] != null)
            {
                Destroy(generatedSwapHeadSprites[i]);
            }
        }
        generatedSwapHeadSprites.Clear();
        swapHeadSpriteCache.Clear();
    }

    void Start()
    {
        if (engageRadius < 0.25f) engageRadius = 5f;
        inBattle = false;
        waitingForPlayerMove = false;
        turnResolutionInProgress = false;
        playerFaintedVisualLocked = false;
        enemyFaintedVisualLocked = false;
        runtimeLayoutInitialized = false;
        lastLayoutScreenSize = Vector2Int.zero;
        hudWasHiddenForBattle = false;
        IsEngagedBattleActive = false;
        showEngageDebug = false;

        playerMover = GetComponent<PlayerMover>();
        playerHealth = GetComponent<PlayerHealth>();
        EnsurePlayerPartySource();
        EnsureActivePartySlotIsUsable();

        AutoFindUI();

        if (battleRoot != null) battleRoot.SetActive(false);
        if (movePanel != null) movePanel.SetActive(false);

        ApplyEncounterLayout();
        HookButtons();
        ApplyBarSprites();
        EnsureButtonLabels();
        ApplyButtonSkins();
        EnsureSwapMenuSprites();
        EnsureBattleAudioAssets();
        EnsureBattleSfxSource();
        EnsureBattleBlackoutOverlay();
        SetBlackoutAlpha(0f);
    }

    void AutoFindUI()
    {
        if (battleRoot == null)
        {
            GameObject root = GameObject.Find("BattleUI");
            if (root != null) battleRoot = root;
        }

        Transform rootTf = battleRoot != null ? battleRoot.transform : null;
        if (rootTf == null) return;

        arenaPanel = FindRect(rootTf, "ArenaPanel");
        uiPanel = FindRect(rootTf, "UIPanel");
        if (hudRoot == null)
        {
            Transform hud = rootTf.parent != null ? rootTf.parent.Find("HUD") : null;
            if (hud != null) hudRoot = hud.gameObject;
        }

        if (messageText == null)
        {
            Transform t = rootTf.Find("MessageText");
            if (t != null) messageText = t.GetComponent<Text>();
        }

        playerNameText = playerNameText ?? FindText(rootTf, "UIPanel/PlayerBar/PlayerName");
        playerTypesText = playerTypesText ?? FindText(rootTf, "UIPanel/PlayerBar/PlayerTypes");
        playerLevelText = playerLevelText ?? FindText(rootTf, "UIPanel/PlayerBar/PlayerLevel");
        playerHpText = playerHpText ?? FindText(rootTf, "UIPanel/PlayerBar/PlayerHpText");
        playerHpBg = playerHpBg ?? FindImage(rootTf, "UIPanel/PlayerBar/PlayerHpBG");
        playerHpFill = playerHpFill ?? FindImage(rootTf, "UIPanel/PlayerBar/PlayerHpBG/PlayerHpFill");
        playerXpBg = playerXpBg ?? FindImage(rootTf, "UIPanel/PlayerBar/PlayerXpBG");
        playerXpFill = playerXpFill ?? FindImage(rootTf, "UIPanel/PlayerBar/PlayerXpBG/PlayerXpFill");
        HidePlayerXpCardBar();

        enemyNameText = enemyNameText ?? FindText(rootTf, "UIPanel/EnemyBar/EnemyName");
        enemyTypesText = enemyTypesText ?? FindText(rootTf, "UIPanel/EnemyBar/EnemyTypes");
        enemyLevelText = enemyLevelText ?? FindText(rootTf, "UIPanel/EnemyBar/EnemyLevel");
        enemyHpText = enemyHpText ?? FindText(rootTf, "UIPanel/EnemyBar/EnemyHpText");
        enemyHpText = enemyHpText ?? FindText(rootTf, "UIPanel/EnemyBar/EnemyHpBG/EnemyHpText");
        enemyHpBg = enemyHpBg ?? FindImage(rootTf, "UIPanel/EnemyBar/EnemyHpBG");
        enemyHpFill = enemyHpFill ?? FindImage(rootTf, "UIPanel/EnemyBar/EnemyHpBG/EnemyHpFill");
        playerSpriteImage = playerSpriteImage ?? FindImage(rootTf, "ArenaPanel/PlayerCreatureImage");
        enemySpriteImage = enemySpriteImage ?? FindImage(rootTf, "ArenaPanel/EnemyCreatureImage");

        attackButton = attackButton ?? FindButton(rootTf, "UIPanel/ActionMenu/AttackButton");
        swapButton = swapButton ?? FindButton(rootTf, "UIPanel/ActionMenu/SwapButton");
        captureButton = captureButton ?? FindButton(rootTf, "UIPanel/ActionMenu/CaptureButton");
        runButton = runButton ?? FindButton(rootTf, "UIPanel/ActionMenu/RunButton");

        if (movePanel == null)
        {
            Transform t = rootTf.Find("UIPanel/MovePanel");
            if (t != null) movePanel = t.gameObject;
        }
        if (actionMenu == null)
        {
            Transform t = rootTf.Find("UIPanel/ActionMenu");
            if (t != null) actionMenu = t.gameObject;
        }

        if (moveButtons == null || moveButtons.Length != 4) moveButtons = new Button[4];
        if (moveTexts == null || moveTexts.Length != 4) moveTexts = new Text[4];

        moveButtons[0] = moveButtons[0] ?? FindButton(rootTf, "UIPanel/MovePanel/MoveButton1");
        moveButtons[1] = moveButtons[1] ?? FindButton(rootTf, "UIPanel/MovePanel/MoveButton2");
        moveButtons[2] = moveButtons[2] ?? FindButton(rootTf, "UIPanel/MovePanel/MoveButton3");
        moveButtons[3] = moveButtons[3] ?? FindButton(rootTf, "UIPanel/MovePanel/MoveButton4");

        moveTexts[0] = moveTexts[0] ?? FindText(rootTf, "UIPanel/MovePanel/MoveButton1/Text");
        moveTexts[1] = moveTexts[1] ?? FindText(rootTf, "UIPanel/MovePanel/MoveButton2/Text");
        moveTexts[2] = moveTexts[2] ?? FindText(rootTf, "UIPanel/MovePanel/MoveButton3/Text");
        moveTexts[3] = moveTexts[3] ?? FindText(rootTf, "UIPanel/MovePanel/MoveButton4/Text");
    }

    Text FindText(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<Text>() : null;
    }

    Image FindImage(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<Image>() : null;
    }

    Button FindButton(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<Button>() : null;
    }

    RectTransform FindRect(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<RectTransform>() : null;
    }

    void EnsurePlayerPartySource()
    {
        if (playerParty != null) return;
        if (playerMover == null)
        {
            playerMover = GetComponent<PlayerMover>();
        }
        if (playerMover == null) return;

        playerParty = playerMover.GetComponent<PlayerCreatureParty>();
        if (playerParty == null)
        {
            playerParty = playerMover.gameObject.AddComponent<PlayerCreatureParty>();
        }
        if (playerParty != null && (playerParty.ActiveCreatures == null || playerParty.ActiveCreatures.Count == 0))
        {
            playerParty.InitializeParty();
        }
    }

    void EnsureCreatureStorageSource()
    {
        EnsurePlayerPartySource();
        if (playerStorage != null) return;
        if (playerMover == null)
        {
            playerMover = GetComponent<PlayerMover>();
        }
        if (playerMover == null) return;

        playerStorage = playerMover.GetComponent<PlayerCreatureStorage>();
        if (playerStorage == null)
        {
            playerStorage = playerMover.gameObject.AddComponent<PlayerCreatureStorage>();
        }
        if (playerStorage != null)
        {
            playerStorage.EnsureInitialized(playerParty);
        }
    }

    bool EnsureActivePartySlotIsUsable()
    {
        EnsurePlayerPartySource();
        if (playerParty == null || playerParty.ActiveCreatures == null || playerParty.ActiveCreatures.Count == 0)
        {
            return false;
        }

        int activeIndex = Mathf.Clamp(playerParty.ActivePartyIndex, 0, playerParty.ActiveCreatures.Count - 1);
        CreatureInstance active = playerParty.ActiveCreatures[activeIndex];
        if (active != null && active.currentHP > 0)
        {
            return true;
        }

        int firstAlive = playerParty.FindFirstAlivePartyIndex();
        if (firstAlive < 0) return false;

        if (firstAlive != activeIndex)
        {
            playerParty.SetActivePartyIndex(firstAlive);
        }
        return true;
    }

    CreatureInstance GetActivePartyInstance()
    {
        EnsurePlayerPartySource();
        if (playerParty == null || playerParty.ActiveCreatures == null || playerParty.ActiveCreatures.Count == 0)
        {
            return null;
        }

        int idx = Mathf.Clamp(playerParty.ActivePartyIndex, 0, playerParty.ActiveCreatures.Count - 1);
        return playerParty.ActiveCreatures[idx];
    }

    void EnsureSwapMenuSprites()
    {
#if UNITY_EDITOR
        if (swapCardBackgroundSprite == null)
        {
            swapCardBackgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_FrameMarker01a.png");
        }
        if (swapCardGlassSprite == null)
        {
            swapCardGlassSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_FrameSlot01c.png");
        }
        if (swapExitIconSprite == null)
        {
            swapExitIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_IconCross01a.png");
        }
        if (levelUpArrowSprite == null)
        {
            levelUpArrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_IconArrow01a.png");
        }
#endif
    }

    void EnsureBattleAudioAssets()
    {
#if UNITY_EDITOR
        if (encounterStartSfx == null)
        {
            encounterStartSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/400 Sounds Pack/Musical Effects/8_bit_negative.wav");
        }
        if (moveDamageSfx == null)
        {
            moveDamageSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/400 Sounds Pack/Retro/hurt.wav");
        }
        if (debuffSfx == null)
        {
            debuffSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/400 Sounds Pack/Retro/undesired_effect.wav");
        }
#endif
    }

    void EnsureBattleSfxSource()
    {
        if (battleSfxSource != null) return;
        battleSfxSource = gameObject.AddComponent<AudioSource>();
        battleSfxSource.playOnAwake = false;
        battleSfxSource.loop = false;
        battleSfxSource.spatialBlend = 0f;
        battleSfxSource.volume = 1f;
    }

    void PlayBattleClip(AudioClip clip)
    {
        if (clip == null) return;
        EnsureBattleSfxSource();
        if (battleSfxSource == null) return;
        battleSfxSource.PlayOneShot(clip);
    }

    void EnsureBattleBlackoutOverlay()
    {
        if (battleBlackoutImage != null && battleBlackoutCanvas != null) return;

        const string blackoutCanvasName = "BattleBlackoutOverlayCanvas";
        Transform existing = transform.Find(blackoutCanvasName);
        if (existing == null)
        {
            GameObject canvasGo = new GameObject(blackoutCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            existing = canvasGo.transform;
        }

        battleBlackoutCanvas = existing.GetComponent<Canvas>();
        if (battleBlackoutCanvas == null) battleBlackoutCanvas = existing.gameObject.AddComponent<Canvas>();
        battleBlackoutCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        battleBlackoutCanvas.overrideSorting = true;
        battleBlackoutCanvas.sortingOrder = short.MaxValue - 4;

        CanvasScaler scaler = existing.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = existing.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = existing.GetComponent<GraphicRaycaster>();
        if (raycaster == null) raycaster = existing.gameObject.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        Transform imageTf = existing.Find("BlackoutImage");
        if (imageTf == null)
        {
            GameObject imageGo = new GameObject("BlackoutImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageGo.transform.SetParent(existing, false);
            imageTf = imageGo.transform;
        }

        battleBlackoutImage = imageTf.GetComponent<Image>();
        if (battleBlackoutImage == null) battleBlackoutImage = imageTf.gameObject.AddComponent<Image>();
        battleBlackoutImage.raycastTarget = false;
        battleBlackoutImage.sprite = null;
        battleBlackoutImage.type = Image.Type.Simple;
        battleBlackoutImage.color = new Color(0f, 0f, 0f, 0f);

        RectTransform imageRt = battleBlackoutImage.rectTransform;
        imageRt.anchorMin = Vector2.zero;
        imageRt.anchorMax = Vector2.one;
        imageRt.offsetMin = Vector2.zero;
        imageRt.offsetMax = Vector2.zero;
    }

    void SetBlackoutAlpha(float alpha)
    {
        EnsureBattleBlackoutOverlay();
        if (battleBlackoutImage == null) return;
        Color c = battleBlackoutImage.color;
        c.a = Mathf.Clamp01(alpha);
        battleBlackoutImage.color = c;
    }

    IEnumerator FadeBlackout(float from, float to, float duration)
    {
        EnsureBattleBlackoutOverlay();
        if (battleBlackoutImage == null)
        {
            yield break;
        }

        float safeDuration = Mathf.Max(0f, duration);
        if (safeDuration <= 0.0001f)
        {
            SetBlackoutAlpha(to);
            yield break;
        }

        SetBlackoutAlpha(from);
        float elapsed = 0f;
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(elapsed / safeDuration);
            SetBlackoutAlpha(Mathf.Lerp(from, to, p));
            yield return null;
        }

        SetBlackoutAlpha(to);
    }

    void Update()
    {
        RecoverFromStaleBattleState();

        if (!inBattle)
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame)
            {
                TryStartBattle();
            }
            return;
        }

        EnsureBattleRuntimeState();
    }

    public bool TryStartBattleFromInput()
    {
        engageDebugPressCount++;
        SetEngageDebug("E pressed. Trying to engage.");
        if (battleStartTransitionInProgress)
        {
            SetEngageDebug("Blocked: encounter transition is already running.");
            return false;
        }
        RecoverFromStaleBattleState(force: true);
        if (inBattle)
        {
            SetEngageDebug("Blocked: already marked as in battle.");
            return false;
        }
        return TryStartBattleInternal();
    }

    void HookButtons()
    {
        if (attackButton != null)
        {
            attackButton.onClick.RemoveAllListeners();
            attackButton.onClick.AddListener(OpenMovePanel);
        }
        if (swapButton != null)
        {
            swapButton.onClick.RemoveAllListeners();
            swapButton.onClick.AddListener(OpenSwapMenu);
        }
        if (captureButton != null)
        {
            captureButton.onClick.RemoveAllListeners();
            captureButton.onClick.AddListener(TryCapture);
        }
        if (runButton != null)
        {
            runButton.onClick.RemoveAllListeners();
            runButton.onClick.AddListener(TryRun);
        }

        for (int i = 0; i < moveButtons.Length; i++)
        {
            int idx = i;
            if (moveButtons[i] != null)
            {
                moveButtons[i].onClick.RemoveAllListeners();
                moveButtons[i].onClick.AddListener(() => SelectMove(idx));
            }
        }
    }

    void EnsureButtonLabels()
    {
        ConfigureButtonLabel(attackButton, "ATTACK");
        ConfigureButtonLabel(swapButton, "SWAP");
        ConfigureButtonLabel(captureButton, "CAPTURE");
        ConfigureButtonLabel(runButton, "RUN");
    }

    void ConfigureButtonLabel(Button button, string text)
    {
        if (button == null) return;
        Text[] labels = button.GetComponentsInChildren<Text>(true);
        if (labels == null || labels.Length == 0)
        {
            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(button.transform, false);
            labels = new[] { textGo.GetComponent<Text>() };
        }

        for (int i = 0; i < labels.Length; i++)
        {
            Text label = labels[i];
            if (label == null) continue;
            if (label.font == null) label.font = GetDefaultUIFont();
            label.text = text;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.fontSize = 42;
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            Outline o = label.GetComponent<Outline>();
            if (o == null) o = label.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.95f);
            o.effectDistance = new Vector2(2f, -2f);

            RectTransform rt = label.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }
    }

    void ApplyButtonSkins()
    {
#if UNITY_EDITOR
        buttonNormalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_Button01a_1.png");
        buttonHighlightTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_Button01a_2.png");
        buttonPressedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_Button01a_3.png");
#endif
        if (buttonNormalTexture == null) buttonNormalTexture = Resources.Load<Texture2D>("UI/BattleButtonNormal");
        if (buttonPressedTexture == null) buttonPressedTexture = Resources.Load<Texture2D>("UI/BattleButtonPressed");
        if (buttonHighlightTexture == null) buttonHighlightTexture = Resources.Load<Texture2D>("UI/BattleButtonHighlight");

        Sprite normal = CreateSprite(buttonNormalTexture);
        Sprite pressed = CreateSprite(buttonPressedTexture);
        Sprite highlighted = CreateSprite(buttonHighlightTexture != null ? buttonHighlightTexture : buttonNormalTexture);

        ApplyButtonSkin(attackButton, normal, highlighted, pressed);
        ApplyButtonSkin(swapButton, normal, highlighted, pressed);
        ApplyButtonSkin(captureButton, normal, highlighted, pressed);
        ApplyButtonSkin(runButton, normal, highlighted, pressed);

        for (int i = 0; i < moveButtons.Length; i++)
        {
            ApplyButtonSkin(moveButtons[i], normal, highlighted, pressed);
        }
        ApplyButtonSkin(backButton, normal, highlighted, pressed);
    }

    Sprite CreateSprite(Texture2D tex)
    {
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    Sprite GetNeutralFillSprite()
    {
        if (neutralFillSprite != null) return neutralFillSprite;
        Texture2D tex = Texture2D.whiteTexture;
        neutralFillSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        neutralFillSprite.name = "BattleNeutralFill";
        return neutralFillSprite;
    }

    void ApplyButtonSkin(Button button, Sprite normal, Sprite highlighted, Sprite pressed)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        if (image == null) return;

        if (normal != null)
        {
            image.sprite = normal;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.95f);
        colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        button.colors = colors;

        SpriteState state = button.spriteState;
        if (highlighted != null) state.highlightedSprite = highlighted;
        if (pressed != null) state.pressedSprite = pressed;
        button.spriteState = state;
        button.transition = Selectable.Transition.SpriteSwap;
    }

    void ApplyBarSprites()
    {
        Sprite neutral = GetNeutralFillSprite();

        if (barBgTexture == null)
        {
            barBgTexture = Resources.Load<Texture2D>("UI/BattleBarBG");
        }
        if (barBgTexture != null)
        {
            Sprite bg = Sprite.Create(barBgTexture, new Rect(0, 0, barBgTexture.width, barBgTexture.height), new Vector2(0.5f, 0.5f), 100f);
            if (playerHpBg != null) playerHpBg.sprite = bg;
            if (enemyHpBg != null) enemyHpBg.sprite = bg;
        }

        if (playerHpBg != null)
        {
            if (playerHpBg.sprite == null && neutral != null) playerHpBg.sprite = neutral;
            playerHpBg.color = new Color(0f, 0f, 0f, 0.94f);
            playerHpBg.type = Image.Type.Simple;
            playerHpBg.preserveAspect = false;
        }
        if (enemyHpBg != null)
        {
            if (enemyHpBg.sprite == null && neutral != null) enemyHpBg.sprite = neutral;
            enemyHpBg.color = new Color(0f, 0f, 0f, 0.94f);
            enemyHpBg.type = Image.Type.Simple;
            enemyHpBg.preserveAspect = false;
        }

        Sprite fill = neutral;
        if (fill != null)
        {
            if (playerHpFill != null) playerHpFill.sprite = fill;
            if (enemyHpFill != null) enemyHpFill.sprite = fill;

            if (playerHpFill != null)
            {
                playerHpFill.type = Image.Type.Filled;
                playerHpFill.fillMethod = Image.FillMethod.Horizontal;
                playerHpFill.fillOrigin = 0;
                playerHpFill.preserveAspect = false;
            }
            if (enemyHpFill != null)
            {
                enemyHpFill.type = Image.Type.Filled;
                enemyHpFill.fillMethod = Image.FillMethod.Horizontal;
                enemyHpFill.fillOrigin = 0;
                enemyHpFill.preserveAspect = false;
            }
        }

        HidePlayerXpCardBar();
    }

    void TryStartBattle()
    {
        TryStartBattleInternal();
    }

    bool TryStartBattleInternal()
    {
        if (battleStartTransitionInProgress)
        {
            SetEngageDebug("Engage blocked: encounter transition already active.");
            return false;
        }

        if (!inBattle)
        {
            IsEngagedBattleActive = false;
        }

        EnsurePlayerPartySource();
        if (playerParty != null)
        {
            if (!playerParty.HasAnyAliveCreatures() || !EnsureActivePartySlotIsUsable())
            {
                SetEngageDebug("Engage blocked: all party creatures are fainted.");
                return false;
            }
        }

        const float strictEncounterRangeTiles = 5f;
        engageRadius = strictEncounterRangeTiles;
        RefreshEngageDebugSnapshot(strictEncounterRangeTiles);
        ClearStaleWildBattleFlags();

        currentEnemyAI = FindEngageTarget(strictEncounterRangeTiles);
        if (currentEnemyAI == null)
        {
            SetEngageDebug("Engage failed: no valid target within 5 tiles.");
            return false;
        }

        float dist = Vector2.Distance(transform.position, currentEnemyAI.transform.position);
        if (dist > strictEncounterRangeTiles + 0.001f)
        {
            SetEngageDebug("Engage blocked: target is farther than 5 tiles.");
            currentEnemyAI = null;
            return false;
        }
        SetEngageDebug("Engaging: " + currentEnemyAI.name + " at distance " + dist.ToString("0.00"));
        StartBattle(currentEnemyAI);
        return true;
    }

    void ClearStaleWildBattleFlags()
    {
        if (IsEngagedBattleActive) return;
        WildCreatureAI[] all = FindObjectsByType<WildCreatureAI>(FindObjectsSortMode.None);
        if (all == null || all.Length == 0) return;
        for (int i = 0; i < all.Length; i++)
        {
            WildCreatureAI ai = all[i];
            if (ai == null) continue;
            if (!ai.gameObject.activeInHierarchy) continue;
            if (ai.IsInBattle()) ai.ExitBattle();
        }
    }

    WildCreatureAI FindNearestEngageableWildAnyDistance()
    {
        Vector2 pos = transform.position;
        WildCreatureAI[] enemies = FindObjectsByType<WildCreatureAI>(FindObjectsSortMode.None);
        if (enemies == null || enemies.Length == 0) return null;

        WildCreatureAI closest = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < enemies.Length; i++)
        {
            WildCreatureAI e = enemies[i];
            if (e == null) continue;
            if (!e.gameObject.activeInHierarchy) continue;
            if (!e.IsAlive()) continue;
            if (e.IsInBattle()) continue;

            float dSqr = ((Vector2)e.transform.position - pos).sqrMagnitude;
            if (dSqr >= bestSqr) continue;
            bestSqr = dSqr;
            closest = e;
        }
        return closest;
    }

    WildCreatureAI FindEngageTarget(float radius)
    {
        float r = Mathf.Max(0.5f, radius);
        float rSqr = r * r;
        Vector2 pos = transform.position;
        WildCreatureAI[] enemies = FindObjectsByType<WildCreatureAI>(FindObjectsSortMode.None);
        if (enemies == null || enemies.Length == 0) return null;

        WildCreatureAI closest = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < enemies.Length; i++)
        {
            WildCreatureAI e = enemies[i];
            if (e == null) continue;
            if (!e.gameObject.activeInHierarchy) continue;
            if (!e.IsAlive()) continue;
            if (e.IsInBattle())
            {
                if (!IsEngagedBattleActive)
                {
                    e.ExitBattle();
                }
                else
                {
                    continue;
                }
            }

            float dSqr = ((Vector2)e.transform.position - pos).sqrMagnitude;
            if (dSqr > rSqr) continue;
            if (dSqr >= bestSqr) continue;

            bestSqr = dSqr;
            closest = e;
        }

        return closest;
    }

    void RecoverFromStaleBattleState(bool force = false)
    {
        if (!inBattle)
        {
            if (force) IsEngagedBattleActive = false;
            return;
        }

        bool enemyValid = currentEnemyAI != null && currentEnemyAI.gameObject != null && currentEnemyAI.gameObject.activeInHierarchy;
        // Keep battle alive as long as the engaged enemy is valid.
        // UI visibility can lag a frame (or be toggled by layout scripts) and should not auto-cancel battle.
        bool keepBattle = enemyValid && !force;
        if (keepBattle) return;

        // If battle state is set but battle UI is gone, recover so engagement input works again.
        inBattle = false;
        waitingForPlayerMove = false;
        hudWasHiddenForBattle = false;
        IsEngagedBattleActive = false;
        currentEnemyAI = null;
        enemyCreature = null;

        if (battleRoot != null)
        {
            battleRoot.SetActive(false);
        }
        if (movePanel != null)
        {
            movePanel.SetActive(false);
        }
        CloseSwapMenu(false);

        if (playerMover != null && (playerHealth == null || playerHealth.currentHealth > 0))
        {
            playerMover.enabled = true;
        }
        SetEngageDebug("Recovered stale battle state.");
    }

    WildCreatureAI FindOrCreateFallbackWildTarget()
    {
        Vector2 pos = transform.position;
        float best = float.MaxValue;
        CreatureHealth bestHealth = null;

        CreatureHealth[] all = FindObjectsByType<CreatureHealth>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            CreatureHealth ch = all[i];
            if (ch == null) continue;
            if (!ch.gameObject.activeInHierarchy) continue;
            if (ch.currentHealth <= 0) continue;
            if (ch.GetComponent<PlayerHealth>() != null) continue;
            if (ch.GetComponent<Follower>() != null) continue;

            float d = ((Vector2)ch.transform.position - pos).sqrMagnitude;
            if (d >= best) continue;
            best = d;
            bestHealth = ch;
        }

        if (bestHealth == null) return null;

        WildCreatureAI ai = bestHealth.GetComponent<WildCreatureAI>();
        if (ai == null) ai = bestHealth.gameObject.AddComponent<WildCreatureAI>();
        ai.ExitBattle();
        return ai;
    }

    void RefreshEngageDebugSnapshot(float radius)
    {
        engageDebugNearbyCount = 0;
        engageDebugAliveCount = 0;
        engageDebugInBattleCount = 0;
        engageDebugNearestDistance = -1f;

        WildCreatureAI[] enemies = FindObjectsByType<WildCreatureAI>(FindObjectsSortMode.None);
        if (enemies == null || enemies.Length == 0) return;

        float r = Mathf.Max(0.5f, radius);
        float rSqr = r * r;
        Vector2 pos = transform.position;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < enemies.Length; i++)
        {
            WildCreatureAI e = enemies[i];
            if (e == null) continue;
            if (!e.gameObject.activeInHierarchy) continue;

            engageDebugNearbyCount++;
            if (e.IsAlive()) engageDebugAliveCount++;
            if (e.IsInBattle()) engageDebugInBattleCount++;

            float dSqr = ((Vector2)e.transform.position - pos).sqrMagnitude;
            if (dSqr <= rSqr && dSqr < bestSqr)
            {
                bestSqr = dSqr;
            }
        }

        if (bestSqr < float.MaxValue)
        {
            engageDebugNearestDistance = Mathf.Sqrt(bestSqr);
        }
    }

    void SetEngageDebug(string msg)
    {
        engageDebugMessage = msg;
        Debug.Log("[Battle Engage Debug] " + msg);
    }

    void OnGUI()
    {
        // Debug panel intentionally disabled for polish builds.
        showEngageDebug = false;
        if (!showEngageDebug) return;

        Rect rect = new Rect(
            engageDebugPanelPosition.x,
            engageDebugPanelPosition.y,
            engageDebugPanelSize.x,
            engageDebugPanelSize.y
        );
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.Box(rect, GUIContent.none);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));
        GUILayout.Label("Battle Engage Debug");
        GUILayout.Label("Presses: " + engageDebugPressCount);
        GUILayout.Label("InBattle: " + inBattle + " | GlobalActive: " + IsEngagedBattleActive);
        if (battleRoot == null)
        {
            GUILayout.Label("BattleRoot: null");
        }
        else
        {
            string parentState = "none";
            Transform p = battleRoot.transform.parent;
            if (p != null)
            {
                parentState = p.name + " self:" + p.gameObject.activeSelf + " inHierarchy:" + p.gameObject.activeInHierarchy;
            }
            GUILayout.Label("BattleRoot: self:" + battleRoot.activeSelf + " inHierarchy:" + battleRoot.activeInHierarchy + " parent: " + parentState);
        }
        GUILayout.Label("CurrentEnemy: " + (currentEnemyAI == null ? "null" : currentEnemyAI.name));
        GUILayout.Label("WaitingForMove: " + waitingForPlayerMove + " | AttackInteractable: " + (attackButton != null && attackButton.interactable));
        GUILayout.Label("Wilds Active: " + engageDebugNearbyCount + " | Alive: " + engageDebugAliveCount + " | Flagged InBattle: " + engageDebugInBattleCount);
        GUILayout.Label("Nearest within max search: " + (engageDebugNearestDistance < 0f ? "none" : engageDebugNearestDistance.ToString("0.00")));
        GUILayout.Label("Message: " + engageDebugMessage);
        GUILayout.EndArea();
    }

    public void StartBattle(WildCreatureAI enemyAI)
    {
        if (inBattle || battleStartTransitionInProgress) return;
        StartCoroutine(BeginEncounterTransitionAndStartBattle(enemyAI));
    }

    IEnumerator BeginEncounterTransitionAndStartBattle(WildCreatureAI enemyAI)
    {
        battleStartTransitionInProgress = true;
        currentEnemyAI = enemyAI;
        EnsureBattleSfxSource();
        EnsureBattleBlackoutOverlay();
        PlayBattleClip(encounterStartSfx);

        yield return StartCoroutine(FadeBlackout(0f, 1f, encounterBlackoutFadeIn));
        float hold = Mathf.Max(0f, encounterBlackoutHold);
        if (hold > 0f)
        {
            yield return new WaitForSecondsRealtime(hold);
        }

        StartBattleImmediate(enemyAI);
        if (!inBattle)
        {
            yield return StartCoroutine(FadeBlackout(1f, 0f, encounterBlackoutFadeOut));
            SetBlackoutAlpha(0f);
            battleStartTransitionInProgress = false;
            yield break;
        }

        yield return StartCoroutine(FadeBlackout(1f, 0f, encounterBlackoutFadeOut));
        SetBlackoutAlpha(0f);
        battleStartTransitionInProgress = false;
    }

    void StartBattleImmediate(WildCreatureAI enemyAI)
    {
        if (inBattle) return;
        currentEnemyAI = enemyAI;

        inBattle = true;
        IsEngagedBattleActive = true;
        waitingForPlayerMove = true;
        battleParticipants.Clear();
        playerFaintedVisualLocked = false;
        enemyFaintedVisualLocked = false;
        hudWasHiddenForBattle = false;
        turnResolutionInProgress = false;
        runtimeLayoutInitialized = false;
        lastLayoutScreenSize = Vector2Int.zero;
        ApplyEncounterLayout();
        HookButtons();
        EnsureButtonLabels();

        if (playerMover != null) playerMover.enabled = false;
        if (enemyAI != null)
        {
            enemyAI.EnterBattle();
            enemyAI.ForceStop();
        }

        EnsurePlayerPartySource();
        if (playerParty != null && !EnsureActivePartySlotIsUsable())
        {
            inBattle = false;
            IsEngagedBattleActive = false;
            waitingForPlayerMove = false;
            turnResolutionInProgress = false;
            if (enemyAI != null)
            {
                enemyAI.ExitBattle();
            }
            if (playerMover != null) playerMover.enabled = true;
            SetMessage("All party creatures are fainted.");
            RefreshTurnInputState();
            return;
        }

        playerCreature = ResolvePlayerCombatant();
        if (playerCreature != null)
        {
            string playerId = ResolveCreatureID(playerCreature.gameObject, playerCreature);
            // Preserve party creature HP/PP across battle sessions.
            ConfigureCombatantByCreatureID(playerCreature, playerId, Mathf.Max(1, playerCreature.level), false);
            TryMarkBattleParticipant(playerCreature);
        }
        else
        {
            Debug.LogWarning("BattleSystem: Could not resolve a player creature combatant. Attack menu will be disabled.");
        }

        enemyCreature = ResolveEnemyCombatant(enemyAI);
        if (enemyCreature != null)
        {
            string enemyId = ResolveCreatureID(enemyAI != null ? enemyAI.gameObject : enemyCreature.gameObject, enemyCreature);
            int enemyLevel = Mathf.Max(1, enemyCreature.level);
            CreatureHealth enemyHealth = enemyAI != null ? enemyAI.GetComponent<CreatureHealth>() : null;
            if (enemyHealth != null && enemyHealth.level > 0) enemyLevel = enemyHealth.level;

            if (enemyAI != null)
            {
                WorldSpawnMarker marker = enemyAI.GetComponent<WorldSpawnMarker>();
                if (marker == null) marker = enemyAI.gameObject.AddComponent<WorldSpawnMarker>();
                if (string.IsNullOrWhiteSpace(marker.creatureID)) marker.creatureID = enemyId;
                marker.level = enemyLevel;
            }

            ConfigureCombatantByCreatureID(enemyCreature, enemyId, enemyLevel, true);
            if (enemyHealth != null && enemyHealth.maxHealth > 0)
            {
                float hpRatio = Mathf.Clamp01((float)enemyHealth.currentHealth / enemyHealth.maxHealth);
                enemyCreature.currentHP = Mathf.Clamp(Mathf.RoundToInt(enemyCreature.maxHP * hpRatio), 1, enemyCreature.maxHP);
            }
            enemyCreature.SyncInstanceRuntimeState();
        }

        if (battleRoot != null) battleRoot.SetActive(true);
        if (movePanel != null) movePanel.SetActive(false);
        CloseSwapMenu(false);
        SetActionMenuVisible(true);
        SetBackButtonVisible(false);
        if (hudRoot != null && battleRoot != null && !battleRoot.transform.IsChildOf(hudRoot.transform))
        {
            hudRoot.SetActive(false);
            hudWasHiddenForBattle = true;
        }

        UpdateCreatureSprites();
        UpdateUI();
        string enemyName = enemyCreature != null ? enemyCreature.creatureName : "Creature";
        SetMessage("A wild " + enemyName + " appears!");
        RefreshTurnInputState();
    }

    CreatureCombatant ResolvePlayerCombatant()
    {
        CreatureInstance activePartyInstance = GetActivePartyInstance();
        if (activePartyInstance != null)
        {
            if (ActivePartyFollowerController.Instance != null)
            {
                CreatureCombatant activeFollowerCombatant = ActivePartyFollowerController.Instance.CurrentFollowerCombatant;
                if (IsValidCombatant(activeFollowerCombatant))
                {
                    if (!ReferenceEquals(activeFollowerCombatant.Instance, activePartyInstance))
                    {
                        CreatureDefinition activeDef = CreatureRegistry.Get(activePartyInstance.definitionID);
                        if (activeDef != null)
                        {
                            activeFollowerCombatant.autoInitWhelpling = false;
                            activeFollowerCombatant.InitFromDefinition(activeDef, activePartyInstance);
                        }
                    }
                    playerCreature = activeFollowerCombatant;
                    return playerCreature;
                }
            }

            if (IsValidCombatant(playerCreature))
            {
                if (!ReferenceEquals(playerCreature.Instance, activePartyInstance))
                {
                    CreatureDefinition activeDef = CreatureRegistry.Get(activePartyInstance.definitionID);
                    if (activeDef != null)
                    {
                        playerCreature.autoInitWhelpling = false;
                        playerCreature.InitFromDefinition(activeDef, activePartyInstance);
                    }
                }
                return playerCreature;
            }
        }

        if (IsValidCombatant(playerCreature)) return playerCreature;

        GameObject frog = GameObject.Find("Frog");
        if (frog != null)
        {
            CreatureCombatant frogCombatant = frog.GetComponent<CreatureCombatant>();
            if (frogCombatant == null) frogCombatant = frog.AddComponent<CreatureCombatant>();
            if (frogCombatant != null) return frogCombatant;
        }

        Follower[] followers = FindObjectsByType<Follower>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < followers.Length; i++)
        {
            Follower follower = followers[i];
            if (follower == null || follower.gameObject == null) continue;
            if (!follower.gameObject.activeInHierarchy) continue;
            CreatureCombatant c = follower.GetComponent<CreatureCombatant>();
            if (c != null) return c;
        }

        if (playerMover != null)
        {
            CreatureCombatant self = playerMover.GetComponent<CreatureCombatant>();
            if (self == null) self = playerMover.gameObject.AddComponent<CreatureCombatant>();
            return self;
        }

        return null;
    }

    CreatureCombatant ResolveEnemyCombatant(WildCreatureAI enemyAI)
    {
        if (enemyAI != null)
        {
            CreatureCombatant c = enemyAI.GetComponent<CreatureCombatant>();
            if (c == null) c = enemyAI.gameObject.AddComponent<CreatureCombatant>();
            return c;
        }

        return IsValidCombatant(enemyCreature) ? enemyCreature : null;
    }

    static bool IsValidCombatant(CreatureCombatant combatant)
    {
        return combatant != null && combatant.gameObject != null;
    }

    public void StartEncounterFromSpawn(CreatureEncounterData data)
    {
        if (data == null || inBattle || battleStartTransitionInProgress) return;

        WildCreatureAI spawned = CreateEncounterEnemy(data);
        if (spawned == null)
        {
            if (SpawnManager.HasInstance)
            {
                // Encounter was consumed but failed to instantiate; release spawn slot.
                SpawnManager.Instance.NotifyBattleResolved();
            }
            Debug.LogWarning("Spawn encounter failed: no usable WildCreatureAI template found.");
            return;
        }

        currentEnemyAI = spawned;
        StartBattle(currentEnemyAI);
    }

    WildCreatureAI CreateEncounterEnemy(CreatureEncounterData data)
    {
        WildCreatureAI template = FindEncounterTemplate();
        if (template == null) return null;

        Vector3 spawnPos = transform.position + new Vector3(1.5f, 0f, 0f);
        WildCreatureAI enemy = Instantiate(template, spawnPos, Quaternion.identity);
        enemy.gameObject.SetActive(true);
        enemy.name = "EncounterEnemy_" + data.creatureID;
        if (enemy.GetComponent<EncounterSpawnMarker>() == null)
        {
            enemy.gameObject.AddComponent<EncounterSpawnMarker>();
        }

        CreatureCombatant cc = enemy.GetComponent<CreatureCombatant>();
        if (cc == null) cc = enemy.gameObject.AddComponent<CreatureCombatant>();
        ConfigureCombatantByCreatureID(cc, data.creatureID, Mathf.Max(1, data.resolvedLevel), true);

        CreatureHealth ch = enemy.GetComponent<CreatureHealth>();
        if (ch != null)
        {
            ch.level = Mathf.Max(1, data.resolvedLevel);
        }

        return enemy;
    }

    WildCreatureAI FindEncounterTemplate()
    {
        WildCreatureAI[] all = FindObjectsByType<WildCreatureAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        WildCreatureAI fallback = null;
        for (int i = 0; i < all.Length; i++)
        {
            WildCreatureAI ai = all[i];
            if (ai == null) continue;
            if (ai.GetComponent<EncounterSpawnMarker>() != null) continue; // skip spawned encounter clones
            if (fallback == null) fallback = ai;
            if (ai.gameObject.activeInHierarchy) return ai;
        }
        return fallback;
    }

    void EndBattle(bool playerWon)
    {
        inBattle = false;
        IsEngagedBattleActive = false;
        battleStartTransitionInProgress = false;
        waitingForPlayerMove = false;
        turnResolutionInProgress = false;
        playerFaintedVisualLocked = false;
        enemyFaintedVisualLocked = false;
        guaranteedDodgeCharges.Clear();
        battleParticipants.Clear();
        SetBlackoutAlpha(0f);

        if (battleRoot != null) battleRoot.SetActive(false);
        if (movePanel != null) movePanel.SetActive(false);
        CloseSwapMenu(false);
        SetActionMenuVisible(true);
        SetBackButtonVisible(false);
        if (hudRoot != null && hudWasHiddenForBattle) hudRoot.SetActive(true);
        hudWasHiddenForBattle = false;

        if (currentEnemyAI != null)
        {
            currentEnemyAI.ExitBattle();
            bool spawnedEncounter = currentEnemyAI.GetComponent<EncounterSpawnMarker>() != null;
            if (spawnedEncounter)
            {
                Destroy(currentEnemyAI.gameObject);
            }
            else if (playerWon)
            {
                currentEnemyAI.gameObject.SetActive(false);
            }
        }

        if (playerMover != null && (playerHealth == null || playerHealth.currentHealth > 0))
        {
            playerMover.enabled = true;
        }

        if (SpawnManager.HasInstance)
        {
            SpawnManager.Instance.NotifyBattleResolved();
        }

        RefreshTurnInputState();
    }

    void OpenMovePanel()
    {
        if (!inBattle || !waitingForPlayerMove) return;
        if (playerCreature == null)
        {
            playerCreature = ResolvePlayerCombatant();
            if (playerCreature == null)
            {
                SetMessage("No active player creature found.");
                RefreshTurnInputState();
                return;
            }
        }
        if (movePanel != null) movePanel.SetActive(true);
        SetActionMenuVisible(false);
        SetBackButtonVisible(true);
        RefreshTurnInputState();

        for (int i = 0; i < moveTexts.Length; i++)
        {
            int unlockLevel = 1 + i * 5;
            bool unlocked = playerCreature.level >= unlockLevel;
            Text liveText = moveTexts[i];
            if (liveText == null && moveButtons[i] != null)
            {
                Transform labelTf = moveButtons[i].transform.Find("Text");
                if (labelTf != null) liveText = labelTf.GetComponent<Text>();
            }

            if (liveText != null)
            {
                if (!unlocked)
                {
                    liveText.text = "Locked";
                }
                else
                {
                    bool hasAttack = playerCreature.attacks != null && i >= 0 && i < playerCreature.attacks.Count && playerCreature.attacks[i] != null;
                    if (hasAttack)
                    {
                        AttackData atk = playerCreature.attacks[i];
                        liveText.text = atk.name + "  " + atk.currentPP + "/" + atk.maxPP;
                    }
                    else
                    {
                        liveText.text = "---";
                        unlocked = false;
                    }
                }
                liveText.alignment = TextAnchor.MiddleCenter;
                liveText.color = new Color(0.08f, 0.08f, 0.10f, 1f);
            }
            if (moveButtons[i] != null)
            {
                moveButtons[i].interactable = waitingForPlayerMove && unlocked;
                UpdateButtonVisualState(moveButtons[i]);
            }
        }
    }

    void TryCapture()
    {
        if (turnResolutionInProgress || !inBattle || !waitingForPlayerMove) return;
        if (playerCreature == null || enemyCreature == null)
        {
            SetMessage("No target to capture.");
            return;
        }
        if (enemyCreature.currentHP <= 0)
        {
            SetMessage("Cannot capture a fainted creature.");
            return;
        }

        EnsurePlayerPartySource();
        EnsureCreatureStorageSource();
        if (playerParty == null)
        {
            SetMessage("Party system unavailable.");
            return;
        }
        if (playerStorage == null)
        {
            SetMessage("Creature storage unavailable.");
            return;
        }

        bool partyHasSpace = playerParty.HasSpaceInParty();
        bool storageHasSpace = playerStorage.HasSpace();
        if (!partyHasSpace && !storageHasSpace)
        {
            SetMessage("No space: party and creature storage are full.");
            return;
        }

        CreatureInstance captured = CreateCapturedInstanceFromEnemy();
        if (captured == null)
        {
            SetMessage("Capture failed.");
            return;
        }

        bool addedToParty;
        if (!playerStorage.TryAddCapturedCreature(captured, out addedToParty))
        {
            SetMessage("No space to capture this creature.");
            return;
        }

        waitingForPlayerMove = false;
        turnResolutionInProgress = true;
        RefreshTurnInputState();

        string targetName = captured.DisplayName;
        SetMessage(addedToParty
            ? targetName + " was captured and joined your party!"
            : targetName + " was captured and sent to creature storage!");

        StartCoroutine(CompleteCaptureAndEndBattle());
    }

    IEnumerator CompleteCaptureAndEndBattle()
    {
        yield return new WaitForSeconds(Mathf.Max(0.45f, actionNarrationDelay * 0.55f));
        turnResolutionInProgress = false;
        EndBattle(true);
    }

    CreatureInstance CreateCapturedInstanceFromEnemy()
    {
        if (enemyCreature == null) return null;

        CreatureInstance source = enemyCreature.Instance;
        CreatureInstance captured = source != null ? CloneCreatureInstance(source) : null;
        if (captured == null)
        {
            CreatureDefinition def = enemyCreature.Definition;
            if (def == null) return null;
            captured = CreatureInstanceFactory.CreateWild(def, Mathf.Max(1, enemyCreature.level));
            if (captured == null) return null;
        }

        captured.ownerID = "player";
        captured.ownershipState = OwnershipState.Captured;
        captured.level = Mathf.Clamp(captured.level <= 0 ? enemyCreature.level : captured.level, 1, CreatureExperienceSystem.MaxLevel);
        if (string.IsNullOrWhiteSpace(captured.creatureUID))
        {
            captured.creatureUID = System.Guid.NewGuid().ToString("N");
        }
        if (string.IsNullOrWhiteSpace(captured.definitionID))
        {
            captured.definitionID = ResolveCreatureID(
                currentEnemyAI != null ? currentEnemyAI.gameObject : enemyCreature.gameObject,
                enemyCreature);
        }

        CreatureDefinition resolved = CreatureRegistry.Get(captured.definitionID);
        if (resolved != null)
        {
            int maxHp = Mathf.Max(1, CreatureInstanceFactory.ComputeMaxHP(resolved, captured.soulTraits, captured.level));
            int hp = enemyCreature.currentHP;
            if (source != null) hp = source.currentHP;
            captured.currentHP = Mathf.Clamp(hp, 0, maxHp);
            if (captured.currentPP == null || captured.currentPP.Length < 4)
            {
                captured.currentPP = new int[4];
            }
        }

        WorldSpawnMarker marker = currentEnemyAI != null ? currentEnemyAI.GetComponent<WorldSpawnMarker>() : null;
        captured.capturedInZoneID = marker != null ? marker.zoneID : captured.capturedInZoneID;
        captured.captureTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return captured;
    }

    static CreatureInstance CloneCreatureInstance(CreatureInstance source)
    {
        if (source == null) return null;
        CreatureInstance clone = new CreatureInstance
        {
            creatureUID = source.creatureUID,
            definitionID = source.definitionID,
            ownerID = source.ownerID,
            ownershipState = source.ownershipState,
            nickname = source.nickname,
            level = source.level,
            currentHP = source.currentHP,
            currentPP = source.currentPP != null ? (int[])source.currentPP.Clone() : new int[4],
            soulTraits = source.soulTraits,
            totalExperience = source.totalExperience,
            totalBattles = source.totalBattles,
            isShiny = source.isShiny,
            capturedInZoneID = source.capturedInZoneID,
            captureTimestamp = source.captureTimestamp,
            familiarityTier = source.familiarityTier
        };
        return clone;
    }

    void OpenSwapMenu()
    {
        OpenSwapMenuInternal(false, true, "Choose a creature to swap.");
    }

    void OpenForcedSwapMenuAfterFaint()
    {
        OpenSwapMenuInternal(true, false, "Your creature fainted. Choose another creature.");
    }

    void OpenSwapMenuInternal(bool forcedSelection, bool consumeTurnOnSelection, string message)
    {
        if (swapMenuOpen)
        {
            swapSelectionForced = forcedSelection;
            swapSelectionConsumesTurn = consumeTurnOnSelection;
            UpdateSwapMenuExitState();
            RefreshSwapMenuCards();
            if (!string.IsNullOrWhiteSpace(message)) SetMessage(message);
            RefreshTurnInputState();
            return;
        }

        if (!inBattle || !waitingForPlayerMove || turnResolutionInProgress) return;
        EnsurePlayerPartySource();
        if (playerParty == null || playerParty.ActiveCreatures == null || playerParty.ActiveCreatures.Count == 0)
        {
            SetMessage("No party creatures available.");
            return;
        }

        if (movePanel != null) movePanel.SetActive(false);
        SetBackButtonVisible(false);
        SetActionMenuVisible(false);

        EnsureSwapMenu();
        swapSelectionForced = forcedSelection;
        swapSelectionConsumesTurn = consumeTurnOnSelection;
        UpdateSwapMenuExitState();
        RefreshSwapMenuCards();

        swapMenuOpen = true;
        swapMenuExitAnimating = false;
        if (swapMenuRoot != null)
        {
            swapMenuRoot.SetActive(true);
            swapMenuRoot.transform.SetAsLastSibling();
        }

        if (!string.IsNullOrWhiteSpace(message)) SetMessage(message);
        RefreshTurnInputState();
    }

    void EnsureSwapMenu()
    {
        if (battleRoot == null) return;
        EnsureSwapMenuSprites();

        Transform existing = battleRoot.transform.Find("SwapMenuOverlay");
        if (existing == null)
        {
            GameObject go = new GameObject("SwapMenuOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(battleRoot.transform, false);
            existing = go.transform;
        }

        swapMenuRoot = existing.gameObject;
        RectTransform rootRt = swapMenuRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        Image overlay = swapMenuRoot.GetComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, Mathf.Clamp01(swapOverlayOpacity));
        overlay.raycastTarget = true;
        overlay.sprite = null;
        overlay.type = Image.Type.Simple;

        Transform cardsTf = swapMenuRoot.transform.Find("SwapCardsRoot");
        if (cardsTf == null)
        {
            GameObject go = new GameObject("SwapCardsRoot", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(swapMenuRoot.transform, false);
            cardsTf = go.transform;
        }
        swapMenuCardsRoot = cardsTf as RectTransform;
        if (swapMenuCardsRoot != null)
        {
            swapMenuCardsRoot.anchorMin = new Vector2(0.5f, 0.5f);
            swapMenuCardsRoot.anchorMax = new Vector2(0.5f, 0.5f);
            swapMenuCardsRoot.pivot = new Vector2(0.5f, 0.5f);
            swapMenuCardsRoot.anchoredPosition = new Vector2(0f, -8f);
            swapMenuCardsRoot.sizeDelta = new Vector2(980f, 560f);
        }

        HorizontalLayoutGroup hLayout = cardsTf.GetComponent<HorizontalLayoutGroup>();
        if (hLayout == null) hLayout = cardsTf.gameObject.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 48f;
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.childControlWidth = false;
        hLayout.childControlHeight = false;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = false;

        ContentSizeFitter hFitter = cardsTf.GetComponent<ContentSizeFitter>();
        if (hFitter == null) hFitter = cardsTf.gameObject.AddComponent<ContentSizeFitter>();
        hFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        hFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform leftColumn = EnsureSwapColumn(cardsTf, "LeftColumn");
        RectTransform rightColumn = EnsureSwapColumn(cardsTf, "RightColumn");
        ConfigureSwapColumn(leftColumn);
        ConfigureSwapColumn(rightColumn);

        bool rebuildCards = swapCardViews.Count != 6;
        if (!rebuildCards)
        {
            for (int i = 0; i < swapCardViews.Count; i++)
            {
                if (swapCardViews[i] == null || swapCardViews[i].root == null)
                {
                    rebuildCards = true;
                    break;
                }
            }
        }

        if (rebuildCards)
        {
            for (int i = 0; i < swapCardViews.Count; i++)
            {
                if (swapCardViews[i] != null && swapCardViews[i].root != null)
                {
                    if (Application.isPlaying) Destroy(swapCardViews[i].root.gameObject);
                    else DestroyImmediate(swapCardViews[i].root.gameObject);
                }
            }
            swapCardViews.Clear();

            for (int i = 0; i < 6; i++)
            {
                RectTransform parent = i < 3 ? leftColumn : rightColumn;
                swapCardViews.Add(BuildSwapCard(i, parent));
            }
        }

        Transform exitTf = swapMenuRoot.transform.Find("SwapExitButton");
        if (exitTf == null)
        {
            GameObject go = new GameObject("SwapExitButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(swapMenuRoot.transform, false);
            exitTf = go.transform;
        }

        RectTransform exitRt = exitTf as RectTransform;
        if (exitRt != null)
        {
            exitRt.anchorMin = new Vector2(1f, 1f);
            exitRt.anchorMax = new Vector2(1f, 1f);
            exitRt.pivot = new Vector2(1f, 1f);
            exitRt.anchoredPosition = new Vector2(-22f, -18f);
            exitRt.sizeDelta = new Vector2(76f, 76f);
            exitRt.localScale = Vector3.one;
        }

        Image exitImg = exitTf.GetComponent<Image>();
        exitImg.sprite = swapExitIconSprite;
        exitImg.color = Color.white;
        exitImg.type = swapExitIconSprite != null && swapExitIconSprite.border.sqrMagnitude > 0f
            ? Image.Type.Sliced
            : Image.Type.Simple;
        exitImg.preserveAspect = true;

        swapMenuExitButton = exitTf.GetComponent<Button>();
        if (swapMenuExitButton != null)
        {
            swapMenuExitButton.onClick.RemoveAllListeners();
            swapMenuExitButton.onClick.AddListener(OnSwapExitPressed);
            ColorBlock cb = swapMenuExitButton.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            cb.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            swapMenuExitButton.colors = cb;
            swapMenuExitButton.transition = Selectable.Transition.ColorTint;
        }

        UpdateSwapMenuExitState();

        swapMenuRoot.transform.SetAsLastSibling();
        if (!swapMenuOpen && swapMenuRoot.activeSelf)
        {
            swapMenuRoot.SetActive(false);
        }
    }

    void UpdateSwapMenuExitState()
    {
        if (swapMenuExitButton == null) return;
        bool showExit = !swapSelectionForced;
        swapMenuExitButton.gameObject.SetActive(showExit);
        swapMenuExitButton.interactable = showExit;
    }

    RectTransform EnsureSwapColumn(Transform cardsRoot, string name)
    {
        Transform t = cardsRoot.Find(name);
        if (t == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
            go.transform.SetParent(cardsRoot, false);
            t = go.transform;
        }
        return t as RectTransform;
    }

    void ConfigureSwapColumn(RectTransform column)
    {
        if (column == null) return;
        column.anchorMin = new Vector2(0.5f, 0.5f);
        column.anchorMax = new Vector2(0.5f, 0.5f);
        column.pivot = new Vector2(0.5f, 0.5f);
        column.anchoredPosition = Vector2.zero;
        column.sizeDelta = new Vector2(460f, 520f);

        VerticalLayoutGroup vLayout = column.GetComponent<VerticalLayoutGroup>();
        if (vLayout == null) vLayout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        vLayout.spacing = 12f;
        vLayout.childAlignment = TextAnchor.MiddleCenter;
        vLayout.childControlWidth = false;
        vLayout.childControlHeight = false;
        vLayout.childForceExpandWidth = false;
        vLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = column.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = column.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement layout = column.GetComponent<LayoutElement>();
        if (layout == null) layout = column.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = 460f;
        layout.preferredHeight = 520f;
    }

    SwapCardView BuildSwapCard(int index, RectTransform parent)
    {
        GameObject slot = new GameObject(
            "SwapCard" + (index + 1),
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        slot.transform.SetParent(parent, false);

        RectTransform slotRt = slot.GetComponent<RectTransform>();
        slotRt.sizeDelta = new Vector2(430f, 102f);
        LayoutElement layout = slot.GetComponent<LayoutElement>();
        layout.preferredWidth = 430f;
        layout.preferredHeight = 102f;
        layout.minWidth = 430f;
        layout.minHeight = 102f;

        Image bg = slot.GetComponent<Image>();
        bg.sprite = swapCardBackgroundSprite;
        bg.color = Color.white;
        bg.type = swapCardBackgroundSprite != null && swapCardBackgroundSprite.border.sqrMagnitude > 0f
            ? Image.Type.Sliced
            : Image.Type.Simple;
        bg.raycastTarget = true;

        Button btn = slot.GetComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.94f);
        cb.pressedColor = new Color(0.87f, 0.87f, 0.87f, 1f);
        cb.disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.95f);
        btn.colors = cb;
        int captured = index;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnSwapCardSelected(captured));
        CanvasGroup canvasGroup = slot.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = slot.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGo.transform.SetParent(slot.transform, false);
        RectTransform iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(66f, 66f);
        iconRt.anchoredPosition = new Vector2(46f, 0f);
        Image icon = iconGo.GetComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        icon.color = Color.white;

        RectTransform infoRoot = new GameObject("Info", typeof(RectTransform)).GetComponent<RectTransform>();
        infoRoot.transform.SetParent(slot.transform, false);
        infoRoot.anchorMin = new Vector2(0f, 0f);
        infoRoot.anchorMax = new Vector2(1f, 1f);
        infoRoot.pivot = new Vector2(0.5f, 0.5f);
        infoRoot.offsetMin = new Vector2(86f, 9f);
        infoRoot.offsetMax = new Vector2(-10f, -9f);

        Text name = CreateSwapCardText("Name", infoRoot, 27, TextAnchor.UpperLeft);
        Text level = CreateSwapCardText("Level", infoRoot, 24, TextAnchor.UpperRight);
        Text hp = CreateSwapCardText("HP", infoRoot, 19, TextAnchor.MiddleRight);

        LayoutSwapText(name.rectTransform, new Vector2(0f, 0.62f), new Vector2(0.75f, 1f));
        LayoutSwapText(level.rectTransform, new Vector2(0.75f, 0.62f), new Vector2(1f, 1f));
        LayoutSwapText(hp.rectTransform, new Vector2(0.58f, 0.30f), new Vector2(1f, 0.47f));

        Image hpBack = CreateSwapBar("HPBarBG", infoRoot, new Color(0f, 0f, 0f, 0.50f), new Vector2(0f, 0.37f), new Vector2(1f, 0.56f));
        Image hpFill = CreateSwapFill(hpBack.rectTransform, new Color(0.16f, 0.92f, 0.22f, 1f));

        Image xpBack = CreateSwapBar("XPBarBG", infoRoot, new Color(0f, 0f, 0f, 0.50f), new Vector2(0f, 0.11f), new Vector2(1f, 0.27f));
        Image xpFill = CreateSwapFill(xpBack.rectTransform, new Color(0.28f, 0.75f, 1f, 1f));
        LayoutSwapBarPair(hpBack.rectTransform, xpBack.rectTransform);

        GameObject glassGo = new GameObject("Glass", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        glassGo.transform.SetParent(slot.transform, false);
        RectTransform glassRt = glassGo.GetComponent<RectTransform>();
        glassRt.anchorMin = Vector2.zero;
        glassRt.anchorMax = Vector2.one;
        glassRt.offsetMin = Vector2.zero;
        glassRt.offsetMax = Vector2.zero;
        Image glass = glassGo.GetComponent<Image>();
        glass.sprite = swapCardGlassSprite;
        glass.type = swapCardGlassSprite != null && swapCardGlassSprite.border.sqrMagnitude > 0f
            ? Image.Type.Sliced
            : Image.Type.Simple;
        glass.color = Color.white;
        glass.raycastTarget = false;
        glass.enabled = false;

        GameObject levelUpArrowGo = new GameObject("LevelUpArrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        levelUpArrowGo.transform.SetParent(slot.transform, false);
        RectTransform levelUpArrowRt = levelUpArrowGo.GetComponent<RectTransform>();
        levelUpArrowRt.anchorMin = new Vector2(1f, 0.5f);
        levelUpArrowRt.anchorMax = new Vector2(1f, 0.5f);
        levelUpArrowRt.pivot = new Vector2(0f, 0.5f);
        levelUpArrowRt.anchoredPosition = new Vector2(8f, 0f);
        levelUpArrowRt.sizeDelta = new Vector2(32f, 32f);
        levelUpArrowRt.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Image levelUpArrow = levelUpArrowGo.GetComponent<Image>();
        levelUpArrow.sprite = levelUpArrowSprite;
        levelUpArrow.color = new Color(1f, 1f, 1f, 0.94f);
        levelUpArrow.preserveAspect = true;
        levelUpArrow.raycastTarget = false;
        levelUpArrow.enabled = false;

        return new SwapCardView
        {
            slotIndex = index,
            button = btn,
            canvasGroup = canvasGroup,
            root = slotRt,
            layout = layout,
            background = bg,
            iconRect = iconRt,
            icon = icon,
            glass = glass,
            levelUpArrowRect = levelUpArrowRt,
            levelUpArrow = levelUpArrow,
            nameText = name,
            levelText = level,
            hpText = hp,
            hpFill = hpFill,
            xpFill = xpFill
        };
    }

    Text CreateSwapCardText(string name, RectTransform parent, int fontSize, TextAnchor alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        go.transform.SetParent(parent, false);

        Text txt = go.GetComponent<Text>();
        txt.font = GetDefaultUIFont();
        txt.fontSize = fontSize;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = alignment;
        txt.color = Color.white;
        txt.raycastTarget = false;

        Outline outline = go.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
        return txt;
    }

    void LayoutSwapText(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    Image CreateSwapBar(string name, RectTransform parent, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        Outline outline = go.GetComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1f, 1f);
        outline.useGraphicAlpha = true;
        return img;
    }

    Image CreateSwapFill(RectTransform parent, Color color)
    {
        GameObject go = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.sprite = GetNeutralFillSprite();
        img.color = color;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.fillAmount = 1f;
        img.preserveAspect = false;
        img.raycastTarget = false;
        return img;
    }

    void LayoutSwapBarPair(RectTransform hpBar, RectTransform xpBar)
    {
        if (hpBar == null || xpBar == null) return;

        const float hpHeight = 10f;
        const float xpHeight = 5f;
        const float xpGapFromHp = 2f;
        const float hpBottom = 17f;

        float xpBottom = hpBottom - xpHeight - xpGapFromHp;
        LayoutSwapBarRect(hpBar, hpBottom, hpHeight);
        LayoutSwapBarRect(xpBar, xpBottom, xpHeight);
    }

    static void LayoutSwapBarRect(RectTransform rt, float bottom, float height)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(0f, bottom);
        rt.offsetMax = new Vector2(0f, bottom + height);
    }

    void RefreshSwapMenuCards()
    {
        EnsurePlayerPartySource();
        SyncActivePartyCreatureFromBattleState();
        if (playerParty == null || playerParty.ActiveCreatures == null) return;

        int count = Mathf.Min(6, playerParty.ActiveCreatures.Count);
        int activeIndex = ResolveActivePartyIndexForCurrentBattleCreature(count);
        CreatureRegistry.Initialize();

        for (int i = 0; i < swapCardViews.Count; i++)
        {
            SwapCardView view = swapCardViews[i];
            if (view == null || view.root == null) continue;

            bool hasCreature = i < count && playerParty.ActiveCreatures[i] != null;
            CreatureInstance inst = hasCreature ? playerParty.ActiveCreatures[i] : null;
            CreatureDefinition def = inst != null ? CreatureRegistry.Get(inst.definitionID) : null;

            view.root.gameObject.SetActive(true);
            view.button.interactable = false;

            if (!hasCreature)
            {
                if (view.icon != null)
                {
                    view.icon.sprite = null;
                    view.icon.enabled = false;
                }
                if (view.nameText != null) view.nameText.text = "--";
                if (view.levelText != null) view.levelText.text = string.Empty;
                if (view.hpText != null) view.hpText.text = string.Empty;
                if (view.hpFill != null) view.hpFill.fillAmount = 0f;
                if (view.xpFill != null) view.xpFill.fillAmount = 0f;
                if (view.glass != null) view.glass.enabled = false;
                if (view.levelUpArrow != null) view.levelUpArrow.enabled = false;
                continue;
            }

            bool isActive = i == activeIndex;
            int level = Mathf.Max(1, inst.level);
            int maxHp;
            int curHp;
            if (isActive && inBattle && playerCreature != null)
            {
                curHp = Mathf.Max(0, playerCreature.currentHP);
                maxHp = Mathf.Max(1, playerCreature.maxHP);
            }
            else
            {
                maxHp = Mathf.Max(1, CreatureInstanceFactory.ComputeMaxHP(def, inst.soulTraits, level));
                curHp = Mathf.Clamp(inst.currentHP, 0, maxHp);
            }
            string displayName = inst.DisplayName;
            bool isFainted = curHp <= 0;
            bool selectable = !isActive && !isFainted;
            if (view.canvasGroup != null)
            {
                bool dimmed = isActive || isFainted;
                view.canvasGroup.alpha = dimmed ? Mathf.Clamp01(swapActiveCardOpacity) : 1f;
            }

            if (view.icon != null)
            {
                view.icon.sprite = ResolveSwapHeadSprite(def);
                view.icon.enabled = view.icon.sprite != null;
            }
            if (view.nameText != null) view.nameText.text = string.IsNullOrWhiteSpace(displayName) ? "Creature" : displayName;
            if (view.levelText != null) view.levelText.text = "Lv " + level;
            if (view.hpText != null) view.hpText.text = curHp + "/" + maxHp;
            float hpRatio = Mathf.Clamp01((float)curHp / maxHp);
            if (view.hpFill != null)
            {
                view.hpFill.fillAmount = hpRatio;
                view.hpFill.color = ResolveHpTierColor(hpRatio);
            }
            if (view.xpFill != null)
            {
                view.xpFill.fillAmount = ComputeSwapXpRatio(inst);
                view.xpFill.color = new Color(0.28f, 0.75f, 1f, 1f);
            }

            if (view.glass != null)
            {
                view.glass.sprite = swapCardGlassSprite;
                view.glass.enabled = isActive;
            }

            UpdateSwapCardLevelUpIndicator(view, inst);

            view.button.interactable = selectable;
        }
    }

    float ComputeSwapXpRatio(CreatureInstance instance)
    {
        if (instance == null) return 0f;
        CreatureDefinition def = CreatureRegistry.Get(instance.definitionID);
        return CreatureExperienceSystem.GetLevelProgress01(instance, def);
    }

    void UpdateSwapCardLevelUpIndicator(SwapCardView view, CreatureInstance instance)
    {
        if (view == null || view.levelUpArrow == null || view.levelUpArrowRect == null)
        {
            return;
        }

        Sprite arrowSprite = levelUpArrowSprite;
        if (arrowSprite != null && view.levelUpArrow.sprite != arrowSprite)
        {
            view.levelUpArrow.sprite = arrowSprite;
        }

        if (instance == null)
        {
            view.levelUpArrow.enabled = false;
            view.levelUpArrowRect.localScale = Vector3.one;
            return;
        }

        if (!CreatureLevelUpSignal.TryGetPulse01(instance, out float pulse01))
        {
            view.levelUpArrow.enabled = false;
            view.levelUpArrowRect.localScale = Vector3.one;
            return;
        }

        float envelope = 1f - Mathf.Clamp01(pulse01);
        float bob = Mathf.Sin(pulse01 * Mathf.PI * 8f) * 4f * envelope;
        float pop = 1f + Mathf.Sin(pulse01 * Mathf.PI * 6f) * 0.16f * envelope;
        Color c = view.levelUpArrow.color;
        c.a = Mathf.Lerp(0.45f, 1f, envelope);
        view.levelUpArrow.color = c;
        view.levelUpArrow.enabled = true;
        view.levelUpArrowRect.localScale = new Vector3(pop, pop, 1f);
        view.levelUpArrowRect.anchoredPosition = new Vector2(8f, bob);
    }

    void TryMarkBattleParticipant(CreatureCombatant combatant)
    {
        if (!inBattle || combatant == null || combatant.Instance == null) return;

        EnsurePlayerPartySource();
        if (playerParty == null || playerParty.ActiveCreatures == null) return;

        CreatureInstance candidate = combatant.Instance;
        for (int i = 0; i < playerParty.ActiveCreatures.Count; i++)
        {
            if (ReferenceEquals(playerParty.ActiveCreatures[i], candidate))
            {
                battleParticipants.Add(candidate);
                return;
            }
        }
    }

    List<CreatureInstance> ResolveBattleXpRecipients()
    {
        List<CreatureInstance> recipients = new List<CreatureInstance>();
        EnsurePlayerPartySource();
        if (playerParty == null || playerParty.ActiveCreatures == null) return recipients;

        for (int i = 0; i < playerParty.ActiveCreatures.Count; i++)
        {
            CreatureInstance inst = playerParty.ActiveCreatures[i];
            if (inst == null) continue;
            if (battleParticipants.Contains(inst))
            {
                recipients.Add(inst);
            }
        }

        if (recipients.Count == 0 && playerCreature != null && playerCreature.Instance != null)
        {
            for (int i = 0; i < playerParty.ActiveCreatures.Count; i++)
            {
                if (ReferenceEquals(playerParty.ActiveCreatures[i], playerCreature.Instance))
                {
                    recipients.Add(playerCreature.Instance);
                    break;
                }
            }
        }

        return recipients;
    }

    float ResolveBattleXpRarityMultiplier(CreatureDefinition enemyDef)
    {
        if (enemyDef == null) return 1f;

        switch (enemyDef.rarityTier)
        {
            case CreatureRarity.Legendary:
                return Mathf.Max(0.1f, legendaryBattleXpMultiplier);
            case CreatureRarity.Elite:
                return Mathf.Max(0.1f, eliteBattleXpMultiplier);
            case CreatureRarity.Rare:
                return Mathf.Max(0.1f, rareBattleXpMultiplier);
            case CreatureRarity.Uncommon:
                return 1.05f;
            default:
                return 1f;
        }
    }

    int CalculateBattleWinXp()
    {
        int enemyLevel = enemyCreature != null ? Mathf.Max(1, enemyCreature.level) : 1;
        CreatureDefinition enemyDef = enemyCreature != null ? enemyCreature.Definition : null;
        float rarityMultiplier = ResolveBattleXpRarityMultiplier(enemyDef);
        float baseXp = Mathf.Max(1, battleWinBaseXp) + Mathf.Max(0f, battleWinPerEnemyLevel) * enemyLevel;
        return Mathf.Max(1, Mathf.RoundToInt(baseXp * rarityMultiplier));
    }

    IEnumerator ResolveBattleVictoryRewards()
    {
        List<CreatureInstance> recipients = ResolveBattleXpRecipients();
        if (recipients.Count == 0) yield break;

        int xpPerRecipient = CalculateBattleWinXp();

        for (int i = 0; i < recipients.Count; i++)
        {
            CreatureInstance inst = recipients[i];
            if (inst == null) continue;

            CreatureDefinition def = CreatureRegistry.Get(inst.definitionID);
            if (def == null) continue;

            ExperienceGainResult gain = CreatureExperienceSystem.AddExperience(inst, def, xpPerRecipient);
            if (ReferenceEquals(playerCreature != null ? playerCreature.Instance : null, inst))
            {
                ApplyInstanceProgressionToCombatant(playerCreature, def, inst);
            }

            if (gain.experienceGranted > 0)
            {
                SetMessage(inst.DisplayName + " gained " + gain.experienceGranted + " XP!");
                UpdateUI();
                RefreshSwapMenuCards();
                yield return new WaitForSeconds(Mathf.Max(0.2f, actionPhaseDelay * 0.8f));
            }

            if (gain.leveledUp)
            {
                CreatureLevelUpSignal.Notify(inst);
                SetMessage(inst.DisplayName + " grew to Lv " + gain.newLevel + "!");
                UpdateUI();
                RefreshSwapMenuCards();
                yield return new WaitForSeconds(Mathf.Max(0.55f, actionNarrationDelay * 0.72f));
            }
        }

        if (playerParty != null)
        {
            playerParty.NotifyPartyChanged();
        }
    }

    void ApplyInstanceProgressionToCombatant(CreatureCombatant combatant, CreatureDefinition def, CreatureInstance inst)
    {
        if (combatant == null || def == null || inst == null) return;

        combatant.level = Mathf.Clamp(inst.level, 1, CreatureExperienceSystem.MaxLevel);
        combatant.creatureName = string.IsNullOrWhiteSpace(inst.DisplayName) ? def.displayName : inst.DisplayName;

        CreatureStats stats = combatant.GetFinalStats();
        combatant.maxHP = Mathf.Max(1, stats.maxHP);
        combatant.attack = Mathf.Max(1, stats.attack);
        combatant.defense = Mathf.Max(1, stats.defense);
        combatant.speed = Mathf.Max(1, stats.speed);
        combatant.currentHP = Mathf.Clamp(inst.currentHP, 0, combatant.maxHP);
        combatant.SyncInstanceRuntimeState();
    }

    int ResolveActivePartyIndexForCurrentBattleCreature(int countLimit)
    {
        if (playerParty == null || playerParty.ActiveCreatures == null || playerParty.ActiveCreatures.Count == 0)
        {
            return 0;
        }

        int count = Mathf.Clamp(countLimit, 0, playerParty.ActiveCreatures.Count);
        if (count <= 0) return 0;

        if (inBattle && playerCreature != null && playerCreature.Instance != null)
        {
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(playerParty.ActiveCreatures[i], playerCreature.Instance))
                {
                    return i;
                }
            }
        }

        return Mathf.Clamp(playerParty.ActivePartyIndex, 0, count - 1);
    }

    Sprite ResolveSwapHeadSprite(CreatureDefinition def)
    {
        if (def == null || def.sprite == null) return null;

        string id = string.IsNullOrWhiteSpace(def.creatureID) ? def.name : def.creatureID;
        if (swapHeadSpriteCache.TryGetValue(id, out Sprite cached) && cached != null)
        {
            return cached;
        }

        Sprite source = def.sprite;
        Rect src = source.textureRect;
        float cropW = Mathf.Clamp(src.width * 0.92f, 8f, src.width);
        float cropH = Mathf.Clamp(src.height * 0.58f, 8f, src.height);
        float centerX = src.center.x;
        float centerY = src.yMin + (src.height * 0.70f);

        float x = Mathf.Clamp(centerX - cropW * 0.5f, src.xMin, src.xMax - cropW);
        float y = Mathf.Clamp(centerY - cropH * 0.5f, src.yMin, src.yMax - cropH);
        Rect crop = new Rect(Mathf.Round(x), Mathf.Round(y), Mathf.Round(cropW), Mathf.Round(cropH));

        Sprite head = Sprite.Create(source.texture, crop, new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        head.name = id + "_swap_head";
        swapHeadSpriteCache[id] = head;
        generatedSwapHeadSprites.Add(head);
        return head;
    }

    void OnSwapCardSelected(int slotIndex)
    {
        if (!swapMenuOpen || turnResolutionInProgress || !inBattle || !waitingForPlayerMove) return;
        EnsurePlayerPartySource();
        if (playerParty == null || playerParty.ActiveCreatures == null) return;

        int count = playerParty.ActiveCreatures.Count;
        if (slotIndex < 0 || slotIndex >= count) return;

        int currentIndex = ResolveActivePartyIndexForCurrentBattleCreature(count);
        if (slotIndex == currentIndex) return;

        CreatureInstance target = playerParty.ActiveCreatures[slotIndex];
        if (target == null)
        {
            SetMessage("Invalid party slot.");
            return;
        }

        if (target.currentHP <= 0)
        {
            SetMessage(target.DisplayName + " has fainted.");
            return;
        }

        bool consumeTurnOnSelection = swapSelectionConsumesTurn;
        CloseSwapMenu(false);
        SetActionMenuVisible(false);
        SetBackButtonVisible(false);
        waitingForPlayerMove = false;
        turnResolutionInProgress = true;
        RefreshTurnInputState();
        StartCoroutine(ResolveSwapTurn(slotIndex, target, consumeTurnOnSelection));
    }

    IEnumerator ResolveSwapTurn(int swapSlotIndex, CreatureInstance swappedInInstance, bool consumeTurnOnSelection)
    {
        if (!inBattle) yield break;
        if (playerCreature == null || enemyCreature == null)
        {
            turnResolutionInProgress = false;
            waitingForPlayerMove = true;
            SetActionMenuVisible(true);
            SetBackButtonVisible(false);
            SetMessage("Choose an action.");
            RefreshTurnInputState();
            yield break;
        }

        if (!turnResolutionInProgress) turnResolutionInProgress = true;
        bool skipIdleForSwap = playerSpriteImage != null && activeAttackAnimations.Add(playerSpriteImage);
        waitingForPlayerMove = false;
        string outgoingName = playerCreature != null ? playerCreature.creatureName : "Creature";
        SetMessage("Return, " + outgoingName + "!");
        RefreshTurnInputState();

        yield return StartCoroutine(AnimatePlayerSwapSlideOut());

        if (playerCreature != null)
        {
            playerCreature.SyncInstanceRuntimeState();
            TryMarkBattleParticipant(playerCreature);
        }

        playerParty.SetActivePartyIndex(swapSlotIndex);
        playerCreature = ResolvePlayerCombatant();

        if (playerCreature != null && playerCreature.Instance != swappedInInstance)
        {
            CreatureDefinition def = CreatureRegistry.Get(swappedInInstance.definitionID);
            if (def != null)
            {
                playerCreature.autoInitWhelpling = false;
                playerCreature.InitFromDefinition(def, swappedInInstance);
            }
        }

        if (playerCreature != null)
        {
            playerCreature.SyncInstanceRuntimeState();
        }

        playerFaintedVisualLocked = false;
        UpdateCreatureSprites();
        UpdateUI();
        yield return StartCoroutine(AnimatePlayerSwapSlideIn());

        if (skipIdleForSwap)
        {
            activeAttackAnimations.Remove(playerSpriteImage);
        }

        string swappedInName = swappedInInstance != null ? swappedInInstance.DisplayName : "Creature";
        SetMessage("Go, " + swappedInName + "!");
        yield return new WaitForSeconds(actionPhaseDelay);

        if (!consumeTurnOnSelection)
        {
            bool playerActsFirstAfterForcedSwap = DecideTurnOrder();
            if (!playerActsFirstAfterForcedSwap)
            {
                SetMessage("Opponent moves first.");
                yield return new WaitForSeconds(opponentTurnDelay);

                AttackData priorityEnemyAttack = ChooseEnemyAttack();
                if (priorityEnemyAttack != null)
                {
                    yield return PerformAttack(enemyCreature, playerCreature, priorityEnemyAttack);
                }

                if (playerCreature == null || playerCreature.currentHP <= 0)
                {
                    turnResolutionInProgress = false;
                    HandlePlayerCreatureFaintInBattle();
                    yield break;
                }

                ApplyEndOfTurnStatuses(playerCreature);
                ApplyEndOfTurnStatuses(enemyCreature);
                UpdateUI();
            }

            waitingForPlayerMove = true;
            turnResolutionInProgress = false;
            SetActionMenuVisible(true);
            SetBackButtonVisible(false);
            SetMessage("Choose an action.");
            RefreshTurnInputState();
            yield break;
        }

        SetMessage("Opponent's turn.");
        yield return new WaitForSeconds(opponentTurnDelay);

        AttackData enemyAttack = ChooseEnemyAttack();
        if (enemyAttack != null)
        {
            yield return PerformAttack(enemyCreature, playerCreature, enemyAttack);
        }

        if (playerCreature == null || playerCreature.currentHP <= 0)
        {
            turnResolutionInProgress = false;
            HandlePlayerCreatureFaintInBattle();
            yield break;
        }

        ApplyEndOfTurnStatuses(playerCreature);
        ApplyEndOfTurnStatuses(enemyCreature);
        UpdateUI();

        waitingForPlayerMove = true;
        turnResolutionInProgress = false;
        SetActionMenuVisible(true);
        SetBackButtonVisible(false);
        SetMessage("Choose an action.");
        RefreshTurnInputState();
    }

    IEnumerator AnimatePlayerSwapSlideOut()
    {
        if (playerSpriteImage == null || playerSpriteImage.rectTransform == null) yield break;
        RectTransform rt = playerSpriteImage.rectTransform;
        Vector3 start = playerSpriteBaseLocalPos;
        Vector3 end = GetPlayerSwapOffscreenLocalPos(start.y, start.z);
        yield return StartCoroutine(AnimateRectLocalSlide(rt, start, end, 0.24f));
        rt.localPosition = end;
        if (playerShadowImage != null) playerShadowImage.enabled = false;
    }

    IEnumerator AnimatePlayerSwapSlideIn()
    {
        if (playerSpriteImage == null || playerSpriteImage.rectTransform == null) yield break;
        RectTransform rt = playerSpriteImage.rectTransform;
        Vector3 end = playerSpriteBaseLocalPos;
        Vector3 start = GetPlayerSwapOffscreenLocalPos(end.y, end.z);
        rt.localPosition = start;
        if (playerShadowImage != null) playerShadowImage.enabled = true;
        yield return StartCoroutine(AnimateRectLocalSlide(rt, start, end, 0.30f));
        rt.localPosition = end;
    }

    Vector3 GetPlayerSwapOffscreenLocalPos(float y, float z)
    {
        float arenaWidth = arenaPanel != null ? arenaPanel.rect.width : Screen.width;
        float offscreenX = -Mathf.Max(460f, arenaWidth * 0.58f);
        return new Vector3(offscreenX, y, z);
    }

    IEnumerator AnimateRectLocalSlide(RectTransform rt, Vector3 from, Vector3 to, float duration)
    {
        if (rt == null) yield break;
        float total = Mathf.Max(0.01f, duration);
        float t = 0f;
        while (t < total)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / total);
            float eased = 1f - Mathf.Pow(1f - p, 3f);
            rt.localPosition = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }
        rt.localPosition = to;
    }

    void OnSwapExitPressed()
    {
        if (!swapMenuOpen || swapMenuExitAnimating) return;
        if (swapSelectionForced) return;
        StartCoroutine(AnimateSwapExitAndClose());
    }

    IEnumerator AnimateSwapExitAndClose()
    {
        if (swapMenuExitButton == null)
        {
            CloseSwapMenu(true);
            SetMessage("Choose an action.");
            yield break;
        }

        swapMenuExitAnimating = true;
        RectTransform rt = swapMenuExitButton.transform as RectTransform;
        Vector3 baseScale = rt != null ? rt.localScale : Vector3.one;

        float t = 0f;
        float shrinkTime = 0.07f;
        while (t < shrinkTime)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / shrinkTime);
            if (rt != null) rt.localScale = Vector3.Lerp(baseScale, baseScale * 0.82f, p);
            yield return null;
        }

        t = 0f;
        float growTime = 0.08f;
        while (t < growTime)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / growTime);
            if (rt != null) rt.localScale = Vector3.Lerp(baseScale * 0.82f, baseScale * 1.08f, p);
            yield return null;
        }

        t = 0f;
        float settleTime = 0.07f;
        while (t < settleTime)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / settleTime);
            if (rt != null) rt.localScale = Vector3.Lerp(baseScale * 1.08f, baseScale, p);
            yield return null;
        }

        if (rt != null) rt.localScale = baseScale;
        swapMenuExitAnimating = false;

        CloseSwapMenu(true);
        SetMessage("Choose an action.");
    }

    void CloseSwapMenu(bool restoreActionMenu)
    {
        swapMenuOpen = false;
        swapMenuExitAnimating = false;
        swapSelectionForced = false;
        swapSelectionConsumesTurn = true;
        if (swapMenuRoot != null)
        {
            swapMenuRoot.SetActive(false);
        }

        if (restoreActionMenu && inBattle)
        {
            if (movePanel != null) movePanel.SetActive(false);
            SetActionMenuVisible(true);
            SetBackButtonVisible(false);
        }
        RefreshTurnInputState();
    }

    void SelectMove(int index)
    {
        if (turnResolutionInProgress) return;
        if (!waitingForPlayerMove || playerCreature == null || enemyCreature == null) return;
        int unlockLevel = 1 + index * 5;
        if (playerCreature.level < unlockLevel) return;
        if (playerCreature.attacks == null || index < 0 || index >= playerCreature.attacks.Count) return;

        AttackData atk = playerCreature.attacks[index];
        if (atk == null) return;
        if (atk.currentPP <= 0) return;

        if (movePanel != null) movePanel.SetActive(false);
        SetActionMenuVisible(true);
        SetBackButtonVisible(false);
        waitingForPlayerMove = false;
        turnResolutionInProgress = true;
        RefreshTurnInputState();
        StartCoroutine(ResolveTurn(atk));
    }

    IEnumerator ResolveTurn(AttackData playerAttack)
    {
        if (!turnResolutionInProgress) turnResolutionInProgress = true;
        waitingForPlayerMove = false;
        RefreshTurnInputState();
        yield return new WaitForSeconds(actionPhaseDelay);

        AttackData enemyAttack = ChooseEnemyAttack();
        yield return PerformAttack(playerCreature, enemyCreature, playerAttack);
        if (enemyCreature.currentHP <= 0)
        {
            yield return StartCoroutine(ResolveBattleVictoryRewards());
            turnResolutionInProgress = false;
            EndBattle(true);
            yield break;
        }

        SetMessage("Opponent's turn.");
        yield return new WaitForSeconds(opponentTurnDelay);

        if (enemyAttack != null)
        {
            yield return PerformAttack(enemyCreature, playerCreature, enemyAttack);
        }
        if (playerCreature.currentHP <= 0)
        {
            turnResolutionInProgress = false;
            HandlePlayerCreatureFaintInBattle();
            yield break;
        }

        ApplyEndOfTurnStatuses(playerCreature);
        ApplyEndOfTurnStatuses(enemyCreature);

        UpdateUI();
        waitingForPlayerMove = true;
        turnResolutionInProgress = false;
        SetActionMenuVisible(true);
        SetBackButtonVisible(false);
        SetMessage("Choose an action.");
        RefreshTurnInputState();
    }

    AttackData ChooseEnemyAttack()
    {
        if (enemyCreature.attacks == null || enemyCreature.attacks.Count == 0)
        {
            string enemyId = ResolveCreatureID(
                currentEnemyAI != null ? currentEnemyAI.gameObject : (enemyCreature != null ? enemyCreature.gameObject : null),
                enemyCreature
            );
            ConfigureCombatantByCreatureID(enemyCreature, enemyId, Mathf.Max(1, enemyCreature.level), false);
        }

        if (enemyCreature.attacks == null || enemyCreature.attacks.Count == 0) return null;

        for (int i = 0; i < enemyCreature.attacks.Count; i++)
        {
            int unlockLevel = 1 + i * 5;
            AttackData candidate = enemyCreature.attacks[i];
            if (candidate == null) continue;
            if (enemyCreature.level >= unlockLevel && candidate.currentPP > 0)
            {
                return candidate;
            }
        }
        return enemyCreature.attacks[0];
    }

    bool DecideTurnOrder()
    {
        CreatureStats playerStats = playerCreature != null ? playerCreature.GetFinalStats() : default;
        CreatureStats enemyStats = enemyCreature != null ? enemyCreature.GetFinalStats() : default;
        int playerSpd = Mathf.Max(1, playerStats.speed);
        int enemySpd = Mathf.Max(1, enemyStats.speed);

        if (playerCreature != null) playerCreature.speed = playerSpd;
        if (enemyCreature != null) enemyCreature.speed = enemySpd;

        if (playerSpd > enemySpd) return true;
        if (playerSpd < enemySpd) return false;
        return Random.value > 0.5f;
    }

    IEnumerator PerformAttack(CreatureCombatant attacker, CreatureCombatant defender, AttackData atk)
    {
        if (attacker == null || defender == null || atk == null) yield break;
        TryMarkBattleParticipant(attacker);

        Image attackerImage = GetSpriteImageForCombatant(attacker);
        Image defenderImage = GetSpriteImageForCombatant(defender);

        if (IsSkippedByStatus(attacker))
        {
            SetMessage(attacker.creatureName + " hesitated!");
            yield return new WaitForSeconds(0.5f);
            yield break;
        }

        atk.currentPP = Mathf.Max(0, atk.currentPP - 1);

        if (TryConsumeGuaranteedDodge(defender))
        {
            SetMessage(defender.creatureName + " dodged the attack!");
            yield return new WaitForSeconds(0.45f);
            yield break;
        }

        if (!RollAccuracy(atk.accuracy, attacker))
        {
            SetMessage(attacker.creatureName + " missed!");
            yield return new WaitForSeconds(0.5f);
            yield break;
        }

        bool isCrit = RollCrit(attacker);
        float typeMultiplier = EvaluateTypeMultiplier(atk.type, defender);
        int damage = CalculateDamage(attacker, defender, atk, isCrit, typeMultiplier);

        bool dealsDamage = atk.baseDamage > 0;
        if (dealsDamage)
        {
            yield return StartCoroutine(AnimateAttack(attackerImage, defenderImage, true));
            PlayBattleClip(moveDamageSfx);
        }
        else
        {
            yield return new WaitForSeconds(0.08f);
        }

        if (dealsDamage)
        {
            defender.currentHP = Mathf.Max(0, defender.currentHP - damage);
        }

        if (atk.specialFlag == MoveFlag.GuaranteeDodge)
        {
            AddGuaranteedDodge(attacker, 1);
        }

        bool debuffApplied = ApplyStatusFromAttack(defender, atk, isCrit);
        if (debuffApplied)
        {
            PlayBattleClip(debuffSfx);
        }

        attacker.SyncInstanceRuntimeState();
        defender.SyncInstanceRuntimeState();
        UpdateUI();

        SetMessage(attacker.creatureName + " used " + atk.name + "!");
        yield return new WaitForSeconds(actionNarrationDelay);

        if (dealsDamage)
        {
            string effectiveness = ResolveEffectivenessNarration(typeMultiplier);
            if (!string.IsNullOrEmpty(effectiveness))
            {
                SetMessage(effectiveness);
                yield return new WaitForSeconds(Mathf.Max(0.65f, actionNarrationDelay * 0.68f));
            }
        }

        if (defender.currentHP <= 0)
        {
            yield return StartCoroutine(AnimateFaintShrink(defender));
        }
    }

    bool RollAccuracy(int accuracy, CreatureCombatant attacker)
    {
        int effective = Mathf.Clamp(accuracy, 1, 100);
        if (attacker != null)
        {
            if (attacker.HasStatus(StatusEffectType.Blind))
            {
                effective = Mathf.Max(1, Mathf.RoundToInt(effective * 0.80f));
            }

            CreatureStats stats = attacker.GetFinalStats();
            effective = Mathf.Clamp(Mathf.RoundToInt(effective * stats.accuracyModifier), 1, 100);
        }

        int roll = Random.Range(1, 101);
        return roll <= effective;
    }

    bool RollCrit(CreatureCombatant attacker)
    {
        float chance = 0.10f;
        if (attacker != null)
        {
            CreatureStats stats = attacker.GetFinalStats();
            chance *= Mathf.Clamp(stats.critModifier, 1f, 2f);
        }
        return Random.value <= chance;
    }

    int CalculateDamage(CreatureCombatant attacker, CreatureCombatant defender, AttackData atk, bool isCrit, float typeMultiplier)
    {
        int moveDamage = isCrit ? atk.critDamage : atk.baseDamage;
        if (moveDamage <= 0) return 0;

        CreatureStats attackerStats = attacker != null ? attacker.GetFinalStats() : default;
        CreatureStats defenderStats = defender != null ? defender.GetFinalStats() : default;

        float attackerAttack = Mathf.Max(1f, attackerStats.attack);
        float defenderDefense = Mathf.Max(1f, defenderStats.defense);
        float attackDivisor = Mathf.Max(0.1f, damageAttackStatDivisor);

        // Step 1: Convert move coefficient into stat-weighted attack power.
        float attackPower = moveDamage * (attackerAttack / attackDivisor);

        // Step 2: Piecewise stat-vs-defense reduction.
        float rawDamage;
        if (attackPower >= defenderDefense)
        {
            float branchMultiplier = Mathf.Max(0.1f, damageHighAttackBranchMultiplier);
            rawDamage = attackPower * branchMultiplier * attackPower / Mathf.Max(1f, attackPower + defenderDefense);
        }
        else
        {
            rawDamage = (attackPower * attackPower) / Mathf.Max(1f, defenderDefense);
        }

        // Step 3: Quadratic-style level gap scaling (tunable exponent and clamps).
        int attackerLevel = attacker != null ? Mathf.Max(1, attacker.level) : 1;
        int defenderLevel = defender != null ? Mathf.Max(1, defender.level) : 1;
        float levelRatio = attackerLevel / Mathf.Max(1f, defenderLevel);
        float levelExponent = Mathf.Max(0.1f, damageLevelRatioExponent);
        float levelMultiplier = Mathf.Pow(levelRatio, levelExponent);
        float levelMin = Mathf.Max(0.01f, damageLevelMultiplierMin);
        float levelMax = Mathf.Max(levelMin, damageLevelMultiplierMax);
        levelMultiplier = Mathf.Clamp(levelMultiplier, levelMin, levelMax);

        float scaledDamage = rawDamage * levelMultiplier;

        // Step 4+: Type + move-specific + status modifiers.
        float multiplier = Mathf.Max(0f, typeMultiplier);

        if (atk.specialFlag == MoveFlag.DoubleDamageIfStatused && defender != null && defender.statusEffects != null && defender.statusEffects.Count > 0)
        {
            multiplier *= 2f;
        }

        if (atk.specialFlag == MoveFlag.DragonBonus && attacker != null)
        {
            CreatureType[] attackerTypes = attacker.GetResolvedTypes();
            for (int i = 0; i < attackerTypes.Length; i++)
            {
                if (attackerTypes[i] == CreatureType.Dragon)
                {
                    multiplier *= 1.25f;
                    break;
                }
            }
        }

        if (attacker != null && attacker.HasStatus(StatusEffectType.Burn) && atk.isPhysical)
        {
            multiplier *= 0.5f;
        }

        int finalDmg = Mathf.FloorToInt(scaledDamage * multiplier);
        return Mathf.Max(Mathf.Max(1, minimumDamagePerHit), finalDmg);
    }

    float EvaluateTypeMultiplier(CreatureType attackType, CreatureCombatant defender)
    {
        CreatureType[] defenderTypes = defender != null ? defender.GetResolvedTypes() : null;
        if (defenderTypes == null || defenderTypes.Length == 0)
        {
            defenderTypes = new[] { CreatureType.Normal };
        }

        float multiplier = 1f;
        for (int i = 0; i < defenderTypes.Length; i++)
        {
            multiplier *= TypeMultiplier(attackType, defenderTypes[i]);
        }
        return multiplier;
    }

    string ResolveEffectivenessNarration(float typeMultiplier)
    {
        if (Mathf.Abs(typeMultiplier - 0.5f) <= 0.02f)
        {
            return "It's weak... (0.5x)";
        }

        if (Mathf.Abs(typeMultiplier - 1.5f) <= 0.02f)
        {
            return "It's very strong! (1.5x)";
        }

        return string.Empty;
    }

    float TypeMultiplier(CreatureType atk, CreatureType def)
    {
        if (atk == CreatureType.Fire)
        {
            if (def == CreatureType.Nature || def == CreatureType.Ice) return 1.5f;
            if (def == CreatureType.Water || def == CreatureType.Earth) return 0.5f;
        }
        else if (atk == CreatureType.Water)
        {
            if (def == CreatureType.Fire || def == CreatureType.Earth) return 1.5f;
            if (def == CreatureType.Lightning) return 0.5f;
        }
        else if (atk == CreatureType.Dragon)
        {
            if (def == CreatureType.Dragon) return 1.5f;
        }
        else if (atk == CreatureType.Nature)
        {
            if (def == CreatureType.Earth || def == CreatureType.Water || def == CreatureType.Dark) return 1.5f;
            if (def == CreatureType.Fire || def == CreatureType.Ice) return 0.5f;
        }
        else if (atk == CreatureType.Ice)
        {
            if (def == CreatureType.Nature || def == CreatureType.Earth || def == CreatureType.Dragon) return 1.5f;
            if (def == CreatureType.Fire) return 0.5f;
        }
        else if (atk == CreatureType.Lightning)
        {
            if (def == CreatureType.Water) return 1.5f;
            if (def == CreatureType.Earth) return 0.5f;
        }
        else if (atk == CreatureType.Earth)
        {
            if (def == CreatureType.Fire || def == CreatureType.Lightning) return 1.5f;
            if (def == CreatureType.Nature || def == CreatureType.Water) return 0.5f;
        }
        else if (atk == CreatureType.Light)
        {
            if (def == CreatureType.Earth || def == CreatureType.Dark) return 1.5f;
        }
        else if (atk == CreatureType.Dark)
        {
            if (def == CreatureType.Nature || def == CreatureType.Light) return 1.5f;
            if (def == CreatureType.Lightning) return 0.5f;
        }
        return 1f;
    }

    bool ApplyStatusFromAttack(CreatureCombatant defender, AttackData atk, bool isCrit)
    {
        if (atk.statusToApply == null) return false;
        if (defender == null) return false;
        if (atk.statusToApply.Value == StatusEffectType.None) return false;

        if (Random.value <= atk.statusChance)
        {
            int turns = atk.statusDuration;
            if (turns == 0)
            {
                turns = isCrit ? 3 : 2;
            }
            defender.AddOrRefreshStatus(atk.statusToApply.Value, turns);
            return true;
        }

        return false;
    }

    bool IsSkippedByStatus(CreatureCombatant combatant)
    {
        if (combatant == null) return false;

        if (combatant.HasStatus(StatusEffectType.Frozen))
        {
            combatant.TickStatus(StatusEffectType.Frozen);
            return true;
        }

        if (combatant.HasStatus(StatusEffectType.Anxious))
        {
            StatusEffect s = combatant.GetStatus(StatusEffectType.Anxious);
            bool acts = Random.value <= 0.10f;
            if (!acts)
            {
                s.turns -= 1;
                if (s.turns <= 0) combatant.statusEffects.Remove(s);
                return true;
            }
        }

        if (combatant.HasStatus(StatusEffectType.Terrified))
        {
            if (Random.value <= 0.35f)
            {
                combatant.TickStatus(StatusEffectType.Terrified);
                return true;
            }
        }

        if (combatant.HasStatus(StatusEffectType.Paralysed))
        {
            if (Random.value <= 0.20f)
            {
                combatant.TickStatus(StatusEffectType.Paralysed);
                return true;
            }
        }

        return false;
    }

    void ApplyEndOfTurnStatuses(CreatureCombatant combatant)
    {
        if (combatant == null) return;

        if (combatant.HasStatus(StatusEffectType.Burn))
        {
            combatant.currentHP = Mathf.Max(0, combatant.currentHP - 2);
            combatant.TickStatus(StatusEffectType.Burn);
        }

        if (combatant.HasStatus(StatusEffectType.Wet))
        {
            combatant.TickStatus(StatusEffectType.Wet);
        }

        if (combatant.HasStatus(StatusEffectType.Paralysed))
        {
            combatant.TickStatus(StatusEffectType.Paralysed);
        }

        if (combatant.HasStatus(StatusEffectType.Poisoned))
        {
            combatant.TickStatus(StatusEffectType.Poisoned);
        }

        if (combatant.HasStatus(StatusEffectType.Blind))
        {
            combatant.TickStatus(StatusEffectType.Blind);
        }

        if (combatant.HasStatus(StatusEffectType.Terrified))
        {
            combatant.TickStatus(StatusEffectType.Terrified);
        }

        combatant.SyncInstanceRuntimeState();
    }

    void AddGuaranteedDodge(CreatureCombatant combatant, int charges)
    {
        if (combatant == null || charges <= 0) return;
        if (guaranteedDodgeCharges.TryGetValue(combatant, out int current))
        {
            guaranteedDodgeCharges[combatant] = current + charges;
        }
        else
        {
            guaranteedDodgeCharges[combatant] = charges;
        }
    }

    bool TryConsumeGuaranteedDodge(CreatureCombatant combatant)
    {
        if (combatant == null) return false;
        if (!guaranteedDodgeCharges.TryGetValue(combatant, out int current)) return false;
        if (current <= 0)
        {
            guaranteedDodgeCharges.Remove(combatant);
            return false;
        }

        current -= 1;
        if (current <= 0)
        {
            guaranteedDodgeCharges.Remove(combatant);
        }
        else
        {
            guaranteedDodgeCharges[combatant] = current;
        }

        return true;
    }

    int FindFirstAliveBenchPartyIndex()
    {
        EnsurePlayerPartySource();
        if (playerParty == null || playerParty.ActiveCreatures == null || playerParty.ActiveCreatures.Count == 0)
        {
            return -1;
        }

        int activeIndex = ResolveActivePartyIndexForCurrentBattleCreature(playerParty.ActiveCreatures.Count);
        for (int i = 0; i < playerParty.ActiveCreatures.Count; i++)
        {
            if (i == activeIndex) continue;
            CreatureInstance candidate = playerParty.ActiveCreatures[i];
            if (candidate == null) continue;
            if (candidate.currentHP > 0) return i;
        }

        return -1;
    }

    bool AnyPartyCreatureAlive()
    {
        EnsurePlayerPartySource();
        if (playerParty == null || playerParty.ActiveCreatures == null || playerParty.ActiveCreatures.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < playerParty.ActiveCreatures.Count; i++)
        {
            CreatureInstance c = playerParty.ActiveCreatures[i];
            if (c != null && c.currentHP > 0) return true;
        }

        return false;
    }

    void HandlePlayerCreatureFaintInBattle()
    {
        if (!inBattle) return;

        SyncActivePartyCreatureFromBattleState();
        int aliveBenchIndex = FindFirstAliveBenchPartyIndex();
        if (aliveBenchIndex >= 0)
        {
            waitingForPlayerMove = true;
            turnResolutionInProgress = false;
            if (movePanel != null) movePanel.SetActive(false);
            SetActionMenuVisible(false);
            SetBackButtonVisible(false);
            OpenForcedSwapMenuAfterFaint();
            RefreshTurnInputState();
            return;
        }

        if (AnyPartyCreatureAlive())
        {
            waitingForPlayerMove = true;
            turnResolutionInProgress = false;
            if (movePanel != null) movePanel.SetActive(false);
            SetActionMenuVisible(false);
            SetBackButtonVisible(false);
            OpenForcedSwapMenuAfterFaint();
            RefreshTurnInputState();
            return;
        }

        HandlePlayerDefeat();
    }

    void HandlePlayerDefeat()
    {
        SetMessage("Your creature fainted!");
        EndBattle(false);
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(playerHealth.maxHealth, true);
        }
    }

    void TryRun()
    {
        if (!inBattle || !waitingForPlayerMove || turnResolutionInProgress) return;

        SyncActivePartyCreatureFromBattleState();
        waitingForPlayerMove = false;
        if (movePanel != null) movePanel.SetActive(false);
        SetActionMenuVisible(true);
        SetBackButtonVisible(false);
        SetMessage("You fled safely.");
        EndBattle(false);
    }

    void UpdateUI()
    {
        SyncActivePartyCreatureFromBattleState();
        EnsureEnemyHpText();
        HidePlayerXpCardBar();

        if (playerCreature != null)
        {
            if (playerNameText != null) playerNameText.text = playerCreature.creatureName;
            if (playerLevelText != null) playerLevelText.text = "Lv " + playerCreature.level;
            if (playerTypesText != null) playerTypesText.text = TypesToString(playerCreature.GetResolvedTypes());
            if (playerHpText != null) playerHpText.text = playerCreature.currentHP + " / " + playerCreature.maxHP;
            float playerHpRatio = playerCreature.maxHP > 0 ? (float)playerCreature.currentHP / playerCreature.maxHP : 0f;
            ApplyHpFillVisual(playerHpFill, playerHpRatio);
        }
        else
        {
            if (playerNameText != null) playerNameText.text = "Unknown";
            if (playerLevelText != null) playerLevelText.text = "Lv ?";
            if (playerTypesText != null) playerTypesText.text = "None";
            if (playerHpText != null) playerHpText.text = "-- / --";
            ApplyHpFillVisual(playerHpFill, 0f);
        }

        if (enemyCreature != null)
        {
            if (enemyNameText != null) enemyNameText.text = enemyCreature.creatureName;
            if (enemyLevelText != null) enemyLevelText.text = "Lv " + enemyCreature.level;
            if (enemyTypesText != null) enemyTypesText.text = TypesToString(enemyCreature.GetResolvedTypes());
            if (enemyHpText != null) enemyHpText.text = enemyCreature.currentHP + " / " + enemyCreature.maxHP;
            float enemyHpRatio = enemyCreature.maxHP > 0 ? (float)enemyCreature.currentHP / enemyCreature.maxHP : 0f;
            ApplyHpFillVisual(enemyHpFill, enemyHpRatio);
        }
        else
        {
            if (enemyNameText != null) enemyNameText.text = "Unknown";
            if (enemyLevelText != null) enemyLevelText.text = "Lv ?";
            if (enemyTypesText != null) enemyTypesText.text = "None";
            if (enemyHpText != null) enemyHpText.text = "-- / --";
            ApplyHpFillVisual(enemyHpFill, 0f);
        }
    }

    void ApplyHpFillVisual(Image hpFill, float ratio)
    {
        if (hpFill == null) return;

        float clamped = Mathf.Clamp01(ratio);
        Sprite neutral = GetNeutralFillSprite();
        if (neutral != null && hpFill.sprite != neutral)
        {
            hpFill.sprite = neutral;
        }
        hpFill.type = Image.Type.Filled;
        hpFill.fillMethod = Image.FillMethod.Horizontal;
        hpFill.fillOrigin = 0;
        hpFill.preserveAspect = false;
        hpFill.material = null;
        hpFill.fillAmount = clamped;
        hpFill.color = ResolveHpTierColor(clamped);
    }

    void HidePlayerXpCardBar()
    {
        if (playerXpFill != null)
        {
            playerXpFill.fillAmount = 0f;
            if (playerXpFill.gameObject.activeSelf)
            {
                playerXpFill.gameObject.SetActive(false);
            }
        }

        if (playerXpBg != null && playerXpBg.gameObject.activeSelf)
        {
            playerXpBg.gameObject.SetActive(false);
        }
    }

    Color ResolveHpTierColor(float ratio)
    {
        float clamped = Mathf.Clamp01(ratio);
        // Requested tiers:
        // 100%-75% = green, 75%-50% = yellow, 50%-25% = orange, 25% and below = red.
        if (clamped > 0.75f) return HpGreen;
        if (clamped > 0.5f) return HpYellow;
        if (clamped > 0.25f) return HpOrange;
        return HpRed;
    }

    void SyncActivePartyCreatureFromBattleState()
    {
        if (!inBattle) return;
        EnsurePlayerPartySource();
        if (playerParty == null || playerParty.ActiveCreatures == null || playerParty.ActiveCreatures.Count == 0) return;
        if (playerCreature == null) return;

        int idx = ResolveActivePartyIndexForCurrentBattleCreature(playerParty.ActiveCreatures.Count);
        if (idx != playerParty.ActivePartyIndex)
        {
            playerParty.SetActivePartyIndex(idx);
        }
        CreatureInstance partyInstance = playerParty.ActiveCreatures[idx];
        if (partyInstance == null) return;

        if (playerCreature.Instance == partyInstance)
        {
            playerCreature.SyncInstanceRuntimeState();
            return;
        }

        CreatureDefinition def = CreatureRegistry.Get(partyInstance.definitionID);
        int maxHp = def != null
            ? Mathf.Max(1, CreatureInstanceFactory.ComputeMaxHP(def, partyInstance.soulTraits, Mathf.Max(1, partyInstance.level)))
            : Mathf.Max(1, playerCreature.maxHP);
        partyInstance.currentHP = Mathf.Clamp(playerCreature.currentHP, 0, maxHp);

        if (partyInstance.currentPP == null || partyInstance.currentPP.Length < 4)
        {
            partyInstance.currentPP = new int[4];
        }

        if (playerCreature.attacks != null)
        {
            for (int i = 0; i < Mathf.Min(playerCreature.attacks.Count, 4); i++)
            {
                AttackData atk = playerCreature.attacks[i];
                if (atk == null) continue;
                partyInstance.currentPP[i] = Mathf.Clamp(atk.currentPP, 0, atk.maxPP);
            }
        }
    }

    string TypesToString(CreatureType[] types)
    {
        if (types == null || types.Length == 0) return "None";
        List<string> labels = new List<string>(types.Length);
        for (int i = 0; i < types.Length; i++)
        {
            if (types[i] == CreatureType.None) continue;
            labels.Add(types[i].ToString());
        }
        if (labels.Count == 0) return "None";
        return string.Join(" / ", labels);
    }

    void SetActionMenuVisible(bool visible)
    {
        if (actionMenu != null)
        {
            actionMenu.SetActive(visible);
        }
    }

    void SetBackButtonVisible(bool visible)
    {
        if (backButton != null)
        {
            backButton.gameObject.SetActive(visible);
            backButton.interactable = visible;
            UpdateButtonVisualState(backButton);
        }
    }

    void OnBackPressed()
    {
        if (swapMenuOpen)
        {
            CloseSwapMenu(true);
            SetMessage("Choose an action.");
            return;
        }
        if (movePanel != null) movePanel.SetActive(false);
        SetActionMenuVisible(true);
        SetBackButtonVisible(false);
        SetMessage("Choose an action.");
        RefreshTurnInputState();
    }

    void ApplyEncounterLayout()
    {
        if (battleRoot == null) return;

        Image bg = battleRoot.GetComponent<Image>();
        if (bg == null)
        {
            bg = battleRoot.AddComponent<Image>();
        }
        if (bg != null)
        {
            bg.color = new Color(0f, 0f, 0f, 1f);
            bg.raycastTarget = false;
        }

        RectTransform rootRt = battleRoot.GetComponent<RectTransform>();
        if (rootRt != null)
        {
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            // Keep root unscaled (or slightly reduced) so anchored children never clip out of view.
            float zoom = Mathf.Clamp(battleUiZoom <= 0f ? 1f : battleUiZoom, 0.9f, 1f);
            rootRt.localScale = Vector3.one * zoom;
        }

        if (arenaPanel != null)
        {
            arenaPanel.anchorMin = new Vector2(0f, 0.28f);
            arenaPanel.anchorMax = new Vector2(1f, 1f);
            arenaPanel.offsetMin = Vector2.zero;
            arenaPanel.offsetMax = Vector2.zero;
            arenaPanel.localScale = Vector3.one;
            Image arenaBg = arenaPanel.GetComponent<Image>();
            if (arenaBg != null) arenaBg.raycastTarget = false;
        }

        if (uiPanel != null)
        {
            uiPanel.anchorMin = new Vector2(0f, 0f);
            uiPanel.anchorMax = new Vector2(1f, 1f);
            uiPanel.offsetMin = Vector2.zero;
            uiPanel.offsetMax = Vector2.zero;
            Image uiBg = uiPanel.GetComponent<Image>();
            if (uiBg != null)
            {
                uiBg.color = new Color(0f, 0f, 0f, 0f);
                uiBg.raycastTarget = false;
            }
            uiPanel.localScale = Vector3.one;
        }

        if (actionMenu != null)
        {
            Image actionBg = actionMenu.GetComponent<Image>();
            if (actionBg != null)
            {
                actionBg.color = new Color(1f, 1f, 1f, 0f);
                actionBg.sprite = null;
                actionBg.raycastTarget = false;
            }
            actionMenu.transform.localScale = Vector3.one;
        }
        if (movePanel != null)
        {
            Image moveBg = movePanel.GetComponent<Image>();
            if (moveBg != null)
            {
                moveBg.color = new Color(1f, 1f, 1f, 0f);
                moveBg.sprite = null;
                moveBg.raycastTarget = false;
            }
            movePanel.transform.localScale = Vector3.one;
        }

        RectTransform playerBar = FindRect(battleRoot.transform, "UIPanel/PlayerBar");
        RectTransform enemyBar = FindRect(battleRoot.transform, "UIPanel/EnemyBar");
        if (playerBar != null)
        {
            playerBar.anchorMin = new Vector2(0f, 1f);
            playerBar.anchorMax = new Vector2(0f, 1f);
            playerBar.pivot = new Vector2(0f, 1f);
            playerBar.anchoredPosition = new Vector2(20f, -20f);
            playerBar.sizeDelta = new Vector2(430f, 120f);
            Image barImage = playerBar.GetComponent<Image>();
            if (barImage != null) barImage.raycastTarget = false;
        }
        if (enemyBar != null)
        {
            enemyBar.anchorMin = new Vector2(1f, 1f);
            enemyBar.anchorMax = new Vector2(1f, 1f);
            enemyBar.pivot = new Vector2(1f, 1f);
            enemyBar.anchoredPosition = new Vector2(-20f, -20f);
            enemyBar.sizeDelta = new Vector2(430f, 120f);
            Image barImage = enemyBar.GetComponent<Image>();
            if (barImage != null) barImage.raycastTarget = false;
        }

        if (actionMenu != null)
        {
            RectTransform rt = actionMenu.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.17f);
                rt.anchorMax = new Vector2(0.5f, 0.17f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(980f, 250f);
            }
        }
        if (movePanel != null)
        {
            RectTransform rt = movePanel.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.17f);
                rt.anchorMax = new Vector2(0.5f, 0.17f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(980f, 250f);
            }
        }

        LayoutActionButtons();
        LayoutMoveButtons();
        EnsureMoveButtonLabels();
        EnsureButtonLabels();
        ApplyButtonSkins();

        SafeLayoutStep("EnsureBattleBackground", EnsureBattleBackground);
        SafeLayoutStep("EnsureSpriteImages", EnsureSpriteImages);
        SafeLayoutStep("EnsureSpriteShadows", EnsureSpriteShadows);
        SafeLayoutStep("EnsureBottomFrame", EnsureBottomFrame);
        SafeLayoutStep("EnsureBackButton", EnsureBackButton);
        SafeLayoutStep("EnsureEnemyHpText", EnsureEnemyHpText);
        SafeLayoutStep("EnsurePlayerXpBar", EnsurePlayerXpBar);
        SafeLayoutStep("EnsureMessagePresentation", EnsureMessagePresentation);
        RefreshTurnInputState();
    }

    void SafeLayoutStep(string stepName, System.Action action)
    {
        if (action == null) return;
        try
        {
            action.Invoke();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("BattleSystem layout step failed: " + stepName + " :: " + ex.Message);
        }
    }

    void EnsureBattleBackground()
    {
        if (battleRoot == null) return;
        if (battleBackgroundSprite == null && battleBackgroundTexture == null)
        {
            battleBackgroundTexture = Resources.Load<Texture2D>("UI/BattleSceneBackground");
        }
#if UNITY_EDITOR
        if (battleBackgroundSprite == null && battleBackgroundTexture == null)
        {
            Texture2D editorTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ChatGPT Image Mar 12, 2026, 08_41_45 PM.png");
            if (editorTex != null)
            {
                battleBackgroundTexture = editorTex;
            }
        }
#endif

        Transform existing = battleRoot.transform.Find("BattleBackground");
        if (existing == null)
        {
            GameObject go = new GameObject("BattleBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(battleRoot.transform, false);
            existing = go.transform;
            existing.SetAsFirstSibling();
        }

        battleBackgroundImage = existing.GetComponent<Image>();
        RectTransform rt = existing as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            // Lift the framing upward so the horizon sits slightly higher.
            rt.anchoredPosition = new Vector2(0f, 98f);
            rt.sizeDelta = new Vector2(0f, 168f);
        }

        if (battleBackgroundImage != null)
        {
            Sprite bgSprite = battleBackgroundSprite != null ? battleBackgroundSprite : CreateSprite(battleBackgroundTexture);
            if (bgSprite != null)
            {
                battleBackgroundImage.sprite = bgSprite;
                battleBackgroundImage.color = Color.white;
            }
            else
            {
                battleBackgroundImage.color = new Color(0.05f, 0.10f, 0.08f, 1f);
            }

            battleBackgroundImage.type = Image.Type.Simple;
            battleBackgroundImage.preserveAspect = false;
            battleBackgroundImage.raycastTarget = false;
        }
    }

    void EnsureBottomFrame()
    {
        if (uiPanel == null) return;
        if (bottomFrameTexture == null)
        {
            bottomFrameTexture = Resources.Load<Texture2D>("UI/BattleFrameMarker");
        }

        Transform existing = uiPanel.Find("BottomFrame");
        if (existing == null)
        {
            GameObject go = new GameObject("BottomFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(uiPanel, false);
            existing = go.transform;
            existing.SetAsFirstSibling();
        }

        bottomFrameImage = existing.GetComponent<Image>();
        RectTransform rt = existing as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0.34f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        if (bottomFrameImage != null)
        {
            if (bottomFrameTexture != null)
            {
                bottomFrameImage.sprite = Sprite.Create(
                    bottomFrameTexture,
                    new Rect(0, 0, bottomFrameTexture.width, bottomFrameTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f
                );
            }
            bottomFrameImage.color = Color.white;
            bottomFrameImage.type = Image.Type.Simple;
            bottomFrameImage.preserveAspect = false;
            bottomFrameImage.raycastTarget = false;
        }
    }

    void EnsureMoveButtonLabels()
    {
        for (int i = 0; i < moveButtons.Length; i++)
        {
            if (moveButtons[i] == null) continue;
            Transform t = moveButtons[i].transform.Find("Text");
            if (t == null)
            {
                GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textGo.transform.SetParent(moveButtons[i].transform, false);
                t = textGo.transform;
            }

            RectTransform rt = t as RectTransform;
            Text txt = t.GetComponent<Text>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
            }
            if (txt != null)
            {
                if (txt.font == null) txt.font = GetDefaultUIFont();
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.white;
                txt.fontSize = 26;
                txt.fontStyle = FontStyle.Bold;
                txt.raycastTarget = false;
                Outline o = txt.GetComponent<Outline>();
                if (o == null) o = txt.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.95f);
                o.effectDistance = new Vector2(2f, -2f);
            }
        }
    }

    void EnsureBackButton()
    {
        if (movePanel == null) return;
        Transform existing = movePanel.transform.Find("BackButton");
        if (existing == null)
        {
            GameObject go = new GameObject("BackButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(movePanel.transform, false);
            existing = go.transform;

            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(existing, false);
            Text t = textGo.GetComponent<Text>();
            t.text = "Back";
            if (t.font == null) t.font = GetDefaultUIFont();
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.fontSize = 20;
            t.fontStyle = FontStyle.Bold;
            RectTransform tr = textGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
        }

        backButton = existing.GetComponent<Button>();
        Image img = existing.GetComponent<Image>();
        RectTransform rtBtn = existing as RectTransform;
        if (img != null) img.color = Color.white;
        if (rtBtn != null)
        {
            rtBtn.anchorMin = new Vector2(0f, 0.5f);
            rtBtn.anchorMax = new Vector2(0f, 0.5f);
            rtBtn.pivot = new Vector2(1f, 0.5f);
            // Keep the back button left of the move buttons, matching prior placement intent.
            rtBtn.anchoredPosition = new Vector2(-235f, -70f);
            rtBtn.sizeDelta = new Vector2(220f, 86f);
        }

        if (backButton != null)
        {
            ConfigureButtonLabel(backButton, "BACK");
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(OnBackPressed);
            backButton.transform.SetAsLastSibling();
            Text backLabel = backButton.GetComponentInChildren<Text>(true);
            if (backLabel != null)
            {
                backLabel.text = "BACK";
                backLabel.color = new Color(0.05f, 0.05f, 0.06f, 1f);
                backLabel.fontSize = 38;
                Outline o = backLabel.GetComponent<Outline>();
                if (o == null) o = backLabel.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(1f, 1f, 1f, 0.82f);
                o.effectDistance = new Vector2(1.5f, -1.5f);
            }
            bool shouldBeVisible = inBattle && movePanel.activeSelf;
            backButton.gameObject.SetActive(shouldBeVisible);
            backButton.interactable = shouldBeVisible;
            UpdateButtonVisualState(backButton);
        }
    }

    void EnsureEnemyHpText()
    {
        if (battleRoot == null) return;

        if (enemyHpText == null)
        {
            Transform rootTf = battleRoot.transform;
            enemyHpText = FindText(rootTf, "UIPanel/EnemyBar/EnemyHpText");
            enemyHpText = enemyHpText ?? FindText(rootTf, "UIPanel/EnemyBar/EnemyHpBG/EnemyHpText");
        }

        if (enemyHpText == null && enemyHpBg != null)
        {
            GameObject go = new GameObject("EnemyHpText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            Transform parent = enemyHpBg.transform.parent != null ? enemyHpBg.transform.parent : enemyHpBg.transform;
            go.transform.SetParent(parent, false);
            enemyHpText = go.GetComponent<Text>();
        }

        if (enemyHpText == null) return;
        if (enemyHpBg != null)
        {
            Transform desiredParent = enemyHpBg.transform.parent != null ? enemyHpBg.transform.parent : enemyHpBg.transform;
            if (enemyHpText.transform.parent != desiredParent)
            {
                enemyHpText.transform.SetParent(desiredParent, false);
            }
        }

        RectTransform rt = enemyHpText.rectTransform;
        RectTransform playerRt = playerHpText != null ? playerHpText.rectTransform : null;
        if (playerHpText != null)
        {
            enemyHpText.font = playerHpText.font != null ? playerHpText.font : GetDefaultUIFont();
            enemyHpText.alignment = playerHpText.alignment;
            enemyHpText.fontSize = playerHpText.fontSize;
            enemyHpText.fontStyle = playerHpText.fontStyle;
            enemyHpText.color = playerHpText.color;
            enemyHpText.raycastTarget = playerHpText.raycastTarget;
            enemyHpText.horizontalOverflow = playerHpText.horizontalOverflow;
            enemyHpText.verticalOverflow = playerHpText.verticalOverflow;
            enemyHpText.lineSpacing = playerHpText.lineSpacing;
            enemyHpText.supportRichText = playerHpText.supportRichText;

            if (playerRt != null)
            {
                rt.anchorMin = playerRt.anchorMin;
                rt.anchorMax = playerRt.anchorMax;
                rt.pivot = playerRt.pivot;
                rt.anchoredPosition = playerRt.anchoredPosition;
                rt.sizeDelta = playerRt.sizeDelta;
            }
        }
        else
        {
            if (enemyHpText.font == null) enemyHpText.font = GetDefaultUIFont();
            enemyHpText.alignment = TextAnchor.UpperLeft;
            enemyHpText.fontSize = 16;
            enemyHpText.fontStyle = FontStyle.Normal;
            enemyHpText.color = Color.white;
            enemyHpText.raycastTarget = true;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(280f, 10f);
            rt.sizeDelta = new Vector2(120f, 20f);
        }

        Outline playerOutline = playerHpText != null ? playerHpText.GetComponent<Outline>() : null;
        Outline enemyOutline = enemyHpText.GetComponent<Outline>();
        if (playerOutline != null)
        {
            if (enemyOutline == null) enemyOutline = enemyHpText.gameObject.AddComponent<Outline>();
            enemyOutline.effectColor = playerOutline.effectColor;
            enemyOutline.effectDistance = playerOutline.effectDistance;
            enemyOutline.useGraphicAlpha = playerOutline.useGraphicAlpha;
        }
        else if (enemyOutline != null)
        {
            if (Application.isPlaying) Destroy(enemyOutline);
            else DestroyImmediate(enemyOutline);
        }

        enemyHpText.transform.SetAsLastSibling();
    }

    void EnsurePlayerXpBar()
    {
        if (battleRoot == null) return;
        RectTransform playerBar = FindRect(battleRoot.transform, "UIPanel/PlayerBar");
        if (playerBar == null) return;

        Transform bgTf = playerBar.Find("PlayerXpBG");
        if (bgTf == null)
        {
            GameObject go = new GameObject("PlayerXpBG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(playerBar, false);
            bgTf = go.transform;
        }

        playerXpBg = bgTf.GetComponent<Image>();
        RectTransform bgRt = playerXpBg.rectTransform;
        bool laidOutFromHp = false;
        if (playerHpBg != null && playerHpBg.rectTransform != null)
        {
            RectTransform hpRt = playerHpBg.rectTransform;
            Vector3[] hpCorners = new Vector3[4];
            hpRt.GetWorldCorners(hpCorners);
            Vector3 hpBottomLeft = playerBar.InverseTransformPoint(hpCorners[0]);
            Vector3 hpTopRight = playerBar.InverseTransformPoint(hpCorners[2]);

            float hpWidth = Mathf.Max(8f, hpTopRight.x - hpBottomLeft.x);
            float hpHeight = Mathf.Max(4f, hpTopRight.y - hpBottomLeft.y);
            float xpHeight = Mathf.Clamp(hpHeight * 0.45f, 3f, hpHeight);
            float xpTop = hpBottomLeft.y - 2f;
            float xpBottom = xpTop - xpHeight;

            bgRt.anchorMin = new Vector2(0.5f, 0.5f);
            bgRt.anchorMax = new Vector2(0.5f, 0.5f);
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.anchoredPosition = new Vector2(hpBottomLeft.x + hpWidth * 0.5f, xpBottom + xpHeight * 0.5f);
            bgRt.sizeDelta = new Vector2(hpWidth, xpHeight);
            laidOutFromHp = true;
        }

        if (!laidOutFromHp)
        {
            bgRt.anchorMin = new Vector2(0.08f, 0.07f);
            bgRt.anchorMax = new Vector2(0.88f, 0.14f);
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
        }
        playerXpBg.raycastTarget = false;
        playerXpBg.sprite = GetNeutralFillSprite();
        playerXpBg.type = Image.Type.Simple;
        playerXpBg.preserveAspect = false;
        playerXpBg.color = new Color(0f, 0f, 0f, 0.94f);

        Transform fillTf = bgTf.Find("PlayerXpFill");
        if (fillTf == null)
        {
            GameObject go = new GameObject("PlayerXpFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(bgTf, false);
            fillTf = go.transform;
        }

        playerXpFill = fillTf.GetComponent<Image>();
        RectTransform fillRt = playerXpFill.rectTransform;
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(1f, 1f);
        fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        playerXpFill.raycastTarget = false;
        playerXpFill.sprite = GetNeutralFillSprite();
        playerXpFill.type = Image.Type.Filled;
        playerXpFill.fillMethod = Image.FillMethod.Horizontal;
        playerXpFill.fillOrigin = 0;
        playerXpFill.preserveAspect = false;
        playerXpFill.material = null;
        playerXpFill.color = new Color(0.28f, 0.75f, 1f, 1f);
    }

    void EnsureSpriteShadows()
    {
        if (arenaPanel == null) return;

        if (shadowEllipseSprite == null)
        {
            shadowEllipseSprite = CreateShadowEllipseSprite();
        }

        if (playerShadowImage == null)
        {
            Transform existing = arenaPanel.Find("PlayerCreatureShadow");
            if (existing == null)
            {
                GameObject go = new GameObject("PlayerCreatureShadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(arenaPanel, false);
                existing = go.transform;
            }
            playerShadowImage = existing.GetComponent<Image>();
        }

        if (enemyShadowImage == null)
        {
            Transform existing = arenaPanel.Find("EnemyCreatureShadow");
            if (existing == null)
            {
                GameObject go = new GameObject("EnemyCreatureShadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(arenaPanel, false);
                existing = go.transform;
            }
            enemyShadowImage = existing.GetComponent<Image>();
        }

        ConfigureShadowRect(playerShadowImage, new Vector2(0.14f, -0.06f), new Vector2(250f, 84f));
        ConfigureShadowRect(enemyShadowImage, new Vector2(0.86f, -0.06f), new Vector2(250f, 84f));
    }

    void ConfigureShadowRect(Image shadow, Vector2 anchor, Vector2 size)
    {
        if (shadow == null) return;
        RectTransform rt = shadow.rectTransform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        shadow.sprite = shadowEllipseSprite;
        shadow.color = new Color(0f, 0f, 0f, 0.42f);
        shadow.raycastTarget = false;
        shadow.preserveAspect = false;
        shadow.enabled = true;
        shadow.transform.SetAsFirstSibling();
    }

    Sprite CreateShadowEllipseSprite()
    {
        const int w = 128;
        const int h = 48;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((w - 1) * 0.5f, (h - 1) * 0.5f);
        float rx = w * 0.48f;
        float ry = h * 0.46f;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float nx = (x - center.x) / rx;
                float ny = (y - center.y) / ry;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float a = Mathf.Clamp01(1f - d);
                a = Mathf.Pow(a, 1.8f) * 0.95f;
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    void EnsureSpriteImages()
    {
        if (arenaPanel == null) return;

        if (playerSpriteImage == null)
        {
            Transform existing = arenaPanel.Find("PlayerCreatureImage");
            if (existing == null)
            {
                GameObject go = new GameObject("PlayerCreatureImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(arenaPanel, false);
                existing = go.transform;
            }
            playerSpriteImage = existing.GetComponent<Image>();
        }

        if (enemySpriteImage == null)
        {
            Transform existing = arenaPanel.Find("EnemyCreatureImage");
            if (existing == null)
            {
                GameObject go = new GameObject("EnemyCreatureImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(arenaPanel, false);
                existing = go.transform;
            }
            enemySpriteImage = existing.GetComponent<Image>();
        }

        if (playerSpriteImage != null)
        {
            RectTransform rt = playerSpriteImage.rectTransform;
            rt.anchorMin = new Vector2(0.14f, -0.04f);
            rt.anchorMax = new Vector2(0.14f, -0.04f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(560f, 560f);
            playerSpriteImage.color = Color.clear;
            playerSpriteImage.raycastTarget = false;
            playerSpriteImage.preserveAspect = true;
            playerSpriteImage.enabled = true;
            playerSpriteImage.transform.SetAsLastSibling();
            playerSpriteBaseLocalPos = rt.localPosition;
        }

        if (enemySpriteImage != null)
        {
            RectTransform rt = enemySpriteImage.rectTransform;
            rt.anchorMin = new Vector2(0.86f, -0.04f);
            rt.anchorMax = new Vector2(0.86f, -0.04f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(560f, 560f);
            enemySpriteImage.color = Color.clear;
            enemySpriteImage.raycastTarget = false;
            enemySpriteImage.preserveAspect = true;
            enemySpriteImage.enabled = true;
            enemySpriteImage.transform.SetAsLastSibling();
            enemySpriteBaseLocalPos = rt.localPosition;
        }
    }

    void LayoutActionButtons()
    {
        LayoutButtonRect(attackButton, new Vector2(-270f, 70f), new Vector2(500f, 120f));
        LayoutButtonRect(swapButton, new Vector2(270f, 70f), new Vector2(500f, 120f));
        LayoutButtonRect(captureButton, new Vector2(-270f, -70f), new Vector2(500f, 120f));
        LayoutButtonRect(runButton, new Vector2(270f, -70f), new Vector2(500f, 120f));
    }

    void LayoutMoveButtons()
    {
        LayoutButtonRect(moveButtons[0], new Vector2(-270f, 70f), new Vector2(510f, 122f));
        LayoutButtonRect(moveButtons[1], new Vector2(270f, 70f), new Vector2(510f, 122f));
        LayoutButtonRect(moveButtons[2], new Vector2(-270f, -70f), new Vector2(510f, 122f));
        LayoutButtonRect(moveButtons[3], new Vector2(270f, -70f), new Vector2(510f, 122f));
    }

    void LayoutButtonRect(Button button, Vector2 pos, Vector2 size)
    {
        if (button == null) return;
        RectTransform rt = button.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    Image GetSpriteImageForCombatant(CreatureCombatant combatant)
    {
        if (combatant == null) return null;
        if (playerCreature == combatant) return playerSpriteImage;
        if (enemyCreature == combatant) return enemySpriteImage;
        return null;
    }

    IEnumerator AnimateAttack(Image attacker, Image defender, bool didHit)
    {
        if (attacker == null || defender == null)
        {
            yield return new WaitForSeconds(0.1f);
            yield break;
        }

        if (!activeAttackAnimations.Add(attacker))
        {
            yield break;
        }

        RectTransform attackerRt = attacker.rectTransform;
        RectTransform defenderRt = defender.rectTransform;

        Vector3 start = attackerRt.localPosition;
        Vector3 target = defenderRt.localPosition;
        Vector3 dir = (target - start).normalized;
        float dashDistance = Mathf.Clamp(Vector3.Distance(start, target) * 0.32f, 110f, 230f);
        Vector3 dashPos = start + (dir * dashDistance);

        float dashTime = 0.12f;
        float t = 0f;
        while (t < dashTime)
        {
            t += Time.deltaTime;
            attackerRt.localPosition = Vector3.Lerp(start, dashPos, Mathf.Clamp01(t / dashTime));
            yield return null;
        }
        attackerRt.localPosition = dashPos;

        if (didHit)
        {
            yield return StartCoroutine(AnimateHitShake(defenderRt, 0.14f, 10f));
        }
        else
        {
            yield return new WaitForSeconds(0.05f);
        }

        float returnTime = 0.14f;
        t = 0f;
        while (t < returnTime)
        {
            t += Time.deltaTime;
            attackerRt.localPosition = Vector3.Lerp(dashPos, start, Mathf.Clamp01(t / returnTime));
            yield return null;
        }
        attackerRt.localPosition = start;
        activeAttackAnimations.Remove(attacker);

        if (attacker == playerSpriteImage)
        {
            playerSpriteBaseLocalPos = attackerRt.localPosition;
        }
        else if (attacker == enemySpriteImage)
        {
            enemySpriteBaseLocalPos = attackerRt.localPosition;
        }
    }

    IEnumerator AnimateHitShake(RectTransform targetRt, float duration, float strength)
    {
        Vector2 start = targetRt.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float x = Mathf.Sin(elapsed * 85f) * strength;
            targetRt.anchoredPosition = start + new Vector2(x, 0f);
            yield return null;
        }
        targetRt.anchoredPosition = start;
    }

    IEnumerator AnimateFaintShrink(CreatureCombatant faintedCombatant)
    {
        Image image = GetSpriteImageForCombatant(faintedCombatant);
        if (image == null || image.rectTransform == null) yield break;

        bool isPlayer = faintedCombatant != null && faintedCombatant == playerCreature;
        if (isPlayer) playerFaintedVisualLocked = true;
        else enemyFaintedVisualLocked = true;

        RectTransform rt = image.rectTransform;
        Vector3 startScale = rt.localScale;
        Vector3 startPos = rt.localPosition;
        float pivotToFeet = Mathf.Abs(startScale.y) * rt.rect.height * Mathf.Clamp01(rt.pivot.y);

        Image shadow = isPlayer ? playerShadowImage : enemyShadowImage;
        Color shadowStart = shadow != null ? shadow.color : Color.clear;

        const float duration = 0.24f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - p, 3f);
            float scaleFactor = Mathf.Clamp01(1f - eased);

            rt.localScale = new Vector3(startScale.x * scaleFactor, startScale.y * scaleFactor, startScale.z);
            rt.localPosition = startPos + new Vector3(0f, -pivotToFeet * (1f - scaleFactor), 0f);

            if (shadow != null)
            {
                Color c = shadowStart;
                c.a = shadowStart.a * scaleFactor;
                shadow.color = c;
            }

            yield return null;
        }

        rt.localScale = new Vector3(0f, 0f, startScale.z);
        rt.localPosition = startPos + new Vector3(0f, -pivotToFeet, 0f);
        if (shadow != null)
        {
            shadow.color = new Color(shadowStart.r, shadowStart.g, shadowStart.b, 0f);
            shadow.enabled = false;
        }
    }

    void UpdateCreatureSprites()
    {
        Sprite playerSprite = ResolveCombatantSprite(playerCreature, true);
        Sprite enemySprite = ResolveCombatantSprite(enemyCreature, false);

        if (playerSpriteImage != null && playerCreature != null)
        {
            if (playerSprite != null)
            {
                playerSpriteImage.sprite = playerSprite;
                playerSpriteImage.preserveAspect = true;
                float scale = ResolveBattleSpriteScale(playerCreature, playerSprite);
                playerSpriteBaseLocalScale = new Vector3(-scale, scale, 1f);
                if (!playerFaintedVisualLocked)
                {
                    playerSpriteImage.rectTransform.localScale = playerSpriteBaseLocalScale;
                }
                playerSpriteImage.color = Color.white;
            }
            else
            {
                playerSpriteImage.sprite = null;
                playerSpriteImage.color = Color.clear;
                playerSpriteBaseLocalScale = Vector3.one;
                playerFaintedVisualLocked = false;
            }
        }

        if (enemySpriteImage != null && enemyCreature != null)
        {
            if (enemySprite != null)
            {
                enemySpriteImage.sprite = enemySprite;
                enemySpriteImage.preserveAspect = true;
                float scale = ResolveBattleSpriteScale(enemyCreature, enemySprite);
                enemySpriteBaseLocalScale = new Vector3(scale, scale, 1f);
                if (!enemyFaintedVisualLocked)
                {
                    enemySpriteImage.rectTransform.localScale = enemySpriteBaseLocalScale;
                }
                enemySpriteImage.color = Color.white;
            }
            else
            {
                enemySpriteImage.sprite = null;
                enemySpriteImage.color = Color.clear;
                enemySpriteBaseLocalScale = Vector3.one;
                enemyFaintedVisualLocked = false;
            }
        }
    }

    Sprite ResolveCombatantSprite(CreatureCombatant combatant, bool isPlayer)
    {
        if (combatant == null) return null;

        if (combatant.Definition != null && combatant.Definition.sprite != null)
        {
            return combatant.Definition.sprite;
        }

        SpriteRenderer sr = combatant.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = combatant.GetComponentInChildren<SpriteRenderer>(true);
        }
        if (sr != null && sr.sprite != null)
        {
            return sr.sprite;
        }

        if (!isPlayer && currentEnemyAI != null)
        {
            SpriteRenderer aiSr = currentEnemyAI.GetComponent<SpriteRenderer>();
            if (aiSr == null) aiSr = currentEnemyAI.GetComponentInChildren<SpriteRenderer>(true);
            if (aiSr != null && aiSr.sprite != null) return aiSr.sprite;
        }

        string creatureId = ResolveCreatureID(combatant.gameObject, combatant);
        return TryLoadCreatureSprite(creatureId);
    }

    float ResolveBattleSpriteScale(CreatureCombatant combatant, Sprite sprite)
    {
        if (sprite == null) return 1f;

        CreatureDefinition def = combatant != null ? combatant.Definition : null;
        float stageFactor = 1f;
        float definitionScale = 1f;
        if (def != null)
        {
            stageFactor = 1f + (Mathf.Clamp(def.evolutionStage, 1, 4) - 1) * 0.12f;
            definitionScale = Mathf.Max(0.05f, def.battleSizeMultiplier);
        }

        float ppu = Mathf.Max(1f, sprite.pixelsPerUnit);
        float nativeHeight = sprite.rect.height / ppu;
        float nativeFactor = Mathf.Clamp(nativeHeight / 2.2f, 0.72f, 1.70f);

        return Mathf.Clamp(nativeFactor * stageFactor * definitionScale, 0.68f, 2.05f);
    }

    void EnsureMessagePresentation()
    {
        if (messageText == null) return;
        if (messageText.font == null) messageText.font = GetDefaultUIFont();

        messageText.alignment = TextAnchor.MiddleCenter;
        messageText.fontSize = 40;
        messageText.fontStyle = FontStyle.Bold;
        messageText.color = Color.white;
        messageText.raycastTarget = false;
        messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
        messageText.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rt = messageText.rectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -24f);
            rt.sizeDelta = new Vector2(980f, 96f);
        }

        Outline outline = messageText.GetComponent<Outline>();
        if (outline == null) outline = messageText.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    Font GetDefaultUIFont()
    {
        if (cachedDefaultFont != null) return cachedDefaultFont;
        // Unity versions differ on built-in font availability; resolve safely.
        cachedDefaultFont = TryGetBuiltinFont("LegacyRuntime.ttf");
        if (cachedDefaultFont == null)
        {
            cachedDefaultFont = TryGetBuiltinFont("Arial.ttf");
        }
        if (cachedDefaultFont == null)
        {
            cachedDefaultFont = FindFirstObjectByType<Font>();
        }
        return cachedDefaultFont;
    }

    Font TryGetBuiltinFont(string resourceName)
    {
        if (string.IsNullOrEmpty(resourceName)) return null;
        try
        {
            return Resources.GetBuiltinResource<Font>(resourceName);
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    void EnsureBattleRuntimeState()
    {
        if (!inBattle) return;
        AutoFindUI();
        if (battleRoot == null) return;

        // If any external script deactivates UI during battle, force it back on.
        bool reactivatedHierarchy = false;
        Transform p = battleRoot.transform.parent;
        while (p != null)
        {
            if (!p.gameObject.activeSelf)
            {
                p.gameObject.SetActive(true);
                reactivatedHierarchy = true;
            }
            p = p.parent;
        }

        if (!battleRoot.activeSelf)
        {
            battleRoot.SetActive(true);
            reactivatedHierarchy = true;
        }

        Vector2Int currentScreenSize = new Vector2Int(Screen.width, Screen.height);
        bool screenSizeChanged = currentScreenSize != lastLayoutScreenSize;
        if (!runtimeLayoutInitialized || screenSizeChanged || reactivatedHierarchy)
        {
            ApplyEncounterLayout();
            HookButtons();
            runtimeLayoutInitialized = true;
            lastLayoutScreenSize = currentScreenSize;
        }

        if (!IsValidCombatant(playerCreature))
        {
            playerCreature = ResolvePlayerCombatant();
        }
        if (!IsValidCombatant(enemyCreature))
        {
            enemyCreature = ResolveEnemyCombatant(currentEnemyAI);
        }

        EnsureButtonLabels();
        EnsureBackButton();
        EnsureSwapMenu();
        if (swapMenuOpen)
        {
            if (swapMenuRoot != null)
            {
                swapMenuRoot.SetActive(true);
                swapMenuRoot.transform.SetAsLastSibling();
            }
            RefreshSwapMenuCards();
            SetActionMenuVisible(false);
            SetBackButtonVisible(false);
        }
        ApplyButtonSkins();
        ApplyBarSprites();
        UpdateCreatureSprites();
        ApplyCreatureIdleAnimation();
        UpdateUI();

        if (currentEnemyAI != null && !currentEnemyAI.IsInBattle())
        {
            currentEnemyAI.EnterBattle();
            currentEnemyAI.ForceStop();
        }

        if (playerMover != null && playerMover.enabled)
        {
            playerMover.enabled = false;
        }

        RefreshTurnInputState();
    }

    void RefreshTurnInputState()
    {
        bool allowInput = inBattle && waitingForPlayerMove && !swapMenuOpen;
        bool hasPlayerCreature = IsValidCombatant(playerCreature);

        if (attackButton != null) attackButton.interactable = allowInput && hasPlayerCreature;
        if (swapButton != null) swapButton.interactable = allowInput;
        if (captureButton != null) captureButton.interactable = allowInput;
        if (runButton != null) runButton.interactable = allowInput;
        UpdateButtonVisualState(attackButton);
        UpdateButtonVisualState(swapButton);
        UpdateButtonVisualState(captureButton);
        UpdateButtonVisualState(runButton);

        if (moveButtons != null)
        {
            for (int i = 0; i < moveButtons.Length; i++)
            {
                if (moveButtons[i] == null) continue;
                if (playerCreature == null)
                {
                    moveButtons[i].interactable = false;
                    UpdateButtonVisualState(moveButtons[i]);
                    continue;
                }

                int unlockLevel = 1 + i * 5;
                bool unlocked = playerCreature.level >= unlockLevel;
                moveButtons[i].interactable = allowInput && unlocked;
                UpdateButtonVisualState(moveButtons[i]);
            }
        }

        if (backButton != null)
        {
            bool canBack = inBattle && !swapMenuOpen && movePanel != null && movePanel.activeSelf;
            backButton.interactable = canBack;
            UpdateButtonVisualState(backButton);
        }
    }

    void UpdateButtonVisualState(Button button)
    {
        if (button == null) return;
        int moveButtonIndex = ResolveMoveButtonIndex(button);
        Color buttonFaceColor = Color.white;
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            if (!button.interactable)
            {
                buttonFaceColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            }
            else if (moveButtonIndex >= 0)
            {
                buttonFaceColor = ResolveMoveButtonFaceColor(moveButtonIndex);
            }
            else
            {
                buttonFaceColor = Color.white;
            }

            image.color = buttonFaceColor;
        }

        Text[] labels = button.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            Text label = labels[i];
            if (label == null) continue;
            if (button == backButton)
            {
                label.color = button.interactable
                    ? new Color(0.08f, 0.08f, 0.10f, 1f)
                    : new Color(0.40f, 0.40f, 0.42f, 1f);
            }
            else
            {
                if (button.interactable && moveButtonIndex >= 0)
                {
                    label.color = ResolveReadableButtonTextColor(buttonFaceColor);
                }
                else
                {
                    label.color = button.interactable
                        ? Color.white
                        : new Color(0.82f, 0.82f, 0.82f, 1f);
                }
            }
        }
    }

    int ResolveMoveButtonIndex(Button button)
    {
        if (button == null || moveButtons == null) return -1;
        for (int i = 0; i < moveButtons.Length; i++)
        {
            if (moveButtons[i] == button) return i;
        }
        return -1;
    }

    Color ResolveMoveButtonFaceColor(int moveButtonIndex)
    {
        if (playerCreature == null || playerCreature.attacks == null) return TypeNormal;
        if (moveButtonIndex < 0 || moveButtonIndex >= playerCreature.attacks.Count) return TypeNormal;
        AttackData atk = playerCreature.attacks[moveButtonIndex];
        if (atk == null) return TypeNormal;
        return ResolveTypeButtonColor(atk.type);
    }

    Color ResolveTypeButtonColor(CreatureType type)
    {
        switch (type)
        {
            case CreatureType.Fire: return TypeFire;
            case CreatureType.Water: return TypeWater;
            case CreatureType.Lightning: return TypeLightning;
            case CreatureType.Earth: return TypeEarth;
            case CreatureType.Nature: return TypeNature;
            case CreatureType.Ice: return TypeIce;
            case CreatureType.Dragon: return TypeDragon;
            case CreatureType.Light: return TypeLight;
            case CreatureType.Dark: return TypeDark;
            case CreatureType.Normal:
            case CreatureType.None:
            default:
                return TypeNormal;
        }
    }

    Color ResolveReadableButtonTextColor(Color background)
    {
        float luminance = (0.299f * background.r) + (0.587f * background.g) + (0.114f * background.b);
        return luminance >= 0.62f
            ? new Color(0.08f, 0.08f, 0.10f, 1f)
            : Color.white;
    }

    void ApplyCreatureIdleAnimation()
    {
        float t = Time.unscaledTime;

        if (playerSpriteImage != null && playerSpriteImage.sprite != null &&
            !activeAttackAnimations.Contains(playerSpriteImage) && !playerFaintedVisualLocked)
        {
            float breathe = (Mathf.Sin(t * 1.9f + 0.15f) + 1f) * 0.5f;
            float ySquash = 1f - (breathe * 0.05f);
            float xStretch = 1f + (breathe * 0.025f);
            playerSpriteImage.rectTransform.localPosition = playerSpriteBaseLocalPos;
            Vector3 baseScale = playerSpriteBaseLocalScale;
            playerSpriteImage.rectTransform.localScale = new Vector3(baseScale.x * xStretch, baseScale.y * ySquash, baseScale.z);
            if (playerShadowImage != null)
            {
                float shadowPulse = 1f + (breathe * 0.05f);
                playerShadowImage.rectTransform.localScale = new Vector3(shadowPulse, 1f, 1f);
            }
        }

        if (enemySpriteImage != null && enemySpriteImage.sprite != null &&
            !activeAttackAnimations.Contains(enemySpriteImage) && !enemyFaintedVisualLocked)
        {
            float breathe = (Mathf.Sin(t * 1.9f + 1.1f) + 1f) * 0.5f;
            float ySquash = 1f - (breathe * 0.05f);
            float xStretch = 1f + (breathe * 0.025f);
            enemySpriteImage.rectTransform.localPosition = enemySpriteBaseLocalPos;
            Vector3 baseScale = enemySpriteBaseLocalScale;
            enemySpriteImage.rectTransform.localScale = new Vector3(baseScale.x * xStretch, baseScale.y * ySquash, baseScale.z);
            if (enemyShadowImage != null)
            {
                float shadowPulse = 1f + (breathe * 0.05f);
                enemyShadowImage.rectTransform.localScale = new Vector3(shadowPulse, 1f, 1f);
            }
        }
    }

    void SetMessage(string msg)
    {
        if (messageText != null) messageText.text = msg;
    }

    void ConfigureCombatantByCreatureID(CreatureCombatant combatant, string creatureID, int level, bool resetHpToFull)
    {
        if (combatant == null) return;

        int lvl = Mathf.Max(1, level);
        string id = CanonicalizeCreatureID(creatureID);
        if (string.IsNullOrWhiteSpace(id)) id = "whelpling";

        if (CreatureRegistry.TryGet(id, out CreatureDefinition def))
        {
            CreatureInstance inst = combatant.Instance;
            bool needsNewInstance = inst == null;
            if (!needsNewInstance)
            {
                string instId = CanonicalizeCreatureID(inst.definitionID);
                needsNewInstance = string.IsNullOrWhiteSpace(instId) || instId != CanonicalizeCreatureID(def.creatureID);
            }

            if (needsNewInstance)
            {
                inst = CreatureInstanceFactory.CreateWild(def, lvl);
            }
            else
            {
                inst.level = lvl;
            }

            if (inst != null)
            {
                int maxHp = CreatureInstanceFactory.ComputeMaxHP(def, inst.soulTraits, inst.level);
                if (resetHpToFull)
                {
                    inst.currentHP = Mathf.Max(1, maxHp);
                    CreatureInstanceFactory.RefillPP(def, inst);
                }
                else
                {
                    inst.currentHP = Mathf.Clamp(inst.currentHP, 0, Mathf.Max(1, maxHp));
                }
            }

            combatant.InitFromDefinition(def, inst);
            if (resetHpToFull)
            {
                combatant.currentHP = combatant.maxHP;
                combatant.SyncInstanceRuntimeState();
            }
            return;
        }

        combatant.autoInitWhelpling = false;
        combatant.InitWhelpling(lvl);
        combatant.creatureName = ToDisplayName(creatureID, "Whelpling");
        if (resetHpToFull || combatant.currentHP > combatant.maxHP)
        {
            combatant.currentHP = combatant.maxHP;
        }
    }

    string ResolveCreatureID(GameObject go, CreatureCombatant fallbackCombatant)
    {
        if (fallbackCombatant != null && fallbackCombatant.Definition != null)
        {
            return CanonicalizeCreatureID(fallbackCombatant.Definition.creatureID);
        }

        if (go != null)
        {
            WorldSpawnMarker marker = go.GetComponent<WorldSpawnMarker>();
            if (marker == null) marker = go.GetComponentInParent<WorldSpawnMarker>();
            if (marker != null && !string.IsNullOrWhiteSpace(marker.creatureID))
            {
                return CanonicalizeCreatureID(marker.creatureID);
            }
        }

        string visualId = TryInferCreatureIDFromVisual(go);
        if (!string.IsNullOrWhiteSpace(visualId))
        {
            return CanonicalizeCreatureID(visualId);
        }

        if (go != null)
        {
            string n = go.name;
            if (!string.IsNullOrWhiteSpace(n))
            {
                n = n
                    .Replace("Wild_", "")
                    .Replace("EncounterEnemy_", "")
                    .Replace("(Clone)", "")
                    .Trim();
                if (!string.IsNullOrWhiteSpace(n))
                {
                    return CanonicalizeCreatureID(n);
                }
            }
        }

        if (fallbackCombatant != null && !string.IsNullOrWhiteSpace(fallbackCombatant.creatureName))
        {
            return CanonicalizeCreatureID(fallbackCombatant.creatureName);
        }

        return "whelpling";
    }

    string NormalizeCreatureID(string id)
    {
        return CreatureRegistry.NormalizeKey(id, keepUnderscore: false);
    }

    string CanonicalizeCreatureID(string id)
    {
        string canonical = CreatureRegistry.CanonicalizeCreatureID(id);
        if (string.IsNullOrWhiteSpace(canonical))
        {
            return "whelpling";
        }
        return canonical;
    }

    string TryInferCreatureIDFromVisual(GameObject go)
    {
        if (go == null) return string.Empty;

        SpriteFromTexture sft = go.GetComponent<SpriteFromTexture>();
        if (sft == null) sft = go.GetComponentInChildren<SpriteFromTexture>(true);
        if (sft != null && sft.texture != null && !string.IsNullOrWhiteSpace(sft.texture.name))
        {
            return sft.texture.name;
        }

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.GetComponentInChildren<SpriteRenderer>(true);
        if (sr != null && sr.sprite != null)
        {
            if (!string.IsNullOrWhiteSpace(sr.sprite.name))
            {
                return sr.sprite.name;
            }
            if (sr.sprite.texture != null && !string.IsNullOrWhiteSpace(sr.sprite.texture.name))
            {
                return sr.sprite.texture.name;
            }
        }

        return string.Empty;
    }

    string ToDisplayName(string id, string fallback)
    {
        if (string.IsNullOrWhiteSpace(id)) return fallback;
        string cleaned = id.Trim().Replace("_", " ");
        if (cleaned.Length == 0) return fallback;
        if (cleaned.Length == 1) return cleaned.ToUpperInvariant();
        return char.ToUpperInvariant(cleaned[0]) + cleaned.Substring(1);
    }

    Sprite TryLoadCreatureSprite(string creatureID)
    {
        string key = CanonicalizeCreatureID(creatureID);
        if (string.IsNullOrEmpty(key)) key = "whelpling";
        string normalizedKey = NormalizeCreatureID(key);
        if (creatureSpriteCache.TryGetValue(key, out Sprite cached))
        {
            return cached;
        }

        string[] names = BuildCreatureNameCandidates(key);
        for (int i = 0; i < names.Length; i++)
        {
            string n = names[i];
            Sprite s = Resources.Load<Sprite>("Creatures/" + n);
            if (s != null)
            {
                creatureSpriteCache[key] = s;
                return s;
            }
        }

        Sprite[] allSprites = Resources.LoadAll<Sprite>("Creatures");
        for (int i = 0; i < allSprites.Length; i++)
        {
            Sprite s = allSprites[i];
            if (s == null) continue;
            string spriteKey = NormalizeCreatureID(s.name);
            if (spriteKey == normalizedKey)
            {
                creatureSpriteCache[key] = s;
                return s;
            }
            if (s.texture != null && NormalizeCreatureID(s.texture.name) == normalizedKey)
            {
                creatureSpriteCache[key] = s;
                return s;
            }
        }

#if UNITY_EDITOR
        string[] roots = { "Assets/Creatures", "Assets/Resources/Creatures" };
        for (int r = 0; r < roots.Length; r++)
        {
            if (!AssetDatabase.IsValidFolder(roots[r])) continue;
            string[] spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { roots[r] });
            for (int i = 0; i < spriteGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(spriteGuids[i]);
                if (string.IsNullOrWhiteSpace(path)) continue;
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (NormalizeCreatureID(fileName) != normalizedKey) continue;

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                {
                    creatureSpriteCache[key] = sprite;
                    return sprite;
                }
            }

            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { roots[r] });
            for (int i = 0; i < textureGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                if (string.IsNullOrWhiteSpace(path)) continue;
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (NormalizeCreatureID(fileName) != normalizedKey) continue;

                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                {
                    Sprite runtime = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 128f);
                    runtime.name = fileName;
                    creatureSpriteCache[key] = runtime;
                    return runtime;
                }
            }
        }
#endif
        return null;
    }

    string[] BuildCreatureNameCandidates(string creatureID)
    {
        if (string.IsNullOrWhiteSpace(creatureID)) return new[] { "whelpling", "Whelpling" };
        string raw = creatureID.Trim();
        string lower = raw.ToLowerInvariant();
        string title = char.ToUpperInvariant(raw[0]) + raw.Substring(1);
        string noUnderscore = raw.Replace("_", " ");
        return new[] { raw, lower, title, noUnderscore };
    }
}
