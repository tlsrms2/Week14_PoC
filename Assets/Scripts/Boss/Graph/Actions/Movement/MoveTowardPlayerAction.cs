using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MoveTowardPlayerAction : BossAction, ISerializationCallbackReceiver
    {
        [SerializeField, Min(0f)] private float seconds = 1f;
        [SerializeField, Min(0f)] private float speedMultiplier = 1f;
        [SerializeField] private AnimationCurve speedCurve;
        [SerializeField, HideInInspector] private bool speedCurveInitialized;
        [SerializeField] private bool stopWhenFinished = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            float remaining = Mathf.Max(0f, seconds);
            float elapsed = 0f;
            while (remaining > 0f)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                context.MoveTowardPlayer(speedMultiplier, GetSpeedCurve(), elapsed, seconds);
                remaining -= Time.deltaTime;
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (stopWhenFinished)
            {
                context.Stop();
            }
        }

        public void OnBeforeSerialize()
        {
            EnsureSpeedCurve();
        }

        public void OnAfterDeserialize()
        {
            EnsureSpeedCurve();
        }

        private AnimationCurve GetSpeedCurve()
        {
            EnsureSpeedCurve();
            return speedCurve;
        }

        private void EnsureSpeedCurve()
        {
            if (speedCurveInitialized && speedCurve != null && speedCurve.length > 0)
            {
                return;
            }

            speedCurve = BossMoveSpeedCurve.CreateConstant();
            speedCurveInitialized = true;
        }
    }

    [Serializable]
    public sealed class MaintainPlayerDistanceAction : BossAction, ISerializationCallbackReceiver
    {
        [SerializeField, Min(0f)] private float durationSeconds = 1f;
        [SerializeField, Min(0.1f)] private float distance = 4f;
        [SerializeField, Min(0f)] private float tolerance = 0.15f;
        [SerializeField, Min(0f)] private float speedMultiplier = 1f;
        [SerializeField] private AnimationCurve speedCurve;
        [SerializeField, HideInInspector] private bool speedCurveInitialized;
        [SerializeField] private bool stopWhenFinished = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            float duration = Mathf.Max(0f, durationSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                context.MaintainPlayerDistance(distance, tolerance, speedMultiplier, GetSpeedCurve(), elapsed, duration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (stopWhenFinished)
            {
                context.Stop();
            }
        }

        public void OnBeforeSerialize()
        {
            EnsureSpeedCurve();
        }

        public void OnAfterDeserialize()
        {
            EnsureSpeedCurve();
        }

        private AnimationCurve GetSpeedCurve()
        {
            EnsureSpeedCurve();
            return speedCurve;
        }

        private void EnsureSpeedCurve()
        {
            if (speedCurveInitialized && speedCurve != null && speedCurve.length > 0)
            {
                return;
            }

            speedCurve = BossMoveSpeedCurve.CreateConstant();
            speedCurveInitialized = true;
        }
    }

    [Serializable]
    public sealed class MoveUntilPlayerDistanceAction : BossAction, ISerializationCallbackReceiver
    {
        [SerializeField, Min(0f), Tooltip("플레이어와의 거리가 이 값 이하가 되면 이동을 멈추고 다음으로 넘어갑니다.")] private float targetDistance = 3f;
        [SerializeField, Min(0f)] private float speedMultiplier = 1f;
        [SerializeField] private AnimationCurve speedCurve;
        [SerializeField, HideInInspector] private bool speedCurveInitialized;
        [SerializeField, Min(0f), Tooltip("0이면 시간 제한 없이 목표 거리에 도달할 때까지 계속 이동합니다.")] private float timeoutSeconds;
        [SerializeField] private bool stopWhenFinished = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null || context.Boss == null || context.Boss.Player == null)
            {
                yield break;
            }

            float elapsed = 0f;
            while (Vector2.Distance(context.Boss.transform.position, context.Boss.Player.position) > targetDistance)
            {
                if (timeoutSeconds > 0f && elapsed >= timeoutSeconds)
                {
                    break;
                }

                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                context.MoveTowardPlayer(speedMultiplier, GetSpeedCurve(), elapsed, 0f);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (stopWhenFinished)
            {
                context.Stop();
            }
        }

        public void OnBeforeSerialize()
        {
            EnsureSpeedCurve();
        }

        public void OnAfterDeserialize()
        {
            EnsureSpeedCurve();
        }

        private AnimationCurve GetSpeedCurve()
        {
            EnsureSpeedCurve();
            return speedCurve;
        }

        private void EnsureSpeedCurve()
        {
            if (speedCurveInitialized && speedCurve != null && speedCurve.length > 0)
            {
                return;
            }

            speedCurve = BossMoveSpeedCurve.CreateConstant();
            speedCurveInitialized = true;
        }
    }

    [Serializable]
    public sealed class StartMoveTowardPlayerAction : BossAction, ISerializationCallbackReceiver
    {
        [SerializeField, Min(0f)] private float durationSeconds;
        [SerializeField, Min(0f)] private float speedMultiplier = 1f;
        [SerializeField] private AnimationCurve speedCurve;
        [SerializeField, HideInInspector] private bool speedCurveInitialized;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            context.StartMoveTowardPlayer(speedMultiplier, GetSpeedCurve(), durationSeconds);
            if (durationSeconds > 0f)
            {
                yield return context.WaitSeconds(durationSeconds);
                context.StopMoveTowardPlayer();
            }

            yield break;
        }

        public void OnBeforeSerialize()
        {
            EnsureSpeedCurve();
        }

        public void OnAfterDeserialize()
        {
            EnsureSpeedCurve();
        }

        private AnimationCurve GetSpeedCurve()
        {
            EnsureSpeedCurve();
            return speedCurve;
        }

        private void EnsureSpeedCurve()
        {
            if (speedCurveInitialized && speedCurve != null && speedCurve.length > 0)
            {
                return;
            }

            speedCurve = BossMoveSpeedCurve.CreateConstant();
            speedCurveInitialized = true;
        }
    }

    [Serializable]
    public sealed class StartMoveAwayFromPlayerAction : BossAction, ISerializationCallbackReceiver
    {
        [SerializeField, Min(0f)] private float durationSeconds;
        [SerializeField, Min(0f)] private float speedMultiplier = 1f;
        [SerializeField] private AnimationCurve speedCurve;
        [SerializeField, HideInInspector] private bool speedCurveInitialized;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            context.StartMoveAwayFromPlayer(speedMultiplier, GetSpeedCurve(), durationSeconds);
            if (durationSeconds > 0f)
            {
                yield return context.WaitSeconds(durationSeconds);
                context.StopMoveTowardPlayer();
            }

            yield break;
        }

        public void OnBeforeSerialize()
        {
            EnsureSpeedCurve();
        }

        public void OnAfterDeserialize()
        {
            EnsureSpeedCurve();
        }

        private AnimationCurve GetSpeedCurve()
        {
            EnsureSpeedCurve();
            return speedCurve;
        }

        private void EnsureSpeedCurve()
        {
            if (speedCurveInitialized && speedCurve != null && speedCurve.length > 0)
            {
                return;
            }

            speedCurve = BossMoveSpeedCurve.CreateConstant();
            speedCurveInitialized = true;
        }
    }

    [Serializable]
    public sealed class StopMovementAction : BossAction
    {
        public override IEnumerator Execute(BossActionContext context)
        {
            context?.StopMoveTowardPlayer();
            yield break;
        }
    }

    [Serializable]
    public sealed class WanderAroundPlayerDistanceAction : BossAction
    {
        [SerializeField, Min(0f)] private float durationSeconds = 2f;
        [SerializeField, Min(0.1f)] private float minDistance = 2.5f;
        [SerializeField, Min(0.1f)] private float maxDistance = 4.5f;
        [SerializeField, Min(0f)] private float speedMultiplier = 1f;
        [SerializeField, Min(0.05f)] private float retargetInterval = 0.6f;
        [SerializeField, Min(0.05f)] private float arriveDistance = 0.25f;
        [SerializeField, Range(1f, 180f)] private float maxRetargetAngleDegrees = 70f;
        [SerializeField] private bool stopWhenFinished = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null || context.Boss == null || context.Boss.Body == null || context.Boss.Player == null)
            {
                yield break;
            }

            float elapsed = 0f;
            float nextRetargetAt = 0f;
            Vector2 playerStart = context.Boss.Player.position;
            Vector2 bossStart = context.Boss.Body.position;
            Vector2 startRadial = bossStart - playerStart;
            float safeMin = Mathf.Min(minDistance, maxDistance);
            float safeMax = Mathf.Max(minDistance, maxDistance);
            float targetAngle = DirectionToAngleOrFallback(startRadial, UnityEngine.Random.Range(0f, 360f));
            float targetDistance = Mathf.Clamp(startRadial.magnitude, safeMin, safeMax);
            Vector2 target = BuildPlayerRelativeTarget(playerStart, targetAngle, targetDistance);
            bool hasTarget = false;

            while (elapsed < durationSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                Vector2 playerPosition = context.Boss.Player.position;
                Vector2 bossPosition = context.Boss.Body.position;
                Vector2 radial = bossPosition - playerPosition;
                float distance = radial.magnitude;
                bool outsideDistanceBand = distance < safeMin || distance > safeMax;
                target = BuildPlayerRelativeTarget(playerPosition, targetAngle, targetDistance);
                if (outsideDistanceBand)
                {
                    targetAngle = DirectionToAngleOrFallback(radial, targetAngle);
                    targetDistance = Mathf.Clamp(distance, safeMin, safeMax);
                    target = BuildPlayerRelativeTarget(playerPosition, targetAngle, targetDistance);
                    nextRetargetAt = elapsed + retargetInterval;
                    hasTarget = true;
                }
                else if (!hasTarget || elapsed >= nextRetargetAt || Vector2.Distance(bossPosition, target) <= arriveDistance)
                {
                    RetargetAroundPlayer(ref targetAngle, ref targetDistance, safeMin, safeMax);
                    target = BuildPlayerRelativeTarget(playerPosition, targetAngle, targetDistance);
                    nextRetargetAt = elapsed + retargetInterval;
                    hasTarget = true;
                }

                MoveTowardPoint(context, target);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (stopWhenFinished)
            {
                context.Stop();
            }
        }

        private void RetargetAroundPlayer(ref float targetAngle, ref float targetDistance, float safeMin, float safeMax)
        {
            float angleDelta = UnityEngine.Random.Range(-maxRetargetAngleDegrees, maxRetargetAngleDegrees);
            float distanceRange = Mathf.Max(0f, safeMax - safeMin);
            float distanceStep = Mathf.Max(0.05f, distanceRange * 0.35f);
            targetAngle += angleDelta;
            targetDistance = Mathf.Clamp(targetDistance + UnityEngine.Random.Range(-distanceStep, distanceStep), safeMin, safeMax);
        }

        private static Vector2 BuildPlayerRelativeTarget(Vector2 playerPosition, float angleDegrees, float distance)
        {
            return playerPosition + BossActionContext.AngleToDirection(angleDegrees) * distance;
        }

        private static float DirectionToAngleOrFallback(Vector2 direction, float fallbackAngle)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return fallbackAngle;
            }

            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        private void MoveTowardPoint(BossActionContext context, Vector2 target)
        {
            Vector2 current = context.Boss.Body.position;
            Vector2 toTarget = target - current;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                context.Stop();
                return;
            }

            context.Boss.SetMovementVelocity(toTarget.normalized * (context.Boss.MoveSpeed * Mathf.Max(0f, speedMultiplier)));
        }
    }

    [Serializable]
    public sealed class MoveBetweenMapPointsAction : BossAction
    {
        [SerializeField] private Vector2 startPosition;
        [SerializeField] private Vector2 endPosition;
        [SerializeField] private bool snapToStart;
        [SerializeField] private bool moveToStartFirst = true;
        [SerializeField, Min(0f)] private float speedMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float arriveDistance = 0.08f;
        [SerializeField, Min(0f)] private float timeoutSeconds;
        [SerializeField] private bool stopWhenFinished = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null || context.Boss == null || context.Boss.Body == null)
            {
                yield break;
            }

            if (snapToStart)
            {
                context.Boss.Body.position = startPosition;
                context.Boss.transform.position = new Vector3(startPosition.x, startPosition.y, context.Boss.transform.position.z);
            }
            else if (moveToStartFirst)
            {
                yield return MoveToPoint(context, startPosition);
            }

            yield return MoveToPoint(context, endPosition);

            if (stopWhenFinished)
            {
                context.Stop();
            }
        }

        private IEnumerator MoveToPoint(BossActionContext context, Vector2 target)
        {
            float elapsed = 0f;
            while (Vector2.Distance(context.Boss.Body.position, target) > arriveDistance)
            {
                if (timeoutSeconds > 0f && elapsed >= timeoutSeconds)
                {
                    yield break;
                }

                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                Vector2 toTarget = target - context.Boss.Body.position;
                context.Boss.SetMovementVelocity(toTarget.normalized * (context.Boss.MoveSpeed * Mathf.Max(0f, speedMultiplier)));
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    internal static class BossMoveSpeedCurve
    {
        public static AnimationCurve CreateConstant()
        {
            return AnimationCurve.Constant(0f, 1f, 1f);
        }
    }
}
