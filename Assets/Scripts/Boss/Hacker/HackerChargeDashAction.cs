using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class HackerChargeDashAction : BossAction, IBossActionDurationProvider, IHackerApproachRangeProvider
    {
        private const string ChargeAnimationTrigger = "Charge";
        private const string ReleaseAnimationTrigger = "Release";
        private const string EndAnimationTrigger = "End";
        private const string IsChargeDashingAnimationParameter = "IsChargeDashing";

        [Header("Animation")]
        [SerializeField, Min(0f)] private float windupSeconds = 0.45f;
        [SerializeField, Min(0f)] private float directionLockLeadSeconds = 0.15f;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.3f;

        [Header("Approach")]
        [SerializeField, Min(0f)] private float approachStartDistance = 8f;
        [SerializeField, Min(0f)] private float approachStopDistance = 3.5f;
        [SerializeField, Min(0f)] private float approachSpeed = 10f;
        [SerializeField, Min(0f)] private float maxApproachSeconds = 0.6f;
        [SerializeField] private AnimationCurve approachSpeedCurve = AnimationCurve.EaseInOut(0f, 0.45f, 1f, 1f);

        [Header("Charge Dash")]
        [SerializeField, Min(0.05f)] private float dashSeconds = 0.5f;
        [SerializeField, Min(0f)] private float dashSpeed = 15f;
        [SerializeField] private AnimationCurve dashSpeedCurve = AnimationCurve.EaseInOut(0f, 0.7f, 1f, 1f);

        [Header("Path Damage")]
        [SerializeField, Min(0.05f)] private float damageRadius = 0.55f;
        [SerializeField, Min(1)] private int damage = 1;

        float IHackerApproachRangeProvider.ApproachStartDistance => approachStartDistance;
        float IHackerApproachRangeProvider.ApproachStopDistance => approachStopDistance;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss == null)
            {
                yield break;
            }

            yield return ApproachToDashDistance(context);
            context.PlayAnimationTrigger(ChargeAnimationTrigger);

            HackerAttackRangeIndicator rangeIndicator = null;
            bool isHologram = context.Boss is HackerHologramBoss;
            float dashDistance = GetDashDistance();
            float elapsed = 0f;
            float directionLockTime = Mathf.Max(0f, windupSeconds - directionLockLeadSeconds);
            Vector2 dashDirection = context.GetDirectionToPlayer(context.Boss.transform.position);
            bool isDirectionLocked = directionLockTime <= 0f;
            while (elapsed < windupSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                if (!isDirectionLocked && elapsed >= directionLockTime)
                {
                    dashDirection = context.GetDirectionToPlayer(context.Boss.transform.position);
                    isDirectionLocked = true;
                }

                Vector2 previewDirection = isDirectionLocked
                    ? dashDirection
                    : context.GetDirectionToPlayer(context.Boss.transform.position);
                if (rangeIndicator == null)
                {
                    rangeIndicator = HackerAttackRangeIndicator.CreateThrust(
                        context.Boss.transform.position,
                        previewDirection,
                        dashDistance,
                        damageRadius * 2f);
                    rangeIndicator.SetHologramStyle(isHologram);
                }
                else
                {
                    rangeIndicator.SetThrust(
                        context.Boss.transform.position,
                        previewDirection,
                        dashDistance,
                        damageRadius * 2f);
                }

                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            if (!isDirectionLocked)
            {
                dashDirection = context.GetDirectionToPlayer(context.Boss.transform.position);
            }

            context.SetAnimationBool(IsChargeDashingAnimationParameter, true);
            context.RestartAnimationTrigger(ReleaseAnimationTrigger);
            context.SetFacingLocked(true);
            context.SetDashing(true);
            rangeIndicator ??= HackerAttackRangeIndicator.CreateThrust(
                context.Boss.transform.position,
                dashDirection,
                dashDistance,
                damageRadius * 2f);
            rangeIndicator.SetHologramStyle(isHologram);
            rangeIndicator.SetThrust(
                context.Boss.transform.position,
                dashDirection,
                dashDistance,
                damageRadius * 2f);
            rangeIndicator.SetFillVisible(true);

            HashSet<PlayerCombatController> hitPlayers = new();
            Vector2 previousPosition = context.Boss.transform.position;
            elapsed = 0f;
            try
            {
                while (elapsed < dashSeconds)
                {
                    if (context.IsExecutionPaused)
                    {
                        context.Stop();
                        yield return null;
                        continue;
                    }

                    Vector2 currentPosition = context.Boss.transform.position;
                    ApplyPathDamage(context, previousPosition, currentPosition, dashDirection, hitPlayers);
                    previousPosition = currentPosition;

                    float progress = Mathf.Clamp01(elapsed / dashSeconds);
                    float speedMultiplier = EvaluateSpeedCurve(dashSpeedCurve, progress);
                    context.Boss.SetMovementVelocity(dashDirection * (dashSpeed * speedMultiplier));
                    elapsed += EnemyTimeScale.DeltaTime;
                    yield return null;
                }

                ApplyPathDamage(
                    context,
                    previousPosition,
                    context.Boss.transform.position,
                    dashDirection,
                    hitPlayers);
            }
            finally
            {
                context.SetDashing(false);
                context.SetFacingLocked(false);
                context.Stop();
                HackerAttackRangeIndicator.Destroy(rangeIndicator);
                context.SetAnimationBool(IsChargeDashingAnimationParameter, false);
                context.RestartAnimationTrigger(EndAnimationTrigger);
            }

            yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, windupSeconds)
                + Mathf.Max(0.05f, dashSeconds)
                + Mathf.Max(0f, recoverySeconds);
            return true;
        }

        private IEnumerator ApproachToDashDistance(BossActionContext context)
        {
            if (context?.Boss == null
                || context.SkipApproachMovement
                || approachStartDistance <= 0f
                || approachSpeed <= 0f
                || maxApproachSeconds <= 0f
                || context.Boss.DistanceToPlayer() <= approachStartDistance)
            {
                yield break;
            }

            float stopDistance = Mathf.Min(approachStartDistance, approachStopDistance);
            try
            {
                float elapsed = 0f;
                while (elapsed < maxApproachSeconds && context.Boss.DistanceToPlayer() > stopDistance)
                {
                    if (context.IsExecutionPaused)
                    {
                        context.Stop();
                        yield return null;
                        continue;
                    }

                    float progress = Mathf.Clamp01(elapsed / maxApproachSeconds);
                    float speedMultiplier = EvaluateSpeedCurve(approachSpeedCurve, progress);
                    context.Boss.SetMovementVelocity(
                        context.GetDirectionToPlayer(context.Boss.transform.position) * (approachSpeed * speedMultiplier));
                    elapsed += EnemyTimeScale.DeltaTime;
                    yield return null;
                }
            }
            finally
            {
                context.Stop();
            }
        }

        private float GetDashDistance()
        {
            const int sampleCount = 24;
            float multiplierSum = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float progress = (i + 0.5f) / sampleCount;
                multiplierSum += EvaluateSpeedCurve(dashSpeedCurve, progress);
            }

            return dashSpeed * dashSeconds * multiplierSum / sampleCount;
        }

        private void ApplyPathDamage(
            BossActionContext context,
            Vector2 start,
            Vector2 end,
            Vector2 direction,
            HashSet<PlayerCombatController> hitPlayers)
        {
            float radius = Mathf.Max(0.05f, damageRadius);
            Vector2 delta = end - start;
            Collider2D[] hits;
            if (delta.sqrMagnitude <= 0.0001f)
            {
                hits = Physics2D.OverlapCircleAll(end, radius);
            }
            else
            {
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                hits = Physics2D.OverlapCapsuleAll(
                    (start + end) * 0.5f,
                    new Vector2(delta.magnitude + radius * 2f, radius * 2f),
                    CapsuleDirection2D.Horizontal,
                    angle);
            }

            for (int i = 0; i < hits.Length; i++)
            {
                PlayerCombatController player = hits[i].GetComponentInParent<PlayerCombatController>();
                if (player != null && hitPlayers.Add(player))
                {
                    player.ReceiveAttack(damage, end, direction);
                }
            }
        }

        private static float EvaluateSpeedCurve(AnimationCurve curve, float progress)
        {
            return curve != null && curve.length > 0
                ? Mathf.Max(0f, curve.Evaluate(progress))
                : 1f;
        }
    }
}
