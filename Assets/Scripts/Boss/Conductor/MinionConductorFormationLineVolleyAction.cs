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
    public sealed class MinionConductorFormationLineVolleyAction : BossAction, IBossGraphValidatedAction
    {
        private const float DefaultFormationMoveSpeed = 24f;
        private const float FormationAlignmentTolerance = 0.08f;
        private const string CopiedVolleysSourcePatternId = "Pattern5";
        private const string CopiedVolleysTargetPatternId = "Pattern7";

        [Header("Formation Straight")]
        [SerializeField] private MinionGraphFormationStraightMode mode = MinionGraphFormationStraightMode.BetweenBossAndPlayer;
        [SerializeField, Min(0.1f)] private float distanceFromPlayer = 6f;
        [SerializeField, Min(0.1f)] private float spacing = 1f;
        [SerializeField, Min(0f)] private float speedMultiplier = 1.2f;
        [SerializeField, Min(0f)] private float bossDistanceBehindMinionLine = 2f;
        [SerializeField, Min(0f)] private float bossMoveSpeedMultiplier = 1.2f;
        [SerializeField, Min(0f)] private float settleSeconds = 1f;
        [SerializeField] private bool waitForFormationDuration = true;

        [Header("Projectile")]
        [SerializeField] private MinionGraphProjectileOriginSpec minionOrigin = new();
        [SerializeField, Min(0.01f)] private float projectileSpeedMultiplier = 1f;
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField, InspectorName("Volleys")] private List<ScoreLaneVolley> volleys = new() { new ScoreLaneVolley() };

        [Header("Line Indicator")]
        [SerializeField] private bool drawLineIndicators = true;
        [SerializeField] private Color lineIndicatorColor = new(0.62f, 0.92f, 1f, 0.72f);
        [SerializeField, Min(0.001f)] private float lineIndicatorWidth = 0.035f;
        [SerializeField, Min(0.1f)] private float lineIndicatorLength = 12f;
        [SerializeField, Min(0f)] private float lineIndicatorFadeSeconds = 0.14f;
        [SerializeField] private int lineIndicatorSortingOrder = 66;

        [Header("Miss Launch")]
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

        private readonly List<ConductorFormationLineProjectileMotion> trackedMotions = new();

        void IBossGraphValidatedAction.OnGraphValidated(BossGraphAsset graph, BossStateNode node)
        {
            CopyPatternVolleysIfTargetNode(graph, node);
        }

        public override IEnumerator Execute(BossActionContext context)
        {
            CopyPatternVolleysIfTargetNode(context?.GraphAsset, context?.CurrentNodeId);

            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host)
                || context.Boss == null
                || context.Boss.Player == null
                || volleys == null
                || volleys.Count == 0)
            {
                yield break;
            }

            List<Minion> minions = GetLineMinions(host.GetControlledMinionsForGraph());
            if (minions.Count == 0)
            {
                yield break;
            }

            Vector2 formationCenterDirection = ResolveFormationCenterDirection(context, minions);
            Vector2 lineDirection = -formationCenterDirection;
            if (lineDirection.sqrMagnitude <= 0.0001f)
            {
                lineDirection = ResolveSharedLineDirection(minions, context.Boss.Player.position);
                formationCenterDirection = -lineDirection;
            }

            float formationDuration = CommandLockedFormation(minions, formationCenterDirection);
            SetFormationFacingOverride(minions, lineDirection);
            List<LineSlot> lineSlots = BuildLineSlots(
                minions,
                lineDirection,
                formationCenterDirection,
                context.Boss.Player);

            if (waitForFormationDuration && formationDuration > 0f)
            {
                yield return WaitForFormationAlignment(context, lineSlots, formationDuration);
            }

            ConductorScoreLaneRushIndicatorVisual indicator = CreateLineIndicators(lineSlots);
            trackedMotions.Clear();

            yield return RunVolleys(context, host, lineSlots, indicator);
            yield return WaitForLineProjectilesToRelease(context, lineSlots, indicator);
            yield return RunMissLaunchPhase(context, host, lineSlots, indicator);
            yield return FadeAndClearIndicator(context, lineSlots, indicator);

            context.Boss?.Stop();
            ClearFormationFacingOverride(minions);
            trackedMotions.Clear();
        }

        private void CopyPatternVolleysIfTargetNode(BossGraphAsset graph, BossStateNode node)
        {
            if (node == null || !IsNodeInPattern(graph, node.NodeId, node.NodeGuid, CopiedVolleysTargetPatternId))
            {
                return;
            }

            CopyVolleysFromPattern(graph, CopiedVolleysSourcePatternId);
        }

        private void CopyPatternVolleysIfTargetNode(BossGraphAsset graph, string nodeId)
        {
            if (!IsNodeInPattern(graph, nodeId, null, CopiedVolleysTargetPatternId))
            {
                return;
            }

            CopyVolleysFromPattern(graph, CopiedVolleysSourcePatternId);
        }

        private void CopyVolleysFromPattern(BossGraphAsset graph, string patternId)
        {
            if (!TryFindScoreLaneRushAction(graph, patternId, out MinionConductorScoreLaneRushAction sourceAction))
            {
                return;
            }

            CopyVolleysFrom(sourceAction.SerializedVolleysForGraphCopy);
        }

        private void CopyVolleysFrom(IReadOnlyList<ScoreLaneVolley> sourceVolleys)
        {
            if (sourceVolleys == null || sourceVolleys.Count == 0 || AreVolleysEqual(sourceVolleys))
            {
                return;
            }

            volleys = new List<ScoreLaneVolley>(sourceVolleys.Count);
            for (int i = 0; i < sourceVolleys.Count; i++)
            {
                ScoreLaneVolley sourceVolley = sourceVolleys[i];
                volleys.Add(sourceVolley != null
                    ? new ScoreLaneVolley(sourceVolley.FireTimings)
                    : new ScoreLaneVolley());
            }
        }

        private bool AreVolleysEqual(IReadOnlyList<ScoreLaneVolley> sourceVolleys)
        {
            if (volleys == null || sourceVolleys == null || volleys.Count != sourceVolleys.Count)
            {
                return false;
            }

            for (int i = 0; i < sourceVolleys.Count; i++)
            {
                IReadOnlyList<ScoreLaneFireTiming> sourceTimings = sourceVolleys[i]?.FireTimings;
                IReadOnlyList<ScoreLaneFireTiming> currentTimings = volleys[i]?.FireTimings;
                int sourceCount = sourceTimings != null ? sourceTimings.Count : 0;
                int currentCount = currentTimings != null ? currentTimings.Count : 0;
                if (sourceCount != currentCount)
                {
                    return false;
                }

                for (int timingIndex = 0; timingIndex < sourceCount; timingIndex++)
                {
                    ScoreLaneFireTiming source = sourceTimings[timingIndex];
                    ScoreLaneFireTiming current = currentTimings[timingIndex];
                    if (source == null || current == null)
                    {
                        if (source != current)
                        {
                            return false;
                        }

                        continue;
                    }

                    if (source.MinionNumber != current.MinionNumber
                        || !Mathf.Approximately(source.FireSeconds, current.FireSeconds)
                        || source.ParryOrder != current.ParryOrder
                        || source.ProjectileName != current.ProjectileName)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool TryFindScoreLaneRushAction(
            BossGraphAsset graph,
            string patternId,
            out MinionConductorScoreLaneRushAction action)
        {
            action = null;
            BossGraphPattern pattern = graph != null ? graph.GetPattern(patternId) : null;
            if (pattern == null)
            {
                return false;
            }

            if (TryFindScoreLaneRushAction(graph, pattern.NodeKeys, out action))
            {
                return true;
            }

            return TryFindScoreLaneRushAction(graph, pattern.NodeIds, out action);
        }

        private static bool TryFindScoreLaneRushAction(
            BossGraphAsset graph,
            IReadOnlyList<string> nodeKeys,
            out MinionConductorScoreLaneRushAction action)
        {
            action = null;
            if (graph == null || nodeKeys == null)
            {
                return false;
            }

            for (int i = 0; i < nodeKeys.Count; i++)
            {
                BossStateNode node = graph.GetNode(nodeKeys[i]);
                if (node?.Action != null
                    && node.Action.GetType() == typeof(MinionConductorScoreLaneRushAction))
                {
                    action = (MinionConductorScoreLaneRushAction)node.Action;
                    return true;
                }
            }

            return false;
        }

        private static bool IsNodeInPattern(
            BossGraphAsset graph,
            string nodeId,
            string nodeGuid,
            string patternId)
        {
            BossGraphPattern pattern = graph != null ? graph.GetPattern(patternId) : null;
            if (pattern == null)
            {
                return false;
            }

            return ContainsNodeKey(pattern.NodeKeys, nodeId, nodeGuid)
                || ContainsNodeKey(pattern.NodeIds, nodeId, nodeGuid);
        }

        private static bool ContainsNodeKey(IReadOnlyList<string> nodeKeys, string nodeId, string nodeGuid)
        {
            if (nodeKeys == null)
            {
                return false;
            }

            for (int i = 0; i < nodeKeys.Count; i++)
            {
                string key = nodeKeys[i];
                if ((!string.IsNullOrWhiteSpace(nodeId) && key == nodeId)
                    || (!string.IsNullOrWhiteSpace(nodeGuid) && key == nodeGuid))
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerator RunVolleys(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<LineSlot> lineSlots,
            ConductorScoreLaneRushIndicatorVisual indicator)
        {
            for (int volleyIndex = 0; volleyIndex < volleys.Count; volleyIndex++)
            {
                ScoreLaneVolley volley = volleys[volleyIndex];
                if (volley == null)
                {
                    continue;
                }

                yield return RunVolley(context, host, lineSlots, indicator, volley);
            }
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

            OrderedParrySequence parrySequence = new(fireTimings);
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
                FireDueProjectiles(context, host, lineSlots, fireTimings, parrySequence, fired, elapsed);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            TickBossFormationAlignment(context, lineSlots);
            UpdateLineIndicators(indicator, lineSlots);
            FireDueProjectiles(context, host, lineSlots, fireTimings, parrySequence, fired, float.PositiveInfinity);
        }

        private void FireDueProjectiles(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<LineSlot> lineSlots,
            IReadOnlyList<ScoreLaneFireTiming> fireTimings,
            OrderedParrySequence parrySequence,
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
                    parrySequence.Skip(timing);
                    continue;
                }

                MinionGraphProjectileFireSpec fireSpec = new MinionGraphProjectileFireSpec(minionOrigin, null, effects, context)
                    .WithFixedDirection(slot.LineDirection);
                EnemyProjectile spawned = slot.Minion.FireOnce(projectile, fireSpec, i);
                if (spawned == null)
                {
                    parrySequence.Skip(timing);
                    continue;
                }

                float projectileSpeed = projectile.Speed * Mathf.Max(0.01f, projectileSpeedMultiplier);
                spawned.ConfigureSpeedMultiplier(projectileSpeedMultiplier);
                spawned.ConfigureChargeMotion(0f, false, false);
                spawned.ConfigurePathIndicatorDelayedUntilLaunch(true);
                spawned.ConfigureExternalMotionDriven(true);
                spawned.ConfigurePlayerCollisionIgnored(true);
                spawned.HoldChargeUntilForcedLaunch();

                ConductorFormationLineProjectileMotion motion = spawned.gameObject.AddComponent<ConductorFormationLineProjectileMotion>();
                motion.Initialize(
                    () => GetLaneOrigin(slot, lineSlots),
                    slot.LineDirection,
                    projectileSpeed,
                    lineIndicatorLength);

                parrySequence.Register(timing, spawned);
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
            List<ConductorFormationLineProjectileMotion> missedMotions = GetReadyMissMotions();
            if (missedMotions.Count == 0)
            {
                yield break;
            }

            yield return RunFinalReleaseFormation(context, lineSlots, indicator);
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
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }
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
                baseDirection = lineSlots[0].FormationCenterDirection;
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

        private List<LineSlot> BuildLineSlots(
            IReadOnlyList<Minion> minions,
            Vector2 lineDirection,
            Vector2 formationCenterDirection,
            Transform player)
        {
            List<LineSlot> slots = new();
            Vector2 safeLineDirection = lineDirection.sqrMagnitude > 0.0001f ? lineDirection.normalized : Vector2.left;
            Vector2 safeCenterDirection = formationCenterDirection.sqrMagnitude > 0.0001f
                ? formationCenterDirection.normalized
                : -safeLineDirection;

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
                    safeCenterDirection,
                    player,
                    distanceFromPlayer,
                    GetCenteredOffset(i, minions.Count, spacing)));
            }

            return slots;
        }

        private float CommandLockedFormation(IReadOnlyList<Minion> minions, Vector2 formationCenterDirection)
        {
            if (minions == null)
            {
                return 0f;
            }

            float moveSpeed = DefaultFormationMoveSpeed * Mathf.Max(0f, speedMultiplier);
            bool hasSharedCenterDirection = formationCenterDirection.sqrMagnitude > 0.0001f;
            Vector2 safeCenterDirection = hasSharedCenterDirection
                ? formationCenterDirection.normalized
                : Vector2.zero;

            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion == null)
                {
                    continue;
                }

                float lateralOffset = GetCenteredOffset(i, minions.Count, spacing);
                if (hasSharedCenterDirection)
                {
                    minion.CommandFormationStraightLockedToPlayerOffset(
                        -lateralOffset,
                        distanceFromPlayer,
                        safeCenterDirection,
                        moveSpeed);
                }
                else
                {
                    minion.CommandFormationStraightLockedToPlayerOffset(
                        lateralOffset,
                        distanceFromPlayer,
                        mode,
                        moveSpeed);
                }
            }

            return Mathf.Max(0f, settleSeconds);
        }

        private IEnumerator WaitForFormationAlignment(
            BossActionContext context,
            IReadOnlyList<LineSlot> lineSlots,
            float maxSeconds)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0f, maxSeconds);
            while (elapsed < duration)
            {
                if (context.IsExecutionPaused)
                {
                    context.Boss?.Stop();
                    yield return null;
                    continue;
                }

                TickBossFormationAlignment(context, lineSlots);
                if (AreLineMinionsAligned(lineSlots) && IsBossFormationAligned(context, lineSlots))
                {
                    yield break;
                }

                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }
        }

        private bool AreLineMinionsAligned(IReadOnlyList<LineSlot> lineSlots)
        {
            if (lineSlots == null || lineSlots.Count == 0)
            {
                return true;
            }

            float toleranceSqr = FormationAlignmentTolerance * FormationAlignmentTolerance;
            for (int i = 0; i < lineSlots.Count; i++)
            {
                LineSlot slot = lineSlots[i];
                if (slot.Minion == null)
                {
                    continue;
                }

                Vector2 current = slot.Minion.transform.position;
                if ((current - GetLaneMinionTarget(slot, lineSlots)).sqrMagnitude > toleranceSqr)
                {
                    return false;
                }
            }

            return true;
        }

        private void TickBossFormationAlignment(BossActionContext context, IReadOnlyList<LineSlot> lineSlots)
        {
            if (context?.Boss == null || context.Boss.Body == null || lineSlots == null || lineSlots.Count == 0)
            {
                return;
            }

            Vector2 target = GetBossFormationTarget(lineSlots);
            Vector2 current = context.Boss.Body.position;
            Vector2 toTarget = target - current;
            float toleranceSqr = FormationAlignmentTolerance * FormationAlignmentTolerance;
            if (toTarget.sqrMagnitude <= toleranceSqr)
            {
                context.Boss.Stop();
                return;
            }

            float speed = context.Boss.MoveSpeed * Mathf.Max(0f, bossMoveSpeedMultiplier);
            context.Boss.SetMovementVelocity(toTarget.normalized * speed);
        }

        private bool IsBossFormationAligned(BossActionContext context, IReadOnlyList<LineSlot> lineSlots)
        {
            if (context?.Boss == null || context.Boss.Body == null || lineSlots == null || lineSlots.Count == 0)
            {
                return true;
            }

            float toleranceSqr = FormationAlignmentTolerance * FormationAlignmentTolerance;
            return ((Vector2)context.Boss.Body.position - GetBossFormationTarget(lineSlots)).sqrMagnitude <= toleranceSqr;
        }

        private Vector2 GetBossFormationTarget(IReadOnlyList<LineSlot> lineSlots)
        {
            if (lineSlots == null || lineSlots.Count == 0)
            {
                return Vector2.zero;
            }

            LineSlot referenceSlot = lineSlots[0];
            Vector2 centerDirection = referenceSlot.FormationCenterDirection.sqrMagnitude > 0.0001f
                ? referenceSlot.FormationCenterDirection.normalized
                : -referenceSlot.LineDirection;
            return GetLaneCenter(lineSlots) + centerDirection * Mathf.Max(0f, bossDistanceBehindMinionLine);
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

        private IEnumerator WaitForLineProjectilesToRelease(
            BossActionContext context,
            IReadOnlyList<LineSlot> lineSlots,
            ConductorScoreLaneRushIndicatorVisual indicator)
        {
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
                yield return null;
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

        private Vector2 ResolveFormationCenterDirection(BossActionContext context, IReadOnlyList<Minion> minions)
        {
            Transform player = context?.Boss?.Player;
            if (player == null)
            {
                return Vector2.right;
            }

            if (context.Boss != null)
            {
                Vector2 bossToPlayer = (Vector2)player.position - (Vector2)context.Boss.transform.position;
                if (bossToPlayer.sqrMagnitude > 0.0001f)
                {
                    return -bossToPlayer.normalized;
                }
            }

            Vector2 minionCenter = GetMinionAimCenter(minions);
            Vector2 playerToMinions = minionCenter - (Vector2)player.position;
            if (playerToMinions.sqrMagnitude > 0.0001f)
            {
                return playerToMinions.normalized;
            }

            return Vector2.right;
        }

        private Vector2 ResolveSharedLineDirection(IReadOnlyList<Minion> minions, Vector2 target)
        {
            if (minions == null || minions.Count == 0)
            {
                return Vector2.left;
            }

            Vector2 center = GetMinionAimCenter(minions);
            Vector2 direction = target - center;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
        }

        private Vector2 GetMinionAimCenter(IReadOnlyList<Minion> minions)
        {
            if (minions == null || minions.Count == 0)
            {
                return Vector2.zero;
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

            return count > 0 ? center / count : Vector2.zero;
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
            Vector2 idealOrigin = GetLaneMinionTarget(slot, lineSlots);
            if (slot.Minion == null)
            {
                return idealOrigin;
            }

            Vector2 authoredOrigin = minionOrigin.GetSpawnOrigin(slot.Minion, 0, slot.LineDirection);
            float forwardOffset = Vector2.Dot(authoredOrigin - idealOrigin, slot.LineDirection);
            return idealOrigin + slot.LineDirection * forwardOffset;
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

            LineSlot referenceSlot = lineSlots[0];
            if (referenceSlot.Player != null && referenceSlot.FormationCenterDirection.sqrMagnitude > 0.0001f)
            {
                return (Vector2)referenceSlot.Player.position
                    + referenceSlot.FormationCenterDirection.normalized * Mathf.Max(0.1f, referenceSlot.DistanceFromPlayer);
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

        private sealed class OrderedParrySequence
        {
            private readonly List<Entry> entries = new();

            public OrderedParrySequence(IReadOnlyList<ScoreLaneFireTiming> timings)
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

            public void Register(ScoreLaneFireTiming timing, EnemyProjectile projectile)
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

            public void Skip(ScoreLaneFireTiming timing)
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

            private Entry FindEntry(ScoreLaneFireTiming timing)
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
                SetSequenceActive(projectile, interceptable);
                if (!interceptable)
                {
                    projectile?.SetParryLockOnIndicatorVisible(false);
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
                public Entry(ScoreLaneFireTiming timing, int sourceIndex)
                {
                    Timing = timing;
                    SourceIndex = sourceIndex;
                    RequiredStepCount = 1;
                }

                public ScoreLaneFireTiming Timing { get; }
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
            public LineSlot(
                Minion minion,
                int minionNumber,
                Vector2 lineDirection,
                Vector2 formationCenterDirection,
                Transform player,
                float distanceFromPlayer,
                float lateralOffset)
            {
                Minion = minion;
                MinionNumber = minionNumber;
                LineDirection = lineDirection;
                FormationCenterDirection = formationCenterDirection;
                Player = player;
                DistanceFromPlayer = distanceFromPlayer;
                LateralOffset = lateralOffset;
            }

            public Minion Minion { get; }
            public int MinionNumber { get; }
            public Vector2 LineDirection { get; }
            public Vector2 FormationCenterDirection { get; }
            public Transform Player { get; }
            public float DistanceFromPlayer { get; }
            public float LateralOffset { get; }
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
        private Vector2 lineDirection = Vector2.left;
        private Vector2 lastOrigin;
        private float speed;
        private float distance;
        private float lineLength;
        private bool hasOrigin;
        private bool readyForMissLaunch;

        public EnemyProjectile Projectile => projectile;
        public Vector2 LineDirection => lineDirection;
        public bool IsLive => projectile != null;
        public bool IsBoundToLine => projectile != null && !readyForMissLaunch;
        public bool IsReadyForMissLaunch => projectile != null && readyForMissLaunch;

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
            distance = Mathf.Min(Mathf.Max(0f, lineLength), distance + speed * deltaTime);
            MoveTo(GetCurrentOrigin() + lineDirection * distance, deltaTime, lineDirection);

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
