using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerCreatureParty))]
public class ActivePartyFollowerController : MonoBehaviour
{
    public static ActivePartyFollowerController Instance { get; private set; }

    [Header("Input")]
    public Key cyclePartyKey = Key.Q;
    public bool disableCycleDuringBattle = true;

    [Header("Follower Movement")]
    public float followDistance = 2f;
    public float hopTilesPerMove = 2f;
    public float movementSpeedMultiplier = 0.33333334f;
    public float hopCrouchTime = 0.07f;
    public float hopAirTime = 0.13f;
    public float hopLandTime = 0.06f;
    public float hopPauseTime = 0.05f;
    public float hopArcHeight = 0.18f;
    public bool spriteFacesRight = true;

    [Header("Follower Visual")]
    [Tooltip("Baseline world scale applied before per-creature overworld size multiplier.")]
    public float followerScaleBaseline = 0.2667868f;
    public Vector3 respawnOffsetFromPlayer = new Vector3(-0.6f, 0.2f, 0f);
    public bool addIdleBounceAnimator = true;
    public string followerObjectName = "__ActivePartyFollower";

    public Transform CurrentFollowerTransform => followerRoot != null ? followerRoot.transform : null;
    public CreatureCombatant CurrentFollowerCombatant => followerCombatant;

    private PlayerCreatureParty party;
    private PlayerHealth playerHealth;
    private PlayerMover playerMover;
    private SpriteRenderer playerRenderer;

    private GameObject followerRoot;
    private SpriteRenderer followerRenderer;
    private Follower followerAI;
    private TopDownSorter followerSorter;
    private FeetAnchorAuto followerFeetAuto;
    private CreatureCombatant followerCombatant;
    private WhelplingBounceAnimator bounceAnimator;
    private Transform followerFeet;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        party = GetComponent<PlayerCreatureParty>();
        playerHealth = GetComponent<PlayerHealth>();
        playerMover = GetComponent<PlayerMover>();
        playerRenderer = GetComponent<SpriteRenderer>();

