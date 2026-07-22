using UnityEngine;

namespace Week14.Combat
{
    [AddComponentMenu("Week14/Combat/Unparryable Enemy Projectile")]
    public sealed class UnparryableEnemyProjectile : EnemyProjectile
    {
        protected override bool AllowsReflectionWhenInterceptDisabled => true;

        protected override void OnProjectileAwake()
        {
            DisableParry();
        }

        protected override void OnProjectileInitialized()
        {
            DisableParry();
        }

        protected override void OnProjectileLaunched()
        {
            DisableParry();
        }

        private void DisableParry()
        {
            ConfigureInterceptable(false);
        }
    }
}
