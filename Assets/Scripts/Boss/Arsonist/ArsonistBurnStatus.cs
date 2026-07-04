using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [AddComponentMenu("")]
    internal sealed class ArsonistBurnStatus : MonoBehaviour
    {
        private PlayerCombatController player;
        private int remainingDamage;
        private int remainingTicks;
        private float tickInterval;
        private float expiresAt;
        private float nextTickAt;
        private bool initialized;

        public void Configure(PlayerCombatController nextPlayer, int totalDamage, float nextTickInterval, float duration)
        {
            player = nextPlayer;
            tickInterval = Mathf.Max(0.05f, nextTickInterval);
            float safeDuration = Mathf.Max(0.05f, duration);
            expiresAt = Time.time + safeDuration;
            remainingDamage = Mathf.Max(1, totalDamage);
            remainingTicks = Mathf.Max(1, Mathf.FloorToInt((safeDuration + 0.0001f) / tickInterval));
            initialized = true;
            nextTickAt = Time.time + Mathf.Min(tickInterval, safeDuration);
        }

        private void Update()
        {
            if (!initialized || player == null || player.Health == null || player.Health.IsDead)
            {
                Destroy(this);
                return;
            }

            if (Time.time < nextTickAt)
            {
                if (Time.time >= expiresAt)
                {
                    Destroy(this);
                }

                return;
            }

            int tickDamage = Mathf.CeilToInt((float)remainingDamage / remainingTicks);
            player.ReceiveAttack(tickDamage, transform.position, Vector2.up);
            remainingDamage -= tickDamage;
            remainingTicks--;
            if (remainingDamage <= 0 || remainingTicks <= 0)
            {
                Destroy(this);
                return;
            }

            nextTickAt = Time.time + tickInterval;
        }
    }
}