        EnsureFollowerObject();
        RefreshFollowerFromActiveParty();
    }

    void OnEnable()
    {
        if (party == null) party = GetComponent<PlayerCreatureParty>();
        if (party != null)
        {
            party.PartyChanged -= HandlePartyChanged;
            party.PartyChanged += HandlePartyChanged;
        }

        EnsureFollowerObject();
        RefreshFollowerFromActiveParty();
    }

    void OnDisable()
    {
        if (party != null)
        {
            party.PartyChanged -= HandlePartyChanged;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        HandleCycleInput();
        ApplyFollowerMovementSettings();
        FaceFollowerTowardPlayer();
        BindFollowerToPlayerHealth();
    }

    void LateUpdate()
    {
        FaceFollowerTowardPlayer();
    }

    private void HandleCycleInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        KeyControl key = kb[cyclePartyKey];
        if (key == null || !key.wasPressedThisFrame) return;
        if (disableCycleDuringBattle && BattleSystem.IsEngagedBattleActive) return;
        if (party == null || party.ActiveCreatures == null) return;

        int count = party.ActiveCreatures.Count;
        if (count <= 0) return;

        int next = (party.ActivePartyIndex + 1) % count;
        party.SetActivePartyIndex(next);
    }

    private void HandlePartyChanged()
    {
        RefreshFollowerFromActiveParty();
    }

    private void EnsureFollowerObject()
    {
        if (followerRoot == null)
        {
            GameObject existing = GameObject.Find(followerObjectName);
            followerRoot = existing;
        }

        if (followerRoot == null)
        {
            followerRoot = new GameObject(followerObjectName);
            followerRoot.transform.position = transform.position + respawnOffsetFromPlayer;
        }

        followerRenderer = EnsureComponent<SpriteRenderer>(followerRoot);
        followerAI = EnsureComponent<Follower>(followerRoot);
        followerSorter = EnsureComponent<TopDownSorter>(followerRoot);
        followerFeetAuto = EnsureComponent<FeetAnchorAuto>(followerRoot);
        followerCombatant = EnsureComponent<CreatureCombatant>(followerRoot);

        if (addIdleBounceAnimator)
        {
            bounceAnimator = EnsureComponent<WhelplingBounceAnimator>(followerRoot);
        }
        else
        {
            bounceAnimator = followerRoot.GetComponent<WhelplingBounceAnimator>();
            if (bounceAnimator != null) bounceAnimator.enabled = false;
        }

        followerFeet = followerRoot.transform.Find("Feet");
        if (followerFeet == null)
        {
            GameObject feetGo = new GameObject("Feet");
            feetGo.transform.SetParent(followerRoot.transform, false);
            feetGo.transform.localPosition = Vector3.zero;
            followerFeet = feetGo.transform;
        }

        followerRoot.layer = gameObject.layer;
        followerAI.target = transform;

        if (playerRenderer != null)
        {
            followerRenderer.sortingLayerID = playerRenderer.sortingLayerID;
        }

        followerSorter.sortMode = TopDownSorter.SortMode.RendererBottomY;
        followerSorter.setSpriteSortPointToPivot = true;
        followerSorter.useFixedShadowOrder = true;
        followerSorter.fixedShadowOrder = -999;
        followerSorter.feetTransform = followerFeet;

        followerFeetAuto.feetAnchor = followerFeet;
        followerFeetAuto.yOffset = 0f;
    }

    private void ApplyFollowerMovementSettings()
    {
        if (followerAI == null) return;
        followerAI.target = transform;
        followerAI.followDistance = followDistance;
        followerAI.hopTilesPerMove = hopTilesPerMove;
        followerAI.movementSpeedMultiplier = movementSpeedMultiplier;
        followerAI.hopCrouchTime = hopCrouchTime;
        followerAI.hopAirTime = hopAirTime;
        followerAI.hopLandTime = hopLandTime;
        followerAI.hopPauseTime = hopPauseTime;
        followerAI.hopArcHeight = hopArcHeight;
        followerAI.spriteFacesRight = spriteFacesRight;
    }

    private void BindFollowerToPlayerHealth()
    {
        if (playerHealth == null) return;
        if (CurrentFollowerTransform == null) return;

        playerHealth.followerToTeleport = CurrentFollowerTransform;
        playerHealth.followerRespawnOffset = respawnOffsetFromPlayer;
    }

    private void RefreshFollowerFromActiveParty()
    {
        EnsureFollowerObject();
        ApplyFollowerMovementSettings();
        BindFollowerToPlayerHealth();

        if (party == null || party.ActiveCreatures == null || party.ActiveCreatures.Count == 0)
        {
            if (followerRoot != null) followerRoot.SetActive(false);
            return;
        }

        CreatureRegistry.Initialize();

        int idx = Mathf.Clamp(party.ActivePartyIndex, 0, party.ActiveCreatures.Count - 1);
        CreatureInstance active = party.ActiveCreatures[idx];
        CreatureDefinition def = active != null ? CreatureRegistry.Get(active.definitionID) : null;

        if (def == null || def.sprite == null)
        {
            if (followerRoot != null) followerRoot.SetActive(false);
            return;
        }

        followerRoot.SetActive(true);
        followerRenderer.enabled = true;
        followerRenderer.sprite = def.sprite;
        followerRenderer.color = Color.white;
        followerRenderer.flipX = false;

        float baseline = Mathf.Max(0.05f, Mathf.Abs(followerScaleBaseline));
        float scale = Mathf.Max(0.05f, baseline * Mathf.Max(0.05f, def.overworldSizeMultiplier));
        followerRoot.transform.localScale = new Vector3(scale, scale, 1f);

        followerCombatant.autoInitWhelpling = false;
        followerCombatant.InitFromDefinition(def, active);

        if (bounceAnimator != null)
        {
            bounceAnimator.enabled = addIdleBounceAnimator;
            if (bounceAnimator.enabled)
            {
                bounceAnimator.RefreshDefaultSprite();
            }
        }

        FaceFollowerTowardPlayer();
    }

    private void FaceFollowerTowardPlayer()
    {
        if (followerRenderer == null || followerRoot == null) return;

        float dx = transform.position.x - followerRoot.transform.position.x;
        if (Mathf.Abs(dx) <= 0.0001f) return;

        bool playerIsLeft = dx < 0f;
        followerRenderer.flipX = spriteFacesRight ? playerIsLeft : !playerIsLeft;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        if (c == null) c = go.AddComponent<T>();
        return c;
    }
}

public static class ActivePartyFollowerBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureFollowerController()
    {
        PlayerMover mover = Object.FindFirstObjectByType<PlayerMover>();
        if (mover == null) return;

        if (mover.GetComponent<PlayerCreatureParty>() == null)
        {
            mover.gameObject.AddComponent<PlayerCreatureParty>();
        }

        if (mover.GetComponent<ActivePartyFollowerController>() == null)
        {
            mover.gameObject.AddComponent<ActivePartyFollowerController>();
        }
    }
}
