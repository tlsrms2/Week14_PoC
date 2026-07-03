using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyProjectile))]
    [AddComponentMenu("Week14/Boss/Arsonist/Molotov Projectile")]
    public sealed class ArsonistMolotovProjectile : MonoBehaviour
    {
        [SerializeField] private Arsonist owner;
        [SerializeField, Min(0.05f)] private float fireRadius = 0.75f;
        [SerializeField, Min(0.05f)] private float fireDuration = 1.35f;

        private EnemyProjectile projectile;
        private bool subscribed;

        public void Initialize(Arsonist nextOwner)
        {
            owner = nextOwner != null ? nextOwner : owner;
            Subscribe();
        }

        private void Awake()
        {
            projectile = GetComponent<EnemyProjectile>();
        }

        private void OnEnable()
        {
            ResolveOwner();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void HandleProjectileDestroyed(
            EnemyProjectile source,
            EnemyProjectileDestroyReason reason,
            Vector3 position)
        {
            Unsubscribe();
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

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (projectile == null)
            {
                projectile = GetComponent<EnemyProjectile>();
            }

            if (projectile == null)
            {
                return;
            }

            projectile.Destroyed += HandleProjectileDestroyed;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || projectile == null)
            {
                subscribed = false;
                return;
            }

            projectile.Destroyed -= HandleProjectileDestroyed;
            subscribed = false;
        }
    }
}
