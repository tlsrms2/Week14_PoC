using System.Collections;
using UnityEngine;
using Week14.Input;

namespace Week14.Combat
{
    internal sealed class PlayerDashController
    {
        private readonly PlayerCombatController.PlayerCombatContext context;
        private Coroutine dashRoutine;

        internal PlayerDashController(PlayerCombatController.PlayerCombatContext context)
        {
            this.context = context;
        }

        internal bool IsDashing { get; private set; }
        internal float AutoParryRadius { get; private set; }

        internal bool TryDash(float distance, float duration, float autoParryRadius)
        {
            if (IsDashing || context.Body == null || distance <= 0f || duration <= 0f)
            {
                return false;
            }

            AutoParryRadius = autoParryRadius;
            dashRoutine = context.CoroutineHost.StartCoroutine(DashRoutine(GetDashDirection(), distance / duration, duration));
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

            Rigidbody2D body = context.Body;
            float peakSpeed = averageSpeed * 2f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float speed = Mathf.Lerp(peakSpeed, 0f, elapsed / duration);
                body.linearVelocity = direction * speed;
                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            body.linearVelocity = Vector2.zero;
            IsDashing = false;
            dashRoutine = null;
        }
    }
}
