using System;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 10;
    public int currentHealth = 10;
    public bool debugDamageKey = true;
    public Transform followerToTeleport;
    public Vector3 followerRespawnOffset = new Vector3(-0.6f, 0.2f, 0f);
    public AudioClip playerDamageSfx;

    public event Action<int, int> OnHealthChanged;
    public event Action OnDied;
    public event Action OnRespawned;

    private bool isDead;
    private Vector3 respawnPosition;
    private float invulnerableUntil = -1f;
    private CameraFollow2D cameraFollow;
    private AudioSource damageSfxSource;

    public bool IsInvulnerable => Time.time < invulnerableUntil;

    void Start()
    {
        EnsureDamageAudioAsset();
        EnsureDamageAudioSource();
        int previousMax = maxHealth;
        if (maxHealth != 10)
        {
            maxHealth = 10;
        }
        respawnPosition = transform.position;
        if (currentHealth <= 0)
        {
            currentHealth = maxHealth;
        }
        if (previousMax != maxHealth && currentHealth == previousMax)
        {
            currentHealth = maxHealth;
        }
        currentHealth = Mathf.Clamp(currentHealth, 1, maxHealth);
        NotifyHealth();
        cameraFollow = FindAnyObjectByType<CameraFollow2D>();
        if (followerToTeleport == null)
        {
            if (ActivePartyFollowerController.Instance != null && ActivePartyFollowerController.Instance.CurrentFollowerTransform != null)
            {
                followerToTeleport = ActivePartyFollowerController.Instance.CurrentFollowerTransform;
            }
        }
        if (followerToTeleport == null)
        {
            GameObject frog = GameObject.Find("Frog");
            if (frog != null) followerToTeleport = frog.transform;
        }
    }

    void Update()
    {
        if (!debugDamageKey || isDead) return;

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.kKey.wasPressedThisFrame)
        {
            TakeDamage(1);
        }
    }

    public void TakeDamage(int amount, bool bypassBattleLock = false)
    {
        if (isDead) return;
        if (IsInvulnerable) return;
        if (!bypassBattleLock && BattleSystem.IsEngagedBattleActive) return;
        if (amount <= 0) return;

        int before = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        int dealt = Mathf.Max(0, before - currentHealth);
        if (dealt > 0)
        {
            TriggerDamageShake(dealt);
            PlayDamageSfx();
        }
        NotifyHealth();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        NotifyHealth();
    }

    void Die()
    {
        isDead = true;

        PlayerMover mover = GetComponent<PlayerMover>();
        if (mover != null) mover.enabled = false;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (OnDied != null) OnDied.Invoke();
    }

    public void Respawn()
    {
        isDead = false;
        invulnerableUntil = -1f;
        currentHealth = maxHealth;
        transform.position = respawnPosition;

        PlayerCreatureParty party = GetComponent<PlayerCreatureParty>();
        if (party != null)
        {
            party.ReviveAllCreaturesToFull();
        }

        PlayerMover mover = GetComponent<PlayerMover>();
        if (mover != null) mover.enabled = true;

        if (followerToTeleport != null)
        {
            followerToTeleport.position = transform.position + followerRespawnOffset;
        }
        else if (ActivePartyFollowerController.Instance != null && ActivePartyFollowerController.Instance.CurrentFollowerTransform != null)
        {
            followerToTeleport = ActivePartyFollowerController.Instance.CurrentFollowerTransform;
            followerToTeleport.position = transform.position + followerRespawnOffset;
        }

        NotifyHealth();
        if (OnRespawned != null) OnRespawned.Invoke();
    }

    void NotifyHealth()
    {
        if (OnHealthChanged != null) OnHealthChanged.Invoke(currentHealth, maxHealth);
    }

    public void SetInvulnerable(float durationSeconds)
    {
        if (durationSeconds <= 0f) return;
        float until = Time.time + durationSeconds;
        if (until > invulnerableUntil)
        {
            invulnerableUntil = until;
        }
    }

    void TriggerDamageShake(int damageAmount)
    {
        if (cameraFollow == null)
        {
            cameraFollow = FindAnyObjectByType<CameraFollow2D>();
        }
        if (cameraFollow == null) return;
        float intensity = Mathf.Clamp(0.8f + (damageAmount * 0.2f), 0.8f, 2.0f);
        cameraFollow.TriggerDamageShake(intensity);
    }

    void EnsureDamageAudioAsset()
    {
#if UNITY_EDITOR
        if (playerDamageSfx == null)
        {
            playerDamageSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/JDSherbert - Ultimate UI SFX Pack (FREE)/Stereo/mp3/JDSherbert - Ultimate UI SFX Pack - Error - 1.mp3");
        }
#endif
    }

    void EnsureDamageAudioSource()
    {
        if (damageSfxSource != null) return;
        damageSfxSource = gameObject.AddComponent<AudioSource>();

        damageSfxSource.playOnAwake = false;
        damageSfxSource.loop = false;
        damageSfxSource.spatialBlend = 0f;
        damageSfxSource.volume = 1f;
    }

    void PlayDamageSfx()
    {
        if (playerDamageSfx == null) return;
        EnsureDamageAudioSource();
        if (damageSfxSource == null) return;
        damageSfxSource.PlayOneShot(playerDamageSfx);
    }
}
