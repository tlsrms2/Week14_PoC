using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionConductorScoreLaneRushSpecialAction : MinionConductorScoreLaneRushAction
    {
        private const float LaneShrinkPushPadding = 0.02f;

        private static readonly ConductorScoreLaneSide[] IndicatorSideOrder =
        {
            ConductorScoreLaneSide.Top,
            ConductorScoreLaneSide.Right,
            ConductorScoreLaneSide.Bottom,
            ConductorScoreLaneSide.Left
        };

        private static readonly LaneStep[] ExecutionOrder =
        {
            TopStep,
            RightStep,
            BottomStep,
            LeftStep
        };

        [SerializeField] private bool drawLaneIndicators = true;
        [SerializeField, InspectorName("Volleys")] private List<Volley> specialVolleys = new() { new Volley() };
        [Header("Lane Indicators")]
        [SerializeField] private Color laneIndicatorColor = new(0.62f, 0.92f, 1f, 0.66f);
        [SerializeField, Min(0.001f)] private float laneIndicatorWidth = 0.035f;
        [SerializeField, Min(0f)] private float laneIndicatorRevealSeconds = 0.05f;
        [SerializeField, Min(0f)] private float laneIndicatorRevealInterval = 0.01f;
        [SerializeField, Min(0f)] private float laneIndicatorHideSeconds = 0.14f;
        [SerializeField, Min(0f)] private float postProjectileClearDelaySeconds = 0.5f;
        [SerializeField] private int laneIndicatorSortingOrder = 66;
        [SerializeField, Min(1)] private int laneIndicatorLineCount = 4;
        [Header("Pattern Camera")]
        [SerializeField, Min(0f)] private float cameraFocusDelaySeconds;
        [SerializeField] private Vector2 cameraFocusWorldCenter;
        [Header("Rush Start Cross Fire")]
        [SerializeField, BossGraphProjectileName] private string rushStartProjectileName = "Default";
        [SerializeField, Min(0f)] private float rushStartLineupWaitSeconds = 0.25f;
        [SerializeField, Min(0f)] private float rushStartFireSeconds = 1f;
        [SerializeField, Min(0.01f)] private float rushStartFireInterval = 0.15f;

        private readonly List<EnemyProjectile> trackedProjectiles = new();
        private ConductorScoreLaneRushIndicatorVisual activeLaneIndicators;
        private BossProjectileSettings rushStartProjectile;
        private MinionGraphProjectileFireSpec rushStartFireSpec;
        private float nextRushStartFireSeconds;

        public override bool TryGetDurationSeconds(BossActionContext context, out float seconds)
        {
            int volleyCount = Mathf.Min(ExecutionOrder.Length, CountAvailableVolleys(specialVolleys));
            if (volleyCount <= 0)
            {
                seconds = 0f;
                return false;
            }

            float laneIndicatorRevealSeconds = GetLaneIndicatorRevealDuration();
            float volleySequenceSeconds = EstimateVolleySequenceDuration(specialVolleys, volleyCount);
            float trackedProjectileRemainingSeconds = Mathf.Max(
                EstimateTrackedProjectileRemainingSeconds(context, specialVolleys, volleyCount, volleySequenceSeconds),
                EstimateRushStartProjectileRemainingSeconds(context));
            float patternSeconds = WindupSeconds
                + laneIndicatorRevealSeconds
                + GetInitialLaneShrinkPreludeSeconds()
                + volleySequenceSeconds
                + trackedProjectileRemainingSeconds
                + Mathf.Max(0f, postProjectileClearDelaySeconds)
                + GetLaneIndicatorHideDuration();
            seconds = Mathf.Max(EstimateBossTargetMoveSeconds(context), patternSeconds);
            return seconds > 0f;
        }

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host)
                || context.Boss == null)
            {
                yield break;
            }

            List<ExecutionVolley> executionVolleys = BuildExecutionVolleys();
            if (executionVolleys.Count == 0)
            {
                yield break;
            }

            Vector2 patternStartPlayerPosition = ResolvePatternCenter(context);
            Coroutine bossMoveRoutine = context.Boss.StartCoroutine(MoveBossToTargetPosition(context));
            ConductorPatternCameraFocus cameraFocus = ConductorPatternCameraFocus.Start(
                context,
                cameraFocusDelaySeconds,
                cameraFocusWorldCenter);
            try
            {
                yield return MinionGraphCommandRunner.WaitWindupIfNeeded(context, WindupSeconds);
                CommandLaneCreationWander(
                    host.GetControlledMinionsForGraph(),
                    GetLaneIndicatorRevealDuration());
                yield return BeforeExecuteVolleys(context, patternStartPlayerPosition);
                yield return ExecuteVolleySequence(context, host, patternStartPlayerPosition, executionVolleys);
                yield return AfterExecuteVolleys(context, patternStartPlayerPosition);
                if (bossMoveRoutine != null)
                {
                    yield return bossMoveRoutine;
                }
            }
            finally
            {
                cameraFocus?.Dispose();

                if (bossMoveRoutine != null && context?.Boss != null)
                {
                    context.Boss.StopCoroutine(bossMoveRoutine);
                }

                context?.Stop();
                ClearActiveLaneIndicators();
                ClearTrackedProjectiles();
            }
        }

        protected override IEnumerator BeforeExecuteVolleys(BossActionContext context, Vector2 patternStartPlayerPosition)
        {
            ClearTrackedProjectiles();
            ClearActiveLaneIndicators();
            activeLaneIndicators = CreateLaneIndicators(
                patternStartPlayerPosition,
                InitialLaneDistance);
            yield return RevealLaneIndicators(context, activeLaneIndicators);
            yield return context.WaitSeconds(InitialLaneHoldSeconds);
            yield return ShrinkLaneIndicators(context, activeLaneIndicators, patternStartPlayerPosition);
        }

        protected override IEnumerator AfterExecuteVolleys(BossActionContext context, Vector2 patternStartPlayerPosition)
        {
            yield return WaitForTrackedProjectiles(context);
            yield return context.WaitSeconds(postProjectileClearDelaySeconds);
            yield return HideLaneIndicators(context, activeLaneIndicators);
            activeLaneIndicators = null;
            ClearTrackedProjectiles();
        }

        protected override void OnVolleyProjectileFired(
            BossActionContext context,
            ExecutionVolley volley,
            FireTiming timing,
            BossProjectileSettings projectile,
            EnemyProjectile spawned)
        {
            TrackProjectile(spawned);
        }

        protected override float GetAdditionalVolleyWaitSeconds(ExecutionVolley volley)
        {
            return GetRushStartCrossFireEndSeconds(volley);
        }

        protected override float GetAdditionalRushStartDelaySeconds(ExecutionVolley volley, int minionNumber)
        {
            return Mathf.Max(0f, rushStartLineupWaitSeconds);
        }

        protected override float EstimateLongestVolleyWaitSeconds(IReadOnlyList<Volley> pool, bool hasNextVolley)
        {
            float movementSeconds = MoveToStartSeconds
                + MaxStartDelaySeconds
                + Mathf.Max(0f, rushStartLineupWaitSeconds)
                + (RushDistance / RushSpeed);
            return Mathf.Max(
                base.EstimateLongestVolleyWaitSeconds(pool, hasNextVolley),
                Mathf.Max(movementSeconds, GetRushStartCrossFireEndSeconds(null)));
        }

        protected override void OnVolleyRushStarted(
            BossActionContext context,
            IMinionPatternHost host,
            ExecutionVolley volley,
            IReadOnlyList<Minion> minions)
        {
            rushStartProjectile = host?.ResolveMinionProjectileSettings(rushStartProjectileName);
            rushStartFireSpec = new MinionGraphProjectileFireSpec(
                    MinionOrigin,
                    null,
                    Effects,
                    context)
                .WithFixedDirection(GetRushStartCrossFireDirection(volley));
            nextRushStartFireSeconds = GetRushStartCrossFireStartSeconds(volley);
        }

        private static Vector2 GetRushStartCrossFireDirection(ExecutionVolley volley)
        {
            Vector2 towardCenter = -GetSideOffset(volley.Side);
            return IsHorizontalRush(volley.Side)
                ? new Vector2(0f, towardCenter.y)
                : new Vector2(towardCenter.x, 0f);
        }

        private float GetRushStartCrossFireStartSeconds(ExecutionVolley volley)
        {
            float moveToStartSeconds = volley != null ? volley.MoveToStartSeconds : MoveToStartSeconds;
            return moveToStartSeconds
                + MaxStartDelaySeconds
                + Mathf.Max(0f, rushStartLineupWaitSeconds);
        }

        private float GetRushStartCrossFireEndSeconds(ExecutionVolley volley)
        {
            return GetRushStartCrossFireStartSeconds(volley) + Mathf.Max(0f, rushStartFireSeconds);
        }

        protected override void TickVolleyRush(
            BossActionContext context,
            IMinionPatternHost host,
            ExecutionVolley volley,
            IReadOnlyList<Minion> minions,
            float elapsed)
        {
            if (rushStartProjectile == null
                || rushStartFireSeconds <= 0f
                || rushStartFireInterval <= 0f
                || minions == null)
            {
                return;
            }

            float fireEndSeconds = GetRushStartCrossFireEndSeconds(volley);
            while (elapsed >= nextRushStartFireSeconds
                && nextRushStartFireSeconds < fireEndSeconds)
            {
                for (int i = 0; i < minions.Count; i++)
                {
                    Minion minion = minions[i];
                    if (minion == null || minion.Health == null || minion.Health.IsDead)
                    {
                        continue;
                    }

                    EnemyProjectile spawned = minion.FireOnce(rushStartProjectile, rushStartFireSpec, i);
                    spawned?.ConfigurePathIndicatorDelayedUntilLaunch(true);
                    TrackProjectile(spawned);
                }

                nextRushStartFireSeconds += rushStartFireInterval;
            }
        }

        protected override List<ExecutionVolley> BuildExecutionVolleys()
        {
            return BuildExecutionVolleysFromPool(specialVolleys, ExecutionOrder);
        }

        private float GetLaneIndicatorRevealDuration()
        {
            if (!drawLaneIndicators)
            {
                return 0f;
            }

            int laneCount = IndicatorSideOrder.Length * Mathf.Max(1, laneIndicatorLineCount);
            return laneCount * (Mathf.Max(0f, laneIndicatorRevealSeconds) + Mathf.Max(0f, laneIndicatorRevealInterval));
        }

        private float GetLaneIndicatorHideDuration()
        {
            return drawLaneIndicators ? Mathf.Max(0f, laneIndicatorHideSeconds) : 0f;
        }

        private float EstimateTrackedProjectileRemainingSeconds(
            BossActionContext context,
            IReadOnlyList<Volley> pool,
            int volleyCount,
            float sequenceSeconds)
        {
            if (pool == null || !MinionGraphActionHost.TryGet(context, out IMinionPatternHost host))
            {
                return 0f;
            }

            float sequenceElapsed = 0f;
            float maxRemainingSeconds = 0f;
            int count = Mathf.Max(0, volleyCount);
            for (int volleyIndex = 0; volleyIndex < count; volleyIndex++)
            {
                bool hasNextVolley = volleyIndex < count - 1;
                float volleyWaitSeconds = EstimateLongestVolleyWaitSeconds(pool, hasNextVolley);
                maxRemainingSeconds = Mathf.Max(
                    maxRemainingSeconds,
                    EstimatePoolProjectileRemainingSeconds(host, pool, sequenceElapsed, sequenceSeconds));
                sequenceElapsed += volleyWaitSeconds;
                if (hasNextVolley)
                {
                    sequenceElapsed += RestSeconds;
                }
            }

            return maxRemainingSeconds;
        }

        private static float EstimatePoolProjectileRemainingSeconds(
            IMinionPatternHost host,
            IReadOnlyList<Volley> pool,
            float sequenceElapsed,
            float sequenceSeconds)
        {
            float maxRemainingSeconds = 0f;
            for (int volleyIndex = 0; volleyIndex < pool.Count; volleyIndex++)
            {
                IReadOnlyList<FireTiming> timings = pool[volleyIndex]?.FireTimings;
                if (timings == null)
                {
                    continue;
                }

                for (int timingIndex = 0; timingIndex < timings.Count; timingIndex++)
                {
                    FireTiming timing = timings[timingIndex];
                    if (timing == null)
                    {
                        continue;
                    }

                    BossProjectileSettings projectile = host.ResolveMinionProjectileSettings(timing.ProjectileName);
                    if (projectile == null)
                    {
                        continue;
                    }

                    float activeSeconds = Mathf.Max(0f, projectile.ChargeSeconds)
                        + Mathf.Max(0f, projectile.Lifetime);
                    float remainingSeconds = sequenceElapsed + timing.FireSeconds + activeSeconds - sequenceSeconds;
                    maxRemainingSeconds = Mathf.Max(maxRemainingSeconds, remainingSeconds);
                }
            }

            return Mathf.Max(0f, maxRemainingSeconds);
        }

        private float EstimateRushStartProjectileRemainingSeconds(BossActionContext context)
        {
            if (rushStartFireSeconds <= 0f
                || rushStartFireInterval <= 0f
                || !MinionGraphActionHost.TryGet(context, out IMinionPatternHost host))
            {
                return 0f;
            }

            BossProjectileSettings projectile = host.ResolveMinionProjectileSettings(rushStartProjectileName);
            if (projectile == null)
            {
                return 0f;
            }

            return Mathf.Max(0f, projectile.ChargeSeconds) + Mathf.Max(0f, projectile.Lifetime);
        }

        private ConductorScoreLaneRushIndicatorVisual CreateLaneIndicators(Vector2 center, float lineDistance)
        {
            GameObject indicatorObject = new("ConductorScoreLaneRushSpecialIndicators");
            ConductorScoreLaneRushIndicatorVisual visual = indicatorObject.AddComponent<ConductorScoreLaneRushIndicatorVisual>();
            Color color = drawLaneIndicators
                ? laneIndicatorColor
                : new Color(laneIndicatorColor.r, laneIndicatorColor.g, laneIndicatorColor.b, 0f);
            visual.Configure(color, laneIndicatorWidth, laneIndicatorSortingOrder);
            visual.ConfigurePlayerBlocking(true);
            visual.ConfigureClearOnExecutionCinematic(true);

            int lineIndex = 0;
            for (int i = 0; i < IndicatorSideOrder.Length; i++)
            {
                ConductorScoreLaneSide side = IndicatorSideOrder[i];
                int lineCount = Mathf.Max(1, laneIndicatorLineCount);
                for (int laneIndex = 0; laneIndex < lineCount; laneIndex++)
                {
                    BuildLaneIndicatorLine(
                        center,
                        side,
                        laneIndex,
                        lineCount,
                        lineDistance,
                        out Vector2 start,
                        out Vector2 end);
                    visual.SetLane(lineIndex, start, end);
                    lineIndex++;
                }
            }

            return visual;
        }

        private IEnumerator ShrinkLaneIndicators(
            BossActionContext context,
            ConductorScoreLaneRushIndicatorVisual visual,
            Vector2 center)
        {
            if (visual == null)
            {
                yield break;
            }

            float initialDistance = InitialLaneDistance;
            float finalDistance = LineDistanceFromPlayer;
            float duration = LaneShrinkSeconds;
            if (duration <= 0f)
            {
                UpdateLaneIndicatorPositions(visual, center, finalDistance);
                PushPlayerWithShrinkingLanes(context, center, initialDistance, finalDistance);
                yield break;
            }

            float elapsed = 0f;
            float previousDistance = initialDistance;
            while (elapsed < duration)
            {
                if (context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                float progress = Mathf.Clamp01(elapsed / duration);
                float distance = Mathf.Lerp(initialDistance, finalDistance, progress);
                UpdateLaneIndicatorPositions(visual, center, distance);
                PushPlayerWithShrinkingLanes(context, center, previousDistance, distance);
                previousDistance = distance;
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            UpdateLaneIndicatorPositions(visual, center, finalDistance);
            PushPlayerWithShrinkingLanes(context, center, previousDistance, finalDistance);
        }

        private void PushPlayerWithShrinkingLanes(
            BossActionContext context,
            Vector2 center,
            float previousDistance,
            float currentDistance)
        {
            if (currentDistance >= previousDistance
                || context?.Boss?.Player == null
                || !TryGetPlayerBodyAndColliders(context.Boss.Player, out Rigidbody2D body, out Collider2D[] colliders))
            {
                return;
            }

            int lineCount = Mathf.Max(1, laneIndicatorLineCount);
            for (int sideIndex = 0; sideIndex < IndicatorSideOrder.Length; sideIndex++)
            {
                ConductorScoreLaneSide side = IndicatorSideOrder[sideIndex];
                for (int laneIndex = 0; laneIndex < lineCount; laneIndex++)
                {
                    if (!TryGetPlayerBounds(colliders, out Bounds playerBounds))
                    {
                        return;
                    }

                    BuildLaneIndicatorLine(
                        center,
                        side,
                        laneIndex,
                        lineCount,
                        previousDistance,
                        out Vector2 previousStart,
                        out Vector2 previousEnd);
                    BuildLaneIndicatorLine(
                        center,
                        side,
                        laneIndex,
                        lineCount,
                        currentDistance,
                        out Vector2 currentStart,
                        out Vector2 currentEnd);
                    if (!TryGetLaneShrinkPushDisplacement(
                            side,
                            playerBounds,
                            previousStart,
                            previousEnd,
                            currentStart,
                            currentEnd,
                            out Vector2 displacement))
                    {
                        continue;
                    }

                    Vector2 currentPosition = body.position;
                    Vector2 targetPosition = GroundMovementConstraint.ClampStep(
                        currentPosition,
                        currentPosition + displacement,
                        colliders);
                    body.position = targetPosition;
                    RemovePlayerOutwardVelocity(body, GetSideOffset(side));
                }
            }
        }

        private static bool TryGetPlayerBodyAndColliders(
            Transform player,
            out Rigidbody2D body,
            out Collider2D[] colliders)
        {
            body = player != null ? player.GetComponentInParent<Rigidbody2D>() : null;
            if (body == null && player != null)
            {
                body = player.GetComponentInChildren<Rigidbody2D>();
            }

            colliders = body != null ? body.GetComponentsInChildren<Collider2D>() : null;
            return body != null && colliders != null && colliders.Length > 0;
        }

        private static bool TryGetPlayerBounds(Collider2D[] colliders, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            if (colliders == null)
            {
                return false;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return hasBounds;
        }

        private static bool TryGetLaneShrinkPushDisplacement(
            ConductorScoreLaneSide side,
            Bounds playerBounds,
            Vector2 previousStart,
            Vector2 previousEnd,
            Vector2 currentStart,
            Vector2 currentEnd,
            out Vector2 displacement)
        {
            displacement = Vector2.zero;
            if (IsHorizontalRush(side))
            {
                float segmentMinX = Mathf.Min(currentStart.x, currentEnd.x);
                float segmentMaxX = Mathf.Max(currentStart.x, currentEnd.x);
                if (playerBounds.max.x < segmentMinX || playerBounds.min.x > segmentMaxX)
                {
                    return false;
                }

                float previousY = previousStart.y;
                float currentY = currentStart.y;
                if (side == ConductorScoreLaneSide.Top)
                {
                    if (playerBounds.max.y <= currentY - LaneShrinkPushPadding
                        || playerBounds.min.y > previousY + LaneShrinkPushPadding)
                    {
                        return false;
                    }

                    displacement.y = currentY - LaneShrinkPushPadding - playerBounds.max.y;
                    return displacement.y < 0f;
                }

                if (playerBounds.min.y >= currentY + LaneShrinkPushPadding
                    || playerBounds.max.y < previousY - LaneShrinkPushPadding)
                {
                    return false;
                }

                displacement.y = currentY + LaneShrinkPushPadding - playerBounds.min.y;
                return displacement.y > 0f;
            }

            float segmentMinY = Mathf.Min(currentStart.y, currentEnd.y);
            float segmentMaxY = Mathf.Max(currentStart.y, currentEnd.y);
            if (playerBounds.max.y < segmentMinY || playerBounds.min.y > segmentMaxY)
            {
                return false;
            }

            float previousX = previousStart.x;
            float currentX = currentStart.x;
            if (side == ConductorScoreLaneSide.Left)
            {
                if (playerBounds.min.x >= currentX + LaneShrinkPushPadding
                    || playerBounds.max.x < previousX - LaneShrinkPushPadding)
                {
                    return false;
                }

                displacement.x = currentX + LaneShrinkPushPadding - playerBounds.min.x;
                return displacement.x > 0f;
            }

            if (playerBounds.max.x <= currentX - LaneShrinkPushPadding
                || playerBounds.min.x > previousX + LaneShrinkPushPadding)
            {
                return false;
            }

            displacement.x = currentX - LaneShrinkPushPadding - playerBounds.max.x;
            return displacement.x < 0f;
        }

        private static void RemovePlayerOutwardVelocity(Rigidbody2D body, Vector2 outwardDirection)
        {
            if (body == null || outwardDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 velocity = body.linearVelocity;
            float outwardSpeed = Vector2.Dot(velocity, outwardDirection);
            if (outwardSpeed > 0f)
            {
                body.linearVelocity = velocity - outwardDirection * outwardSpeed;
            }
        }

        private void UpdateLaneIndicatorPositions(
            ConductorScoreLaneRushIndicatorVisual visual,
            Vector2 center,
            float lineDistance)
        {
            if (visual == null)
            {
                return;
            }

            int lineIndex = 0;
            int lineCount = Mathf.Max(1, laneIndicatorLineCount);
            for (int i = 0; i < IndicatorSideOrder.Length; i++)
            {
                ConductorScoreLaneSide side = IndicatorSideOrder[i];
                for (int laneIndex = 0; laneIndex < lineCount; laneIndex++)
                {
                    BuildLaneIndicatorLine(
                        center,
                        side,
                        laneIndex,
                        lineCount,
                        lineDistance,
                        out Vector2 start,
                        out Vector2 end);
                    visual.SetLane(lineIndex, start, end);
                    visual.SetProgress(lineIndex, 1f);
                    lineIndex++;
                }
            }
        }

        private IEnumerator RevealLaneIndicators(BossActionContext context, ConductorScoreLaneRushIndicatorVisual visual)
        {
            if (visual == null)
            {
                yield break;
            }

            visual.SetAlpha(1f);
            for (int i = 0; i < visual.LaneCount; i++)
            {
                yield return AnimateLaneIndicatorRange(context, visual, i, 1, 0f, 1f, laneIndicatorRevealSeconds);
                if (laneIndicatorRevealInterval > 0f)
                {
                    yield return context.WaitSeconds(laneIndicatorRevealInterval);
                }
            }
        }

        private IEnumerator HideLaneIndicators(BossActionContext context, ConductorScoreLaneRushIndicatorVisual visual)
        {
            if (visual == null)
            {
                yield break;
            }

            yield return FadeLaneIndicators(context, visual, 1f, 0f, laneIndicatorHideSeconds);
            visual.ClearAndDestroy();
        }

        private IEnumerator AnimateLaneIndicatorRange(
            BossActionContext context,
            ConductorScoreLaneRushIndicatorVisual visual,
            int startIndex,
            int count,
            float from,
            float to,
            float seconds)
        {
            if (visual == null)
            {
                yield break;
            }

            float duration = Mathf.Max(0f, seconds);
            if (duration <= 0f)
            {
                SetLaneIndicatorRangeProgress(visual, startIndex, count, to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                float t = Mathf.Clamp01(elapsed / duration);
                SetLaneIndicatorRangeProgress(visual, startIndex, count, Mathf.Lerp(from, to, t));
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            SetLaneIndicatorRangeProgress(visual, startIndex, count, to);
        }

        private IEnumerator FadeLaneIndicators(
            BossActionContext context,
            ConductorScoreLaneRushIndicatorVisual visual,
            float from,
            float to,
            float seconds)
        {
            if (visual == null)
            {
                yield break;
            }

            float duration = Mathf.Max(0f, seconds);
            if (duration <= 0f)
            {
                visual.SetAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                float t = Mathf.Clamp01(elapsed / duration);
                visual.SetAlpha(Mathf.Lerp(from, to, t));
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            visual.SetAlpha(to);
        }

        private IEnumerator WaitForTrackedProjectiles(BossActionContext context)
        {
            while (HasLiveTrackedProjectile())
            {
                if (context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                yield return null;
            }
        }

        private static void SetLaneIndicatorRangeProgress(
            ConductorScoreLaneRushIndicatorVisual visual,
            int startIndex,
            int count,
            float progress)
        {
            if (visual == null)
            {
                return;
            }

            int endIndex = Mathf.Min(visual.LaneCount, startIndex + Mathf.Max(1, count));
            for (int i = startIndex; i < endIndex; i++)
            {
                visual.SetProgress(i, progress);
            }
        }

        private bool HasLiveTrackedProjectile()
        {
            for (int i = trackedProjectiles.Count - 1; i >= 0; i--)
            {
                EnemyProjectile projectile = trackedProjectiles[i];
                if (projectile == null || !projectile.gameObject.activeInHierarchy)
                {
                    if (projectile != null)
                    {
                        projectile.Destroyed -= HandleTrackedProjectileDestroyed;
                    }

                    trackedProjectiles.RemoveAt(i);
                    continue;
                }

                return true;
            }

            return false;
        }

        private void HandleTrackedProjectileDestroyed(
            EnemyProjectile projectile,
            EnemyProjectileDestroyReason reason,
            Vector3 position)
        {
            if (projectile != null)
            {
                projectile.Destroyed -= HandleTrackedProjectileDestroyed;
            }

            trackedProjectiles.Remove(projectile);
        }

        private void TrackProjectile(EnemyProjectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            trackedProjectiles.Add(projectile);
            projectile.Destroyed += HandleTrackedProjectileDestroyed;
        }

        private void ClearTrackedProjectiles()
        {
            for (int i = 0; i < trackedProjectiles.Count; i++)
            {
                if (trackedProjectiles[i] != null)
                {
                    trackedProjectiles[i].Destroyed -= HandleTrackedProjectileDestroyed;
                }
            }

            trackedProjectiles.Clear();
        }

        private void BuildLaneIndicatorLine(
            Vector2 center,
            ConductorScoreLaneSide side,
            int laneIndex,
            int laneCount,
            float lineDistance,
            out Vector2 start,
            out Vector2 end)
        {
            bool positiveDirection = GetFixedRushPositiveDirection(side);
            Vector2 rushDirection = GetRushDirection(side, positiveDirection);
            Vector2 lineAxis = IsHorizontalRush(side) ? Vector2.up : Vector2.right;
            Vector2 lineCenter = center + GetSideOffset(side) * Mathf.Max(0.1f, lineDistance);
            float centeredOffset = (Mathf.Max(1, laneCount) - 1) * 0.5f;
            Vector2 laneCenter = lineCenter + lineAxis * ((laneIndex - centeredOffset) * LineSpacing);
            start = laneCenter - rushDirection * (RushDistance * 0.5f);
            end = laneCenter + rushDirection * (RushDistance * 0.5f);
        }

        private static bool GetFixedRushPositiveDirection(ConductorScoreLaneSide side)
        {
            return side switch
            {
                ConductorScoreLaneSide.Right => false,
                ConductorScoreLaneSide.Bottom => false,
                _ => true
            };
        }

        private void ClearActiveLaneIndicators()
        {
            if (activeLaneIndicators == null)
            {
                return;
            }

            activeLaneIndicators.ClearAndDestroy();
            activeLaneIndicators = null;
        }
    }
}
