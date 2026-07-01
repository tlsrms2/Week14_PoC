using System.Collections;
using UnityEngine;
using Week14.Input;

namespace Week14.Combat
{
    internal sealed class PlayerDashController
    {
        private readonly PlayerCombatController.PlayerCombatContext context;

        internal PlayerDashController(PlayerCombatController.PlayerCombatContext context)
        {
            this.context = context;
        }

        private const string EnemyLayerName = "Enemy";
        private const string EnemyBulletLayerName = "EnemyBullet";

        internal bool IsDashing { get; private set; }
        internal float AutoParryRadius { get; private set; }
        internal RollSkillVfxSettings VfxSettings { get; private set; } = RollSkillVfxSettings.Default;

        internal bool TryDash(float distance, float duration)
        {
            return TryDash(distance, duration, 0f);
        }

        internal bool TryDash(float distance, float duration, float autoParryRadius)
        {
            return TryDash(distance, duration, autoParryRadius, RollSkillVfxSettings.Default);
        }

        internal bool TryDash(float distance, float duration, float autoParryRadius, RollSkillVfxSettings vfxSettings)
        {
            if (IsDashing || context.Body == null || distance <= 0f || duration <= 0f
                || (context.Health != null && context.Health.IsDead))
            {
                return false;
            }

            AutoParryRadius = autoParryRadius;
            VfxSettings = vfxSettings.Sanitized;
            context.CoroutineHost.StartCoroutine(DashRoutine(GetDashDirection(), distance / duration, duration));
            return true;
        }

        private Vector2 GetDashDirection()
        {
            Vector2 moveInput = GameInput.Move;
            if (moveInput.sqrMagnitude > 0.0001f)
            {
                return moveInput.normalized;
            }

            Rigidbody2D body = context.Body;
            return body != null && body.linearVelocity.sqrMagnitude > 0.0001f
                ? body.linearVelocity.normalized
                : (Vector2)context.PlayerTransform.up;
        }

        private IEnumerator DashRoutine(Vector2 direction, float averageSpeed, float duration)
        {
            IsDashing = true;
            context.Visual?.PlayRoll();

            int playerLayer = context.PlayerGameObject.layer;
            int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
            int enemyBulletLayer = LayerMask.NameToLayer(EnemyBulletLayerName);
            bool ignoreEnemy = enemyLayer >= 0;
            bool ignoreEnemyBullet = enemyBulletLayer >= 0;

            if (ignoreEnemy) Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, true);
            if (ignoreEnemyBullet) Physics2D.IgnoreLayerCollision(playerLayer, enemyBulletLayer, true);

            Rigidbody2D body = context.Body;
            float peakSpeed = averageSpeed * 2f;
            float elapsed = 0f;
            float nextAfterimageAt = 0f;
            while (elapsed < duration)
            {
                if (elapsed >= nextAfterimageAt)
                {
                    PlayerDashVfx.SpawnRollAfterimage(
                        context.CoroutineHost,
                        context.BodyRenderers,
                        VfxSettings.AfterimageSeconds,
                        VfxSettings.AfterimageColor);
                    nextAfterimageAt += VfxSettings.AfterimageInterval;
                }

                float speed = Mathf.Lerp(peakSpeed, 0f, elapsed / duration);
                body.linearVelocity = GroundMovementConstraint.ClampVelocity(body, direction * speed);
                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            body.linearVelocity = Vector2.zero;
            if (ignoreEnemy) Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, false);
            if (ignoreEnemyBullet) Physics2D.IgnoreLayerCollision(playerLayer, enemyBulletLayer, false);
            IsDashing = false;
        }
    }
}
