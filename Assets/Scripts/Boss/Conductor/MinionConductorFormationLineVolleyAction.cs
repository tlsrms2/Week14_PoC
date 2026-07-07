using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionConductorFormationLineVolleyAction : BossAction
    {
        [Serializable]
        public sealed class FireTiming
        {
            [SerializeField, Min(1)] private int minionNumber = 1;
            [SerializeField, Min(0f)] private float fireSeconds;
            [SerializeField, BossGraphProjectileName] private string projectileName = "Default";

            public int MinionNumber => Mathf.Max(1, minionNumber);
            public float FireSeconds => Mathf.Max(0f, fireSeconds);
            public string ProjectileName => projectileName?.Trim();
        }

        [Serializable]
        public sealed class Volley
        {
            [SerializeField, Min(0f)] private float restSeconds = 0.25f;
            [SerializeField] private List<FireTiming> fireTimings = new() { new FireTiming() };

            public float RestSeconds => Mathf.Max(0f, restSeconds);
            public IReadOnlyList<FireTiming> FireTimings => fireTimings;
        }

        [Header("Formation Straight")]
        [SerializeField] private MinionGraphFormationStraightMode mode = MinionGraphFormationStraightMode.BetweenBossAndPlayer;
        [SerializeField, Min(0.1f)] private float distanceFromPlayer = 6f;
        [SerializeField, Min(0.1f)] private float spacing = 1f;
        [SerializeField, Min(0f)] private float speedMultiplier = 1.2f;
        [SerializeField, Min(0f)] private float settleSeconds = 1f;
        [SerializeField] private bool waitForFormationDuration = true;

        [Header("Projectile")]
        [SerializeField] private MinionGraphProjectileOriginSpec minionOrigin = new();
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField, InspectorName("Volleys")] private List<Volley> volleys = new() { new Volley() };

        [Header("Line Indicator")]
        [SerializeField] private bool drawLineIndicators = true;
        [SerializeField] private Color lineIndicatorColor = new(0.62f, 0.92f, 1f, 0.72f);
        [SerializeField, Min(0.001f)] private float lineIndicatorWidth = 0.035f;
        [SerializeField, Min(0.1f)] private float lineIndicatorLength = 12f;
        [SerializeField, Min(0f)] private float lineIndicatorFadeSeconds = 0.14f;
        [SerializeField] private int lineIndicatorSortingOrder = 66;

        [Header("End")]
        [SerializeField] private bool waitForProjectilesToClear = true;
        [SerializeField, Min(0f)] private float postProjectileClearDelaySeconds = 0.1f;

        private readonly List<EnemyProjectile> trackedProjectiles = new();

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

            MinionGraphCommandRequest formationRequest = MinionGraphCommandRequest.FormationStraight(
                mode,
                distanceFromPlayer,
                spacing,
                speedMultiplier,
                settleSeconds);
            float formationDuration = host.CommandMinions(formationRequest);
            yield return MinionGraphCommandRunner.WaitForDurationIfNeeded(
                context,
                formationDuration,
                waitForFormationDuration);

            List<Minion> minions = GetIndicatorMinions(host.GetControlledMinionsForGraph());
            if (minions.Count == 0)
            {
                yield break;
            }

            float timelineSeconds = GetTimelineSeconds();
            HoldMinions(minions, timelineSeconds + postProjectileClearDelaySeconds + lineIndicatorFadeSeconds);

            Vector2 target = context.Boss.Player.position;
            List<LineSlot> lineSlots = BuildLineSlots(minions, target);
            ConductorScoreLaneRushIndicatorVisual indicator = CreateLineIndicators(lineSlots);

            trackedProjectiles.Clear();
            yield return RunVolleys(context, host, lineSlots);

            if (waitForProjectilesToClear)
            {
                yield return WaitForTrackedProjectiles(context);
            }

            yield return context.WaitSeconds(postProjectileClearDelaySeconds);
            yield return FadeAndClearIndicator(context, indicator);
            trackedProjectiles.Clear();
        }

        private IEnumerator RunVolleys(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<LineSlot> lineSlots)
        {
            for (int volleyIndex = 0; volleyIndex < volleys.Count; volleyIndex++)
            {
                Volley volley = volleys[volleyIndex];
                if (volley == null)
                {
                    continue;
                }

                yield return RunVolley(context, host, lineSlots, volley);
                if (volley.RestSeconds > 0f && HasNextVolley(volleyIndex + 1))
                {
                    yield return context.WaitSeconds(volley.RestSeconds);
                }
            }
        }

        private IEnumerator RunVolley(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<LineSlot> lineSlots,
            Volley volley)
        {
            IReadOnlyList<FireTiming> fireTimings = volley.FireTimings;
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
                    yield return null;
                    continue;
                }

                FireDueProjectiles(context, host, lineSlots, fireTimings, fired, elapsed);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            FireDueProjectiles(context, host, lineSlots, fireTimings, fired, float.PositiveInfinity);
        }

        private void FireDueProjectiles(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<LineSlot> lineSlots,
            IReadOnlyList<FireTiming> fireTimings,
            bool[] fired,
            float elapsed)
        {
            for (int i = 0; i < fireTimings.Count; i++)
            {
                FireTiming timing = fireTimings[i];
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
                    .WithFixedDirection(slot.Direction)
                    .WithProjectilePathIndicatorSuppressed();
                EnemyProjectile spawned = slot.Minion.FireOnce(projectile, fireSpec, 0);
                if (spawned != null)
                {
                    trackedProjectiles.Add(spawned);
                }
            }
        }

        private List<LineSlot> BuildLineSlots(IReadOnlyList<Minion> minions, Vector2 target)
        {
            List<LineSlot> slots = new();
            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion == null)
                {
                    continue;
                }

                Vector2 aimOrigin = minionOrigin.GetAimOrigin(minion, 0);
                Vector2 direction = target - aimOrigin;
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    direction = Vector2.left;
                }

                Vector2 origin = minionOrigin.GetSpawnOrigin(minion, 0, direction);
                direction = target - origin;
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    direction = Vector2.left;
                }

                slots.Add(new LineSlot(
                    minion,
                    GetMinionNumber(minion, i),
                    origin,
                    direction.normalized));
            }

            return slots;
        }

        private ConductorScoreLaneRushIndicatorVisual CreateLineIndicators(IReadOnlyList<LineSlot> lineSlots)
        {
            if (!drawLineIndicators || lineSlots == null || lineSlots.Count == 0)
            {
                return null;
            }

            GameObject indicatorObject = new("ConductorFormationLineVolleyIndicators");
            ConductorScoreLaneRushIndicatorVisual visual = indicatorObject.AddComponent<ConductorScoreLaneRushIndicatorVisual>();
            visual.Configure(lineIndicatorColor, lineIndicatorWidth, lineIndicatorSortingOrder);
            for (int i = 0; i < lineSlots.Count; i++)
            {
                LineSlot slot = lineSlots[i];
                visual.SetLane(i, slot.Origin, slot.Origin + slot.Direction * Mathf.Max(0.1f, lineIndicatorLength));
                visual.SetProgress(i, 1f);
            }

            return visual;
        }

        private IEnumerator FadeAndClearIndicator(BossActionContext context, ConductorScoreLaneRushIndicatorVisual indicator)
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
                    yield return null;
                    continue;
                }

                indicator.SetAlpha(1f - Mathf.Clamp01(elapsed / duration));
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            indicator.ClearAndDestroy();
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

        private float GetTimelineSeconds()
        {
            float seconds = 0f;
            if (volleys == null)
            {
                return seconds;
            }

            for (int i = 0; i < volleys.Count; i++)
            {
                Volley volley = volleys[i];
                if (volley == null)
                {
                    continue;
                }

                seconds += GetMaxFireSeconds(volley.FireTimings);
                if (HasNextVolley(i + 1))
                {
                    seconds += volley.RestSeconds;
                }
            }

            return seconds;
        }

        private bool HasNextVolley(int startIndex)
        {
            if (volleys == null)
            {
                return false;
            }

            for (int i = startIndex; i < volleys.Count; i++)
            {
                if (volleys[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static float GetMaxFireSeconds(IReadOnlyList<FireTiming> fireTimings)
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

        private static List<Minion> GetIndicatorMinions(IReadOnlyList<Minion> source)
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

        private static void HoldMinions(IReadOnlyList<Minion> minions, float holdSeconds)
        {
            if (minions == null)
            {
                return;
            }

            for (int i = 0; i < minions.Count; i++)
            {
                if (minions[i] != null)
                {
                    minions[i].CommandHoldPosition(Mathf.Max(0f, holdSeconds));
                }
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

        private readonly struct LineSlot
        {
            public LineSlot(Minion minion, int minionNumber, Vector2 origin, Vector2 direction)
            {
                Minion = minion;
                MinionNumber = minionNumber;
                Origin = origin;
                Direction = direction;
            }

            public Minion Minion { get; }
            public int MinionNumber { get; }
            public Vector2 Origin { get; }
            public Vector2 Direction { get; }
        }
    }
}
