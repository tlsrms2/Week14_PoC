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
    public class MinionConductorScoreLaneRushAction : BossAction
    {
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

            public int MinionNumber => Mathf.Max(1, minionNumber);
            public float FireSeconds => Mathf.Max(0f, fireSeconds);
            public int ParryOrder => Mathf.Max(1, parryOrder);
            public string ProjectileName => projectileName?.Trim();
        }

        [Serializable]
        public sealed class Volley
        {
            [SerializeField] private ConductorScoreLaneSide side;
            [SerializeField] private bool rushPositiveDirection = true;
            [SerializeField, Min(0.1f)] private float lineDistanceFromPlayer = 3f;
            [SerializeField, Min(0f)] private float lineSpacing = 0.7f;
            [SerializeField, Min(0f)] private float moveToStartSeconds = 0.35f;
            [SerializeField, Min(0f)] private float rushDistance = 8f;
            [SerializeField, Min(0.01f)] private float rushSpeed = 10f;
            [SerializeField, Min(0f)] private float restSeconds = 0.35f;
            [SerializeField] private List<StartTiming> startTimings = new();
            [SerializeField] private List<FireTiming> fireTimings = new() { new FireTiming() };

            public Volley()
            {
            }

            public Volley(
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
                this.side = side;
                this.rushPositiveDirection = rushPositiveDirection;
                this.lineDistanceFromPlayer = Mathf.Max(0.1f, lineDistanceFromPlayer);
                this.lineSpacing = Mathf.Max(0f, lineSpacing);
                this.moveToStartSeconds = Mathf.Max(0f, moveToStartSeconds);
                this.rushDistance = Mathf.Max(0f, rushDistance);
                this.rushSpeed = Mathf.Max(0.01f, rushSpeed);
                this.restSeconds = Mathf.Max(0f, restSeconds);
                this.startTimings = startTimings != null ? new List<StartTiming>(startTimings) : new List<StartTiming>();
                this.fireTimings = fireTimings != null ? new List<FireTiming>(fireTimings) : new List<FireTiming>();
            }

            public ConductorScoreLaneSide Side => side;
            public bool RushPositiveDirection => rushPositiveDirection;
            public float LineDistanceFromPlayer => Mathf.Max(0.1f, lineDistanceFromPlayer);
            public float LineSpacing => Mathf.Max(0f, lineSpacing);
            public float MoveToStartSeconds => Mathf.Max(0f, moveToStartSeconds);
            public float RushDistance => Mathf.Max(0f, rushDistance);
            public float RushSpeed => Mathf.Max(0.01f, rushSpeed);
            public float RestSeconds => Mathf.Max(0f, restSeconds);
            public IReadOnlyList<StartTiming> StartTimings => startTimings;
            public IReadOnlyList<FireTiming> FireTimings => fireTimings;
        }

        [SerializeField] private MinionGraphProjectileOriginSpec minionOrigin = new();
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField, Min(0f)] private float windupSeconds;
        [SerializeField, InspectorName("Volleys")] private List<Volley> volleys = new() { new Volley() };
        [SerializeField] private bool drawParryTieLinks = true;
        [SerializeField] private Color parryTieLinkColor = new(0.62f, 0.92f, 1f, 0.78f);
        [SerializeField, Min(0.001f)] private float parryTieLinkWidth = 0.04f;
        [SerializeField, Min(0f)] private float parryTieArcHeight = 0.32f;
        [SerializeField, Range(3, 24)] private int parryTieSegments = 8;
        [SerializeField] private int parryTieSortingOrder = 68;
        [SerializeField] private bool waitForDuration = true;

        protected IReadOnlyList<Volley> Volleys => volleys;
        protected float WindupSeconds => Mathf.Max(0f, windupSeconds);

        public override IEnumerator Execute(BossActionContext context)
        {
            IReadOnlyList<Volley> executionVolleys = Volleys;
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host)
                || context.Boss == null
                || executionVolleys == null
                || executionVolleys.Count == 0)
            {
                yield break;
            }

            Vector2 patternStartPlayerPosition = ResolvePatternCenter(context);
            yield return MinionGraphCommandRunner.WaitWindupIfNeeded(context, WindupSeconds);
            yield return BeforeExecuteVolleys(context, patternStartPlayerPosition);
            yield return ExecuteVolleySequence(context, host, patternStartPlayerPosition, executionVolleys);
            yield return AfterExecuteVolleys(context, patternStartPlayerPosition);
        }

        protected IEnumerator ExecuteVolleySequence(
            BossActionContext context,
            IMinionPatternHost host,
            Vector2 patternStartPlayerPosition,
            IReadOnlyList<Volley> executionVolleys)
        {
            MinionGraphProjectileFireSpec fireSpec = new(minionOrigin, null, effects, context);
            for (int volleyIndex = 0; volleyIndex < executionVolleys.Count; volleyIndex++)
            {
                Volley volley = executionVolleys[volleyIndex];
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

        protected virtual IEnumerator AfterExecuteVolleys(BossActionContext context, Vector2 patternStartPlayerPosition)
        {
            yield break;
        }

        protected virtual Vector2 ResolvePatternCenter(BossActionContext context)
        {
            if (context.Boss.Player != null)
            {
                return context.Boss.Player.position;
            }

            return context.Boss.transform.position;
        }

        protected virtual bool ShouldDrawParryTieLinks()
        {
            return drawParryTieLinks;
        }

        private IEnumerator ExecuteVolley(
            BossActionContext context,
            IMinionPatternHost host,
            MinionGraphProjectileFireSpec fireSpec,
            Volley volley,
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
            float waitSeconds = Mathf.Max(maxFireSeconds, (waitForDuration || hasNextVolley) ? movementDuration : 0f);
            MinionGraphProjectileFireSpec volleyFireSpec = fireSpec.WithFixedDirection(GetProjectileDirection(volley.Side));
            OrderedParrySequence parrySequence = new(volley.FireTimings, CreateTieLinkSettings());
            bool[] fired = new bool[volley.FireTimings != null ? volley.FireTimings.Count : 0];
            float elapsed = 0f;

            while (elapsed < waitSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                FireDueProjectiles(context, host, volleyFireSpec, volley, minions, parrySequence, fired, elapsed);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            FireDueProjectiles(context, host, volleyFireSpec, volley, minions, parrySequence, fired, float.PositiveInfinity);
        }

        protected virtual void OnVolleyProjectileFired(
            BossActionContext context,
            Volley volley,
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
            Volley volley,
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
                float startDelay = GetStartDelay(volley, minionNumber);
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
            Volley volley,
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

        private static float GetStartDelay(Volley volley, int minionNumber)
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

        private static float GetMaxFireSeconds(Volley volley)
        {
            IReadOnlyList<FireTiming> timings = volley.FireTimings;
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

        private static Vector2 GetRushDirection(Volley volley)
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

        private static bool HasNextVolley(IReadOnlyList<Volley> source, int startIndex)
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
                entry.RequiredStepCount = 1;
                entry.CompletedStepCount = 0;
                entry.LinkReleased = !projectile.IsCharging;
                if (projectile is IConductorMultiStepOrderedProjectile multiStepProjectile)
                {
                    entry.RequiredStepCount = Mathf.Max(1, multiStepProjectile.RequiredSequenceSteps);
                    entry.CompletedStepCount = Mathf.Clamp(
                        multiStepProjectile.CompletedSequenceSteps,
                        0,
                        entry.RequiredStepCount);
                    entry.SequenceStepHandler = completedProjectile =>
                        HandleSequenceStepCompleted(entry, completedProjectile);
                    multiStepProjectile.SequenceStepCompleted += entry.SequenceStepHandler;
                }

                SetInterceptable(projectile, false);
                entry.LaunchedHandler = launched => HandleLaunched(entry, launched);
                projectile.Launched += entry.LaunchedHandler;
                entry.DestroyedHandler = (destroyed, reason, _) => HandleDestroyed(entry, destroyed, reason);
                projectile.Destroyed += entry.DestroyedHandler;
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
                ReleaseEntryHandlers(entry, projectile);
                entry.Completed = true;
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
                    if (entries[i].Projectile != null)
                    {
                        SetInterceptable(entries[i].Projectile, false);
                    }
                }

                int targetStep = GetCurrentTargetStep();
                if (targetStep < 0)
                {
                    RefreshTieLinks();
                    return;
                }

                for (int i = 0; i < entries.Count; i++)
                {
                    Entry entry = entries[i];
                    if (entry.Completed || entry.CompletedStepCount != targetStep)
                    {
                        continue;
                    }

                    if (entry.Projectile == null)
                    {
                        RefreshTieLinks();
                        return;
                    }

                    SetInterceptable(entry.Projectile, true);
                    break;
                }

                RefreshTieLinks();
            }

            private int GetCurrentTargetStep()
            {
                int targetStep = int.MaxValue;
                for (int i = 0; i < entries.Count; i++)
                {
                    Entry entry = entries[i];
                    if (!entry.Completed)
                    {
                        targetStep = Mathf.Min(targetStep, entry.CompletedStepCount);
                    }
                }

                return targetStep == int.MaxValue ? -1 : targetStep;
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

            private static void ReleaseEntryHandlers(Entry entry, EnemyProjectile projectile)
            {
                if (entry == null)
                {
                    return;
                }

                if (projectile != null && entry.DestroyedHandler != null)
                {
                    projectile.Destroyed -= entry.DestroyedHandler;
                }

                if (projectile != null && entry.LaunchedHandler != null)
                {
                    projectile.Launched -= entry.LaunchedHandler;
                }

                if (projectile is IConductorMultiStepOrderedProjectile multiStepProjectile
                    && entry.SequenceStepHandler != null)
                {
                    multiStepProjectile.SequenceStepCompleted -= entry.SequenceStepHandler;
                }

                entry.DestroyedHandler = null;
                entry.LaunchedHandler = null;
                entry.SequenceStepHandler = null;
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
                public Action<EnemyProjectile> SequenceStepHandler { get; set; }
            }
        }
    }
}
