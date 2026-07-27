using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    internal sealed class HackerGroundGunTurretController : MonoBehaviour
    {
        private BossActionContext context;
        private HackerThrownWeapon weapon;
        private string projectileName;
        private BossProjectileSettings projectileFallback;
        private string muzzleChildPath;
        private Vector2 muzzleLocalOffset;
        private bool rotateTowardPlayer;
        private float activeSeconds;
        private float fireInterval;
        private int maxShotCount;
        private GameObject fireEffectPrefab;
        private float fireEffectRotationOffsetDegrees;
        private float fireEffectScale;
        private string fireSfxId;
        private float elapsed;
        private float nextFireAt;
        private int shotCount;

        internal void Activate(
            BossActionContext nextContext,
            string nextProjectileName,
            BossProjectileSettings nextProjectileFallback,
            string nextMuzzleChildPath,
            Vector2 nextMuzzleLocalOffset,
            bool nextRotateTowardPlayer,
            float nextActiveSeconds,
            float nextFireInterval,
            int nextMaxShotCount,
            GameObject nextFireEffectPrefab,
            float nextFireEffectRotationOffsetDegrees,
            float nextFireEffectScale,
            string nextFireSfxId)
        {
            context = nextContext;
            weapon = GetComponent<HackerThrownWeapon>();
            projectileName = nextProjectileName;
            projectileFallback = nextProjectileFallback;
            muzzleChildPath = nextMuzzleChildPath;
            muzzleLocalOffset = nextMuzzleLocalOffset;
            rotateTowardPlayer = nextRotateTowardPlayer;
            activeSeconds = Mathf.Max(0.05f, nextActiveSeconds);
            fireInterval = Mathf.Max(0.01f, nextFireInterval);
            maxShotCount = Mathf.Max(1, nextMaxShotCount);
            fireEffectPrefab = nextFireEffectPrefab;
            fireEffectRotationOffsetDegrees = nextFireEffectRotationOffsetDegrees;
            fireEffectScale = Mathf.Max(0.01f, nextFireEffectScale);
            fireSfxId = HackerSfxIds.Resolve(nextFireSfxId, HackerSfxIds.BossNormalShot);
            elapsed = 0f;
            nextFireAt = 0f;
            shotCount = 0;
            enabled = true;
        }

        private void Update()
        {
            if (context?.Boss == null || weapon == null || !weapon.IsGrounded)
            {
                Destroy(this);
                return;
            }

            if (context.IsExecutionPaused)
            {
                return;
            }

            if (elapsed >= activeSeconds || shotCount >= maxShotCount)
            {
                Destroy(this);
                return;
            }

            if (elapsed >= nextFireAt)
            {
                FireShot();
                shotCount++;
                nextFireAt += fireInterval;
            }

            elapsed += EnemyTimeScale.DeltaTime;
        }

        private void FireShot()
        {
            if (rotateTowardPlayer || weapon.IsGrounded)
            {
                weapon.FacePlayer();
            }

            Transform muzzle = weapon.GetChildTransform(muzzleChildPath) ?? weapon.transform;
            Vector3 origin = muzzle.position + muzzle.TransformVector(muzzleLocalOffset);
            Vector2 direction = context.GetDirectionToPlayer(origin);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            BossProjectileSettings settings = context.ResolveGraphProjectileSettings(projectileName) ?? projectileFallback;
            if (settings?.Prefab == null)
            {
                return;
            }

            EnemyProjectile firedProjectile = context.FireProjectile(
                settings,
                origin,
                direction,
                0f,
                projectileName: projectileName);
            if (firedProjectile != null)
            {
                float angleDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
                    + fireEffectRotationOffsetDegrees;
                ProjectileVfx.PlayPrefab(
                    fireEffectPrefab,
                    origin,
                    Quaternion.Euler(0f, 0f, angleDegrees),
                    null,
                    fireEffectScale,
                    false);
                IgnoreWeaponCollisions(firedProjectile);
                context.PlaySfx(fireSfxId);
            }
        }

        private void IgnoreWeaponCollisions(EnemyProjectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            Collider2D[] projectileColliders = projectile.GetComponentsInChildren<Collider2D>(true);
            Collider2D[] weaponColliders = weapon.GetComponentsInChildren<Collider2D>(true);
            for (int projectileIndex = 0; projectileIndex < projectileColliders.Length; projectileIndex++)
            {
                Collider2D projectileCollider = projectileColliders[projectileIndex];
                if (projectileCollider == null)
                {
                    continue;
                }

                for (int weaponIndex = 0; weaponIndex < weaponColliders.Length; weaponIndex++)
                {
                    Collider2D weaponCollider = weaponColliders[weaponIndex];
                    if (weaponCollider != null)
                    {
                        Physics2D.IgnoreCollision(projectileCollider, weaponCollider, true);
                    }
                }
            }
        }
    }
}
