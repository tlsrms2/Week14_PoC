using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    public sealed class BossGraphRunner
    {
        private const int MaxImmediateTransitionsPerFrame = 16;
        private readonly Dictionary<string, int> nextSequenceIndexes = new();
        private readonly Dictionary<string, AttackSequenceAsset> lastSequences = new();
        private string currentNodeId;

        public void Reset()
        {
            nextSequenceIndexes.Clear();
            lastSequences.Clear();
            currentNodeId = null;
        }

        public IEnumerator RunLoop(BossGraphAsset graph, BossActionContext context)
        {
            if (graph == null || context == null)
            {
                yield break;
            }

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
                BossSequenceEntry entry = SelectSequence(node);
                AttackSequenceAsset sequence = entry?.Sequence;
                if (sequence == null)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                yield return sequence.Execute(context);
                yield return context.ApplyPendingEnrageIfAny();
                context.Stop();
                yield return context.WaitSeconds(GetRecoverySeconds(node));
                TryApplyTransition(graph, context, node, true);
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
                if (transition == null || transition.FromNodeId != currentNode.NodeId)
                {
                    continue;
                }

                BossStateNode targetNode = graph.GetNode(transition.ToNodeId);
                if (targetNode == null || !IsTransitionConditionMet(transition, context, sequenceEnded))
                {
                    continue;
                }

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
            if (node.Sequences.Count <= 1 || !lastSequences.TryGetValue(nodeKey, out AttackSequenceAsset lastSequence))
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

        private static float GetRecoverySeconds(BossStateNode node)
        {
            if (node == null)
            {
                return 0f;
            }

            return Random.Range(Mathf.Max(0f, node.MinRecoverySeconds), Mathf.Max(node.MinRecoverySeconds, node.MaxRecoverySeconds));
        }

        private void TrackLast(string nodeKey, BossSequenceEntry entry)
        {
            if (entry?.Sequence != null)
            {
                lastSequences[nodeKey] = entry.Sequence;
            }
        }
    }
}
