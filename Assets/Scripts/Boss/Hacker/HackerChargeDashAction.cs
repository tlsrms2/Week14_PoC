using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
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

        [Header("Dash Effect")]
        [Tooltip("실제 차지 대시가 시작될 때 보스 뒤에 한 번 생성할 이펙트 프리팹입니다. 오른쪽 대시 방향을 기준으로 제작된 프리팹을 사용합니다.")]
        [SerializeField] private GameObject dashEffectPrefab;
        [Tooltip("Boss Graph Editor 하이어러키에서 이펙트 생성 위치를 드래그해 지정합니다.")]
        [SerializeField, BossGraphBossChildPath] private string dashEffectSpawnPointPath;
        [Tooltip("대시 이펙트 프리팹 원본 스케일에 곱할 배율입니다.")]
        [SerializeField, Min(0.01f)] private float dashEffectScale = 1f;

        [Header("Trajectory VFX")]
        [Tooltip("경로를 이어 붙일 정사각형 화살표 인디케이터 프리팹입니다. 프리팹의 Animator와 Sorting 설정을 그대로 사용합니다.")]
        [SerializeField] private GameObject trajectoryIndicatorPrefab;
        [Tooltip("인디케이터 한 칸이 차지할 경로 길이입니다. 0이면 프리팹 SpriteRenderer의 가로 크기를 자동으로 사용합니다.")]
        [SerializeField, Min(0f)] private float trajectoryTileSpacing;
        [Tooltip("프리팹 화살표가 오른쪽을 향하지 않을 때 보정할 Z축 회전값입니다.")]
        [SerializeField] private float trajectoryTileRotationOffset;
        [Tooltip("대기 시작 시 인디케이터 색입니다.")]
        [SerializeField] private Color trajectoryReadyColor = Color.white;
        [Tooltip("대기가 끝났을 때 인디케이터 색입니다.")]
        [SerializeField] private Color trajectoryChargedColor = Color.red;

        [Header("Attack Effect")]
        [Tooltip("Release 애니메이션과 돌진 공격이 시작될 때 1회 생성되는 공격 이펙트입니다.")]
        [FormerlySerializedAs("dashEffect")]
        [SerializeField] private BossActionPrefabEffectSettings attackEffect = new();

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

            float dashDistance = GetDashDistance();
            float elapsed = 0f;
            float directionLockTime = Mathf.Max(0f, windupSeconds - directionLockLeadSeconds);
            Vector2 dashDirection = context.GetDirectionToPlayer(context.Boss.transform.position);
            bool isDirectionLocked = directionLockTime <= 0f;
            BossDashTrajectoryVfx trajectoryVfx = SpawnTrajectoryVfx(dashDistance);
            BossDashAttackArea attackArea = trajectoryVfx != null
                ? trajectoryVfx.AttackArea
                : default;
            if (trajectoryVfx != null)
            {
                context.RegisterTransientVisual(trajectoryVfx.gameObject);
                trajectoryVfx.UpdateVfx(context.Boss.transform.position, dashDirection, 0f);
            }

            try
            {
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
                    elapsed += EnemyTimeScale.DeltaTime;
                    trajectoryVfx?.UpdateVfx(
                        context.Boss.transform.position,
                        previewDirection,
                        windupSeconds > 0f ? elapsed / windupSeconds : 1f);
                    yield return null;
                }
            }
            finally
            {
                if (trajectoryVfx != null)
                {
                    context.UnregisterTransientVisual(trajectoryVfx.gameObject);
                    trajectoryVfx.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(trajectoryVfx.gameObject);
                }
            }

            if (!isDirectionLocked)
            {
                dashDirection = context.GetDirectionToPlayer(context.Boss.transform.position);
            }

            context.SetAnimationBool(IsChargeDashingAnimationParameter, true);
            context.RestartAnimationTrigger(ReleaseAnimationTrigger);
            object facingLockOwner = new();
            context.SetFacingLocked(facingLockOwner, true);
            context.SetDashing(true);
            context.SetAutomaticDashContactDamageSuppressed(attackArea.IsValid);
            HackerDashEffect.PlayPrefab(
                dashEffectPrefab,
                context,
                dashEffectSpawnPointPath,
                dashEffectScale,
                dashDirection);
            GameObject attackEffectInstance = HackerDashEffect.Play(
                attackEffect,
                context,
                dashDirection);

            HashSet<PlayerCombatController> hitPlayers = new();
            Vector2 attackOrigin = context.Boss.transform.position;
            Vector2 previousPosition = context.Boss.transform.position;
            float previousAttackDistance = 0f;
            elapsed = 0f;
            try
            {
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

                        float progress = Mathf.Clamp01(elapsed / dashSeconds);
                        float speedMultiplier = EvaluateSpeedCurve(dashSpeedCurve, progress);
                        context.Boss.SetMovementVelocity(dashDirection * (dashSpeed * speedMultiplier));

                        float nextElapsed = Mathf.Min(dashSeconds, elapsed + EnemyTimeScale.DeltaTime);
                        if (attackArea.IsValid)
                        {
                            float nextAttackDistance = BossDashMotion.GetDistanceAtProgress(
                                dashSpeed,
                                dashSeconds,
                                dashSpeedCurve,
                                nextElapsed / dashSeconds);
                            ApplyIndicatorAreaDamage(
                                context,
                                attackArea,
                                attackOrigin,
                                dashDirection,
                                previousAttackDistance,
                                nextAttackDistance,
                                hitPlayers);
                            previousAttackDistance = nextAttackDistance;
                        }
                        else
                        {
                            Vector2 currentPosition = context.Boss.transform.position;
                            ApplyLegacyPathDamage(
                                context,
                                previousPosition,
                                currentPosition,
                                dashDirection,
                                hitPlayers);
                            previousPosition = currentPosition;
                        }

                        elapsed = nextElapsed;
                        yield return null;
                    }

                    if (!attackArea.IsValid)
                    {
                        ApplyLegacyPathDamage(
                            context,
                            previousPosition,
                            context.Boss.transform.position,
                            dashDirection,
                            hitPlayers);
                    }
                }
                finally
                {
                    context.SetDashing(false);
                    context.Stop();
                    context.SetAnimationBool(IsChargeDashingAnimationParameter, false);
                    context.RestartAnimationTrigger(EndAnimationTrigger);
                }

                yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
                yield return WaitForAttackEffect(attackEffectInstance);
            }
            finally
            {
                context.SetFacingLocked(facingLockOwner, false);
            }
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
            return BossDashMotion.GetDistance(dashSpeed, dashSeconds, dashSpeedCurve);
        }

        private BossDashTrajectoryVfx SpawnTrajectoryVfx(float indicatorLength)
        {
            if (trajectoryIndicatorPrefab == null)
            {
                return null;
            }

            return BossDashTrajectoryVfx.Spawn(
                trajectoryIndicatorPrefab,
                indicatorLength,
                trajectoryTileSpacing,
                trajectoryTileRotationOffset,
                trajectoryReadyColor,
                trajectoryChargedColor);
        }

        private void ApplyIndicatorAreaDamage(
            BossActionContext context,
            BossDashAttackArea attackArea,
            Vector2 origin,
            Vector2 direction,
            float fromDistance,
            float toDistance,
            HashSet<PlayerCombatController> hitPlayers)
        {
            if (!attackArea.TryGetSegmentBox(
                    origin,
                    direction,
                    fromDistance,
                    toDistance,
                    out Vector2 center,
                    out Vector2 size,
                    out float angle))
            {
                return;
            }

            ApplyBoxDamage(center, size, angle, direction, hitPlayers);
        }

        private void ApplyBoxDamage(
            Vector2 center,
            Vector2 size,
            float angle,
            Vector2 direction,
            HashSet<PlayerCombatController> hitPlayers)
        {
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, angle);
            for (int i = 0; i < hits.Length; i++)
            {
                PlayerCombatController player = hits[i].GetComponentInParent<PlayerCombatController>();
                if (player != null && hitPlayers.Add(player))
                {
                    player.ReceiveAttack(damage, center, direction);
                }
            }
        }

        private void ApplyLegacyPathDamage(
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

        private static IEnumerator WaitForAttackEffect(GameObject effectInstance)
        {
            while (effectInstance != null)
            {
                yield return null;
            }
        }
    }
}
