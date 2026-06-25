using System;
using UnityEngine;

namespace Week14.Combat
{
    public enum BulletChangeSource
    {
        None,
        Generic,
        Hit,
        Attack,
        Parry,
        Execution,
        WeaponSwitch,
        Expired
    }

    public class BulletGauge : MonoBehaviour
    {
        private int maxBullets = 1;
        private int currentBullets = 1;

        public event Action<BulletGauge> Emptied;
        public event Action<int, int> Changed;

        public int CurrentBullets => currentBullets;
        public int MaxBullets => maxBullets;
        public BulletChangeSource LastChangeSource { get; private set; }
        public bool IsEmpty => currentBullets <= 0;

        public void Configure(int maxValue, bool refill)
        {
            Configure(maxValue, refill, BulletChangeSource.None);
        }

        public void Configure(int maxValue, bool refill, BulletChangeSource source)
        {
            maxBullets = Mathf.Max(1, maxValue);
            currentBullets = refill ? maxBullets : Mathf.Clamp(currentBullets, 0, maxBullets);
            LastChangeSource = source;
            Changed?.Invoke(currentBullets, maxBullets);
        }

        public bool TrySpend(int amount, BulletChangeSource source = BulletChangeSource.Generic)
        {
            if (amount <= 0 || IsEmpty)
            {
                return false;
            }

            currentBullets = Mathf.Max(0, currentBullets - amount);
            LastChangeSource = source;
            Changed?.Invoke(currentBullets, maxBullets);

            if (IsEmpty)
            {
                Emptied?.Invoke(this);
            }

            return true;
        }

        public bool Restore(int amount, BulletChangeSource source = BulletChangeSource.Generic)
        {
            if (amount <= 0 || currentBullets >= maxBullets)
            {
                return false;
            }

            currentBullets = Mathf.Min(maxBullets, currentBullets + amount);
            LastChangeSource = source;
            Changed?.Invoke(currentBullets, maxBullets);
            return true;
        }
    }
}
