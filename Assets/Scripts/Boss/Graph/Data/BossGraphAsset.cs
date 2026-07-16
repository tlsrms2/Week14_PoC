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
        [SerializeField] private List<string> defaultPatternIds = new();
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
        public IReadOnlyList<string> DefaultPatternIds => defaultPatternIds;
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
            ValidateGraphAwareActions();
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

        private void ValidateGraphAwareActions()
        {
            if (stateNodes == null)
            {
                return;
            }

            for (int i = 0; i < stateNodes.Count; i++)
            {
                BossStateNode node = stateNodes[i];
                if (node?.Action is IBossGraphValidatedAction action)
                {
                    action.OnGraphValidated(this, node);
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
        [SerializeField, Min(0)] private int phaseMaxHp;
        [SerializeField, Min(0f)] private float patternIntervalSeconds;
        [SerializeField, Min(0f)] private float intervalMoveSpeed;
        [SerializeField, Min(0f)] private float initialPatternDelaySeconds;
        [SerializeField] private bool bossCanFlyOverGround;
        [SerializeField] private bool minionsCanFlyOverGround;
        [SerializeField] private string openingPatternId;
        [SerializeField] private string signaturePatternId;
        [SerializeField, Range(0f, 100f)] private float signaturePatternHpPercent = 50f;
        [SerializeField] private string forcedPatternId;
        [SerializeField] private List<BossGraphPatternEntry> patterns = new();

        public int PhaseIndex => phaseIndex;
        public BossSequenceSelectionMode SelectionMode => selectionMode;
        public int PhaseMaxHp => Mathf.Max(0, phaseMaxHp);
        public float PatternIntervalSeconds => Mathf.Max(0f, patternIntervalSeconds);
        // 0보다 크면 PatternIntervalSeconds 대기 동안 가만히 서 있는 대신, 대기 시작 시점에 뽑은
        // 랜덤한 한 방향으로 이 속도만큼 천천히 이동한다(EnemyTimeScale의 영향을 그대로 받는다).
        // 0이면 기존처럼 가만히 서서 대기한다.
        public float IntervalMoveSpeed => Mathf.Max(0f, intervalMoveSpeed);
        // 이 페이즈에 처음 진입해서 첫 패턴을 고르기 전까지 한 번만 대기하는 시간이다. 패턴과 패턴
        // 사이에 매번 적용되는 PatternIntervalSeconds와 달리, 페이즈 진입 후 딱 한 번만 적용된다.
        public float InitialPatternDelaySeconds => Mathf.Max(0f, initialPatternDelaySeconds);
        public bool BossCanFlyOverGround => bossCanFlyOverGround;
        public bool MinionsCanFlyOverGround => minionsCanFlyOverGround;
        public string OpeningPatternId => openingPatternId;
        public string SignaturePatternId => signaturePatternId;
        public float SignaturePatternHpRatio => Mathf.Clamp(signaturePatternHpPercent, 0f, 100f) * 0.01f;
        // 보스의 ShouldUseForcedGraphPattern()이 true를 반환하는 동안, 정상적인 가중치 선택 대신
        // 다음 패턴으로 무조건 이 패턴을 쓴다(예: Assassin - 단검이 일정 개수 이상 쌓였을 때).
        public string ForcedPatternId => forcedPatternId;
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
        [SerializeField, Min(0)] private int minPatternsPlayed;

        public string PatternId => patternId;
        public int Weight => Mathf.Max(0, weight);
        public int CooldownPatternCount => Mathf.Max(0, cooldownPatternCount);
        // 이 페이즈에서 (이 패턴 자신을 포함해) 총 몇 개의 패턴이 먼저 끝나야 이 패턴이 뽑힐 자격이
        // 생기는지를 나타낸다. cooldownPatternCount(반복 억제, 조건이 안 맞으면 완화될 수 있음)와 달리
        // 이건 최초 등장을 늦추는 절대 조건이라 완화되지 않는다.
        public int MinPatternsPlayed => Mathf.Max(0, minPatternsPlayed);

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
        [SerializeField, HideInInspector, Min(0)] private int fromOutputPortIndex;
        [SerializeField] private BossTransitionConditionType conditionType;
        [SerializeField] private float threshold;
        [SerializeField] private int phaseIndex;

        public string FromNodeKey => !string.IsNullOrWhiteSpace(fromNodeGuid) ? fromNodeGuid : fromNodeId;
        public string ToNodeKey => !string.IsNullOrWhiteSpace(toNodeGuid) ? toNodeGuid : toNodeId;
        public string FromNodeGuid => fromNodeGuid;
        public string ToNodeGuid => toNodeGuid;
        public string FromNodeId => fromNodeId;
        public string ToNodeId => toNodeId;
        public int FromOutputPortIndex => Mathf.Clamp(fromOutputPortIndex, 0, 1);
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
