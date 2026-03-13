using System;
using UnityEngine;

public class CreatureHealth : MonoBehaviour
{
    public int maxHealth = 3;
    public int currentHealth = 3;
    public int level = 1;
    public bool destroyOnDeath = false;

    public event Action<CreatureHealth, int> OnDamaged;
    public event Action<CreatureHealth> OnDied;

    void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 1, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        if (currentHealth <= 0) return;

        int before = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        int dealt = Mathf.Max(0, before - currentHealth);
        if (dealt > 0 && OnDamaged != null) OnDamaged.Invoke(this, dealt);
        if (currentHealth <= 0)
        {
            if (OnDied != null) OnDied.Invoke(this);

            if (destroyOnDeath)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
