using System;
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

            if (IsArsonistGroundHazardCollider(other))
            {
                return;
            }

            if (other.GetComponentInParent<PlayerProjectile>() != null)
            {
                return;
            }

            if (interceptPending && other.GetComponentInParent<PlayerCombatController>() != null)
            {
                return;
            }

            if (!reflectedByPlayer && IsOwnerCollider(other))
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
                    if (TryResolveReflectedEnemyHit(other.GetComponentInParent<Health>()))
                    {
                        return;
                    }

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
                    if (TryResolveReflectedEnemyHit(other.GetComponentInParent<Health>()))
                    {
                        return;
                    }

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
            if (reflectedByPlayer)
            {
                return false;
            }

            return player != null && !ignorePlayerCollision;
        }

        protected virtual bool TryApplyPlayerHit(PlayerCombatController player)
        {
            if (player == null)
            {
                return false;
            }

            if (player.ReceiveAttack(bulletDamage, transform.position, flightDirection))
            {
                if (playerHitKnockbackSpeed > 0f)
                {
                    player.ApplyExternalKnockback(
                        flightDirection,
                        playerHitKnockbackSpeed,
                        playerHitKnockbackStaggerSeconds);
                }

                return true;
            }

            // 무적 때문에 데미지가 안 들어간 경우에도 탄환 자체는 파괴합니다(플레이어를 그냥 통과하지 않도록).
            // 그 외 사유(실행 연출 중, 대시 중, 이미 사망 등)로 막힌 경우는 기존처럼 그대로 통과시킵니다.
            return PlayerCombatController.IsExternallyInvulnerable;
        }

        protected virtual void OnPlayerHit(PlayerCombatController player) { }

        private bool TryResolveReflectedEnemyCollisionSweep()
        {
            if (!reflectedByPlayer || resolved || isDestroying)
            {
                return false;
            }

            Vector2 currentPosition = transform.position;
            Vector2 delta = currentPosition - lastWallCheckPosition;
            float radius = Mathf.Max(0.001f, projectileRadius);
            if (delta.sqrMagnitude > 0.000001f)
            {
                float distance = delta.magnitude;
                RaycastHit2D[] hits = Physics2D.CircleCastAll(
                    lastWallCheckPosition,
                    radius,
                    delta / distance,
                    distance);
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                for (int i = 0; i < hits.Length; i++)
                {
                    if (TryResolveReflectedEnemyCollider(hits[i].collider))
                    {
                        return true;
                    }
                }
            }

            Collider2D[] overlaps = Physics2D.OverlapCircleAll(currentPosition, radius);
            for (int i = 0; i < overlaps.Length; i++)
            {
                if (TryResolveReflectedEnemyCollider(overlaps[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveReflectedEnemyCollider(Collider2D other)
        {
            if (other == null || other.transform.IsChildOf(transform))
            {
                return false;
            }

            if (IsGroundCollider(other)
                || IsWallCollider(other)
                || IsArsonistGroundHazardCollider(other)
                || other.GetComponentInParent<PlayerCombatController>() != null
                || other.GetComponentInParent<PlayerProjectile>() != null
                || other.GetComponentInParent<EnemyProjectile>() != null)
            {
                return false;
            }

            if (other.GetComponentInParent<BossAI>() == null
                && other.GetComponentInParent<Minion>() == null)
            {
                return false;
            }

            return TryResolveReflectedEnemyHit(other.GetComponentInParent<Health>());
        }

        private static bool IsArsonistGroundHazardCollider(Collider2D collider)
        {
            return collider != null
                && (collider.GetComponentInParent<ArsonistOilPatch>() != null
                    || collider.GetComponentInParent<ArsonistFireArea>() != null
                    || collider.GetComponentInParent<ArsonistWaterArea>() != null);
        }

        private bool ShouldIgnoreBossCollision(BossAI hitBoss)
        {
            if (hitBoss == null)
            {
                return false;
            }

            if (reflectedByPlayer)
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

            if (reflectedByPlayer)
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

        private bool IsOwnerCollider(Collider2D other)
        {
            return ownerTransform != null
                && other != null
                && (other.transform == ownerTransform
                    || other.transform.IsChildOf(ownerTransform)
                    || ownerTransform.IsChildOf(other.transform));
        }

        private bool TryResolveReflectedEnemyHit(Health targetHealth)
        {
            if (!reflectedByPlayer || resolved || targetHealth == null)
            {
                return false;
            }

            Vector2 hitDirection = flightDirection.sqrMagnitude > 0.0001f
                ? flightDirection
                : Vector2.right;
            resolved = true;
            PlayerProjectile.TryApplyDamageToHealth(
                targetHealth,
                reflectedDamage,
                true,
                transform.position,
                hitDirection,
                projectileColor);
            DestroyProjectile();
            return true;
        }

    }
}
