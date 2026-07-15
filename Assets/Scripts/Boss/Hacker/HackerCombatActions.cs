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

    [Serializable]
    public sealed class HackerMeleeAttackAction : BossAction, IBossActionDurationProvider, IHackerApproachRangeProvider
    {
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
        [SerializeField, Min(0.05f)] private float parryIndicatorRadius = 0.38f;

        [Header("Hacking")]
        [SerializeField, Min(1)] private int hackingPerHit = 1;

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
            HackerMeleeParryWindow parryWindow = null;
            bool isHologram = context.Boss is HackerHologramBoss;
            Vector2 attackDirection = GetPlayerSideDirection(context);
            if (context.Boss is HackerBossAI hacker)
            {
                hacker.FaceHorizontalDirection(attackDirection.x);
            }

            context.SetFacingLocked(true);
            try
            {
                context.PlayAnimationTrigger(GetAnimationTrigger());
                GetMeleeEllipse(context, attackDirection, out Vector2 ellipseCenter, out float ellipseAngleDegrees);
                rangeIndicator = HackerAttackRangeIndicator.CreateEllipse(
                    ellipseCenter,
                    range,
                    ellipseMinorRadius,
                    ellipseAngleDegrees);
                rangeIndicator.SetHologramStyle(isHologram);
                yield return WaitWithMeleeEllipseIndicator(
                    context,
                    attackDirection,
                    Mathf.Max(0f, windupSeconds - parryWindowSeconds),
                    rangeIndicator);

                bool wasParried = false;
                GameObject parryObject = new("HackerMeleeParryWindow");
                Transform parryAnchor = context.GetBossChildTransform(parryAnchorPath) ?? context.Boss.transform;
                parryObject.transform.position = parryAnchor.position;
                parryWindow = parryObject.AddComponent<HackerMeleeParryWindow>();
                parryWindow.SetHologramStyle(isHologram);
                parryWindow.Initialize(
                    parryAnchor,
                    parryIndicatorRadius,
                    parryWindowSeconds,
                    () => wasParried = true);

                float remainingWindup = Mathf.Min(windupSeconds, parryWindowSeconds);
                float elapsed = 0f;
                while (elapsed < remainingWindup && !wasParried)
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

                if (parryWindow != null)
                {
                    UnityEngine.Object.Destroy(parryWindow.gameObject);
                    parryWindow = null;
                }

                if (wasParried)
                {
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
                if (parryWindow != null)
                {
                    UnityEngine.Object.Destroy(parryWindow.gameObject);
                }

                HackerAttackRangeIndicator.Destroy(rangeIndicator);
                context.SetFacingLocked(false);
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

                if (player.ReceiveAttack(damage, ellipseCenter, majorAxis))
                {
                    if (context.Boss is HackerBossAI hacker)
                    {
                        hacker.ApplyHacking(player, hackingPerHit);
                    }
                }
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

            UnityEngine.Object.Instantiate(consecutiveSlamDustPrefab, context.Boss.transform.position, Quaternion.identity);
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
        [SerializeField, Min(0.05f)] private float parryIndicatorRadius = 0.38f;

        [SerializeField, Min(1)] private int hackingPerHit = 1;

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

            context.SetFacingLocked(true);
            context.PlayAnimationTrigger(animationTriggerName);
            HackerAttackRangeIndicator rangeIndicator = HackerAttackRangeIndicator.CreateThrust(origin, direction, length, width);
            rangeIndicator.SetHologramStyle(isHologram);
            float thrustExecutionSeconds = Mathf.Max(0f, thrustAdvanceSeconds);
            yield return WaitWithThrustIndicator(
                context,
                direction,
                Mathf.Max(0f, windupSeconds - parryWindowSeconds),
                rangeIndicator);

            bool wasParried = false;
            GameObject parryObject = new("HackerThrustParryWindow");
            Transform parryAnchor = context.GetBossChildTransform(parryAnchorPath) ?? context.Boss.transform;
            Transform bossTransform = context.Boss.transform;
            Vector3 parryWorldOffset = parryAnchor.position - bossTransform.position;
            parryWorldOffset.x = Mathf.Abs(parryWorldOffset.x) * Mathf.Sign(direction.x);
            parryObject.transform.position = bossTransform.position + parryWorldOffset;
            HackerMeleeParryWindow parryWindow = parryObject.AddComponent<HackerMeleeParryWindow>();
            parryWindow.SetHologramStyle(isHologram);
            parryWindow.Initialize(
                bossTransform,
                parryWorldOffset,
                parryIndicatorRadius,
                parryWindowSeconds,
                () => wasParried = true);

            float remainingWindup = Mathf.Min(windupSeconds, parryWindowSeconds);
            float elapsed = 0f;
            while (elapsed < remainingWindup && !wasParried)
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

            if (parryWindow != null)
            {
                UnityEngine.Object.Destroy(parryWindow.gameObject);
            }
            if (wasParried)
            {
                context.SetFacingLocked(false);
                HackerAttackRangeIndicator.Destroy(rangeIndicator);
                context.Stop();
                float cancelledAttackSeconds = Mathf.Max(0f, remainingWindup - elapsed)
                    + thrustExecutionSeconds;
                yield return HackerMeleeAttackAction.Wait(context, cancelledAttackSeconds);
                yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
                yield break;
            }

            HashSet<PlayerCombatController> hitPlayers = new();
            rangeIndicator?.SetFillVisible(true);
            ApplyLineDamage(context, context.Boss.transform.position, direction, hitPlayers);
            IEnumerator payload = CreateThrustPayload(context, direction);
            if (payload != null)
            {
                context.Boss.StartCoroutine(payload);
            }

            yield return AdvanceDuringThrust(
                context,
                direction,
                rangeIndicator,
                hitPlayers,
                thrustExecutionSeconds);
            context.SetFacingLocked(false);
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
                        if (context.Boss is HackerBossAI hacker)
                        {
                            hacker.ApplyHacking(player, hackingPerHit);
                        }
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
            float seconds)
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
                    ApplyLineDamage(context, context.Boss.transform.position, direction, hitPlayers);
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
        [SerializeField] private string animationTriggerName = "Dash";
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

        [Header("Hologram")]
        [SerializeField, Range(-180f, 180f)] private float hologramRetreatAngleDegrees = 30f;
        [SerializeField, Min(0f)] private float hologramRetreatFollowDelaySeconds = 5f;
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
            context.PlayAnimationTrigger(animationTriggerName);
            context.SetFacingLocked(true);
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
                }
                else
                {
                    Vector2 dashDirection = direction == HackerDashDirection.Retreat
                        ? -playerDirection
                        : playerDirection;
                    if (direction == HackerDashDirection.Retreat
                        && context.Boss is HackerHologramBoss)
                    {
                        dashDirection = Rotate(dashDirection, hologramRetreatAngleDegrees);
                    }

                    context.SetDashing(true);
                    yield return DashInDirection(
                        context,
                        dashDirection,
                        direction == HackerDashDirection.Approach);

                    if (direction == HackerDashDirection.Retreat
                        && context.Boss is HackerHologramBoss hologram)
                    {
                        hologram.PauseRecordedPositionFollowing(hologramRetreatFollowDelaySeconds);
                        yield return HackerMeleeAttackAction.Wait(
                            context,
                            hologramRetreatFollowDelaySeconds);
                    }
                }
            }
            finally
            {
                context.SetDashing(false);
                context.SetFacingLocked(false);
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
            float elapsed = 0f;
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

                if (direction == HackerDashDirection.Retreat
                    && context.Boss is HackerHologramBoss hologram)
                {
                    hologram.AddReplayPositionOffset(
                        dashDirection * (currentSpeed * EnemyTimeScale.DeltaTime));
                }
                else
                {
                    context.Boss.SetMovementVelocity(dashDirection * currentSpeed);
                }

                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }
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
