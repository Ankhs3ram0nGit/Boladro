using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BattleManager : MonoBehaviour
{
    public float engageRadius = 5f;
    public float turnInterval = 0.6f;
    public int playerDamage = 1;
    public int enemyDamage = 1;

    private bool inBattle;
    private WildCreatureAI currentEnemy;
    private PlayerHealth playerHealth;
    private PlayerMover playerMover;

    void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerMover = GetComponent<PlayerMover>();
    }

    void Update()
    {
        if (inBattle) return;

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame)
        {
            TryStartBattle();
        }
    }

    void TryStartBattle()
    {
        WildCreatureAI[] enemies = FindObjectsByType<WildCreatureAI>(FindObjectsSortMode.None);
        if (enemies == null || enemies.Length == 0) return;

        List<WildCreatureAI> candidates = new List<WildCreatureAI>();
        Vector2 pos = transform.position;

        for (int i = 0; i < enemies.Length; i++)
        {
            WildCreatureAI e = enemies[i];
            if (e == null) continue;
            if (!e.gameObject.activeInHierarchy) continue;
            if (!e.IsAlive()) continue;
            if (e.IsInBattle()) continue;
            if (Vector2.Distance(pos, e.transform.position) <= engageRadius)
            {
                candidates.Add(e);
            }
        }

        if (candidates.Count == 0) return;

        currentEnemy = candidates[Random.Range(0, candidates.Count)];
        StartCoroutine(BattleRoutine());
    }

    IEnumerator BattleRoutine()
    {
        inBattle = true;

        if (playerMover != null) playerMover.enabled = false;
        if (currentEnemy != null)
        {
            currentEnemy.EnterBattle();
            currentEnemy.ForceStop();
        }

        CreatureHealth enemyHealth = currentEnemy != null ? currentEnemy.GetComponent<CreatureHealth>() : null;

        while (true)
        {
            yield return new WaitForSeconds(turnInterval);

            if (playerHealth == null || enemyHealth == null) break;
            if (playerHealth.currentHealth <= 0 || enemyHealth.currentHealth <= 0) break;

            enemyHealth.TakeDamage(playerDamage);
            if (enemyHealth.currentHealth <= 0) break;

            playerHealth.TakeDamage(enemyDamage);
            if (playerHealth.currentHealth <= 0) break;
        }

        if (currentEnemy != null) currentEnemy.ExitBattle();
        currentEnemy = null;

        if (playerMover != null && (playerHealth == null || playerHealth.currentHealth > 0))
        {
            playerMover.enabled = true;
        }

        inBattle = false;
    }
}
