using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Week14.Enemy
{
    public sealed class BossGraphRunner
    {
        private const int MaxImmediateTransitionsPerFrame = 16;
        private readonly Dictionary<string, int> nextSequenceIndexes = new();
        private readonly Dictionary<string, BossGraphActionAsset> lastSequences = new();
        private readonly Dictionary<string, List<BossSequenceEntry>> sequenceBags = new();
        private readonly Dictionary<string, int> patternCooldownRemainingCounts = new();
        private readonly HashSet<int> openingPatternsPlayed = new();
        private readonly HashSet<int> signaturePatternsPlayed = new();
        // 페이즈별로 InitialPatternDelaySeconds 대기를 이미 적용했는지 추적한다. 쿨다운/Min Patterns
        // Played와 달리 "이 페이즈에서 있었던 일" 히스토리가 아니라 "패턴 루프가 막 (재)시작됐다"는
        // 상태이므로, 그로기 종료/은신 전환처럼 코루틴이 다시 시작될 때마다(ResetTraversalState) 매번
        // 같이 초기화된다 — 그래야 상태 전환 뒤 첫 패턴 앞에 항상 시작 딜레이가 다시 걸린다.
        private readonly HashSet<int> initialPatternDelayApplied = new();
        // 페이즈별로 지금까지 몇 개의 패턴이 완료됐는지 누적한다. Min Patterns Played 조건(최초 등장을
        // 늦추는 절대 조건) 판정에 쓰인다 — cooldownPatternCount(반복 억제)와 달리 페이즈가 바뀌면
        // Reset()에서 같이 초기화된다.
        private readonly Dictionary<int, int> patternsPlayedCountByPhase = new();
        private string currentNodeId;
        private string previousRuntimeNodeId;
        private BossGraphAsset activeGraph;
        // 지금 RunPhasePatternLoop가 실행 중인 패턴. StopCoroutine으로 도중에 캔슬돼도 카운트/쿨다운이
        // 반영되도록, GraphBossAI.StopGraphPattern이 코루틴을 끊기 직전에 RegisterInFlightPatternIfNeeded()
        // 를 직접 호출한다(try/finally + Dispose 전파에 기대지 않는다).
        private BossGraphPhase inFlightPhase;
        private BossGraphPatternEntry inFlightPatternEntry;
        private bool inFlightRegistered = true;

        // 진짜 새 시작(페이즈 전환 등)에만 쓴다. 쿨다운/Min Patterns Played처럼 "이 페이즈에서
        // 지금까지 있었던 일"을 나타내는 페이즈별 히스토리까지 전부 지운다.
        public void Reset()
        {
            ResetTraversalState();
            patternCooldownRemainingCounts.Clear();
            openingPatternsPlayed.Clear();
            signaturePatternsPlayed.Clear();
            patternsPlayedCountByPhase.Clear();
        }

        // 그로기 등으로 패턴 루프 코루틴이 중간에 끊겼다가 같은 페이즈에서 다시 시작될 때 쓴다.
        // 그래프 순회 위치(현재 노드, 시퀀스 진행도)만 지우고, 쿨다운/Min Patterns Played 같은
        // 페이즈별 히스토리는 보존한다 — 안 그러면 그로기가 걸릴 때마다 Min Patterns Played 조건이
        // 0부터 다시 카운트되어, 그로기를 유발하는 패턴이 있는 페이즈에서는 Min이 걸린 패턴이
        // 사실상 영영 등장하지 못하는 문제가 생긴다.
        public void RestartAfterInterruption()
        {
            ResetTraversalState();
        }

        private void ResetTraversalState()
        {
            BossGraphRuntimeState.Clear(activeGraph);
            nextSequenceIndexes.Clear();
            lastSequences.Clear();
            sequenceBags.Clear();
            initialPatternDelayApplied.Clear();
            currentNodeId = null;
            previousRuntimeNodeId = null;
        }

        public IEnumerator RunLoop(BossGraphAsset graph, BossActionContext context)
        {
            if (graph == null || context == null)
            {
                yield break;
            }

            activeGraph = graph;
            try
            {
                if (graph.DebugForceSinglePattern && graph.GetPattern(graph.DebugForcedPatternId) != null)
                {
                    yield return RunForcedPatternLoop(graph, context);
                    yield break;
                }

                if (graph.UsesPhasePatternLayout)
                {
                    yield return RunPhasePatternLoop(graph, context);
                    yield break;
                }

                yield return RunLegacyNodeLoop(graph, context);
            }
            finally
            {
                BossGraphRuntimeState.Clear(graph);
                if (activeGraph == graph)
                {
                    activeGraph = null;
                }
            }
        }

        // 개발용 패턴 패널에서 기존 실행 규칙을 그대로 사용해 지정 패턴만 한 번 실행한다.
        public IEnumerator RunPatternOnce(
            BossGraphAsset graph,
            BossGraphPattern pattern,
            BossActionContext context)
        {
            if (graph == null || pattern == null || context == null)
            {
                yield break;
            }

            activeGraph = graph;
            ResetTraversalState();
            try
            {
                yield return ExecutePattern(graph, pattern, context);
                context.Stop();
            }
            finally
            {
                BossGraphRuntimeState.Clear(graph);
                if (activeGraph == graph)
                {
                    activeGraph = null;
                }
            }
        }

        private IEnumerator RunLegacyNodeLoop(BossGraphAsset graph, BossActionContext context)
        {
            int immediateTransitionCount = 0;
            while (true)
            {
                BossStateNode node = ResolveCurrentNode(graph, context);
                if (TryApplyTransition(graph, context, node, false))
                {
                    immediateTransitionCount++;
                    if (immediateTransitionCount >= MaxImmediateTransitionsPerFrame)
                    {
                        immediateTransitionCount = 0;
                        context.Stop();
                        yield return null;
                    }

                    continue;
                }

                immediateTransitionCount = 0;
                BossAction directAction = node?.HasDirectAction == true ? node.Action : null;
                BossSequenceEntry entry = directAction == null ? SelectSequence(node) : null;
                BossGraphActionAsset sequence = entry?.Sequence;
                if (directAction == null && sequence == null)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                try
                {
                    BossGraphRuntimeState.SetCurrentNode(graph, node.NodeId, previousRuntimeNodeId);
                    previousRuntimeNodeId = node.NodeId;
                    context.BeginNodeExecution(node.NodeId);
                    if (directAction != null)
                    {
                        yield return directAction.Execute(context);
                    }
                    else
                    {
                        yield return sequence.Execute(context);
                    }

                    context.Stop();
                }
                finally
                {
                    context.EndNodeExecution();
                    context.ClearPatternScopedBossChildAims();
                }

                TryApplyTransition(graph, context, node, true);
            }
        }

        private IEnumerator RunForcedPatternLoop(BossGraphAsset graph, BossActionContext context)
        {
            while (true)
            {
                BossGraphPattern pattern = graph.GetPattern(graph.DebugForcedPatternId);
                IReadOnlyList<string> nodeKeys = pattern?.NodeKeys;
                if (nodeKeys == null || nodeKeys.Count == 0)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                yield return ExecutePattern(graph, pattern, context);
                context.Stop();

                BossGraphPhase phase = graph.GetPhase(context.Boss.CurrentPhaseIndex);
                yield return WaitBetweenPatterns(phase, context);
            }
        }

        // 패턴과 패턴 사이 대기(PatternIntervalSeconds)를 처리한다. IntervalMoveSpeed가 0보다 크면
        // 가만히 서서 기다리는 대신, 대기 시작 시점에 뽑은 랜덤한 한 방향으로 그 속도만큼 천천히
        // 이동하며 기다린다(대기 도중 방향은 바뀌지 않는다). BossAI.SetMovementVelocity가 내부에서
        // EnemyTimeScale.Current를 곱하므로 시간 슬로우도 자동으로 반영된다.
        private static IEnumerator WaitBetweenPatterns(BossGraphPhase phase, BossActionContext context)
        {
            if (phase == null || phase.PatternIntervalSeconds <= 0f)
            {
                yield break;
            }

            if (phase.IntervalMoveSpeed > 0f && context.Boss != null)
            {
                Vector2 direction = Random.insideUnitCircle;
                direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;

                float remaining = phase.PatternIntervalSeconds;
                while (remaining > 0f)
                {
                    if (context.IsExecutionPaused)
                    {
                        context.Stop();
                        yield return null;
                        continue;
                    }

                    context.UpdateBossChildAims();
                    context.Boss.SetMovementVelocity(direction * phase.IntervalMoveSpeed);
                    remaining -= EnemyTimeScale.DeltaTime;
                    yield return null;
                }

                context.Stop();
            }
            else
            {
                yield return context.WaitSeconds(phase.PatternIntervalSeconds);
                context.Stop();
            }
        }

        private IEnumerator RunPhasePatternLoop(BossGraphAsset graph, BossActionContext context)
        {
            while (true)
            {
                BossGraphPhase phase = graph.GetPhase(context.Boss.CurrentPhaseIndex);
                if (phase != null && phase.InitialPatternDelaySeconds > 0f && initialPatternDelayApplied.Add(phase.PhaseIndex))
                {
                    yield return context.WaitSeconds(phase.InitialPatternDelaySeconds);
                    context.Stop();
                }

                BossGraphPattern pattern = ResolvePhasePattern(graph, phase, context, out BossGraphPatternEntry patternEntry);
                IReadOnlyList<string> nodeKeys = pattern?.NodeKeys;
                if (nodeKeys == null || nodeKeys.Count == 0)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                // try/finally + StopCoroutine의 Dispose 전파에 기대지 않는다 — 이만큼 깊이 중첩된
                // yield return 체인(RunGraphPatternLoop→RunLoop→RunPhasePatternLoop→ExecutePattern→...)
                // 에서는 StopCoroutine이 finally를 안정적으로 안 돌려준다는 게 실측으로 확인됐다. 대신
                // "지금 이 패턴이 실행 중"이라는 걸 필드에 기록해두고, 정상 완료 시엔 바로 아래에서,
                // 그로기 등으로 캔슬될 때는 StopGraphPattern이 코루틴을 끊기 직전에 직접
                // RegisterInFlightPatternIfNeeded()를 호출해 등록한다.
                inFlightPhase = phase;
                inFlightPatternEntry = patternEntry;
                inFlightRegistered = false;

                yield return ExecutePattern(graph, pattern, context);
                RegisterInFlightPatternIfNeeded();
                context.Stop();
                if (TryGetPendingSignaturePattern(graph, phase, context, out BossGraphPattern signaturePattern))
                {
                    signaturePatternsPlayed.Add(phase.PhaseIndex);
                    yield return ExecutePattern(graph, signaturePattern, context);
                    context.Stop();
                }

                yield return WaitBetweenPatterns(phase, context);
            }
        }

        private static IEnumerator ExecutePattern(
            BossGraphAsset graph,
            BossGraphPattern pattern,
            BossActionContext context)
        {
            context?.ClearPatternTerminationRequest();
            try
            {
                if (TryBuildHologramReplayPlan(graph, pattern, out HackerHologramReplayPlan hologramPlan))
                {
                    yield return ExecuteHologramReplayPattern(graph, hologramPlan, context);
                    yield break;
                }

                yield return ExecutePatternNodes(graph, pattern?.NodeKeys, context, true);
            }
            finally
            {
                if (context?.Boss is HackerBossAI hacker
                    && context.Boss is not HackerHologramBoss)
                {
                    hacker.ClearPatternSpawnedWeapons();
                }

                context?.ClearPatternTerminationRequest();
            }
        }

        private static IEnumerator ExecuteHologramReplayPattern(
            BossGraphAsset graph,
            HackerHologramReplayPlan plan,
            BossActionContext context,
            bool executePrelude = true)
        {
            if (executePrelude && plan.PreludeNodeKeys.Count > 0)
            {
                yield return ExecutePatternNodes(
                    graph,
                    plan.PreludeNodeKeys,
                    context,
                    true);
            }

            // Hologram Replay가 Fire Wire 분기 아래에 있을 때는, 선행 Fire Wire의 결과로
            // 선택되지 않은 경로를 먼저 제외해야 한다. 그렇지 않으면 Replay 경로가 항상 실행된다.
            if (executePrelude
                && TryGetHologramReplayBranchSelection(
                    graph,
                    plan,
                    context,
                    out bool isReplayBranchSelected,
                    out string replayBranchRootNodeKey)
                && !isReplayBranchSelected)
            {
                if (TryBuildSelectedHologramReplayPlan(
                        graph,
                        plan.AllPatternNodeKeys,
                        context,
                        out HackerHologramReplayPlan selectedPlan))
                {
                    yield return ExecuteHologramReplayPattern(
                        graph,
                        selectedPlan,
                        context,
                        false);
                    yield break;
                }

                HashSet<string> skippedNodeKeys = GetRuntimeNodeKeySet(graph, plan.PreludeNodeKeys);
                SkipHackerFireWireBranchPath(graph, replayBranchRootNodeKey, skippedNodeKeys);
                yield return ExecutePatternNodes(
                    graph,
                    plan.AllPatternNodeKeys,
                    context,
                    true,
                    null,
                    null,
                    skippedNodeKeys);
                yield break;
            }

            yield return ExecuteSinglePatternNode(
                graph,
                plan.ReplayNode,
                null,
                context,
                true);

            if (context?.Boss is not HackerBossAI hacker
                || !hacker.TryGetHologram(out HackerHologramBoss hologram))
            {
                yield return ExecutePatternNodes(
                    graph,
                    plan.PatternNodeKeys,
                    context,
                    true,
                    plan.ReplayNode.NodeId);
                yield break;
            }

            float hologramStartDelaySeconds = plan.ReplayNode.Action is HackerHologramReplayAction replayAction
                ? replayAction.HologramStartDelaySeconds
                : 0.01f;
            BossActionContext hologramContext = new(
                hologram,
                hologram.Stop,
                () => BossAI.IsExecutionPausedForState,
                graph);
            Dictionary<string, BossAction> hologramActions = ClonePatternActions(
                graph,
                plan.PatternNodeKeys);
            // 3페이즈 진입 연출 중에는 리플레이가 시작되어 연출을 취소하지 않도록 한다.
            yield return hologram.WaitForSummonEntrance();
            hologram.BeginRecordedReplay(hologramStartDelaySeconds);
            try
            {
                List<IEnumerator> routines = new()
                {
                    ExecuteRecordedBodyPattern(
                        graph,
                        plan.PatternNodeKeys,
                        context,
                        hologram,
                        plan.ReplayNode.NodeId),
                    ExecuteDelayedHologramActions(
                        graph,
                        plan.PatternNodeKeys,
                        hologramContext,
                        plan.ReplayNode.NodeId,
                        hologramStartDelaySeconds,
                        hologramActions)
                };
                yield return RunParallelRoutines(routines);

                if (hologram != null)
                {
                    yield return hologram.WaitForRecordedReplayCompletion();
                    yield return hologram.ReturnToOwner();
                }
            }
            finally
            {
                if (hologram != null)
                {
                    hologram.CancelRecordedReplay();
                }
            }
        }

        private static IEnumerator ExecuteRecordedBodyPattern(
            BossGraphAsset graph,
            IReadOnlyList<string> nodeKeys,
            BossActionContext context,
            HackerHologramBoss hologram,
            string initialPreviousNodeId)
        {
            try
            {
                yield return ExecutePatternNodes(
                    graph,
                    nodeKeys,
                    context,
                    true,
                    initialPreviousNodeId);
            }
            finally
            {
                if (hologram != null)
                {
                    hologram.EndRecordedReplayCapture();
                }
            }
        }

        private static IEnumerator ExecuteDelayedHologramActions(
            BossGraphAsset graph,
            IReadOnlyList<string> nodeKeys,
            BossActionContext context,
            string initialPreviousNodeId,
            float delaySeconds,
            IReadOnlyDictionary<string, BossAction> actionOverrides)
        {
            yield return context.WaitSeconds(Mathf.Max(0.01f, delaySeconds));
            if (context?.Boss is not HackerHologramBoss hologram)
            {
                yield return ExecutePatternNodes(
                    graph,
                    nodeKeys,
                    context,
                    false,
                    initialPreviousNodeId,
                    actionOverrides);
                yield break;
            }

            List<CoroutineStack> actionStacks = new();
            try
            {
                while (hologram.IsRecordingReplayActions
                    || hologram.HasPendingReplayActionGroups
                    || actionStacks.Count > 0)
                {
                    while (hologram.TryDequeueReplayActionGroup(out IReadOnlyList<string> recordedNodeIds))
                    {
                        List<BossStateNode> recordedGroup = ResolveRecordedHologramActionGroup(
                            graph,
                            recordedNodeIds);
                        if (recordedGroup.Count > 0)
                        {
                            actionStacks.Add(new CoroutineStack(ExecutePatternNodeGroup(
                                graph,
                                recordedGroup,
                                null,
                                context,
                                false,
                                actionOverrides)));
                        }
                    }

                    for (int i = actionStacks.Count - 1; i >= 0; i--)
                    {
                        if (!actionStacks[i].MoveNext())
                        {
                            actionStacks.RemoveAt(i);
                        }
                    }

                    if (hologram.IsRecordingReplayActions
                        || hologram.HasPendingReplayActionGroups
                        || actionStacks.Count > 0)
                    {
                        yield return null;
                    }
                }
            }
            finally
            {
                ClearConductorMinionOutlineHolds(context);
                context.ClearPatternScopedBossChildAims();
            }
        }

        private static List<BossStateNode> ResolveRecordedHologramActionGroup(
            BossGraphAsset graph,
            IReadOnlyList<string> nodeIds)
        {
            List<BossStateNode> nodes = new();
            if (graph == null || nodeIds == null)
            {
                return nodes;
            }

            for (int i = 0; i < nodeIds.Count; i++)
            {
                BossStateNode node = graph.GetNode(nodeIds[i]);
                if (node?.Action != null)
                {
                    nodes.Add(node);
                }
            }

            return nodes;
        }

        private static IEnumerator ExecutePatternNodes(
            BossGraphAsset graph,
            IReadOnlyList<string> nodeKeys,
            BossActionContext context,
            bool updateRuntimeState,
            string initialPreviousNodeId = null,
            IReadOnlyDictionary<string, BossAction> actionOverrides = null,
            ISet<string> initialSkippedNodeKeys = null)
        {
            if (graph == null || nodeKeys == null || context == null)
            {
                yield break;
            }

            try
            {
                Dictionary<string, List<BossStateNode>> parallelGroups = BuildPatternParallelGroups(graph, nodeKeys);
                List<List<BossStateNode>> executionGroups = BuildPatternExecutionGroups(graph, nodeKeys, parallelGroups);
                int[] conductorOutlineReleaseCounts = new int[executionGroups.Count];
                bool conductorPatternCompleteEffectPlayed = false;
                HashSet<string> skippedNodeKeys = initialSkippedNodeKeys != null
                    ? new HashSet<string>(initialSkippedNodeKeys, StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal);
                string previousNodeId = initialPreviousNodeId;
                for (int i = 0; i < executionGroups.Count; i++)
                {
                    if (context.IsPatternTerminationRequested)
                    {
                        yield break;
                    }

                    List<BossStateNode> group = executionGroups[i];
                    List<BossStateNode> activeGroup = group
                        .Where(node => node != null && !skippedNodeKeys.Contains(GetRuntimeNodeKey(node)))
                        .ToList();
                    if (activeGroup.Count > 0)
                    {
                        Action<BossStateNode> onNodeCompleted = null;
                        if (!conductorPatternCompleteEffectPlayed
                            && context.Boss is Conductor conductor
                            && ContainsProjectileEmissionAction(activeGroup)
                            && !ContainsFutureProjectileEmissionAction(
                                executionGroups,
                                i + 1,
                                skippedNodeKeys))
                        {
                            int remainingProjectileEmissionActionCount =
                                CountProjectileEmissionActions(activeGroup);
                            onNodeCompleted = completedNode =>
                            {
                                if (completedNode?.Action is not IBossProjectileEmissionAction)
                                {
                                    return;
                                }

                                remainingProjectileEmissionActionCount--;
                                if (remainingProjectileEmissionActionCount <= 0
                                    && !conductorPatternCompleteEffectPlayed
                                    && !context.IsPatternTerminationRequested)
                                {
                                    conductor.PlayPatternCompleteEffect();
                                    conductorPatternCompleteEffectPlayed = true;
                                }
                            };
                        }

                        RecordHologramReplayActionGroup(context, activeGroup);
                        yield return ExecutePatternNodeGroup(
                            graph,
                            activeGroup,
                            previousNodeId,
                            context,
                            updateRuntimeState,
                            actionOverrides,
                            onNodeCompleted);
                        ApplyHackerFireWireBranchSelection(graph, activeGroup, context, skippedNodeKeys);

                        if (context.IsPatternTerminationRequested)
                        {
                            yield break;
                        }
                    }

                    int newOutlineHoldCount = context.ConsumeConductorMinionOutlineHoldRequests();
                    if (newOutlineHoldCount > 0 && conductorOutlineReleaseCounts.Length > 0)
                    {
                        int releaseGroupIndex = Mathf.Min(i + 1, conductorOutlineReleaseCounts.Length - 1);
                        conductorOutlineReleaseCounts[releaseGroupIndex] += newOutlineHoldCount;
                    }

                    ReleaseConductorMinionOutlineHolds(context, conductorOutlineReleaseCounts[i]);
                    previousNodeId = activeGroup.Count > 0 ? activeGroup[activeGroup.Count - 1].NodeId : previousNodeId;
                    context.Stop();
                }
            }
            finally
            {
                ClearConductorMinionOutlineHolds(context);
                context.ClearPatternScopedBossChildAims();
            }
        }

        private static bool ContainsProjectileEmissionAction(IReadOnlyList<BossStateNode> nodes)
        {
            if (nodes == null)
            {
                return false;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i]?.Action is IBossProjectileEmissionAction)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountProjectileEmissionActions(IReadOnlyList<BossStateNode> nodes)
        {
            if (nodes == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i]?.Action is IBossProjectileEmissionAction)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool ContainsFutureProjectileEmissionAction(
            IReadOnlyList<List<BossStateNode>> executionGroups,
            int startIndex,
            ISet<string> skippedNodeKeys)
        {
            if (executionGroups == null)
            {
                return false;
            }

            for (int groupIndex = Mathf.Max(0, startIndex); groupIndex < executionGroups.Count; groupIndex++)
            {
                List<BossStateNode> group = executionGroups[groupIndex];
                for (int nodeIndex = 0; nodeIndex < group.Count; nodeIndex++)
                {
                    BossStateNode node = group[nodeIndex];
                    if (node?.Action is IBossProjectileEmissionAction
                        && (skippedNodeKeys == null
                            || !skippedNodeKeys.Contains(GetRuntimeNodeKey(node))))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void RecordHologramReplayActionGroup(
            BossActionContext context,
            IReadOnlyList<BossStateNode> nodes)
        {
            if (context?.Boss is HackerBossAI hacker
                && hacker.TryGetHologram(out HackerHologramBoss hologram))
            {
                hologram.RecordReplayActionGroup(nodes);
            }
        }

        private static bool TryBuildHologramReplayPlan(
            BossGraphAsset graph,
            BossGraphPattern pattern,
            out HackerHologramReplayPlan plan)
        {
            plan = default;
            if (graph == null || pattern?.NodeKeys == null)
            {
                return false;
            }

            Dictionary<string, BossStateNode> patternNodes = new(StringComparer.Ordinal);
            BossStateNode replayNode = null;
            for (int i = 0; i < pattern.NodeKeys.Count; i++)
            {
                BossStateNode node = graph.GetNode(pattern.NodeKeys[i]);
                string nodeKey = GetRuntimeNodeKey(node);
                if (node == null || string.IsNullOrWhiteSpace(nodeKey))
                {
                    continue;
                }

                patternNodes[nodeKey] = node;
                if (replayNode == null && node.Action is HackerHologramReplayAction)
                {
                    replayNode = node;
                }
            }

            if (replayNode == null)
            {
                return false;
            }

            return TryCreateHologramReplayPlan(
                graph,
                pattern.NodeKeys,
                patternNodes,
                replayNode,
                out plan);
        }

        private static bool TryBuildSelectedHologramReplayPlan(
            BossGraphAsset graph,
            IReadOnlyList<string> patternNodeKeys,
            BossActionContext context,
            out HackerHologramReplayPlan selectedPlan)
        {
            selectedPlan = default;
            if (graph == null || patternNodeKeys == null)
            {
                return false;
            }

            Dictionary<string, BossStateNode> patternNodes = BuildPatternNodeLookup(graph, patternNodeKeys);
            for (int i = 0; i < patternNodeKeys.Count; i++)
            {
                BossStateNode replayNode = graph.GetNode(patternNodeKeys[i]);
                if (replayNode?.Action is not HackerHologramReplayAction
                    || !TryCreateHologramReplayPlan(
                        graph,
                        patternNodeKeys,
                        patternNodes,
                        replayNode,
                        out HackerHologramReplayPlan candidatePlan)
                    || !TryGetHologramReplayBranchSelection(
                        graph,
                        candidatePlan,
                        context,
                        out bool isReplayBranchSelected,
                        out _)
                    || !isReplayBranchSelected)
                {
                    continue;
                }

                selectedPlan = candidatePlan;
                return true;
            }

            return false;
        }

        private static bool TryCreateHologramReplayPlan(
            BossGraphAsset graph,
            IReadOnlyList<string> patternNodeKeys,
            IReadOnlyDictionary<string, BossStateNode> patternNodes,
            BossStateNode replayNode,
            out HackerHologramReplayPlan plan)
        {
            plan = default;
            if (graph == null
                || patternNodeKeys == null
                || patternNodes == null
                || replayNode?.Action is not HackerHologramReplayAction)
            {
                return false;
            }

            string replayNodeKey = GetRuntimeNodeKey(replayNode);
            string patternStartKey = GetTransitionTargetKey(graph, replayNode, 0);
            if (string.IsNullOrWhiteSpace(replayNodeKey)
                || string.IsNullOrWhiteSpace(patternStartKey))
            {
                return false;
            }

            HashSet<string> replayPatternNodeKeys = CollectHologramReplayPath(
                graph,
                patternNodes,
                replayNodeKey,
                patternStartKey);
            if (replayPatternNodeKeys.Count == 0)
            {
                return false;
            }

            HashSet<string> preludeNodeKeys = CollectHologramReplayPrelude(
                graph,
                patternNodes,
                replayNodeKey);
            plan = new HackerHologramReplayPlan(
                replayNode,
                GetOrderedPatternNodeKeys(patternNodeKeys, graph, preludeNodeKeys),
                GetOrderedPatternNodeKeys(patternNodeKeys, graph, replayPatternNodeKeys),
                patternNodeKeys);
            return true;
        }

        private static Dictionary<string, BossStateNode> BuildPatternNodeLookup(
            BossGraphAsset graph,
            IReadOnlyList<string> patternNodeKeys)
        {
            Dictionary<string, BossStateNode> patternNodes = new(StringComparer.Ordinal);
            if (graph == null || patternNodeKeys == null)
            {
                return patternNodes;
            }

            for (int i = 0; i < patternNodeKeys.Count; i++)
            {
                BossStateNode node = graph.GetNode(patternNodeKeys[i]);
                string nodeKey = GetRuntimeNodeKey(node);
                if (node != null && !string.IsNullOrWhiteSpace(nodeKey))
                {
                    patternNodes[nodeKey] = node;
                }
            }

            return patternNodes;
        }

        private static bool TryGetHologramReplayBranchSelection(
            BossGraphAsset graph,
            HackerHologramReplayPlan plan,
            BossActionContext context,
            out bool isReplayBranchSelected,
            out string replayBranchRootNodeKey)
        {
            isReplayBranchSelected = true;
            replayBranchRootNodeKey = string.Empty;
            if (graph == null || plan.ReplayNode == null || plan.PreludeNodeKeys == null)
            {
                return false;
            }

            string replayNodeKey = GetRuntimeNodeKey(plan.ReplayNode);
            if (string.IsNullOrWhiteSpace(replayNodeKey))
            {
                return false;
            }

            for (int i = 0; i < plan.PreludeNodeKeys.Count; i++)
            {
                BossStateNode branchNode = graph.GetNode(plan.PreludeNodeKeys[i]);
                if (branchNode?.Action is not HackerFireWireBranchAction
                    || !TryGetHackerFireWireBranchTargets(
                        graph,
                        branchNode,
                        out string grabbedTargetKey,
                        out string missedTargetKey))
                {
                    continue;
                }

                bool isReplayOnGrabbedPath = IsNodeReachableFrom(
                    graph,
                    grabbedTargetKey,
                    replayNodeKey);
                bool isReplayOnMissedPath = IsNodeReachableFrom(
                    graph,
                    missedTargetKey,
                    replayNodeKey);
                if (!isReplayOnGrabbedPath && !isReplayOnMissedPath)
                {
                    continue;
                }

                bool isPlayerGrabbed = HackerFireWireBranchAction.IsPlayerGrabbed(context);
                isReplayBranchSelected = isPlayerGrabbed
                    ? isReplayOnGrabbedPath
                    : isReplayOnMissedPath;
                replayBranchRootNodeKey = isReplayOnGrabbedPath
                    ? grabbedTargetKey
                    : missedTargetKey;
                return true;
            }

            return false;
        }

        private static bool IsNodeReachableFrom(
            BossGraphAsset graph,
            string startNodeKey,
            string targetNodeKey)
        {
            if (graph == null
                || string.IsNullOrWhiteSpace(startNodeKey)
                || string.IsNullOrWhiteSpace(targetNodeKey))
            {
                return false;
            }

            HashSet<string> visitedNodeKeys = new(StringComparer.Ordinal);
            Queue<string> pendingNodeKeys = new();
            pendingNodeKeys.Enqueue(startNodeKey);
            while (pendingNodeKeys.Count > 0)
            {
                string currentNodeKey = pendingNodeKeys.Dequeue();
                if (!visitedNodeKeys.Add(currentNodeKey))
                {
                    continue;
                }

                if (currentNodeKey == targetNodeKey)
                {
                    return true;
                }

                foreach (string nextNodeKey in GetBranchTraversalTargets(graph, currentNodeKey))
                {
                    if (!visitedNodeKeys.Contains(nextNodeKey))
                    {
                        pendingNodeKeys.Enqueue(nextNodeKey);
                    }
                }
            }

            return false;
        }

        private static HashSet<string> GetRuntimeNodeKeySet(
            BossGraphAsset graph,
            IReadOnlyList<string> nodeKeys)
        {
            HashSet<string> runtimeNodeKeys = new(StringComparer.Ordinal);
            if (graph == null || nodeKeys == null)
            {
                return runtimeNodeKeys;
            }

            for (int i = 0; i < nodeKeys.Count; i++)
            {
                string nodeKey = GetRuntimeNodeKey(graph.GetNode(nodeKeys[i]));
                if (!string.IsNullOrWhiteSpace(nodeKey))
                {
                    runtimeNodeKeys.Add(nodeKey);
                }
            }

            return runtimeNodeKeys;
        }

        private static HashSet<string> CollectHologramReplayPath(
            BossGraphAsset graph,
            IReadOnlyDictionary<string, BossStateNode> patternNodes,
            string replayNodeKey,
            string startNodeKey)
        {
            HashSet<string> nodeKeys = new(StringComparer.Ordinal);
            if (!patternNodes.ContainsKey(startNodeKey))
            {
                return nodeKeys;
            }

            Queue<string> pendingNodeKeys = new();
            pendingNodeKeys.Enqueue(startNodeKey);
            while (pendingNodeKeys.Count > 0)
            {
                string sourceNodeKey = pendingNodeKeys.Dequeue();
                if (sourceNodeKey == replayNodeKey || !nodeKeys.Add(sourceNodeKey))
                {
                    continue;
                }

                BossStateNode sourceNode = patternNodes[sourceNodeKey];
                if (graph.Transitions != null)
                {
                    for (int i = 0; i < graph.Transitions.Count; i++)
                    {
                        BossTransition transition = graph.Transitions[i];
                        if (transition == null || !transition.IsFromNode(sourceNode))
                        {
                            continue;
                        }

                        string targetNodeKey = GetRuntimeNodeKey(graph.GetNode(transition.ToNodeKey));
                        if (!string.IsNullOrWhiteSpace(targetNodeKey)
                            && targetNodeKey != replayNodeKey
                            && patternNodes.ContainsKey(targetNodeKey))
                        {
                            pendingNodeKeys.Enqueue(targetNodeKey);
                        }
                    }
                }

                // 홀로그램 재생도 본체와 동일한 P 병렬 묶음을 실행해야 한다.
                // Fire Wire 분기는 P 엣지를 분기 선택에 사용하므로 병렬 재생 대상에서 제외한다.
                if (sourceNode.Action is HackerFireWireBranchAction || graph.ParallelEdges == null)
                {
                    continue;
                }

                for (int i = 0; i < graph.ParallelEdges.Count; i++)
                {
                    BossParallelEdge edge = graph.ParallelEdges[i];
                    BossStateNode edgeSource = graph.GetNode(edge?.FromNodeKey);
                    BossStateNode edgeTarget = graph.GetNode(edge?.ToNodeKey);
                    if (edge == null || edgeSource?.Action is HackerFireWireBranchAction)
                    {
                        continue;
                    }

                    string edgeSourceKey = GetRuntimeNodeKey(edgeSource);
                    string edgeTargetKey = GetRuntimeNodeKey(edgeTarget);
                    string pairedNodeKey = edgeSourceKey == sourceNodeKey
                        ? edgeTargetKey
                        : edgeTargetKey == sourceNodeKey
                            ? edgeSourceKey
                            : string.Empty;
                    if (!string.IsNullOrWhiteSpace(pairedNodeKey)
                        && pairedNodeKey != replayNodeKey
                        && patternNodes.ContainsKey(pairedNodeKey))
                    {
                        pendingNodeKeys.Enqueue(pairedNodeKey);
                    }
                }
            }

            return nodeKeys;
        }

        private static HashSet<string> CollectHologramReplayPrelude(
            BossGraphAsset graph,
            IReadOnlyDictionary<string, BossStateNode> patternNodes,
            string replayNodeKey)
        {
            HashSet<string> nodeKeys = new(StringComparer.Ordinal);
            Queue<string> pendingNodeKeys = new();
            pendingNodeKeys.Enqueue(replayNodeKey);
            while (pendingNodeKeys.Count > 0)
            {
                string targetNodeKey = pendingNodeKeys.Dequeue();
                if (graph.Transitions == null)
                {
                    continue;
                }

                for (int i = 0; i < graph.Transitions.Count; i++)
                {
                    BossTransition transition = graph.Transitions[i];
                    BossStateNode transitionTarget = graph.GetNode(transition?.ToNodeKey);
                    if (transition == null || GetRuntimeNodeKey(transitionTarget) != targetNodeKey)
                    {
                        continue;
                    }

                    string sourceNodeKey = GetRuntimeNodeKey(graph.GetNode(transition.FromNodeKey));
                    if (!string.IsNullOrWhiteSpace(sourceNodeKey)
                        && sourceNodeKey != replayNodeKey
                        && patternNodes.ContainsKey(sourceNodeKey)
                        && nodeKeys.Add(sourceNodeKey))
                    {
                        pendingNodeKeys.Enqueue(sourceNodeKey);
                    }
                }
            }

            return nodeKeys;
        }

        private static List<string> GetOrderedPatternNodeKeys(
            IReadOnlyList<string> patternNodeKeys,
            BossGraphAsset graph,
            ISet<string> selectedNodeKeys)
        {
            List<string> orderedNodeKeys = new();
            if (patternNodeKeys == null || selectedNodeKeys == null)
            {
                return orderedNodeKeys;
            }

            HashSet<string> addedNodeKeys = new(StringComparer.Ordinal);
            for (int i = 0; i < patternNodeKeys.Count; i++)
            {
                BossStateNode node = graph.GetNode(patternNodeKeys[i]);
                string nodeKey = GetRuntimeNodeKey(node);
                if (!string.IsNullOrWhiteSpace(nodeKey)
                    && selectedNodeKeys.Contains(nodeKey)
                    && addedNodeKeys.Add(nodeKey))
                {
                    orderedNodeKeys.Add(patternNodeKeys[i]);
                }
            }

            return orderedNodeKeys;
        }

        private static void ApplyHackerFireWireBranchSelection(
            BossGraphAsset graph,
            IReadOnlyList<BossStateNode> executedNodes,
            BossActionContext context,
            ISet<string> skippedNodeKeys)
        {
            if (graph == null || executedNodes == null || skippedNodeKeys == null)
            {
                return;
            }

            for (int i = 0; i < executedNodes.Count; i++)
            {
                BossStateNode branchNode = executedNodes[i];
                if (branchNode?.Action is not HackerFireWireBranchAction
                    || !TryGetHackerFireWireBranchTargets(graph, branchNode, out string grabbedTargetKey, out string missedTargetKey))
                {
                    continue;
                }

                string skippedTargetKey = HackerFireWireBranchAction.IsPlayerGrabbed(context)
                    ? missedTargetKey
                    : grabbedTargetKey;
                if (!string.IsNullOrWhiteSpace(skippedTargetKey))
                {
                    SkipHackerFireWireBranchPath(graph, skippedTargetKey, skippedNodeKeys);
                }
            }
        }

        private static void SkipHackerFireWireBranchPath(
            BossGraphAsset graph,
            string rootNodeKey,
            ISet<string> skippedNodeKeys)
        {
            if (graph == null || string.IsNullOrWhiteSpace(rootNodeKey) || skippedNodeKeys == null || !skippedNodeKeys.Add(rootNodeKey))
            {
                return;
            }

            Queue<string> pendingNodeKeys = new();
            pendingNodeKeys.Enqueue(rootNodeKey);
            while (pendingNodeKeys.Count > 0)
            {
                string sourceNodeKey = pendingNodeKeys.Dequeue();
                foreach (string targetNodeKey in GetBranchTraversalTargets(graph, sourceNodeKey))
                {
                    if (IsReachedOnlyFromSkippedNodes(graph, targetNodeKey, skippedNodeKeys)
                        && skippedNodeKeys.Add(targetNodeKey))
                    {
                        pendingNodeKeys.Enqueue(targetNodeKey);
                    }
                }
            }
        }

        private static IEnumerable<string> GetBranchTraversalTargets(BossGraphAsset graph, string sourceNodeKey)
        {
            BossStateNode sourceNode = graph?.GetNode(sourceNodeKey);
            if (sourceNode == null)
            {
                yield break;
            }

            if (graph.Transitions != null)
            {
                for (int i = 0; i < graph.Transitions.Count; i++)
                {
                    BossTransition transition = graph.Transitions[i];
                    if (transition != null && transition.IsFromNode(sourceNode))
                    {
                        string targetNodeKey = GetRuntimeNodeKey(graph.GetNode(transition.ToNodeKey));
                        if (!string.IsNullOrWhiteSpace(targetNodeKey))
                        {
                            yield return targetNodeKey;
                        }
                    }
                }
            }

            if (graph.ParallelEdges == null)
            {
                yield break;
            }

            for (int i = 0; i < graph.ParallelEdges.Count; i++)
            {
                BossParallelEdge edge = graph.ParallelEdges[i];
                BossStateNode edgeSource = graph.GetNode(edge?.FromNodeKey);
                BossStateNode edgeTarget = graph.GetNode(edge?.ToNodeKey);
                if (edge == null || edgeSource == null || edgeTarget == null)
                {
                    continue;
                }

                string targetNodeKey = edgeSource == sourceNode
                    ? GetRuntimeNodeKey(edgeTarget)
                    : sourceNode.Action is not HackerFireWireBranchAction && edgeTarget == sourceNode
                        ? GetRuntimeNodeKey(edgeSource)
                        : string.Empty;
                if (!string.IsNullOrWhiteSpace(targetNodeKey))
                {
                    yield return targetNodeKey;
                }
            }
        }

        private static bool IsReachedOnlyFromSkippedNodes(
            BossGraphAsset graph,
            string targetNodeKey,
            ISet<string> skippedNodeKeys)
        {
            BossStateNode targetNode = graph?.GetNode(targetNodeKey);
            if (targetNode == null)
            {
                return false;
            }

            bool hasIncomingConnection = false;
            if (graph.Transitions != null)
            {
                for (int i = 0; i < graph.Transitions.Count; i++)
                {
                    BossTransition transition = graph.Transitions[i];
                    BossStateNode transitionTarget = graph.GetNode(transition?.ToNodeKey);
                    if (transition == null || transitionTarget != targetNode)
                    {
                        continue;
                    }

                    hasIncomingConnection = true;
                    if (!skippedNodeKeys.Contains(GetRuntimeNodeKey(graph.GetNode(transition.FromNodeKey))))
                    {
                        return false;
                    }
                }
            }

            if (graph.ParallelEdges != null)
            {
                for (int i = 0; i < graph.ParallelEdges.Count; i++)
                {
                    BossParallelEdge edge = graph.ParallelEdges[i];
                    BossStateNode edgeSource = graph.GetNode(edge?.FromNodeKey);
                    BossStateNode edgeTarget = graph.GetNode(edge?.ToNodeKey);
                    if (edge == null || edgeSource == null || edgeTarget == null)
                    {
                        continue;
                    }

                    string pairedNodeKey = edgeTarget == targetNode
                        ? GetRuntimeNodeKey(edgeSource)
                        : edgeSource.Action is not HackerFireWireBranchAction && edgeSource == targetNode
                            ? GetRuntimeNodeKey(edgeTarget)
                            : string.Empty;
                    if (string.IsNullOrWhiteSpace(pairedNodeKey))
                    {
                        continue;
                    }

                    hasIncomingConnection = true;
                    if (!skippedNodeKeys.Contains(pairedNodeKey))
                    {
                        return false;
                    }
                }
            }

            return hasIncomingConnection;
        }

        private static bool TryGetHackerFireWireBranchTargets(
            BossGraphAsset graph,
            BossStateNode branchNode,
            out string grabbedTargetKey,
            out string missedTargetKey)
        {
            grabbedTargetKey = GetTransitionTargetKey(graph, branchNode, 0);
            missedTargetKey = GetTransitionTargetKey(graph, branchNode, 1);
            if (string.IsNullOrWhiteSpace(missedTargetKey))
            {
                missedTargetKey = GetFirstParallelTargetKey(graph, branchNode);
            }

            return !string.IsNullOrWhiteSpace(grabbedTargetKey)
                && !string.IsNullOrWhiteSpace(missedTargetKey);
        }

        private static string GetTransitionTargetKey(
            BossGraphAsset graph,
            BossStateNode sourceNode,
            int outputPortIndex)
        {
            if (graph?.Transitions == null || sourceNode == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < graph.Transitions.Count; i++)
            {
                BossTransition transition = graph.Transitions[i];
                if (transition == null
                    || !transition.IsFromNode(sourceNode)
                    || transition.FromOutputPortIndex != outputPortIndex)
                {
                    continue;
                }

                return GetRuntimeNodeKey(graph.GetNode(transition.ToNodeKey));
            }

            return string.Empty;
        }

        private static string GetFirstParallelTargetKey(BossGraphAsset graph, BossStateNode sourceNode)
        {
            if (graph?.ParallelEdges == null || sourceNode == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < graph.ParallelEdges.Count; i++)
            {
                BossParallelEdge edge = graph.ParallelEdges[i];
                if (edge == null || !edge.IsFromNode(sourceNode))
                {
                    continue;
                }

                return GetRuntimeNodeKey(graph.GetNode(edge.ToNodeKey));
            }

            return string.Empty;
        }

        private static IEnumerator ExecutePatternNodeGroup(
            BossGraphAsset graph,
            IReadOnlyList<BossStateNode> nodes,
            string previousNodeId,
            BossActionContext context,
            bool updateRuntimeState,
            IReadOnlyDictionary<string, BossAction> actionOverrides,
            Action<BossStateNode> onNodeCompleted = null)
        {
            bool synchronizeMeleeAdvance = nodes.Count(node => node?.Action is HackerMeleeAttackAction) == 1
                && nodes.Any(node => node?.Action is HackerSequentialSweepFireAction
                    || node?.Action is FireRadialEmissionAction);
            if (synchronizeMeleeAdvance)
            {
                context.BeginMeleeAdvanceSynchronization();
            }

            ConfigureConductorCueDurationCompanions(nodes, context);
            List<ConductorCueOverlayPlan> overlayPlans = BuildConductorCueOverlayPlans(nodes, context);
            HashSet<BossStateNode> overlayCueNodes = new(overlayPlans.Select(plan => plan.CueNode));
            List<IEnumerator> routines = new();
            for (int i = 0; i < overlayPlans.Count; i++)
            {
                routines.Add(ExecuteDelayedConductorCue(
                    overlayPlans[i],
                    context,
                    () => ShouldStartConductorCueOverlayEarly(nodes)));
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                BossStateNode node = nodes[i];
                if (node?.Action != null && !overlayCueNodes.Contains(node))
                {
                    BossAction actionOverride = null;
                    string nodeKey = GetRuntimeNodeKey(node);
                    if (actionOverrides != null && !string.IsNullOrWhiteSpace(nodeKey))
                    {
                        actionOverrides.TryGetValue(nodeKey, out actionOverride);
                    }

                    routines.Add(ExecuteSinglePatternNode(
                        graph,
                        node,
                        previousNodeId,
                        context,
                        updateRuntimeState,
                        actionOverride,
                        onNodeCompleted));
                }
            }

            if (routines.Count == 0)
            {
                ClearConductorCueDurationCompanions(nodes);
                yield break;
            }

            try
            {
                yield return RunParallelRoutines(routines);
            }
            finally
            {
                if (synchronizeMeleeAdvance)
                {
                    context.EndMeleeAdvanceSynchronization();
                }

                ClearConductorCueOverlayEarlyStart(nodes);
                ClearConductorCueDurationCompanions(nodes);
            }
        }

        private static IEnumerator ExecuteSinglePatternNode(
            BossGraphAsset graph,
            BossStateNode node,
            string previousNodeId,
            BossActionContext context,
            bool updateRuntimeState,
            BossAction actionOverride = null,
            Action<BossStateNode> onCompleted = null)
        {
            BossAction action = actionOverride ?? node?.Action;
            if (action == null)
            {
                yield break;
            }

            bool completed = false;
            try
            {
                if (updateRuntimeState)
                {
                    BossGraphRuntimeState.SetCurrentNode(graph, node.NodeId, previousNodeId);
                }
                context.BeginNodeExecution(node.NodeId);
                if (action is ConductorConductingCueAction cue
                    && context.Boss is Conductor conductor)
                {
                    yield return conductor.PlayConductingPattern(
                        cue.PatternId,
                        context,
                        cue.CreateSettings(),
                        holdMinionOutlineAfterCue: true);
                }
                else
                {
                    yield return action.Execute(context);
                }

                completed = true;
            }
            finally
            {
                context.EndNodeExecution();
                if (completed)
                {
                    onCompleted?.Invoke(node);
                }
            }
        }

        private static Dictionary<string, BossAction> ClonePatternActions(
            BossGraphAsset graph,
            IReadOnlyList<string> nodeKeys)
        {
            Dictionary<string, BossAction> clones = new(StringComparer.Ordinal);
            if (graph == null || nodeKeys == null)
            {
                return clones;
            }

            for (int i = 0; i < nodeKeys.Count; i++)
            {
                BossStateNode node = graph.GetNode(nodeKeys[i]);
                string nodeKey = GetRuntimeNodeKey(node);
                if (node?.Action != null && !string.IsNullOrWhiteSpace(nodeKey))
                {
                    clones[nodeKey] = CloneBossAction(node.Action);
                }
            }

            return clones;
        }

        private static BossAction CloneBossAction(BossAction source)
        {
            if (source == null)
            {
                return null;
            }

            try
            {
                BossAction clone = Activator.CreateInstance(source.GetType()) as BossAction;
                if (clone == null)
                {
                    return source;
                }

                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), clone);
                return clone;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[BossGraphRunner] 홀로그램 액션 복제 실패: {source.GetType().Name} ({exception.Message})");
                return source;
            }
        }

        private static List<ConductorCueOverlayPlan> BuildConductorCueOverlayPlans(
            IReadOnlyList<BossStateNode> nodes,
            BossActionContext context)
        {
            List<ConductorCueOverlayPlan> plans = new();
            if (nodes == null || context?.Boss is not Conductor conductor)
            {
                return plans;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                BossStateNode cueNode = nodes[i];
                ConductorConductingCueAction cue = GetConductorCueAction(cueNode);
                if (cue == null)
                {
                    continue;
                }

                IConductorCueOverlayExplicitStartSource explicitStartSource = nodes
                    .Where(node => node != cueNode)
                    .Select(node => node?.Action)
                    .OfType<IConductorCueOverlayExplicitStartSource>()
                    .FirstOrDefault();
                if (explicitStartSource != null)
                {
                    plans.Add(new ConductorCueOverlayPlan(
                        cueNode,
                        cue,
                        0f,
                        explicitStartSource));
                    continue;
                }

                if (!cue.TryGetTotalSeconds(conductor, out float cueSeconds)
                    || !TryGetParallelGroupActionDuration(nodes, cueNode, context, out float actionSeconds))
                {
                    continue;
                }

                float delaySeconds = Mathf.Max(0f, actionSeconds - cueSeconds);
                plans.Add(new ConductorCueOverlayPlan(cueNode, cue, delaySeconds));
            }

            return plans;
        }

        private static void ConfigureConductorCueDurationCompanions(
            IReadOnlyList<BossStateNode> nodes,
            BossActionContext context)
        {
            if (nodes == null || context?.Boss is not Conductor conductor)
            {
                return;
            }

            float longestCueSeconds = 0f;
            for (int i = 0; i < nodes.Count; i++)
            {
                ConductorConductingCueAction cue = GetConductorCueAction(nodes[i]);
                if (cue != null && cue.TryGetTotalSeconds(conductor, out float cueSeconds))
                {
                    longestCueSeconds = Mathf.Max(longestCueSeconds, cueSeconds);
                }
            }

            if (longestCueSeconds <= 0f)
            {
                return;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i]?.Action is IConductorCueDurationCompanion companion)
                {
                    companion.SetMinimumConductorCueDuration(longestCueSeconds);
                }
            }
        }

        private static void ClearConductorCueDurationCompanions(IReadOnlyList<BossStateNode> nodes)
        {
            if (nodes == null)
            {
                return;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i]?.Action is IConductorCueDurationCompanion companion)
                {
                    companion.ClearMinimumConductorCueDuration();
                }
            }
        }

        private static bool TryGetParallelGroupActionDuration(
            IReadOnlyList<BossStateNode> nodes,
            BossStateNode cueNode,
            BossActionContext context,
            out float durationSeconds)
        {
            durationSeconds = 0f;
            if (nodes == null || cueNode == null)
            {
                return false;
            }

            bool found = false;
            for (int i = 0; i < nodes.Count; i++)
            {
                BossStateNode node = nodes[i];
                if (node == cueNode
                    || !IsConductorCueTimingActionNode(node)
                    || !TryGetActionDurationSeconds(node.Action, context, out float nodeDurationSeconds))
                {
                    continue;
                }

                durationSeconds = Mathf.Max(durationSeconds, nodeDurationSeconds);
                found = true;
            }

            return found;
        }

        private static bool IsConductorCueTimingActionNode(BossStateNode node)
        {
            return node != null
                && node.Action != null
                && GetConductorCueAction(node) == null;
        }

        private static bool TryGetActionDurationSeconds(
            BossAction action,
            BossActionContext context,
            out float durationSeconds)
        {
            durationSeconds = 0f;
            if (action is IBossActionContextDurationProvider contextProvider
                && contextProvider.TryGetDurationSeconds(context, out durationSeconds)
                && durationSeconds > 0f)
            {
                return true;
            }

            return action is IBossActionDurationProvider provider
                && provider.TryGetDurationSeconds(out durationSeconds)
                && durationSeconds > 0f;
        }

        private static IEnumerator ExecuteDelayedConductorCue(
            ConductorCueOverlayPlan plan,
            BossActionContext context,
            System.Func<bool> shouldStartImmediately)
        {
            if (context?.Boss is not Conductor conductor || plan.Cue == null)
            {
                yield break;
            }

            if (plan.ExplicitStartSource != null)
            {
                while (!plan.ExplicitStartSource.ShouldStartConductorCueOverlay)
                {
                    if (plan.ExplicitStartSource.IsConductorCueOverlaySourceFinished)
                    {
                        yield break;
                    }

                    if (context.IsExecutionPaused)
                    {
                        context.Stop();
                    }

                    yield return null;
                }
            }
            else if (plan.DelaySeconds > 0f)
            {
                float remainingSeconds = plan.DelaySeconds;
                while (remainingSeconds > 0f)
                {
                    if (shouldStartImmediately?.Invoke() == true)
                    {
                        break;
                    }

                    if (context.IsExecutionPaused)
                    {
                        context.Stop();
                        yield return null;
                        continue;
                    }

                    remainingSeconds -= EnemyTimeScale.DeltaTime;
                    yield return null;
                }
            }

            if (!conductor.TryGetConductingPattern(plan.Cue.PatternId, out ConductorConductingPattern pattern)
                || pattern == null
                || !pattern.HasDrawableStroke)
            {
                yield break;
            }

            yield return conductor.PlayConductingPattern(
                plan.Cue.PatternId,
                context,
                plan.Cue.CreateSettings(),
                holdMinionOutlineAfterCue: true);
        }

        private static void ReleaseConductorMinionOutlineHolds(BossActionContext context, int count)
        {
            if (count <= 0 || context?.Boss is not Conductor conductor)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                conductor.ReleaseMinionOutlinePatternVisibilityHold();
            }
        }

        private static void ClearConductorMinionOutlineHolds(BossActionContext context)
        {
            context?.ClearConductorMinionOutlineHoldRequests();
            if (context?.Boss is Conductor conductor)
            {
                conductor.ClearMinionOutlinePatternVisibility();
            }
        }

        private static bool ShouldStartConductorCueOverlayEarly(IReadOnlyList<BossStateNode> nodes)
        {
            if (nodes == null)
            {
                return false;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i]?.Action is IConductorCueOverlayEarlyStartSource earlyStartSource
                    && earlyStartSource.ShouldStartConductorCueOverlayEarly)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ClearConductorCueOverlayEarlyStart(IReadOnlyList<BossStateNode> nodes)
        {
            if (nodes == null)
            {
                return;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i]?.Action is IConductorCueOverlayEarlyStartSource earlyStartSource)
                {
                    earlyStartSource.ClearConductorCueOverlayEarlyStart();
                }

                if (nodes[i]?.Action is IConductorCueOverlayExplicitStartSource explicitStartSource)
                {
                    explicitStartSource.ClearConductorCueOverlayStart();
                }
            }
        }

        private static ConductorConductingCueAction GetConductorCueAction(BossStateNode node)
        {
            return node?.Action as ConductorConductingCueAction;
        }

        private readonly struct HackerHologramReplayPlan
        {
            public HackerHologramReplayPlan(
                BossStateNode replayNode,
                IReadOnlyList<string> preludeNodeKeys,
                IReadOnlyList<string> patternNodeKeys,
                IReadOnlyList<string> allPatternNodeKeys)
            {
                ReplayNode = replayNode;
                PreludeNodeKeys = preludeNodeKeys ?? Array.Empty<string>();
                PatternNodeKeys = patternNodeKeys ?? Array.Empty<string>();
                AllPatternNodeKeys = allPatternNodeKeys ?? Array.Empty<string>();
            }

            public BossStateNode ReplayNode { get; }
            public IReadOnlyList<string> PreludeNodeKeys { get; }
            public IReadOnlyList<string> PatternNodeKeys { get; }
            public IReadOnlyList<string> AllPatternNodeKeys { get; }
        }

        private readonly struct ConductorCueOverlayPlan
        {
            public ConductorCueOverlayPlan(
                BossStateNode cueNode,
                ConductorConductingCueAction cue,
                float delaySeconds,
                IConductorCueOverlayExplicitStartSource explicitStartSource = null)
            {
                CueNode = cueNode;
                Cue = cue;
                DelaySeconds = Mathf.Max(0f, delaySeconds);
                ExplicitStartSource = explicitStartSource;
            }

            public BossStateNode CueNode { get; }
            public ConductorConductingCueAction Cue { get; }
            public float DelaySeconds { get; }
            public IConductorCueOverlayExplicitStartSource ExplicitStartSource { get; }
        }

        private static IEnumerator RunParallelRoutines(IReadOnlyList<IEnumerator> routines)
        {
            List<CoroutineStack> stacks = new();
            for (int i = 0; i < routines.Count; i++)
            {
                if (routines[i] != null)
                {
                    stacks.Add(new CoroutineStack(routines[i]));
                }
            }

            while (stacks.Count > 0)
            {
                for (int i = stacks.Count - 1; i >= 0; i--)
                {
                    if (!stacks[i].MoveNext())
                    {
                        stacks.RemoveAt(i);
                    }
                }

                if (stacks.Count > 0)
                {
                    yield return null;
                }
            }
        }

        private static string GetRuntimeNodeKey(BossStateNode node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrWhiteSpace(node.NodeGuid) ? node.NodeGuid : node.NodeId;
        }

        private static Dictionary<string, List<BossStateNode>> BuildPatternParallelGroups(
            BossGraphAsset graph,
            IReadOnlyList<string> nodeKeys)
        {
            Dictionary<string, List<BossStateNode>> groupsByNodeKey = new(StringComparer.Ordinal);
            if (graph?.ParallelEdges == null || nodeKeys == null || nodeKeys.Count == 0)
            {
                return groupsByNodeKey;
            }

            Dictionary<string, BossStateNode> patternNodes = new(StringComparer.Ordinal);
            Dictionary<string, int> patternOrder = new(StringComparer.Ordinal);
            Dictionary<string, List<string>> links = new(StringComparer.Ordinal);
            for (int i = 0; i < nodeKeys.Count; i++)
            {
                BossStateNode node = graph.GetNode(nodeKeys[i]);
                string runtimeNodeKey = GetRuntimeNodeKey(node);
                if (node == null || string.IsNullOrWhiteSpace(runtimeNodeKey) || patternNodes.ContainsKey(runtimeNodeKey))
                {
                    continue;
                }

                patternNodes[runtimeNodeKey] = node;
                patternOrder[runtimeNodeKey] = i;
                links[runtimeNodeKey] = new List<string>();
            }

            for (int i = 0; i < graph.ParallelEdges.Count; i++)
            {
                BossParallelEdge edge = graph.ParallelEdges[i];
                BossStateNode sourceNode = graph.GetNode(edge?.FromNodeKey);
                BossStateNode targetNode = graph.GetNode(edge?.ToNodeKey);
                string sourceKey = GetRuntimeNodeKey(sourceNode);
                string targetKey = GetRuntimeNodeKey(targetNode);
                if (sourceNode?.Action is HackerFireWireBranchAction)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(sourceKey)
                    && !string.IsNullOrWhiteSpace(targetKey)
                    && sourceKey != targetKey
                    && links.ContainsKey(sourceKey)
                    && links.ContainsKey(targetKey))
                {
                    AddUnique(links[sourceKey], targetKey);
                    AddUnique(links[targetKey], sourceKey);
                }
            }

            HashSet<string> visitedNodeKeys = new(StringComparer.Ordinal);
            foreach (string startKey in patternNodes.Keys.OrderBy(key => patternOrder[key]))
            {
                if (!visitedNodeKeys.Add(startKey) || links[startKey].Count == 0)
                {
                    continue;
                }

                List<string> groupKeys = new();
                Queue<string> queue = new();
                queue.Enqueue(startKey);
                while (queue.Count > 0)
                {
                    string nodeKey = queue.Dequeue();
                    groupKeys.Add(nodeKey);
                    for (int i = 0; i < links[nodeKey].Count; i++)
                    {
                        string linkedKey = links[nodeKey][i];
                        if (visitedNodeKeys.Add(linkedKey))
                        {
                            queue.Enqueue(linkedKey);
                        }
                    }
                }

                List<BossStateNode> group = groupKeys
                    .OrderBy(key => patternOrder[key])
                    .Select(key => patternNodes[key])
                    .ToList();
                for (int i = 0; i < groupKeys.Count; i++)
                {
                    groupsByNodeKey[groupKeys[i]] = group;
                }
            }

            return groupsByNodeKey;
        }

        private static List<List<BossStateNode>> BuildPatternExecutionGroups(
            BossGraphAsset graph,
            IReadOnlyList<string> nodeKeys,
            IReadOnlyDictionary<string, List<BossStateNode>> parallelGroups)
        {
            Dictionary<string, BossStateNode> patternNodes = new(StringComparer.Ordinal);
            Dictionary<string, int> nodeOrder = BuildNodeOrder(graph);
            for (int i = 0; i < nodeKeys.Count; i++)
            {
                BossStateNode node = graph.GetNode(nodeKeys[i]);
                string nodeKey = GetRuntimeNodeKey(node);
                if (node == null || string.IsNullOrWhiteSpace(nodeKey) || patternNodes.ContainsKey(nodeKey))
                {
                    continue;
                }

                patternNodes[nodeKey] = node;
            }

            Dictionary<string, string> nodeGroupKeys = new(StringComparer.Ordinal);
            Dictionary<string, List<BossStateNode>> groupNodes = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, BossStateNode> pair in patternNodes)
            {
                string groupKey = GetCanonicalGroupKey(pair.Key, parallelGroups, patternNodes, nodeOrder);
                nodeGroupKeys[pair.Key] = groupKey;
                if (!groupNodes.ContainsKey(groupKey))
                {
                    groupNodes[groupKey] = GetGroupNodes(groupKey, parallelGroups, patternNodes, nodeOrder);
                }
            }

            Dictionary<string, List<string>> outgoingGroups = new(StringComparer.Ordinal);
            Dictionary<string, List<string>> incomingGroups = new(StringComparer.Ordinal);
            foreach (string groupKey in groupNodes.Keys)
            {
                outgoingGroups[groupKey] = new List<string>();
                incomingGroups[groupKey] = new List<string>();
            }

            if (graph?.Transitions != null)
            {
                for (int i = 0; i < graph.Transitions.Count; i++)
                {
                    BossTransition transition = graph.Transitions[i];
                    BossStateNode fromNode = graph.GetNode(transition?.FromNodeKey);
                    BossStateNode toNode = graph.GetNode(transition?.ToNodeKey);
                    string fromNodeKey = GetRuntimeNodeKey(fromNode);
                    string toNodeKey = GetRuntimeNodeKey(toNode);
                    if (!nodeGroupKeys.TryGetValue(fromNodeKey, out string fromGroupKey)
                        || !nodeGroupKeys.TryGetValue(toNodeKey, out string toGroupKey)
                        || fromGroupKey == toGroupKey)
                    {
                        continue;
                    }

                    AddUnique(outgoingGroups[fromGroupKey], toGroupKey);
                    AddUnique(incomingGroups[toGroupKey], fromGroupKey);
                }
            }

            if (graph?.ParallelEdges != null)
            {
                for (int i = 0; i < graph.ParallelEdges.Count; i++)
                {
                    BossParallelEdge edge = graph.ParallelEdges[i];
                    BossStateNode fromNode = graph.GetNode(edge?.FromNodeKey);
                    BossStateNode toNode = graph.GetNode(edge?.ToNodeKey);
                    if (fromNode?.Action is not HackerFireWireBranchAction)
                    {
                        continue;
                    }

                    string fromNodeKey = GetRuntimeNodeKey(fromNode);
                    string toNodeKey = GetRuntimeNodeKey(toNode);
                    if (!nodeGroupKeys.TryGetValue(fromNodeKey, out string fromGroupKey)
                        || !nodeGroupKeys.TryGetValue(toNodeKey, out string toGroupKey)
                        || fromGroupKey == toGroupKey)
                    {
                        continue;
                    }

                    AddUnique(outgoingGroups[fromGroupKey], toGroupKey);
                    AddUnique(incomingGroups[toGroupKey], fromGroupKey);
                }
            }

            List<string> startGroupKeys = groupNodes.Keys
                .Where(groupKey => incomingGroups[groupKey].Count == 0)
                .OrderBy(groupKey => GetGroupOrder(groupKey, groupNodes, nodeOrder))
                .ToList();
            if (startGroupKeys.Count == 0)
            {
                startGroupKeys.AddRange(groupNodes.Keys.OrderBy(groupKey => GetGroupOrder(groupKey, groupNodes, nodeOrder)));
            }

            List<List<BossStateNode>> executionGroups = new();
            HashSet<string> visitedGroupKeys = new(StringComparer.Ordinal);
            for (int i = 0; i < startGroupKeys.Count; i++)
            {
                AppendExecutionGroupOrder(
                    startGroupKeys[i],
                    groupNodes,
                    outgoingGroups,
                    nodeOrder,
                    visitedGroupKeys,
                    executionGroups);
            }

            foreach (string groupKey in groupNodes.Keys.OrderBy(groupKey => GetGroupOrder(groupKey, groupNodes, nodeOrder)))
            {
                AppendExecutionGroupOrder(
                    groupKey,
                    groupNodes,
                    outgoingGroups,
                    nodeOrder,
                    visitedGroupKeys,
                    executionGroups);
            }

            return executionGroups;
        }

        private static Dictionary<string, int> BuildNodeOrder(BossGraphAsset graph)
        {
            Dictionary<string, int> nodeOrder = new(StringComparer.Ordinal);
            if (graph?.StateNodes == null)
            {
                return nodeOrder;
            }

            for (int i = 0; i < graph.StateNodes.Count; i++)
            {
                string nodeKey = GetRuntimeNodeKey(graph.StateNodes[i]);
                if (!string.IsNullOrWhiteSpace(nodeKey) && !nodeOrder.ContainsKey(nodeKey))
                {
                    nodeOrder[nodeKey] = i;
                }
            }

            return nodeOrder;
        }

        private static string GetCanonicalGroupKey(
            string nodeKey,
            IReadOnlyDictionary<string, List<BossStateNode>> parallelGroups,
            IReadOnlyDictionary<string, BossStateNode> patternNodes,
            IReadOnlyDictionary<string, int> nodeOrder)
        {
            if (parallelGroups == null || !parallelGroups.TryGetValue(nodeKey, out List<BossStateNode> group))
            {
                return nodeKey;
            }

            return group
                .Select(GetRuntimeNodeKey)
                .Where(patternNodes.ContainsKey)
                .OrderBy(key => nodeOrder.TryGetValue(key, out int order) ? order : int.MaxValue)
                .FirstOrDefault() ?? nodeKey;
        }

        private static List<BossStateNode> GetGroupNodes(
            string groupKey,
            IReadOnlyDictionary<string, List<BossStateNode>> parallelGroups,
            IReadOnlyDictionary<string, BossStateNode> patternNodes,
            IReadOnlyDictionary<string, int> nodeOrder)
        {
            IEnumerable<BossStateNode> nodes = parallelGroups != null && parallelGroups.TryGetValue(groupKey, out List<BossStateNode> group)
                ? group.Where(node => patternNodes.ContainsKey(GetRuntimeNodeKey(node)))
                : new[] { patternNodes[groupKey] };
            return nodes
                .GroupBy(GetRuntimeNodeKey)
                .Select(nodeGroup => nodeGroup.First())
                .OrderBy(node => nodeOrder.TryGetValue(GetRuntimeNodeKey(node), out int order) ? order : int.MaxValue)
                .ToList();
        }

        private static int GetGroupOrder(
            string groupKey,
            IReadOnlyDictionary<string, List<BossStateNode>> groupNodes,
            IReadOnlyDictionary<string, int> nodeOrder)
        {
            if (!groupNodes.TryGetValue(groupKey, out List<BossStateNode> nodes) || nodes.Count == 0)
            {
                return int.MaxValue;
            }

            return nodes.Min(node => nodeOrder.TryGetValue(GetRuntimeNodeKey(node), out int order) ? order : int.MaxValue);
        }

        private static void AppendExecutionGroupOrder(
            string groupKey,
            IReadOnlyDictionary<string, List<BossStateNode>> groupNodes,
            IReadOnlyDictionary<string, List<string>> outgoingGroups,
            IReadOnlyDictionary<string, int> nodeOrder,
            HashSet<string> visitedGroupKeys,
            List<List<BossStateNode>> executionGroups)
        {
            if (!groupNodes.ContainsKey(groupKey) || !visitedGroupKeys.Add(groupKey))
            {
                return;
            }

            executionGroups.Add(groupNodes[groupKey]);
            if (!outgoingGroups.TryGetValue(groupKey, out List<string> nextGroupKeys))
            {
                return;
            }

            foreach (string nextGroupKey in nextGroupKeys.OrderBy(key => GetGroupOrder(key, groupNodes, nodeOrder)))
            {
                AppendExecutionGroupOrder(nextGroupKey, groupNodes, outgoingGroups, nodeOrder, visitedGroupKeys, executionGroups);
            }
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!values.Contains(value))
            {
                values.Add(value);
            }
        }

        private BossStateNode ResolveCurrentNode(BossGraphAsset graph, BossActionContext context)
        {
            BossStateNode currentNode = graph.GetNode(currentNodeId);
            if (currentNode != null)
            {
                return currentNode;
            }

            currentNode = graph.GetNodeForPhase(context.Boss.CurrentPhaseIndex);
            currentNodeId = currentNode?.NodeId;
            return currentNode;
        }

        private bool TryApplyTransition(
            BossGraphAsset graph,
            BossActionContext context,
            BossStateNode currentNode,
            bool sequenceEnded)
        {
            if (graph.Transitions == null || currentNode == null)
            {
                return false;
            }

            int requiredOutputPortIndex = -1;
            if (currentNode.Action is HackerFireWireBranchAction)
            {
                requiredOutputPortIndex = HackerFireWireBranchAction.IsPlayerGrabbed(context) ? 0 : 1;
            }

            for (int i = 0; i < graph.Transitions.Count; i++)
            {
                BossTransition transition = graph.Transitions[i];
                if (transition == null
                    || !transition.IsFromNode(currentNode)
                    || (requiredOutputPortIndex >= 0 && transition.FromOutputPortIndex != requiredOutputPortIndex))
                {
                    continue;
                }

                BossStateNode targetNode = graph.GetNode(transition.ToNodeKey);
                if (targetNode == null || !IsTransitionConditionMet(transition, context, sequenceEnded))
                {
                    continue;
                }

                BossGraphRuntimeState.SetCurrentNode(graph, targetNode.NodeId, currentNode.NodeId);
                previousRuntimeNodeId = currentNode.NodeId;
                currentNodeId = targetNode.NodeId;
                return true;
            }

            return false;
        }

        private static bool IsTransitionConditionMet(
            BossTransition transition,
            BossActionContext context,
            bool sequenceEnded)
        {
            BossAI boss = context.Boss;
            if (boss == null)
            {
                return sequenceEnded && transition.ConditionType == BossTransitionConditionType.SequenceEnded;
            }

            return transition.ConditionType switch
            {
                BossTransitionConditionType.SequenceEnded => sequenceEnded,
                BossTransitionConditionType.HpRatioLessOrEqual => GetHpRatio(boss) <= transition.Threshold,
                BossTransitionConditionType.PhaseIndexEquals => boss.CurrentPhaseIndex == transition.PhaseIndex,
                BossTransitionConditionType.PlayerDetected => boss.IsPlayerDetected(),
                BossTransitionConditionType.HpEmpty => boss.IsHpEmpty,
                BossTransitionConditionType.HpNotEmpty => !boss.IsHpEmpty,
                BossTransitionConditionType.Staggered => boss.IsStaggered,
                BossTransitionConditionType.NotStaggered => !boss.IsStaggered,
                BossTransitionConditionType.ExecutionLocked => boss.IsExecutionLocked,
                BossTransitionConditionType.ExecutionPaused => context.IsExecutionPaused,
                BossTransitionConditionType.LivesLessOrEqual => boss.CurrentLives <= transition.PhaseIndex,
                _ => false
            };
        }

        private static float GetHpRatio(BossAI boss)
        {
            if (boss == null || boss.HpGauge == null || boss.HpGauge.MaxBullets <= 0)
            {
                return 0f;
            }

            return Mathf.Clamp01((float)boss.HpGauge.CurrentBullets / boss.HpGauge.MaxBullets);
        }

        private BossSequenceEntry SelectSequence(BossStateNode node)
        {
            if (node == null || node.Sequences == null || node.Sequences.Count == 0)
            {
                return null;
            }

            string nodeKey = string.IsNullOrWhiteSpace(node.NodeId) ? node.PhaseIndex.ToString() : node.NodeId;
            return node.SelectionMode switch
            {
                BossSequenceSelectionMode.Random => SelectRandom(node),
                BossSequenceSelectionMode.RandomNoRepeat => SelectRandomNoRepeat(node, nodeKey),
                BossSequenceSelectionMode.WeightedRandom => SelectWeightedRandom(node),
                BossSequenceSelectionMode.ShuffledBag => SelectShuffledBag(node, nodeKey),
                _ => SelectSequential(node, nodeKey)
            };
        }

        private BossSequenceEntry SelectSequential(BossStateNode node, string nodeKey)
        {
            int index = nextSequenceIndexes.TryGetValue(nodeKey, out int nextIndex) ? nextIndex : 0;
            BossSequenceEntry entry = GetEntryWrapping(node, index);
            nextSequenceIndexes[nodeKey] = index + 1;
            TrackLast(nodeKey, entry);
            return entry;
        }

        private BossSequenceEntry SelectRandom(BossStateNode node)
        {
            BossSequenceEntry entry = GetEntryWrapping(node, Random.Range(0, node.Sequences.Count));
            return entry;
        }

        private BossSequenceEntry SelectRandomNoRepeat(BossStateNode node, string nodeKey)
        {
            if (node.Sequences.Count <= 1 || !lastSequences.TryGetValue(nodeKey, out BossGraphActionAsset lastSequence))
            {
                BossSequenceEntry firstEntry = SelectRandom(node);
                TrackLast(nodeKey, firstEntry);
                return firstEntry;
            }

            for (int attempts = 0; attempts < 12; attempts++)
            {
                BossSequenceEntry candidate = SelectRandom(node);
                if (candidate?.Sequence != null && candidate.Sequence != lastSequence)
                {
                    TrackLast(nodeKey, candidate);
                    return candidate;
                }
            }

            return SelectSequential(node, nodeKey);
        }

        private BossSequenceEntry SelectWeightedRandom(BossStateNode node)
        {
            int totalWeight = 0;
            for (int i = 0; i < node.Sequences.Count; i++)
            {
                totalWeight += node.Sequences[i]?.Weight ?? 0;
            }

            if (totalWeight <= 0)
            {
                return SelectRandom(node);
            }

            int value = Random.Range(0, totalWeight);
            for (int i = 0; i < node.Sequences.Count; i++)
            {
                BossSequenceEntry entry = node.Sequences[i];
                int weight = entry?.Weight ?? 0;
                if (weight <= 0)
                {
                    continue;
                }

                if (value < weight)
                {
                    return entry;
                }

                value -= weight;
            }

            return SelectRandom(node);
        }

        private BossSequenceEntry SelectShuffledBag(BossStateNode node, string nodeKey)
        {
            if (!sequenceBags.TryGetValue(nodeKey, out List<BossSequenceEntry> bag) || bag == null || bag.Count == 0)
            {
                bag = BuildShuffledBag(node.Sequences);
                sequenceBags[nodeKey] = bag;
            }

            BossSequenceEntry entry = bag[^1];
            bag.RemoveAt(bag.Count - 1);
            TrackLast(nodeKey, entry);
            return entry;
        }

        private static List<T> BuildShuffledBag<T>(IReadOnlyList<T> source)
        {
            List<T> bag = new(source);
            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }

            return bag;
        }

        private static BossSequenceEntry GetEntryWrapping(BossStateNode node, int index)
        {
            if (node == null || node.Sequences == null || node.Sequences.Count == 0)
            {
                return null;
            }

            int safeIndex = Mathf.Abs(index) % node.Sequences.Count;
            return node.Sequences[safeIndex];
        }

        private void TrackLast(string nodeKey, BossSequenceEntry entry)
        {
            if (entry?.Sequence != null)
            {
                lastSequences[nodeKey] = entry.Sequence;
            }
        }

        private BossGraphPattern ResolvePhasePattern(
            BossGraphAsset graph,
            BossGraphPhase phase,
            BossActionContext context,
            out BossGraphPatternEntry patternEntry)
        {
            patternEntry = null;
            if (phase == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(phase.OpeningPatternId) && openingPatternsPlayed.Add(phase.PhaseIndex))
            {
                patternEntry = FindPatternEntry(phase, phase.OpeningPatternId);
                return graph.GetPattern(phase.OpeningPatternId);
            }

            // 정상적인 가중치 선택보다 우선한다 — 보스가 "지금 이 패턴을 무조건 써야 한다"고 판단하면
            // (예: Assassin의 단검 개수 조건) 매번(반복적으로) 이 패턴을 강제로 고른다.
            if (!string.IsNullOrWhiteSpace(phase.ForcedPatternId) && context?.Boss?.ShouldUseForcedGraphPatternForRunner() == true)
            {
                patternEntry = FindPatternEntry(phase, phase.ForcedPatternId);
                return graph.GetPattern(phase.ForcedPatternId);
            }

            patternEntry = SelectPattern(phase);
            return graph.GetPattern(patternEntry?.PatternId);
        }

        private bool TryGetPendingSignaturePattern(
            BossGraphAsset graph,
            BossGraphPhase phase,
            BossActionContext context,
            out BossGraphPattern pattern)
        {
            pattern = null;
            if (graph == null
                || phase == null
                || context?.Boss == null
                || context.Boss.CurrentPhaseIndex != phase.PhaseIndex
                || signaturePatternsPlayed.Contains(phase.PhaseIndex)
                || string.IsNullOrWhiteSpace(phase.SignaturePatternId)
                || GetHpRatio(context.Boss) > phase.SignaturePatternHpRatio)
            {
                return false;
            }

            pattern = graph.GetPattern(phase.SignaturePatternId);
            return pattern?.NodeKeys != null && pattern.NodeKeys.Count > 0;
        }

        private BossGraphPatternEntry SelectPattern(BossGraphPhase phase)
        {
            if (phase == null || phase.Patterns == null || phase.Patterns.Count == 0)
            {
                return null;
            }

            return SelectWeightedReadyPattern(phase, true)
                ?? SelectWeightedReadyPattern(phase, false);
        }

        private BossGraphPatternEntry SelectWeightedReadyPattern(
            BossGraphPhase phase,
            bool requireCooldownReady)
        {
            int totalWeight = 0;
            for (int i = 0; i < phase.Patterns.Count; i++)
            {
                BossGraphPatternEntry entry = phase.Patterns[i];
                if (!CanSelectPatternEntry(phase, entry, requireCooldownReady))
                {
                    continue;
                }

                totalWeight += entry.Weight;
            }

            if (totalWeight <= 0)
            {
                return SelectRandomReadyPattern(phase, requireCooldownReady);
            }

            int value = Random.Range(0, totalWeight);
            for (int i = 0; i < phase.Patterns.Count; i++)
            {
                BossGraphPatternEntry entry = phase.Patterns[i];
                if (!CanSelectPatternEntry(phase, entry, requireCooldownReady))
                {
                    continue;
                }

                if (value < entry.Weight)
                {
                    return entry;
                }

                value -= entry.Weight;
            }

            return SelectRandomReadyPattern(phase, requireCooldownReady);
        }

        private BossGraphPatternEntry SelectRandomReadyPattern(
            BossGraphPhase phase,
            bool requireCooldownReady)
        {
            int selectableCount = 0;
            for (int i = 0; i < phase.Patterns.Count; i++)
            {
                if (CanSelectPatternEntry(phase, phase.Patterns[i], requireCooldownReady))
                {
                    selectableCount++;
                }
            }

            if (selectableCount <= 0)
            {
                return null;
            }

            int selectedIndex = Random.Range(0, selectableCount);
            for (int i = 0; i < phase.Patterns.Count; i++)
            {
                BossGraphPatternEntry entry = phase.Patterns[i];
                if (!CanSelectPatternEntry(phase, entry, requireCooldownReady))
                {
                    continue;
                }

                if (selectedIndex == 0)
                {
                    return entry;
                }

                selectedIndex--;
            }

            return null;
        }

        private bool CanSelectPatternEntry(
            BossGraphPhase phase,
            BossGraphPatternEntry entry,
            bool requireCooldownReady)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.PatternId))
            {
                return false;
            }

            // Min Patterns Played는 쿨다운과 달리 완화(fallback)되지 않는 절대 조건이라, requireCooldownReady
            // 값과 상관없이 항상 체크한다.
            int playedCount = patternsPlayedCountByPhase.TryGetValue(phase.PhaseIndex, out int count) ? count : 0;
            if (playedCount < entry.MinPatternsPlayed)
            {
                return false;
            }

            if (!requireCooldownReady)
            {
                return true;
            }

            string key = GetPatternCooldownKey(phase, entry.PatternId);
            return !patternCooldownRemainingCounts.TryGetValue(key, out int remainingCount) || remainingCount <= 0;
        }

        private static BossGraphPatternEntry FindPatternEntry(BossGraphPhase phase, string patternId)
        {
            if (phase?.Patterns == null || string.IsNullOrWhiteSpace(patternId))
            {
                return null;
            }

            for (int i = 0; i < phase.Patterns.Count; i++)
            {
                BossGraphPatternEntry entry = phase.Patterns[i];
                if (entry != null && entry.PatternId == patternId)
                {
                    return entry;
                }
            }

            return null;
        }

        // RunPhasePatternLoop가 정상적으로 패턴을 끝냈을 때, 그리고 GraphBossAI.StopGraphPattern이
        // 실행 중이던 패턴을 캔슬하기 직전에 각각 호출한다. 한쪽에서 이미 등록했으면 아무 것도 안 한다.
        public void RegisterInFlightPatternIfNeeded()
        {
            if (inFlightRegistered)
            {
                return;
            }

            inFlightRegistered = true;
            RegisterCompletedPattern(inFlightPhase, inFlightPatternEntry);
        }

        private void RegisterCompletedPattern(BossGraphPhase phase, BossGraphPatternEntry entry)
        {
            if (phase == null || entry == null || string.IsNullOrWhiteSpace(entry.PatternId))
            {
                return;
            }

            patternsPlayedCountByPhase[phase.PhaseIndex] =
                (patternsPlayedCountByPhase.TryGetValue(phase.PhaseIndex, out int playedCount) ? playedCount : 0) + 1;

            string completedKey = GetPatternCooldownKey(phase, entry.PatternId);
            ReduceOtherPatternCooldowns(phase, completedKey);

            int cooldownPatternCount = entry.CooldownPatternCount;
            if (cooldownPatternCount <= 0)
            {
                patternCooldownRemainingCounts.Remove(completedKey);
                return;
            }

            patternCooldownRemainingCounts[completedKey] = cooldownPatternCount;
        }

        private void ReduceOtherPatternCooldowns(BossGraphPhase phase, string completedKey)
        {
            if (patternCooldownRemainingCounts.Count == 0)
            {
                return;
            }

            string phaseKeyPrefix = $"{phase.PhaseIndex}:";
            List<string> keys = patternCooldownRemainingCounts.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                if (key == completedKey || !key.StartsWith(phaseKeyPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                int nextCount = patternCooldownRemainingCounts[key] - 1;
                if (nextCount <= 0)
                {
                    patternCooldownRemainingCounts.Remove(key);
                }
                else
                {
                    patternCooldownRemainingCounts[key] = nextCount;
                }
            }
        }

        private static string GetPatternCooldownKey(BossGraphPhase phase, string patternId)
        {
            return $"{phase.PhaseIndex}:{patternId}";
        }
    }

    internal sealed class CoroutineStack
    {
        private readonly Stack<IEnumerator> stack = new();

        public CoroutineStack(IEnumerator root)
        {
            if (root != null)
            {
                stack.Push(root);
            }
        }

        public bool MoveNext()
        {
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                if (!current.MoveNext())
                {
                    stack.Pop();
                    continue;
                }

                if (current.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }

                return true;
            }

            return false;
        }
    }

    public readonly struct BossGraphRuntimeSnapshot
    {
        public BossGraphRuntimeSnapshot(string currentNodeId, string edgeFromNodeId, string edgeToNodeId)
        {
            CurrentNodeId = currentNodeId;
            EdgeFromNodeId = edgeFromNodeId;
            EdgeToNodeId = edgeToNodeId;
        }

        public string CurrentNodeId { get; }
        public string EdgeFromNodeId { get; }
        public string EdgeToNodeId { get; }
    }

    public static class BossGraphRuntimeState
    {
        private static readonly Dictionary<int, BossGraphRuntimeSnapshot> snapshots = new();

        public static void SetCurrentNode(BossGraphAsset graph, string nodeId, string edgeFromNodeId = null)
        {
            if (graph == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            string fromNodeId = !string.IsNullOrWhiteSpace(edgeFromNodeId) && edgeFromNodeId != nodeId
                ? edgeFromNodeId
                : null;
            snapshots[graph.GetInstanceID()] = new BossGraphRuntimeSnapshot(nodeId, fromNodeId, fromNodeId != null ? nodeId : null);
        }

        public static bool TryGetSnapshot(BossGraphAsset graph, out BossGraphRuntimeSnapshot snapshot)
        {
            snapshot = default;
            return graph != null && snapshots.TryGetValue(graph.GetInstanceID(), out snapshot);
        }

        public static void Clear(BossGraphAsset graph)
        {
            if (graph != null)
            {
                snapshots.Remove(graph.GetInstanceID());
            }
        }
    }
}
