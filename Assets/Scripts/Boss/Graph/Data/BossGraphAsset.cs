using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Combat;

namespace Week14.Enemy
{
    [CreateAssetMenu(menuName = "Week14/Boss/Boss Graph", fileName = "BossGraph")]
    public sealed class BossGraphAsset : ScriptableObject
    {
        [FormerlySerializedAs("references")]
        [SerializeField] private BossGraphReferenceSettings referenceSettings = new();
        [SerializeField] private List<BossStateNode> stateNodes = new()
        {
            new BossStateNode()
        };
        [SerializeField] private List<BossGraphPattern> patterns = new();
        [SerializeField] private List<BossGraphPhase> phases = new();
        [SerializeField] private List<BossTransition> transitions = new();
        [SerializeField] private List<BossParallelEdge> parallelEdges = new();
        [SerializeField] private bool debugForceSinglePattern;
        [SerializeField] private string debugForcedPatternId;

        public BossGraphReferenceSettings References => referenceSettings;
        public CombatEffectData EffectData => referenceSettings != null ? referenceSettings.EffectData : null;
        public BossColorSettings ColorSettings => referenceSettings != null ? referenceSettings.ColorSettings : null;
        public BossGraphActionCategoryAsset ActionCategories => referenceSettings != null ? referenceSettings.ActionCategories : null;
        public IReadOnlyList<BossStateNode> StateNodes => stateNodes;
        public IReadOnlyList<BossGraphPattern> Patterns => patterns;
        public IReadOnlyList<BossGraphPhase> Phases => phases;
        public IReadOnlyList<BossTransition> Transitions => transitions;
        public IReadOnlyList<BossParallelEdge> ParallelEdges => parallelEdges;
        public bool DebugForceSinglePattern => debugForceSinglePattern;
        public string DebugForcedPatternId => debugForcedPatternId;
        public bool UsesPhasePatternLayout => phases != null && phases.Count > 0;

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

            return stateNodes[0];
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
                if (node != null && (node.NodeGuid == nodeId || node.NodeId == nodeId))
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

        private void OnValidate()
        {
            EnsureNodeGuids();
        }

        private void EnsureNodeGuids()
        {
            if (stateNodes == null)
            {
                return;
            }

            HashSet<string> existingGuids = new();
            Dictionary<string, string> nodeIdToGuid = new(StringComparer.Ordinal);
            for (int i = 0; i < stateNodes.Count; i++)
            {
                BossStateNode node = stateNodes[i];
                node?.EnsureNodeGuid(existingGuids);
                if (node != null && !string.IsNullOrWhiteSpace(node.NodeId) && !string.IsNullOrWhiteSpace(node.NodeGuid))
                {
                    nodeIdToGuid[node.NodeId] = node.NodeGuid;
                }
            }

            if (patterns != null)
            {
                for (int i = 0; i < patterns.Count; i++)
                {
                    patterns[i]?.EnsureNodeGuids(nodeIdToGuid);
                }
            }

            if (transitions != null)
            {
                for (int i = 0; i < transitions.Count; i++)
                {
                    transitions[i]?.EnsureNodeGuids(nodeIdToGuid);
                }
            }

            if (parallelEdges != null)
            {
                for (int i = 0; i < parallelEdges.Count; i++)
                {
                    parallelEdges[i]?.EnsureNodeGuids(nodeIdToGuid);
                }
            }
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
        WeightedRandom,
        ShuffledBag
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
        LivesLessOrEqual = 11
    }

    [Serializable]
    public sealed class BossStateNode
    {
        [SerializeField] private string nodeId = "Phase1";
        [SerializeField, HideInInspector] private string nodeGuid;
        [SerializeField] private BossGraphNodeKind nodeKind = BossGraphNodeKind.Attack;
        [SerializeField, Min(0)] private int phaseIndex;
        [SerializeField] private BossSequenceSelectionMode selectionMode;
        [SerializeReference] private BossAction action;
        [SerializeField] private List<BossSequenceEntry> sequences = new();
        [SerializeField] private Vector2 editorPosition;

