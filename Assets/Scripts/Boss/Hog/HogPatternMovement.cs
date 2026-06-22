using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    internal sealed class HogPatternMovement
    {
        public IEnumerator WaitSeconds(
            float seconds,
            Action onTick,
            Func<bool> isExecutionPaused,
            Action stop)
        {
            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                if (isExecutionPaused?.Invoke() == true)
                {
                    stop?.Invoke();
                    yield return null;
                    continue;
                }

                onTick?.Invoke();
                remaining -= Time.deltaTime;
                yield return null;
            }
        }

        public IEnumerator WaitWhileExecutionPaused(Func<bool> isExecutionPaused, Action stop)
        {
            while (isExecutionPaused?.Invoke() == true)
            {
                stop?.Invoke();
                yield return null;
            }
        }

        public void MoveTowardPlayer(
            Transform player,
            Rigidbody2D body,
            Transform origin,
            float moveSpeed,
            float speedMultiplier)
        {
            if (player == null || body == null || origin == null)
            {
                return;
            }

            Vector2 direction = (Vector2)player.position - (Vector2)origin.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            body.linearVelocity = direction.normalized * (moveSpeed * Mathf.Max(0f, speedMultiplier));
        }
    }
}
