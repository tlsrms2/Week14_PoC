using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Week14/Boss/Arsonist/Molotov Projectile")]
    public sealed class ArsonistMolotovProjectile : EnemyProjectile
    {
        [SerializeField] private ArsonistBossAI owner;
        [SerializeField, Min(0.05f)] private float fireRadius = 0.75f;
        [SerializeField, Min(0.05f)] private float fireDuration = 1.35f;
        [SerializeField] private Color fireColor = new(1f, 0.35f, 0.05f, 0.9f);
        [SerializeField, Min(0f)] private float parriedFireDamageDelay = 0.35f;

        public void Initialize(ArsonistBossAI nextOwner)
        {
            owner = nextOwner != null ? nextOwner : owner;
        }

        protected override void OnProjectileAwake()
        {
            ResolveOwner();
        }

        protected override void OnProjectileDestroying(
            EnemyProjectileDestroyReason reason,
            Vector3 position)
        {
            if (reason == EnemyProjectileDestroyReason.OwnerDestroyed)
            {
                return;
            }

            ResolveOwner();
            float playerDamageDelay = reason == EnemyProjectileDestroyReason.Intercepted
                ? parriedFireDamageDelay
                : 0f;
            if (reason == EnemyProjectileDestroyReason.Intercepted)
            {
                owner?.DelayFireDamageFor(PlayerCombatController.Active, playerDamageDelay);
            }

            owner?.CreateFireArea(position, fireRadius, fireDuration, fireColor, playerDamageDelay);
        }

        private void ResolveOwner()
        {
            if (owner != null)
            {
                return;
            }

            owner = GetComponentInParent<ArsonistBossAI>();
            if (owner == null)
            {
                owner = FindFirstObjectByType<ArsonistBossAI>();
            }
        }
    }
}