        public string NodeId => nodeId;
        public string NodeGuid => nodeGuid;
        public BossGraphNodeKind NodeKind => nodeKind;
        public int PhaseIndex => phaseIndex;
        public BossSequenceSelectionMode SelectionMode => selectionMode;
        public BossAction Action => action ?? ActionSequence?.Action;
        public bool HasDirectAction => action != null;
        public IReadOnlyList<BossSequenceEntry> Sequences => sequences;
        public BossGraphActionAsset ActionSequence => sequences != null && sequences.Count > 0 ? sequences[0]?.Sequence : null;
        public Vector2 EditorPosition => editorPosition;

        internal void EnsureNodeGuid(ISet<string> existingGuids)
        {
            if (existingGuids == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(nodeGuid) || existingGuids.Contains(nodeGuid))
            {
                nodeGuid = Guid.NewGuid().ToString("N");
            }

            existingGuids.Add(nodeGuid);
        }
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
        [SerializeField, HideInInspector] private List<string> nodeGuids = new();
        [SerializeField] private List<string> nodeIds = new();

        public string PatternId => patternId;
        public IReadOnlyList<string> NodeKeys => nodeGuids != null && nodeGuids.Count > 0 ? nodeGuids : nodeIds;
        public IReadOnlyList<string> NodeGuids => nodeGuids;
        public IReadOnlyList<string> NodeIds => nodeIds;

        internal void EnsureNodeGuids(IReadOnlyDictionary<string, string> nodeIdToGuid)
        {
            if (nodeIds == null || nodeIdToGuid == null)
            {
                return;
            }

            nodeGuids ??= new List<string>();
            nodeGuids.Clear();
            for (int i = 0; i < nodeIds.Count; i++)
            {
                string nodeId = nodeIds[i];
                if (!string.IsNullOrWhiteSpace(nodeId) && nodeIdToGuid.TryGetValue(nodeId, out string nodeGuid))
                {
                    nodeGuids.Add(nodeGuid);
                }
            }
        }
    }

    [Serializable]
    public sealed class BossGraphPhase
    {
        [SerializeField, Min(0)] private int phaseIndex;
        [SerializeField, HideInInspector] private BossSequenceSelectionMode selectionMode;
        [SerializeField, Min(0f)] private float patternIntervalSeconds;
        [SerializeField] private bool bossCanFlyOverGround;
        [SerializeField] private bool minionsCanFlyOverGround;
        [SerializeField] private string openingPatternId;
        [SerializeField] private List<BossGraphPatternEntry> patterns = new();

        public int PhaseIndex => phaseIndex;
        public BossSequenceSelectionMode SelectionMode => selectionMode;
        public float PatternIntervalSeconds => Mathf.Max(0f, patternIntervalSeconds);
        public bool BossCanFlyOverGround => bossCanFlyOverGround;
        public bool MinionsCanFlyOverGround => minionsCanFlyOverGround;
        public string OpeningPatternId => openingPatternId;
        public IReadOnlyList<BossGraphPatternEntry> Patterns => patterns;
    }

    [Serializable]
    public sealed class BossGraphPatternEntry : ISerializationCallbackReceiver
    {
        [SerializeField] private string patternId;
        [SerializeField, Min(0)] private int weight = 1;
        [FormerlySerializedAs("cooldownSeconds")]
        [SerializeField, HideInInspector] private float legacyCooldownSeconds = -1f;
        [SerializeField, Min(0)] private int cooldownPatternCount;

        public string PatternId => patternId;
        public int Weight => Mathf.Max(0, weight);
        public int CooldownPatternCount => Mathf.Max(0, cooldownPatternCount);

        public void OnBeforeSerialize()
        {
            legacyCooldownSeconds = -1f;
        }

