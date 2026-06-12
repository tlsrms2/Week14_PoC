using System;
using UnityEngine;

namespace Week14.Combat
{
    public sealed class HeatGauge : MonoBehaviour
    {
        private float maxHeat = 1f;
        private float coolingPerSecond;
        private float overheatSeconds;
        private float heatAfterOverheatRatio;

        private float currentHeat;
        private float overheatTimer;
        private float coolingSuppressedTimer;

        public event Action<HeatGauge> Overheated;
        public event Action<HeatGauge> Recovered;
        public event Action<float, float> Changed;

        public float CurrentHeat => currentHeat;
        public float MaxHeat => maxHeat;
        public bool IsOverheated { get; private set; }

        private void Update()
        {
            if (IsOverheated)
            {
                TickOverheat();
                return;
            }

            if (coolingSuppressedTimer > 0f)
            {
                coolingSuppressedTimer -= Time.deltaTime;
                return;
            }

            if (coolingPerSecond > 0f && currentHeat > 0f)
            {
                ReduceHeat(coolingPerSecond * Time.deltaTime);
            }
        }

        public void Configure(float maxValue, float cooling, float overheatDuration, float recoveryRatio, bool resetHeat)
        {
            maxHeat = Mathf.Max(1f, maxValue);
            coolingPerSecond = Mathf.Max(0f, cooling);
            overheatSeconds = Mathf.Max(0f, overheatDuration);
            heatAfterOverheatRatio = Mathf.Clamp01(recoveryRatio);

            if (resetHeat)
            {
                currentHeat = 0f;
                IsOverheated = false;
                overheatTimer = 0f;
                coolingSuppressedTimer = 0f;
            }

            currentHeat = Mathf.Clamp(currentHeat, 0f, maxHeat);
            Changed?.Invoke(currentHeat, maxHeat);
        }

        public void AddHeat(float amount)
        {
            if (amount <= 0f || IsOverheated)
            {
                return;
            }

            currentHeat = Mathf.Min(maxHeat, currentHeat + amount);
            Changed?.Invoke(currentHeat, maxHeat);

            if (currentHeat >= maxHeat)
            {
                BeginOverheat();
            }
        }

        public void ReduceHeat(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            currentHeat = Mathf.Max(0f, currentHeat - amount);
            Changed?.Invoke(currentHeat, maxHeat);
        }

        public void SuppressCooling(float seconds)
        {
            coolingSuppressedTimer = Mathf.Max(coolingSuppressedTimer, seconds);
        }

        private void TickOverheat()
        {
            overheatTimer -= Time.deltaTime;
            if (overheatTimer > 0f)
            {
                return;
            }

            IsOverheated = false;
            currentHeat = maxHeat * heatAfterOverheatRatio;
            Changed?.Invoke(currentHeat, maxHeat);
            Recovered?.Invoke(this);
        }

        private void BeginOverheat()
        {
            IsOverheated = true;
            overheatTimer = overheatSeconds;
            currentHeat = maxHeat;
            Changed?.Invoke(currentHeat, maxHeat);
            Overheated?.Invoke(this);

            if (overheatSeconds <= 0f)
            {
                TickOverheat();
            }
        }
    }
}
