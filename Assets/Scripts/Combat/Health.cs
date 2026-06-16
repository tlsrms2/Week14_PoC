using System;
using UnityEngine;

namespace Week14.Combat
{
    public sealed class Health : MonoBehaviour
    {
        public event Action<Health> Died;

        public bool IsDead { get; private set; }

        public bool TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f)
            {
                return false;
            }

            Kill();
            return true;
        }

        public void Kill()
        {
            if (IsDead)
            {
                return;
            }

            IsDead = true;
            Died?.Invoke(this);
        }
    }
}
