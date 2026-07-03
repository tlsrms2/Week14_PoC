using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    public partial class EnemyProjectile
    {
        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (IsGroundCollider(other))
            {
                return;
            }

            PlayerProjectile playerProjectile = other.GetComponentInParent<PlayerProjectile>();
            if (playerProjectile != null)
            {
                playerProjectile.TryDestroyByEnemyProjectileClash(this);
                return;
            }

            if (interceptPending && other.GetComponentInParent<PlayerCombatController>() != null)
            {
                return;
            }

            PlayerCombatController player = other.GetComponentInParent<PlayerCombatController>();
            if (IsCharging && player == null)
            {
                return;
            }

            if (IsWallCollider(other))
            {
                DestroyProjectile();
                return;
            }

            if (player == null)
            {
                BossAI hitBoss = other.GetComponentInParent<BossAI>();
                if (hitBoss != null)
                {
                    if (ShouldIgnoreBossCollision(hitBoss))
                    {
                        return;
                    }

                    DestroyProjectile();
                    return;
                }

                Minion hitMinion = other.GetComponentInParent<Minion>();
                if (hitMinion != null)
                {
                    if (ShouldIgnoreMinionCollision(hitMinion))
                    {
                        return;
                    }

                    DestroyProjectile();
                    return;
                }

                if (other.GetComponentInParent<EnemyProjectile>() != null)
                {
                    return;
                }

                if (TrySplitOnObstacle(other))
                {
                    return;
                }

                DestroyProjectile();
                return;
            }

            if (resolved)
            {
                return;
            }

            if (!CanHitPlayer(player) || !TryApplyPlayerHit(player))
            {
                return;
            }

            resolved = true;
            OnPlayerHit(player);
            DestroyProjectile(EnemyProjectileDestroyReason.PlayerHit);
        }

        protected virtual bool CanHitPlayer(PlayerCombatController player)
        {
            return player != null;
        }

        protected virtual bool TryApplyPlayerHit(PlayerCombatController player)
        {
            return player != null
                && player.ReceiveAttack(bulletDamage, transform.position, flightDirection);
        }

        protected virtual void OnPlayerHit(PlayerCombatController player) { }

        private bool ShouldIgnoreBossCollision(BossAI hitBoss)
        {
            if (hitBoss == null)
            {
                return false;
            }

            if (hitBoss == ownerBoss)
            {
                return true;
            }

            Transform sourceOwnerTransform = ownerMinion?.Owner?.MinionOwnerTransform;
            return sourceOwnerTransform != null && sourceOwnerTransform == hitBoss.transform;
        }

        private bool ShouldIgnoreMinionCollision(Minion hitMinion)
        {
            if (hitMinion == null)
            {
                return false;
            }

            if (hitMinion == ownerMinion)
            {
                return true;
            }

            Transform hitOwnerTransform = hitMinion.Owner?.MinionOwnerTransform;
            if (hitOwnerTransform == null)
            {
                return false;
            }

            if (ownerMinion != null)
            {
                Transform sourceOwnerTransform = ownerMinion.Owner?.MinionOwnerTransform;
                return sourceOwnerTransform != null && sourceOwnerTransform == hitOwnerTransform;
            }

            return ownerBoss != null && ownerBoss.transform == hitOwnerTransform;
        }

    }
}
