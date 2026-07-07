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
        private const float DefaultFormationMoveSpeed = 24f;

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
        [SerializeField, Min(0.01f)] private float projectileSpeedMultiplier = 1f;
        [SerializeField] private bool preserveLineDirectionOnLaunch = true;
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField, InspectorName("Volleys")] private List<Volley> volleys = new() { new Volley() };

        [Header("Prepare Phase")]
        [SerializeField] private bool ignorePlayerCollisionDuringPrepare = true;

        [Header("Line Indicator")]
        [SerializeField] private bool drawLineIndicators = true;
        [SerializeField] private Color lineIndicatorColor = new(0.62f, 0.92f, 1f, 0.72f);
        [SerializeField, Min(0.001f)] private float lineIndicatorWidth = 0.035f;
        [SerializeField, Min(0.1f)] private float lineIndicatorLength = 12f;
        [SerializeField, Min(0f)] private float lineIndicatorFadeSeconds = 0.14f;
        [SerializeField] private int lineIndicatorSortingOrder = 66;

        [Header("Launch Phase")]
        [SerializeField, BossGraphProjectileName] private string missedLaunchProjectileName = "Default";
        [SerializeField, Min(0.01f)] private float missedProjectileMinSpeed = 5f;
        [SerializeField, Min(0.01f)] private float missedProjectileMaxSpeed = 9f;
        [SerializeField, Min(0f)] private float missedProjectileMinArcHeight = 0.8f;
        [SerializeField, Min(0f)] private float missedProjectileMaxArcHeight = 2.4f;
        [SerializeField] private bool alternateMissedProjectileArcSide = true;
        [SerializeField, Min(0.1f)] private float launchFormationCircleRadius = 2.8f;
        [SerializeField] private bool launchFormationSideBySide;
        [SerializeField, Min(1f)] private float launchFormationAngleSpacingDegrees = 28f;
        [SerializeField, Min(0f)] private float launchFormationSpeedMultiplier = 0.45f;
        [SerializeField, Min(0f)] private float launchFormationSettleSeconds = 0.5f;

        [Header("Final Shot")]
        [SerializeField] private bool fireFinalProjectile = true;
        [SerializeField, BossGraphProjectileName] private string finalProjectileName = "Default";
        [SerializeField] private MinionGraphProjectileOriginSpec finalProjectileOrigin = new();
        [SerializeField] private BossGraphProjectileAimSpec finalProjectileAim = new();
        [SerializeField] private BossGraphEffectSettings finalProjectileEffects = new();

        private readonly List<ConductorFormationLineProjectileMotion> trackedMotions = new();

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

            List<Minion> minions = GetIndicatorMinions(host.GetControlledMinionsForGraph());
            if (minions.Count == 0)
            {
                yield break;
            }

            float formationDuration = CommandLockedFormation(minions);
            if (waitForFormationDuration && formationDuration > 0f)
            {
                yield return context.WaitSeconds(formationDuration);
            }

            Vector2 target = context.Boss.Player.position;
            Vector2 lineDirection = ResolveSharedLineDirection(minions, target);
            SetFormationFacingOverride(minions, lineDirection);
            List<LineSlot> lineSlots = BuildLineSlots(minions, lineDirection);
            ConductorScoreLaneRushIndicatorVisual indicator = CreateLineIndicators(lineSlots);
            trackedMotions.Clear();
            yield return RunVolleys(context, host, lineSlots, indicator);

            yield return WaitForPrepareProjectilesToReachLineEnd(context, lineSlots, indicator);
            yield return RunLaunchPhase(context, host, lineSlots, indicator);
            ClearFormationFacingOverride(minions);
            trackedMotions.Clear();
        }

        private IEnumerator RunVolleys(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<LineSlot> lineSlots,
            ConductorScoreLaneRushIndicatorVisual indicator)
        {
            for (int volleyIndex = 0; volleyIndex < volleys.Count; volleyIndex++)
            {
                Volley volley = volleys[volleyIndex];
                if (volley == null)
                {
                    continue;
                }

                yield return RunVolley(context, host, lineSlots, indicator, volley);
                if (volley.RestSeconds > 0f && HasNextVolley(volleyIndex + 1))
                {
                    yield return WaitSecondsWithLineUpdate(context, volley.RestSeconds, lineSlots, indicator);
                }
            }
        }

        private IEnumerator RunVolley(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<LineSlot> lineSlots,
            ConductorScoreLaneRushIndicatorVisual indicator,
            Volley volley)
        {
            IReadOnlyList<FireTiming> fireTimings = volley.FireTimings;
            if (fireTimings == null || fireTimings.Count == 0)
            {
                yield break;
            }

            OrderedParrySequence parrySequence = new(fireTimings);
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

                UpdateLineIndicators(indicator, lineSlots);
                FireDueProjectiles(context, host, lineSlots, fireTimings, parrySequence, fired, elapsed);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            UpdateLineIndicators(indicator, lineSlots);
            FireDueProjectiles(context, host, lineSlots, fireTimings, parrySequence, fired, float.PositiveInfinity);
        }

        private void FireDueProjectiles(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<LineSlot> lineSlots,
            IReadOnlyList<FireTiming> fireTimings,
            OrderedParrySequence parrySequence,
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
                    parrySequence.Skip(timing);
                    continue;
                }

                MinionGraphProjectileFireSpec fireSpec = new MinionGraphProjectileFireSpec(minionOrigin, null, effects, context)
                    .WithFixedDirection(slot.LineDirection)
                    .WithProjectilePathIndicatorSuppressed();
                EnemyProjectile spawned = slot.Minion.FireOnce(projectile, fireSpec, 0);
                if (spawned != null)
                {
                    float projectileSpeed = projectile.Speed * Mathf.Max(0.01f, projectileSpeedMultiplier);
                    spawned.ConfigureSpeedMultiplier(projectileSpeedMultiplier);
                    spawned.ConfigureChargeMotion(0f, false, false);
                    spawned.ConfigurePreserveLaunchDirectionOnLaunch(preserveLineDirectionOnLaunch);
                    spawned.ConfigurePlayerCollisionIgnored(ignorePlayerCollisionDuringPrepare);
                    ConductorFormationLineProjectileMotion motion = spawned.gameObject.AddComponent<ConductorFormationLineProjectileMotion>();
                    motion.Initialize(
                        () => GetLaneOrigin(slot, lineSlots),
                        slot.LineDirection,
                        projectileSpeed,
                        lineIndicatorLength);
                    parrySequence.Register(timing, spawned);
                    trackedMotions.Add(motion);
                }
                else
                {
                    parrySequence.Skip(timing);
                }
            }
        }

        private List<LineSlot> BuildLineSlots(IReadOnlyList<Minion> minions, Vector2 lineDirection)
        {
            List<LineSlot> slots = new();
            Vector2 safeLineDirection = lineDirection.sqrMagnitude > 0.0001f ? lineDirection.normalized : Vector2.left;
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
                    safeLineDirection,
                    GetCenteredOffset(i, minions.Count, spacing)));
            }

            return slots;
        }

        private float CommandLockedFormation(IReadOnlyList<Minion> minions)
        {
            if (minions == null)
            {
                return 0f;
            }

            float moveSpeed = DefaultFormationMoveSpeed * Mathf.Max(0f, speedMultiplier);
            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion == null)
                {
                    continue;
                }

                minion.CommandFormationStraightLockedToPlayerOffset(
                    GetCenteredOffset(i, minions.Count, spacing),
                    distanceFromPlayer,
                    mode,
                    moveSpeed);
            }

            return Mathf.Max(0f, settleSeconds);
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
                visual.SetLane(i, origin, origin + slot.LineDirection * Mathf.Max(0.1f, lineIndicatorLength));
                visual.SetProgress(i, 1f);
            }
        }

        private IEnumerator WaitForPrepareProjectilesToReachLineEnd(
            BossActionContext context,
            IReadOnlyList<LineSlot> lineSlots,
            ConductorScoreLaneRushIndicatorVisual indicator)
        {
            while (HasPreparingProjectileBeforeLineEnd())
            {
                if (context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                UpdateLineIndicators(indicator, lineSlots);
                yield return null;
            }
        }

        private IEnumerator RunLaunchPhase(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<LineSlot> lineSlots,
            ConductorScoreLaneRushIndicatorVisual indicator)
        {
            List<ConductorFormationLineProjectileMotion> missedMotions = GetLiveProjectileMotions();
            CommandLaunchFormationCircle(host);
            List<ConductorFormationLineProjectileMotion> launchMotions = BeginMissedProjectileLaunches(
                context,
                host,
                missedMotions);

            float fadeElapsed = 0f;
            bool indicatorCleared = false;
            while (HasLiveProjectileMotion(launchMotions))
            {
                if (context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                TickLaunchIndicatorFade(indicator, lineSlots, ref fadeElapsed, ref indicatorCleared);
                yield return null;
            }

            if (!indicatorCleared && indicator != null)
            {
                indicator.ClearAndDestroy();
            }

            FireFinalProjectile(context, host);
        }

        private List<ConductorFormationLineProjectileMotion> BeginMissedProjectileLaunches(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<ConductorFormationLineProjectileMotion> motions)
        {
            List<ConductorFormationLineProjectileMotion> launchMotions = new();
            if (motions == null)
            {
                return launchMotions;
            }

            for (int i = 0; i < motions.Count; i++)
            {
                ConductorFormationLineProjectileMotion motion = motions[i];
                if (motion == null || !motion.IsLive)
                {
                    continue;
                }

                float side = alternateMissedProjectileArcSide
                    ? (i % 2 == 0 ? 1f : -1f)
                    : (UnityEngine.Random.value < 0.5f ? -1f : 1f);
                float launchSpeed = RandomRange(missedProjectileMinSpeed, missedProjectileMaxSpeed);
                float arcHeight = RandomRange(missedProjectileMinArcHeight, missedProjectileMaxArcHeight);
                EnemyProjectile replacement = SpawnMissedLaunchProjectile(context, host, motion);
                if (replacement != null)
                {
                    ConductorFormationLineProjectileMotion launchMotion =
                        replacement.gameObject.AddComponent<ConductorFormationLineProjectileMotion>();
                    launchMotion.InitializeLaunchPhase(
                        context.Boss.Player,
                        motion.LineDirection,
                        launchSpeed,
                        arcHeight,
                        side);
                    motion.Projectile?.DestroyFromOwner();
                    launchMotions.Add(launchMotion);
                    continue;
                }

                motion.BeginLaunchPhase(
                    context.Boss.Player,
                    launchSpeed,
                    arcHeight,
                    side);
                launchMotions.Add(motion);
            }

            return launchMotions;
        }

        private EnemyProjectile SpawnMissedLaunchProjectile(
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
                suppressHoming: true,
                projectileName: missedLaunchProjectileName);
            if (replacement == null)
            {
                return null;
            }

            replacement.ConfigurePathIndicatorSuppressed(true);
            replacement.ConfigurePlayerCollisionIgnored(false);
            replacement.ConfigureExternalMotionDriven(true);
            return replacement;
        }

        private void CommandLaunchFormationCircle(IMinionPatternHost host)
        {
            if (host == null)
            {
                return;
            }

            MinionGraphCommandRequest request = MinionGraphCommandRequest.FormationCircle(
                launchFormationCircleRadius,
                launchFormationSideBySide,
                launchFormationAngleSpacingDegrees,
                launchFormationSpeedMultiplier,
                launchFormationSettleSeconds);
            host.CommandMinions(request);
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

        private void TickLaunchIndicatorFade(
            ConductorScoreLaneRushIndicatorVisual indicator,
            IReadOnlyList<LineSlot> lineSlots,
            ref float fadeElapsed,
            ref bool indicatorCleared)
        {
            if (indicator == null || indicatorCleared)
            {
                return;
            }

            float duration = Mathf.Max(0f, lineIndicatorFadeSeconds);
            if (duration <= 0f)
            {
                indicator.ClearAndDestroy();
                indicatorCleared = true;
                return;
            }

            UpdateLineIndicators(indicator, lineSlots);
            indicator.SetAlpha(1f - Mathf.Clamp01(fadeElapsed / duration));
            fadeElapsed += EnemyTimeScale.DeltaTime;
            if (fadeElapsed >= duration)
            {
                indicator.ClearAndDestroy();
                indicatorCleared = true;
            }
        }

        private bool HasPreparingProjectileBeforeLineEnd()
        {
            for (int i = trackedMotions.Count - 1; i >= 0; i--)
            {
                ConductorFormationLineProjectileMotion motion = trackedMotions[i];
                if (motion == null || !motion.IsLive)
                {
                    trackedMotions.RemoveAt(i);
                    continue;
                }

                if (!motion.IsReadyForLaunchPhase)
                {
                    return true;
                }
            }

            return false;
        }

        private List<ConductorFormationLineProjectileMotion> GetLiveProjectileMotions()
        {
            List<ConductorFormationLineProjectileMotion> liveMotions = new();
            for (int i = trackedMotions.Count - 1; i >= 0; i--)
            {
                ConductorFormationLineProjectileMotion motion = trackedMotions[i];
                if (motion == null || !motion.IsLive)
                {
                    trackedMotions.RemoveAt(i);
                    continue;
                }

                liveMotions.Add(motion);
            }

            liveMotions.Reverse();
            return liveMotions;
        }

        private static bool HasLiveProjectileMotion(IReadOnlyList<ConductorFormationLineProjectileMotion> motions)
        {
            if (motions == null)
            {
                return false;
            }

            for (int i = 0; i < motions.Count; i++)
            {
                if (motions[i] != null && motions[i].IsLive)
                {
                    return true;
                }
            }

            return false;
        }

        private static float RandomRange(float a, float b)
        {
            float min = Mathf.Min(a, b);
            float max = Mathf.Max(a, b);
            return Mathf.Approximately(min, max) ? min : UnityEngine.Random.Range(min, max);
        }

        private IEnumerator WaitSecondsWithLineUpdate(
            BossActionContext context,
            float seconds,
            IReadOnlyList<LineSlot> lineSlots,
            ConductorScoreLaneRushIndicatorVisual indicator)
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

        private Vector2 ResolveSharedLineDirection(IReadOnlyList<Minion> minions, Vector2 target)
        {
            if (minions == null || minions.Count == 0)
            {
                return Vector2.left;
            }

            Vector2 center = Vector2.zero;
            int count = 0;
            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion == null)
                {
                    continue;
                }

                center += (Vector2)minionOrigin.GetAimOrigin(minion, 0);
                count++;
            }

            if (count <= 0)
            {
                return Vector2.left;
            }

            center /= count;
            Vector2 direction = target - center;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
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

        private Vector2 GetLaneOrigin(LineSlot slot, IReadOnlyList<LineSlot> lineSlots)
        {
            Vector2 laneCenter = GetLaneCenter(lineSlots);
            Vector2 lineAxis = GetLineAxis(slot.LineDirection);
            Vector2 idealOrigin = laneCenter + lineAxis * slot.LateralOffset;
            if (slot.Minion == null)
            {
                return idealOrigin;
            }

            Vector2 authoredOrigin = minionOrigin.GetSpawnOrigin(slot.Minion, 0, slot.LineDirection);
            float forwardOffset = Vector2.Dot(authoredOrigin - idealOrigin, slot.LineDirection);
            return idealOrigin + slot.LineDirection * forwardOffset;
        }

        private static Vector2 GetLaneCenter(IReadOnlyList<LineSlot> lineSlots)
        {
            if (lineSlots == null || lineSlots.Count == 0)
            {
                return Vector2.zero;
            }

            Vector2 sum = Vector2.zero;
            int count = 0;
            for (int i = 0; i < lineSlots.Count; i++)
            {
                Minion minion = lineSlots[i].Minion;
                if (minion == null)
                {
                    continue;
                }

                sum += (Vector2)minion.transform.position;
                count++;
            }

            return count > 0 ? sum / count : Vector2.zero;
        }

        private static Vector2 GetLineAxis(Vector2 lineDirection)
        {
            Vector2 safeDirection = lineDirection.sqrMagnitude > 0.0001f ? lineDirection.normalized : Vector2.left;
            return new Vector2(-safeDirection.y, safeDirection.x);
        }

        private sealed class OrderedParrySequence
        {
            private readonly List<Entry> entries = new();

            public OrderedParrySequence(IReadOnlyList<FireTiming> timings)
            {
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
                if (projectile is IConductorMultiStepOrderedProjectile multiStepProjectile)
                {
                    entry.RequiredStepCount = Mathf.Max(1, multiStepProjectile.RequiredSequenceSteps);
                    entry.CompletedStepCount = Mathf.Clamp(
                        multiStepProjectile.CompletedSequenceSteps,
                        0,
                        entry.RequiredStepCount);
                    entry.SequenceStepHandler = completedProjectile => HandleSequenceStepCompleted(entry, completedProjectile);
                    multiStepProjectile.SequenceStepCompleted += entry.SequenceStepHandler;
                }

                SetInterceptable(projectile, false);
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
                        return;
                    }

                    SetInterceptable(entry.Projectile, true);
                    return;
                }
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

                if (projectile is IConductorMultiStepOrderedProjectile multiStepProjectile
                    && entry.SequenceStepHandler != null)
                {
                    multiStepProjectile.SequenceStepCompleted -= entry.SequenceStepHandler;
                }

                entry.DestroyedHandler = null;
                entry.SequenceStepHandler = null;
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
                public bool Completed
                {
                    get => CompletedStepCount >= RequiredStepCount;
                    set => CompletedStepCount = value ? RequiredStepCount : 0;
                }
                public Action<EnemyProjectile, EnemyProjectileDestroyReason, Vector3> DestroyedHandler { get; set; }
                public Action<EnemyProjectile> SequenceStepHandler { get; set; }
            }
        }

        private readonly struct LineSlot
        {
            public LineSlot(Minion minion, int minionNumber, Vector2 lineDirection, float lateralOffset)
            {
                Minion = minion;
                MinionNumber = minionNumber;
                LineDirection = lineDirection;
                LateralOffset = lateralOffset;
            }

            public Minion Minion { get; }
            public int MinionNumber { get; }
            public Vector2 LineDirection { get; }
            public float LateralOffset { get; }
        }
    }

    [AddComponentMenu("")]
    internal sealed class ConductorFormationLineProjectileMotion : MonoBehaviour
    {
        private EnemyProjectile projectile;
        private Rigidbody2D body;
        private Func<Vector2> originProvider;
        private Transform launchTarget;
        private Vector2 lineDirection = Vector2.left;
        private Vector2 lastOrigin;
        private Vector2 launchStart;
        private Vector2 launchArcAxis;
        private float speed;
        private float distance;
        private float lineLength;
        private float launchTravelDistance;
        private float launchArcHeight;
        private bool hasOrigin;
        private bool launchPhase;

        public EnemyProjectile Projectile => projectile;
        public Vector2 LineDirection => lineDirection;
        public bool IsLive => projectile != null;
        public bool IsReadyForLaunchPhase => launchPhase || distance >= Mathf.Max(0f, lineLength) - 0.001f;

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

            Vector2 currentOrigin = GetCurrentOrigin();
            distance = Mathf.Max(0f, Vector2.Dot((Vector2)transform.position - currentOrigin, lineDirection));
            MoveTo(currentOrigin + lineDirection * distance, 0f, lineDirection);
        }

        public void InitializeLaunchPhase(
            Transform target,
            Vector2 fallbackDirection,
            float launchSpeed,
            float arcHeight,
            float arcSide)
        {
            projectile = GetComponent<EnemyProjectile>();
            body = GetComponent<Rigidbody2D>();
            originProvider = null;
            lineDirection = fallbackDirection.sqrMagnitude > 0.0001f
                ? fallbackDirection.normalized
                : Vector2.left;
            lineLength = 0f;
            distance = 0f;
            hasOrigin = true;
            lastOrigin = transform.position;
            BeginLaunchPhase(target, launchSpeed, arcHeight, arcSide);
        }

        public void BeginLaunchPhase(
            Transform target,
            float launchSpeed,
            float arcHeight,
            float arcSide)
        {
            if (projectile == null)
            {
                return;
            }

            launchPhase = true;
            launchTarget = target;
            launchStart = transform.position;
            launchTravelDistance = 0f;
            launchArcHeight = Mathf.Max(0f, arcHeight);
            speed = Mathf.Max(0.01f, launchSpeed);
            projectile.ConfigureExternalMotionDriven(true);
            projectile.ConfigurePlayerCollisionIgnored(false);
            SetMissedProjectileInterceptable(projectile, false);
            projectile.ForceLaunchStateForExternalMotion();

            Vector2 toTarget = GetLaunchTargetPosition() - launchStart;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                toTarget = lineDirection.sqrMagnitude > 0.0001f ? lineDirection : Vector2.left;
            }

            float side = Mathf.Approximately(arcSide, 0f) ? 1f : Mathf.Sign(arcSide);
            launchArcAxis = new Vector2(-toTarget.y, toTarget.x).normalized * side;
        }

        private void LateUpdate()
        {
            if (projectile == null || PlayerCombatController.IsExecutionCinematicActive)
            {
                StopBody();
                return;
            }

            float deltaTime = EnemyTimeScale.DeltaTime;
            if (launchPhase)
            {
                TickLaunch(deltaTime);
            }
            else
            {
                TickPrepare(deltaTime);
            }
        }

        private void TickPrepare(float deltaTime)
        {
            distance = Mathf.Min(Mathf.Max(0f, lineLength), distance + speed * deltaTime);
            MoveTo(GetCurrentOrigin() + lineDirection * distance, deltaTime, lineDirection);
        }

        private void TickLaunch(float deltaTime)
        {
            Vector2 previous = body != null ? body.position : (Vector2)transform.position;
            Vector2 target = GetLaunchTargetPosition();
            float travelLength = Mathf.Max(0.01f, Vector2.Distance(launchStart, target));
            launchTravelDistance += speed * deltaTime;
            float t = Mathf.Clamp01(launchTravelDistance / travelLength);
            Vector2 control = (launchStart + target) * 0.5f + launchArcAxis * launchArcHeight;
            Vector2 nextPosition = EvaluateQuadratic(launchStart, control, target, t);
            Vector2 visualDirection = target - nextPosition;
            if (visualDirection.sqrMagnitude <= 0.0001f)
            {
                visualDirection = nextPosition - previous;
            }

            MoveTo(nextPosition, deltaTime, visualDirection.sqrMagnitude > 0.0001f ? visualDirection.normalized : lineDirection);
        }

        private Vector2 GetCurrentOrigin()
        {
            if (originProvider == null)
            {
                return lastOrigin;
            }

            lastOrigin = originProvider.Invoke();
            hasOrigin = true;
            return lastOrigin;
        }

        private Vector2 GetLaunchTargetPosition()
        {
            if (launchTarget != null)
            {
                return launchTarget.position;
            }

            return launchStart + lineDirection * Mathf.Max(0.1f, lineLength);
        }

        private static Vector2 EvaluateQuadratic(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float inverseT = 1f - Mathf.Clamp01(t);
            return inverseT * inverseT * a
                + 2f * inverseT * t * b
                + t * t * c;
        }

        private static void SetMissedProjectileInterceptable(EnemyProjectile projectile, bool interceptable)
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

        private void MoveTo(Vector2 position, float deltaTime, Vector2 visualDirection)
        {
            Vector2 previous = hasOrigin && body != null ? body.position : (Vector2)transform.position;
            if (body != null)
            {
                body.position = position;
                body.linearVelocity = deltaTime > 0f ? (position - previous) / deltaTime : Vector2.zero;
            }

            Vector2 direction = visualDirection.sqrMagnitude > 0.0001f ? visualDirection.normalized : lineDirection;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.SetPositionAndRotation(
                new Vector3(position.x, position.y, transform.position.z),
                Quaternion.Euler(0f, 0f, angle));
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
