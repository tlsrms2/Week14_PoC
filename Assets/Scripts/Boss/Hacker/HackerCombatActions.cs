using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Combat;

namespace Week14.Enemy
{
    public enum HackerMeleeAttackStyle
    {
        CutTopToBottom,
        Slam,
        Lift,
        CutBottomToTop
    }

    public enum HackerDashDirection
    {
        Approach,
        Retreat,
        DiagonalPlayer
    }

    internal static class HackerDashEffect
    {
        internal static GameObject Play(
            BossActionPrefabEffectSettings effect,
            BossActionContext context,
            Vector2 dashDirection,
            string spawnPointPathOverride = null)
        {
            if (effect == null)
            {
                return null;
            }

            float worldYRotation = dashDirection.x < 0f ? 180f : 0f;
            return effect.PlayAtWorldYRotation(
                context,
                spawnPointPathOverride,
                worldYRotation);
        }

        internal static void PlayPrefab(
            GameObject prefab,
            BossActionContext context,
            string spawnPointPath,
            float scale,
            Vector2 dashDirection)
        {
            if (prefab == null || context == null)
            {
                return;
            }

            Transform spawnPoint = context.GetBossChildTransform(spawnPointPath);
            if (spawnPoint == null)
            {
                return;
            }

            float worldYRotation = dashDirection.x < 0f ? 180f : 0f;
            ProjectileVfx.PlayPrefab(
                prefab,
                spawnPoint.position,
                Quaternion.Euler(0f, worldYRotation, 0f),
                null,
                Mathf.Max(0.01f, scale),
                false);
        }
    }

    [Serializable]
    public sealed class HackerMeleeAttackAction : BossAction, IBossActionDurationProvider, IHackerApproachRangeProvider
    {
        private const string ReleaseAnimationTrigger = "Release";

        [Header("Attack")]
        [SerializeField] private HackerMeleeAttackStyle style;
        [FormerlySerializedAs("cutTriggerName")]
        [SerializeField] private string cutTopToBottomTriggerName = "Cut";
        [SerializeField] private string cutBottomToTopTriggerName = "CutBottomToTop";
        [SerializeField] private string slamTriggerName = "Slam";
        [SerializeField] private string liftTriggerName = "Lift";
        [SerializeField, Min(0f)] private float windupSeconds = 0.7f;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.35f;
        [SerializeField, Min(0.05f)] private float range = 2f;
        [SerializeField, Min(0.05f)] private float ellipseMinorRadius = 0.7f;
        [SerializeField, Min(0f)] private float ellipseForwardOffset = 0.7f;
        [SerializeField, Min(1)] private int damage = 1;

        [Header("Approach")]
        [SerializeField, Min(0f)] private float approachStartDistance = 6f;
        [SerializeField, Min(0f)] private float approachStopDistance = 1.5f;
        [SerializeField, Min(0f)] private float approachSpeed = 12f;
        [SerializeField, Min(0f)] private float maxApproachSeconds = 0.8f;

        [SerializeField] private AnimationCurve approachSpeedCurve = AnimationCurve.EaseInOut(0f, 0.45f, 1f, 1f);

        [Header("Attack Advance")]
        [SerializeField, Min(0f)] private float attackAdvanceSeconds = 0.18f;
        [SerializeField, Min(0f)] private float attackAdvanceSpeed = 4f;
        [SerializeField] private AnimationCurve attackAdvanceSpeedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.7f);
        [SerializeField, Range(0f, 180f)] private float attackAdvanceTrackingAngleDegrees = 90f;

        [Header("Parry")]
        [SerializeField, BossGraphBossChildPath] private string parryAnchorPath;
        [SerializeField, Min(0.01f)] private float parryWindowSeconds = 0.36f;

        [Header("Attack Effect")]
        [SerializeField] private bool spawnEffectOnAttack;
        [SerializeField] private GameObject attackEffectPrefab;
        [SerializeField, BossGraphBossChildPath] private string attackEffectSpawnPointPath;
        [SerializeField] private Vector2 attackEffectPositionOffset;
        [SerializeField, Min(0.01f)] private float attackEffectScale = 1f;
        [SerializeField] private SpawnEffectFollowMode attackEffectFollowMode = SpawnEffectFollowMode.FollowSpawnPoint;
        [SerializeField] private bool attackEffectFollowRotation = true;

        [Header("Parried Effect")]
        [SerializeField] private bool spawnEffectOnParried;
        [SerializeField] private GameObject parriedEffectPrefab;
        [SerializeField, BossGraphBossChildPath] private string parriedEffectSpawnPointPath;
        [SerializeField] private Vector2 parriedEffectPositionOffset;
        [SerializeField, Min(0.01f)] private float parriedEffectScale = 1f;
        [SerializeField] private SpawnEffectFollowMode parriedEffectFollowMode = SpawnEffectFollowMode.FollowSpawnPoint;
        [SerializeField] private bool parriedEffectFollowRotation = true;

