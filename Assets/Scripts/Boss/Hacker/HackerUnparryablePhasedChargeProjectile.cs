using UnityEngine;

namespace Week14.Enemy
{
    [AddComponentMenu("Week14/Boss/Hacker Unparryable Phased Charge Projectile")]
    public sealed class HackerUnparryablePhasedChargeProjectile : HackerPhasedChargeProjectile
    {
        protected override bool AllowsReflectionWhenInterceptDisabled => true;

        protected override void OnProjectileAwake()
        {
            base.OnProjectileAwake();
            DisableParry();
        }

        protected override void OnProjectileInitialized()
        {
            base.OnProjectileInitialized();
            DisableParry();
        }

        protected override void OnProjectileLaunched()
        {
            base.OnProjectileLaunched();
            DisableParry();
        }

        private void DisableParry()
        {
            ConfigureInterceptable(false);
        }
    }
}
