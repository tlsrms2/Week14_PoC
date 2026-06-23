using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    public sealed class BossGraphRunner
    {
        private const int MaxImmediateTransitionsPerFrame = 16;
        private readonly Dictionary<string, int> nextSequenceIndexes = new();
        private readonly Dictionary<string, BossGraphActionAsset> lastSequences = new();
        private readonly Dictionary<int, int> nextPatternIndexes = new();
        private readonly Dictionary<int, string> lastPatterns = new();
        private string currentNodeId;
        private string previousRuntimeNodeId;
        private BossGraphAsset activeGraph;

        public void Reset()
        {
            BossGraphRuntimeState.Clear(activeGraph);
            nextSequenceIndexes.Clear();
            lastSequences.Clear();
            nextPatternIndexes.Clear();
            lastPatterns.Clear();
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
                    context.SetCurrentNodeId(node.NodeId);
                    if (directAction != null)
                    {
                        yield return directAction.Execute(context);
                    }
                    else
                    {
                        yield return sequence.Execute(context);
                    }

                    yield return context.ApplyPendingEnrageIfAny();
                    context.Stop();
                }
                finally
                {
                    context.SetCurrentNodeId(null);
                    context.ClearPatternScopedBossChildAims();
                }

                TryApplyTransition(graph, context, node, true);
            }
        }

        private IEnumerator RunPhasePatternLoop(BossGraphAsset graph, BossActionContext context)
        {
            while (true)
            {
                BossGraphPhase phase = graph.GetPhase(context.Boss.CurrentPhaseIndex);
                BossGraphPatternEntry entry = SelectPattern(phase);
                BossGraphPattern pattern = graph.GetPattern(entry?.PatternId);
                IReadOnlyList<string> nodeKeys = pattern?.NodeKeys;
                if (nodeKeys == null || nodeKeys.Count == 0)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                yield return ExecutePattern(graph, pattern, context);
                yield return context.ApplyPendingEnrageIfAny();
                context.Stop();
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
                for (int i = 0; i < nodeKeys.Count; i++)
                {
                    BossStateNode node = graph.GetNode(nodeKeys[i]);
                    BossAction action = node?.Action;
                    if (action == null)
                    {
                        continue;
                    }

                    try
                    {
                        BossStateNode previousNode = i > 0 ? graph.GetNode(nodeKeys[i - 1]) : null;
                        string previousNodeId = previousNode?.NodeId;
                        BossGraphRuntimeState.SetCurrentNode(graph, node.NodeId, previousNodeId);
                        context.SetCurrentNodeId(node.NodeId);
                        yield return action.Execute(context);
                    }
                    finally
                    {
                        context.SetCurrentNodeId(null);
                    }

                    yield return context.ApplyPendingEnrageIfAny();
                    context.Stop();
                }
            }
            finally
            {
                context.ClearPatternScopedBossChildAims();
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

            for (int i = 0; i < graph.Transitions.Count; i++)
            {
                BossTransition transition = graph.Transitions[i];
                if (transition == null || !transition.IsFromNode(currentNode))
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
                BossTransitionConditionType.EnragePhaseEquals => boss.CurrentEnragePhase == transition.PhaseIndex,
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

        private BossGraphPatternEntry SelectPattern(BossGraphPhase phase)
        {
            if (phase == null || phase.Patterns == null || phase.Patterns.Count == 0)
            {
                return null;
            }

            int phaseKey = phase.PhaseIndex;
            return phase.SelectionMode switch
            {
                BossSequenceSelectionMode.Random => SelectRandomPattern(phase),
                BossSequenceSelectionMode.RandomNoRepeat => SelectRandomNoRepeatPattern(phase, phaseKey),
                BossSequenceSelectionMode.WeightedRandom => SelectWeightedRandomPattern(phase),
                _ => SelectSequentialPattern(phase, phaseKey)
            };
        }

        private BossGraphPatternEntry SelectSequentialPattern(BossGraphPhase phase, int phaseKey)
        {
            int index = nextPatternIndexes.TryGetValue(phaseKey, out int nextIndex) ? nextIndex : 0;
            BossGraphPatternEntry entry = GetPatternEntryWrapping(phase, index);
            nextPatternIndexes[phaseKey] = index + 1;
            TrackLastPattern(phaseKey, entry);
            return entry;
        }

        private static BossGraphPatternEntry SelectRandomPattern(BossGraphPhase phase)
        {
            return GetPatternEntryWrapping(phase, Random.Range(0, phase.Patterns.Count));
        }

        private BossGraphPatternEntry SelectRandomNoRepeatPattern(BossGraphPhase phase, int phaseKey)
        {
            if (phase.Patterns.Count <= 1 || !lastPatterns.TryGetValue(phaseKey, out string lastPatternId))
            {
                BossGraphPatternEntry firstEntry = SelectRandomPattern(phase);
                TrackLastPattern(phaseKey, firstEntry);
                return firstEntry;
            }

            for (int attempts = 0; attempts < 12; attempts++)
            {
                BossGraphPatternEntry candidate = SelectRandomPattern(phase);
                if (candidate != null && candidate.PatternId != lastPatternId)
                {
                    TrackLastPattern(phaseKey, candidate);
                    return candidate;
                }
            }

            return SelectSequentialPattern(phase, phaseKey);
        }

        private static BossGraphPatternEntry SelectWeightedRandomPattern(BossGraphPhase phase)
        {
            int totalWeight = 0;
            for (int i = 0; i < phase.Patterns.Count; i++)
            {
                totalWeight += phase.Patterns[i]?.Weight ?? 0;
            }

            if (totalWeight <= 0)
            {
                return SelectRandomPattern(phase);
            }

            int value = Random.Range(0, totalWeight);
            for (int i = 0; i < phase.Patterns.Count; i++)
            {
                BossGraphPatternEntry entry = phase.Patterns[i];
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

            return SelectRandomPattern(phase);
        }

        private static BossGraphPatternEntry GetPatternEntryWrapping(BossGraphPhase phase, int index)
        {
            if (phase == null || phase.Patterns == null || phase.Patterns.Count == 0)
            {
                return null;
            }

            int safeIndex = Mathf.Abs(index) % phase.Patterns.Count;
            return phase.Patterns[safeIndex];
        }

        private void TrackLastPattern(int phaseKey, BossGraphPatternEntry entry)
        {
            if (entry != null && !string.IsNullOrWhiteSpace(entry.PatternId))
            {
                lastPatterns[phaseKey] = entry.PatternId;
            }
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