        public void OnAfterDeserialize()
        {
            if (legacyCooldownSeconds <= 0f || cooldownPatternCount > 0)
            {
                return;
            }

            cooldownPatternCount = Mathf.CeilToInt(legacyCooldownSeconds);
            legacyCooldownSeconds = -1f;
        }
    }

    [Serializable]
    public sealed class BossTransition
    {
        [SerializeField, HideInInspector] private string fromNodeGuid;
        [SerializeField, HideInInspector] private string toNodeGuid;
        [SerializeField] private string fromNodeId;
        [SerializeField] private string toNodeId;
        [SerializeField] private BossTransitionConditionType conditionType;
        [SerializeField] private float threshold;
        [SerializeField] private int phaseIndex;

        public string FromNodeKey => !string.IsNullOrWhiteSpace(fromNodeGuid) ? fromNodeGuid : fromNodeId;
        public string ToNodeKey => !string.IsNullOrWhiteSpace(toNodeGuid) ? toNodeGuid : toNodeId;
        public string FromNodeGuid => fromNodeGuid;
        public string ToNodeGuid => toNodeGuid;
        public string FromNodeId => fromNodeId;
        public string ToNodeId => toNodeId;
        public BossTransitionConditionType ConditionType => conditionType;
        public float Threshold => threshold;
        public int PhaseIndex => phaseIndex;

        public bool IsFromNode(BossStateNode node)
        {
            if (node == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(fromNodeGuid)
                ? fromNodeGuid == node.NodeGuid
                : fromNodeId == node.NodeId;
        }

        internal void EnsureNodeGuids(IReadOnlyDictionary<string, string> nodeIdToGuid)
        {
            if (nodeIdToGuid == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(fromNodeId) && nodeIdToGuid.TryGetValue(fromNodeId, out string nextFromGuid))
            {
                fromNodeGuid = nextFromGuid;
            }

            if (!string.IsNullOrWhiteSpace(toNodeId) && nodeIdToGuid.TryGetValue(toNodeId, out string nextToGuid))
            {
                toNodeGuid = nextToGuid;
            }
        }
    }

    [Serializable]
    public sealed class BossParallelEdge
    {
        [SerializeField, HideInInspector] private string fromNodeGuid;
        [SerializeField, HideInInspector] private string toNodeGuid;
        [SerializeField] private string fromNodeId;
        [SerializeField] private string toNodeId;
        [SerializeField, HideInInspector, Min(0)] private int laneIndex;
        [SerializeField, HideInInspector, Min(0)] private int targetLaneIndex;

        public string FromNodeKey => !string.IsNullOrWhiteSpace(fromNodeGuid) ? fromNodeGuid : fromNodeId;
        public string ToNodeKey => !string.IsNullOrWhiteSpace(toNodeGuid) ? toNodeGuid : toNodeId;
        public string FromNodeGuid => fromNodeGuid;
        public string ToNodeGuid => toNodeGuid;
        public string FromNodeId => fromNodeId;
        public string ToNodeId => toNodeId;
        public int LaneIndex => Mathf.Clamp(laneIndex, 0, 0);
        public int TargetLaneIndex => Mathf.Clamp(targetLaneIndex, 0, 0);

        public bool IsFromNode(BossStateNode node)
        {
            if (node == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(fromNodeGuid)
                ? fromNodeGuid == node.NodeGuid
                : fromNodeId == node.NodeId;
        }

        internal void EnsureNodeGuids(IReadOnlyDictionary<string, string> nodeIdToGuid)
        {
            if (nodeIdToGuid == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(fromNodeId) && nodeIdToGuid.TryGetValue(fromNodeId, out string nextFromGuid))
            {
                fromNodeGuid = nextFromGuid;
            }

            if (!string.IsNullOrWhiteSpace(toNodeId) && nodeIdToGuid.TryGetValue(toNodeId, out string nextToGuid))
            {
                toNodeGuid = nextToGuid;
            }
        }
    }
}
