using UnityEngine;

namespace Week14.Combat
{
    public partial class EnemyProjectile
    {
        public virtual bool TryDestroyByInterceptShot(out bool parried)
        {
            if (!CanReceiveInterceptShot())
            {
                parried = false;
                return false;
            }

            parried = true;
            CompleteInterceptAndDestroy();
            return true;
        }

        protected bool CanReceiveInterceptShot()
        {
            return !resolved && !isDestroying && canBeIntercepted;
        }

        protected void CompletePartialIntercept()
        {
            interceptPending = false;
            SetParryLockOnIndicatorVisible(false);
        }

        protected void CompleteInterceptAndDestroy()
        {
            resolved = true;
            interceptPending = false;
            DestroyProjectile(EnemyProjectileDestroyReason.Intercepted);
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
            AnyDestroyed?.Invoke(this, reason);
            UnregisterInterceptGroup();
            activeProjectiles.Remove(this);
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

            if (pooledByProjectilePool)
            {
                OnProjectileReturnedToPool();
                ProjectilePool.Release(this);
            }
            else
            {
                Destroy(gameObject);
            }
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

        protected virtual void OnProjectileReturnedToPool()
        {
            Launched = null;
            RadialSplit = null;
            RadialSplitImminent = null;
            Destroyed = null;
        }

    }
}
