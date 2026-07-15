using UnityEngine;
using Week14.Audio;
using Week14.Enemy;

namespace Week14.Combat
{
    // 스폰된 뒤 일정 시간(ChargeSeconds) 동안 패링되지 않으면 자동으로 사방(360도)에 총알을 뿌리고
    // 스스로 사라지는 탄환입니다. 패링 판정과 "패링되면 안 터짐" 취소는 EnemyProjectile의 기존
    // 충전(Charge) 기능을 그대로 재사용합니다 — 패링에 성공하면 충전이 끝나기 전에 파괴되어
    // OnProjectileLaunched 자체가 호출되지 않으므로, 폭발도 자동으로 취소됩니다.
    // 스폰(소환)은 이 프리팹을 매개변수로 넘겨 발사하는 아무 그래프 액션(예: AssassinSpawnRandomBombsAction)이
    // 담당하고, "터질 때 무슨 일이 일어나는지"(몇 발을, 어떤 탄으로, 어떤 연출로 뿌릴지)는 전부 이 탄 자신이 들고 있습니다.
    [AddComponentMenu("Week14/Combat/Parry Timed Radial Burst Projectile")]
    public sealed class ParryTimedRadialBurstProjectile : EnemyProjectile
    {
        [Header("남은 시간 게이지")]
        [SerializeField, Tooltip("남은 충전시간 비율을 표시할 원형 게이지 스프라이트 렌더러입니다. 비워두면 게이지를 표시하지 않습니다.")]
        private SpriteRenderer chargeGaugeRenderer;

        [Header("360도 폭발")]
        [Tooltip("터질 때 사방으로 발사할 총알의 프리팹/설정입니다.")]
        [SerializeField] private BossProjectileSettings burstProjectile = new();
        [Tooltip("터질 때 사방으로 흩어지는 총알 개수입니다.")]
        [SerializeField, Min(1)] private int burstBulletCount = 12;
        [SerializeField] private float burstStartAngleOffset;
        [SerializeField, BossGraphSfxId] private string burstSfxId;
        [SerializeField] private BossGraphEffectSettings burstEffects = new();

        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");

        private MaterialPropertyBlock chargeGaugePropertyBlock;

        protected override void OnProjectileInitialized()
        {
            SetChargeGaugeVisible(IsCharging);
        }

        protected override void OnProjectileChargeTick()
        {
            if (!IsCharging)
            {
                return;
            }

            SetChargeGaugeFill(1f - ChargeProgress01);
        }

        protected override void OnProjectileLaunched()
        {
            SetChargeGaugeVisible(false);
            FireBurst();
            DestroyFromOwner();
        }

        private void FireBurst()
        {
            Vector3 burstOrigin = transform.position;
            int count = Mathf.Max(1, burstBulletCount);
            float step = 360f / count;

            for (int i = 0; i < count; i++)
            {
                Vector2 shotDirection = AngleToDirection(burstStartAngleOffset + step * i);
                EnemyProjectile firedProjectile = FireBurstBullet(burstOrigin, shotDirection);
                if (firedProjectile == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(burstSfxId))
                {
                    SoundManager.PlaySfx(burstSfxId);
                }

                PlayBurstOriginEffects(burstOrigin);
                PlayBurstMuzzleFlash(burstOrigin, shotDirection);
            }
        }

        private EnemyProjectile FireBurstBullet(Vector3 burstOrigin, Vector2 shotDirection)
        {
            EnemyProjectile prefab = burstProjectile?.Prefab;
            if (prefab == null)
            {
                return null;
            }

            return Spawn(
                prefab,
                OwnerBullets,
                burstOrigin,
                shotDirection,
                burstProjectile.BulletDamage,
                0f,
                burstProjectile.Speed,
                burstProjectile.Lifetime,
                burstProjectile.Radius,
                ProjectileColor,
                burstProjectile.TrailSeconds,
                burstProjectile.TrailWidthMultiplier,
                false,
                0f,
                0f);
        }

        private void PlayBurstOriginEffects(Vector3 burstOrigin)
        {
            BossGraphParticleEffectSettings explosion = burstEffects?.Explosion;
            if (explosion != null && explosion.Enabled)
            {
                ProjectileVfx.PlayHogExplosion(burstOrigin, explosion.Color, explosion.Scale, explosion.Count);
            }

            BossGraphParticleEffectSettings smoke = burstEffects?.Smoke;
            if (smoke != null && smoke.Enabled)
            {
                ProjectileVfx.PlayHogSmokeBurst(burstOrigin, smoke.Color, smoke.Scale, smoke.Count);
            }
        }

        private void PlayBurstMuzzleFlash(Vector3 burstOrigin, Vector2 shotDirection)
        {
            BossGraphParticleEffectSettings muzzleFlash = burstEffects?.MuzzleFlash;
            if (muzzleFlash == null || !muzzleFlash.Enabled)
            {
                return;
            }

            ProjectileVfx.PlayMuzzleFlash(burstOrigin, shotDirection, muzzleFlash.Color, muzzleFlash.Scale);
        }

        private static Vector2 AngleToDirection(float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private void SetChargeGaugeVisible(bool visible)
        {
            if (chargeGaugeRenderer != null)
            {
                chargeGaugeRenderer.enabled = visible;
            }
        }

        private void SetChargeGaugeFill(float remainingRatio)
        {
            if (chargeGaugeRenderer == null)
            {
                return;
            }

            chargeGaugePropertyBlock ??= new MaterialPropertyBlock();
            chargeGaugeRenderer.GetPropertyBlock(chargeGaugePropertyBlock);
            chargeGaugePropertyBlock.SetFloat(FillAmountId, remainingRatio);
            chargeGaugeRenderer.SetPropertyBlock(chargeGaugePropertyBlock);
        }
    }
}
