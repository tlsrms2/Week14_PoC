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
                remaining -= EnemyTimeScale.DeltaTime;
                elapsed += EnemyTimeScale.DeltaTime;
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
                elapsed += EnemyTimeScale.DeltaTime;
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
                elapsed += EnemyTimeScale.DeltaTime;
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

    internal static class BossMoveSpeedCurve
    {
        public static AnimationCurve CreateConstant()
        {
            return AnimationCurve.Constant(0f, 1f, 1f);
        }
    }
}
