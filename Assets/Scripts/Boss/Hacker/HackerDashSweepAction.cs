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
        private const string IsDashSweepingAnimationParameter = "IsDashSweeping";

        [Header("Charge")]
        [SerializeField] private string chargeTriggerName = "DashSweepCharge";
        [SerializeField, BossGraphBossChildPath] private string parryAnchorPath;
        [SerializeField, Min(0.05f)] private float chargeSeconds = 0.7f;
        [SerializeField, Min(0.05f)] private float parryWindowSeconds = 0.35f;
        [SerializeField] private HackerParrySpawnEffectSettings parrySpawnEffect = new();

        [Header("Dash Sweep")]
        [SerializeField] private string sweepTriggerName = "DashSweep";
        [SerializeField, BossGraphSfxId] private string sweepSfxId = HackerSfxIds.OrbitSweep;
        [SerializeField, Min(0.05f)] private float dashSeconds = 0.35f;
        [SerializeField, Min(0f)] private float dashSpeed = 13f;
        [SerializeField] private AnimationCurve dashSpeedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.7f);
        [SerializeField, Min(0.05f)] private float sweepRadius = 1.5f;
        [SerializeField, Min(0f)] private float sweepForwardOffset = 0.9f;
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.3f;

        [Header("Attack Effect")]
        [SerializeField] private BossActionPrefabEffectSettings attackEffect = new();

        [Header("Parried Effect")]
        [SerializeField] private BossActionPrefabEffectSettings parriedEffect = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss == null)
            {
                yield break;
            }

            HackerParryBait parryBait = null;
            HackerAttackRangeIndicator rangeIndicator = null;
            context.SetAnimationBool(IsDashSweepingAnimationParameter, true);
            try
            {
                context.RestartAnimationTrigger(chargeTriggerName);
                bool isHologram = context.Boss is HackerHologramBoss;
                Transform parryAnchor = context.GetBossChildTransform(parryAnchorPath) ?? context.Boss.transform;
                if (parrySpawnEffect?.Play(parryAnchor.position) == true)
                {
                    yield return HackerMeleeAttackAction.Wait(
                        context,
                        HackerParrySpawnEffectSettings.LeadSeconds);
                }

                parryBait = HackerParryBait.Spawn(
                    context,
                    (context.Boss as HackerBossAI)?.ParryProjectileSettings,
                    parryAnchor.position,
                    parryAnchor,
                    Vector3.zero,
                    Mathf.Min(chargeSeconds, parryWindowSeconds),
                    chargeSeconds,
                    countPatternReward: true);

                float elapsed = 0f;
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
                        rangeIndicator = HackerAttackRangeIndicator.CreateCircle(context, center, sweepRadius);
                        rangeIndicator?.SetHologramStyle(isHologram);
                    }
                    else
                    {
                        rangeIndicator?.SetCircle(center, sweepRadius);
                    }

                    elapsed += EnemyTimeScale.DeltaTime;
                    yield return null;
                }

                bool wasParried = parryBait?.WasParried == true;
                parryBait?.Dispose();
                parryBait = null;
                HackerAttackRangeIndicator.Destroy(rangeIndicator);
                rangeIndicator = null;
                if (wasParried)
                {
                    context.SetAnimationBool(IsDashSweepingAnimationParameter, false);
                    parriedEffect?.Play(context);
                    context.Stop();
                    yield return HackerMeleeAttackAction.Wait(context, dashSeconds);
                    yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
                    yield break;
                }

                context.RestartAnimationTrigger(sweepTriggerName);
                context.PlaySfx(HackerSfxIds.Resolve(sweepSfxId, HackerSfxIds.OrbitSweep));
                attackEffect?.Play(context);
                Vector2 dashDirection = context.GetDirectionToPlayer(context.Boss.transform.position);
                using IDisposable facingLock = context.AcquireFacingLock();
                context.SetDashing(true);
                Vector2 sweepCenter = (Vector2)context.Boss.transform.position + dashDirection * sweepForwardOffset;
                rangeIndicator = HackerAttackRangeIndicator.CreateCircle(context, sweepCenter, sweepRadius);
                rangeIndicator?.SetHologramStyle(isHologram);
                rangeIndicator?.SetFillVisible(true);
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
                    rangeIndicator?.SetCircle(sweepCenter, sweepRadius);
                    ApplySweepDamage(context, dashDirection, hitPlayers);
                    elapsed += EnemyTimeScale.DeltaTime;
                    yield return null;
                }

                context.SetDashing(false);
                context.SetAnimationBool(IsDashSweepingAnimationParameter, false);
                context.Stop();
                sweepCenter = (Vector2)context.Boss.transform.position + dashDirection * sweepForwardOffset;
                rangeIndicator?.SetCircle(sweepCenter, sweepRadius);
                ApplySweepDamage(context, dashDirection, hitPlayers);
                HackerAttackRangeIndicator.Destroy(rangeIndicator);
                rangeIndicator = null;
                yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
            }
            finally
            {
                parryBait?.Dispose();
                HackerAttackRangeIndicator.Destroy(rangeIndicator);
                context.SetDashing(false);
                context.SetAnimationBool(IsDashSweepingAnimationParameter, false);
                context.Stop();
            }
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0.05f, chargeSeconds)
                + Mathf.Max(0.05f, dashSeconds)
                + Mathf.Max(0f, recoverySeconds)
                + (parrySpawnEffect?.LeadDurationSeconds ?? 0f);
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
