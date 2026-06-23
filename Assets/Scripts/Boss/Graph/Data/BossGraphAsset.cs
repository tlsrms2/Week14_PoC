using System;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [CreateAssetMenu(menuName = "Week14/Boss/Boss Graph", fileName = "BossGraph")]
    public sealed class BossGraphAsset : ScriptableObject
    {
        [SerializeField] private string startNodeId = "Phase1";
        [SerializeField] private BossGraphReferenceSettings references = new();
        [SerializeField] private List<BossStateNode> stateNodes = new()
        {
            new BossStateNode()
        };
        [SerializeField] private List<BossGraphPattern> patterns = new();
        [SerializeField] private List<BossGraphPhase> phases = new();
        [SerializeField] private List<BossTransition> transitions = new();

        public BossGraphReferenceSettings References => references;
        public CombatEffectData EffectData => references != null ? references.EffectData : null;
        public BossColorSettings ColorSettings => references != null ? references.ColorSettings : null;
        public BossGraphActionCategoryAsset ActionCategories => references != null ? references.ActionCategories : null;
        public IReadOnlyList<BossStateNode> StateNodes => stateNodes;
        public IReadOnlyList<BossGraphPattern> Patterns => patterns;
        public IReadOnlyList<BossGraphPhase> Phases => phases;
        public IReadOnlyList<BossTransition> Transitions => transitions;
        public bool UsesPhasePatternLayout => phases != null && phases.Count > 0;

        public BossStateNode GetStartNode()
        {
            BossStateNode node = FindNode(startNodeId);
            return node ?? (stateNodes != null && stateNodes.Count > 0 ? stateNodes[0] : null);
        }

        public BossStateNode GetNodeForPhase(int phaseIndex)
        {
            if (stateNodes == null || stateNodes.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < stateNodes.Count; i++)
            {
                BossStateNode node = stateNodes[i];
                if (node != null && node.PhaseIndex == phaseIndex)
                {
                    return node;
                }
            }

            return GetStartNode();
        }

        public BossStateNode GetNode(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || stateNodes == null)
            {
                return null;
            }

            for (int i = 0; i < stateNodes.Count; i++)
            {
                BossStateNode node = stateNodes[i];
                if (node != null && node.NodeId == nodeId)
                {
                    return node;
                }
            }

            return null;
        }

        public BossGraphPattern GetPattern(string patternId)
        {
            if (string.IsNullOrWhiteSpace(patternId) || patterns == null)
            {
                return null;
            }

            for (int i = 0; i < patterns.Count; i++)
            {
                BossGraphPattern pattern = patterns[i];
                if (pattern != null && pattern.PatternId == patternId)
                {
                    return pattern;
                }
            }

            return null;
        }

        public BossGraphPhase GetPhase(int phaseIndex)
        {
            if (phases == null || phases.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < phases.Count; i++)
            {
                BossGraphPhase phase = phases[i];
                if (phase != null && phase.PhaseIndex == phaseIndex)
                {
                    return phase;
                }
            }

            return phases[0];
        }

        private BossStateNode FindNode(string nodeId)
        {
            return GetNode(nodeId);
        }
    }

    [Serializable]
    public sealed class BossGraphReferenceSettings
    {
        [SerializeField] private CombatEffectData effectData;
        [SerializeField] private BossColorSettings colorSettings;
        [SerializeField] private BossGraphActionCategoryAsset actionCategories;

        public CombatEffectData EffectData => effectData;
        public BossColorSettings ColorSettings => colorSettings;
        public BossGraphActionCategoryAsset ActionCategories => actionCategories;
    }

    public enum BossSequenceSelectionMode
    {
        Sequential,
        Random,
        RandomNoRepeat,
        WeightedRandom
    }

    public enum BossTransitionConditionType
    {
        SequenceEnded,
        HpRatioLessOrEqual,
        PhaseIndexEquals,
        PlayerDetected,
        HpEmpty,
        HpNotEmpty,
        Staggered,
        NotStaggered,
        ExecutionLocked,
        ExecutionPaused,
        EnragePhaseEquals,
        LivesLessOrEqual
    }

    [Serializable]
    public sealed class BossStateNode
    {
        [SerializeField] private string nodeId = "Phase1";
        [SerializeField] private BossGraphNodeKind nodeKind = BossGraphNodeKind.Attack;
        [SerializeField, Min(0)] private int phaseIndex;
        [SerializeField] private BossSequenceSelectionMode selectionMode;
        [SerializeField] private List<BossSequenceEntry> sequences = new();
        [SerializeField] private Vector2 editorPosition;

        public string NodeId => nodeId;
        public BossGraphNodeKind NodeKind => nodeKind;
        public int PhaseIndex => phaseIndex;
        public BossSequenceSelectionMode SelectionMode => selectionMode;
        public IReadOnlyList<BossSequenceEntry> Sequences => sequences;
        public BossGraphActionAsset ActionSequence => sequences != null && sequences.Count > 0 ? sequences[0]?.Sequence : null;
        public Vector2 EditorPosition => editorPosition;
    }

    [Serializable]
    public sealed class BossSequenceEntry
    {
        [SerializeField] private BossGraphActionAsset sequence;
        [SerializeField, Min(0)] private int weight = 1;

        public BossGraphActionAsset Sequence => sequence;
        public int Weight => Mathf.Max(0, weight);
    }

    [Serializable]
    public sealed class BossGraphPattern
    {
        [SerializeField] private string patternId = "Pattern1";
        [SerializeField] private List<string> nodeIds = new();

        public string PatternId => patternId;
        public IReadOnlyList<string> NodeIds => nodeIds;
    }

    [Serializable]
    public sealed class BossGraphPhase
    {
        [SerializeField, Min(0)] private int phaseIndex;
        [SerializeField] private BossSequenceSelectionMode selectionMode;
        [SerializeField, Min(0f)] private float patternIntervalSeconds;
        [SerializeField] private List<BossGraphPatternEntry> patterns = new();

        public int PhaseIndex => phaseIndex;
        public BossSequenceSelectionMode SelectionMode => selectionMode;
        public float PatternIntervalSeconds => Mathf.Max(0f, patternIntervalSeconds);
        public IReadOnlyList<BossGraphPatternEntry> Patterns => patterns;
    }

    [Serializable]
    public sealed class BossGraphPatternEntry
    {
        [SerializeField] private string patternId;
        [SerializeField, Min(0)] private int weight = 1;

        public string PatternId => patternId;
        public int Weight => Mathf.Max(0, weight);
    }

    [Serializable]
    public sealed class BossTransition
    {
        [SerializeField] private string fromNodeId;
        [SerializeField] private string toNodeId;
        [SerializeField] private BossTransitionConditionType conditionType;
        [SerializeField] private float threshold;
        [SerializeField] private int phaseIndex;

        public string FromNodeId => fromNodeId;
        public string ToNodeId => toNodeId;
        public BossTransitionConditionType ConditionType => conditionType;
        public float Threshold => threshold;
        public int PhaseIndex => phaseIndex;
    }
}
