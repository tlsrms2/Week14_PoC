using System;
using UnityEngine;

namespace Week14.Combat
{
    public enum HeatChangeSource
    {
        None,
        Generic,
        Hit,
        Parry,
        Defense
    }

    public sealed class HeatGauge : MonoBehaviour
    {
        private float maxHeat = 1f;
        private float coolingPerSecond;
        private float overheatSeconds;

        private float currentHeat;
        private float overheatTimer;
        private float coolingSuppressedTimer;
        private Health health;

        public event Action<HeatGauge> Overheated;
        public event Action<HeatGauge> Recovered;
        public event Action<float, float> Changed;

        public float CurrentHeat => currentHeat;
        public float MaxHeat => maxHeat;
        public float OverheatRemainingRatio => IsOverheated && overheatSeconds > 0f
            ? Mathf.Clamp01(overheatTimer / overheatSeconds)
            : 0f;
        public HeatChangeSource LastChangeSource { get; private set; }
        public bool IsOverheated { get; private set; }

        private void Awake()
        {
            health = GetComponent<Health>();
        }

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
                ReduceHeat(coolingPerSecond * GetHealthCoolingMultiplier() * Time.deltaTime);
            }
        }

        public void Configure(float maxValue, float cooling, float overheatDuration, bool resetHeat)
        {
            maxHeat = Mathf.Max(1f, maxValue);
            coolingPerSecond = Mathf.Max(0f, cooling);
            overheatSeconds = Mathf.Max(0f, overheatDuration);

            if (resetHeat)
            {
                currentHeat = 0f;
                IsOverheated = false;
                overheatTimer = 0f;
                coolingSuppressedTimer = 0f;
                LastChangeSource = HeatChangeSource.None;
            }

            currentHeat = Mathf.Clamp(currentHeat, 0f, maxHeat);
            Changed?.Invoke(currentHeat, maxHeat);
        }

        public void AddHeat(float amount, HeatChangeSource source = HeatChangeSource.Generic)
        {
            if (amount <= 0f || IsOverheated)
            {
                return;
            }

            currentHeat = Mathf.Min(maxHeat, currentHeat + amount);
            LastChangeSource = source;
            Changed?.Invoke(currentHeat, maxHeat);

            if (currentHeat >= maxHeat)
            {
                BeginOverheat();
            }
        }

        public void AddHeatWithoutOverheat(float amount, HeatChangeSource source = HeatChangeSource.Generic)
        {
            if (amount <= 0f || IsOverheated)
            {
                return;
            }

            currentHeat = Mathf.Min(maxHeat, currentHeat + amount);
            LastChangeSource = source;
            Changed?.Invoke(currentHeat, maxHeat);
        }

        public void ReduceHeat(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            currentHeat = Mathf.Max(0f, currentHeat - amount);
            LastChangeSource = HeatChangeSource.None;
            Changed?.Invoke(currentHeat, maxHeat);
        }

        public void SuppressCooling(float seconds)
        {
            coolingSuppressedTimer = Mathf.Max(coolingSuppressedTimer, seconds);
        }

        private float GetHealthCoolingMultiplier()
        {
            if (health == null)
            {
                return 1f;
            }

            return Mathf.Clamp01(health.CurrentDurability / Mathf.Max(1f, health.MaxDurability));
        }

        private void TickOverheat()
        {
            overheatTimer -= Time.deltaTime;
            if (overheatTimer > 0f)
            {
                return;
            }

            IsOverheated = false;
            currentHeat = 0f;
            LastChangeSource = HeatChangeSource.None;
            Changed?.Invoke(currentHeat, maxHeat);
            Recovered?.Invoke(this);
        }

        private void BeginOverheat()
        {
            IsOverheated = true;
            overheatTimer = overheatSeconds;
            currentHeat = maxHeat;
            LastChangeSource = HeatChangeSource.Generic;
            Changed?.Invoke(currentHeat, maxHeat);
            Overheated?.Invoke(this);

            if (overheatSeconds <= 0f)
            {
                TickOverheat();
            }
        }
    }
}
