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
        private string currentNodeId;
        private string previousRuntimeNodeId;
        private BossGraphAsset activeGraph;

        public void Reset()
        {
            BossGraphRuntimeState.Clear(activeGraph);
            nextSequenceIndexes.Clear();
            lastSequences.Clear();
            sequenceBags.Clear();
            patternCooldownRemainingCounts.Clear();
            openingPatternsPlayed.Clear();
            signaturePatternsPlayed.Clear();
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
                if (phase != null && phase.PatternIntervalSeconds > 0f)
                {
                    yield return context.WaitSeconds(phase.PatternIntervalSeconds);
                    context.Stop();
                }
            }
        }

        private IEnumerator RunPhasePatternLoop(BossGraphAsset graph, BossActionContext context)
        {
            while (true)
            {
                BossGraphPhase phase = graph.GetPhase(context.Boss.CurrentPhaseIndex);
                BossGraphPattern pattern = ResolvePhasePattern(graph, phase, out BossGraphPatternEntry patternEntry);
                IReadOnlyList<string> nodeKeys = pattern?.NodeKeys;
                if (nodeKeys == null || nodeKeys.Count == 0)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                yield return ExecutePattern(graph, pattern, context);
                RegisterCompletedPattern(phase, patternEntry);
                context.Stop();
                if (TryGetPendingSignaturePattern(graph, phase, context, out BossGraphPattern signaturePattern))
                {
                    signaturePatternsPlayed.Add(phase.PhaseIndex);
                    yield return ExecutePattern(graph, signaturePattern, context);
                    context.Stop();
                }

                if (phase.PatternIntervalSeconds > 0f)
                {
                    yield return context.WaitSeconds(phase.PatternIntervalSeconds);
                    context.Stop();
                }
            }
        }

        private static IEnumerator ExecutePattern(
            BossGraphAsset graph,
            BossGraphPattern pattern,
            BossActionContext context)
        {
            try
            {
                IReadOnlyList<string> nodeKeys = pattern.NodeKeys;
                Dictionary<string, List<BossStateNode>> parallelGroups = BuildPatternParallelGroups(graph, nodeKeys);
                List<List<BossStateNode>> executionGroups = BuildPatternExecutionGroups(graph, nodeKeys, parallelGroups);
                int[] conductorOutlineReleaseCounts = new int[executionGroups.Count];
                HashSet<string> skippedNodeKeys = new(StringComparer.Ordinal);
                string previousNodeId = null;
                for (int i = 0; i < executionGroups.Count; i++)
                {
                    List<BossStateNode> group = executionGroups[i];
                    List<BossStateNode> activeGroup = group
                        .Where(node => node != null && !skippedNodeKeys.Contains(GetRuntimeNodeKey(node)))
                        .ToList();
                    if (activeGroup.Count > 0)
                    {
                        yield return ExecutePatternNodeGroup(graph, activeGroup, previousNodeId, context);
                        ApplyHackerFireWireBranchSelection(graph, activeGroup, context, skippedNodeKeys);
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

            if (sourceNode.Action is not HackerFireWireBranchAction || graph.ParallelEdges == null)
            {
                yield break;
            }

            for (int i = 0; i < graph.ParallelEdges.Count; i++)
            {
                BossParallelEdge edge = graph.ParallelEdges[i];
                if (edge != null && edge.IsFromNode(sourceNode))
                {
                    string targetNodeKey = GetRuntimeNodeKey(graph.GetNode(edge.ToNodeKey));
                    if (!string.IsNullOrWhiteSpace(targetNodeKey))
                    {
                        yield return targetNodeKey;
                    }
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
                    if (edge == null || edgeSource?.Action is not HackerFireWireBranchAction || edgeTarget != targetNode)
                    {
                        continue;
                    }

                    hasIncomingConnection = true;
                    if (!skippedNodeKeys.Contains(GetRuntimeNodeKey(edgeSource)))
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
            BossActionContext context)
        {
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
                    routines.Add(ExecuteSinglePatternNode(graph, node, previousNodeId, context));
                }
            }

            if (routines.Count == 0)
            {
                yield break;
            }

            try
            {
                yield return RunParallelRoutines(routines);
            }
            finally
            {
                ClearConductorCueOverlayEarlyStart(nodes);
            }
        }

        private static IEnumerator ExecuteSinglePatternNode(
            BossGraphAsset graph,
            BossStateNode node,
            string previousNodeId,
            BossActionContext context)
        {
            try
            {
                BossGraphRuntimeState.SetCurrentNode(graph, node.NodeId, previousNodeId);
                context.BeginNodeExecution(node.NodeId);
                if (node.Action is ConductorConductingCueAction cue
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
                    yield return node.Action.Execute(context);
                }
            }
            finally
            {
                context.EndNodeExecution();
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
                if (cue == null
                    || !cue.TryGetTotalSeconds(conductor, out float cueSeconds)
                    || !TryGetParallelGroupActionDuration(nodes, cueNode, context, out float actionSeconds))
                {
                    continue;
                }

                float delaySeconds = Mathf.Max(0f, actionSeconds - cueSeconds);
                plans.Add(new ConductorCueOverlayPlan(cueNode, cue, delaySeconds));
            }

            return plans;
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

            if (plan.DelaySeconds > 0f)
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
            }
        }

        private static ConductorConductingCueAction GetConductorCueAction(BossStateNode node)
        {
            return node?.Action as ConductorConductingCueAction;
        }

        private readonly struct ConductorCueOverlayPlan
        {
            public ConductorCueOverlayPlan(
                BossStateNode cueNode,
                ConductorConductingCueAction cue,
                float delaySeconds)
            {
                CueNode = cueNode;
                Cue = cue;
                DelaySeconds = Mathf.Max(0f, delaySeconds);
            }

            public BossStateNode CueNode { get; }
            public ConductorConductingCueAction Cue { get; }
            public float DelaySeconds { get; }
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

        private void RegisterCompletedPattern(BossGraphPhase phase, BossGraphPatternEntry entry)
        {
            if (phase == null || entry == null || string.IsNullOrWhiteSpace(entry.PatternId))
            {
                return;
            }

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
