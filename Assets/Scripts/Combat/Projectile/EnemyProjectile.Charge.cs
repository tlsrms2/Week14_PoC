using UnityEngine;

namespace Week14.Combat
{
    public partial class EnemyProjectile
    {
        private void TickCharge()
        {
            SnapToChargeAnchor();

            if (aimAtPlayerWhileCharging)
            {
                AimAtPlayerWhileCharging();
            }

            if (body != null)
            {
                body.linearVelocity = flightDirection * chargeDriftSpeed;
            }

            UpdateChargeGrowth();
            UpdateHomingChargeBlink();
            UpdateChargeTrail();
            UpdateChargeVfx();
            UpdatePathIndicatorPreview();
            OnProjectileChargeTick();
            if (isDestroying)
            {
                return;
            }

            if (Time.time < chargeEndsAt)
            {
                return;
            }

            SnapToChargeAnchor();
            if (TryReplaceWithLaunchPrefab())
            {
                return;
            }

            launched = true;
            chargeAnchor = null;
            ApplyProjectileColor(launchedColor);
            if (growScaleWhileCharging)
            {
                transform.localScale = chargeGrowthEndScale;
                baseLocalScale = transform.localScale;
            }

            if (homingEnabled)
            {
                AimAtPlayerWhileCharging();
            }
            else if (aimAtPlayerOnLaunch)
            {
                AimAtPlayerWhileCharging(aimAtPlayerOnLaunchSpreadDegrees);
            }

            if (playSmokeOnLaunch)
            {
                ProjectileVfx.PlayHogSmokeBurst(transform.position, launchSmokeColor, launchSmokeScale, 18);
            }

            radialSplitAt = splitRadiallyOnLaunch ? Time.time + radialSplitDelaySeconds : 0f;

            SetChargeVfxVisible(false);
            BeginPathIndicator();
            if (body != null)
            {
                body.linearVelocity = flightDirection * projectileSpeed;
            }

            Launched?.Invoke(this);
            OnProjectileLaunched();
        }
        private void SnapToChargeAnchor()
        {
            if (chargeAnchor == null)
            {
                return;
            }

            Vector3 position = chargeAnchor.position;
            position.z = transform.position.z;
            transform.position = position;
            if (body != null)
            {
                body.position = position;
            }
        }

        private void UpdateChargeGrowth()
        {
            if (!growScaleWhileCharging || projectileChargeSeconds <= 0f)
            {
                return;
            }

            float t = 1f - Mathf.Clamp01((chargeEndsAt - Time.time) / projectileChargeSeconds);
            t = Mathf.SmoothStep(0f, 1f, t);
            transform.localScale = Vector3.Lerp(chargeGrowthStartScale, chargeGrowthEndScale, t);
        }

        private void UpdateHomingChargeBlink()
        {
            if (!homingEnabled || projectileChargeSeconds <= 0f)
            {
                return;
            }

            float remainingRatio = Mathf.Clamp01((chargeEndsAt - Time.time) / projectileChargeSeconds);
            if (remainingRatio <= HomingChargeSolidColorRemainingRatio)
            {
                ApplyProjectileColor(homingBlinkColor);
                return;
            }

            float blinkRate = Mathf.Lerp(HomingChargeBlinkMaxRate, HomingChargeBlinkMinRate, remainingRatio);
            homingBlinkPhase += Time.deltaTime * blinkRate;
            Color nextColor = Mathf.Repeat(homingBlinkPhase, 1f) >= 0.5f
                ? homingBlinkColor
                : chargingColor;
            ApplyProjectileColor(nextColor);
        }

        private void UpdateChargeTrail()
        {
            if (projectileTrail == null)
            {
                projectileTrail = GetComponent<TrailRenderer>();
            }

            if (projectileTrail == null)
            {
                return;
            }

            projectileTrail.emitting = true;
            projectileTrail.AddPosition(transform.position);
        }

        private void BeginTrail()
        {
            projectileTrail = GetComponent<TrailRenderer>();
            if (projectileTrail == null)
            {
                return;
            }

            projectileTrail.Clear();
            projectileTrail.emitting = true;
            projectileTrail.AddPosition(transform.position);
        }

    }
}
