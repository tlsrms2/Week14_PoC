using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class HackerGroundGunTurretAction : BossAction, IBossActionDurationProvider
    {
        [Header("Projectile")]
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();

        [Header("Turret")]
        [SerializeField] private string muzzleChildPath;
        [SerializeField] private Vector2 muzzleLocalOffset;
        [SerializeField] private bool rotateGunTowardPlayer = true;
        [SerializeField, Min(0f)] private float windupSeconds = 0.2f;
        [SerializeField, Min(0.05f)] private float activeSeconds = 3f;
        [SerializeField, Min(0.01f)] private float fireInterval = 0.3f;
        [SerializeField, Min(1)] private int maxShotCount = 8;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.2f;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is not HackerBossAI hacker
                || !TryGetTurretWeapon(hacker, out HackerThrownWeapon turretWeapon))
            {
                yield break;
            }

            yield return HackerMeleeAttackAction.Wait(context, windupSeconds);
            float elapsed = 0f;
            float nextFireAt = 0f;
            int shotCount = 0;
            while (elapsed < activeSeconds && shotCount < maxShotCount && turretWeapon != null)
            {
                if (context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                Vector2 direction = context.GetDirectionToPlayer(turretWeapon.transform.position);
                if (rotateGunTowardPlayer)
                {
                    turretWeapon.transform.right = direction;
                }

                Transform muzzle = turretWeapon.GetChildTransform(muzzleChildPath) ?? turretWeapon.transform;
                Vector3 origin = muzzle.position + muzzle.TransformVector(muzzleLocalOffset);
                direction = context.GetDirectionToPlayer(origin);

                if (elapsed >= nextFireAt)
                {
                    FireProjectile(context, origin, direction, turretWeapon);
                    context.PlaySfx(fireSfxId);
                    shotCount++;
                    nextFireAt += Mathf.Max(0.01f, fireInterval);
                }

                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, windupSeconds)
                + Mathf.Max(0.05f, activeSeconds)
                + Mathf.Max(0f, recoverySeconds);
            return true;
        }

        private static bool TryGetTurretWeapon(HackerBossAI hacker, out HackerThrownWeapon weapon)
        {
            if (hacker.TryGetGroundedWeapon(HackerThrownWeaponType.Gun, out weapon)
                && weapon != null
                && weapon.IsGrounded)
            {
                return true;
            }

            if (hacker.TryGetGroundedWeapon(HackerThrownWeaponType.Bayonet, out weapon)
                && weapon != null
                && weapon.IsGrounded)
            {
                return true;
            }

            weapon = null;
            return false;
        }

        private void FireProjectile(BossActionContext context, Vector3 origin, Vector2 direction, HackerThrownWeapon gun)
        {
            BossProjectileSettings settings = context.ResolveGraphProjectileSettings(projectileName) ?? projectile;
            if (settings?.Prefab == null)
            {
                return;
            }

            EnemyProjectile firedProjectile = context.FireProjectile(settings, origin, direction, 0f, projectileName: projectileName);
            IgnoreGunCollisions(firedProjectile, gun);
        }

        private static void IgnoreGunCollisions(EnemyProjectile projectile, HackerThrownWeapon gun)
        {
            if (projectile == null || gun == null)
            {
                return;
            }

            Collider2D[] projectileColliders = projectile.GetComponentsInChildren<Collider2D>(true);
            Collider2D[] gunColliders = gun.GetComponentsInChildren<Collider2D>(true);
            for (int projectileIndex = 0; projectileIndex < projectileColliders.Length; projectileIndex++)
            {
                Collider2D projectileCollider = projectileColliders[projectileIndex];
                if (projectileCollider == null)
                {
                    continue;
                }

                for (int gunIndex = 0; gunIndex < gunColliders.Length; gunIndex++)
                {
                    Collider2D gunCollider = gunColliders[gunIndex];
                    if (gunCollider != null)
                    {
                        Physics2D.IgnoreCollision(projectileCollider, gunCollider, true);
                    }
                }
            }
        }
    }
}
