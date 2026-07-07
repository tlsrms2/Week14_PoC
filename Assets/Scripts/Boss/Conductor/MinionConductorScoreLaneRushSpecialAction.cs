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
        [Serializable]
        private sealed class SpecialVolley
        {
            [SerializeField, Min(0f)] private float restSeconds = 0.35f;
            [SerializeField] private List<FireTiming> fireTimings = new() { new FireTiming() };

            public float RestSeconds => Mathf.Max(0f, restSeconds);
            public IReadOnlyList<FireTiming> FireTimings => fireTimings;
        }

        private readonly struct LaneStep
        {
            public LaneStep(ConductorScoreLaneSide side, bool rushPositiveDirection)
            {
                Side = side;
                RushPositiveDirection = rushPositiveDirection;
            }

            public ConductorScoreLaneSide Side { get; }
            public bool RushPositiveDirection { get; }
        }

        private static readonly ConductorScoreLaneSide[] IndicatorSideOrder =
        {
            ConductorScoreLaneSide.Top,
            ConductorScoreLaneSide.Right,
            ConductorScoreLaneSide.Bottom,
            ConductorScoreLaneSide.Left
        };

        private static readonly LaneStep[] ExecutionOrder =
        {
            new(ConductorScoreLaneSide.Top, true),
            new(ConductorScoreLaneSide.Right, false),
            new(ConductorScoreLaneSide.Bottom, false),
            new(ConductorScoreLaneSide.Left, true)
        };

        [SerializeField] private bool drawLaneIndicators = true;
        [SerializeField, InspectorName("Pattern Center")] private Vector2 patternCenter;
        [Header("Common Rush Settings")]
        [SerializeField, Min(0.1f)] private float lineDistanceFromPlayer = 3f;
        [SerializeField, Min(0f)] private float lineSpacing = 0.7f;
        [SerializeField, Min(0f)] private float moveToStartSeconds = 0.35f;
        [SerializeField, Min(0f)] private float rushDistance = 8f;
        [SerializeField, Min(0.01f)] private float rushSpeed = 10f;
        [SerializeField] private List<StartTiming> startTimings = new();
        [SerializeField, InspectorName("Volleys")] private List<SpecialVolley> specialVolleys = new() { new SpecialVolley() };
        [Header("Lane Indicators")]
        [SerializeField] private Color laneIndicatorColor = new(0.62f, 0.92f, 1f, 0.66f);
        [SerializeField, Min(0.001f)] private float laneIndicatorWidth = 0.035f;
        [SerializeField, Min(0f)] private float laneIndicatorRevealSeconds = 0.05f;
        [SerializeField, Min(0f)] private float laneIndicatorRevealInterval = 0.01f;
        [SerializeField, Min(0f)] private float laneIndicatorHideSeconds = 0.14f;
        [SerializeField, Min(0f)] private float postProjectileClearDelaySeconds = 0.5f;
        [SerializeField] private int laneIndicatorSortingOrder = 66;
        [SerializeField, Min(1)] private int laneIndicatorLineCount = 4;

        private readonly List<EnemyProjectile> trackedProjectiles = new();
        private ConductorScoreLaneRushIndicatorVisual activeLaneIndicators;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host)
                || context.Boss == null)
            {
                yield break;
            }

            List<Volley> executionVolleys = BuildExecutionVolleys();
            if (executionVolleys.Count == 0)
            {
                yield break;
            }

            Vector2 patternStartPlayerPosition = ResolvePatternCenter(context);
            yield return MinionGraphCommandRunner.WaitWindupIfNeeded(context, WindupSeconds);
            float prepositionSeconds = CommandMinionsToFirstVolleyStart(host, patternStartPlayerPosition, executionVolleys[0]);
            yield return BeforeExecuteVolleys(context, patternStartPlayerPosition);
            float remainingPrepositionSeconds = prepositionSeconds - GetLaneIndicatorRevealDuration();
            if (remainingPrepositionSeconds > 0f)
            {
                yield return context.WaitSeconds(remainingPrepositionSeconds);
            }

            yield return ExecuteVolleySequence(context, host, patternStartPlayerPosition, executionVolleys);
            yield return AfterExecuteVolleys(context, patternStartPlayerPosition);
        }

        protected override IEnumerator BeforeExecuteVolleys(BossActionContext context, Vector2 patternStartPlayerPosition)
        {
            trackedProjectiles.Clear();
            ClearActiveLaneIndicators();
            activeLaneIndicators = CreateLaneIndicators(patternStartPlayerPosition);
            yield return RevealLaneIndicators(context, activeLaneIndicators);
        }

        protected override Vector2 ResolvePatternCenter(BossActionContext context)
        {
            return patternCenter;
        }

        protected override IEnumerator AfterExecuteVolleys(BossActionContext context, Vector2 patternStartPlayerPosition)
        {
            yield return WaitForTrackedProjectiles(context);
            yield return context.WaitSeconds(postProjectileClearDelaySeconds);
            yield return HideLaneIndicators(context, activeLaneIndicators);
            activeLaneIndicators = null;
            trackedProjectiles.Clear();
        }

        protected override void OnVolleyProjectileFired(
            BossActionContext context,
            Volley volley,
            FireTiming timing,
            BossProjectileSettings projectile,
            EnemyProjectile spawned)
        {
            if (spawned != null)
            {
                trackedProjectiles.Add(spawned);
            }
        }

        private List<Volley> BuildExecutionVolleys()
        {
            List<Volley> executionVolleys = new();
            List<int> poolIndices = GetAvailablePoolIndices();
            int stepCount = Mathf.Min(ExecutionOrder.Length, poolIndices.Count);
            for (int stepIndex = 0; stepIndex < stepCount; stepIndex++)
            {
                int choiceIndex = UnityEngine.Random.Range(0, poolIndices.Count);
                SpecialVolley selected = specialVolleys[poolIndices[choiceIndex]];
                poolIndices.RemoveAt(choiceIndex);

                LaneStep step = ExecutionOrder[stepIndex];
                executionVolleys.Add(new Volley(
                    step.Side,
                    step.RushPositiveDirection,
                    lineDistanceFromPlayer,
                    lineSpacing,
                    moveToStartSeconds,
                    rushDistance,
                    rushSpeed,
                    selected.RestSeconds,
                    startTimings,
                    selected.FireTimings));
            }

            return executionVolleys;
        }

        private List<int> GetAvailablePoolIndices()
        {
            List<int> poolIndices = new();
            if (specialVolleys == null)
            {
                return poolIndices;
            }

            for (int i = 0; i < specialVolleys.Count; i++)
            {
                if (specialVolleys[i] != null)
                {
                    poolIndices.Add(i);
                }
            }

            return poolIndices;
        }

        private float CommandMinionsToFirstVolleyStart(
            IMinionPatternHost host,
            Vector2 center,
            Volley firstVolley)
        {
            if (host == null || firstVolley == null)
            {
                return 0f;
            }

            List<Minion> minions = GetOrderedMinions(host.GetControlledMinionsForGraph());
            if (minions.Count == 0)
            {
                return 0f;
            }

            float maxDuration = 0f;
            Vector2 rushDirection = GetRushDirection(firstVolley.Side, firstVolley.RushPositiveDirection);
            Vector2 lineCenter = center + GetSideOffset(firstVolley.Side) * firstVolley.LineDistanceFromPlayer;
            Vector2 lineAxis = IsHorizontalRush(firstVolley.Side) ? Vector2.up : Vector2.right;
            float centeredOffset = (minions.Count - 1) * 0.5f;
            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion == null)
                {
                    continue;
                }

                Vector2 laneCenter = lineCenter + lineAxis * ((i - centeredOffset) * firstVolley.LineSpacing);
                Vector2 startPosition = laneCenter - rushDirection * (firstVolley.RushDistance * 0.5f);
                float duration = minion.CommandScoreLaneRush(
                    startPosition,
                    rushDirection,
                    moveToStartSeconds,
                    0f,
                    0f,
                    firstVolley.RushSpeed);
                maxDuration = Mathf.Max(maxDuration, duration);
            }

            return maxDuration;
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

        private static List<Minion> GetOrderedMinions(IReadOnlyList<Minion> source)
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
            return results;
        }

        private static int GetSortNumber(Minion minion)
        {
            return minion != null && minion.HasOwnerSlotNumber ? minion.OwnerSlotNumber : int.MaxValue;
        }

        private ConductorScoreLaneRushIndicatorVisual CreateLaneIndicators(Vector2 center)
        {
            if (!drawLaneIndicators)
            {
                return null;
            }

            GameObject indicatorObject = new("ConductorScoreLaneRushSpecialIndicators");
            ConductorScoreLaneRushIndicatorVisual visual = indicatorObject.AddComponent<ConductorScoreLaneRushIndicatorVisual>();
            visual.Configure(laneIndicatorColor, laneIndicatorWidth, laneIndicatorSortingOrder);

            int lineIndex = 0;
            for (int i = 0; i < IndicatorSideOrder.Length; i++)
            {
                ConductorScoreLaneSide side = IndicatorSideOrder[i];
                int lineCount = Mathf.Max(1, laneIndicatorLineCount);
                for (int laneIndex = 0; laneIndex < lineCount; laneIndex++)
                {
                    BuildLaneIndicatorLine(center, side, laneIndex, lineCount, out Vector2 start, out Vector2 end);
                    visual.SetLane(lineIndex, start, end);
                    lineIndex++;
                }
            }

            return visual;
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
                if (trackedProjectiles[i] == null)
                {
                    trackedProjectiles.RemoveAt(i);
                    continue;
                }

                return true;
            }

            return false;
        }

        private void BuildLaneIndicatorLine(
            Vector2 center,
            ConductorScoreLaneSide side,
            int laneIndex,
            int laneCount,
            out Vector2 start,
            out Vector2 end)
        {
            bool positiveDirection = GetFixedRushPositiveDirection(side);
            Vector2 rushDirection = GetRushDirection(side, positiveDirection);
            Vector2 lineAxis = IsHorizontalRush(side) ? Vector2.up : Vector2.right;
            Vector2 lineCenter = center + GetSideOffset(side) * Mathf.Max(0.1f, lineDistanceFromPlayer);
            float centeredOffset = (Mathf.Max(1, laneCount) - 1) * 0.5f;
            Vector2 laneCenter = lineCenter + lineAxis * ((laneIndex - centeredOffset) * Mathf.Max(0f, lineSpacing));
            start = laneCenter - rushDirection * (Mathf.Max(0f, rushDistance) * 0.5f);
            end = laneCenter + rushDirection * (Mathf.Max(0f, rushDistance) * 0.5f);
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
