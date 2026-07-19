using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionConductorFormationLineVolleyAction : BossAction, IBossActionContextDurationProvider, IBossProjectileEmissionAction
    {
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
        [SerializeField] private BossGraphEffectSettings effects = new();

        [Header("Line Indicator")]
        [SerializeField] private bool drawLineIndicators = true;
        [SerializeField] private Color lineIndicatorColor = new(0.62f, 0.92f, 1f, 0.72f);
        [SerializeField, Min(0.001f)] private float lineIndicatorWidth = 0.035f;
        [SerializeField, Min(0.1f)] private float lineIndicatorLength = 12f;
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

        [Header("Boss Reposition")]
        [SerializeField] private bool repositionBossAtPatternStart;
        [SerializeField] private Vector2 bossTargetPosition;
        [SerializeField, Min(0.01f)] private float bossMoveSpeedMultiplier = 1f;
        [SerializeField, Min(0.001f)] private float bossArriveDistance = 0.04f;
        [SerializeField, Min(0.01f)] private float bossMoveTimeoutSeconds = 5f;

        [Header("Pattern Camera")]
        [SerializeField, Min(0f)] private float cameraFocusDelaySeconds;
        [SerializeField] private Vector2 cameraFocusWorldCenter;
        private float activeNorthSouthStaffDistance;
        private float activeWestStaffDistance;

        public bool TryGetDurationSeconds(BossActionContext context, out float seconds)
        {
            seconds = 0f;
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host)
                || context?.Boss == null
                || context.Boss.Player == null)
            {
                return false;
            }

            List<Minion> minions = GetLineMinions(host.GetControlledMinionsForGraph());
            if (minions.Count == 0)
            {
                return false;
            }

            float patternSeconds = GetStaffPreludeSeconds();
            seconds = Mathf.Max(patternSeconds, EstimateBossRepositionSeconds(context));
            return seconds > 0f;
        }

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host)
                || context.Boss == null
                || context.Boss.Player == null)
            {
                yield break;
            }

            yield return host.EnsureMinionCount(DroneCount);
            List<Minion> minions = GetLineMinions(host.GetControlledMinionsForGraph());
            if (minions.Count != DroneCount)
            {
                yield break;
            }

            ConductorScoreLaneRushIndicatorVisual staffIndicator = null;
            Coroutine bossMoveRoutine = repositionBossAtPatternStart
                ? context.Boss.StartCoroutine(MoveBossToTargetPosition(context))
                : null;
            ConductorPatternCameraFocus cameraFocus = ConductorPatternCameraFocus.Start(
                context,
                cameraFocusDelaySeconds,
                cameraFocusWorldCenter);
            try
            {
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
            }
            finally
            {
                cameraFocus?.Dispose();

                if (bossMoveRoutine != null && context?.Boss != null)
                {
                    context.Boss.StopCoroutine(bossMoveRoutine);
                }

                staffIndicator?.ClearAndDestroy();

                context.Boss?.Stop();
                ClearFormationFacingOverride(minions);
                activeNorthSouthStaffDistance = 0f;
                activeWestStaffDistance = 0f;
            }
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
                + GetWestStaffVolleySeconds();
        }

        private IEnumerator MoveBossToTargetPosition(BossActionContext context)
        {
            if (!repositionBossAtPatternStart || context?.Boss == null || context.Boss.Body == null)
            {
                yield break;
            }

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
    }
}
