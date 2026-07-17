using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    using ScoreLaneFireTiming = MinionConductorScoreLaneRushAction.FireTiming;
    using ScoreLaneVolley = MinionConductorScoreLaneRushAction.Volley;

    [Serializable]
    public sealed class MinionConductorFormationLineVolleyAction : BossAction, IBossActionContextDurationProvider, IConductorCueOverlayEarlyStartSource
    {
        private const float DefaultFormationMoveSpeed = 24f;
        private const float FormationAlignmentTolerance = 0.08f;
        private const float ShrinkingStaffPushPadding = 0.08f;
        private const int DroneCount = 4;

        [Serializable]
        private sealed class TimedVolley
        {
            [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
            [SerializeField, Min(0f)] private float fireSeconds = 1f;
            [SerializeField, Min(0.01f)] private float fireInterval = 0.15f;
            [SerializeField, Min(0f)] private float restSeconds = 0.2f;

            public string ProjectileName => projectileName;
            public float FireSeconds => Mathf.Max(0f, fireSeconds);
            public float FireInterval => Mathf.Max(0.01f, fireInterval);
            public float RestSeconds => Mathf.Max(0f, restSeconds);
        }

        [Serializable]
        private sealed class CenterVolley
        {
            [SerializeField, Min(0f)] private float fireSeconds = 1f;
            [SerializeField, Min(0.01f)] private float fireInterval = 0.15f;
            [SerializeField, Min(0f)] private float restSeconds = 0.2f;
            [SerializeField] private List<CenterFireProjectile> projectiles = new();

            public float FireSeconds => Mathf.Max(0f, fireSeconds);
            public float FireInterval => Mathf.Max(0.01f, fireInterval);
            public float RestSeconds => Mathf.Max(0f, restSeconds);
            public IReadOnlyList<CenterFireProjectile> Projectiles => projectiles;
        }

        [Header("Shared Projectile")]
        [SerializeField] private MinionGraphProjectileOriginSpec minionOrigin = new();
        [SerializeField, InspectorName("Projectile Speed Multiplier"), Min(0.01f)] private float projectileSpeedMultiplier = 1f;
        [SerializeField] private BossGraphEffectSettings effects = new();
        [Header("Stage 3 Merge Volleys")]
        [SerializeField, InspectorName("Volleys")] private List<ScoreLaneVolley> volleys = new() { new ScoreLaneVolley() };

        [Header("Line Indicator")]
        [SerializeField] private bool drawLineIndicators = true;
        [SerializeField] private Color lineIndicatorColor = new(0.62f, 0.92f, 1f, 0.72f);
        [SerializeField, Min(0.001f)] private float lineIndicatorWidth = 0.035f;
        [SerializeField, Min(0.1f)] private float lineIndicatorLength = 12f;
        [SerializeField, Min(0f)] private float lineIndicatorFadeSeconds = 0.14f;
        [SerializeField] private int lineIndicatorSortingOrder = 66;

        [Header("Stage 1 North/South Staff")]
        [SerializeField, InspectorName("Staff Center")] private Vector2 staffCenter;
        [SerializeField, Min(0.1f)] private float northSouthStaffDistance = 4f;
        [SerializeField, Min(1f)] private float initialNorthSouthStaffDistanceMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float northSouthStaffHoldSeconds = 0.2f;
        [SerializeField, Min(0f)] private float northSouthStaffShrinkSeconds = 0.6f;
        [SerializeField, Min(0.01f)] private float staffLineSpacing = 0.6f;
        [Header("Staff Creation Wander")]
        [SerializeField, Min(0f)] private float staffCreationWanderSpeed = 3.2f;
        [SerializeField, Min(0.1f)] private float staffCreationWanderRadius = 2.8f;
        [SerializeField, Min(0.1f)] private float staffCreationWanderRetargetSeconds = 1.5f;
        [SerializeField, InspectorName("Side Drone Distance"), Min(0.1f)] private float sideDroneDistance = 12f;
        [SerializeField, Min(0.01f)] private float sideDronePairSpacing = 1.4f;
        [SerializeField, Min(0f)] private float sideDroneSetupSeconds = 0.6f;
        [SerializeField, Min(0.01f)] private float sideDronePassMoveSeconds = 0.7f;
        [SerializeField, InspectorName("Projectile Speed Multiplier"), Min(0.01f)] private float sideDroneProjectileSpeedMultiplier = 1f;
        [SerializeField, InspectorName("Volleys")] private List<TimedVolley> northSouthVolleys = new();

        [Header("Stage 2 West Staff")]
        [SerializeField, Min(0.1f)] private float westStaffDistance = 4f;
        [SerializeField, Min(1f)] private float initialWestStaffDistanceMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float westStaffHoldSeconds = 0.2f;
        [SerializeField, Min(0f)] private float westStaffShrinkSeconds = 0.6f;
        [SerializeField, Min(0.01f)] private float westStaffLineSpacing = 0.6f;
        [SerializeField, Min(0.1f)] private float westDroneDistance = 4f;
        [SerializeField, Min(0.01f)] private float westDroneLineSpacing = 1.25f;
        [SerializeField, Min(0f)] private float westStaffSetupSeconds = 0.45f;
        [SerializeField, InspectorName("Volleys")] private List<CenterVolley> westStaffVolleys = new();
        [Header("Stage 3 North/South Merge")]
        [SerializeField, Min(0f)] private float staffCollapseSeconds = 0.6f;

        [Header("Boss Reposition")]
        [SerializeField] private bool repositionBossAtPatternStart;
        [SerializeField] private Vector2 bossTargetPosition;
        [SerializeField, Min(0.01f)] private float bossMoveSpeedMultiplier = 1f;
        [SerializeField, Min(0.001f)] private float bossArriveDistance = 0.04f;
        [SerializeField, Min(0.01f)] private float bossMoveTimeoutSeconds = 5f;

        [Header("Pattern Camera")]
        [SerializeField, Min(0f)] private float cameraFocusDelaySeconds;
        [SerializeField] private Vector2 cameraFocusWorldCenter;

        [Header("Miss Launch")]
        [SerializeField, InspectorName("Line Travel Multiplier"), Min(1f)] private float missLaunchLineTravelMultiplier = 1.5f;
        [SerializeField, BossGraphProjectileName] private string missedLaunchProjectileName = "Default";
        [SerializeField, Min(0f)] private float missedLaunchIntervalSeconds = 0.12f;

        [Header("Final Release Formation")]
        [SerializeField] private bool commandFinalReleaseFormation = true;
        [SerializeField, Min(0.1f)] private float launchFormationCircleRadius = 4f;
        [SerializeField] private bool launchFormationSideBySide;
        [SerializeField, Min(1f)] private float launchFormationAngleSpacingDegrees = 45f;
        [SerializeField, Min(0f)] private float launchFormationSpeedMultiplier = 1f;
        [SerializeField, Min(0f)] private float launchFormationSettleSeconds = 0.5f;

        [Header("Final Shot")]
        [SerializeField] private bool fireFinalProjectile = true;
        [SerializeField, BossGraphProjectileName] private string finalProjectileName = "Default";
        [SerializeField] private MinionGraphProjectileOriginSpec finalProjectileOrigin = new();
        [SerializeField] private BossGraphProjectileAimSpec finalProjectileAim = new();
        [SerializeField] private BossGraphEffectSettings finalProjectileEffects = new();

        [Header("Early Clear Wander")]
        [SerializeField, Min(0f)] private float earlyClearWanderSeconds = 3f;
        [SerializeField, Min(0f)] private float earlyClearWanderSpeed = 3.2f;
        [SerializeField, Min(0.1f)] private float earlyClearWanderRadius = 2.8f;
        [SerializeField, Min(0.1f)] private float earlyClearWanderRetargetSeconds = 1.5f;

        private readonly List<ConductorFormationLineProjectileMotion> trackedMotions = new();
        private bool completedByAllProjectilesCleared;
        private float activeNorthSouthStaffDistance;
        private float activeWestStaffDistance;
        private bool isBossRepositioning;

        public bool ShouldStartConductorCueOverlayEarly => completedByAllProjectilesCleared;

        public void ClearConductorCueOverlayEarlyStart()
        {
            completedByAllProjectilesCleared = false;
        }

        public bool TryGetDurationSeconds(BossActionContext context, out float seconds)
        {
            seconds = 0f;
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host)
                || context?.Boss == null
                || context.Boss.Player == null
                || volleys == null
                || volleys.Count == 0)
            {
                return false;
            }

            List<Minion> minions = GetLineMinions(host.GetControlledMinionsForGraph());
            if (minions.Count == 0)
            {
                return false;
            }

            List<LineSlot> lineSlots = BuildStaffLineSlots(minions);

            float lineVolleySeconds = EstimateLongestLineVolleySeconds(host);
            int missCount = EstimateMaxMissLaunchCount(host);
            float finishSeconds = missCount > 0
                ? EstimateMissLaunchPhaseSeconds(context, lineSlots, missCount)
                : GetLineIndicatorFadeSeconds();

            float patternSeconds = GetStaffPreludeSeconds()
                + lineVolleySeconds
                + finishSeconds;
            seconds = Mathf.Max(patternSeconds, EstimateBossRepositionSeconds(context));
            return seconds > 0f;
        }

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host)
                || context.Boss == null
                || context.Boss.Player == null
                || volleys == null
                || volleys.Count == 0)
            {
                yield break;
            }

            yield return host.EnsureMinionCount(DroneCount);
            List<Minion> minions = GetLineMinions(host.GetControlledMinionsForGraph());
            if (minions.Count != DroneCount)
            {
                yield break;
            }

            List<LineSlot> lineSlots = BuildStaffLineSlots(minions);
            ConductorScoreLaneRushIndicatorVisual staffIndicator = null;
            ConductorScoreLaneRushIndicatorVisual volleyIndicator = null;
            Coroutine bossMoveRoutine = repositionBossAtPatternStart
                ? context.Boss.StartCoroutine(MoveBossToTargetPosition(context))
                : null;
            ConductorPatternCameraFocus cameraFocus = ConductorPatternCameraFocus.Start(
                context,
                cameraFocusDelaySeconds,
                cameraFocusWorldCenter);
            try
            {
                completedByAllProjectilesCleared = false;
                activeNorthSouthStaffDistance = GetInitialNorthSouthStaffDistance();
                SetFormationFacingOverride(minions, Vector2.left);
                CommandStaffCreationWander(
                    minions,
                    northSouthStaffHoldSeconds + northSouthStaffShrinkSeconds);
                staffIndicator = CreateStaffIndicators();
                yield return WaitSeconds(context, northSouthStaffHoldSeconds);
                yield return ShrinkNorthSouthStaff(context, staffIndicator);

                CommandSideDronePairs(minions, sideDroneSetupSeconds);
                yield return WaitSeconds(context, sideDroneSetupSeconds);
                yield return RunNorthSouthVolleys(context, host, minions);

                activeWestStaffDistance = GetInitialWestStaffDistance();
                CommandStaffCreationWander(
                    minions,
                    westStaffHoldSeconds + westStaffShrinkSeconds);
                AddWestStaff(staffIndicator);
                yield return WaitSeconds(context, westStaffHoldSeconds);
                yield return ShrinkWestStaff(context, staffIndicator);

                CommandWestDroneLine(minions, westStaffSetupSeconds);
                yield return WaitSeconds(context, westStaffSetupSeconds);
                yield return RunWestStaffVolleys(context, host, minions);
                yield return CollapseStaff(context, staffIndicator, lineSlots);

                staffIndicator?.ClearAndDestroy();
                staffIndicator = null;

                volleyIndicator = CreateLineIndicators(lineSlots);
                trackedMotions.Clear();

                yield return RunVolleys(context, host, lineSlots, volleyIndicator);
                if (completedByAllProjectilesCleared)
                {
                    CompleteEarlyAfterClearedProjectiles(minions, staffIndicator, volleyIndicator);
                    staffIndicator = null;
                    volleyIndicator = null;
                    yield break;
                }

                yield return WaitForLineProjectilesToRelease(context, lineSlots, volleyIndicator);
                if (completedByAllProjectilesCleared)
                {
                    CompleteEarlyAfterClearedProjectiles(minions, staffIndicator, volleyIndicator);
                    staffIndicator = null;
                    volleyIndicator = null;
                    yield break;
                }

                staffIndicator?.ClearAndDestroy();
                staffIndicator = null;
                volleyIndicator?.ClearAndDestroy();
                volleyIndicator = null;
                yield return RunMissLaunchPhase(context, host, lineSlots, null);
            }
            finally
            {
                cameraFocus?.Dispose();

                if (bossMoveRoutine != null && context?.Boss != null)
                {
                    context.Boss.StopCoroutine(bossMoveRoutine);
                }

                staffIndicator?.ClearAndDestroy();
                volleyIndicator?.ClearAndDestroy();

                context.Boss?.Stop();
                ClearFormationFacingOverride(minions);
                trackedMotions.Clear();
                activeNorthSouthStaffDistance = 0f;
                activeWestStaffDistance = 0f;
                isBossRepositioning = false;
            }
        }

        private void CompleteEarlyAfterClearedProjectiles(
            IReadOnlyList<Minion> minions,
            ConductorScoreLaneRushIndicatorVisual staffIndicator,
            ConductorScoreLaneRushIndicatorVisual volleyIndicator)
        {
            staffIndicator?.ClearAndDestroy();
            volleyIndicator?.ClearAndDestroy();
            CommandLineMinionsEarlyClearWander(minions);
        }

        private float GetStaffPreludeSeconds()
        {
            return Mathf.Max(0f, northSouthStaffHoldSeconds)
                + Mathf.Max(0f, northSouthStaffShrinkSeconds)
                + Mathf.Max(0f, sideDroneSetupSeconds)
                + GetNorthSouthVolleySeconds()
                + Mathf.Max(0f, westStaffHoldSeconds)
                + Mathf.Max(0f, westStaffShrinkSeconds)
                + Mathf.Max(0f, westStaffSetupSeconds)
                + GetWestStaffVolleySeconds()
                + Mathf.Max(0f, staffCollapseSeconds);
        }

        private float GetMissLaunchLineTravelDistance()
        {
            return Mathf.Max(0.1f, lineIndicatorLength)
                * Mathf.Max(1f, missLaunchLineTravelMultiplier);
        }

        private IEnumerator MoveBossToTargetPosition(BossActionContext context)
        {
            if (!repositionBossAtPatternStart || context?.Boss == null || context.Boss.Body == null)
            {
                yield break;
            }

            isBossRepositioning = true;
            try
            {
                float arriveDistance = Mathf.Max(0.001f, bossArriveDistance);
                float arriveDistanceSqr = arriveDistance * arriveDistance;
                float elapsed = 0f;
                while (((Vector2)context.Boss.Body.position - bossTargetPosition).sqrMagnitude > arriveDistanceSqr
                    && elapsed < Mathf.Max(0.01f, bossMoveTimeoutSeconds))
                {
                    if (context.IsExecutionPaused)
                    {
                        context.Boss.Stop();
                        yield return null;
                        continue;
                    }

                    Vector2 current = context.Boss.Body.position;
                    Vector2 toTarget = bossTargetPosition - current;
                    float speed = context.Boss.MoveSpeed * Mathf.Max(0.01f, bossMoveSpeedMultiplier);
                    context.Boss.SetMovementVelocity(toTarget.normalized * speed);
                    elapsed += EnemyTimeScale.DeltaTime;
                    yield return null;
                }
            }
            finally
            {
                isBossRepositioning = false;
                context?.Boss?.Stop();
            }
        }

        private float EstimateBossRepositionSeconds(BossActionContext context)
        {
            if (!repositionBossAtPatternStart || context?.Boss == null)
            {
                return 0f;
            }

            Vector2 current = context.Boss.Body != null
                ? context.Boss.Body.position
                : context.Boss.transform.position;
            float distance = Mathf.Max(
                0f,
                Vector2.Distance(current, bossTargetPosition) - Mathf.Max(0.001f, bossArriveDistance));
            float speed = context.Boss.MoveSpeed * Mathf.Max(0.01f, bossMoveSpeedMultiplier);
            float estimate = speed > 0f ? distance / speed : 0f;
            return Mathf.Min(estimate, Mathf.Max(0.01f, bossMoveTimeoutSeconds));
        }

        private float GetNorthSouthVolleySeconds()
        {
            if (northSouthVolleys == null || northSouthVolleys.Count == 0)
            {
                return 0f;
            }

            float seconds = 0f;
            for (int i = 0; i < northSouthVolleys.Count; i++)
            {
                TimedVolley volley = northSouthVolleys[i];
                if (volley != null)
                {
                    seconds += volley.FireSeconds + volley.RestSeconds;
                }
            }

            return seconds;
        }

        private float GetWestStaffVolleySeconds()
        {
            if (westStaffVolleys == null || westStaffVolleys.Count == 0)
            {
                return 0f;
            }

            float seconds = 0f;
            for (int i = 0; i < westStaffVolleys.Count; i++)
            {
                CenterVolley volley = westStaffVolleys[i];
                if (volley != null)
                {
                    seconds += volley.FireSeconds + volley.RestSeconds;
                }
            }

            return seconds;
        }

        private ConductorScoreLaneRushIndicatorVisual CreateStaffIndicators()
        {
            GameObject indicatorObject = new("ConductorFormationLineVolleyStaffs");
            ConductorScoreLaneRushIndicatorVisual visual = indicatorObject.AddComponent<ConductorScoreLaneRushIndicatorVisual>();
            Color color = drawLineIndicators
                ? lineIndicatorColor
                : new Color(lineIndicatorColor.r, lineIndicatorColor.g, lineIndicatorColor.b, 0f);
            visual.Configure(color, lineIndicatorWidth, lineIndicatorSortingOrder);
            visual.ConfigurePlayerBlocking(true);

            for (int i = 0; i < DroneCount * 2; i++)
            {
                SetNorthSouthStaffLine(visual, i, 1f);
            }

            return visual;
        }

        private void AddWestStaff(ConductorScoreLaneRushIndicatorVisual visual)
        {
            if (visual == null)
            {
                return;
            }

            for (int i = 0; i < DroneCount; i++)
            {
                SetWestStaffLine(visual, i, 1f);
            }
        }

        private void SetNorthSouthStaffLine(
            ConductorScoreLaneRushIndicatorVisual visual,
            int lineIndex,
            float progress)
        {
            if (visual == null || lineIndex < 0 || lineIndex >= DroneCount * 2)
            {
                return;
            }

            GetNorthSouthStaffLine(lineIndex, out Vector2 start, out Vector2 end);
            visual.SetLane(lineIndex, start, end);
            visual.SetProgress(lineIndex, progress);
        }

        private void GetNorthSouthStaffLine(int lineIndex, out Vector2 start, out Vector2 end)
        {
            float distance = activeNorthSouthStaffDistance > 0f
                ? activeNorthSouthStaffDistance
                : Mathf.Max(0.1f, northSouthStaffDistance);
            GetNorthSouthStaffLine(lineIndex, distance, out start, out end);
        }

        private void GetNorthSouthStaffLine(
            int lineIndex,
            float distance,
            out Vector2 start,
            out Vector2 end)
        {
            int groupIndex = lineIndex < DroneCount ? 0 : 1;
            int laneIndex = Mathf.Abs(lineIndex % DroneCount);
            float staffY = staffCenter.y + (groupIndex == 0 ? distance : -distance);
            float y = staffY + GetCenteredOffset(laneIndex, DroneCount, staffLineSpacing);
            float halfLength = Mathf.Max(0.1f, lineIndicatorLength);
            start = new Vector2(staffCenter.x - halfLength, y);
            end = new Vector2(staffCenter.x + halfLength, y);
        }

        private void GetFinalStaffLine(int laneIndex, out Vector2 start, out Vector2 end)
        {
            float y = staffCenter.y + GetCenteredOffset(laneIndex, DroneCount, staffLineSpacing);
            float halfLength = Mathf.Max(0.1f, lineIndicatorLength);
            start = new Vector2(staffCenter.x - halfLength, y);
            end = new Vector2(staffCenter.x + halfLength, y);
        }

        private void SetWestStaffLine(
            ConductorScoreLaneRushIndicatorVisual visual,
            int laneIndex,
            float progress)
        {
            if (visual == null || laneIndex < 0 || laneIndex >= DroneCount)
            {
                return;
            }

            GetWestStaffLine(laneIndex, out Vector2 start, out Vector2 end);
            int visualIndex = DroneCount * 2 + laneIndex;
            visual.SetLane(visualIndex, start, end);
            visual.SetProgress(visualIndex, progress);
        }

        private void GetWestStaffLine(int laneIndex, out Vector2 start, out Vector2 end)
        {
            float distance = activeWestStaffDistance > 0f
                ? activeWestStaffDistance
                : Mathf.Max(0.1f, westStaffDistance);
            GetWestStaffLine(laneIndex, distance, out start, out end);
        }

        private void GetWestStaffLine(
            int laneIndex,
            float distance,
            out Vector2 start,
            out Vector2 end)
        {
            float x = staffCenter.x - distance + GetCenteredOffset(laneIndex, DroneCount, westStaffLineSpacing);
            float halfLength = Mathf.Max(0.1f, lineIndicatorLength);
            start = new Vector2(x, staffCenter.y - halfLength);
            end = new Vector2(x, staffCenter.y + halfLength);
        }

        private float GetInitialNorthSouthStaffDistance()
        {
            return Mathf.Max(0.1f, northSouthStaffDistance)
                * Mathf.Max(1f, initialNorthSouthStaffDistanceMultiplier);
        }

        private float GetInitialWestStaffDistance()
        {
            return Mathf.Max(0.1f, westStaffDistance)
                * Mathf.Max(1f, initialWestStaffDistanceMultiplier);
        }

        private IEnumerator ShrinkNorthSouthStaff(
            BossActionContext context,
            ConductorScoreLaneRushIndicatorVisual visual)
        {
            float initialDistance = GetInitialNorthSouthStaffDistance();
            float targetDistance = Mathf.Max(0.1f, northSouthStaffDistance);
            float duration = Mathf.Max(0f, northSouthStaffShrinkSeconds);
            if (duration <= 0f)
            {
                activeNorthSouthStaffDistance = targetDistance;
                for (int i = 0; i < DroneCount * 2; i++)
                {
                    SetNorthSouthStaffLine(visual, i, 1f);
                }

                PushPlayerWithShrinkingNorthSouthStaff(context, initialDistance, targetDistance);

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
                activeNorthSouthStaffDistance = Mathf.Lerp(initialDistance, targetDistance, progress);
                for (int i = 0; i < DroneCount * 2; i++)
                {
                    SetNorthSouthStaffLine(visual, i, 1f);
                }

                PushPlayerWithShrinkingNorthSouthStaff(context, previousDistance, activeNorthSouthStaffDistance);
                previousDistance = activeNorthSouthStaffDistance;

                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            activeNorthSouthStaffDistance = targetDistance;
            for (int i = 0; i < DroneCount * 2; i++)
            {
                SetNorthSouthStaffLine(visual, i, 1f);
            }

            PushPlayerWithShrinkingNorthSouthStaff(context, previousDistance, targetDistance);
        }

        private IEnumerator ShrinkWestStaff(
            BossActionContext context,
            ConductorScoreLaneRushIndicatorVisual visual)
        {
            float initialDistance = GetInitialWestStaffDistance();
            float targetDistance = Mathf.Max(0.1f, westStaffDistance);
            float duration = Mathf.Max(0f, westStaffShrinkSeconds);
            if (duration <= 0f)
            {
                activeWestStaffDistance = targetDistance;
                AddWestStaff(visual);
                PushPlayerWithShrinkingWestStaff(context, initialDistance, targetDistance);
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
                activeWestStaffDistance = Mathf.Lerp(initialDistance, targetDistance, progress);
                AddWestStaff(visual);
                PushPlayerWithShrinkingWestStaff(context, previousDistance, activeWestStaffDistance);
                previousDistance = activeWestStaffDistance;
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            activeWestStaffDistance = targetDistance;
            AddWestStaff(visual);
            PushPlayerWithShrinkingWestStaff(context, previousDistance, targetDistance);
        }

        private void PushPlayerWithShrinkingNorthSouthStaff(
            BossActionContext context,
            float previousDistance,
            float currentDistance)
        {
            if (currentDistance >= previousDistance || context?.Boss?.Player == null)
            {
                return;
            }

            for (int i = 0; i < DroneCount * 2; i++)
            {
                GetNorthSouthStaffLine(i, previousDistance, out Vector2 previousStart, out Vector2 previousEnd);
                GetNorthSouthStaffLine(i, currentDistance, out Vector2 currentStart, out Vector2 currentEnd);
                GroundMovementConstraint.PushPlayerOutOfMovingLine(
                    context.Boss.Player,
                    previousStart,
                    previousEnd,
                    currentStart,
                    currentEnd,
                    ShrinkingStaffPushPadding);
            }
        }

        private void PushPlayerWithShrinkingWestStaff(
            BossActionContext context,
            float previousDistance,
            float currentDistance)
        {
            if (currentDistance >= previousDistance || context?.Boss?.Player == null)
            {
                return;
            }

            for (int i = 0; i < DroneCount; i++)
            {
                GetWestStaffLine(i, previousDistance, out Vector2 previousStart, out Vector2 previousEnd);
                GetWestStaffLine(i, currentDistance, out Vector2 currentStart, out Vector2 currentEnd);
                GroundMovementConstraint.PushPlayerOutOfMovingLine(
                    context.Boss.Player,
                    previousStart,
                    previousEnd,
                    currentStart,
                    currentEnd,
                    ShrinkingStaffPushPadding);
            }
        }

        private void CommandSideDronePairs(IReadOnlyList<Minion> minions, float moveSeconds)
        {
            if (minions == null)
            {
                return;
            }

            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion == null)
                {
                    continue;
                }

                CommandMoveToPosition(minion, GetSideDronePosition(i), moveSeconds);
            }
        }

        private Vector2 GetSideDronePosition(int index)
        {
            return GetSideCrossStartPosition(index, false);
        }

        private IEnumerator RunNorthSouthVolleys(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<Minion> minions)
        {
            if (northSouthVolleys == null || northSouthVolleys.Count == 0)
            {
                yield break;
            }

            for (int i = 0; i < northSouthVolleys.Count; i++)
            {
                TimedVolley volley = northSouthVolleys[i];
                if (volley == null)
                {
                    continue;
                }

                yield return RunSideCrossVolley(
                    context,
                    host,
                    minions,
                    volley.ProjectileName,
                    volley.FireSeconds,
                    volley.FireInterval);
                yield return WaitSeconds(context, volley.RestSeconds);
            }
        }

        private IEnumerator RunSideCrossVolley(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<Minion> minions,
            string projectileName,
            float fireSeconds,
            float fireInterval)
        {
            BossProjectileSettings projectile = host?.ResolveMinionProjectileSettings(projectileName);
            if (projectile == null || fireSeconds <= 0f || minions == null)
            {
                yield break;
            }

            MinionGraphProjectileFireSpec fireSpec = new(minionOrigin, null, effects, context);
            float elapsed = 0f;
            float nextFireSeconds = 0f;
            int passIndex = 0;
            while (elapsed < fireSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                while (elapsed >= nextFireSeconds)
                {
                    CommandSideCrossPass(minions, passIndex++);
                    for (int i = 0; i < minions.Count; i++)
                    {
                        Minion minion = minions[i];
                        if (minion == null || minion.Health == null || minion.Health.IsDead)
                        {
                            continue;
                        }

                        Vector2 direction = i < 2 ? Vector2.right : Vector2.left;
                        EnemyProjectile spawned = minion.FireOnce(projectile, fireSpec.WithFixedDirection(direction), i);
                        spawned?.ConfigureSpeedMultiplier(sideDroneProjectileSpeedMultiplier);
                    }

                    nextFireSeconds += Mathf.Max(0.01f, fireInterval);
                }

                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }
        }

        private void CommandSideCrossPass(IReadOnlyList<Minion> minions, int passIndex)
        {
            bool invert = passIndex % 2 != 0;
            float duration = Mathf.Max(0.01f, sideDronePassMoveSeconds);
            float speed = sideDronePairSpacing / duration;
            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion == null)
                {
                    continue;
                }

                bool movesUp = (i % 2 == 0) != invert;
                Vector2 start = GetSideCrossStartPosition(i, invert);
                minion.CommandScoreLaneRush(
                    start,
                    movesUp ? Vector2.up : Vector2.down,
                    0f,
                    0f,
                    sideDronePairSpacing,
                    speed);
            }
        }

        private Vector2 GetSideCrossStartPosition(int index, bool invert)
        {
            bool isLeft = index < 2;
            bool movesUp = (index % 2 == 0) != invert;
            float verticalOffset = GetSideCrossVerticalOffset(isLeft, movesUp);
            return new Vector2(
                staffCenter.x + (isLeft ? -1f : 1f) * Mathf.Max(0.1f, sideDroneDistance),
                staffCenter.y + verticalOffset);
        }

        private float GetSideCrossVerticalOffset(bool isLeft, bool movesUp)
        {
            float halfSpacing = Mathf.Max(0.01f, sideDronePairSpacing) * 0.5f;
            if (isLeft)
            {
                return movesUp ? -halfSpacing * 3f : -halfSpacing;
            }

            return movesUp ? halfSpacing : halfSpacing * 3f;
        }

        private void CommandWestDroneLine(IReadOnlyList<Minion> minions, float moveSeconds)
        {
            if (minions == null)
            {
                return;
            }

            for (int i = 0; i < minions.Count; i++)
            {
                CommandMoveToPosition(minions[i], GetWestDronePosition(i), moveSeconds);
            }
        }

        private Vector2 GetWestDronePosition(int index)
        {
            return staffCenter
                + Vector2.right * Mathf.Max(0.1f, westDroneDistance)
                + Vector2.up * GetCenteredOffset(index, DroneCount, westDroneLineSpacing);
        }

        private IEnumerator RunWestStaffVolleys(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<Minion> minions)
        {
            if (westStaffVolleys == null || westStaffVolleys.Count == 0)
            {
                yield break;
            }

            for (int i = 0; i < westStaffVolleys.Count; i++)
            {
                CenterVolley volley = westStaffVolleys[i];
                if (volley == null)
                {
                    continue;
                }

                yield return RunCenterVolley(
                    context,
                    host,
                    minions,
                    volley.FireSeconds,
                    volley.FireInterval,
                    volley.Projectiles);
                yield return WaitSeconds(context, volley.RestSeconds);
            }
        }

        private IEnumerator RunCenterVolley(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<Minion> minions,
            float fireSeconds,
            float fireInterval,
            IReadOnlyList<CenterFireProjectile> projectileSettings)
        {
            if (fireSeconds <= 0f || minions == null)
            {
                yield break;
            }

            float elapsed = 0f;
            float nextFireSeconds = 0f;
            while (elapsed < fireSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                while (elapsed >= nextFireSeconds)
                {
                    for (int i = 0; i < minions.Count; i++)
                    {
                        Minion minion = minions[i];
                        if (minion == null || minion.Health == null || minion.Health.IsDead)
                        {
                            continue;
                        }

                        int minionNumber = GetMinionNumber(minion, i);
                        BossProjectileSettings projectile = host?.ResolveMinionProjectileSettings(
                            GetCenterProjectileName(projectileSettings, minionNumber));
                        if (projectile == null)
                        {
                            continue;
                        }

                        MinionGraphProjectileFireSpec fireSpec = new MinionGraphProjectileFireSpec(minionOrigin, null, effects, context)
                            .WithFixedDirection(Vector2.left);
                        minion.FireOnce(projectile, fireSpec, i);
                    }

                    nextFireSeconds += Mathf.Max(0.01f, fireInterval);
                }

                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }
        }

        private string GetCenterProjectileName(
            IReadOnlyList<CenterFireProjectile> projectileSettings,
            int minionNumber)
        {
            if (projectileSettings != null)
            {
                for (int i = 0; i < projectileSettings.Count; i++)
                {
                    CenterFireProjectile setting = projectileSettings[i];
                    if (setting != null && setting.MinionNumber == minionNumber)
                    {
                        return setting.ProjectileName;
                    }
                }
            }

            return string.Empty;
        }

        private IEnumerator CollapseStaff(
            BossActionContext context,
            ConductorScoreLaneRushIndicatorVisual visual,
            IReadOnlyList<LineSlot> lineSlots)
        {
            int[] retainedLines = { 0, 1, 6, 7 };
            for (int i = 0; i < DroneCount * 3; i++)
            {
                if (i != retainedLines[0]
                    && i != retainedLines[1]
                    && i != retainedLines[2]
                    && i != retainedLines[3])
                {
                    visual?.SetProgress(i, 0f);
                }
            }

            float elapsed = 0f;
            float duration = Mathf.Max(0f, staffCollapseSeconds);
            while (elapsed < duration)
            {
                if (context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                float t = Mathf.Clamp01(elapsed / duration);
                for (int i = 0; i < retainedLines.Length; i++)
                {
                    int sourceLine = retainedLines[i];
                    GetNorthSouthStaffLine(sourceLine, out Vector2 sourceStart, out Vector2 sourceEnd);
                    GetFinalStaffLine(i, out Vector2 targetStart, out Vector2 targetEnd);
                    visual?.SetLane(sourceLine, Vector2.Lerp(sourceStart, targetStart, t), Vector2.Lerp(sourceEnd, targetEnd, t));
                    visual?.SetProgress(sourceLine, 1f);
                }

                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            for (int i = 0; i < retainedLines.Length; i++)
            {
                GetFinalStaffLine(i, out Vector2 start, out Vector2 end);
                visual?.SetLane(retainedLines[i], start, end);
                visual?.SetProgress(retainedLines[i], 1f);
            }
        }

        private static IEnumerator WaitSeconds(BossActionContext context, float seconds)
        {
            float elapsed = 0f;
            while (elapsed < Mathf.Max(0f, seconds))
            {
                if (context != null && context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }
        }

        private static void CommandMoveToPosition(Minion minion, Vector2 target, float moveSeconds)
        {
            if (minion == null)
            {
                return;
            }

            minion.CommandScoreLaneRush(target, Vector2.right, Mathf.Max(0f, moveSeconds), 0f, 0f, 1f);
        }

        private void CommandStaffCreationWander(IReadOnlyList<Minion> minions, float duration)
        {
            if (minions == null || duration <= 0f)
            {
                return;
            }

            for (int i = 0; i < minions.Count; i++)
            {
                minions[i]?.CommandWander(
                    duration,
                    staffCreationWanderSpeed,
                    staffCreationWanderRadius,
                    staffCreationWanderRetargetSeconds);
            }
        }

        private IEnumerator RunVolleys(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<LineSlot> lineSlots,
            ConductorScoreLaneRushIndicatorVisual indicator)
        {
            ScoreLaneVolley volley = SelectRandomVolley();
            if (volley == null)
            {
                yield break;
            }

            yield return RunVolley(context, host, lineSlots, indicator, volley);
        }

        private ScoreLaneVolley SelectRandomVolley()
        {
            List<ScoreLaneVolley> candidates = new();
            for (int i = 0; i < volleys.Count; i++)
            {
                ScoreLaneVolley volley = volleys[i];
                if (volley?.FireTimings != null && volley.FireTimings.Count > 0)
                {
                    candidates.Add(volley);
                }
            }

            return candidates.Count > 0 ? candidates[UnityEngine.Random.Range(0, candidates.Count)] : null;
        }

        private IEnumerator RunVolley(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<LineSlot> lineSlots,
            ConductorScoreLaneRushIndicatorVisual indicator,
            ScoreLaneVolley volley)
        {
            IReadOnlyList<ScoreLaneFireTiming> fireTimings = volley.FireTimings;
            if (fireTimings == null || fireTimings.Count == 0)
            {
                yield break;
            }

            bool[] fired = new bool[fireTimings.Count];
            float waitSeconds = GetMaxFireSeconds(fireTimings);
            float elapsed = 0f;

            while (elapsed < waitSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    context.Boss?.Stop();
                    yield return null;
                    continue;
                }

                TickBossFormationAlignment(context, lineSlots);
                UpdateLineIndicators(indicator, lineSlots);
                FireDueProjectiles(context, host, lineSlots, fireTimings, fired, elapsed);
                if (TryCompleteByClearedLineProjectiles(AreAllFireTimingsProcessed(fireTimings, fired)))
                {
                    yield break;
                }

                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            TickBossFormationAlignment(context, lineSlots);
            UpdateLineIndicators(indicator, lineSlots);
            FireDueProjectiles(context, host, lineSlots, fireTimings, fired, float.PositiveInfinity);
            TryCompleteByClearedLineProjectiles(true);
        }

        private bool TryCompleteByClearedLineProjectiles(bool allFireTimingsProcessed)
        {
            if (!allFireTimingsProcessed || HasLiveLineProjectiles())
            {
                return false;
            }

            completedByAllProjectilesCleared = true;
            return true;
        }

        private static bool AreAllFireTimingsProcessed(IReadOnlyList<ScoreLaneFireTiming> fireTimings, bool[] fired)
        {
            if (fireTimings == null)
            {
                return true;
            }

            for (int i = 0; i < fireTimings.Count; i++)
            {
                if (fireTimings[i] != null && (i >= fired.Length || !fired[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private void FireDueProjectiles(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<LineSlot> lineSlots,
            IReadOnlyList<ScoreLaneFireTiming> fireTimings,
            bool[] fired,
            float elapsed)
        {
            for (int i = 0; i < fireTimings.Count; i++)
            {
                ScoreLaneFireTiming timing = fireTimings[i];
                if (timing == null || i >= fired.Length || fired[i] || elapsed < timing.FireSeconds)
                {
                    continue;
                }

                fired[i] = true;
                LineSlot slot = FindSlot(lineSlots, timing.MinionNumber);
                BossProjectileSettings projectile = host.ResolveMinionProjectileSettings(timing.ProjectileName);
                if (slot.Minion == null || projectile == null)
                {
                    continue;
                }

                MinionGraphProjectileFireSpec fireSpec = new MinionGraphProjectileFireSpec(minionOrigin, null, effects, context)
                    .WithFixedDirection(slot.LineDirection);
                EnemyProjectile spawned = slot.Minion.FireOnce(projectile, fireSpec, i);
                if (spawned == null)
                {
                    continue;
                }

                float projectileSpeed = projectile.Speed * Mathf.Max(0.01f, projectileSpeedMultiplier);
                spawned.ConfigureSpeedMultiplier(projectileSpeedMultiplier);
                spawned.ConfigureChargeMotion(0f, false, false);
                spawned.ConfigurePathIndicatorDelayedUntilLaunch(true);
                spawned.ConfigureExternalMotionDriven(true);
                spawned.ConfigurePlayerCollisionIgnored(true);
                SetSequenceActive(spawned, true);
                spawned.HoldChargeUntilForcedLaunch();

                ConductorFormationLineProjectileMotion motion = spawned.gameObject.AddComponent<ConductorFormationLineProjectileMotion>();
                motion.Initialize(
                    () => GetLaneOrigin(slot, lineSlots),
                    slot.LineDirection,
                    projectileSpeed,
                    GetMissLaunchLineTravelDistance());

                trackedMotions.Add(motion);
            }
        }

        private void FireFinalProjectile(BossActionContext context, IMinionPatternHost host)
        {
            if (!fireFinalProjectile
                || context == null
                || !MinionGraphActionHost.TryResolveProjectile(host, finalProjectileName, out BossProjectileSettings projectile))
            {
                return;
            }

            MinionGraphProjectileFireSpec fireSpec = new(
                finalProjectileOrigin,
                finalProjectileAim,
                finalProjectileEffects,
                context);
            MinionGraphCommandRequest request = MinionGraphCommandRequest.RepeatFire(projectile, 1, 0f, fireSpec);
            host.CommandMinions(request);
        }

        private IEnumerator RunMissLaunchPhase(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<LineSlot> lineSlots,
            ConductorScoreLaneRushIndicatorVisual indicator)
        {
            if (!HasReadyMissMotion())
            {
                yield break;
            }

            yield return RunFinalReleaseFormation(context, lineSlots, indicator);
            if (indicator != null)
            {
                indicator.ClearAndDestroy();
            }

            List<ConductorFormationLineProjectileMotion> missedMotions = GetReadyMissMotions();
            if (missedMotions.Count == 0)
            {
                yield break;
            }

            for (int i = 0; i < missedMotions.Count; i++)
            {
                while (context.IsExecutionPaused)
                {
                    yield return null;
                }

                ConductorFormationLineProjectileMotion motion = missedMotions[i];
                if (motion != null && motion.IsReadyForMissLaunch)
                {
                    SpawnMissLaunchProjectile(context, host, motion);
                    motion.DestroySourceProjectile();
                }

                if (i == missedMotions.Count - 1)
                {
                    FireFinalProjectile(context, host);
                    yield break;
                }

                yield return WaitMissLaunchInterval(context, lineSlots, indicator, missedLaunchIntervalSeconds);
            }
        }

        private IEnumerator RunFinalReleaseFormation(
            BossActionContext context,
            IReadOnlyList<LineSlot> lineSlots,
            ConductorScoreLaneRushIndicatorVisual indicator)
        {
            if (!commandFinalReleaseFormation || context?.Boss == null || context.Boss.Player == null)
            {
                yield break;
            }

            List<FinalReleaseSlot> finalSlots = BuildFinalReleaseSlots(context, lineSlots);
            if (finalSlots.Count == 0)
            {
                yield break;
            }

            float moveSpeed = DefaultFormationMoveSpeed * Mathf.Max(0f, launchFormationSpeedMultiplier);
            for (int i = 0; i < finalSlots.Count; i++)
            {
                FinalReleaseSlot slot = finalSlots[i];
                slot.Minion.CommandAngleDistance(slot.AngleDegrees, slot.Radius, moveSpeed);
            }

            context.Boss.Stop();
            float elapsed = 0f;
            float settleSeconds = Mathf.Max(0f, launchFormationSettleSeconds);
            float fadeSeconds = Mathf.Max(0f, lineIndicatorFadeSeconds);
            float maxWaitSeconds = settleSeconds
                + GetFinalReleaseMoveSeconds(finalSlots, context.Boss.Player, moveSpeed)
                + 0.25f;
            while ((elapsed < settleSeconds || !AreFinalReleaseMinionsAligned(finalSlots, context.Boss.Player))
                && elapsed < maxWaitSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    context.Boss.Stop();
                    yield return null;
                    continue;
                }

                UpdateLineIndicators(indicator, lineSlots);
                ApplyReleaseFormationIndicatorFade(indicator, elapsed, fadeSeconds);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            if (indicator != null)
            {
                indicator.ClearAndDestroy();
            }
        }

        private static void ApplyReleaseFormationIndicatorFade(
            ConductorScoreLaneRushIndicatorVisual indicator,
            float elapsed,
            float fadeSeconds)
        {
            if (indicator == null)
            {
                return;
            }

            indicator.SetAlpha(fadeSeconds > 0f ? 1f - Mathf.Clamp01(elapsed / fadeSeconds) : 0f);
        }

        private List<FinalReleaseSlot> BuildFinalReleaseSlots(
            BossActionContext context,
            IReadOnlyList<LineSlot> lineSlots)
        {
            List<FinalReleaseSlot> finalSlots = new();
            if (context?.Boss == null || context.Boss.Player == null || lineSlots == null || lineSlots.Count == 0)
            {
                return finalSlots;
            }

            Vector2 playerPosition = context.Boss.Player.position;
            Vector2 laneCenter = GetLaneCenter(lineSlots);
            Vector2 baseDirection = laneCenter - playerPosition;
            if (baseDirection.sqrMagnitude <= 0.0001f)
            {
                baseDirection = -lineSlots[0].LineDirection;
            }

            baseDirection = baseDirection.sqrMagnitude > 0.0001f ? baseDirection.normalized : Vector2.right;
            float currentRadius = Vector2.Distance(playerPosition, laneCenter);
            float radius = Mathf.Max(Mathf.Max(0.1f, currentRadius), launchFormationCircleRadius);
            float spacingDegrees = Mathf.Max(1f, launchFormationAngleSpacingDegrees);

            int liveIndex = 0;
            for (int i = 0; i < lineSlots.Count; i++)
            {
                Minion minion = lineSlots[i].Minion;
                if (minion == null || minion.Health == null || minion.Health.IsDead)
                {
                    continue;
                }

                float offsetDegrees = launchFormationSideBySide
                    ? GetSideBySideFormationAngle(liveIndex, spacingDegrees)
                    : GetFormationAngle(liveIndex, spacingDegrees);
                Vector2 direction = RotateDirection(baseDirection, offsetDegrees);
                finalSlots.Add(new FinalReleaseSlot(
                    minion,
                    DirectionToAngleDegrees(direction),
                    radius));
                liveIndex++;
            }

            return finalSlots;
        }

        private bool AreFinalReleaseMinionsAligned(
            IReadOnlyList<FinalReleaseSlot> finalSlots,
            Transform player)
        {
            if (finalSlots == null || finalSlots.Count == 0 || player == null)
            {
                return true;
            }

            float toleranceSqr = FormationAlignmentTolerance * FormationAlignmentTolerance;
            for (int i = 0; i < finalSlots.Count; i++)
            {
                FinalReleaseSlot slot = finalSlots[i];
                if (slot.Minion == null || slot.Minion.Health == null || slot.Minion.Health.IsDead)
                {
                    continue;
                }

                Vector2 target = (Vector2)player.position + AngleToDirection(slot.AngleDegrees) * slot.Radius;
                if (((Vector2)slot.Minion.transform.position - target).sqrMagnitude > toleranceSqr)
                {
                    return false;
                }
            }

            return true;
        }

        private float GetFinalReleaseMoveSeconds(
            IReadOnlyList<FinalReleaseSlot> finalSlots,
            Transform player,
            float moveSpeed)
        {
            if (finalSlots == null || player == null || moveSpeed <= 0f)
            {
                return 0f;
            }

            float maxDistance = 0f;
            for (int i = 0; i < finalSlots.Count; i++)
            {
                FinalReleaseSlot slot = finalSlots[i];
                if (slot.Minion == null)
                {
                    continue;
                }

                Vector2 target = (Vector2)player.position + AngleToDirection(slot.AngleDegrees) * slot.Radius;
                maxDistance = Mathf.Max(maxDistance, Vector2.Distance(slot.Minion.transform.position, target));
            }

            return maxDistance / moveSpeed;
        }

        private EnemyProjectile SpawnMissLaunchProjectile(
            BossActionContext context,
            IMinionPatternHost host,
            ConductorFormationLineProjectileMotion sourceMotion)
        {
            if (context?.Boss == null
                || context.Boss.Player == null
                || sourceMotion == null
                || sourceMotion.Projectile == null
                || !MinionGraphActionHost.TryResolveProjectile(host, missedLaunchProjectileName, out BossProjectileSettings projectile))
            {
                return null;
            }

            Vector2 origin = sourceMotion.transform.position;
            Vector2 direction = (Vector2)context.Boss.Player.position - origin;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = sourceMotion.LineDirection;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.left;
            }

            EnemyProjectile replacement = context.FireProjectile(
                projectile,
                origin,
                direction.normalized,
                0f,
                aimAtPlayerWhileChargingOverride: false,
                aimAtPlayerOnLaunchOverride: false,
                chargeSecondsOverride: 0f,
                suppressHoming: false,
                projectileName: missedLaunchProjectileName);
            if (replacement == null)
            {
                return null;
            }

            SetSequenceActive(replacement, true);
            replacement.ConfigurePlayerCollisionIgnored(false);
            replacement.ConfigurePathIndicatorSuppressed(false);
            replacement.ConfigureSpeedMultiplier(0.5f);
            replacement.RestorePrefabTrailColor();
            return replacement;
        }

        private IEnumerator WaitMissLaunchInterval(
            BossActionContext context,
            IReadOnlyList<LineSlot> lineSlots,
            ConductorScoreLaneRushIndicatorVisual indicator,
            float seconds)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0f, seconds);
            while (elapsed < duration)
            {
                if (context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                UpdateLineIndicators(indicator, lineSlots);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }
        }

        private List<LineSlot> BuildStaffLineSlots(IReadOnlyList<Minion> minions)
        {
            List<LineSlot> slots = new();
            if (minions == null)
            {
                return slots;
            }

            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion == null)
                {
                    continue;
                }

                slots.Add(new LineSlot(
                    minion,
                    GetMinionNumber(minion, i),
                    Vector2.left,
                    GetCenteredOffset(i, minions.Count, staffLineSpacing),
                    staffCenter));
            }

            return slots;
        }

        private void TickBossFormationAlignment(BossActionContext context, IReadOnlyList<LineSlot> lineSlots)
        {
            if (!isBossRepositioning)
            {
                context?.Boss?.Stop();
            }
        }

        private ConductorScoreLaneRushIndicatorVisual CreateLineIndicators(IReadOnlyList<LineSlot> lineSlots)
        {
            if (lineSlots == null || lineSlots.Count == 0)
            {
                return null;
            }

            GameObject indicatorObject = new("ConductorFormationLineVolleyIndicators");
            ConductorScoreLaneRushIndicatorVisual visual = indicatorObject.AddComponent<ConductorScoreLaneRushIndicatorVisual>();
            Color color = drawLineIndicators
                ? lineIndicatorColor
                : new Color(lineIndicatorColor.r, lineIndicatorColor.g, lineIndicatorColor.b, 0f);
            visual.Configure(color, lineIndicatorWidth, lineIndicatorSortingOrder);
            visual.ConfigurePlayerBlocking(true);
            UpdateLineIndicators(visual, lineSlots);
            return visual;
        }

        private void UpdateLineIndicators(
            ConductorScoreLaneRushIndicatorVisual visual,
            IReadOnlyList<LineSlot> lineSlots)
        {
            if (visual == null || lineSlots == null)
            {
                return;
            }

            for (int i = 0; i < lineSlots.Count; i++)
            {
                LineSlot slot = lineSlots[i];
                if (slot.Minion == null)
                {
                    visual.SetProgress(i, 0f);
                    continue;
                }

                Vector2 origin = GetLaneOrigin(slot, lineSlots);
                visual.SetLane(i, origin, origin + slot.LineDirection * GetMissLaunchLineTravelDistance());
                visual.SetProgress(i, 1f);
            }
        }

        private IEnumerator WaitForLineProjectilesToRelease(
            BossActionContext context,
            IReadOnlyList<LineSlot> lineSlots,
            ConductorScoreLaneRushIndicatorVisual indicator)
        {
            float timeoutSeconds = GetBoundLineProjectileTimeoutSeconds();
            float elapsed = 0f;
            while (HasBoundLineProjectiles())
            {
                if (context.IsExecutionPaused)
                {
                    context.Boss?.Stop();
                    yield return null;
                    continue;
                }

                TickBossFormationAlignment(context, lineSlots);
                UpdateLineIndicators(indicator, lineSlots);
                if (TryCompleteByClearedLineProjectiles(true))
                {
                    yield break;
                }

                elapsed += EnemyTimeScale.DeltaTime;
                if (timeoutSeconds <= 0f || elapsed >= timeoutSeconds)
                {
                    ForceReadyBoundLineProjectiles();
                    yield break;
                }

                yield return null;
            }

            TryCompleteByClearedLineProjectiles(true);
        }

        private float GetBoundLineProjectileTimeoutSeconds()
        {
            float timeoutSeconds = 0f;
            for (int i = trackedMotions.Count - 1; i >= 0; i--)
            {
                ConductorFormationLineProjectileMotion motion = trackedMotions[i];
                if (motion == null || !motion.IsLive)
                {
                    trackedMotions.RemoveAt(i);
                    continue;
                }

                if (motion.IsBoundToLine)
                {
                    timeoutSeconds = Mathf.Max(timeoutSeconds, motion.EstimatedRemainingSeconds);
                }
            }

            return timeoutSeconds + 0.5f;
        }

        private void ForceReadyBoundLineProjectiles()
        {
            for (int i = 0; i < trackedMotions.Count; i++)
            {
                ConductorFormationLineProjectileMotion motion = trackedMotions[i];
                if (motion != null && motion.IsBoundToLine)
                {
                    motion.ForceReadyForMissLaunch();
                }
            }
        }

        private bool HasBoundLineProjectiles()
        {
            for (int i = trackedMotions.Count - 1; i >= 0; i--)
            {
                ConductorFormationLineProjectileMotion motion = trackedMotions[i];
                if (motion == null || !motion.IsLive)
                {
                    trackedMotions.RemoveAt(i);
                    continue;
                }

                if (motion.IsBoundToLine)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasLiveLineProjectiles()
        {
            for (int i = trackedMotions.Count - 1; i >= 0; i--)
            {
                ConductorFormationLineProjectileMotion motion = trackedMotions[i];
                if (motion == null || !motion.IsLive)
                {
                    trackedMotions.RemoveAt(i);
                    continue;
                }

                return true;
            }

            return false;
        }

        private List<ConductorFormationLineProjectileMotion> GetReadyMissMotions()
        {
            List<ConductorFormationLineProjectileMotion> readyMotions = new();
            for (int i = trackedMotions.Count - 1; i >= 0; i--)
            {
                ConductorFormationLineProjectileMotion motion = trackedMotions[i];
                if (motion == null || !motion.IsLive)
                {
                    trackedMotions.RemoveAt(i);
                    continue;
                }

                if (motion.IsReadyForMissLaunch)
                {
                    readyMotions.Add(motion);
                }
            }

            readyMotions.Reverse();
            return readyMotions;
        }

        private bool HasReadyMissMotion()
        {
            for (int i = trackedMotions.Count - 1; i >= 0; i--)
            {
                ConductorFormationLineProjectileMotion motion = trackedMotions[i];
                if (motion == null || !motion.IsLive)
                {
                    trackedMotions.RemoveAt(i);
                    continue;
                }

                if (motion.IsReadyForMissLaunch)
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerator FadeAndClearIndicator(
            BossActionContext context,
            IReadOnlyList<LineSlot> lineSlots,
            ConductorScoreLaneRushIndicatorVisual indicator)
        {
            if (indicator == null)
            {
                yield break;
            }

            float duration = Mathf.Max(0f, lineIndicatorFadeSeconds);
            if (duration <= 0f)
            {
                indicator.ClearAndDestroy();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (context.IsExecutionPaused)
                {
                    context.Boss?.Stop();
                    yield return null;
                    continue;
                }

                TickBossFormationAlignment(context, lineSlots);
                UpdateLineIndicators(indicator, lineSlots);
                indicator.SetAlpha(1f - Mathf.Clamp01(elapsed / duration));
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            indicator.ClearAndDestroy();
        }

        private static float GetMaxFireSeconds(IReadOnlyList<ScoreLaneFireTiming> fireTimings)
        {
            float maxSeconds = 0f;
            if (fireTimings == null)
            {
                return maxSeconds;
            }

            for (int i = 0; i < fireTimings.Count; i++)
            {
                if (fireTimings[i] != null)
                {
                    maxSeconds = Mathf.Max(maxSeconds, fireTimings[i].FireSeconds);
                }
            }

            return maxSeconds;
        }

        private float EstimateLongestLineVolleySeconds(IMinionPatternHost host)
        {
            float maxSeconds = 0f;
            if (host == null || volleys == null)
            {
                return maxSeconds;
            }

            for (int volleyIndex = 0; volleyIndex < volleys.Count; volleyIndex++)
            {
                IReadOnlyList<ScoreLaneFireTiming> timings = volleys[volleyIndex]?.FireTimings;
                if (timings == null || timings.Count == 0)
                {
                    continue;
                }

                maxSeconds = Mathf.Max(maxSeconds, EstimateLineVolleySeconds(host, timings));
            }

            return maxSeconds;
        }

        private float EstimateLineVolleySeconds(
            IMinionPatternHost host,
            IReadOnlyList<ScoreLaneFireTiming> timings)
        {
            float maxSeconds = GetMaxFireSeconds(timings);
            for (int i = 0; i < timings.Count; i++)
            {
                ScoreLaneFireTiming timing = timings[i];
                if (timing == null)
                {
                    continue;
                }

                BossProjectileSettings projectile = host.ResolveMinionProjectileSettings(timing.ProjectileName);
                if (projectile == null)
                {
                    continue;
                }

                maxSeconds = Mathf.Max(
                    maxSeconds,
                    timing.FireSeconds + EstimateLineProjectileTravelSeconds(projectile));
            }

            return maxSeconds;
        }

        private float EstimateLineProjectileTravelSeconds(BossProjectileSettings projectile)
        {
            if (projectile == null)
            {
                return 0f;
            }

            float speed = projectile.Speed * Mathf.Max(0.01f, projectileSpeedMultiplier);
            return speed > 0f ? GetMissLaunchLineTravelDistance() / speed : 0f;
        }

        private int EstimateMaxMissLaunchCount(IMinionPatternHost host)
        {
            int maxCount = 0;
            if (host == null || volleys == null)
            {
                return maxCount;
            }

            for (int volleyIndex = 0; volleyIndex < volleys.Count; volleyIndex++)
            {
                IReadOnlyList<ScoreLaneFireTiming> timings = volleys[volleyIndex]?.FireTimings;
                if (timings == null)
                {
                    continue;
                }

                int count = 0;
                for (int timingIndex = 0; timingIndex < timings.Count; timingIndex++)
                {
                    ScoreLaneFireTiming timing = timings[timingIndex];
                    if (timing != null && host.ResolveMinionProjectileSettings(timing.ProjectileName) != null)
                    {
                        count++;
                    }
                }

                maxCount = Mathf.Max(maxCount, count);
            }

            return maxCount;
        }

        private float EstimateMissLaunchPhaseSeconds(
            BossActionContext context,
            IReadOnlyList<LineSlot> lineSlots,
            int missCount)
        {
            if (missCount <= 0)
            {
                return 0f;
            }

            float seconds = EstimateFinalReleaseFormationSeconds(context, lineSlots);
            seconds += Mathf.Max(0f, missedLaunchIntervalSeconds) * Mathf.Max(0, missCount - 1);
            return seconds;
        }

        private float EstimateFinalReleaseFormationSeconds(
            BossActionContext context,
            IReadOnlyList<LineSlot> lineSlots)
        {
            if (!commandFinalReleaseFormation || context?.Boss == null || context.Boss.Player == null)
            {
                return 0f;
            }

            List<FinalReleaseSlot> finalSlots = BuildFinalReleaseSlots(context, lineSlots);
            if (finalSlots.Count == 0)
            {
                return 0f;
            }

            float moveSpeed = DefaultFormationMoveSpeed * Mathf.Max(0f, launchFormationSpeedMultiplier);
            return Mathf.Max(0f, launchFormationSettleSeconds)
                + GetFinalReleaseMoveSeconds(finalSlots, context.Boss.Player, moveSpeed)
                + 0.25f;
        }

        private float GetLineIndicatorFadeSeconds()
        {
            return drawLineIndicators ? Mathf.Max(0f, lineIndicatorFadeSeconds) : 0f;
        }

        private static List<Minion> GetLineMinions(IReadOnlyList<Minion> source)
        {
            List<Minion> results = new();
            if (source == null)
            {
                return results;
            }

            for (int i = 0; i < source.Count; i++)
            {
                Minion minion = source[i];
                if (minion != null && minion.Health != null && !minion.Health.IsDead)
                {
                    results.Add(minion);
                }
            }

            results.Sort((a, b) => GetSortNumber(a).CompareTo(GetSortNumber(b)));
            if (results.Count > 4)
            {
                results.RemoveRange(4, results.Count - 4);
            }

            return results;
        }

        private static LineSlot FindSlot(IReadOnlyList<LineSlot> lineSlots, int minionNumber)
        {
            if (lineSlots == null)
            {
                return default;
            }

            for (int i = 0; i < lineSlots.Count; i++)
            {
                if (lineSlots[i].MinionNumber == minionNumber)
                {
                    return lineSlots[i];
                }
            }

            return default;
        }

        private static void SetFormationFacingOverride(IReadOnlyList<Minion> minions, Vector2 direction)
        {
            if (minions == null)
            {
                return;
            }

            for (int i = 0; i < minions.Count; i++)
            {
                minions[i]?.SetGraphFacingDirectionOverride(direction);
            }
        }

        private static void ClearFormationFacingOverride(IReadOnlyList<Minion> minions)
        {
            if (minions == null)
            {
                return;
            }

            for (int i = 0; i < minions.Count; i++)
            {
                minions[i]?.ClearGraphFacingDirectionOverride();
            }
        }

        private void CommandLineMinionsEarlyClearWander(IReadOnlyList<Minion> minions)
        {
            if (minions == null)
            {
                return;
            }

            for (int i = 0; i < minions.Count; i++)
            {
                minions[i]?.CommandWander(
                    earlyClearWanderSeconds,
                    earlyClearWanderSpeed,
                    earlyClearWanderRadius,
                    earlyClearWanderRetargetSeconds);
            }
        }

        private static int GetSortNumber(Minion minion)
        {
            return minion != null && minion.HasOwnerSlotNumber ? minion.OwnerSlotNumber : int.MaxValue;
        }

        private static int GetMinionNumber(Minion minion, int fallbackIndex)
        {
            return minion != null && minion.HasOwnerSlotNumber ? minion.OwnerSlotNumber : fallbackIndex + 1;
        }

        private static float GetCenteredOffset(int index, int count, float offsetSpacing)
        {
            if (count <= 1 || offsetSpacing <= 0f)
            {
                return 0f;
            }

            float centerIndex = (count - 1) * 0.5f;
            return (Mathf.Clamp(index, 0, count - 1) - centerIndex) * offsetSpacing;
        }

        private static float GetFormationAngle(int index, float spacingDegrees)
        {
            if (index <= 0)
            {
                return 0f;
            }

            int ring = (index + 1) / 2;
            float sign = index % 2 == 1 ? 1f : -1f;
            return sign * ring * Mathf.Max(1f, spacingDegrees);
        }

        private static float GetSideBySideFormationAngle(int index, float spacingDegrees)
        {
            int ring = Mathf.Max(0, index) / 2 + 1;
            float sign = index % 2 == 0 ? 1f : -1f;
            return sign * ring * Mathf.Max(1f, spacingDegrees);
        }

        private static Vector2 AngleToDirection(float degrees)
        {
            return BossActionContext.AngleToDirection(degrees);
        }

        private static float DirectionToAngleDegrees(Vector2 direction)
        {
            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        private static Vector2 RotateDirection(Vector2 direction, float degrees)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Vector2.right;
            }

            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            Vector2 normalized = direction.normalized;
            return new Vector2(
                normalized.x * cos - normalized.y * sin,
                normalized.x * sin + normalized.y * cos);
        }

        private Vector2 GetLaneOrigin(LineSlot slot, IReadOnlyList<LineSlot> lineSlots)
        {
            if (slot.Minion == null)
            {
                return GetLaneMinionTarget(slot, lineSlots);
            }

            return minionOrigin.GetSpawnOrigin(slot.Minion, 0, slot.LineDirection);
        }

        private Vector2 GetLaneMinionTarget(LineSlot slot, IReadOnlyList<LineSlot> lineSlots)
        {
            return GetLaneCenter(lineSlots) + GetLineAxis(slot.LineDirection) * slot.LateralOffset;
        }

        private static Vector2 GetLaneCenter(IReadOnlyList<LineSlot> lineSlots)
        {
            if (lineSlots == null || lineSlots.Count == 0)
            {
                return Vector2.zero;
            }

            return lineSlots[0].LaneCenter;
        }

        private static Vector2 GetLineAxis(Vector2 lineDirection)
        {
            Vector2 safeDirection = lineDirection.sqrMagnitude > 0.0001f ? lineDirection.normalized : Vector2.left;
            return new Vector2(-safeDirection.y, safeDirection.x);
        }

        private static void SetSequenceActive(EnemyProjectile projectile, bool active)
        {
            if (projectile == null)
            {
                return;
            }

            if (projectile is IConductorMultiStepOrderedProjectile multiStepProjectile)
            {
                multiStepProjectile.SetSequenceActive(active);
            }
            else if (projectile is ConductorOrderedParryProjectile orderedProjectile)
            {
                orderedProjectile.SetSequenceActive(active);
            }
            else
            {
                projectile.ConfigureInterceptable(active);
            }
        }

        private readonly struct LineSlot
        {
            public LineSlot(
                Minion minion,
                int minionNumber,
                Vector2 lineDirection,
                float lateralOffset,
                Vector2 laneCenter)
            {
                Minion = minion;
                MinionNumber = minionNumber;
                LineDirection = lineDirection;
                LateralOffset = lateralOffset;
                LaneCenter = laneCenter;
            }

            public Minion Minion { get; }
            public int MinionNumber { get; }
            public Vector2 LineDirection { get; }
            public float LateralOffset { get; }
            public Vector2 LaneCenter { get; }
        }

        [Serializable]
        private sealed class CenterFireProjectile
        {
            [SerializeField, Min(1)] private int minionNumber = 1;
            [SerializeField, BossGraphProjectileName] private string projectileName = "Default";

            public CenterFireProjectile()
            {
            }

            public int MinionNumber => minionNumber;
            public string ProjectileName => projectileName;
        }

        private readonly struct FinalReleaseSlot
        {
            public FinalReleaseSlot(Minion minion, float angleDegrees, float radius)
            {
                Minion = minion;
                AngleDegrees = angleDegrees;
                Radius = radius;
            }

            public Minion Minion { get; }
            public float AngleDegrees { get; }
            public float Radius { get; }
        }
    }

    [AddComponentMenu("")]
    internal sealed class ConductorFormationLineProjectileMotion : MonoBehaviour
    {
        private EnemyProjectile projectile;
        private Rigidbody2D body;
        private Func<Vector2> originProvider;
        private Vector2 lastOrigin;
        private Vector2 lineDirection = Vector2.left;
        private float speed;
        private float distance;
        private float lineLength;
        private Quaternion lockedRotation = Quaternion.identity;
        private bool hasOrigin;
        private bool readyForMissLaunch;

        public EnemyProjectile Projectile => projectile;
        public Vector2 LineDirection => lineDirection;
        public bool IsLive => projectile != null && projectile.gameObject.activeInHierarchy;
        public bool IsBoundToLine => IsLive && !readyForMissLaunch;
        public bool IsReadyForMissLaunch => IsLive && readyForMissLaunch && !HasParryProgressed;
        public float EstimatedRemainingSeconds
        {
            get
            {
                float remainingDistance = Mathf.Max(0f, lineLength - distance);
                return speed > 0f ? remainingDistance / speed : 0f;
            }
        }

        public void Initialize(
            Func<Vector2> nextOriginProvider,
            Vector2 nextLineDirection,
            float nextSpeed,
            float nextLineLength)
        {
            projectile = GetComponent<EnemyProjectile>();
            body = GetComponent<Rigidbody2D>();
            originProvider = nextOriginProvider;
            lineDirection = nextLineDirection.sqrMagnitude > 0.0001f
                ? nextLineDirection.normalized
                : Vector2.left;
            speed = Mathf.Max(0f, nextSpeed);
            lineLength = Mathf.Max(0f, nextLineLength);
            lockedRotation = transform.rotation;

            Vector2 currentOrigin = SampleOrigin();
            lastOrigin = currentOrigin;
            hasOrigin = true;
            distance = Mathf.Clamp(
                Vector2.Dot((Vector2)transform.position - currentOrigin, lineDirection),
                0f,
                lineLength);
            MoveTo(currentOrigin + lineDirection * distance);
            if (lineLength <= 0f || speed <= 0f)
            {
                ForceReadyForMissLaunch();
            }
        }

        public void ForceReadyForMissLaunch()
        {
            if (readyForMissLaunch)
            {
                return;
            }

            distance = Mathf.Max(0f, lineLength);
            Vector2 currentOrigin = SampleOrigin();
            lastOrigin = currentOrigin;
            hasOrigin = true;
            MoveTo(currentOrigin + lineDirection * distance);
            MarkReadyForMissLaunch();
        }

        private void LateUpdate()
        {
            if (projectile == null)
            {
                StopBody();
                Destroy(this);
                return;
            }

            if (readyForMissLaunch || PlayerCombatController.IsExecutionCinematicActive)
            {
                StopBody();
                return;
            }

            TickLineMotion(EnemyTimeScale.DeltaTime);
        }

        private void TickLineMotion(float deltaTime)
        {
            Vector2 currentOrigin = SampleOrigin();
            Vector2 originDelta = hasOrigin ? currentOrigin - lastOrigin : Vector2.zero;
            Vector2 lateralDelta = originDelta - lineDirection * Vector2.Dot(originDelta, lineDirection);
            Vector2 currentPosition = body != null ? body.position : (Vector2)transform.position;

            distance = Mathf.Min(Mathf.Max(0f, lineLength), distance + speed * deltaTime);
            lastOrigin = currentOrigin;
            hasOrigin = true;
            MoveTo(currentPosition + lateralDelta + lineDirection * (speed * deltaTime));

            if (distance >= Mathf.Max(0f, lineLength) - 0.001f)
            {
                MarkReadyForMissLaunch();
            }
        }

        private void MarkReadyForMissLaunch()
        {
            if (readyForMissLaunch)
            {
                return;
            }

            readyForMissLaunch = true;
            if (projectile != null)
            {
                SetSequenceActiveOnRelease(projectile);
                projectile.ConfigurePlayerCollisionIgnored(false);
                projectile.ForceLaunchStateForExternalMotion();
                projectile.ConfigureExternalMotionDriven(true);
                StopBody();
            }
        }

        private bool HasParryProgressed
        {
            get
            {
                return projectile is IConductorMultiStepOrderedProjectile multiStepProjectile
                    && multiStepProjectile.CompletedSequenceSteps > 0;
            }
        }

        private static void SetSequenceActiveOnRelease(EnemyProjectile target)
        {
            if (target is IConductorMultiStepOrderedProjectile multiStepProjectile)
            {
                multiStepProjectile.SetSequenceActive(true);
                return;
            }

            if (target is ConductorOrderedParryProjectile orderedProjectile)
            {
                orderedProjectile.SetSequenceActive(true);
                return;
            }

            target.ConfigureInterceptable(true);
        }

        public void DestroySourceProjectile()
        {
            if (projectile != null)
            {
                projectile.DestroyFromOwner();
                projectile = null;
            }

            Destroy(this);
        }

        private Vector2 SampleOrigin()
        {
            if (originProvider == null)
            {
                return lastOrigin;
            }

            return originProvider.Invoke();
        }

        private void MoveTo(Vector2 position)
        {
            if (body != null)
            {
                body.position = position;
                body.linearVelocity = Vector2.zero;
            }

            transform.SetPositionAndRotation(
                new Vector3(position.x, position.y, transform.position.z),
                lockedRotation);
        }

        private void StopBody()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }
    }
}
