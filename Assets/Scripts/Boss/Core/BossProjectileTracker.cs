using System.Collections.Generic;
using Week14.Combat;

namespace Week14.Enemy
{
    internal sealed class BossProjectileTracker
    {
        private readonly List<EnemyProjectile> activeProjectiles = new();

        public void Register(EnemyProjectile projectile)
        {
            PruneInactive();
            if (projectile == null || activeProjectiles.Contains(projectile))
            {
                return;
            }

            activeProjectiles.Add(projectile);
        }

        public void Unregister(EnemyProjectile projectile)
        {
            if (projectile != null)
            {
                activeProjectiles.Remove(projectile);
            }
        }

        public void DestroyAll()
        {
            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                if (activeProjectiles[i] != null)
                {
                    activeProjectiles[i].DestroyFromOwner();
                }
            }

            activeProjectiles.Clear();
        }

        private void PruneInactive()
        {
            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                if (activeProjectiles[i] == null)
                {
                    activeProjectiles.RemoveAt(i);
                }
            }
        }
    }
}
