using System;
using System.Collections;
using UnityEngine;
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
            if (turretWeapon == null || !turretWeapon.IsGrounded)
            {
                yield break;
            }

            HackerGroundGunTurretController controller = turretWeapon.GetComponent<HackerGroundGunTurretController>();
            if (controller == null)
            {
                controller = turretWeapon.gameObject.AddComponent<HackerGroundGunTurretController>();
            }

            controller.Activate(
                context,
                projectileName,
                projectile,
                muzzleChildPath,
                muzzleLocalOffset,
                rotateGunTowardPlayer,
                activeSeconds,
                fireInterval,
                maxShotCount,
                fireSfxId);
            yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, windupSeconds)
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

    }
}
