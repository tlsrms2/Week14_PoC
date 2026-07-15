using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class HackerDashSweepAction : BossAction, IBossActionDurationProvider
    {
        [Header("Charge")]
        [SerializeField] private string chargeTriggerName = "DashSweepCharge";
        [SerializeField, BossGraphBossChildPath] private string parryAnchorPath;
        [SerializeField, Min(0.05f)] private float chargeSeconds = 0.7f;
        [SerializeField, Min(0.05f)] private float parryWindowSeconds = 0.35f;
        [Tooltip("Prefab에 ParryBaitRewardProjectile 컴포넌트가 있어야 합니다.")]
        [SerializeField] private BossProjectileSettings parryProjectile = new();
        [Tooltip("공격 시점에 파편이 원래 모습으로 결합되는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float parryReassembleSeconds = 0.2f;

        [Header("Dash Sweep")]
        [SerializeField] private string sweepTriggerName = "DashSweep";
        [SerializeField, Min(0.05f)] private float dashSeconds = 0.35f;
        [SerializeField, Min(0f)] private float dashSpeed = 13f;
        [SerializeField] private AnimationCurve dashSpeedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.7f);
        [SerializeField, Min(0.05f)] private float sweepRadius = 1.5f;
        [SerializeField, Min(0f)] private float sweepForwardOffset = 0.9f;
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.3f;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss == null)
            {
                yield break;
            }

            context.PlayAnimationTrigger(chargeTriggerName);
            bool isHologram = context.Boss is HackerHologramBoss;
            Transform parryAnchor = context.GetBossChildTransform(parryAnchorPath) ?? context.Boss.transform;
            HackerParryBait parryBait = HackerParryBait.Spawn(
                context,
                parryProjectile,
                parryAnchor.position,
                parryAnchor,
                Vector3.zero,
                Mathf.Min(chargeSeconds, parryWindowSeconds),
                chargeSeconds,
                parryReassembleSeconds);

            float elapsed = 0f;
            HackerAttackRangeIndicator rangeIndicator = null;
            while (elapsed < chargeSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                Vector2 direction = context.GetDirectionToPlayer(context.Boss.transform.position);
                Vector2 center = (Vector2)context.Boss.transform.position + direction * sweepForwardOffset;
                if (rangeIndicator == null)
                {
                    rangeIndicator = HackerAttackRangeIndicator.CreateCircle(center, sweepRadius);
                    rangeIndicator.SetHologramStyle(isHologram);
                }
                else
                {
                    rangeIndicator.SetCircle(center, sweepRadius);
                }

                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            bool wasParried = parryBait?.WasParried == true;
            parryBait?.Dispose();

            HackerAttackRangeIndicator.Destroy(rangeIndicator);
            if (wasParried)
            {
                context.Stop();
                yield return HackerMeleeAttackAction.Wait(context, dashSeconds);
                yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
                yield break;
            }

            context.PlayAnimationTrigger(sweepTriggerName);
            Vector2 dashDirection = context.GetDirectionToPlayer(context.Boss.transform.position);
            context.SetFacingLocked(true);
            context.SetDashing(true);
            Vector2 sweepCenter = (Vector2)context.Boss.transform.position + dashDirection * sweepForwardOffset;
            rangeIndicator = HackerAttackRangeIndicator.CreateCircle(sweepCenter, sweepRadius);
            rangeIndicator.SetHologramStyle(isHologram);
            rangeIndicator.SetFillVisible(true);
            HashSet<PlayerCombatController> hitPlayers = new();
            elapsed = 0f;
            while (elapsed < dashSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                float progress = Mathf.Clamp01(elapsed / dashSeconds);
                float speedMultiplier = EvaluateSpeedCurve(dashSpeedCurve, progress);
                context.Boss.SetMovementVelocity(dashDirection * (dashSpeed * speedMultiplier));
                sweepCenter = (Vector2)context.Boss.transform.position + dashDirection * sweepForwardOffset;
                rangeIndicator.SetCircle(sweepCenter, sweepRadius);
                ApplySweepDamage(context, dashDirection, hitPlayers);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            context.SetDashing(false);
            context.SetFacingLocked(false);
            context.Stop();
            sweepCenter = (Vector2)context.Boss.transform.position + dashDirection * sweepForwardOffset;
            rangeIndicator.SetCircle(sweepCenter, sweepRadius);
            ApplySweepDamage(context, dashDirection, hitPlayers);
            HackerAttackRangeIndicator.Destroy(rangeIndicator);
            yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0.05f, chargeSeconds) + Mathf.Max(0.05f, dashSeconds) + Mathf.Max(0f, recoverySeconds);
            return true;
        }

        private void ApplySweepDamage(
            BossActionContext context,
            Vector2 direction,
            HashSet<PlayerCombatController> hitPlayers)
        {
            Vector2 center = (Vector2)context.Boss.transform.position + direction * sweepForwardOffset;
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, sweepRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                PlayerCombatController player = hits[i].GetComponentInParent<PlayerCombatController>();
                if (player != null && hitPlayers.Add(player))
                {
                    player.ReceiveAttack(damage, center, direction);
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
