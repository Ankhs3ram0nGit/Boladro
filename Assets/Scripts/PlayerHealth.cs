using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 10;
    public int currentHealth = 10;
    public bool debugDamageKey = true;
    public Transform followerToTeleport;
    public Vector3 followerRespawnOffset = new Vector3(-0.6f, 0.2f, 0f);

    public event Action<int, int> OnHealthChanged;
    public event Action OnDied;
    public event Action OnRespawned;

    private bool isDead;
    private Vector3 respawnPosition;
    private float invulnerableUntil = -1f;
    private CameraFollow2D cameraFollow;

    public bool IsInvulnerable => Time.time < invulnerableUntil;

    void Start()
    {
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

        PlayerMover mover = GetComponent<PlayerMover>();
        if (mover != null) mover.enabled = true;

        if (followerToTeleport != null)
        {
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
}
