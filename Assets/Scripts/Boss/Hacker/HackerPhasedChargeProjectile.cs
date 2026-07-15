using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [AddComponentMenu("Week14/Boss/Hacker Phased Charge Projectile")]
    public sealed class HackerPhasedChargeProjectile : EnemyProjectile
    {
        [SerializeField, Range(0f, 1f)] private float chargeAlpha = 0.3f;

        private SpriteRenderer[] spriteRenderers;
        private Collider2D[] collisionColliders;
        private Color[] launchColors;

        protected override void OnProjectileAwake()
        {
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            collisionColliders = GetComponentsInChildren<Collider2D>(true);
        }

        protected override void OnProjectileInitialized()
        {
            CaptureCurrentColors();
            ApplyChargeState(IsCharging);
        }

        protected override void OnProjectileChargeTick()
        {
            ApplyChargeState(true);
        }

        protected override void OnProjectileLaunched()
        {
            CaptureCurrentColors();
            ApplyChargeState(false);
        }

        protected override bool CanHitPlayer(PlayerCombatController player)
        {
            return !IsCharging && base.CanHitPlayer(player);
        }

        private void CaptureCurrentColors()
        {
            if (spriteRenderers == null)
            {
                return;
            }

            launchColors = new Color[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    launchColors[i] = spriteRenderers[i].color;
                }
            }
        }

        private void ApplyChargeState(bool isCharging)
        {
            SetCollisionEnabled(!isCharging);
            SetSpriteAlpha(isCharging ? chargeAlpha : 1f);
        }

        private void SetCollisionEnabled(bool enabled)
        {
            if (collisionColliders == null)
            {
                return;
            }

            for (int i = 0; i < collisionColliders.Length; i++)
            {
                if (collisionColliders[i] != null)
                {
                    collisionColliders[i].enabled = enabled;
                }
            }
        }

        private void SetSpriteAlpha(float alphaMultiplier)
        {
            if (spriteRenderers == null)
            {
                return;
            }

            float clampedAlpha = Mathf.Clamp01(alphaMultiplier);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = spriteRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (IsParryLockOnIndicatorRenderer(renderer))
                {
                    continue;
                }

                Color color = launchColors != null && i < launchColors.Length
                    ? launchColors[i]
                    : renderer.color;
                color.a *= clampedAlpha;
                renderer.color = color;
            }
        }
    }
}
