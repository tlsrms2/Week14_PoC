using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Week14/Boss/Arsonist/Molotov Projectile")]
    public sealed class ArsonistMolotovProjectile : EnemyProjectile
    {
        [SerializeField] private Arsonist owner;
        [SerializeField, Min(0.05f)] private float fireRadius = 0.75f;
        [SerializeField, Min(0.05f)] private float fireDuration = 1.35f;

        public void Initialize(Arsonist nextOwner)
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
            owner?.CreateFireArea(position, fireRadius, fireDuration);
        }

        private void ResolveOwner()
        {
            if (owner != null)
            {
                return;
            }

            owner = GetComponentInParent<Arsonist>();
            if (owner == null)
            {
                owner = FindFirstObjectByType<Arsonist>();
            }
        }
    }
}
