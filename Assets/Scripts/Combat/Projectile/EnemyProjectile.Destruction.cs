using UnityEngine;

namespace Week14.Combat
{
    public partial class EnemyProjectile
    {
        public bool TryDestroyByInterceptShot(out bool parried)
        {
            if (resolved || isDestroying || !canBeIntercepted)
            {
                parried = false;
                return false;
            }

            parried = true;
            resolved = true;

            DestroyProjectile(EnemyProjectileDestroyReason.Intercepted);
            return true;
        }

        public bool TryReserveIntercept()
        {
            if (!CanBeIntercepted)
            {
                return false;
            }

            interceptPending = true;
            SetParryLockOnIndicatorVisible(false);
            return true;
        }

        public void CancelInterceptReservation()
        {
            if (resolved || isDestroying)
            {
                return;
            }

            interceptPending = false;
        }

        public void DestroyFromOwner()
        {
            resolved = true;
            DestroyProjectile(EnemyProjectileDestroyReason.OwnerDestroyed);
        }

        protected void DestroyProjectile(EnemyProjectileDestroyReason reason = EnemyProjectileDestroyReason.Unknown)
        {
            if (isDestroying)
            {
                return;
            }

            isDestroying = true;
            Vector3 destroyPosition = transform.position;
            OnProjectileDestroying(reason, destroyPosition);
            Destroyed?.Invoke(this, reason, destroyPosition);
            UnregisterInterceptGroup();
            ReleaseOwnerProjectileSlot();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            SetChargeVfxVisible(false);
            SetPathIndicatorVisible(false);
            SetParryLockOnIndicatorVisible(false);

            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }

            Destroy(gameObject);
        }

        protected virtual void OnDestroy()
        {
            UnregisterInterceptGroup();
            activeProjectiles.Remove(this);
            ReleaseOwnerProjectileSlot();
        }

        private void ReleaseOwnerProjectileSlot()
        {
            if (ownerSlotReleased)
            {
                return;
            }

            ownerSlotReleased = true;
            ownerBoss?.UnregisterActiveProjectile(this);
            ownerMinion?.UnregisterActiveProjectile(this);
        }

        protected virtual void OnProjectileDestroying(EnemyProjectileDestroyReason reason, Vector3 position) { }

    }
}
