using System.Collections;
using UnityEngine;
using Week14.Audio;
using Week14.Enemy;

namespace Week14.Combat
{
    internal sealed class PlayerDamageReceiver
    {
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

        private readonly PlayerCombatController.PlayerCombatContext context;
        private Coroutine hitStopRoutine;
        private float hitStopPreviousTimeScale = 1f;
        private float nextEnemyBodyContactDamageAt;
        private float enemyBodyContactStaggerEndsAt;
        private float bodyHitColorEndsAt;
        private MaterialPropertyBlock bodyFlashPropertyBlock;

        internal PlayerDamageReceiver(PlayerCombatController.PlayerCombatContext context)
        {
            this.context = context;
        }

        internal bool IsBodyContactStaggered => Time.time < enemyBodyContactStaggerEndsAt;

        internal void TryReceiveEnemyBodyContact(Collider2D other, Vector2 hitPosition)
        {
            PlayerCombatConfig config = context.Config;
            if (other == null || config == null || Time.time < nextEnemyBodyContactDamageAt)
            {
                return;
            }

            if (!IsEnemyBodyContact(other))
            {
                return;
            }

            Vector2 playerPosition = context.PlayerTransform.position;
            Vector2 hitDirection = playerPosition - hitPosition;
            if (hitDirection.sqrMagnitude <= 0.0001f)
            {
                hitDirection = playerPosition - (Vector2)other.transform.position;
            }

            if (hitDirection.sqrMagnitude <= 0.0001f)
            {
                hitDirection = Vector2.right;
            }

            if (ReceiveAttack(config.EnemyBodyContactBulletDamage, hitPosition, hitDirection.normalized))
            {
                ApplyEnemyBodyContactKnockback(hitDirection.normalized);
                nextEnemyBodyContactDamageAt = Time.time + config.EnemyBodyContactCooldownSeconds;
            }
        }

        internal bool ReceiveAttack(int bulletDamage)
        {
            return ReceiveAttack(bulletDamage, context.PlayerTransform.position, Vector2.right);
        }

        internal bool ReceiveAttack(int bulletDamage, Vector3 hitPosition, Vector2 hitDirection)
        {
            PlayerCombatConfig config = context.Config;
            Health health = context.Health;
            BulletGauge bullets = context.Bullets;

            if (context.IsExecuting || context.IsDashing || health == null || health.IsDead || config == null)
            {
                return false;
            }

            if (bullets == null || bullets.IsEmpty)
            {
                health.Kill();
            }
            else
            {
                bullets.TrySpend(Mathf.Clamp(bulletDamage, 1, bullets.CurrentBullets), BulletChangeSource.Hit);
                SoundManager.PlaySfx("BulletLoss");
                PlayHitStop();
            }

            FlashBodyHitColor();
            ProjectileVfx.PlayPlayerAttackImpact(
                hitPosition,
                hitDirection,
                config.PlayerBodyHitColor,
                config.PlayerHitSparkCount,
                config.PlayerHitBackSparkCount,
                config.PlayerHitFlameCount,
                config.PlayerHitEffectScale);
            context.CameraFollow?.PlayImpact(hitDirection, 0.16f, 0.18f, 0.1f);
            return true;
        }

        internal void UpdateBodyColor(bool force = false)
        {
            SpriteRenderer[] bodyRenderers = context.BodyRenderers;
            PlayerCombatConfig config = context.Config;
            if (bodyRenderers == null || bodyRenderers.Length == 0 || config == null)
            {
                return;
            }

            BulletGauge bullets = context.Bullets;
            bool flashing = Time.time < bodyHitColorEndsAt;
            Color? overrideColor = !flashing && bullets != null && bullets.IsEmpty
                ? config.PlayerBodyBulletEmptyColor
                : null;
            float flashAmount = flashing ? 1f : 0f;

            bodyFlashPropertyBlock ??= new MaterialPropertyBlock();

            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                SpriteRenderer renderer = bodyRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Color targetColor = overrideColor ?? GetBodyBaseColor(i);
                targetColor.a = renderer.color.a;
                if (force || renderer.color != targetColor)
                {
                    renderer.color = targetColor;
                }

                renderer.GetPropertyBlock(bodyFlashPropertyBlock);
                bodyFlashPropertyBlock.SetColor(FlashColorId, config.PlayerBodyHitColor);
                bodyFlashPropertyBlock.SetFloat(FlashAmountId, flashAmount);
                renderer.SetPropertyBlock(bodyFlashPropertyBlock);
            }
        }

        internal void StopHitStop()
        {
            if (hitStopRoutine == null)
            {
                return;
            }

            context.CoroutineHost.StopCoroutine(hitStopRoutine);
            PlayerCombatConfig config = context.Config;
            if (Mathf.Approximately(Time.timeScale, config != null ? config.HitStopTimeScale : Time.timeScale))
            {
                Time.timeScale = hitStopPreviousTimeScale;
            }

            hitStopRoutine = null;
        }

        private void ApplyEnemyBodyContactKnockback(Vector2 direction)
        {
            PlayerCombatConfig config = context.Config;
            Rigidbody2D body = context.Body;
            if (body == null || config == null)
            {
                return;
            }

            float staggerSeconds = Mathf.Max(0f, config.EnemyBodyContactStaggerSeconds);
            enemyBodyContactStaggerEndsAt = Mathf.Max(enemyBodyContactStaggerEndsAt, Time.time + staggerSeconds);
            body.linearVelocity = direction * Mathf.Max(0f, config.EnemyBodyContactKnockbackSpeed);
        }

        private bool IsEnemyBodyContact(Collider2D other)
        {
            if (other.GetComponentInParent<EnemyProjectile>() != null)
            {
                return false;
            }

            Drone drone = other.GetComponentInParent<Drone>();
            if (drone != null)
            {
                return !drone.SuppressesBodyContactDamage;
            }

            BossAI boss = other.GetComponentInParent<BossAI>();
            return boss != null && !boss.IsFinalDeathSequencePlaying && !context.IsWaitingForVictoryPanel;
        }

        private void FlashBodyHitColor()
        {
            PlayerCombatConfig config = context.Config;
            if (config == null)
            {
                return;
            }

            bodyHitColorEndsAt = Time.time + config.BodyHitColorSeconds;
            UpdateBodyColor(true);
        }

        private void PlayHitStop()
        {
            PlayerCombatConfig config = context.Config;
            if (config == null || config.HitStopSeconds <= 0f)
            {
                return;
            }

            if (hitStopRoutine == null)
            {
                hitStopPreviousTimeScale = Time.timeScale;
            }
            else
            {
                context.CoroutineHost.StopCoroutine(hitStopRoutine);
            }

            Time.timeScale = config.HitStopTimeScale;
            hitStopRoutine = context.CoroutineHost.StartCoroutine(HitStopRoutine(config.HitStopTimeScale, config.HitStopSeconds));
        }

        private IEnumerator HitStopRoutine(float appliedTimeScale, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);

            if (Mathf.Approximately(Time.timeScale, appliedTimeScale))
            {
                Time.timeScale = hitStopPreviousTimeScale;
            }

            hitStopRoutine = null;
        }

        private Color GetBodyBaseColor(int index)
        {
            Color[] bodyBaseColors = context.BodyBaseColors;
            return bodyBaseColors != null && index >= 0 && index < bodyBaseColors.Length
                ? bodyBaseColors[index]
                : Color.white;
        }
    }
}
