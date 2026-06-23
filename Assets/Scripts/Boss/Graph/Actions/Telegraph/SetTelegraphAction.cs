using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class SetTelegraphAction : BossAction
    {
        private enum AimMode
        {
            None,
            Player,
            FixedAngle
        }

        [SerializeField] private string telegraphPath;
        [SerializeField] private string aimOriginPath;
        [SerializeField] private bool active = true;
        [SerializeField] private AimMode aimMode = AimMode.Player;
        [SerializeField] private bool trackPlayer;
        [SerializeField] private bool flipYByFacing = true;
        [SerializeField] private float angleDegrees;
        [SerializeField, Min(0f)] private float duration;
        [SerializeField] private bool deactivateAfterDuration;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            context.SetBossChildActive(telegraphPath, active);
            RotateTelegraph(context);

            if (duration <= 0f)
            {
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                context.Stop();
                if (trackPlayer)
                {
                    RotateTelegraph(context);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (deactivateAfterDuration)
            {
                context.SetBossChildActive(telegraphPath, false);
            }
        }

        private void RotateTelegraph(BossActionContext context)
        {
            Vector2 direction = GetAimDirection(context);
            if (direction.sqrMagnitude > 0.0001f)
            {
                context.RotateBossChildRight(telegraphPath, direction, flipYByFacing);
            }
        }

        private Vector2 GetAimDirection(BossActionContext context)
        {
            return aimMode switch
            {
                AimMode.Player => context.GetDirectionToPlayer(GetAimOrigin(context)),
                AimMode.FixedAngle => BossActionContext.AngleToDirection(angleDegrees),
                _ => Vector2.zero
            };
        }

        private Vector3 GetAimOrigin(BossActionContext context)
        {
            if (string.IsNullOrWhiteSpace(aimOriginPath))
            {
                return context.GetBossChildPosition(telegraphPath);
            }

            return context.GetBossChildPosition(aimOriginPath);
        }
    }
}
