using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [AddComponentMenu("")]
    internal sealed class ArsonistBurnStatus : MonoBehaviour
    {
        private PlayerCombatController player;
        private int damage;
        private float tickInterval;
        private float expiresAt;
        private float nextTickAt;
        private bool initialized;

        public void Configure(PlayerCombatController nextPlayer, int nextDamage, float nextTickInterval, float duration)
        {
            player = nextPlayer;
            damage = Mathf.Max(1, nextDamage);
            tickInterval = Mathf.Max(0.05f, nextTickInterval);
            expiresAt = Time.time + Mathf.Max(0.05f, duration);
            if (!initialized)
            {
                initialized = true;
                nextTickAt = Time.time + tickInterval;
            }
        }

        private void Update()
        {
            if (!initialized || player == null || player.Health == null || player.Health.IsDead)
            {
                Destroy(this);
                return;
            }

            if (Time.time >= expiresAt)
            {
                Destroy(this);
                return;
            }

            if (Time.time < nextTickAt)
            {
                return;
            }

            player.ReceiveAttack(damage, transform.position, Vector2.up);
            nextTickAt = Time.time + tickInterval;
        }
    }
}
