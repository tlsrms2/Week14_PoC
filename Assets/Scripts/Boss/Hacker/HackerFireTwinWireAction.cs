using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class HackerFireTwinWireAction : BossAction, IBossActionDurationProvider
    {
        [SerializeField, BossGraphBossChildPath] private string launchOriginPath;
        [SerializeField] private string animationTriggerName = "FireTwinWire";
        [SerializeField, Min(0f)] private float windupSeconds = 0.25f;
        [SerializeField, Range(1f, 359f)] private float wireAngleDegrees = 120f;
        [SerializeField, Min(0.05f)] private float maxFlightSeconds = 1f;
        [SerializeField, Min(0.05f)] private float wallAttachedSeconds = 4f;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.2f;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is not HackerBossAI hacker)
            {
                yield break;
            }

            Transform launchOrigin = context.GetBossChildTransform(launchOriginPath) ?? hacker.transform;
            Vector2 targetDirection = context.GetDirectionToPlayer(launchOrigin.position);
            hacker.FaceHorizontalDirection(targetDirection.x);
            using IDisposable facingLock = context.AcquireFacingLock();
            context.PlayAnimationTrigger(animationTriggerName);
            yield return HackerMeleeAttackAction.Wait(context, windupSeconds);

            launchOrigin = context.GetBossChildTransform(launchOriginPath) ?? hacker.transform;
            float halfAngle = wireAngleDegrees * 0.5f;
            FireWire(hacker, launchOrigin, Rotate(targetDirection, halfAngle));
            FireWire(hacker, launchOrigin, Rotate(targetDirection, -halfAngle));
            yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, windupSeconds) + Mathf.Max(0f, recoverySeconds);
            return true;
        }

        private void FireWire(HackerBossAI hacker, Transform launchOrigin, Vector2 direction)
        {
            HackerWireSettings wireSettings = hacker.WireSettings;
            HackerWire.CreatePersistentWallWire(
                hacker,
                launchOrigin,
                direction,
                wireSettings.FlightSpeed,
                maxFlightSeconds,
                wallAttachedSeconds,
                wireSettings.Width,
                wireSettings.HitRadius,
                wireSettings.Color,
                dissolveOnPlayerTouch: true);
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(
                direction.x * cos - direction.y * sin,
                direction.x * sin + direction.y * cos);
        }
    }
}
