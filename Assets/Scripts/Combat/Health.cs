using System;
using UnityEngine;

namespace Week14.Combat
{
    public sealed class Health : MonoBehaviour
    {
        private float maxHealth = 1f;

        private float currentHealth;
        private bool deferDeathAtZero;

        public event Action<Health> Died;
        public event Action<Health> DurabilityDepleted;
        public event Action<float, float> Changed;

        public float CurrentDurability => currentHealth;
        public float MaxDurability => maxHealth;
        public bool IsDurabilityDepleted => currentHealth <= 0f;
        public bool IsDead { get; private set; }

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        public void SetMaxDurability(float value, bool refill)
        {
            maxHealth = Mathf.Max(1f, value);
            currentHealth = refill ? maxHealth : Mathf.Clamp(currentHealth, 0f, maxHealth);
            IsDead = currentHealth <= 0f && !deferDeathAtZero;
            Changed?.Invoke(currentHealth, maxHealth);
        }

        public void SetDeferDeathAtZero(bool value)
        {
            deferDeathAtZero = value;
        }

        public bool TakeDamage(float amount)
        {
            if (IsDead || IsDurabilityDepleted || amount <= 0f)
            {
                return false;
            }

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            Changed?.Invoke(currentHealth, maxHealth);

            if (currentHealth > 0f)
            {
                return false;
            }

            if (deferDeathAtZero)
            {
                DurabilityDepleted?.Invoke(this);
                return true;
            }

            IsDead = true;
            Died?.Invoke(this);
            return true;
        }

        public void Heal(float amount)
        {
            if (IsDead || IsDurabilityDepleted || amount <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            Changed?.Invoke(currentHealth, maxHealth);
        }

        public void Kill()
        {
            if (IsDead)
            {
                return;
            }

            currentHealth = 0f;
            IsDead = true;
            Changed?.Invoke(currentHealth, maxHealth);
            Died?.Invoke(this);
        }
    }
}
