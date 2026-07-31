using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public enum ConductorScoreLaneSide
    {
        Top,
        Bottom,
        Left,
        Right
    }

    [Serializable]
    public class MinionConductorScoreLaneRushAction : BossAction, IBossActionContextDurationProvider, IBossProjectileEmissionAction
    {
        private const float LaneShrinkPushPadding = 0.02f;
        private const float DefaultInitialLaneDistanceMultiplier = 1.5f;
        private const float DefaultInitialLaneHoldSeconds = 0.2f;
        private const float DefaultLaneShrinkSeconds = 0.6f;

        [Serializable]
        public sealed class StartTiming
        {
            [SerializeField, Min(1)] private int minionNumber = 1;
            [SerializeField, Min(0f)] private float startSeconds;

            public int MinionNumber => Mathf.Max(1, minionNumber);
            public float StartSeconds => Mathf.Max(0f, startSeconds);
        }

        [Serializable]
        public sealed class FireTiming
        {
            [SerializeField, Min(1)] private int minionNumber = 1;
            [SerializeField, Min(0f)] private float fireSeconds;
            [SerializeField, Min(1)] private int parryOrder = 1;
            [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
            [SerializeField, BossGraphSfxId] private string fireSfxId;
            [SerializeField, BossGraphSfxId] private string launchSfxId;

            public FireTiming()
            {
            }

            public FireTiming(FireTiming source)
            {
                if (source == null)
                {
                    return;
                }

                minionNumber = source.MinionNumber;
                fireSeconds = source.FireSeconds;
                parryOrder = source.ParryOrder;
                projectileName = source.ProjectileName;
                fireSfxId = source.FireSfxId;
                launchSfxId = source.LaunchSfxId;
            }

            public int MinionNumber => Mathf.Max(1, minionNumber);
            public float FireSeconds => Mathf.Max(0f, fireSeconds);
            public int ParryOrder => Mathf.Max(1, parryOrder);
            public string ProjectileName => projectileName?.Trim();
            public string FireSfxId => fireSfxId;
            public string LaunchSfxId => launchSfxId;
        }

        [Serializable]
        public sealed class Volley
        {
            [SerializeField] private List<FireTiming> fireTimings = new() { new FireTiming() };

            public Volley()
            {
            }

            public Volley(IReadOnlyList<FireTiming> fireTimings)
            {
                this.fireTimings = new List<FireTiming>();
                if (fireTimings == null)
                {
                    return;
                }

                for (int i = 0; i < fireTimings.Count; i++)
                {
                    this.fireTimings.Add(fireTimings[i] != null
                        ? new FireTiming(fireTimings[i])
                        : new FireTiming());
                }
            }

            public IReadOnlyList<FireTiming> FireTimings => fireTimings;
        }

        protected readonly struct LaneStep
        {
            public LaneStep(ConductorScoreLaneSide side, bool rushPositiveDirection)
            {
                Side = side;
                RushPositiveDirection = rushPositiveDirection;
            }

            public ConductorScoreLaneSide Side { get; }
            public bool RushPositiveDirection { get; }
        }

        protected sealed class ExecutionVolley
        {
            public ExecutionVolley(
                ConductorScoreLaneSide side,
                bool rushPositiveDirection,
                float lineDistanceFromPlayer,
                float lineSpacing,
                float moveToStartSeconds,
                float rushDistance,
                float rushSpeed,
                float restSeconds,
                IReadOnlyList<StartTiming> startTimings,
                IReadOnlyList<FireTiming> fireTimings)
            {
                Side = side;
                RushPositiveDirection = rushPositiveDirection;
                LineDistanceFromPlayer = Mathf.Max(0.1f, lineDistanceFromPlayer);
                LineSpacing = Mathf.Max(0f, lineSpacing);
                MoveToStartSeconds = Mathf.Max(0f, moveToStartSeconds);
                RushDistance = Mathf.Max(0f, rushDistance);
                RushSpeed = Mathf.Max(0.01f, rushSpeed);
                RestSeconds = Mathf.Max(0f, restSeconds);
                StartTimings = startTimings != null ? new List<StartTiming>(startTimings) : new List<StartTiming>();
                FireTimings = fireTimings != null ? new List<FireTiming>(fireTimings) : new List<FireTiming>();
            }

            public ConductorScoreLaneSide Side { get; }
            public bool RushPositiveDirection { get; }
            public float LineDistanceFromPlayer { get; }
            public float LineSpacing { get; }
            public float MoveToStartSeconds { get; }
            public float RushDistance { get; }
            public float RushSpeed { get; }
            public float RestSeconds { get; }
            public IReadOnlyList<StartTiming> StartTimings { get; }
            public IReadOnlyList<FireTiming> FireTimings { get; }
        }

        protected static readonly LaneStep TopStep = new(ConductorScoreLaneSide.Top, true);
        protected static readonly LaneStep RightStep = new(ConductorScoreLaneSide.Right, false);
        protected static readonly LaneStep BottomStep = new(ConductorScoreLaneSide.Bottom, false);
        protected static readonly LaneStep LeftStep = new(ConductorScoreLaneSide.Left, true);

        private static readonly LaneStep[] SingleSideCandidates =
        {
            TopStep,
            RightStep,
            BottomStep,
            LeftStep
        };

        private static readonly LaneStep[] TopBottomSteps =
        {
            TopStep,
            BottomStep
        };

        private static readonly LaneStep[] LeftRightSteps =
        {
            LeftStep,
            RightStep
        };

        [SerializeField] private MinionGraphProjectileOriginSpec minionOrigin = new();
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField, Min(0f)] private float windupSeconds;
        [SerializeField, InspectorName("Pattern Center")] private Vector2 patternCenter;
        [SerializeField, InspectorName("Use Two Sides")] private bool useTwoSides;
        [Header("Common Sound")]
        [Tooltip("Fire Timing에 개별 사운드가 지정되어 있지 않을 때 대신 재생할 공통 사운드입니다.")]
        [SerializeField, BossGraphSfxId] private string defaultFireSfxId;
        [SerializeField, BossGraphSfxId] private string defaultLaunchSfxId;
        [Header("Common Rush Settings")]
        [SerializeField, Min(0.1f)] private float lineDistanceFromPlayer = 3f;
        [SerializeField, Min(0f)] private float lineSpacing = 0.7f;
        [SerializeField, Min(0f)] private float moveToStartSeconds = 0.35f;
        [SerializeField, Min(0f)] private float rushDistance = 8f;
        [SerializeField, Min(0.01f)] private float rushSpeed = 10f;
        [SerializeField, Min(0f)] private float restSeconds = 0.35f;
        [SerializeField] private List<StartTiming> startTimings = new();
        [SerializeField, InspectorName("Volleys")] private List<Volley> volleys = new() { new Volley() };
        [Header("Initial Lane Shrink")]
        [SerializeField, Min(1f)] private float initialLaneDistanceMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float initialLaneHoldSeconds = 0.2f;
        [SerializeField, Min(0f)] private float laneShrinkSeconds = 0.6f;
        [Header("Lane Creation Wander")]
        [SerializeField, Min(0f)] private float laneCreationWanderSpeed = 3.2f;
        [SerializeField, Min(0.1f)] private float laneCreationWanderRadius = 2.8f;
        [SerializeField, Min(0.1f)] private float laneCreationWanderRetargetSeconds = 1.5f;
        [Header("Lane Indicators")]
        [SerializeField, InspectorName("Draw Lane Indicators")] private bool standardDrawLaneIndicators = true;
        [SerializeField] private Color standardLaneIndicatorColor = new(0.62f, 0.92f, 1f, 0.66f);
        [SerializeField, Min(0.001f)] private float standardLaneIndicatorWidth = 0.035f;
        [SerializeField, Min(0f)] private float standardLaneIndicatorRevealSeconds = 0.05f;
        [SerializeField, Min(0f)] private float standardLaneIndicatorRevealInterval = 0.01f;
        [SerializeField, Min(0f)] private float standardLaneIndicatorHideSeconds = 0.14f;
        [SerializeField] private int standardLaneIndicatorSortingOrder = 66;
        [SerializeField, Min(1)] private int standardLaneIndicatorLineCount = 4;
        [SerializeField] private bool drawParryTieLinks = true;
        [SerializeField] private Color parryTieLinkColor = new(0.62f, 0.92f, 1f, 0.78f);
        [SerializeField, Min(0.001f)] private float parryTieLinkWidth = 0.04f;
        [SerializeField, Min(0f)] private float parryTieArcHeight = 0.32f;
        [SerializeField, Range(3, 24)] private int parryTieSegments = 8;
        [SerializeField] private int parryTieSortingOrder = 68;
        [SerializeField] private bool waitForDuration = true;
        [Header("Boss Reposition")]
        [SerializeField] private Vector2 bossTargetPosition;
        [SerializeField, Min(0.01f)] private float bossOriginMoveSpeedMultiplier = 1f;
        [SerializeField, Min(0.001f)] private float bossOriginArriveDistance = 0.04f;
        [SerializeField, Min(0.01f)] private float bossMoveTimeoutSeconds = 5f;

        private ConductorScoreLaneRushIndicatorVisual activeStandardLaneIndicators;

        protected IReadOnlyList<Volley> Volleys => volleys;
        protected float WindupSeconds => Mathf.Max(0f, windupSeconds);
        protected float LineDistanceFromPlayer => Mathf.Max(0.1f, lineDistanceFromPlayer);
        protected float LineSpacing => Mathf.Max(0f, lineSpacing);
        protected float MoveToStartSeconds => Mathf.Max(0f, moveToStartSeconds);
        protected float RushDistance => Mathf.Max(0f, rushDistance);
        protected float RushSpeed => Mathf.Max(0.01f, rushSpeed);
        protected float RestSeconds => Mathf.Max(0f, restSeconds);
        protected float InitialLaneDistanceMultiplier => initialLaneDistanceMultiplier >= 1f
            ? initialLaneDistanceMultiplier
            : DefaultInitialLaneDistanceMultiplier;
        protected float InitialLaneDistance => LineDistanceFromPlayer * InitialLaneDistanceMultiplier;
        protected float InitialLaneHoldSeconds => initialLaneDistanceMultiplier >= 1f
            ? Mathf.Max(0f, initialLaneHoldSeconds)
            : DefaultInitialLaneHoldSeconds;
        protected float LaneShrinkSeconds => initialLaneDistanceMultiplier >= 1f
            ? Mathf.Max(0f, laneShrinkSeconds)
            : DefaultLaneShrinkSeconds;
        protected float MaxStartDelaySeconds => GetMaxStartDelaySeconds(startTimings);
        protected IReadOnlyList<StartTiming> StartTimings => startTimings;
        protected MinionGraphProjectileOriginSpec MinionOrigin => minionOrigin;
        protected BossGraphEffectSettings Effects => effects;
        internal IReadOnlyList<Volley> SerializedVolleysForGraphCopy => volleys;

        public virtual bool TryGetDurationSeconds(BossActionContext context, out float seconds)
        {
            int volleyCount = GetStandardEstimatedVolleyCount();
            if (volleyCount <= 0)
            {
                seconds = 0f;
                return false;
            }

            float patternSeconds = WindupSeconds
                + GetStandardLaneIndicatorRevealDuration(volleyCount)
                + GetInitialLaneShrinkPreludeSeconds()
                + EstimateVolleySequenceDuration(Volleys, volleyCount)
                + GetStandardLaneIndicatorHideDuration();
            seconds = Mathf.Max(EstimateBossTargetMoveSeconds(context), patternSeconds);
            return seconds > 0f;
        }

        public override IEnumerator Execute(BossActionContext context)
        {
            List<ExecutionVolley> executionVolleys = BuildExecutionVolleys();
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host)
                || context.Boss == null
                || executionVolleys.Count == 0)
            {
                yield break;
            }

            Vector2 patternStartPlayerPosition = ResolvePatternCenter(context);
            Conductor.ConductingAnimationLease conductingAnimation =
                (context.Boss as Conductor)?.CreateConductingAnimationLease();
            Action onBossMoveCompleted = conductingAnimation != null ? conductingAnimation.Begin : null;
            Coroutine bossMoveRoutine = context.Boss.StartCoroutine(
                MoveBossToTargetPosition(context, onBossMoveCompleted));
            try
            {
                yield return MinionGraphCommandRunner.WaitWindupIfNeeded(context, WindupSeconds);
                CommandLaneCreationWander(
                    host.GetControlledMinionsForGraph(),
                    GetStandardLaneIndicatorRevealDuration(executionVolleys.Count));
                yield return BeforeExecuteVolleys(context, patternStartPlayerPosition, executionVolleys);
                yield return ExecuteVolleySequence(context, host, patternStartPlayerPosition, executionVolleys);
                yield return AfterExecuteVolleys(context, patternStartPlayerPosition, executionVolleys);
                if (bossMoveRoutine != null)
                {
                    yield return bossMoveRoutine;
                }
            }
            finally
            {
                if (bossMoveRoutine != null && context?.Boss != null)
                {
                    context.Boss.StopCoroutine(bossMoveRoutine);
                }

                context?.Stop();
                ClearActiveStandardLaneIndicators();
                conductingAnimation?.Dispose();
            }
        }

        protected IEnumerator MoveBossToTargetPosition(BossActionContext context, Action onCompleted = null)
        {
            if (context?.Boss == null || context.Boss.Body == null)
            {
                yield break;
            }

            float arriveDistance = Mathf.Max(0.001f, bossOriginArriveDistance);
            float arriveDistanceSqr = arriveDistance * arriveDistance;
            float elapsed = 0f;
            while (((Vector2)context.Boss.Body.position - bossTargetPosition).sqrMagnitude > arriveDistanceSqr
                && elapsed < Mathf.Max(0.01f, bossMoveTimeoutSeconds))
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                Vector2 toTarget = bossTargetPosition - context.Boss.Body.position;
                float speed = context.Boss.MoveSpeed * Mathf.Max(0.01f, bossOriginMoveSpeedMultiplier);
                context.Boss.SetMovementVelocity(toTarget.normalized * speed);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            context.Boss.Stop();
            onCompleted?.Invoke();
        }

        protected float EstimateBossTargetMoveSeconds(BossActionContext context)
        {
            if (context?.Boss == null)
            {
                return 0f;
            }

            Vector2 current = context.Boss.Body != null
                ? context.Boss.Body.position
                : context.Boss.transform.position;
            float distance = Mathf.Max(
                0f,
                Vector2.Distance(current, bossTargetPosition) - Mathf.Max(0.001f, bossOriginArriveDistance));
            float speed = context.Boss.MoveSpeed * Mathf.Max(0.01f, bossOriginMoveSpeedMultiplier);
            float estimate = speed > 0f ? distance / speed : 0f;
            return Mathf.Min(estimate, Mathf.Max(0.01f, bossMoveTimeoutSeconds));
        }

        protected IEnumerator ExecuteVolleySequence(
            BossActionContext context,
            IMinionPatternHost host,
            Vector2 patternStartPlayerPosition,
            IReadOnlyList<ExecutionVolley> executionVolleys)
        {
            MinionGraphProjectileFireSpec fireSpec = new(minionOrigin, null, effects, context);
            for (int volleyIndex = 0; volleyIndex < executionVolleys.Count; volleyIndex++)
            {
                ExecutionVolley volley = executionVolleys[volleyIndex];
                if (volley == null)
                {
                    continue;
                }

                bool hasNextVolley = HasNextVolley(executionVolleys, volleyIndex + 1);
                yield return ExecuteVolley(context, host, fireSpec, volley, hasNextVolley, patternStartPlayerPosition);

                if (hasNextVolley && volley.RestSeconds > 0f)
                {
                    HoldMinionsAtCurrentPositions(host.GetControlledMinionsForGraph(), volley.RestSeconds);
                    yield return context.WaitSeconds(volley.RestSeconds);
                }
            }
        }

        protected virtual IEnumerator BeforeExecuteVolleys(BossActionContext context, Vector2 patternStartPlayerPosition)
        {
            yield break;
        }

        protected virtual IEnumerator BeforeExecuteVolleys(
            BossActionContext context,
            Vector2 patternStartPlayerPosition,
            IReadOnlyList<ExecutionVolley> executionVolleys)
        {
            ClearActiveStandardLaneIndicators();
            activeStandardLaneIndicators = CreateStandardLaneIndicators(
                patternStartPlayerPosition,
                executionVolleys,
                InitialLaneDistanceMultiplier);
            yield return RevealStandardLaneIndicators(context, activeStandardLaneIndicators);
            yield return context.WaitSeconds(InitialLaneHoldSeconds);
            (context.Boss as Conductor)?.PlayPlaceStaffOnLaneConvergence();
            yield return ShrinkStandardLaneIndicators(
                context,
                activeStandardLaneIndicators,
                patternStartPlayerPosition,
                executionVolleys);
            yield return BeforeExecuteVolleys(context, patternStartPlayerPosition);
        }

        protected virtual IEnumerator AfterExecuteVolleys(BossActionContext context, Vector2 patternStartPlayerPosition)
        {
            yield break;
        }

        protected virtual IEnumerator AfterExecuteVolleys(
            BossActionContext context,
            Vector2 patternStartPlayerPosition,
            IReadOnlyList<ExecutionVolley> executionVolleys)
        {
            yield return AfterExecuteVolleys(context, patternStartPlayerPosition);
            yield return HideStandardLaneIndicators(context, activeStandardLaneIndicators);
            activeStandardLaneIndicators = null;
        }

        protected virtual Vector2 ResolvePatternCenter(BossActionContext context)
        {
            return patternCenter;
        }

        protected virtual bool ShouldDrawParryTieLinks()
        {
            return drawParryTieLinks;
        }

        private ConductorScoreLaneRushIndicatorVisual CreateStandardLaneIndicators(
            Vector2 center,
            IReadOnlyList<ExecutionVolley> executionVolleys,
            float distanceMultiplier)
        {
            if (executionVolleys == null)
            {
                return null;
            }

            GameObject indicatorObject = new("ConductorScoreLaneRushIndicators");
            ConductorScoreLaneRushIndicatorVisual visual = indicatorObject.AddComponent<ConductorScoreLaneRushIndicatorVisual>();
            Color color = standardDrawLaneIndicators
                ? standardLaneIndicatorColor
                : new Color(
                    standardLaneIndicatorColor.r,
                    standardLaneIndicatorColor.g,
                    standardLaneIndicatorColor.b,
                    0f);
            visual.Configure(
                color,
                standardLaneIndicatorWidth,
                standardLaneIndicatorSortingOrder);
            visual.ConfigurePlayerBlocking(true);
            visual.ConfigureClearOnExecutionCinematic(true);

            int lineIndex = 0;
            int lineCount = Mathf.Max(1, standardLaneIndicatorLineCount);
            for (int volleyIndex = 0; volleyIndex < executionVolleys.Count; volleyIndex++)
            {
                ExecutionVolley volley = executionVolleys[volleyIndex];
                if (volley == null)
                {
                    continue;
                }

                for (int laneIndex = 0; laneIndex < lineCount; laneIndex++)
                {
                    BuildStandardLaneIndicatorLine(
                        center,
                        volley,
                        laneIndex,
                        lineCount,
                        distanceMultiplier,
                        out Vector2 start,
                        out Vector2 end);
                    visual.SetLane(lineIndex, start, end);
                    lineIndex++;
                }
            }

            if (lineIndex > 0)
            {
                return visual;
            }

            visual.ClearAndDestroy();
            return null;
        }

        private IEnumerator ShrinkStandardLaneIndicators(
            BossActionContext context,
            ConductorScoreLaneRushIndicatorVisual visual,
            Vector2 center,
            IReadOnlyList<ExecutionVolley> executionVolleys)
        {
            if (visual == null || executionVolleys == null)
            {
                yield break;
            }

            float initialMultiplier = InitialLaneDistanceMultiplier;
            float duration = LaneShrinkSeconds;
            if (duration <= 0f)
            {
                UpdateStandardLaneIndicatorPositions(visual, center, executionVolleys, 1f);
                PushPlayerWithShrinkingStandardLanes(
                    context,
                    center,
                    executionVolleys,
                    initialMultiplier,
                    1f);
                yield break;
            }

            float elapsed = 0f;
            float previousMultiplier = initialMultiplier;
            while (elapsed < duration)
            {
                if (context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                float progress = Mathf.Clamp01(elapsed / duration);
                float multiplier = Mathf.Lerp(initialMultiplier, 1f, progress);
                UpdateStandardLaneIndicatorPositions(visual, center, executionVolleys, multiplier);
                PushPlayerWithShrinkingStandardLanes(
                    context,
                    center,
                    executionVolleys,
                    previousMultiplier,
                    multiplier);
                previousMultiplier = multiplier;
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            UpdateStandardLaneIndicatorPositions(visual, center, executionVolleys, 1f);
            PushPlayerWithShrinkingStandardLanes(
                context,
                center,
                executionVolleys,
                previousMultiplier,
                1f);
        }

        private void UpdateStandardLaneIndicatorPositions(
            ConductorScoreLaneRushIndicatorVisual visual,
            Vector2 center,
            IReadOnlyList<ExecutionVolley> executionVolleys,
            float distanceMultiplier)
        {
            if (visual == null || executionVolleys == null)
            {
                return;
            }

            int lineIndex = 0;
            int lineCount = Mathf.Max(1, standardLaneIndicatorLineCount);
            for (int volleyIndex = 0; volleyIndex < executionVolleys.Count; volleyIndex++)
            {
                ExecutionVolley volley = executionVolleys[volleyIndex];
                if (volley == null)
                {
                    continue;
                }

                for (int laneIndex = 0; laneIndex < lineCount; laneIndex++)
                {
                    BuildStandardLaneIndicatorLine(
                        center,
                        volley,
                        laneIndex,
                        lineCount,
                        distanceMultiplier,
                        out Vector2 start,
                        out Vector2 end);
                    visual.SetLane(lineIndex, start, end);
                    visual.SetProgress(lineIndex, 1f);
                    lineIndex++;
                }
            }
        }

        private void PushPlayerWithShrinkingStandardLanes(
            BossActionContext context,
            Vector2 center,
            IReadOnlyList<ExecutionVolley> executionVolleys,
            float previousMultiplier,
            float currentMultiplier)
        {
            if (previousMultiplier <= currentMultiplier
                || context?.Boss?.Player == null
                || executionVolleys == null)
            {
                return;
            }

            int lineCount = Mathf.Max(1, standardLaneIndicatorLineCount);
            for (int volleyIndex = 0; volleyIndex < executionVolleys.Count; volleyIndex++)
            {
                ExecutionVolley volley = executionVolleys[volleyIndex];
                if (volley == null)
                {
                    continue;
                }

                for (int laneIndex = 0; laneIndex < lineCount; laneIndex++)
                {
                    BuildStandardLaneIndicatorLine(
                        center,
                        volley,
                        laneIndex,
                        lineCount,
                        previousMultiplier,
                        out Vector2 previousStart,
                        out Vector2 previousEnd);
                    BuildStandardLaneIndicatorLine(
                        center,
                        volley,
                        laneIndex,
                        lineCount,
                        currentMultiplier,
                        out Vector2 currentStart,
                        out Vector2 currentEnd);
                    GroundMovementConstraint.PushPlayerOutOfMovingLine(
                        context.Boss.Player,
                        previousStart,
                        previousEnd,
                        currentStart,
                        currentEnd,
                        LaneShrinkPushPadding);
                }
            }
        }

        private IEnumerator RevealStandardLaneIndicators(
            BossActionContext context,
            ConductorScoreLaneRushIndicatorVisual visual)
        {
            if (visual == null)
            {
                yield break;
            }

            visual.SetAlpha(1f);
            for (int i = 0; i < visual.LaneCount; i++)
            {
                yield return AnimateStandardLaneIndicatorRange(
                    context,
                    visual,
                    i,
                    1,
                    0f,
                    1f,
                    standardLaneIndicatorRevealSeconds);

                if (standardLaneIndicatorRevealInterval > 0f)
                {
                    yield return context.WaitSeconds(standardLaneIndicatorRevealInterval);
                }
            }
        }

        private IEnumerator HideStandardLaneIndicators(
            BossActionContext context,
            ConductorScoreLaneRushIndicatorVisual visual)
        {
            if (visual == null)
            {
                yield break;
            }

            yield return FadeStandardLaneIndicators(context, visual, 1f, 0f, standardLaneIndicatorHideSeconds);
            visual.ClearAndDestroy();
        }

        private IEnumerator AnimateStandardLaneIndicatorRange(
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
                SetStandardLaneIndicatorRangeProgress(visual, startIndex, count, to);
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
                SetStandardLaneIndicatorRangeProgress(visual, startIndex, count, Mathf.Lerp(from, to, t));
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            SetStandardLaneIndicatorRangeProgress(visual, startIndex, count, to);
        }

        private IEnumerator FadeStandardLaneIndicators(
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

        private static void SetStandardLaneIndicatorRangeProgress(
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

        private void BuildStandardLaneIndicatorLine(
            Vector2 center,
            ExecutionVolley volley,
            int laneIndex,
            int laneCount,
            float distanceMultiplier,
            out Vector2 start,
            out Vector2 end)
        {
            Vector2 rushDirection = GetRushDirection(volley.Side, volley.RushPositiveDirection);
            Vector2 lineAxis = IsHorizontalRush(volley.Side) ? Vector2.up : Vector2.right;
            float lineDistance = volley.LineDistanceFromPlayer * Mathf.Max(1f, distanceMultiplier);
            Vector2 lineCenter = center + GetSideOffset(volley.Side) * lineDistance;
            float centeredOffset = (Mathf.Max(1, laneCount) - 1) * 0.5f;
            Vector2 laneCenter = lineCenter + lineAxis * ((laneIndex - centeredOffset) * volley.LineSpacing);
            start = laneCenter - rushDirection * (volley.RushDistance * 0.5f);
            end = laneCenter + rushDirection * (volley.RushDistance * 0.5f);
        }

        private void ClearActiveStandardLaneIndicators()
        {
            if (activeStandardLaneIndicators == null)
            {
                return;
            }

            activeStandardLaneIndicators.ClearAndDestroy();
            activeStandardLaneIndicators = null;
        }

        protected float EstimateVolleySequenceDuration(IReadOnlyList<Volley> pool, int volleyCount)
        {
            int count = Mathf.Max(0, volleyCount);
            float totalSeconds = 0f;
            for (int i = 0; i < count; i++)
            {
                bool hasNextVolley = i < count - 1;
                totalSeconds += EstimateLongestVolleyWaitSeconds(pool, hasNextVolley);
                if (hasNextVolley)
                {
                    totalSeconds += RestSeconds;
                }
            }

            return totalSeconds;
        }

        protected static int CountAvailableVolleys(IReadOnlyList<Volley> pool)
        {
            int count = 0;
            if (pool == null)
            {
                return count;
            }

            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private int GetStandardEstimatedVolleyCount()
        {
            int laneStepCount = useTwoSides ? 2 : 1;
            return Mathf.Min(laneStepCount, CountAvailableVolleys(Volleys));
        }

        private float GetStandardLaneIndicatorRevealDuration(int volleyCount)
        {
            if (!standardDrawLaneIndicators)
            {
                return 0f;
            }

            int laneCount = Mathf.Max(0, volleyCount) * Mathf.Max(1, standardLaneIndicatorLineCount);
            return laneCount * (Mathf.Max(0f, standardLaneIndicatorRevealSeconds)
                + Mathf.Max(0f, standardLaneIndicatorRevealInterval));
        }

        private float GetStandardLaneIndicatorHideDuration()
        {
            return standardDrawLaneIndicators ? Mathf.Max(0f, standardLaneIndicatorHideSeconds) : 0f;
        }

        protected void CommandLaneCreationWander(IReadOnlyList<Minion> minions, float revealSeconds)
        {
            float duration = Mathf.Max(0f, revealSeconds) + GetInitialLaneShrinkPreludeSeconds();
            if (minions == null || duration <= 0f)
            {
                return;
            }

            for (int i = 0; i < minions.Count; i++)
            {
                minions[i]?.CommandWander(
                    duration,
                    laneCreationWanderSpeed,
                    laneCreationWanderRadius,
                    laneCreationWanderRetargetSeconds);
            }
        }

        protected float GetInitialLaneShrinkPreludeSeconds()
        {
            return InitialLaneHoldSeconds + LaneShrinkSeconds;
        }

        protected virtual float EstimateLongestVolleyWaitSeconds(IReadOnlyList<Volley> pool, bool hasNextVolley)
        {
            float movementDuration = EstimateMovementDurationSeconds();
            float longestSeconds = 0f;
            if (pool == null)
            {
                return longestSeconds;
            }

            for (int i = 0; i < pool.Count; i++)
            {
                Volley volley = pool[i];
                if (volley == null)
                {
                    continue;
                }

                float waitForMovementSeconds = (waitForDuration || hasNextVolley) ? movementDuration : 0f;
                float waitSeconds = Mathf.Max(GetMaxFireSeconds(volley.FireTimings), waitForMovementSeconds);
                longestSeconds = Mathf.Max(longestSeconds, waitSeconds);
            }

            return longestSeconds;
        }

        private float EstimateMovementDurationSeconds()
        {
            return MoveToStartSeconds
                + GetMaxStartDelaySeconds(StartTimings)
                + (RushDistance / RushSpeed);
        }

        private static float GetMaxStartDelaySeconds(IReadOnlyList<StartTiming> timings)
        {
            float maxSeconds = 0f;
            if (timings == null)
            {
                return maxSeconds;
            }

            for (int i = 0; i < timings.Count; i++)
            {
                if (timings[i] != null)
                {
                    maxSeconds = Mathf.Max(maxSeconds, timings[i].StartSeconds);
                }
            }

            return maxSeconds;
        }

        protected virtual List<ExecutionVolley> BuildExecutionVolleys()
        {
            return BuildExecutionVolleysFromPool(Volleys, GetSelectedLaneSteps());
        }

        protected List<ExecutionVolley> BuildExecutionVolleysFromPool(
            IReadOnlyList<Volley> pool,
            IReadOnlyList<LaneStep> laneSteps)
        {
            return BuildExecutionVolleysFromPool(
                pool,
                laneSteps,
                LineDistanceFromPlayer,
                LineSpacing,
                MoveToStartSeconds,
                RushDistance,
                RushSpeed,
                RestSeconds,
                StartTimings);
        }

        protected static List<ExecutionVolley> BuildExecutionVolleysFromPool(
            IReadOnlyList<Volley> pool,
            IReadOnlyList<LaneStep> laneSteps,
            float lineDistanceFromPlayer,
            float lineSpacing,
            float moveToStartSeconds,
            float rushDistance,
            float rushSpeed,
            float restSeconds,
            IReadOnlyList<StartTiming> startTimings)
        {
            List<ExecutionVolley> executionVolleys = new();
            List<int> poolIndices = GetAvailablePoolIndices(pool);
            if (laneSteps == null || laneSteps.Count == 0 || poolIndices.Count == 0)
            {
                return executionVolleys;
            }

            int stepCount = Mathf.Min(laneSteps.Count, poolIndices.Count);
            for (int stepIndex = 0; stepIndex < stepCount; stepIndex++)
            {
                int choiceIndex = UnityEngine.Random.Range(0, poolIndices.Count);
                Volley selected = pool[poolIndices[choiceIndex]];
                poolIndices.RemoveAt(choiceIndex);

                LaneStep step = laneSteps[stepIndex];
                executionVolleys.Add(new ExecutionVolley(
                    step.Side,
                    step.RushPositiveDirection,
                    lineDistanceFromPlayer,
                    lineSpacing,
                    moveToStartSeconds,
                    rushDistance,
                    rushSpeed,
                    restSeconds,
                    startTimings,
                    selected.FireTimings));
            }

            return executionVolleys;
        }

        private static List<int> GetAvailablePoolIndices(IReadOnlyList<Volley> pool)
        {
            List<int> poolIndices = new();
            if (pool == null)
            {
                return poolIndices;
            }

            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null)
                {
                    poolIndices.Add(i);
                }
            }

            return poolIndices;
        }

        private IReadOnlyList<LaneStep> GetSelectedLaneSteps()
        {
            if (!useTwoSides)
            {
                int sideIndex = UnityEngine.Random.Range(0, SingleSideCandidates.Length);
                return new[] { SingleSideCandidates[sideIndex] };
            }

            return UnityEngine.Random.Range(0, 2) == 0 ? TopBottomSteps : LeftRightSteps;
        }

        private IEnumerator ExecuteVolley(
            BossActionContext context,
            IMinionPatternHost host,
            MinionGraphProjectileFireSpec fireSpec,
            ExecutionVolley volley,
            bool hasNextVolley,
            Vector2 patternStartPlayerPosition)
        {
            List<Minion> minions = GetOrderedMinions(host.GetControlledMinionsForGraph());
            if (minions.Count == 0)
            {
                yield break;
            }

            Vector2 rushDirection = GetRushDirection(volley);
            float movementDuration = CommandScoreLaneRush(minions, patternStartPlayerPosition, volley, rushDirection);
            float maxFireSeconds = GetMaxFireSeconds(volley);
            float waitSeconds = Mathf.Max(
                maxFireSeconds,
                (waitForDuration || hasNextVolley) ? movementDuration : 0f);
            waitSeconds = Mathf.Max(waitSeconds, GetAdditionalVolleyWaitSeconds(volley));
            MinionGraphProjectileFireSpec volleyFireSpec = fireSpec.WithFixedDirection(GetProjectileDirection(volley.Side));
            OrderedParrySequence parrySequence = new(volley.FireTimings, CreateTieLinkSettings());
            bool[] fired = new bool[volley.FireTimings != null ? volley.FireTimings.Count : 0];
            float elapsed = 0f;

            OnVolleyRushStarted(context, host, volley, minions);

            while (elapsed < waitSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                TickVolleyRush(context, host, volley, minions, elapsed);
                FireDueProjectiles(context, host, volleyFireSpec, volley, minions, parrySequence, fired, elapsed);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            FireDueProjectiles(context, host, volleyFireSpec, volley, minions, parrySequence, fired, float.PositiveInfinity);
        }

        protected virtual float GetAdditionalVolleyWaitSeconds(ExecutionVolley volley)
        {
            return 0f;
        }

        protected virtual float GetAdditionalRushStartDelaySeconds(ExecutionVolley volley, int minionNumber)
        {
            return 0f;
        }

        protected virtual void OnVolleyRushStarted(
            BossActionContext context,
            IMinionPatternHost host,
            ExecutionVolley volley,
            IReadOnlyList<Minion> minions)
        {
        }

        protected virtual void TickVolleyRush(
            BossActionContext context,
            IMinionPatternHost host,
            ExecutionVolley volley,
            IReadOnlyList<Minion> minions,
            float elapsed)
        {
        }

        protected virtual void OnVolleyProjectileFired(
            BossActionContext context,
            ExecutionVolley volley,
            FireTiming timing,
            BossProjectileSettings projectile,
            EnemyProjectile spawned)
        {
        }

        private TieLinkSettings CreateTieLinkSettings()
        {
            return new TieLinkSettings(
                ShouldDrawParryTieLinks(),
                parryTieLinkColor,
                parryTieLinkWidth,
                parryTieArcHeight,
                parryTieSegments,
                parryTieSortingOrder);
        }

        private float CommandScoreLaneRush(
            IReadOnlyList<Minion> minions,
            Vector2 center,
            ExecutionVolley volley,
            Vector2 rushDirection)
        {
            float maxDuration = 0f;
            Vector2 lineCenter = center + GetSideOffset(volley.Side) * volley.LineDistanceFromPlayer;
            Vector2 lineAxis = IsHorizontalRush(volley.Side) ? Vector2.up : Vector2.right;
            float centeredOffset = (minions.Count - 1) * 0.5f;

            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion == null)
                {
                    continue;
                }

                int minionNumber = GetMinionNumber(minion, i);
                Vector2 laneCenter = lineCenter + lineAxis * ((i - centeredOffset) * volley.LineSpacing);
                Vector2 startPosition = laneCenter - rushDirection * (volley.RushDistance * 0.5f);
                float startDelay = GetStartDelay(volley, minionNumber)
                    + GetAdditionalRushStartDelaySeconds(volley, minionNumber);
                float duration = minion.CommandScoreLaneRush(
                    startPosition,
                    rushDirection,
                    volley.MoveToStartSeconds,
                    startDelay,
                    volley.RushDistance,
                    volley.RushSpeed);
                maxDuration = Mathf.Max(maxDuration, duration);
            }

            return maxDuration;
        }

        private void FireDueProjectiles(
            BossActionContext context,
            IMinionPatternHost host,
            MinionGraphProjectileFireSpec fireSpec,
            ExecutionVolley volley,
            IReadOnlyList<Minion> minions,
            OrderedParrySequence parrySequence,
            bool[] fired,
            float elapsed)
        {
            IReadOnlyList<FireTiming> timings = volley.FireTimings;
            if (timings == null)
            {
                return;
            }

            for (int i = 0; i < timings.Count; i++)
            {
                FireTiming timing = timings[i];
                if (timing == null || i >= fired.Length || fired[i] || elapsed < timing.FireSeconds)
                {
                    continue;
                }

                fired[i] = true;
                Minion minion = FindMinion(minions, timing.MinionNumber);
                BossProjectileSettings projectile = host.ResolveMinionProjectileSettings(timing.ProjectileName);
                if (minion == null || projectile == null)
                {
                    parrySequence.Skip(timing);
                    continue;
                }

                EnemyProjectile spawned = minion.FireOnce(projectile, fireSpec, i);
                spawned?.ConfigurePathIndicatorDelayedUntilLaunch(true);
                if (spawned != null)
                {
                    context.PlaySfx(ResolveFireSfxId(timing));
                    context.PlaySfxOnLaunch(spawned, ResolveLaunchSfxId(timing));
                }

                parrySequence.Register(timing, spawned);
                OnVolleyProjectileFired(context, volley, timing, projectile, spawned);
            }
        }

        private static void HoldMinionsAtCurrentPositions(IReadOnlyList<Minion> minions, float holdSeconds)
        {
            if (minions == null || holdSeconds <= 0f)
            {
                return;
            }

            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion != null && minion.Health != null && !minion.Health.IsDead)
                {
                    minion.CommandHoldPosition(holdSeconds);
                }
            }
        }

        private static List<Minion> GetOrderedMinions(IReadOnlyList<Minion> source)
        {
            List<Minion> results = new();
            if (source == null)
            {
                return results;
            }

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null && source[i].Health != null && !source[i].Health.IsDead)
                {
                    results.Add(source[i]);
                }
            }

            results.Sort((a, b) => GetSortNumber(a).CompareTo(GetSortNumber(b)));
            return results;
        }

        private static int GetSortNumber(Minion minion)
        {
            return minion != null && minion.HasOwnerSlotNumber ? minion.OwnerSlotNumber : int.MaxValue;
        }

        private static int GetMinionNumber(Minion minion, int fallbackIndex)
        {
            return minion != null && minion.HasOwnerSlotNumber ? minion.OwnerSlotNumber : fallbackIndex + 1;
        }

        private static Minion FindMinion(IReadOnlyList<Minion> minions, int minionNumber)
        {
            if (minions == null)
            {
                return null;
            }

            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion != null && GetMinionNumber(minion, i) == minionNumber)
                {
                    return minion;
                }
            }

            return null;
        }

        private static float GetStartDelay(ExecutionVolley volley, int minionNumber)
        {
            IReadOnlyList<StartTiming> timings = volley.StartTimings;
            if (timings == null)
            {
                return 0f;
            }

            for (int i = 0; i < timings.Count; i++)
            {
                StartTiming timing = timings[i];
                if (timing != null && timing.MinionNumber == minionNumber)
                {
                    return timing.StartSeconds;
                }
            }

            return 0f;
        }

        private string ResolveFireSfxId(FireTiming timing)
        {
            return !string.IsNullOrWhiteSpace(timing.FireSfxId) ? timing.FireSfxId : defaultFireSfxId;
        }

        private string ResolveLaunchSfxId(FireTiming timing)
        {
            return !string.IsNullOrWhiteSpace(timing.LaunchSfxId) ? timing.LaunchSfxId : defaultLaunchSfxId;
        }

        private static float GetMaxFireSeconds(ExecutionVolley volley)
        {
            return GetMaxFireSeconds(volley.FireTimings);
        }

        protected static float GetMaxFireSeconds(IReadOnlyList<FireTiming> timings)
        {
            float maxSeconds = 0f;
            if (timings == null)
            {
                return maxSeconds;
            }

            for (int i = 0; i < timings.Count; i++)
            {
                if (timings[i] != null)
                {
                    maxSeconds = Mathf.Max(maxSeconds, timings[i].FireSeconds);
                }
            }

            return maxSeconds;
        }

        protected static Vector2 GetSideOffset(ConductorScoreLaneSide side)
        {
            return side switch
            {
                ConductorScoreLaneSide.Bottom => Vector2.down,
                ConductorScoreLaneSide.Left => Vector2.left,
                ConductorScoreLaneSide.Right => Vector2.right,
                _ => Vector2.up
            };
        }

        private static Vector2 GetRushDirection(ExecutionVolley volley)
        {
            return GetRushDirection(volley.Side, volley.RushPositiveDirection);
        }

        protected static Vector2 GetRushDirection(ConductorScoreLaneSide side, bool positiveDirection)
        {
            if (IsHorizontalRush(side))
            {
                return positiveDirection ? Vector2.right : Vector2.left;
            }

            return positiveDirection ? Vector2.up : Vector2.down;
        }

        private static Vector2 GetProjectileDirection(ConductorScoreLaneSide side)
        {
            return side switch
            {
                ConductorScoreLaneSide.Right => Vector2.up,
                ConductorScoreLaneSide.Bottom => Vector2.right,
                ConductorScoreLaneSide.Left => Vector2.down,
                _ => Vector2.right
            };
        }

        protected static bool IsHorizontalRush(ConductorScoreLaneSide side)
        {
            return side == ConductorScoreLaneSide.Top || side == ConductorScoreLaneSide.Bottom;
        }

        private static bool HasNextVolley(IReadOnlyList<ExecutionVolley> source, int startIndex)
        {
            if (source == null)
            {
                return false;
            }

            for (int i = startIndex; i < source.Count; i++)
            {
                if (source[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct TieLinkSettings
        {
            public TieLinkSettings(bool enabled, Color color, float width, float arcHeight, int segments, int sortingOrder)
            {
                Enabled = enabled;
                Color = color;
                Width = Mathf.Max(0.001f, width);
                ArcHeight = Mathf.Max(0f, arcHeight);
                Segments = Mathf.Clamp(segments, 3, 24);
                SortingOrder = sortingOrder;
            }

            public bool Enabled { get; }
            public Color Color { get; }
            public float Width { get; }
            public float ArcHeight { get; }
            public int Segments { get; }
            public int SortingOrder { get; }
        }

        private sealed class OrderedParrySequence
        {
            private readonly List<Entry> entries = new();
            private readonly List<ConductorOrderedParryLinkVisual.ProjectilePair> tieLinkPairs = new();
            private readonly TieLinkSettings tieLinkSettings;
            private ConductorOrderedParryLinkVisual tieLinkVisual;

            public OrderedParrySequence(IReadOnlyList<FireTiming> timings, TieLinkSettings nextTieLinkSettings)
            {
                tieLinkSettings = nextTieLinkSettings;
                if (timings == null)
                {
                    return;
                }

                for (int i = 0; i < timings.Count; i++)
                {
                    if (timings[i] != null)
                    {
                        entries.Add(new Entry(timings[i], i));
                    }
                }

                entries.Sort((a, b) =>
                {
                    int orderCompare = a.Timing.ParryOrder.CompareTo(b.Timing.ParryOrder);
                    return orderCompare != 0 ? orderCompare : a.SourceIndex.CompareTo(b.SourceIndex);
                });
            }

            public void Register(FireTiming timing, EnemyProjectile projectile)
            {
                Entry entry = FindEntry(timing);
                if (entry == null)
                {
                    return;
                }

                if (projectile == null)
                {
                    entry.Completed = true;
                    Refresh();
                    return;
                }

                entry.Projectile = projectile;
                entry.LinkReleased = !projectile.IsCharging;
                ConfigureEntryProgress(entry, projectile);
                SetInterceptable(projectile, false);
                AttachEntryHandlers(entry, projectile);
                Refresh();
            }

            public void Skip(FireTiming timing)
            {
                Entry entry = FindEntry(timing);
                if (entry == null)
                {
                    return;
                }

                ReleaseEntryHandlers(entry, entry.Projectile);
                entry.Projectile = null;
                entry.Completed = true;
                Refresh();
            }

            private Entry FindEntry(FireTiming timing)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].Timing == timing)
                    {
                        return entries[i];
                    }
                }

                return null;
            }

            private void HandleDestroyed(Entry entry, EnemyProjectile projectile, EnemyProjectileDestroyReason _)
            {
                if (entry == null || projectile == null || entry.Projectile != projectile)
                {
                    return;
                }

                ReleaseEntryHandlers(entry, projectile);
                entry.Projectile = null;
                entry.Completed = true;
                Refresh();
            }

            private void HandleLaunchReplaced(
                Entry entry,
                EnemyProjectile source,
                EnemyProjectile replacement)
            {
                if (entry == null
                    || source == null
                    || replacement == null
                    || entry.Projectile != source)
                {
                    return;
                }

                DetachEntryHandlers(entry, source);
                entry.Projectile = replacement;
                entry.LinkReleased = !replacement.IsCharging;
                ConfigureEntryProgress(entry, replacement);
                AttachEntryHandlers(entry, replacement);
                Refresh();
            }

            private void HandleLaunched(Entry entry, EnemyProjectile projectile)
            {
                if (entry == null || projectile == null || entry.Projectile != projectile)
                {
                    return;
                }

                entry.LinkReleased = true;
                RefreshTieLinks();
            }

            private void HandleSequenceStepCompleted(Entry entry, EnemyProjectile projectile)
            {
                if (entry == null || projectile == null || entry.Projectile != projectile)
                {
                    return;
                }

                int nextCompletedSteps = entry.CompletedStepCount + 1;
                if (projectile is IConductorMultiStepOrderedProjectile multiStepProjectile)
                {
                    entry.RequiredStepCount = Mathf.Max(1, multiStepProjectile.RequiredSequenceSteps);
                    nextCompletedSteps = Mathf.Max(nextCompletedSteps, multiStepProjectile.CompletedSequenceSteps);
                }

                entry.CompletedStepCount = Mathf.Clamp(nextCompletedSteps, 0, entry.RequiredStepCount);
                Refresh();
            }

            private void Refresh()
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (!entries[i].Completed && entries[i].Projectile != null)
                    {
                        SetInterceptable(entries[i].Projectile, false);
                    }
                }

                Entry targetEntry = GetCurrentTargetEntry();
                if (targetEntry == null)
                {
                    RefreshTieLinks();
                    return;
                }

                if (targetEntry.Projectile != null)
                {
                    SetInterceptable(targetEntry.Projectile, true);
                }

                RefreshTieLinks();
            }

            private Entry GetCurrentTargetEntry()
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    Entry entry = entries[i];
                    if (!entry.Completed && entry.Projectile != null)
                    {
                        return entry;
                    }
                }

                return null;
            }

            private static void SetInterceptable(EnemyProjectile projectile, bool interceptable)
            {
                if (projectile == null)
                {
                    return;
                }

                if (projectile is IConductorMultiStepOrderedProjectile multiStepProjectile)
                {
                    multiStepProjectile.SetSequenceActive(interceptable);
                }
                else if (projectile is ConductorOrderedParryProjectile orderedProjectile)
                {
                    orderedProjectile.SetSequenceActive(interceptable);
                }
                else
                {
                    projectile.ConfigureInterceptable(interceptable);
                }

                if (!interceptable)
                {
                    projectile.SetParryLockOnIndicatorVisible(false);
                }
            }

            private void ConfigureEntryProgress(Entry entry, EnemyProjectile projectile)
            {
                entry.RequiredStepCount = 1;
                entry.CompletedStepCount = 0;
                if (projectile is not IConductorMultiStepOrderedProjectile multiStepProjectile)
                {
                    return;
                }

                entry.RequiredStepCount = Mathf.Max(1, multiStepProjectile.RequiredSequenceSteps);
                entry.CompletedStepCount = Mathf.Clamp(
                    multiStepProjectile.CompletedSequenceSteps,
                    0,
                    entry.RequiredStepCount);
            }

            private void AttachEntryHandlers(Entry entry, EnemyProjectile projectile)
            {
                entry.LaunchedHandler ??= launched => HandleLaunched(entry, launched);
                entry.DestroyedHandler ??= (destroyed, reason, _) => HandleDestroyed(entry, destroyed, reason);
                entry.ReplacementHandler ??= (source, replacement) =>
                    HandleLaunchReplaced(entry, source, replacement);

                projectile.Launched -= entry.LaunchedHandler;
                projectile.Launched += entry.LaunchedHandler;
                projectile.Destroyed -= entry.DestroyedHandler;
                projectile.Destroyed += entry.DestroyedHandler;
                projectile.LaunchReplaced -= entry.ReplacementHandler;
                projectile.LaunchReplaced += entry.ReplacementHandler;

                if (projectile is not IConductorMultiStepOrderedProjectile multiStepProjectile)
                {
                    return;
                }

                entry.SequenceStepHandler ??= completedProjectile =>
                    HandleSequenceStepCompleted(entry, completedProjectile);
                multiStepProjectile.SequenceStepCompleted -= entry.SequenceStepHandler;
                multiStepProjectile.SequenceStepCompleted += entry.SequenceStepHandler;
            }

            private static void ReleaseEntryHandlers(Entry entry, EnemyProjectile projectile)
            {
                if (entry == null)
                {
                    return;
                }

                DetachEntryHandlers(entry, projectile);

                entry.DestroyedHandler = null;
                entry.LaunchedHandler = null;
                entry.ReplacementHandler = null;
                entry.SequenceStepHandler = null;
            }

            private static void DetachEntryHandlers(Entry entry, EnemyProjectile projectile)
            {
                if (entry == null || projectile == null)
                {
                    return;
                }

                if (entry.DestroyedHandler != null)
                {
                    projectile.Destroyed -= entry.DestroyedHandler;
                }

                if (entry.LaunchedHandler != null)
                {
                    projectile.Launched -= entry.LaunchedHandler;
                }

                if (entry.ReplacementHandler != null)
                {
                    projectile.LaunchReplaced -= entry.ReplacementHandler;
                }

                if (projectile is IConductorMultiStepOrderedProjectile multiStepProjectile
                    && entry.SequenceStepHandler != null)
                {
                    multiStepProjectile.SequenceStepCompleted -= entry.SequenceStepHandler;
                }
            }

            private void RefreshTieLinks()
            {
                if (!tieLinkSettings.Enabled)
                {
                    return;
                }

                tieLinkPairs.Clear();
                for (int i = 0; i < entries.Count - 1; i++)
                {
                    Entry from = entries[i];
                    Entry to = entries[i + 1];
                    if (from.Completed
                        || to.Completed
                        || from.LinkReleased
                        || to.LinkReleased
                        || from.Projectile == null
                        || to.Projectile == null)
                    {
                        continue;
                    }

                    tieLinkPairs.Add(new ConductorOrderedParryLinkVisual.ProjectilePair(from.Projectile, to.Projectile));
                }

                if (tieLinkPairs.Count <= 0)
                {
                    if (IsComplete())
                    {
                        ClearTieLinks();
                    }
                    else if (tieLinkVisual != null)
                    {
                        tieLinkVisual.SetPairs(tieLinkPairs);
                    }

                    return;
                }

                EnsureTieLinkVisual().SetPairs(tieLinkPairs);
            }

            private ConductorOrderedParryLinkVisual EnsureTieLinkVisual()
            {
                if (tieLinkVisual != null)
                {
                    return tieLinkVisual;
                }

                GameObject linkObject = new("ConductorOrderedParryTieLinks");
                tieLinkVisual = linkObject.AddComponent<ConductorOrderedParryLinkVisual>();
                tieLinkVisual.Configure(
                    tieLinkSettings.Color,
                    tieLinkSettings.Width,
                    tieLinkSettings.ArcHeight,
                    tieLinkSettings.Segments,
                    tieLinkSettings.SortingOrder);
                return tieLinkVisual;
            }

            private void ClearTieLinks()
            {
                if (tieLinkVisual == null)
                {
                    return;
                }

                tieLinkVisual.ClearAndDestroy();
                tieLinkVisual = null;
            }

            private bool IsComplete()
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (!entries[i].Completed)
                    {
                        return false;
                    }
                }

                return true;
            }

            private sealed class Entry
            {
                public Entry(FireTiming timing, int sourceIndex)
                {
                    Timing = timing;
                    SourceIndex = sourceIndex;
                    RequiredStepCount = 1;
                }

                public FireTiming Timing { get; }
                public int SourceIndex { get; }
                public EnemyProjectile Projectile { get; set; }
                public int RequiredStepCount { get; set; }
                public int CompletedStepCount { get; set; }
                public bool LinkReleased { get; set; }
                public bool Completed
                {
                    get => CompletedStepCount >= RequiredStepCount;
                    set => CompletedStepCount = value ? RequiredStepCount : 0;
                }
                public Action<EnemyProjectile, EnemyProjectileDestroyReason, Vector3> DestroyedHandler { get; set; }
                public Action<EnemyProjectile> LaunchedHandler { get; set; }
                public Action<EnemyProjectile, EnemyProjectile> ReplacementHandler { get; set; }
                public Action<EnemyProjectile> SequenceStepHandler { get; set; }
            }
        }
    }
}