        float IHackerApproachRangeProvider.ApproachStartDistance => approachStartDistance;
        float IHackerApproachRangeProvider.ApproachStopDistance => approachStopDistance;

        [Header("Slam")]
        [SerializeField, Min(0f)] private float slamChainSeconds = 2f;
        [SerializeField] private GameObject consecutiveSlamDustPrefab;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss == null)
            {
                yield break;
            }

            yield return ApproachToMeleeDistance(context);
            HackerAttackRangeIndicator rangeIndicator = null;
            HackerParryBait parryBait = null;
            bool isHologram = context.Boss is HackerHologramBoss;
            Vector2 attackDirection = GetPlayerSideDirection(context);
            if (context.Boss is HackerBossAI hacker)
            {
                hacker.FaceHorizontalDirection(attackDirection.x);
            }

            using IDisposable facingLock = context.AcquireFacingLock();
            try
            {
                context.PlayAnimationTrigger(GetAnimationTrigger());
                GetMeleeEllipse(context, attackDirection, out Vector2 ellipseCenter, out float ellipseAngleDegrees);
                rangeIndicator = HackerAttackRangeIndicator.CreateEllipse(
                    context,
                    ellipseCenter,
                    range,
                    ellipseMinorRadius,
                    ellipseAngleDegrees);
                rangeIndicator?.SetHologramStyle(isHologram);
                yield return WaitWithMeleeEllipseIndicator(
                    context,
                    attackDirection,
                    Mathf.Max(0f, windupSeconds - parryWindowSeconds),
                    rangeIndicator);

                Transform parryAnchor = context.GetBossChildTransform(parryAnchorPath) ?? context.Boss.transform;
                float remainingWindup = Mathf.Min(windupSeconds, parryWindowSeconds);
                parryBait = HackerParryBait.Spawn(
                    context,
                    (context.Boss as HackerBossAI)?.ParryProjectileSettings,
                    parryAnchor.position,
                    parryAnchor,
                    Vector3.zero,
                    remainingWindup,
                    remainingWindup,
                    countPatternReward: true);

                float elapsed = 0f;
                while (elapsed < remainingWindup)
                {
                    if (context.IsExecutionPaused)
                    {
                        context.Stop();
                        yield return null;
                        continue;
                    }

                    GetMeleeEllipse(context, attackDirection, out ellipseCenter, out ellipseAngleDegrees);
                    rangeIndicator?.SetEllipse(ellipseCenter, range, ellipseMinorRadius, ellipseAngleDegrees);
                    elapsed += EnemyTimeScale.DeltaTime;
                    yield return null;
                }

                bool wasParried = parryBait?.WasParried == true;
                parryBait?.Dispose();
                parryBait = null;
                context.PlayAnimationTrigger(ReleaseAnimationTrigger);

                if (wasParried)
                {
                    TrySpawnParriedEffect(context);
                    HackerAttackRangeIndicator.Destroy(rangeIndicator);
                    rangeIndicator = null;
                    context.Stop();
                    yield return Wait(context, Mathf.Max(0f, remainingWindup - elapsed));
                    yield return AdvanceDuringAttack(
                        context,
                        attackDirection,
                        attackAdvanceSeconds,
                        attackAdvanceSpeed,
                        attackAdvanceSpeedCurve,
                        null,
                        null,
                        false);
                    context.NotifyMeleeAttackAdvanceCompleted();
                }
                else
                {
                    GetMeleeEllipse(context, attackDirection, out ellipseCenter, out ellipseAngleDegrees);
                    HashSet<PlayerCombatController> hitPlayers = new();
                    rangeIndicator?.SetFillVisible(true);
                    TrySpawnAttackEffect(context);
                    ApplyEllipseDamage(context, ellipseCenter, ellipseAngleDegrees, hitPlayers);
                    yield return AdvanceDuringAttack(
                        context,
                        attackDirection,
                        attackAdvanceSeconds,
                        attackAdvanceSpeed,
                        attackAdvanceSpeedCurve,
                        rangeIndicator,
                        hitPlayers,
                        true);
                    context.NotifyMeleeAttackAdvanceCompleted();
                    TrySpawnConsecutiveSlamDust(context);
                }
            }
            finally
            {
                parryBait?.Dispose();
                HackerAttackRangeIndicator.Destroy(rangeIndicator);
            }

            yield return Wait(context, recoverySeconds);
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, windupSeconds)
                + Mathf.Max(0f, attackAdvanceSeconds)
                + Mathf.Max(0f, recoverySeconds);
            return true;
        }

        private string GetAnimationTrigger()
        {
            return style switch
            {
                HackerMeleeAttackStyle.Slam => slamTriggerName,
                HackerMeleeAttackStyle.Lift => liftTriggerName,
                HackerMeleeAttackStyle.CutBottomToTop => cutBottomToTopTriggerName,
                _ => cutTopToBottomTriggerName
            };
        }

        private void ApplyEllipseDamage(
            BossActionContext context,
            Vector2 ellipseCenter,
            float ellipseAngleDegrees,
            HashSet<PlayerCombatController> hitPlayers)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(ellipseCenter, Mathf.Max(range, ellipseMinorRadius));
            float radians = ellipseAngleDegrees * Mathf.Deg2Rad;
            Vector2 majorAxis = new(Mathf.Cos(radians), Mathf.Sin(radians));
            Vector2 minorAxis = new(-majorAxis.y, majorAxis.x);
            for (int i = 0; i < hits.Length; i++)
            {
                PlayerCombatController player = hits[i].GetComponentInParent<PlayerCombatController>();
                if (player == null || hitPlayers.Contains(player))
                {
                    continue;
                }

                Vector2 offset = (Vector2)player.transform.position - ellipseCenter;
                float majorDistance = Vector2.Dot(offset, majorAxis) / range;
                float minorDistance = Vector2.Dot(offset, minorAxis) / ellipseMinorRadius;
                if (majorDistance * majorDistance + minorDistance * minorDistance > 1f)
                {
                    continue;
                }

                hitPlayers.Add(player);

                player.ReceiveAttack(damage, ellipseCenter, majorAxis);
            }
        }

        private void TrySpawnConsecutiveSlamDust(BossActionContext context)
        {
            if (style != HackerMeleeAttackStyle.Slam
                || context.Boss is not HackerBossAI hacker
                || !hacker.IsConsecutiveSlam(slamChainSeconds)
                || consecutiveSlamDustPrefab == null)
            {
                return;
            }

            GameObject dust = UnityEngine.Object.Instantiate(
                consecutiveSlamDustPrefab,
                context.Boss.transform.position,
                Quaternion.identity);
            BossSorting.ApplyToChildren(dust);
        }

        private void TrySpawnAttackEffect(BossActionContext context)
        {
            TrySpawnEffect(
                context,
                spawnEffectOnAttack,
                attackEffectPrefab,
                attackEffectSpawnPointPath,
                attackEffectPositionOffset,
                attackEffectScale,
                attackEffectFollowMode,
                attackEffectFollowRotation);
        }

        private void TrySpawnParriedEffect(BossActionContext context)
        {
            TrySpawnEffect(
                context,
                spawnEffectOnParried,
                parriedEffectPrefab,
                parriedEffectSpawnPointPath,
                parriedEffectPositionOffset,
                parriedEffectScale,
                parriedEffectFollowMode,
                parriedEffectFollowRotation);
        }

        private static void TrySpawnEffect(
            BossActionContext context,
            bool isEnabled,
            GameObject effectPrefab,
            string spawnPointPath,
            Vector2 positionOffset,
            float scale,
            SpawnEffectFollowMode followMode,
            bool followRotation)
        {
            BossActionPrefabEffectSettings.Play(
                context,
                isEnabled,
                effectPrefab,
                spawnPointPath,
                positionOffset,
                scale,
                followMode,
                followRotation);
        }

        internal static IEnumerator Wait(BossActionContext context, float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }
        }

        private IEnumerator WaitWithMeleeEllipseIndicator(
            BossActionContext context,
            Vector2 attackDirection,
            float seconds,
            HackerAttackRangeIndicator indicator)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                GetMeleeEllipse(context, attackDirection, out Vector2 ellipseCenter, out float ellipseAngleDegrees);
                indicator?.SetEllipse(ellipseCenter, range, ellipseMinorRadius, ellipseAngleDegrees);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }
        }

        private IEnumerator ApproachToMeleeDistance(BossActionContext context)
        {
            if (context?.Boss == null
                || context.SkipApproachMovement
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
                context.SetDashing(false);
                context.Stop();
            }
        }

        internal static IEnumerator ApproachIntoRange(BossActionContext context, float attackRange, float speed, float maxSeconds)
        {
            if (context?.Boss == null
                || context.Boss.DistanceToPlayer() <= attackRange
                || speed <= 0f
                || maxSeconds <= 0f)
            {
                yield break;
            }

            context.SetDashing(true);
            float elapsed = 0f;
            while (elapsed < maxSeconds && context.Boss.DistanceToPlayer() > attackRange)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                context.Boss.SetMovementVelocity(context.GetDirectionToPlayer(context.Boss.transform.position) * speed);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            context.SetDashing(false);
            context.Stop();
        }

        private IEnumerator AdvanceDuringAttack(
            BossActionContext context,
            Vector2 direction,
            float seconds,
            float speed,
            AnimationCurve speedCurve,
            HackerAttackRangeIndicator rangeIndicator,
            HashSet<PlayerCombatController> hitPlayers,
            bool applyDamage)
        {
            if (context?.Boss == null || seconds <= 0f)
            {
                yield break;
            }

            Vector2 movementDirection = GetConstrainedAdvanceDirection(
                context,
                direction,
                attackAdvanceTrackingAngleDegrees);
            try
            {
                float elapsed = 0f;
                while (elapsed < seconds)
                {
                    if (context.IsExecutionPaused)
                    {
                        context.Stop();
                        yield return null;
                        continue;
                    }

                    float progress = Mathf.Clamp01(elapsed / seconds);
                    if (speed > 0f)
                    {
                        context.Boss.SetMovementVelocity(movementDirection * (speed * EvaluateSpeedCurve(speedCurve, progress)));
                    }

                    GetMeleeEllipse(context, direction, out Vector2 ellipseCenter, out float ellipseAngleDegrees);
                    rangeIndicator?.SetEllipse(ellipseCenter, range, ellipseMinorRadius, ellipseAngleDegrees);
                    if (applyDamage)
                    {
                        ApplyEllipseDamage(context, ellipseCenter, ellipseAngleDegrees, hitPlayers);
                    }

                    elapsed += EnemyTimeScale.DeltaTime;
                    yield return null;
                }
            }
            finally
            {
                context.Stop();
            }
        }

        private static float EvaluateSpeedCurve(AnimationCurve curve, float progress)
        {
            return curve != null && curve.length > 0
                ? Mathf.Max(0f, curve.Evaluate(progress))
                : 1f;
        }

        internal static Vector2 GetConstrainedAdvanceDirection(
            BossActionContext context,
            Vector2 attackDirection,
            float trackingAngleDegrees)
        {
            if (context?.Boss == null || attackDirection.sqrMagnitude <= 0.0001f)
            {
                return Vector2.zero;
            }

            Vector2 playerDirection = context.GetDirectionToPlayer(context.Boss.transform.position);
            float maximumAngle = Mathf.Clamp(trackingAngleDegrees, 0f, 180f);
            float signedPlayerAngle = Vector2.SignedAngle(attackDirection, playerDirection);
            float clampedAngle = Mathf.Clamp(signedPlayerAngle, -maximumAngle, maximumAngle);
            return RotateDirection(attackDirection.normalized, clampedAngle);
        }

        private static Vector2 RotateDirection(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(
                direction.x * cosine - direction.y * sine,
                direction.x * sine + direction.y * cosine);
        }

        private void GetMeleeEllipse(
            BossActionContext context,
            Vector2 attackDirection,
            out Vector2 center,
            out float angleDegrees)
        {
            bool attackingLeft = attackDirection.x < 0f;
            bool bottomToTop = style == HackerMeleeAttackStyle.CutBottomToTop
                || style == HackerMeleeAttackStyle.Lift;
            center = (Vector2)context.Boss.transform.position + attackDirection * ellipseForwardOffset;

            float vertical = bottomToTop ? 1f : -1f;
            float diagonalHorizontal = attackingLeft ? 1f : -1f;
            Vector2 diagonal = new Vector2(diagonalHorizontal, vertical).normalized;
            angleDegrees = Mathf.Atan2(diagonal.y, diagonal.x) * Mathf.Rad2Deg;
        }

        private static Vector2 GetPlayerSideDirection(BossActionContext context)
        {
            Vector2 toPlayer = context.GetDirectionToPlayer(context.Boss.transform.position);
            if (Mathf.Abs(toPlayer.x) > 0.0001f)
            {
                return toPlayer.x < 0f ? Vector2.left : Vector2.right;
            }

            bool facingLeft = context.Boss is HackerBossAI hacker && hacker.IsFacingLeft;
            return facingLeft ? Vector2.left : Vector2.right;
        }

    }

    [Serializable]
    public class HackerThrustAction : BossAction, IBossActionDurationProvider, IHackerApproachRangeProvider
    {
        private const string ReleaseAnimationTrigger = "Release";

        [SerializeField] private string animationTriggerName = "Thrust";
        [SerializeField, Min(0f)] private float windupSeconds = 0.5f;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.25f;
        [SerializeField, Min(0.05f)] private float length = 3f;
        [SerializeField, Min(0.05f)] private float width = 0.7f;
        [SerializeField, Min(1)] private int damage = 1;

        [Header("Knockback")]
        [SerializeField, Min(0f)] private float knockbackSpeed = 8f;
        [SerializeField, Min(0f)] private float knockbackStaggerSeconds = 0.12f;

        [Header("Approach")]
        [SerializeField, Min(0f)] private float approachStartDistance = 7f;
        [SerializeField, Min(0f)] private float approachStopDistance = 2f;
        [SerializeField, Min(0f)] private float approachSpeed = 12f;
        [SerializeField, Min(0f)] private float maxApproachSeconds = 0.8f;
        [SerializeField] private AnimationCurve approachSpeedCurve = AnimationCurve.EaseInOut(0f, 0.45f, 1f, 1f);

        [Header("Thrust Advance")]
        [SerializeField, Min(0f)] private float thrustAdvanceSeconds = 0.18f;
        [SerializeField, Min(0f)] private float thrustAdvanceSpeed = 4f;
        [SerializeField] private AnimationCurve thrustAdvanceSpeedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.7f);
        [SerializeField, Range(0f, 180f)] private float thrustAdvanceTrackingAngleDegrees = 90f;

        [Header("Parry")]
        [SerializeField, BossGraphBossChildPath] private string parryAnchorPath;
        [SerializeField, Min(0.01f)] private float parryWindowSeconds = 0.36f;

        [Header("Attack Effect")]
        [SerializeField] private BossActionPrefabEffectSettings attackEffect = new();

        [Header("Parried Effect")]
        [SerializeField] private BossActionPrefabEffectSettings parriedEffect = new();

        protected float ThrustLength => length;

        float IHackerApproachRangeProvider.ApproachStartDistance => approachStartDistance;
        float IHackerApproachRangeProvider.ApproachStopDistance => approachStopDistance;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss == null)
            {
                yield break;
            }

            yield return ApproachToThrustDistance(context);
            Vector2 origin = context.Boss.transform.position;
            Vector2 direction = GetHorizontalThrustDirection(context, origin);
            bool isHologram = context.Boss is HackerHologramBoss;
            if (context.Boss is HackerBossAI hacker)
            {
                hacker.FaceHorizontalDirection(direction.x);
            }

            using IDisposable facingLock = context.AcquireFacingLock();
            context.RestartAnimationTrigger(animationTriggerName);
            HackerAttackRangeIndicator rangeIndicator = HackerAttackRangeIndicator.CreateThrust(
                context,
                origin,
                direction,
                length,
                width);
            rangeIndicator?.SetHologramStyle(isHologram);
            float thrustExecutionSeconds = Mathf.Max(0f, thrustAdvanceSeconds);
            yield return WaitWithThrustIndicator(
                context,
                direction,
                Mathf.Max(0f, windupSeconds - parryWindowSeconds),
                rangeIndicator);

            Transform parryAnchor = context.GetBossChildTransform(parryAnchorPath) ?? context.Boss.transform;
            Transform bossTransform = context.Boss.transform;
            Vector3 parryWorldOffset = parryAnchor.position - bossTransform.position;
            parryWorldOffset.x = Mathf.Abs(parryWorldOffset.x) * Mathf.Sign(direction.x);
            float remainingWindup = Mathf.Min(windupSeconds, parryWindowSeconds);
            HackerParryBait parryBait = HackerParryBait.Spawn(
                context,
                (context.Boss as HackerBossAI)?.ParryProjectileSettings,
                bossTransform.position + parryWorldOffset,
                bossTransform,
                parryWorldOffset,
                remainingWindup,
                remainingWindup,
                countPatternReward: true);

            float elapsed = 0f;
            while (elapsed < remainingWindup)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                rangeIndicator?.SetThrust(context.Boss.transform.position, direction, length, width);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            bool wasParried = parryBait?.WasParried == true;
            parryBait?.Dispose();
            context.RestartAnimationTrigger(ReleaseAnimationTrigger);
            HashSet<PlayerCombatController> hitPlayers = null;
            if (wasParried)
            {
                parriedEffect?.Play(context);
                HackerAttackRangeIndicator.Destroy(rangeIndicator);
                rangeIndicator = null;
                context.Stop();
                yield return HackerMeleeAttackAction.Wait(
                    context,
                    Mathf.Max(0f, remainingWindup - elapsed));
            }
            else
            {
                hitPlayers = new HashSet<PlayerCombatController>();
                rangeIndicator?.SetFillVisible(true);
                attackEffect?.Play(context);
                ApplyLineDamage(context, context.Boss.transform.position, direction, hitPlayers);
                IEnumerator payload = CreateThrustPayload(context, direction);
                if (payload != null)
                {
                    context.Boss.StartCoroutine(payload);
                }
            }

            yield return AdvanceDuringThrust(
                context,
                direction,
                rangeIndicator,
                hitPlayers,
                thrustExecutionSeconds,
                applyDamage: !wasParried);
            HackerAttackRangeIndicator.Destroy(rangeIndicator);
            yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, windupSeconds)
                + Mathf.Max(0f, thrustAdvanceSeconds)
                + Mathf.Max(0f, recoverySeconds);
            return true;
        }

        protected virtual IEnumerator CreateThrustPayload(BossActionContext context, Vector2 direction)
        {
            yield break;
        }

        private void ApplyLineDamage(
            BossActionContext context,
            Vector2 origin,
            Vector2 direction,
            HashSet<PlayerCombatController> hitPlayers)
        {
            Vector2 center = origin + direction * (length * 0.5f);
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, length * 0.5f + width * 0.5f);
            Vector2 perpendicular = new(-direction.y, direction.x);
            for (int i = 0; i < hits.Length; i++)
            {
                PlayerCombatController player = hits[i].GetComponentInParent<PlayerCombatController>();
                if (player == null || hitPlayers.Contains(player))
                {
                    continue;
                }

                Vector2 offset = (Vector2)player.transform.position - origin;
                float forward = Vector2.Dot(offset, direction);
                if (forward >= 0f && forward <= length && Mathf.Abs(Vector2.Dot(offset, perpendicular)) <= width * 0.5f)
                {
                    hitPlayers.Add(player);
                    if (player.ReceiveAttack(damage, origin, direction))
                    {
                        player.ApplyExternalKnockback(direction, knockbackSpeed, knockbackStaggerSeconds);
                    }
                }
            }
        }

        private static Vector2 GetHorizontalThrustDirection(BossActionContext context, Vector2 origin)
        {
            Vector2 toPlayer = context.GetDirectionToPlayer(origin);
            return toPlayer.x < 0f ? Vector2.left : Vector2.right;
        }

        private IEnumerator ApproachToThrustDistance(BossActionContext context)
        {
            if (context?.Boss == null
                || context.SkipApproachMovement
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

        private IEnumerator WaitWithThrustIndicator(
            BossActionContext context,
            Vector2 direction,
            float seconds,
            HackerAttackRangeIndicator indicator)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                indicator?.SetThrust(context.Boss.transform.position, direction, length, width);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }
        }

        private IEnumerator AdvanceDuringThrust(
            BossActionContext context,
            Vector2 direction,
            HackerAttackRangeIndicator indicator,
            HashSet<PlayerCombatController> hitPlayers,
            float seconds,
            bool applyDamage)
        {
            if (seconds <= 0f || thrustAdvanceSpeed <= 0f)
            {
                yield break;
            }

            Vector2 movementDirection = HackerMeleeAttackAction.GetConstrainedAdvanceDirection(
                context,
                direction,
                thrustAdvanceTrackingAngleDegrees);
            try
            {
                float elapsed = 0f;
                while (elapsed < seconds)
                {
                    if (context.IsExecutionPaused)
                    {
                        context.Stop();
                        yield return null;
                        continue;
                    }

                    float progress = Mathf.Clamp01(elapsed / seconds);
                    float speedMultiplier = EvaluateSpeedCurve(thrustAdvanceSpeedCurve, progress);
                    context.Boss.SetMovementVelocity(movementDirection * (thrustAdvanceSpeed * speedMultiplier));
                    indicator?.SetThrust(context.Boss.transform.position, direction, length, width);
                    if (applyDamage)
                    {
                        ApplyLineDamage(context, context.Boss.transform.position, direction, hitPlayers);
                    }

                    elapsed += EnemyTimeScale.DeltaTime;
                    yield return null;
                }
            }
            finally
            {
                context.Stop();
            }
        }

        private static float EvaluateSpeedCurve(AnimationCurve curve, float progress)
        {
            return curve != null && curve.length > 0
                ? Mathf.Max(0f, curve.Evaluate(progress))
                : 1f;
        }
    }

    [Serializable]
    public sealed class HackerDashAction : BossAction, IBossActionDurationProvider, IHackerApproachRangeProvider
    {
        [SerializeField] private HackerDashDirection direction;
        [SerializeField, Min(0f)] private float windupSeconds = 0.2f;

        [Header("Approach")]
        [SerializeField, Min(0f)] private float approachStartDistance = 7f;
        [SerializeField, Min(0f)] private float approachStopDistance = 2f;
        [SerializeField, Min(0f)] private float approachSpeed = 12f;
        [SerializeField, Min(0f)] private float maxApproachSeconds = 0.6f;
        [SerializeField] private AnimationCurve approachSpeedCurve = AnimationCurve.EaseInOut(0f, 0.45f, 1f, 1f);

        [Header("Dash")]
        [SerializeField, Min(0.05f)] private float dashSeconds = 0.35f;
        [SerializeField, Min(0f)] private float dashSpeed = 12f;
        [SerializeField] private AnimationCurve dashSpeedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.7f);
        [SerializeField, Range(1f, 89f)] private float playerDiagonalAngleDegrees = 45f;
        [SerializeField, Min(0f)] private float diagonalIntervalSeconds = 0.15f;

        [Header("Dash Effect")]
        [Tooltip("실제 대시가 시작될 때 보스 뒤에 한 번 생성할 이펙트 프리팹입니다. 오른쪽 대시 방향을 기준으로 제작된 프리팹을 사용합니다.")]
        [SerializeField] private GameObject dashEffectPrefab;
        [Tooltip("Boss Graph Editor 하이어러키에서 이펙트 생성 위치를 드래그해 지정합니다.")]
        [SerializeField, BossGraphBossChildPath] private string dashEffectSpawnPointPath;
        [Tooltip("대시 이펙트 프리팹 원본 스케일에 곱할 배율입니다.")]
        [SerializeField, Min(0.01f)] private float dashEffectScale = 1f;

        // 기존 그래프 에셋의 설정 유실을 막기 위한 런타임 폴백이다.
        [SerializeField, HideInInspector] private BossActionPrefabEffectSettings dashEffect = new();
        [SerializeField, HideInInspector] private string approachDashEffectSpawnPointPath;
        [SerializeField, HideInInspector] private string retreatDashEffectSpawnPointPath;

        [Header("Dash Afterimage")]
        [Tooltip("대시 중 잔상을 생성하는 간격입니다.")]
        [SerializeField, Min(0.01f)] private float afterimageInterval = 0.045f;
        [Tooltip("잔상 하나가 사라지는 데 걸리는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float afterimageDuration = 0.2f;
        [Tooltip("대시 잔상이 순서대로 사용할 색상입니다. 비어 있으면 기본 네온 팔레트를 사용합니다.")]
        [SerializeField] private Color[] afterimagePalette =
        {
            new(1f, 0.82f, 0.12f, 0.44f),
            new(1f, 0.24f, 0.05f, 0.4f),
            new(1f, 0.05f, 0.48f, 0.36f),
            new(0.08f, 0.82f, 1f, 0.34f)
        };
        [Tooltip("홀로그램 대시 잔상에 추가로 곱할 알파 배율입니다.")]
        [SerializeField, Range(0f, 1f)] private float hologramAfterimageAlphaMultiplier = 0.45f;

        [Header("Hologram")]
        [Tooltip("홀로그램이 본체 경로와 갈라질 때 아래쪽으로 회전할 각도입니다.")]
        [SerializeField, Range(0f, 180f)] private float hologramRetreatAngleDegrees = 30f;
        [FormerlySerializedAs("hologramRetreatFollowDelaySeconds")]
        [Tooltip("홀로그램이 후퇴 또는 Diagonal Player 대시 후 본체의 기록 위치를 다시 따르기 전까지 대기할 시간입니다.")]
        [SerializeField, Min(0f)] private float hologramRetreatWaitSeconds = 5f;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.2f;

        float IHackerApproachRangeProvider.ApproachStartDistance => approachStartDistance;
        float IHackerApproachRangeProvider.ApproachStopDistance => approachStopDistance;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss == null)
            {
                yield break;
            }

            yield return ApproachToDashDistance(context);
            using IDisposable facingLock = context.AcquireFacingLock();
            yield return HackerMeleeAttackAction.Wait(context, windupSeconds);
            try
            {
                Vector2 playerDirection = context.GetDirectionToPlayer(context.Boss.transform.position);
                if (direction == HackerDashDirection.DiagonalPlayer)
                {
                    context.SetDashing(true);
                    yield return DashInDirection(
                        context,
                        Rotate(playerDirection, playerDiagonalAngleDegrees),
                        false);
                    context.SetDashing(false);
                    context.Stop();
                    yield return HackerMeleeAttackAction.Wait(context, diagonalIntervalSeconds);
                    context.SetDashing(true);
                    yield return DashInDirection(
                        context,
                        Rotate(playerDirection, -playerDiagonalAngleDegrees),
                        false);

                    if (context.Boss is HackerHologramBoss diagonalHologram)
                    {
                        diagonalHologram.PauseRecordedPositionFollowing(hologramRetreatWaitSeconds);
                        yield return HackerMeleeAttackAction.Wait(
                            context,
                            hologramRetreatWaitSeconds);
                    }
                }
                else
                {
                    Vector2 dashDirection = direction == HackerDashDirection.Retreat
                        ? -playerDirection
                        : playerDirection;
                    context.SetDashing(true);
                    yield return DashInDirection(
                        context,
                        dashDirection,
                        direction == HackerDashDirection.Approach);

                    if (direction == HackerDashDirection.Retreat
                        && context.Boss is HackerHologramBoss hologram)
                    {
                        hologram.PauseRecordedPositionFollowing(hologramRetreatWaitSeconds);
                        yield return HackerMeleeAttackAction.Wait(
                            context,
                            hologramRetreatWaitSeconds);
                    }
                }
            }
            finally
            {
                context.SetDashing(false);
                context.Stop();
            }

            yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            float dashCount = direction == HackerDashDirection.DiagonalPlayer ? 2f : 1f;
            seconds = Mathf.Max(0f, windupSeconds)
                + Mathf.Max(0.05f, dashSeconds) * dashCount
                + (direction == HackerDashDirection.DiagonalPlayer ? Mathf.Max(0f, diagonalIntervalSeconds) : 0f)
                + Mathf.Max(0f, recoverySeconds);
            return true;
        }

        private IEnumerator DashInDirection(
            BossActionContext context,
            Vector2 dashDirection,
            bool stopAtApproachDistance)
        {
            if (context?.Boss == null)
            {
                yield break;
            }

            dashDirection = dashDirection.sqrMagnitude > 0.0001f ? dashDirection.normalized : Vector2.right;
            PlayDashEffect(context, dashDirection);
            float elapsed = 0f;
            float nextAfterimageAt = 0f;
            int afterimageColorIndex = 0;
            while (elapsed < dashSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                float progress = Mathf.Clamp01(elapsed / dashSeconds);
                if (direction == HackerDashDirection.Retreat
                    && context.Boss is HackerHologramBoss hologram)
                {
                    hologram.SetReplayPositionArcOffset(hologramRetreatAngleDegrees * progress);
                }

                float speedMultiplier = EvaluateSpeedCurve(dashSpeedCurve, progress);
                float currentSpeed = dashSpeed * speedMultiplier;
                if (stopAtApproachDistance)
                {
                    float landingDistance = Mathf.Min(approachStartDistance, approachStopDistance);
                    float remainingDistance = context.Boss.DistanceToPlayer() - landingDistance;
                    if (remainingDistance <= 0f)
                    {
                        yield break;
                    }

                    dashDirection = context.GetDirectionToPlayer(context.Boss.transform.position);
                    float deltaTime = Mathf.Max(0.0001f, EnemyTimeScale.DeltaTime);
                    currentSpeed = Mathf.Min(currentSpeed, remainingDistance / deltaTime);
                }

                context.Boss.SetMovementVelocity(dashDirection * currentSpeed);

                if (elapsed >= nextAfterimageAt)
                {
                    SpawnAfterimage(context.Boss, afterimageColorIndex);
                    afterimageColorIndex++;
                    nextAfterimageAt += Mathf.Max(0.01f, afterimageInterval);
                }

                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }
        }

        private void PlayDashEffect(BossActionContext context, Vector2 dashDirection)
        {
            if (dashEffectPrefab != null)
            {
                HackerDashEffect.PlayPrefab(
                    dashEffectPrefab,
                    context,
                    dashEffectSpawnPointPath,
                    dashEffectScale,
                    dashDirection);
                return;
            }

            string legacySpawnPointPath = direction == HackerDashDirection.Retreat
                ? retreatDashEffectSpawnPointPath
                : approachDashEffectSpawnPointPath;
            HackerDashEffect.Play(
                dashEffect,
                context,
                dashDirection,
                legacySpawnPointPath);
        }

        private void SpawnAfterimage(BossAI boss, int colorIndex)
        {
            SpriteRenderer[] renderers = GetAfterimageRenderers(boss);
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            Color tint = ResolveAfterimageColor(colorIndex);
            if (boss is HackerHologramBoss)
            {
                tint.a *= Mathf.Clamp01(hologramAfterimageAlphaMultiplier);
            }

            PlayerDashVfx.SpawnRollAfterimage(
                boss,
                renderers,
                null,
                Mathf.Max(0.01f, afterimageDuration),
                tint);
        }

        private static SpriteRenderer[] GetAfterimageRenderers(BossAI boss)
        {
            SpriteRenderer[] sourceRenderers = boss != null ? boss.BodyRenderers : null;
            if (sourceRenderers == null || sourceRenderers.Length == 0)
            {
                return sourceRenderers;
            }

            int includedCount = 0;
            for (int i = 0; i < sourceRenderers.Length; i++)
            {
                if (!IsExecutionIndicatorRenderer(sourceRenderers[i]))
                {
                    includedCount++;
                }
            }

            if (includedCount == sourceRenderers.Length)
            {
                return sourceRenderers;
            }

            SpriteRenderer[] filteredRenderers = new SpriteRenderer[includedCount];
            int targetIndex = 0;
            for (int i = 0; i < sourceRenderers.Length; i++)
            {
                SpriteRenderer renderer = sourceRenderers[i];
                if (IsExecutionIndicatorRenderer(renderer))
                {
                    continue;
                }

                filteredRenderers[targetIndex++] = renderer;
            }

            return filteredRenderers;
        }

        private static bool IsExecutionIndicatorRenderer(SpriteRenderer renderer)
        {
            return renderer != null
                && renderer.name.StartsWith("ExecutionIndicator_Click", StringComparison.Ordinal);
        }

        private Color ResolveAfterimageColor(int colorIndex)
        {
            if (afterimagePalette != null && afterimagePalette.Length > 0)
            {
                return afterimagePalette[colorIndex % afterimagePalette.Length];
            }

            return (colorIndex % 4) switch
            {
                0 => new Color(1f, 0.82f, 0.12f, 0.44f),
                1 => new Color(1f, 0.24f, 0.05f, 0.4f),
                2 => new Color(1f, 0.05f, 0.48f, 0.36f),
                _ => new Color(0.08f, 0.82f, 1f, 0.34f)
            };
        }

        private IEnumerator ApproachToDashDistance(BossActionContext context)
        {
            if ((direction != HackerDashDirection.Approach && direction != HackerDashDirection.DiagonalPlayer)
                || context?.Boss == null
                || approachSpeed <= 0f
                || maxApproachSeconds <= 0f
                || context.Boss.DistanceToPlayer() <= approachStartDistance)
            {
                yield break;
            }

            float stopDistance = direction == HackerDashDirection.Approach
                ? approachStartDistance
                : Mathf.Min(approachStartDistance, approachStopDistance);
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

        private static float EvaluateSpeedCurve(AnimationCurve curve, float progress)
        {
            return curve != null && curve.length > 0
                ? Mathf.Max(0f, curve.Evaluate(progress))
                : 1f;
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(
                direction.x * cosine - direction.y * sine,
                direction.x * sine + direction.y * cosine);
        }
    }
}
