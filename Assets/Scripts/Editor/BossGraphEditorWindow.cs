using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Week14.Enemy;

public sealed class BossGraphEditorWindow : EditorWindow
{
    private BossGraphAsset graphAsset;
    private SerializedObject graphObject;
    private VisualElement graphArea;
    private BossGraphView graphView;
    private IMGUIContainer detailsPanel;
    private IMGUIContainer bossHierarchyPanel;
    private BossGraphActionAsset selectedActionAsset;
    private Editor selectedActionEditor;
    private Transform explicitBossHierarchyRoot;
    private Transform bossHierarchyRoot;
    private Transform bossHierarchySelectedTransform;
    private string bossHierarchySelectedPath;
    private List<string> graphProjectileNames = new();
    private readonly Dictionary<int, bool> bossHierarchyFoldouts = new();
    private readonly Dictionary<int, int> phasePatternAddIndexes = new();
    private Vector2 detailsScroll;
    private Vector2 bossHierarchyScroll;
    private Vector2 bossHierarchyPanelPosition;
    private Vector2 bossHierarchyDragStartMouse;
    private Vector2 bossHierarchyDragStartPosition;
    private int detailsTabIndex;
    private string selectedElementKey;
    private bool showActionCategories;
    private bool bossHierarchyCollapsed;
    private bool bossHierarchyPanelDragging;
    private bool bossHierarchyPositionInitialized;
    private bool rebuildQueued;
    private bool graphStateSaveQueued;

    private static readonly string[] DetailTabLabels = { "패턴", "설정" };

    private const float GraphNodeWidth = 220f;
    private const float GraphNodeHeight = 92f;
    private const float PatternFramePadding = 16f;
    private const float PhaseFramePadding = 34f;
    private const float BossHierarchyPanelWidth = 340f;
    private const float BossHierarchyPanelHeight = 300f;
    private const float BossHierarchyCollapsedWidth = 180f;
    private const float BossHierarchyCollapsedHeight = 34f;
    private const float BossHierarchyPanelMargin = 8f;
    private const int SelectedElementDetailsIndex = -1;

    private void OnEnable()
    {
        EditorApplication.update += UpdateRuntimeHighlight;
    }

    [MenuItem("Window/Week14/Boss Graph Editor")]
    public static void OpenWindow()
    {
        BossGraphEditorWindow window = GetWindow<BossGraphEditorWindow>();
        window.titleContent = new GUIContent("Boss Graph");
        window.Show();
    }

    public static void Open(BossGraphAsset asset)
    {
        OpenWindow();
        BossGraphEditorWindow window = GetWindow<BossGraphEditorWindow>();
        window.SetGraphProjectileNames(null);
        window.SetExplicitBossHierarchyRoot(null);
        window.SetGraph(asset);
    }

    public static void Open(BossGraphAsset asset, IEnumerable<string> projectileNames)
    {
        OpenWindow();
        BossGraphEditorWindow window = GetWindow<BossGraphEditorWindow>();
        window.SetGraphProjectileNames(projectileNames);
        window.SetExplicitBossHierarchyRoot(null);
        window.SetGraph(asset);
    }

    public static void Open(BossGraphAsset asset, IEnumerable<string> projectileNames, Transform bossRoot)
    {
        OpenWindow();
        BossGraphEditorWindow window = GetWindow<BossGraphEditorWindow>();
        window.SetGraphProjectileNames(projectileNames);
        window.SetExplicitBossHierarchyRoot(bossRoot);
        window.SetGraph(asset);
    }

    private void SetGraphProjectileNames(IEnumerable<string> projectileNames)
    {
        graphProjectileNames = projectileNames?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .ToList() ?? new List<string>();
    }

    private void SetExplicitBossHierarchyRoot(Transform bossRoot)
    {
        explicitBossHierarchyRoot = bossRoot;
    }

    private void CreateGUI()
    {
        VisualElement content = new()
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                flexGrow = 1f
            }
        };

        graphArea = new VisualElement
        {
            style =
            {
                position = Position.Relative,
                flexGrow = 1f
            }
        };
        graphArea.RegisterCallback<GeometryChangedEvent>(_ => EnsureBossHierarchyPanelPosition());

        graphView = new BossGraphView
        {
            name = "Boss Graph View"
        };
        graphView.graphViewChanged = OnGraphViewChanged;
        graphView.style.flexGrow = 1f;
        graphView.RegisterCallback<MouseDownEvent>(HandleGraphContextMenu, TrickleDown.TrickleDown);
        graphView.RegisterCallback<KeyDownEvent>(HandleGraphKeyDown, TrickleDown.TrickleDown);
        graphView.RegisterCallback<MouseUpEvent>(evt =>
        {
            HandleGraphMouseUp(evt);
            detailsPanel?.MarkDirtyRepaint();
            RefreshGroupFramesFromNodePositions();
        });
        graphView.RegisterCallback<KeyUpEvent>(_ => detailsPanel?.MarkDirtyRepaint());
        graphArea.Add(graphView);

        bossHierarchyPanel = new IMGUIContainer(DrawBossHierarchyPanel)
        {
            style =
            {
                position = Position.Absolute,
                left = BossHierarchyPanelMargin,
                top = BossHierarchyPanelMargin,
                width = BossHierarchyPanelWidth,
                height = BossHierarchyPanelHeight,
                backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f)
            }
        };
        graphArea.Add(bossHierarchyPanel);
        content.Add(graphArea);

        detailsPanel = new IMGUIContainer(DrawDetailsPanel)
        {
            style =
            {
                width = 340f,
                flexShrink = 0f
            }
        };
        content.Add(detailsPanel);
        rootVisualElement.Add(content);

        if (graphAsset != null)
        {
            RebuildGraph();
        }
    }

    private void SetGraph(BossGraphAsset asset)
    {
        graphAsset = asset;
        graphObject = graphAsset != null ? new SerializedObject(graphAsset) : null;
        bossHierarchyRoot = explicitBossHierarchyRoot != null ? explicitBossHierarchyRoot : FindBossHierarchyRoot(graphAsset);
        SetBossHierarchySelection(bossHierarchyRoot, string.Empty);
        if (graphProjectileNames.Count == 0)
        {
            SetGraphProjectileNames(GetProjectileNamesFromBoss(bossHierarchyRoot));
        }

        SelectActionAsset(null);

        RebuildGraph();
        detailsPanel?.MarkDirtyRepaint();
        bossHierarchyPanel?.MarkDirtyRepaint();
    }

    private void OnDisable()
    {
        EditorApplication.update -= UpdateRuntimeHighlight;
        graphView?.ClearRuntimeHighlight();
        SaveGraphStateIfChanged("Save Boss Graph Layout", true, false);
        DestroyActionEditor();
    }

    private void RebuildGraph()
    {
        if (graphView == null)
        {
            return;
        }

        graphView.ClearGraph();
        if (graphObject == null)
        {
            return;
        }

        graphObject.Update();
        SerializedProperty stateNodes = graphObject.FindProperty("stateNodes");
        if (stateNodes == null)
        {
            return;
        }

        for (int i = 0; i < stateNodes.arraySize; i++)
        {
            SerializedProperty node = stateNodes.GetArrayElementAtIndex(i);
            graphView.AddStateNode(CreateNodeView(node, i));
        }

        AddTransitionViews();
        RefreshGroupFramesFromNodePositions();
        UpdateRuntimeHighlight();
    }

    private void UpdateRuntimeHighlight()
    {
        if (graphView == null)
        {
            return;
        }

        if (!EditorApplication.isPlaying || graphAsset == null)
        {
            graphView.ClearRuntimeHighlight();
            return;
        }

        if (BossGraphRuntimeState.TryGetSnapshot(graphAsset, out BossGraphRuntimeSnapshot snapshot))
        {
            graphView.SetRuntimeHighlight(snapshot.CurrentNodeId, snapshot.EdgeFromNodeId, snapshot.EdgeToNodeId);
            return;
        }

        graphView.ClearRuntimeHighlight();
    }

    private BossGraphNodeView CreateNodeView(SerializedProperty node, int index)
    {
        string nodeId = GetString(node, "nodeId", $"Node {index + 1}");
        BossGraphNodeKind nodeKind = (BossGraphNodeKind)GetEnum(node, "nodeKind", (int)BossGraphNodeKind.Attack);
        Vector2 position = GetVector2(node, "editorPosition", new Vector2(80f + index * 260f, 120f));
        string displayName = GetNodeDisplayName(node, index);
        if (position == Vector2.zero)
        {
            position = new Vector2(80f + index * 260f, 120f);
        }

        BossGraphNodeView nodeView = new(index, nodeId)
        {
            title = displayName
        };
        nodeView.SetDragNodeIdsProvider(() => graphView != null
            ? graphView.GetNodeIdsForDrag(nodeView)
            : new List<string> { nodeId });
        nodeView.SetPosition(new Rect(position, new Vector2(GraphNodeWidth, GraphNodeHeight)));
        nodeView.SetNodeKind(nodeKind, GetNodeKindColor(nodeKind));
        nodeView.AddToClassList("boss-graph-node");
        return nodeView;
    }

    private void AddTransitionViews()
    {
        SerializedProperty transitions = graphObject.FindProperty("transitions");
        if (transitions == null)
        {
            return;
        }

        for (int i = 0; i < transitions.arraySize; i++)
        {
            SerializedProperty transition = transitions.GetArrayElementAtIndex(i);
            string fromNodeId = GetString(transition, "fromNodeId", string.Empty);
            string toNodeId = GetString(transition, "toNodeId", string.Empty);
            BossGraphNodeView fromNode = graphView.FindNode(fromNodeId);
            BossGraphNodeView toNode = graphView.FindNode(toNodeId);
            if (fromNode == null || toNode == null || fromNode == toNode)
            {
                continue;
            }

            graphView.AddTransitionEdge(fromNode, toNode, GetTransitionLabel(transition));
        }
    }

    private void AddStateNode(Vector2? editorPosition)
    {
        if (graphObject == null)
        {
            return;
        }

        SaveGraph();
        Undo.RecordObject(graphAsset, "Add Boss Graph Node");
        graphObject.Update();
        SerializedProperty stateNodes = graphObject.FindProperty("stateNodes");
        string nodeId = GetUniqueElementId(stateNodes, "nodeId", "Node");
        stateNodes.arraySize++;

        int index = stateNodes.arraySize - 1;
        Vector2 nodePosition = editorPosition ?? new Vector2(80f + index * 260f, 120f);
        SerializedProperty node = stateNodes.GetArrayElementAtIndex(index);
        SetString(node, "nodeId", nodeId);
        SetString(node, "nodeGuid", Guid.NewGuid().ToString("N"));
        SetEnum(node, "nodeKind", (int)BossGraphNodeKind.Attack);
        SetInt(node, "phaseIndex", 0);
        SetEnum(node, "selectionMode", 0);
        SetVector2(node, "editorPosition", nodePosition);

        SerializedProperty sequences = node.FindPropertyRelative("sequences");
        sequences?.ClearArray();

        graphObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(graphAsset);
        AssetDatabase.SaveAssets();
        RebuildGraph();
        graphView?.SelectNode(nodeId);
        detailsPanel?.MarkDirtyRepaint();
    }

    private void HandleGraphContextMenu(MouseDownEvent evt)
    {
        if (evt.button != 1 || graphObject == null || graphView == null || !IsGraphEmptyAreaTarget(evt.target))
        {
            return;
        }

        Vector2 nodePosition = graphView.LocalToContentPosition(evt.localMousePosition);
        GenericMenu menu = new();
        menu.AddItem(new GUIContent("Add Node"), false, () => AddStateNode(nodePosition));
        menu.ShowAsContext();
        evt.StopImmediatePropagation();
    }

    private static bool IsGraphEmptyAreaTarget(IEventHandler target)
    {
        for (VisualElement current = target as VisualElement; current != null; current = current.parent)
        {
            if (current is BossGraphNodeView || current is Edge || current is Port)
            {
                return false;
            }

            if (current is GraphView)
            {
                break;
            }
        }

        return true;
    }

    private void DeleteSelectedNodes()
    {
        if (graphObject == null || graphView == null)
        {
            return;
        }

        List<int> indexes = graphView.GetSelectedNodeIndexes();
        List<Edge> selectedEdges = graphView.GetSelectedEdges();
        if (indexes.Count == 0 && selectedEdges.Count == 0)
        {
            return;
        }

        indexes.Sort((a, b) => b.CompareTo(a));
        Undo.RecordObject(graphAsset, "Delete Boss Graph Selection");
        graphObject.Update();
        SerializedProperty stateNodes = graphObject.FindProperty("stateNodes");
        HashSet<string> deletedNodeIds = GetNodeIds(stateNodes, indexes);
        for (int i = 0; i < indexes.Count; i++)
        {
            int index = indexes[i];
            if (index >= 0 && index < stateNodes.arraySize)
            {
                stateNodes.DeleteArrayElementAtIndex(index);
            }
        }

        RemoveTransitionsForNodes(deletedNodeIds);
        RemovePatternNodeReferences(deletedNodeIds);
        SyncPatternsFromTransitions(ReadTransitionSnapshots(graphObject.FindProperty("transitions")));
        if (indexes.Count == 0)
        {
            graphView.DeleteEdges(selectedEdges);
            SaveTransitions();
        }

        graphObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(graphAsset);
        AssetDatabase.SaveAssets();
        selectedElementKey = string.Empty;
        detailsTabIndex = 0;
        RebuildGraph();
        detailsPanel?.MarkDirtyRepaint();
    }

    private void HandleGraphKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace)
        {
            return;
        }

        if (graphView == null
            || (graphView.GetSelectedNodeIndexes().Count == 0 && graphView.GetSelectedEdges().Count == 0))
        {
            return;
        }

        DeleteSelectedNodes();
        evt.StopImmediatePropagation();
    }

    private void SaveGraph()
    {
        if (graphObject == null || graphView == null)
        {
            return;
        }

        graphObject.Update();
        Undo.RecordObject(graphAsset, "Save Boss Graph Node Positions");
        SerializedProperty stateNodes = graphObject.FindProperty("stateNodes");
        IReadOnlyList<BossGraphNodeView> nodeViews = graphView.NodeViews;
        for (int i = 0; i < nodeViews.Count; i++)
        {
            BossGraphNodeView nodeView = nodeViews[i];
            if (nodeView.NodeIndex < 0 || nodeView.NodeIndex >= stateNodes.arraySize)
            {
                continue;
            }

            SerializedProperty node = stateNodes.GetArrayElementAtIndex(nodeView.NodeIndex);
            SetVector2(node, "editorPosition", nodeView.GetPosition().position);
        }

        SaveTransitions();
        graphObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(graphAsset);
        AssetDatabase.SaveAssets();
        RebuildGraph();
        detailsPanel?.MarkDirtyRepaint();
    }

    private void HandleGraphMouseUp(MouseUpEvent evt)
    {
        if (evt.button != 0 || graphObject == null || graphView == null)
        {
            return;
        }

        if (SaveGraphStateIfChanged("Edit Boss Graph Layout", true, true))
        {
            RefreshGroupFramesFromNodePositions();
        }
    }

    private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
    {
        bool transitionChanged = (graphViewChange.edgesToCreate != null && graphViewChange.edgesToCreate.Count > 0)
            || (graphViewChange.elementsToRemove != null && graphViewChange.elementsToRemove.OfType<Edge>().Any());
        bool nodeLayoutChanged = graphViewChange.movedElements != null
            && graphViewChange.movedElements.OfType<BossGraphNodeView>().Any();
        if (transitionChanged || nodeLayoutChanged)
        {
            ScheduleGraphStateSave();
        }

        return graphViewChange;
    }

    private void ScheduleGraphStateSave()
    {
        if (graphStateSaveQueued)
        {
            return;
        }

        graphStateSaveQueued = true;
        EditorApplication.delayCall += () =>
        {
            graphStateSaveQueued = false;
            if (this == null || graphObject == null || graphAsset == null || graphView == null)
            {
                return;
            }

            if (!SaveGraphStateIfChanged("Edit Boss Graph Connections", true, true))
            {
                return;
            }

            RefreshGroupFramesFromNodePositions();
            detailsPanel?.MarkDirtyRepaint();
        };
    }

    private bool SaveGraphStateIfChanged(string undoName, bool saveAssets, bool recordUndo)
    {
        if (graphObject == null || graphAsset == null || graphView == null)
        {
            return false;
        }

        graphObject.Update();
        if (recordUndo)
        {
            Undo.RecordObject(graphAsset, undoName);
        }

        bool changed = WriteNodePositionsFromCurrentView();
        changed |= SaveTransitions();
        if (!changed)
        {
            return false;
        }

        graphObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(graphAsset);
        if (saveAssets)
        {
            AssetDatabase.SaveAssets();
        }

        return true;
    }

    private bool WriteNodePositionsFromCurrentView()
    {
        SerializedProperty stateNodes = graphObject.FindProperty("stateNodes");
        if (stateNodes == null)
        {
            return false;
        }

        bool changed = false;
        IReadOnlyList<BossGraphNodeView> nodeViews = graphView.NodeViews;
        for (int i = 0; i < nodeViews.Count; i++)
        {
            BossGraphNodeView nodeView = nodeViews[i];
            if (nodeView.NodeIndex < 0 || nodeView.NodeIndex >= stateNodes.arraySize)
            {
                continue;
            }

            SerializedProperty node = stateNodes.GetArrayElementAtIndex(nodeView.NodeIndex);
            SerializedProperty editorPosition = node.FindPropertyRelative("editorPosition");
            if (editorPosition == null)
            {
                continue;
            }

            Vector2 nextPosition = nodeView.GetPosition().position;
            if ((editorPosition.vector2Value - nextPosition).sqrMagnitude <= 0.01f)
            {
                continue;
            }

            editorPosition.vector2Value = nextPosition;
            changed = true;
        }

        return changed;
    }

    private bool SaveTransitions()
    {
        SerializedProperty transitions = graphObject.FindProperty("transitions");
        if (transitions == null || graphView == null)
        {
            return false;
        }

        Dictionary<string, TransitionValues> existingValues = new();
        List<string> existingOrder = new();
        for (int i = 0; i < transitions.arraySize; i++)
        {
            SerializedProperty transition = transitions.GetArrayElementAtIndex(i);
            string fromNodeId = GetString(transition, "fromNodeId", string.Empty);
            string toNodeId = GetString(transition, "toNodeId", string.Empty);
            if (string.IsNullOrWhiteSpace(fromNodeId) || string.IsNullOrWhiteSpace(toNodeId) || fromNodeId == toNodeId)
            {
                continue;
            }

            string key = GetTransitionKey(fromNodeId, toNodeId);
            if (existingValues.ContainsKey(key))
            {
                continue;
            }

            existingOrder.Add(key);
            existingValues[key] = new TransitionValues(
                GetEnum(transition, "conditionType", (int)BossTransitionConditionType.SequenceEnded),
                GetFloat(transition, "threshold", 0f),
                GetInt(transition, "phaseIndex", 0));
        }

        Dictionary<string, TransitionEndpoint> currentEdges = new();
        foreach (Edge edge in graphView.EdgeViews)
        {
            if (edge?.output?.node is not BossGraphNodeView fromNode
                || edge.input?.node is not BossGraphNodeView toNode
                || fromNode == toNode
                || string.IsNullOrWhiteSpace(fromNode.NodeId)
                || string.IsNullOrWhiteSpace(toNode.NodeId))
            {
                continue;
            }

            string key = GetTransitionKey(fromNode.NodeId, toNode.NodeId);
            if (!currentEdges.ContainsKey(key))
            {
                currentEdges[key] = new TransitionEndpoint(fromNode.NodeId, toNode.NodeId);
            }
        }

        List<TransitionSnapshot> nextTransitions = new();
        HashSet<string> addedKeys = new();
        for (int i = 0; i < existingOrder.Count; i++)
        {
            string key = existingOrder[i];
            if (!currentEdges.TryGetValue(key, out TransitionEndpoint endpoint))
            {
                continue;
            }

            nextTransitions.Add(new TransitionSnapshot(endpoint, existingValues[key]));
            addedKeys.Add(key);
        }

        foreach (KeyValuePair<string, TransitionEndpoint> pair in currentEdges)
        {
            if (addedKeys.Contains(pair.Key))
            {
                continue;
            }

            nextTransitions.Add(new TransitionSnapshot(pair.Value, TransitionValues.Default));
        }

        bool transitionsChanged = !AreTransitionSnapshotsEqual(transitions, nextTransitions);
        if (transitionsChanged)
        {
            transitions.ClearArray();
            for (int i = 0; i < nextTransitions.Count; i++)
            {
                TransitionSnapshot snapshot = nextTransitions[i];
                transitions.InsertArrayElementAtIndex(i);
                SerializedProperty transition = transitions.GetArrayElementAtIndex(i);
                SetString(transition, "fromNodeId", snapshot.Endpoint.FromNodeId);
                SetString(transition, "toNodeId", snapshot.Endpoint.ToNodeId);
                SetEnum(transition, "conditionType", snapshot.Values.ConditionType);
                SetFloat(transition, "threshold", snapshot.Values.Threshold);
                SetInt(transition, "phaseIndex", snapshot.Values.PhaseIndex);
            }
        }

        bool patternsChanged = SyncPatternsFromTransitions(nextTransitions);
        bool guidReferencesChanged = SyncGuidReferences();
        return transitionsChanged || patternsChanged || guidReferencesChanged;
    }

    private bool SyncGuidReferences()
    {
        Dictionary<string, string> nodeIdToGuid = BuildNodeIdToGuidMap();
        if (nodeIdToGuid.Count == 0)
        {
            return false;
        }

        bool changed = false;
        SerializedProperty transitions = graphObject.FindProperty("transitions");
        if (transitions != null)
        {
            for (int i = 0; i < transitions.arraySize; i++)
            {
                SerializedProperty transition = transitions.GetArrayElementAtIndex(i);
                changed |= SetGuidFromNodeId(transition, "fromNodeId", "fromNodeGuid", nodeIdToGuid);
                changed |= SetGuidFromNodeId(transition, "toNodeId", "toNodeGuid", nodeIdToGuid);
            }
        }

        SerializedProperty patterns = graphObject.FindProperty("patterns");
        if (patterns == null)
        {
            return changed;
        }

        for (int patternIndex = 0; patternIndex < patterns.arraySize; patternIndex++)
        {
            SerializedProperty pattern = patterns.GetArrayElementAtIndex(patternIndex);
            SerializedProperty nodeIds = pattern.FindPropertyRelative("nodeIds");
            SerializedProperty nodeGuids = pattern.FindPropertyRelative("nodeGuids");
            if (nodeIds == null || nodeGuids == null)
            {
                continue;
            }

            List<string> nextGuids = new();
            for (int nodeIndex = 0; nodeIndex < nodeIds.arraySize; nodeIndex++)
            {
                string nodeId = nodeIds.GetArrayElementAtIndex(nodeIndex).stringValue;
                if (!string.IsNullOrWhiteSpace(nodeId) && nodeIdToGuid.TryGetValue(nodeId, out string nodeGuid))
                {
                    nextGuids.Add(nodeGuid);
                }
            }

            if (StringArrayEquals(nodeGuids, nextGuids))
            {
                continue;
            }

            nodeGuids.ClearArray();
            for (int i = 0; i < nextGuids.Count; i++)
            {
                nodeGuids.InsertArrayElementAtIndex(i);
                nodeGuids.GetArrayElementAtIndex(i).stringValue = nextGuids[i];
            }

            changed = true;
        }

        return changed;
    }

    private Dictionary<string, string> BuildNodeIdToGuidMap()
    {
        Dictionary<string, string> nodeIdToGuid = new(StringComparer.Ordinal);
        SerializedProperty stateNodes = graphObject.FindProperty("stateNodes");
        if (stateNodes == null)
        {
            return nodeIdToGuid;
        }

        for (int i = 0; i < stateNodes.arraySize; i++)
        {
            SerializedProperty node = stateNodes.GetArrayElementAtIndex(i);
            string nodeId = GetString(node, "nodeId", string.Empty);
            string nodeGuid = GetString(node, "nodeGuid", string.Empty);
            if (!string.IsNullOrWhiteSpace(nodeId) && !string.IsNullOrWhiteSpace(nodeGuid))
            {
                nodeIdToGuid[nodeId] = nodeGuid;
            }
        }

        return nodeIdToGuid;
    }

    private static bool SetGuidFromNodeId(
        SerializedProperty root,
        string nodeIdPropertyName,
        string nodeGuidPropertyName,
        IReadOnlyDictionary<string, string> nodeIdToGuid)
    {
        string nodeId = GetString(root, nodeIdPropertyName, string.Empty);
        SerializedProperty nodeGuid = root.FindPropertyRelative(nodeGuidPropertyName);
        if (nodeGuid == null || string.IsNullOrWhiteSpace(nodeId) || !nodeIdToGuid.TryGetValue(nodeId, out string nextGuid))
        {
            return false;
        }

        if (nodeGuid.stringValue == nextGuid)
        {
            return false;
        }

        nodeGuid.stringValue = nextGuid;
        return true;
    }

    private static bool StringArrayEquals(SerializedProperty array, IReadOnlyList<string> values)
    {
        if (array == null || array.arraySize != values.Count)
        {
            return false;
        }

        for (int i = 0; i < values.Count; i++)
        {
            if (array.GetArrayElementAtIndex(i).stringValue != values[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreTransitionSnapshotsEqual(
        SerializedProperty transitions,
        IReadOnlyList<TransitionSnapshot> nextTransitions)
    {
        if (transitions.arraySize != nextTransitions.Count)
        {
            return false;
        }

        for (int i = 0; i < transitions.arraySize; i++)
        {
            SerializedProperty transition = transitions.GetArrayElementAtIndex(i);
            TransitionSnapshot snapshot = nextTransitions[i];
            if (GetString(transition, "fromNodeId", string.Empty) != snapshot.Endpoint.FromNodeId
                || GetString(transition, "toNodeId", string.Empty) != snapshot.Endpoint.ToNodeId
                || GetEnum(transition, "conditionType", (int)BossTransitionConditionType.SequenceEnded) != snapshot.Values.ConditionType
                || !Mathf.Approximately(GetFloat(transition, "threshold", 0f), snapshot.Values.Threshold)
                || GetInt(transition, "phaseIndex", 0) != snapshot.Values.PhaseIndex)
            {
                return false;
            }
        }

        return true;
    }

    private static List<TransitionSnapshot> ReadTransitionSnapshots(SerializedProperty transitions)
    {
        List<TransitionSnapshot> snapshots = new();
        if (transitions == null)
        {
            return snapshots;
        }

        for (int i = 0; i < transitions.arraySize; i++)
        {
            SerializedProperty transition = transitions.GetArrayElementAtIndex(i);
            string fromNodeId = GetString(transition, "fromNodeId", string.Empty);
            string toNodeId = GetString(transition, "toNodeId", string.Empty);
            if (string.IsNullOrWhiteSpace(fromNodeId) || string.IsNullOrWhiteSpace(toNodeId) || fromNodeId == toNodeId)
            {
                continue;
            }

            snapshots.Add(new TransitionSnapshot(
                new TransitionEndpoint(fromNodeId, toNodeId),
                new TransitionValues(
                    GetEnum(transition, "conditionType", (int)BossTransitionConditionType.SequenceEnded),
                    GetFloat(transition, "threshold", 0f),
                    GetInt(transition, "phaseIndex", 0))));
        }

        return snapshots;
    }

    private bool SyncPatternsFromTransitions(IReadOnlyList<TransitionSnapshot> transitions)
    {
        SerializedProperty stateNodes = graphObject.FindProperty("stateNodes");
        SerializedProperty patterns = graphObject.FindProperty("patterns");
        if (stateNodes == null || patterns == null)
        {
            return false;
        }

        List<string> stateNodeIds = ReadStateNodeIds(stateNodes);
        List<PatternSnapshot> currentPatterns = ReadPatternSnapshots(patterns);
        List<PatternSnapshot> nextPatterns = BuildConnectedPatternSnapshots(stateNodeIds, transitions, currentPatterns);
        bool patternsChanged = RewritePatternSnapshots(patterns, nextPatterns);
        bool phasesChanged = RewritePhasePatternIds(
            graphObject.FindProperty("phases"),
            BuildPatternReplacementMap(currentPatterns, nextPatterns),
            new HashSet<string>(nextPatterns.Select(pattern => pattern.PatternId)));
        return patternsChanged || phasesChanged;
    }

    private static List<string> ReadStateNodeIds(SerializedProperty stateNodes)
    {
        List<string> nodeIds = new();
        for (int i = 0; i < stateNodes.arraySize; i++)
        {
            string nodeId = GetString(stateNodes.GetArrayElementAtIndex(i), "nodeId", string.Empty);
            if (!string.IsNullOrWhiteSpace(nodeId) && !nodeIds.Contains(nodeId))
            {
                nodeIds.Add(nodeId);
            }
        }

        return nodeIds;
    }

    private static List<PatternSnapshot> BuildConnectedPatternSnapshots(
        IReadOnlyList<string> stateNodeIds,
        IReadOnlyList<TransitionSnapshot> transitions,
        IReadOnlyList<PatternSnapshot> currentPatterns)
    {
        Dictionary<string, int> nodeOrder = new();
        Dictionary<string, List<string>> undirectedLinks = new();
        Dictionary<string, List<string>> outgoingLinks = new();
        Dictionary<string, List<string>> incomingLinks = new();
        for (int i = 0; i < stateNodeIds.Count; i++)
        {
            string nodeId = stateNodeIds[i];
            nodeOrder[nodeId] = i;
            undirectedLinks[nodeId] = new List<string>();
            outgoingLinks[nodeId] = new List<string>();
            incomingLinks[nodeId] = new List<string>();
        }

        for (int i = 0; i < transitions.Count; i++)
        {
            string fromNodeId = transitions[i].Endpoint.FromNodeId;
            string toNodeId = transitions[i].Endpoint.ToNodeId;
            if (!nodeOrder.ContainsKey(fromNodeId) || !nodeOrder.ContainsKey(toNodeId) || fromNodeId == toNodeId)
            {
                continue;
            }

            AddUnique(undirectedLinks[fromNodeId], toNodeId);
            AddUnique(undirectedLinks[toNodeId], fromNodeId);
            AddUnique(outgoingLinks[fromNodeId], toNodeId);
            AddUnique(incomingLinks[toNodeId], fromNodeId);
        }

        List<List<string>> components = BuildConnectedComponents(stateNodeIds, undirectedLinks, nodeOrder);
        HashSet<string> usedPatternIds = new();
        HashSet<string> reservedPatternIds = new(currentPatterns
            .Select(pattern => pattern.PatternId)
            .Where(patternId => !string.IsNullOrWhiteSpace(patternId)));
        List<PatternSnapshot> nextPatterns = new();
        for (int i = 0; i < components.Count; i++)
        {
            List<string> orderedNodeIds = OrderComponentNodes(components[i], outgoingLinks, incomingLinks, nodeOrder);
            string patternId = FindReusablePatternId(orderedNodeIds, currentPatterns, usedPatternIds);
            if (string.IsNullOrWhiteSpace(patternId))
            {
                patternId = GetUniquePatternId(reservedPatternIds, usedPatternIds);
            }

            usedPatternIds.Add(patternId);
            nextPatterns.Add(new PatternSnapshot(patternId, orderedNodeIds));
        }

        return nextPatterns;
    }

    private static List<List<string>> BuildConnectedComponents(
        IReadOnlyList<string> stateNodeIds,
        Dictionary<string, List<string>> undirectedLinks,
        Dictionary<string, int> nodeOrder)
    {
        List<List<string>> components = new();
        HashSet<string> visitedNodeIds = new();
        for (int i = 0; i < stateNodeIds.Count; i++)
        {
            string startNodeId = stateNodeIds[i];
            if (!visitedNodeIds.Add(startNodeId))
            {
                continue;
            }

            List<string> component = new();
            Queue<string> queue = new();
            queue.Enqueue(startNodeId);
            while (queue.Count > 0)
            {
                string nodeId = queue.Dequeue();
                component.Add(nodeId);
                if (!undirectedLinks.TryGetValue(nodeId, out List<string> linkedNodeIds))
                {
                    continue;
                }

                for (int linkedIndex = 0; linkedIndex < linkedNodeIds.Count; linkedIndex++)
                {
                    string linkedNodeId = linkedNodeIds[linkedIndex];
                    if (visitedNodeIds.Add(linkedNodeId))
                    {
                        queue.Enqueue(linkedNodeId);
                    }
                }
            }

            component.Sort((first, second) => nodeOrder[first].CompareTo(nodeOrder[second]));
            components.Add(component);
        }

        components.Sort((first, second) => nodeOrder[first[0]].CompareTo(nodeOrder[second[0]]));
        return components;
    }

    private static List<string> OrderComponentNodes(
        IReadOnlyList<string> component,
        Dictionary<string, List<string>> outgoingLinks,
        Dictionary<string, List<string>> incomingLinks,
        Dictionary<string, int> nodeOrder)
    {
        HashSet<string> componentNodeIds = new(component);
        List<string> startNodeIds = component
            .Where(nodeId => !incomingLinks[nodeId].Any(componentNodeIds.Contains))
            .OrderBy(nodeId => nodeOrder[nodeId])
            .ToList();
        if (startNodeIds.Count == 0 && component.Count > 0)
        {
            startNodeIds.Add(component.OrderBy(nodeId => nodeOrder[nodeId]).First());
        }

        List<string> orderedNodeIds = new();
        HashSet<string> visitedNodeIds = new();
        for (int i = 0; i < startNodeIds.Count; i++)
        {
            AppendConnectedNodeOrder(startNodeIds[i], componentNodeIds, outgoingLinks, nodeOrder, visitedNodeIds, orderedNodeIds);
        }

        foreach (string nodeId in component.OrderBy(nodeId => nodeOrder[nodeId]))
        {
            AppendConnectedNodeOrder(nodeId, componentNodeIds, outgoingLinks, nodeOrder, visitedNodeIds, orderedNodeIds);
        }

        return orderedNodeIds;
    }

    private static void AppendConnectedNodeOrder(
        string nodeId,
        HashSet<string> componentNodeIds,
        Dictionary<string, List<string>> outgoingLinks,
        Dictionary<string, int> nodeOrder,
        HashSet<string> visitedNodeIds,
        List<string> orderedNodeIds)
    {
        if (!componentNodeIds.Contains(nodeId) || !visitedNodeIds.Add(nodeId))
        {
            return;
        }

        orderedNodeIds.Add(nodeId);
        List<string> nextNodeIds = outgoingLinks[nodeId]
            .Where(componentNodeIds.Contains)
            .OrderBy(nextNodeId => nodeOrder[nextNodeId])
            .ToList();
        for (int i = 0; i < nextNodeIds.Count; i++)
        {
            AppendConnectedNodeOrder(nextNodeIds[i], componentNodeIds, outgoingLinks, nodeOrder, visitedNodeIds, orderedNodeIds);
        }
    }

    private static void AddUnique(List<string> values, string value)
    {
        if (!values.Contains(value))
        {
            values.Add(value);
        }
    }

    private static string FindReusablePatternId(
        IReadOnlyList<string> nodeIds,
        IReadOnlyList<PatternSnapshot> currentPatterns,
        HashSet<string> usedPatternIds)
    {
        string bestPatternId = string.Empty;
        int bestOverlap = 0;
        for (int i = 0; i < currentPatterns.Count; i++)
        {
            PatternSnapshot pattern = currentPatterns[i];
            if (usedPatternIds.Contains(pattern.PatternId))
            {
                continue;
            }

            int overlap = pattern.NodeIds.Count(nodeIds.Contains);
            if (overlap > bestOverlap)
            {
                bestOverlap = overlap;
                bestPatternId = pattern.PatternId;
            }
        }

        return bestPatternId;
    }

    private static string GetUniquePatternId(HashSet<string> reservedPatternIds, HashSet<string> usedPatternIds)
    {
        for (int i = 1; i < 1000; i++)
        {
            string candidate = $"Pattern{i}";
            if (!reservedPatternIds.Contains(candidate) && !usedPatternIds.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"Pattern{Guid.NewGuid():N}";
    }

    private static List<PatternSnapshot> ReadPatternSnapshots(SerializedProperty patterns)
    {
        List<PatternSnapshot> snapshots = new();
        for (int patternIndex = 0; patternIndex < patterns.arraySize; patternIndex++)
        {
            SerializedProperty pattern = patterns.GetArrayElementAtIndex(patternIndex);
            string patternId = GetString(pattern, "patternId", $"Pattern{patternIndex + 1}");
            List<string> nodeIds = new();
            SerializedProperty nodeIdsProperty = pattern.FindPropertyRelative("nodeIds");
            if (nodeIdsProperty != null)
            {
                for (int nodeIndex = 0; nodeIndex < nodeIdsProperty.arraySize; nodeIndex++)
                {
                    string nodeId = nodeIdsProperty.GetArrayElementAtIndex(nodeIndex).stringValue;
                    if (!string.IsNullOrWhiteSpace(nodeId))
                    {
                        nodeIds.Add(nodeId);
                    }
                }
            }

            snapshots.Add(new PatternSnapshot(patternId, nodeIds));
        }

        return snapshots;
    }

    private static Dictionary<string, List<string>> BuildPatternReplacementMap(
        IReadOnlyList<PatternSnapshot> currentPatterns,
        IReadOnlyList<PatternSnapshot> nextPatterns)
    {
        Dictionary<string, List<string>> replacementMap = new();
        for (int currentIndex = 0; currentIndex < currentPatterns.Count; currentIndex++)
        {
            PatternSnapshot currentPattern = currentPatterns[currentIndex];
            List<string> replacements = new();
            for (int nextIndex = 0; nextIndex < nextPatterns.Count; nextIndex++)
            {
                PatternSnapshot nextPattern = nextPatterns[nextIndex];
                if (currentPattern.NodeIds.Any(nextPattern.NodeIds.Contains))
                {
                    replacements.Add(nextPattern.PatternId);
                }
            }

            replacementMap[currentPattern.PatternId] = replacements;
        }

        return replacementMap;
    }

    private static bool RewritePatternSnapshots(SerializedProperty patterns, IReadOnlyList<PatternSnapshot> nextPatterns)
    {
        if (ArePatternSnapshotsEqual(ReadPatternSnapshots(patterns), nextPatterns))
        {
            return false;
        }

        patterns.ClearArray();
        for (int patternIndex = 0; patternIndex < nextPatterns.Count; patternIndex++)
        {
            PatternSnapshot snapshot = nextPatterns[patternIndex];
            patterns.InsertArrayElementAtIndex(patternIndex);
            SerializedProperty pattern = patterns.GetArrayElementAtIndex(patternIndex);
            SetString(pattern, "patternId", snapshot.PatternId);
            SerializedProperty nodeIds = pattern.FindPropertyRelative("nodeIds");
            nodeIds.ClearArray();
            for (int nodeIndex = 0; nodeIndex < snapshot.NodeIds.Count; nodeIndex++)
            {
                nodeIds.InsertArrayElementAtIndex(nodeIndex);
                nodeIds.GetArrayElementAtIndex(nodeIndex).stringValue = snapshot.NodeIds[nodeIndex];
            }
        }

        return true;
    }

    private static bool ArePatternSnapshotsEqual(
        IReadOnlyList<PatternSnapshot> currentPatterns,
        IReadOnlyList<PatternSnapshot> nextPatterns)
    {
        if (currentPatterns.Count != nextPatterns.Count)
        {
            return false;
        }

        for (int i = 0; i < currentPatterns.Count; i++)
        {
            if (currentPatterns[i].PatternId != nextPatterns[i].PatternId
                || currentPatterns[i].NodeIds.Count != nextPatterns[i].NodeIds.Count)
            {
                return false;
            }

            for (int nodeIndex = 0; nodeIndex < currentPatterns[i].NodeIds.Count; nodeIndex++)
            {
                if (currentPatterns[i].NodeIds[nodeIndex] != nextPatterns[i].NodeIds[nodeIndex])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool RewritePhasePatternIds(
        SerializedProperty phases,
        IReadOnlyDictionary<string, List<string>> replacementMap,
        HashSet<string> validPatternIds)
    {
        if (phases == null || replacementMap == null || replacementMap.Count == 0)
        {
            return false;
        }

        bool changed = false;
        for (int phaseIndex = 0; phaseIndex < phases.arraySize; phaseIndex++)
        {
            SerializedProperty phase = phases.GetArrayElementAtIndex(phaseIndex);
            SerializedProperty patternEntries = phase.FindPropertyRelative("patterns");
            if (patternEntries == null)
            {
                continue;
            }

            List<PhasePatternEntrySnapshot> nextEntries = new();
            HashSet<string> usedPatternIds = new();
            for (int entryIndex = 0; entryIndex < patternEntries.arraySize; entryIndex++)
            {
                SerializedProperty entry = patternEntries.GetArrayElementAtIndex(entryIndex);
                string patternId = GetString(entry, "patternId", string.Empty);
                int weight = GetInt(entry, "weight", 1);
                if (replacementMap.TryGetValue(patternId, out List<string> replacementIds))
                {
                    for (int replacementIndex = 0; replacementIndex < replacementIds.Count; replacementIndex++)
                    {
                        string replacementId = replacementIds[replacementIndex];
                        if (!validPatternIds.Contains(replacementId) || !usedPatternIds.Add(replacementId))
                        {
                            continue;
                        }

                        nextEntries.Add(new PhasePatternEntrySnapshot(replacementId, weight));
                    }

                    continue;
                }

                if (!validPatternIds.Contains(patternId) || !usedPatternIds.Add(patternId))
                {
                    continue;
                }

                nextEntries.Add(new PhasePatternEntrySnapshot(patternId, weight));
            }

            if (ArePhasePatternEntriesEqual(patternEntries, nextEntries))
            {
                continue;
            }

            patternEntries.ClearArray();
            for (int entryIndex = 0; entryIndex < nextEntries.Count; entryIndex++)
            {
                PhasePatternEntrySnapshot snapshot = nextEntries[entryIndex];
                patternEntries.InsertArrayElementAtIndex(entryIndex);
                SerializedProperty entry = patternEntries.GetArrayElementAtIndex(entryIndex);
                SetString(entry, "patternId", snapshot.PatternId);
                SetInt(entry, "weight", snapshot.Weight);
            }

            changed = true;
        }

        return changed;
    }

    private static bool ArePhasePatternEntriesEqual(
        SerializedProperty patternEntries,
        IReadOnlyList<PhasePatternEntrySnapshot> nextEntries)
    {
        if (patternEntries.arraySize != nextEntries.Count)
        {
            return false;
        }

        for (int i = 0; i < patternEntries.arraySize; i++)
        {
            SerializedProperty entry = patternEntries.GetArrayElementAtIndex(i);
            if (GetString(entry, "patternId", string.Empty) != nextEntries[i].PatternId
                || GetInt(entry, "weight", 1) != nextEntries[i].Weight)
            {
                return false;
            }
        }

        return true;
    }

    private static string GetTransitionKey(string fromNodeId, string toNodeId)
    {
        return $"{fromNodeId}->{toNodeId}";
    }

    private static string GetTransitionLabel(SerializedProperty transition)
    {
        BossTransitionConditionType conditionType =
            (BossTransitionConditionType)GetEnum(transition, "conditionType", (int)BossTransitionConditionType.SequenceEnded);
        return conditionType switch
        {
            BossTransitionConditionType.SequenceEnded => string.Empty,
            BossTransitionConditionType.HpRatioLessOrEqual => $"HP <= {GetFloat(transition, "threshold", 0f):0.##}",
            BossTransitionConditionType.PhaseIndexEquals => $"Phase = {GetInt(transition, "phaseIndex", 0) + 1}",
            BossTransitionConditionType.EnragePhaseEquals => $"Enrage = {GetInt(transition, "phaseIndex", 0)}",
            BossTransitionConditionType.LivesLessOrEqual => $"Lives <= {GetInt(transition, "phaseIndex", 0)}",
            _ => conditionType.ToString()
        };
    }

    private static Rect GetNormalizedNodeRect(BossGraphNodeView nodeView)
    {
        Rect rect = nodeView.GetPosition();
        rect.width = Mathf.Max(GraphNodeWidth, rect.width);
        rect.height = Mathf.Max(GraphNodeHeight, rect.height);
        return rect;
    }

    private void RefreshGroupFramesFromNodePositions()
    {
        if (graphObject == null || graphView == null)
        {
            return;
        }

        graphObject.Update();
        BuildGroupFramesFromCurrentNodePositions(
            graphObject.FindProperty("patterns"),
            null,
            out List<FrameLayout> patternFrames,
            out _);
        graphView.ReplacePatternFrames(patternFrames.Select(frame => new BossGraphFrameView(frame.Label, frame.Color, frame.Rect, false)));
        graphView.ReplacePhaseFrames(null);
    }

    private void BuildGroupFramesFromCurrentNodePositions(
        SerializedProperty patterns,
        SerializedProperty phases,
        out List<FrameLayout> patternFrames,
        out List<FrameLayout> phaseFrames)
    {
        patternFrames = new List<FrameLayout>();
        phaseFrames = new List<FrameLayout>();
        if (patterns == null || graphView == null)
        {
            return;
        }

        Dictionary<string, Rect> patternBounds = new();
        for (int patternIndex = 0; patternIndex < patterns.arraySize; patternIndex++)
        {
            SerializedProperty pattern = patterns.GetArrayElementAtIndex(patternIndex);
            string patternId = GetString(pattern, "patternId", $"Pattern {patternIndex + 1}");
            SerializedProperty nodeIds = pattern.FindPropertyRelative("nodeIds");
            if (nodeIds == null || nodeIds.arraySize == 0)
            {
                continue;
            }

            bool hasBounds = false;
            Rect bounds = default;
            for (int nodeIndex = 0; nodeIndex < nodeIds.arraySize; nodeIndex++)
            {
                string nodeId = nodeIds.GetArrayElementAtIndex(nodeIndex).stringValue;
                BossGraphNodeView nodeView = graphView.FindNode(nodeId);
                if (nodeView == null)
                {
                    continue;
                }

                Rect nodeRect = GetNormalizedNodeRect(nodeView);
                if (!hasBounds)
                {
                    bounds = nodeRect;
                    hasBounds = true;
                }
                else
                {
                    bounds = Encapsulate(bounds, nodeRect);
                }
            }

            if (hasBounds)
            {
                Rect frameRect = ExpandRect(bounds, PatternFramePadding);
                patternBounds[patternId] = frameRect;
                patternFrames.Add(new FrameLayout(frameRect, patternId, GetPatternFrameColor(patternIndex)));
            }
        }

        BuildPhaseFrames(phases, patternBounds, phaseFrames);
    }

    private void DrawBossHierarchyPanel()
    {
        ApplyBossHierarchyPanelSize();
        Vector2 panelSize = GetBossHierarchyPanelSize();
        EditorGUI.DrawRect(new Rect(0f, 0f, panelSize.x, panelSize.y), new Color(0.18f, 0.18f, 0.18f, 1f));

        if (bossHierarchyCollapsed)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(BossHierarchyCollapsedWidth - 4f)))
            {
                Rect headerRect = GUILayoutUtility.GetRect(0f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
                string label = bossHierarchyRoot != null ? bossHierarchyRoot.name : "Boss";
                Rect buttonRect = new(headerRect.xMax - 56f, headerRect.y, 56f, headerRect.height);
                Rect labelRect = headerRect;
                labelRect.xMax = buttonRect.xMin - 4f;
                EditorGUI.LabelField(labelRect, label, EditorStyles.boldLabel);
                if (GUI.Button(buttonRect, "펼치기"))
                {
                    bossHierarchyCollapsed = false;
                    ApplyBossHierarchyPanelSize();
                    bossHierarchyPanel?.MarkDirtyRepaint();
                }

                HandleBossHierarchyPanelMove(labelRect);
            }

            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(BossHierarchyPanelWidth - 4f)))
        {
            Rect headerRect = GUILayoutUtility.GetRect(0f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            Rect collapseRect = new(headerRect.xMax - 48f, headerRect.y, 48f, headerRect.height);
            Rect titleRect = headerRect;
            titleRect.xMax = collapseRect.xMin - 4f;
            EditorGUI.LabelField(titleRect, "Boss", EditorStyles.boldLabel);
            if (GUI.Button(collapseRect, "축소"))
            {
                bossHierarchyCollapsed = true;
                ApplyBossHierarchyPanelSize();
                bossHierarchyPanel?.MarkDirtyRepaint();
                return;
            }

            HandleBossHierarchyPanelMove(titleRect);

            if (bossHierarchyRoot == null)
            {
                EditorGUILayout.HelpBox("Boss 인스펙터에서 그래프를 열면 하이어러키가 표시됩니다.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("Boss", bossHierarchyRoot, typeof(Transform), true);
                }

                EditorGUILayout.LabelField("투사체 위치 필드로 드래그", EditorStyles.miniLabel);
                bossHierarchyScroll = EditorGUILayout.BeginScrollView(bossHierarchyScroll, GUILayout.Height(BossHierarchyPanelHeight - 86f));
                DrawBossHierarchyNode(bossHierarchyRoot, string.Empty, 0, true);
                EditorGUILayout.EndScrollView();
            }
        }
    }

    private void ApplyBossHierarchyPanelSize()
    {
        if (bossHierarchyPanel == null)
        {
            return;
        }

        bossHierarchyPanel.style.width = bossHierarchyCollapsed ? BossHierarchyCollapsedWidth : BossHierarchyPanelWidth;
        bossHierarchyPanel.style.height = bossHierarchyCollapsed ? BossHierarchyCollapsedHeight : BossHierarchyPanelHeight;
        EnsureBossHierarchyPanelPosition();
    }

    private Vector2 GetBossHierarchyPanelSize()
    {
        return bossHierarchyCollapsed
            ? new Vector2(BossHierarchyCollapsedWidth, BossHierarchyCollapsedHeight)
            : new Vector2(BossHierarchyPanelWidth, BossHierarchyPanelHeight);
    }

    private void EnsureBossHierarchyPanelPosition()
    {
        if (bossHierarchyPanel == null || graphArea == null)
        {
            return;
        }

        if (graphArea.layout.width <= 0f || graphArea.layout.height <= 0f)
        {
            return;
        }

        if (!bossHierarchyPositionInitialized)
        {
            Vector2 size = GetBossHierarchyPanelSize();
            float y = Mathf.Max(BossHierarchyPanelMargin, graphArea.layout.height - size.y - BossHierarchyPanelMargin);
            bossHierarchyPanelPosition = new Vector2(BossHierarchyPanelMargin, y);
            bossHierarchyPositionInitialized = true;
        }

        bossHierarchyPanelPosition = ClampBossHierarchyPanelPosition(bossHierarchyPanelPosition);
        ApplyBossHierarchyPanelPosition();
    }

    private void ApplyBossHierarchyPanelPosition()
    {
        if (bossHierarchyPanel == null)
        {
            return;
        }

        bossHierarchyPanel.style.left = bossHierarchyPanelPosition.x;
        bossHierarchyPanel.style.top = bossHierarchyPanelPosition.y;
    }

    private Vector2 ClampBossHierarchyPanelPosition(Vector2 position)
    {
        if (graphArea == null)
        {
            return position;
        }

        Vector2 size = GetBossHierarchyPanelSize();
        float maxX = Mathf.Max(BossHierarchyPanelMargin, graphArea.layout.width - size.x - BossHierarchyPanelMargin);
        float maxY = Mathf.Max(BossHierarchyPanelMargin, graphArea.layout.height - size.y - BossHierarchyPanelMargin);
        return new Vector2(
            Mathf.Clamp(position.x, BossHierarchyPanelMargin, maxX),
            Mathf.Clamp(position.y, BossHierarchyPanelMargin, maxY));
    }

    private void HandleBossHierarchyPanelMove(Rect dragRect)
    {
        Event currentEvent = Event.current;
        EditorGUIUtility.AddCursorRect(dragRect, MouseCursor.Pan);
        int controlId = GUIUtility.GetControlID("BossHierarchyPanelMove".GetHashCode(), FocusType.Passive, dragRect);

        if (currentEvent.type == EventType.MouseDown
            && currentEvent.button == 0
            && dragRect.Contains(currentEvent.mousePosition))
        {
            bossHierarchyPanelDragging = true;
            bossHierarchyDragStartMouse = GUIUtility.GUIToScreenPoint(currentEvent.mousePosition);
            bossHierarchyDragStartPosition = bossHierarchyPanelPosition;
            GUIUtility.hotControl = controlId;
            currentEvent.Use();
            return;
        }

        if (GUIUtility.hotControl != controlId || !bossHierarchyPanelDragging)
        {
            return;
        }

        if (currentEvent.type == EventType.MouseDrag)
        {
            Vector2 currentMouse = GUIUtility.GUIToScreenPoint(currentEvent.mousePosition);
            bossHierarchyPanelPosition = ClampBossHierarchyPanelPosition(bossHierarchyDragStartPosition + currentMouse - bossHierarchyDragStartMouse);
            ApplyBossHierarchyPanelPosition();
            bossHierarchyPanel?.MarkDirtyRepaint();
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseUp || currentEvent.type == EventType.Ignore)
        {
            bossHierarchyPanelDragging = false;
            GUIUtility.hotControl = 0;
            currentEvent.Use();
        }
    }

    private void SetBossHierarchySelection(Transform target, string targetPath)
    {
        bossHierarchySelectedTransform = target;
        bossHierarchySelectedPath = !string.IsNullOrWhiteSpace(targetPath)
            ? targetPath
            : GetBossHierarchyPath(target);
        ExpandBossHierarchyFoldouts(target);
        bossHierarchyPanel?.MarkDirtyRepaint();
        Repaint();
    }

    private void ExpandBossHierarchyFoldouts(Transform target)
    {
        if (bossHierarchyRoot == null || target == null)
        {
            return;
        }

        Transform current = target;
        while (current != null)
        {
            bossHierarchyFoldouts[current.GetInstanceID()] = true;
            if (current == bossHierarchyRoot)
            {
                break;
            }

            current = current.parent;
        }
    }

    private string GetBossHierarchyPath(Transform target)
    {
        if (target == null || target == bossHierarchyRoot || !IsBossHierarchyTransform(target))
        {
            return string.Empty;
        }

        List<string> names = new();
        Transform current = target;
        while (current != null && current != bossHierarchyRoot)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private bool IsBossHierarchyTransform(Transform target)
    {
        if (target == null || bossHierarchyRoot == null)
        {
            return false;
        }

        Transform current = target;
        while (current != null)
        {
            if (current == bossHierarchyRoot)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void DrawBossHierarchyNode(Transform node, string path, int depth, bool isRoot)
    {
        if (node == null)
        {
            return;
        }

        int foldoutKey = node.GetInstanceID();
        if (!bossHierarchyFoldouts.ContainsKey(foldoutKey))
        {
            bossHierarchyFoldouts[foldoutKey] = true;
        }

        Rect rowRect = GUILayoutUtility.GetRect(0f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
        Rect indentedRect = rowRect;
        indentedRect.xMin += depth * 14f;

        bool hasChildren = node.childCount > 0;
        Rect foldoutRect = new(indentedRect.x, indentedRect.y, 14f, indentedRect.height);
        Rect labelRect = indentedRect;
        labelRect.xMin += hasChildren ? 14f : 16f;
        bool selected = IsBossHierarchySelectedNode(node, path, isRoot);
        if (selected)
        {
            EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.48f, 0.95f, 0.22f));
        }

        if (hasChildren)
        {
            bossHierarchyFoldouts[foldoutKey] = EditorGUI.Foldout(foldoutRect, bossHierarchyFoldouts[foldoutKey], GUIContent.none);
        }

        string label = isRoot ? $"{node.name} (Root)" : node.name;
        EditorGUI.LabelField(labelRect, label);
        HandleBossHierarchySelection(labelRect, node, path);
        if (!isRoot)
        {
            HandleBossHierarchyDrag(labelRect, node, path);
        }

        if (!hasChildren || !bossHierarchyFoldouts[foldoutKey])
        {
            return;
        }

        for (int i = 0; i < node.childCount; i++)
        {
            Transform child = node.GetChild(i);
            string childPath = string.IsNullOrWhiteSpace(path) ? child.name : $"{path}/{child.name}";
            DrawBossHierarchyNode(child, childPath, depth + 1, false);
        }
    }

    private void HandleBossHierarchySelection(Rect selectionRect, Transform node, string path)
    {
        Event currentEvent = Event.current;
        if (node == null
            || currentEvent.type != EventType.MouseDown
            || currentEvent.button != 0
            || !selectionRect.Contains(currentEvent.mousePosition))
        {
            return;
        }

        SetBossHierarchySelection(node, path);
        GUIUtility.keyboardControl = 0;
        currentEvent.Use();
    }

    private bool IsBossHierarchySelectedNode(Transform node, string path, bool isRoot)
    {
        if (node == null || bossHierarchySelectedTransform == null)
        {
            return false;
        }

        if (bossHierarchySelectedTransform == node)
        {
            return true;
        }

        if (isRoot)
        {
            return bossHierarchySelectedTransform == bossHierarchyRoot
                || (bossHierarchySelectedTransform == node && string.IsNullOrWhiteSpace(bossHierarchySelectedPath));
        }

        if (!string.IsNullOrWhiteSpace(bossHierarchySelectedPath))
        {
            return bossHierarchySelectedPath == path;
        }

        return false;
    }

    private static void HandleBossHierarchyDrag(Rect dragRect, Transform node, string path)
    {
        Event currentEvent = Event.current;
        if (node == null || string.IsNullOrWhiteSpace(path) || !dragRect.Contains(currentEvent.mousePosition))
        {
            return;
        }

        if (currentEvent.type != EventType.MouseDrag)
        {
            return;
        }

        DragAndDrop.PrepareStartDrag();
        DragAndDrop.SetGenericData(BossGraphDragKeys.BossChildPath, path);
        DragAndDrop.objectReferences = new UnityEngine.Object[] { node.gameObject };
        DragAndDrop.StartDrag(path);
        currentEvent.Use();
    }

    private static Transform FindBossHierarchyRoot(BossGraphAsset graph)
    {
        if (graph == null)
        {
            return null;
        }

        BossAI[] bosses = Resources.FindObjectsOfTypeAll<BossAI>();
        for (int i = 0; i < bosses.Length; i++)
        {
            BossAI boss = bosses[i];
            if (boss == null)
            {
                continue;
            }

            SerializedObject bossObject = new(boss);
            SerializedProperty graphProperty = bossObject.FindProperty("bossGraph");
            if (graphProperty != null && graphProperty.objectReferenceValue == graph)
            {
                return boss.transform;
            }
        }

        return null;
    }

    private static List<string> GetProjectileNamesFromBoss(Transform bossRoot)
    {
        List<string> names = new();
        if (bossRoot == null)
        {
            return names;
        }

        BossAI boss = bossRoot.GetComponent<BossAI>();
        if (boss == null)
        {
            return names;
        }

        SerializedObject bossObject = new(boss);
        SerializedProperty projectiles = bossObject.FindProperty("graphProjectiles");
        if (projectiles == null)
        {
            return names;
        }

        for (int i = 0; i < projectiles.arraySize; i++)
        {
            string projectileName = projectiles.GetArrayElementAtIndex(i)
                .FindPropertyRelative("projectileName")?.stringValue;
            if (!string.IsNullOrWhiteSpace(projectileName) && !names.Contains(projectileName))
            {
                names.Add(projectileName);
            }
        }

        return names;
    }

    private void DrawDetailsPanel()
    {
        DrawRightPanelMenu();
        SyncSelectedElementDetailsState();
        int toolbarIndex = detailsTabIndex == SelectedElementDetailsIndex
            ? SelectedElementDetailsIndex
            : Mathf.Clamp(detailsTabIndex, 0, DetailTabLabels.Length - 1);
        int nextToolbarIndex = GUILayout.Toolbar(toolbarIndex, DetailTabLabels);
        if (nextToolbarIndex != toolbarIndex && nextToolbarIndex >= 0)
        {
            detailsTabIndex = nextToolbarIndex;
            detailsScroll = Vector2.zero;
        }

        EditorGUILayout.Space(6f);

        detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll);
        if (graphObject == null)
        {
            EditorGUILayout.HelpBox("BossGraphAsset을 선택하세요.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        graphObject.Update();
        DrawGraphValidation();
        EditorGUILayout.Space(8f);

        if (detailsTabIndex == SelectedElementDetailsIndex && DrawSelectedElementDetails())
        {
            EditorGUILayout.EndScrollView();
            return;
        }

        switch (detailsTabIndex)
        {
            case 0:
                DrawPatternPanel();
                break;
            case 1:
                DrawSettingsPanel();
                break;
        }

        EditorGUILayout.EndScrollView();
    }

    private void SyncSelectedElementDetailsState()
    {
        string currentSelectionKey = GetSelectedElementKey();
        if (currentSelectionKey == selectedElementKey)
        {
            return;
        }

        selectedElementKey = currentSelectionKey;
        if (!string.IsNullOrWhiteSpace(selectedElementKey))
        {
            detailsTabIndex = SelectedElementDetailsIndex;
            detailsScroll = Vector2.zero;
            return;
        }

        if (detailsTabIndex == SelectedElementDetailsIndex)
        {
            detailsTabIndex = 0;
            detailsScroll = Vector2.zero;
        }
    }

    private string GetSelectedElementKey()
    {
        ISelectable selection = graphView?.GetPrimarySelection();
        if (selection is BossGraphNodeView nodeView)
        {
            return GetNodeSelectionKey(nodeView.NodeId);
        }

        return string.Empty;
    }

    private static string GetNodeSelectionKey(string nodeId)
    {
        return string.IsNullOrWhiteSpace(nodeId) ? string.Empty : $"node:{nodeId}";
    }

    private string GetSelectedNodeId()
    {
        return graphView?.GetPrimarySelection() is BossGraphNodeView nodeView
            ? nodeView.NodeId
            : string.Empty;
    }

    private void SelectNodeDetails(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return;
        }

        graphView?.SelectNode(nodeId);
        selectedElementKey = GetNodeSelectionKey(nodeId);
        detailsTabIndex = SelectedElementDetailsIndex;
    }

    private bool DrawSelectedElementDetails()
    {
        ISelectable selection = graphView?.GetPrimarySelection();
        if (selection is BossGraphNodeView nodeView)
        {
            DrawNodeDetails(nodeView);
            return true;
        }

        if (selection is Edge edge)
        {
            DrawTransitionDetails(edge);
            return true;
        }

        return false;
    }

    private void DrawEmptySelectionHint()
    {
        if (graphView?.GetPrimarySelection() == null)
        {
            EditorGUILayout.HelpBox("노드를 선택하면 여기에서 액션을 바로 편집할 수 있습니다.", MessageType.Info);
        }
    }

    private void DrawRightPanelMenu()
    {
        EditorGUILayout.LabelField("Boss Graph", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        BossGraphAsset nextGraph = (BossGraphAsset)EditorGUILayout.ObjectField("Graph", graphAsset, typeof(BossGraphAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            SetGraph(nextGraph);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(graphObject == null))
            {
                if (GUILayout.Button("Save"))
                {
                    SaveGraph();
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(graphObject == null))
            {
                if (GUILayout.Button("Delete Selected"))
                {
                    DeleteSelectedNodes();
                }
            }
        }

        EditorGUILayout.Space(6f);
    }

    private void DrawSettingsPanel()
    {
        DrawGraphReferences();
        EditorGUILayout.Space(8f);
        DrawPhaseSettings();
    }

    private void DrawGraphReferences()
    {
        SerializedProperty references = graphObject.FindProperty("referenceSettings");
        if (references == null)
        {
            return;
        }

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(references, new GUIContent("참조"), true);
        if (EditorGUI.EndChangeCheck())
        {
            graphObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(graphAsset);
        }

        EditorGUILayout.Space(4f);
        showActionCategories = EditorGUILayout.Foldout(showActionCategories, "Action Categories", true);
        if (showActionCategories)
        {
            DrawActionCategorySettings(references);
        }
    }

    private void DrawActionCategorySettings(SerializedProperty references)
    {
        SerializedProperty categoriesProperty = references.FindPropertyRelative("actionCategories");
        if (categoriesProperty == null)
        {
            return;
        }

        BossGraphActionCategoryAsset categories = categoriesProperty.objectReferenceValue as BossGraphActionCategoryAsset;
        if (categories == null)
        {
            EditorGUILayout.HelpBox("노드 종류별 Action 허용 규칙 SO를 참조에 넣어야 합니다.", MessageType.Warning);
            if (GUILayout.Button("Action Categories SO 생성"))
            {
                categories = CreateActionCategoryAsset();
                if (categories != null)
                {
                    categoriesProperty.objectReferenceValue = categories;
                    graphObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(graphAsset);
                }
            }

            return;
        }

        EnsureActionCategoryRules(categories);
        SerializedObject categoriesObject = new(categories);
        categoriesObject.Update();
        EditorGUI.BeginChangeCheck();
        foreach (BossGraphActionMenuItem item in BossGraphActionEditorUtility.ActionMenuItems)
        {
            BossGraphNodeKind currentKind = categories.GetNodeKind(item.ActionType);
            BossGraphNodeKind nextKind = (BossGraphNodeKind)EditorGUILayout.EnumPopup(
                BossGraphActionEditorUtility.GetActionLabel(item.ActionType),
                currentKind);
            if (nextKind != currentKind)
            {
                Undo.RecordObject(categories, "Change Boss Graph Action Category");
                categories.SetActionKind(item.ActionType, nextKind);
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(categories);
            categoriesObject.Update();
        }
    }

    private static void EnsureActionCategoryRules(BossGraphActionCategoryAsset categories)
    {
        bool changed = false;
        foreach (BossGraphActionMenuItem item in BossGraphActionEditorUtility.ActionMenuItems)
        {
            BossGraphNodeKind defaultKind = BossGraphActionCategoryAsset.GetDefaultNodeKind(item.ActionType);
            if (categories.HasActionKind(item.ActionType))
            {
                BossGraphNodeKind currentKind = categories.GetNodeKind(item.ActionType);
                if (currentKind != defaultKind
                    && (currentKind == BossGraphNodeKind.Utility || defaultKind == BossGraphNodeKind.Utility))
                {
                    categories.SetActionKind(item.ActionType, defaultKind);
                    changed = true;
                }

                continue;
            }

            categories.SetActionKind(item.ActionType, defaultKind);
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(categories);
        }
    }

    private BossGraphActionCategoryAsset CreateActionCategoryAsset()
    {
        if (graphAsset == null)
        {
            return null;
        }

        BossGraphActionCategoryAsset categories = CreateInstance<BossGraphActionCategoryAsset>();
        categories.name = $"{graphAsset.name}_ActionCategories";
        foreach (BossGraphActionMenuItem item in BossGraphActionEditorUtility.ActionMenuItems)
        {
            categories.SetActionKind(item.ActionType, BossGraphActionCategoryAsset.GetDefaultNodeKind(item.ActionType));
        }

        Undo.RegisterCreatedObjectUndo(categories, "Create Boss Graph Action Categories");
        AssetDatabase.AddObjectToAsset(categories, graphAsset);
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graphAsset));
        AssetDatabase.SaveAssets();
        return categories;
    }

    private BossGraphActionCategoryAsset GetActionCategoryAsset()
    {
        SerializedProperty references = graphObject?.FindProperty("referenceSettings");
        return references?.FindPropertyRelative("actionCategories")?.objectReferenceValue as BossGraphActionCategoryAsset;
    }

    private bool TryGetSelectedNodeKind(out BossGraphNodeKind nodeKind)
    {
        nodeKind = BossGraphNodeKind.Attack;
        if (graphObject == null || graphView?.GetPrimarySelection() is not BossGraphNodeView nodeView)
        {
            return false;
        }

        SerializedProperty stateNodes = graphObject.FindProperty("stateNodes");
        if (stateNodes == null || nodeView.NodeIndex < 0 || nodeView.NodeIndex >= stateNodes.arraySize)
        {
            return false;
        }

        SerializedProperty node = stateNodes.GetArrayElementAtIndex(nodeView.NodeIndex);
        nodeKind = (BossGraphNodeKind)GetEnum(node, "nodeKind", (int)BossGraphNodeKind.Attack);
        return true;
    }

    private void DrawGraphValidation()
    {
        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
        BossGraphValidationUtility.DrawMessages(BossGraphValidationUtility.Validate(graphObject), 5);
    }

    private void DrawPatternPanel()
    {
        SerializedProperty patterns = graphObject.FindProperty("patterns");
        if (patterns == null)
        {
            return;
        }

        DrawEmptySelectionHint();
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Patterns", EditorStyles.boldLabel);

        bool changed = false;
        EditorGUI.BeginChangeCheck();
        for (int i = 0; i < patterns.arraySize; i++)
        {
            SerializedProperty pattern = patterns.GetArrayElementAtIndex(i);
            if (DrawPatternItem(pattern, i, patterns, out bool deletedPattern))
            {
                changed = true;
            }

            if (deletedPattern)
            {
                break;
            }
        }

        changed |= EditorGUI.EndChangeCheck();
        if (changed)
        {
            WriteNodePositionsFromCurrentView();
            SyncGuidReferences();
            graphObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(graphAsset);
            RefreshGroupFramesFromNodePositions();
        }
    }

    private bool DrawPatternItem(
        SerializedProperty pattern,
        int patternIndex,
        SerializedProperty patterns,
        out bool deletedPattern)
    {
        deletedPattern = false;
        bool changed = false;
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string fallbackLabel = $"Pattern {patternIndex + 1}";
                string patternIdValue = GetString(pattern, "patternId", fallbackLabel);
                pattern.isExpanded = EditorGUILayout.Foldout(
                    pattern.isExpanded,
                    patternIdValue,
                    true);

                if (GUILayout.Button("Del", EditorStyles.miniButton, GUILayout.Width(34f)))
                {
                    DeletePattern(patterns, patternIndex, patternIdValue);
                    deletedPattern = true;
                    return true;
                }
            }

            if (!pattern.isExpanded)
            {
                return changed;
            }

            EditorGUI.indentLevel++;
            SerializedProperty patternId = pattern.FindPropertyRelative("patternId");
            if (patternId != null)
            {
                EditorGUILayout.PropertyField(patternId, new GUIContent("Pattern Id"));
            }

            SerializedProperty nodeIds = pattern.FindPropertyRelative("nodeIds");
            if (nodeIds != null)
            {
                changed |= DrawPatternNodeIds(nodeIds);
            }

            EditorGUI.indentLevel--;
        }

        return changed;
    }

    private bool DrawPatternNodeIds(SerializedProperty nodeIds)
    {
        bool changed = false;
        EditorGUILayout.LabelField("Nodes", EditorStyles.boldLabel);
        if (nodeIds.arraySize == 0)
        {
            EditorGUILayout.HelpBox("그래프 노드를 드롭해서 패턴에 추가하세요.", MessageType.Info);
        }

        for (int i = 0; i < nodeIds.arraySize; i++)
        {
            SerializedProperty nodeId = nodeIds.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.HorizontalScope())
            {
                string displayName = GetPatternNodeDisplayName(nodeId.stringValue);
                if (GUILayout.Button(displayName, EditorStyles.miniButtonLeft))
                {
                    graphView?.SelectNode(nodeId.stringValue);
                    detailsPanel?.MarkDirtyRepaint();
                }

                using (new EditorGUI.DisabledScope(i == 0))
                {
                    if (GUILayout.Button("Up", EditorStyles.miniButtonMid, GUILayout.Width(34f)))
                    {
                        Undo.RecordObject(graphAsset, "Edit Boss Graph Pattern Nodes");
                        nodeIds.MoveArrayElement(i, i - 1);
                        changed = true;
                    }
                }

                using (new EditorGUI.DisabledScope(i >= nodeIds.arraySize - 1))
                {
                    if (GUILayout.Button("Down", EditorStyles.miniButtonMid, GUILayout.Width(48f)))
                    {
                        Undo.RecordObject(graphAsset, "Edit Boss Graph Pattern Nodes");
                        nodeIds.MoveArrayElement(i, i + 1);
                        changed = true;
                    }
                }

                if (GUILayout.Button("Del", EditorStyles.miniButtonRight, GUILayout.Width(34f)))
                {
                    Undo.RecordObject(graphAsset, "Edit Boss Graph Pattern Nodes");
                    nodeIds.DeleteArrayElementAtIndex(i);
                    changed = true;
                    break;
                }
            }
        }

        Rect dropRect = GUILayoutUtility.GetRect(0f, 36f, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, "Drop Graph Nodes Here", EditorStyles.helpBox);
        changed |= HandlePatternNodeDrop(dropRect, nodeIds);
        return changed;
    }

    private void DeletePattern(SerializedProperty patterns, int patternIndex, string patternId)
    {
        if (patterns == null || patternIndex < 0 || patternIndex >= patterns.arraySize)
        {
            return;
        }

        Undo.RecordObject(graphAsset, "Delete Boss Graph Pattern");
        SerializedProperty pattern = patterns.GetArrayElementAtIndex(patternIndex);
        HashSet<string> patternNodeIds = ReadPatternNodeIdSet(pattern);
        patterns.DeleteArrayElementAtIndex(patternIndex);
        RemovePhasePatternReferences(patternId);
        RemoveTransitionsInsidePattern(patternNodeIds);
        SyncGuidReferences();
        graphObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(graphAsset);
        RefreshGroupFramesFromNodePositions();
        ScheduleRebuildGraph();
    }

    private static HashSet<string> ReadPatternNodeIdSet(SerializedProperty pattern)
    {
        HashSet<string> nodeIds = new(StringComparer.Ordinal);
        SerializedProperty nodeIdsProperty = pattern?.FindPropertyRelative("nodeIds");
        if (nodeIdsProperty == null)
        {
            return nodeIds;
        }

        for (int i = 0; i < nodeIdsProperty.arraySize; i++)
        {
            string nodeId = nodeIdsProperty.GetArrayElementAtIndex(i).stringValue;
            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                nodeIds.Add(nodeId);
            }
        }

        return nodeIds;
    }

    private void RemovePhasePatternReferences(string patternId)
    {
        if (string.IsNullOrWhiteSpace(patternId))
        {
            return;
        }

        SerializedProperty phases = graphObject.FindProperty("phases");
        if (phases == null)
        {
            return;
        }

        for (int phaseIndex = 0; phaseIndex < phases.arraySize; phaseIndex++)
        {
            SerializedProperty entries = phases.GetArrayElementAtIndex(phaseIndex).FindPropertyRelative("patterns");
            if (entries == null)
            {
                continue;
            }

            for (int entryIndex = entries.arraySize - 1; entryIndex >= 0; entryIndex--)
            {
                if (GetString(entries.GetArrayElementAtIndex(entryIndex), "patternId", string.Empty) == patternId)
                {
                    entries.DeleteArrayElementAtIndex(entryIndex);
                }
            }
        }
    }

    private void RemoveTransitionsInsidePattern(HashSet<string> patternNodeIds)
    {
        if (patternNodeIds == null || patternNodeIds.Count < 2)
        {
            return;
        }

        SerializedProperty transitions = graphObject.FindProperty("transitions");
        if (transitions == null)
        {
            return;
        }

        for (int i = transitions.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty transition = transitions.GetArrayElementAtIndex(i);
            string fromNodeId = GetString(transition, "fromNodeId", string.Empty);
            string toNodeId = GetString(transition, "toNodeId", string.Empty);
            if (patternNodeIds.Contains(fromNodeId) && patternNodeIds.Contains(toNodeId))
            {
                transitions.DeleteArrayElementAtIndex(i);
            }
        }
    }

    private bool HandlePatternNodeDrop(Rect dropRect, SerializedProperty nodeIds)
    {
        Event currentEvent = Event.current;
        if (!dropRect.Contains(currentEvent.mousePosition) || !TryGetDraggedNodeIds(out List<string> draggedNodeIds))
        {
            return false;
        }

        if (currentEvent.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            currentEvent.Use();
            return false;
        }

        if (currentEvent.type != EventType.DragPerform)
        {
            return false;
        }

        DragAndDrop.AcceptDrag();
        bool changed = AddPatternNodeIds(nodeIds, draggedNodeIds);
        currentEvent.Use();
        return changed;
    }

    private bool AddPatternNodeIds(SerializedProperty nodeIds, IReadOnlyList<string> draggedNodeIds)
    {
        bool changed = false;
        HashSet<string> existingNodeIds = new();
        for (int i = 0; i < nodeIds.arraySize; i++)
        {
            string existingNodeId = nodeIds.GetArrayElementAtIndex(i).stringValue;
            if (!string.IsNullOrWhiteSpace(existingNodeId))
            {
                existingNodeIds.Add(existingNodeId);
            }
        }

        for (int i = 0; i < draggedNodeIds.Count; i++)
        {
            string nodeId = draggedNodeIds[i];
            if (string.IsNullOrWhiteSpace(nodeId) || existingNodeIds.Contains(nodeId) || !HasStateNode(nodeId))
            {
                continue;
            }

            if (!changed)
            {
                Undo.RecordObject(graphAsset, "Edit Boss Graph Pattern Nodes");
            }

            int insertIndex = nodeIds.arraySize;
            nodeIds.InsertArrayElementAtIndex(insertIndex);
            nodeIds.GetArrayElementAtIndex(insertIndex).stringValue = nodeId;
            existingNodeIds.Add(nodeId);
            changed = true;
        }

        return changed;
    }

    private static bool TryGetDraggedNodeIds(out List<string> nodeIds)
    {
        nodeIds = new List<string>();
        object data = DragAndDrop.GetGenericData(BossGraphDragKeys.NodeIds);
        if (data is string[] array)
        {
            nodeIds.AddRange(array.Where(nodeId => !string.IsNullOrWhiteSpace(nodeId)));
        }
        else if (data is string singleNodeId && !string.IsNullOrWhiteSpace(singleNodeId))
        {
            nodeIds.Add(singleNodeId);
        }

        return nodeIds.Count > 0;
    }

    private string GetPatternNodeDisplayName(string nodeId)
    {
        int nodeIndex = FindStateNodeIndex(nodeId);
        if (nodeIndex < 0)
        {
            return string.IsNullOrWhiteSpace(nodeId) ? "(Missing Node)" : $"{nodeId} (Missing)";
        }

        SerializedProperty stateNodes = graphObject.FindProperty("stateNodes");
        SerializedProperty node = stateNodes.GetArrayElementAtIndex(nodeIndex);
        BossGraphNodeKind nodeKind = (BossGraphNodeKind)GetEnum(node, "nodeKind", (int)BossGraphNodeKind.Attack);
        return $"{GetNodeDisplayName(node, nodeIndex)} ({nodeKind})";
    }

    private string GetNodeDisplayName(SerializedProperty node, int nodeIndex)
    {
        string baseName = GetNodeActionDisplayName(node);
        int totalCount = CountNodeDisplayNames(baseName);
        if (totalCount <= 1)
        {
            return baseName;
        }

        int occurrence = GetNodeDisplayNameOccurrence(baseName, nodeIndex);
        return $"{baseName} {Mathf.Max(1, occurrence):00}";
    }

    private int CountNodeDisplayNames(string baseName)
    {
        SerializedProperty stateNodes = graphObject?.FindProperty("stateNodes");
        if (stateNodes == null || string.IsNullOrWhiteSpace(baseName))
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < stateNodes.arraySize; i++)
        {
            SerializedProperty node = stateNodes.GetArrayElementAtIndex(i);
            if (GetNodeActionDisplayName(node) == baseName)
            {
                count++;
            }
        }

        return count;
    }

    private int GetNodeDisplayNameOccurrence(string baseName, int nodeIndex)
    {
        SerializedProperty stateNodes = graphObject?.FindProperty("stateNodes");
        if (stateNodes == null || string.IsNullOrWhiteSpace(baseName))
        {
            return 1;
        }

        int maxIndex = Mathf.Min(nodeIndex, stateNodes.arraySize - 1);
        int occurrence = 0;
        for (int i = 0; i <= maxIndex; i++)
        {
            SerializedProperty node = stateNodes.GetArrayElementAtIndex(i);
            if (GetNodeActionDisplayName(node) == baseName)
            {
                occurrence++;
            }
        }

        return occurrence;
    }

    private static string GetNodeActionDisplayName(SerializedProperty node)
    {
        Type actionType = GetNodeActionType(node);
        if (actionType == null)
        {
            return "Empty Action";
        }

        string label = BossGraphActionEditorUtility.GetActionLabel(actionType);
        int slashIndex = label.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < label.Length - 1)
        {
            label = label.Substring(slashIndex + 1);
        }

        const string actionSuffix = " Action";
        if (label.EndsWith(actionSuffix, StringComparison.Ordinal))
        {
            label = label.Substring(0, label.Length - actionSuffix.Length);
        }

        return string.IsNullOrWhiteSpace(label) ? "Empty Action" : label;
    }

    private bool HasStateNode(string nodeId)
    {
        return FindStateNodeIndex(nodeId) >= 0;
    }

    private int FindStateNodeIndex(string nodeId)
    {
        if (graphObject == null || string.IsNullOrWhiteSpace(nodeId))
        {
            return -1;
        }

        SerializedProperty stateNodes = graphObject.FindProperty("stateNodes");
        if (stateNodes == null)
        {
            return -1;
        }

        for (int i = 0; i < stateNodes.arraySize; i++)
        {
            SerializedProperty node = stateNodes.GetArrayElementAtIndex(i);
            if (GetString(node, "nodeId", string.Empty) == nodeId)
            {
                return i;
            }
        }

        return -1;
    }

    private void DrawPhaseSettings()
    {
        SerializedProperty phases = graphObject.FindProperty("phases");
        if (phases == null)
        {
            return;
        }

        SerializedProperty patterns = graphObject.FindProperty("patterns");
        List<string> patternIds = ReadPatternIds(patterns);
        EditorGUILayout.LabelField("Phases", EditorStyles.boldLabel);
        bool changed = false;
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Phase 추가"))
            {
                AddPhase(phases);
                changed = true;
            }
        }

        EditorGUI.BeginChangeCheck();
        for (int i = 0; i < phases.arraySize; i++)
        {
            SerializedProperty phase = phases.GetArrayElementAtIndex(i);
            changed |= DrawPhaseItem(phase, i, phases, patternIds);
        }

        changed |= EditorGUI.EndChangeCheck();
        if (changed)
        {
            WriteNodePositionsFromCurrentView();
            graphObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(graphAsset);
            RefreshGroupFramesFromNodePositions();
        }
    }

    private bool DrawPhaseItem(
        SerializedProperty phase,
        int phaseArrayIndex,
        SerializedProperty phases,
        IReadOnlyList<string> patternIds)
    {
        bool changed = false;
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                int phaseIndex = GetInt(phase, "phaseIndex", phaseArrayIndex);
                phase.isExpanded = EditorGUILayout.Foldout(phase.isExpanded, $"Phase {phaseIndex + 1}", true);

                using (new EditorGUI.DisabledScope(phaseArrayIndex == 0))
                {
                    if (GUILayout.Button("Up", EditorStyles.miniButtonLeft, GUILayout.Width(34f)))
                    {
                        Undo.RecordObject(graphAsset, "Edit Boss Graph Phases");
                        phases.MoveArrayElement(phaseArrayIndex, phaseArrayIndex - 1);
                        changed = true;
                    }
                }

                using (new EditorGUI.DisabledScope(phaseArrayIndex >= phases.arraySize - 1))
                {
                    if (GUILayout.Button("Down", EditorStyles.miniButtonMid, GUILayout.Width(48f)))
                    {
                        Undo.RecordObject(graphAsset, "Edit Boss Graph Phases");
                        phases.MoveArrayElement(phaseArrayIndex, phaseArrayIndex + 1);
                        changed = true;
                    }
                }

                if (GUILayout.Button("Del", EditorStyles.miniButtonRight, GUILayout.Width(34f)))
                {
                    Undo.RecordObject(graphAsset, "Edit Boss Graph Phases");
                    phases.DeleteArrayElementAtIndex(phaseArrayIndex);
                    changed = true;
                }
            }

            if (!phase.isExpanded)
            {
                return changed;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(phase.FindPropertyRelative("phaseIndex"), new GUIContent("Phase Index"));
            EditorGUILayout.PropertyField(phase.FindPropertyRelative("selectionMode"), new GUIContent("Selection Mode"));
            EditorGUILayout.PropertyField(
                phase.FindPropertyRelative("patternIntervalSeconds"),
                new GUIContent("Pattern Interval Seconds"));

            SerializedProperty phasePatterns = phase.FindPropertyRelative("patterns");
            if (phasePatterns != null)
            {
                changed |= DrawPhasePatternEntries(phasePatterns, phaseArrayIndex, patternIds);
            }

            EditorGUI.indentLevel--;
        }

        return changed;
    }

    private bool DrawPhasePatternEntries(
        SerializedProperty phasePatterns,
        int phaseArrayIndex,
        IReadOnlyList<string> patternIds)
    {
        bool changed = false;
        EditorGUILayout.LabelField("Patterns", EditorStyles.boldLabel);
        if (patternIds.Count == 0)
        {
            EditorGUILayout.HelpBox("먼저 패턴을 만들어야 페이즈에 추가할 수 있습니다.", MessageType.Info);
            return changed;
        }

        for (int i = 0; i < phasePatterns.arraySize; i++)
        {
            SerializedProperty entry = phasePatterns.GetArrayElementAtIndex(i);
            changed |= DrawPhasePatternEntry(phasePatterns, entry, i, patternIds);
        }

        changed |= DrawAddPhasePatternRow(phasePatterns, phaseArrayIndex, patternIds);
        return changed;
    }

    private bool DrawPhasePatternEntry(
        SerializedProperty phasePatterns,
        SerializedProperty entry,
        int entryIndex,
        IReadOnlyList<string> patternIds)
    {
        bool changed = false;
        SerializedProperty patternId = entry.FindPropertyRelative("patternId");
        SerializedProperty weight = entry.FindPropertyRelative("weight");
        using (new EditorGUILayout.HorizontalScope())
        {
            string nextPatternId = DrawPatternIdPopup(patternId.stringValue, patternIds);
            if (nextPatternId != patternId.stringValue)
            {
                patternId.stringValue = nextPatternId;
                changed = true;
            }

            int nextWeight = EditorGUILayout.IntField(Mathf.Max(0, weight.intValue), GUILayout.Width(42f));
            nextWeight = Mathf.Max(0, nextWeight);
            if (nextWeight != weight.intValue)
            {
                weight.intValue = nextWeight;
                changed = true;
            }

            using (new EditorGUI.DisabledScope(entryIndex == 0))
            {
                if (GUILayout.Button("Up", EditorStyles.miniButtonLeft, GUILayout.Width(34f)))
                {
                    Undo.RecordObject(graphAsset, "Edit Boss Graph Phase Patterns");
                    phasePatterns.MoveArrayElement(entryIndex, entryIndex - 1);
                    changed = true;
                }
            }

            using (new EditorGUI.DisabledScope(entryIndex >= phasePatterns.arraySize - 1))
            {
                if (GUILayout.Button("Down", EditorStyles.miniButtonMid, GUILayout.Width(48f)))
                {
                    Undo.RecordObject(graphAsset, "Edit Boss Graph Phase Patterns");
                    phasePatterns.MoveArrayElement(entryIndex, entryIndex + 1);
                    changed = true;
                }
            }

            if (GUILayout.Button("Del", EditorStyles.miniButtonRight, GUILayout.Width(34f)))
            {
                Undo.RecordObject(graphAsset, "Edit Boss Graph Phase Patterns");
                phasePatterns.DeleteArrayElementAtIndex(entryIndex);
                changed = true;
            }
        }

        return changed;
    }

    private bool DrawAddPhasePatternRow(
        SerializedProperty phasePatterns,
        int phaseArrayIndex,
        IReadOnlyList<string> patternIds)
    {
        List<string> addablePatternIds = patternIds
            .Where(patternId => !HasPhasePatternEntry(phasePatterns, patternId))
            .ToList();
        if (addablePatternIds.Count == 0)
        {
            EditorGUILayout.HelpBox("모든 패턴이 이 페이즈에 추가되어 있습니다.", MessageType.Info);
            return false;
        }

        int selectedIndex = phasePatternAddIndexes.TryGetValue(phaseArrayIndex, out int savedIndex) ? savedIndex : 0;
        selectedIndex = Mathf.Clamp(selectedIndex, 0, addablePatternIds.Count - 1);
        using (new EditorGUILayout.HorizontalScope())
        {
            selectedIndex = EditorGUILayout.Popup(selectedIndex, addablePatternIds.ToArray());
            phasePatternAddIndexes[phaseArrayIndex] = selectedIndex;

            if (GUILayout.Button("Pattern 추가", GUILayout.Width(92f)))
            {
                AddPhasePatternEntry(phasePatterns, addablePatternIds[selectedIndex]);
                phasePatternAddIndexes[phaseArrayIndex] = 0;
                return true;
            }
        }

        return false;
    }

    private void AddPhase(SerializedProperty phases)
    {
        Undo.RecordObject(graphAsset, "Add Boss Graph Phase");
        int phaseArrayIndex = phases.arraySize;
        phases.InsertArrayElementAtIndex(phaseArrayIndex);
        SerializedProperty phase = phases.GetArrayElementAtIndex(phaseArrayIndex);
        SetInt(phase, "phaseIndex", phaseArrayIndex);
        SetEnum(phase, "selectionMode", 0);
        SetFloat(phase, "patternIntervalSeconds", 0f);
        SerializedProperty phasePatterns = phase.FindPropertyRelative("patterns");
        phasePatterns?.ClearArray();
    }

    private void AddPhasePatternEntry(SerializedProperty phasePatterns, string patternId)
    {
        if (string.IsNullOrWhiteSpace(patternId))
        {
            return;
        }

        Undo.RecordObject(graphAsset, "Add Boss Graph Phase Pattern");
        int entryIndex = phasePatterns.arraySize;
        phasePatterns.InsertArrayElementAtIndex(entryIndex);
        SerializedProperty entry = phasePatterns.GetArrayElementAtIndex(entryIndex);
        SetString(entry, "patternId", patternId);
        SetInt(entry, "weight", 1);
    }

    private static bool HasPhasePatternEntry(SerializedProperty phasePatterns, string patternId)
    {
        for (int i = 0; i < phasePatterns.arraySize; i++)
        {
            if (GetString(phasePatterns.GetArrayElementAtIndex(i), "patternId", string.Empty) == patternId)
            {
                return true;
            }
        }

        return false;
    }

    private static string DrawPatternIdPopup(string currentPatternId, IReadOnlyList<string> patternIds)
    {
        List<string> values = new();
        List<string> labels = new();
        if (string.IsNullOrWhiteSpace(currentPatternId))
        {
            values.Add(string.Empty);
            labels.Add("(None)");
        }
        else if (!patternIds.Contains(currentPatternId))
        {
            values.Add(currentPatternId);
            labels.Add($"{currentPatternId} (Missing)");
        }

        for (int i = 0; i < patternIds.Count; i++)
        {
            values.Add(patternIds[i]);
            labels.Add(patternIds[i]);
        }

        int selectedIndex = Mathf.Max(0, values.IndexOf(currentPatternId));
        int nextIndex = EditorGUILayout.Popup(selectedIndex, labels.ToArray());
        return values[nextIndex];
    }

    private static List<string> ReadPatternIds(SerializedProperty patterns)
    {
        List<string> patternIds = new();
        if (patterns == null)
        {
            return patternIds;
        }

        for (int i = 0; i < patterns.arraySize; i++)
        {
            string patternId = GetString(patterns.GetArrayElementAtIndex(i), "patternId", string.Empty);
            if (!string.IsNullOrWhiteSpace(patternId) && !patternIds.Contains(patternId))
            {
                patternIds.Add(patternId);
            }
        }

        return patternIds;
    }

    private void DrawNodeDetails(BossGraphNodeView nodeView)
    {
        SerializedProperty stateNodes = graphObject.FindProperty("stateNodes");
        if (stateNodes == null || nodeView.NodeIndex < 0 || nodeView.NodeIndex >= stateNodes.arraySize)
        {
            EditorGUILayout.HelpBox("선택한 노드 데이터를 찾을 수 없습니다.", MessageType.Warning);
            return;
        }

        SerializedProperty node = stateNodes.GetArrayElementAtIndex(nodeView.NodeIndex);
        string oldNodeId = GetString(node, "nodeId", string.Empty);
        int oldNodeKind = GetEnum(node, "nodeKind", (int)BossGraphNodeKind.Attack);
        int oldPhaseIndex = GetInt(node, "phaseIndex", nodeView.NodeIndex);

        EditorGUI.BeginChangeCheck();
        DrawNodeCoreFields(node, nodeView.NodeIndex);
        bool nodeChanged = EditorGUI.EndChangeCheck();

        string newNodeId = GetString(node, "nodeId", string.Empty);
        int newNodeKind = GetEnum(node, "nodeKind", (int)BossGraphNodeKind.Attack);
        int newPhaseIndex = GetInt(node, "phaseIndex", nodeView.NodeIndex);
        if (nodeChanged)
        {
            if (!string.IsNullOrWhiteSpace(oldNodeId) && !string.IsNullOrWhiteSpace(newNodeId) && oldNodeId != newNodeId)
            {
                UpdateTransitionNodeIds(oldNodeId, newNodeId);
            }

            graphObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(graphAsset);

            if (oldNodeKind != newNodeKind)
            {
                BossGraphNodeKind updatedNodeKind = (BossGraphNodeKind)newNodeKind;
                nodeView.SetNodeKind(updatedNodeKind, GetNodeKindColor(updatedNodeKind));
            }

            if (oldNodeId != newNodeId || oldPhaseIndex != newPhaseIndex || oldNodeKind != newNodeKind)
            {
                ScheduleRebuildGraph(newNodeId);
            }
            else
            {
                detailsPanel?.MarkDirtyRepaint();
            }
        }

        BossGraphNodeKind nodeKind = (BossGraphNodeKind)newNodeKind;
        DrawNodeActionSelector(node, nodeKind);
        Type actionType = GetNodeActionType(node);
        if (actionType != null && !IsActionAllowedForNodeKind(actionType, nodeKind))
        {
            EditorGUILayout.HelpBox("현재 Action 타입이 노드 Type과 맞지 않습니다. 이 Type에 맞는 Action을 다시 선택하세요.", MessageType.Warning);
            return;
        }

        DrawNodeActionEditor(node, nodeKind);
    }

    private void DrawTransitionDetails(Edge edge)
    {
        if (!TryGetEdgeNodeIds(edge, out string fromNodeId, out string toNodeId))
        {
            EditorGUILayout.HelpBox("선택한 연결 데이터를 찾을 수 없습니다.", MessageType.Warning);
            return;
        }

        SerializedProperty transition = FindTransitionProperty(fromNodeId, toNodeId);
        if (transition == null)
        {
            if (SaveTransitions())
            {
                graphObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(graphAsset);
                graphObject.Update();
            }

            transition = FindTransitionProperty(fromNodeId, toNodeId);
        }

        if (transition == null)
        {
            EditorGUILayout.HelpBox("선택한 연결이 아직 저장되지 않았습니다.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"{fromNodeId} -> {toNodeId}", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(transition.FindPropertyRelative("conditionType"));
        EditorGUILayout.PropertyField(transition.FindPropertyRelative("threshold"));
        EditorGUILayout.PropertyField(transition.FindPropertyRelative("phaseIndex"));
        if (!EditorGUI.EndChangeCheck())
        {
            return;
        }

        graphObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(graphAsset);
        ScheduleRebuildGraph();
    }

    private static bool TryGetEdgeNodeIds(Edge edge, out string fromNodeId, out string toNodeId)
    {
        fromNodeId = string.Empty;
        toNodeId = string.Empty;
        if (edge?.output?.node is not BossGraphNodeView fromNode
            || edge.input?.node is not BossGraphNodeView toNode
            || string.IsNullOrWhiteSpace(fromNode.NodeId)
            || string.IsNullOrWhiteSpace(toNode.NodeId))
        {
            return false;
        }

        fromNodeId = fromNode.NodeId;
        toNodeId = toNode.NodeId;
        return true;
    }

    private SerializedProperty FindTransitionProperty(string fromNodeId, string toNodeId)
    {
        SerializedProperty transitions = graphObject.FindProperty("transitions");
        if (transitions == null)
        {
            return null;
        }

        for (int i = 0; i < transitions.arraySize; i++)
        {
            SerializedProperty transition = transitions.GetArrayElementAtIndex(i);
            if (GetString(transition, "fromNodeId", string.Empty) == fromNodeId
                && GetString(transition, "toNodeId", string.Empty) == toNodeId)
            {
                return transition;
            }
        }

        return null;
    }

    private void DrawNodeCoreFields(SerializedProperty node, int nodeIndex)
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Action Name", GetNodeDisplayName(node, nodeIndex));
        }

        EditorGUILayout.PropertyField(node.FindPropertyRelative("nodeKind"), new GUIContent("Type"));
    }

    private static BossGraphActionAsset GetNodeActionAsset(SerializedProperty node)
    {
        SerializedProperty sequences = node?.FindPropertyRelative("sequences");
        if (sequences == null || sequences.arraySize == 0)
        {
            return null;
        }

        SerializedProperty entry = sequences.GetArrayElementAtIndex(0);
        return entry.FindPropertyRelative("sequence")?.objectReferenceValue as BossGraphActionAsset;
    }

    private static SerializedProperty GetNodeActionProperty(SerializedProperty node)
    {
        return node?.FindPropertyRelative("action");
    }

    private void DrawNodeActionSelector(SerializedProperty node, BossGraphNodeKind nodeKind)
    {
        List<BossGraphActionMenuItem> allowedActions = GetAllowedActionMenuItems(nodeKind);
        if (allowedActions.Count == 0)
        {
            EditorGUILayout.HelpBox("이 타입에 설정 가능한 Action이 없습니다.", MessageType.Warning);
            return;
        }

        Type currentActionType = GetNodeActionType(node);
        List<GUIContent> labels = new() { new GUIContent("<선택>") };
        int currentIndex = 0;
        for (int i = 0; i < allowedActions.Count; i++)
        {
            BossGraphActionMenuItem item = allowedActions[i];
            labels.Add(new GUIContent(item.MenuPath));
            if (currentActionType == item.ActionType)
            {
                currentIndex = i + 1;
            }
        }

        EditorGUI.BeginChangeCheck();
        int nextIndex = EditorGUILayout.Popup(new GUIContent("Action"), currentIndex, labels.ToArray());
        if (!EditorGUI.EndChangeCheck() || nextIndex <= 0)
        {
            return;
        }

        SetNodeAction(node, allowedActions[nextIndex - 1]);
    }

    private List<BossGraphActionMenuItem> GetAllowedActionMenuItems(BossGraphNodeKind nodeKind)
    {
        BossGraphActionCategoryAsset categories = GetActionCategoryAsset();
        return BossGraphActionEditorUtility.ActionMenuItems
            .Where(item => GetEffectiveActionNodeKind(item.ActionType, categories) == nodeKind)
            .ToList();
    }

    private static BossGraphNodeKind GetEffectiveActionNodeKind(Type actionType, BossGraphActionCategoryAsset categories)
    {
        BossGraphNodeKind defaultKind = BossGraphActionCategoryAsset.GetDefaultNodeKind(actionType);
        BossGraphNodeKind configuredKind = categories != null
            ? categories.GetNodeKind(actionType)
            : defaultKind;

        if (defaultKind == BossGraphNodeKind.Utility)
        {
            return BossGraphNodeKind.Utility;
        }

        if (configuredKind == BossGraphNodeKind.Utility)
        {
            return defaultKind;
        }

        return configuredKind;
    }

    private bool IsActionAllowedForNodeKind(Type actionType, BossGraphNodeKind nodeKind)
    {
        if (actionType == null)
        {
            return false;
        }

        return GetEffectiveActionNodeKind(actionType, GetActionCategoryAsset()) == nodeKind;
    }

    private static Type GetNodeActionType(SerializedProperty node)
    {
        BossAction directAction = GetNodeActionProperty(node)?.managedReferenceValue as BossAction;
        return directAction?.GetType() ?? GetNodeActionType(GetNodeActionAsset(node));
    }

    private static Type GetNodeActionType(BossGraphActionAsset actionAsset)
    {
        return actionAsset?.Action?.GetType();
    }

    private void SetNodeAction(SerializedProperty node, BossGraphActionMenuItem actionItem)
    {
        SerializedProperty action = GetNodeActionProperty(node);
        if (action == null)
        {
            return;
        }

        string nodeId = GetString(node, "nodeId", string.Empty);
        Undo.RecordObject(graphAsset, "Change Boss Graph Node Action");
        action.managedReferenceValue = actionItem.Create();
        node.FindPropertyRelative("sequences")?.ClearArray();
        graphObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(graphAsset);

        SelectActionAsset(null);
        SelectNodeDetails(nodeId);
        ScheduleRebuildGraph(nodeId);
        detailsPanel?.MarkDirtyRepaint();
    }

    private static void SetActionAssetSingleAction(BossGraphActionAsset actionAsset, BossGraphActionMenuItem actionItem)
    {
        if (actionAsset == null)
        {
            return;
        }

        Undo.RecordObject(actionAsset, "Change Boss Graph Action");
        SerializedObject actionObject = new(actionAsset);
        SerializedProperty actions = actionObject.FindProperty("actions");
        if (actions == null)
        {
            return;
        }

        actions.ClearArray();
        actions.InsertArrayElementAtIndex(0);
        actions.GetArrayElementAtIndex(0).managedReferenceValue = actionItem.Create();
        actionObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(actionAsset);
        AssetDatabase.SaveAssets();
    }

    private void DrawNodeActionEditor(SerializedProperty node, BossGraphNodeKind nodeKind)
    {
        SerializedProperty action = GetNodeActionProperty(node);
        if (action != null && action.managedReferenceValue != null)
        {
            selectedActionAsset = null;
            DestroyActionEditor();

            EditorGUILayout.Space(6f);
            BossGraphProjectileNameOptions.Set(graphProjectileNames);
            BossGraphActionFilterContext.Set(nodeKind, GetActionCategoryAsset());
            try
            {
                BossGraphBossHierarchyOptions.Set(bossHierarchyRoot);
                BossGraphAimStartNodeOptions.Set(graphObject);
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(action, new GUIContent(GetNodeActionLabel(action)), true);
                if (EditorGUI.EndChangeCheck())
                {
                    graphObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(graphAsset);
                    detailsPanel?.MarkDirtyRepaint();
                }
            }
            finally
            {
                BossGraphBossHierarchyOptions.Clear();
                BossGraphAimStartNodeOptions.Clear();
                BossGraphActionFilterContext.Clear();
                BossGraphProjectileNameOptions.Clear();
            }

            return;
        }

        BossGraphActionAsset actionAsset = GetNodeActionAsset(node);
        if (actionAsset == null)
        {
            return;
        }

        EditorGUILayout.Space(6f);
        if (selectedActionEditor == null || selectedActionEditor.target != actionAsset)
        {
            DestroyActionEditor();
            selectedActionAsset = actionAsset;
            selectedActionEditor = Editor.CreateEditor(actionAsset);
        }

        BossGraphProjectileNameOptions.Set(graphProjectileNames);
        BossGraphActionFilterContext.Set(nodeKind, GetActionCategoryAsset());
        try
        {
            BossGraphBossHierarchyOptions.Set(bossHierarchyRoot);
            BossGraphAimStartNodeOptions.Set(graphObject);
            selectedActionEditor?.OnInspectorGUI();
        }
        finally
        {
            BossGraphBossHierarchyOptions.Clear();
            BossGraphAimStartNodeOptions.Clear();
            BossGraphActionFilterContext.Clear();
            BossGraphProjectileNameOptions.Clear();
        }
    }

    private static string GetNodeActionLabel(SerializedProperty action)
    {
        object value = action?.managedReferenceValue;
        return value == null
            ? "Action"
            : ObjectNames.NicifyVariableName(value.GetType().Name);
    }

    private void SelectActionAsset(BossGraphActionAsset sequence)
    {
        if (selectedActionAsset == sequence)
        {
            return;
        }

        selectedActionAsset = sequence;
        DestroyActionEditor();
    }

    private void DestroyActionEditor()
    {
        if (selectedActionEditor == null)
        {
            return;
        }

        DestroyImmediate(selectedActionEditor);
        selectedActionEditor = null;
    }

    private void UpdateTransitionNodeIds(string oldNodeId, string newNodeId)
    {
        SerializedProperty transitions = graphObject.FindProperty("transitions");
        if (transitions == null)
        {
            UpdatePatternNodeIds(oldNodeId, newNodeId);
            return;
        }

        for (int i = 0; i < transitions.arraySize; i++)
        {
            SerializedProperty transition = transitions.GetArrayElementAtIndex(i);
            if (GetString(transition, "fromNodeId", string.Empty) == oldNodeId)
            {
                SetString(transition, "fromNodeId", newNodeId);
            }

            if (GetString(transition, "toNodeId", string.Empty) == oldNodeId)
            {
                SetString(transition, "toNodeId", newNodeId);
            }
        }

        UpdatePatternNodeIds(oldNodeId, newNodeId);
    }

    private void UpdatePatternNodeIds(string oldNodeId, string newNodeId)
    {
        SerializedProperty patterns = graphObject.FindProperty("patterns");
        if (patterns == null)
        {
            return;
        }

        for (int patternIndex = 0; patternIndex < patterns.arraySize; patternIndex++)
        {
            SerializedProperty nodeIds = patterns.GetArrayElementAtIndex(patternIndex).FindPropertyRelative("nodeIds");
            if (nodeIds == null)
            {
                continue;
            }

            for (int nodeIndex = 0; nodeIndex < nodeIds.arraySize; nodeIndex++)
            {
                SerializedProperty nodeId = nodeIds.GetArrayElementAtIndex(nodeIndex);
                if (nodeId.stringValue == oldNodeId)
                {
                    nodeId.stringValue = newNodeId;
                }
            }
        }
    }

    private void ScheduleRebuildGraph(string nodeIdToRestore = null)
    {
        if (rebuildQueued)
        {
            return;
        }

        string selectedNodeId = !string.IsNullOrWhiteSpace(nodeIdToRestore)
            ? nodeIdToRestore
            : GetSelectedNodeId();
        bool restoreSelectedNodeDetails = detailsTabIndex == SelectedElementDetailsIndex
            && !string.IsNullOrWhiteSpace(selectedNodeId);
        rebuildQueued = true;
        EditorApplication.delayCall += () =>
        {
            rebuildQueued = false;
            if (this == null)
            {
                return;
            }

            RebuildGraph();
            if (restoreSelectedNodeDetails)
            {
                SelectNodeDetails(selectedNodeId);
            }

            detailsPanel?.MarkDirtyRepaint();
        };
    }

    private void RemoveTransitionsForNodes(HashSet<string> nodeIds)
    {
        if (nodeIds == null || nodeIds.Count == 0)
        {
            return;
        }

        SerializedProperty transitions = graphObject.FindProperty("transitions");
        if (transitions == null)
        {
            return;
        }

        for (int i = transitions.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty transition = transitions.GetArrayElementAtIndex(i);
            string fromNodeId = GetString(transition, "fromNodeId", string.Empty);
            string toNodeId = GetString(transition, "toNodeId", string.Empty);
            if (nodeIds.Contains(fromNodeId) || nodeIds.Contains(toNodeId))
            {
                transitions.DeleteArrayElementAtIndex(i);
            }
        }
    }

    private void RemovePatternNodeReferences(HashSet<string> nodeIds)
    {
        if (nodeIds == null || nodeIds.Count == 0)
        {
            return;
        }

        SerializedProperty patterns = graphObject.FindProperty("patterns");
        if (patterns == null)
        {
            return;
        }

        for (int patternIndex = 0; patternIndex < patterns.arraySize; patternIndex++)
        {
            SerializedProperty nodeIdsProperty = patterns.GetArrayElementAtIndex(patternIndex).FindPropertyRelative("nodeIds");
            if (nodeIdsProperty == null)
            {
                continue;
            }

            for (int nodeIndex = nodeIdsProperty.arraySize - 1; nodeIndex >= 0; nodeIndex--)
            {
                if (nodeIds.Contains(nodeIdsProperty.GetArrayElementAtIndex(nodeIndex).stringValue))
                {
                    nodeIdsProperty.DeleteArrayElementAtIndex(nodeIndex);
                }
            }
        }
    }

    private static HashSet<string> GetNodeIds(SerializedProperty stateNodes, List<int> indexes)
    {
        HashSet<string> nodeIds = new();
        for (int i = 0; i < indexes.Count; i++)
        {
            int index = indexes[i];
            if (index < 0 || index >= stateNodes.arraySize)
            {
                continue;
            }

            SerializedProperty node = stateNodes.GetArrayElementAtIndex(index);
            string nodeId = GetString(node, "nodeId", string.Empty);
            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                nodeIds.Add(nodeId);
            }
        }

        return nodeIds;
    }

    private static Dictionary<string, Vector2> BuildPatternLayout(
        SerializedProperty stateNodes,
        SerializedProperty patterns,
        SerializedProperty phases,
        out List<FrameLayout> phaseFrames)
    {
        Dictionary<string, Vector2> nodePositions = new();
        phaseFrames = new List<FrameLayout>();
        if (stateNodes == null || patterns == null || patterns.arraySize == 0)
        {
            return nodePositions;
        }

        Dictionary<string, List<string>> patternNodeIds = ReadPatternNodeIds(patterns);
        Dictionary<string, Rect> nodeRects = new();
        for (int nodeIndex = 0; nodeIndex < stateNodes.arraySize; nodeIndex++)
        {
            SerializedProperty node = stateNodes.GetArrayElementAtIndex(nodeIndex);
            string nodeId = GetString(node, "nodeId", string.Empty);
            if (string.IsNullOrWhiteSpace(nodeId) || nodeRects.ContainsKey(nodeId))
            {
                continue;
            }

            Vector2 position = GetVector2(node, "editorPosition", new Vector2(80f + nodeIndex * 260f, 120f));
            if (position == Vector2.zero)
            {
                position = new Vector2(80f + nodeIndex * 260f, 120f);
            }

            nodeRects[nodeId] = new Rect(position, new Vector2(GraphNodeWidth, GraphNodeHeight));
        }

        Dictionary<string, Rect> patternBounds = new();
        foreach (KeyValuePair<string, List<string>> pair in patternNodeIds)
        {
            bool hasBounds = false;
            Rect bounds = default;
            for (int nodeIndex = 0; nodeIndex < pair.Value.Count; nodeIndex++)
            {
                string nodeId = pair.Value[nodeIndex];
                if (string.IsNullOrWhiteSpace(nodeId) || !nodeRects.TryGetValue(nodeId, out Rect nodeRect))
                {
                    continue;
                }

                bounds = hasBounds ? Encapsulate(bounds, nodeRect) : nodeRect;
                hasBounds = true;
            }

            if (hasBounds)
            {
                patternBounds[pair.Key] = bounds;
            }
        }

        BuildPhaseFrames(phases, patternBounds, phaseFrames);
        return nodePositions;
    }

    private static Dictionary<string, List<string>> ReadPatternNodeIds(SerializedProperty patterns)
    {
        Dictionary<string, List<string>> patternNodeIds = new();
        for (int patternIndex = 0; patternIndex < patterns.arraySize; patternIndex++)
        {
            SerializedProperty pattern = patterns.GetArrayElementAtIndex(patternIndex);
            string patternId = GetString(pattern, "patternId", string.Empty);
            if (string.IsNullOrWhiteSpace(patternId) || patternNodeIds.ContainsKey(patternId))
            {
                continue;
            }

            List<string> nodeIds = new();
            SerializedProperty nodeIdsProperty = pattern.FindPropertyRelative("nodeIds");
            if (nodeIdsProperty != null)
            {
                for (int nodeIndex = 0; nodeIndex < nodeIdsProperty.arraySize; nodeIndex++)
                {
                    string nodeId = nodeIdsProperty.GetArrayElementAtIndex(nodeIndex).stringValue;
                    if (!string.IsNullOrWhiteSpace(nodeId))
                    {
                        nodeIds.Add(nodeId);
                    }
                }
            }

            patternNodeIds[patternId] = nodeIds;
        }

        return patternNodeIds;
    }

    private static void BuildPhaseFrames(
        SerializedProperty phases,
        IReadOnlyDictionary<string, Rect> patternBounds,
        List<FrameLayout> phaseFrames)
    {
        if (phases == null || phases.arraySize == 0)
        {
            return;
        }

        for (int phaseArrayIndex = 0; phaseArrayIndex < phases.arraySize; phaseArrayIndex++)
        {
            SerializedProperty phase = phases.GetArrayElementAtIndex(phaseArrayIndex);
            SerializedProperty patternEntries = phase.FindPropertyRelative("patterns");
            if (patternEntries == null || patternEntries.arraySize == 0)
            {
                continue;
            }

            bool hasBounds = false;
            Rect bounds = default;
            for (int entryIndex = 0; entryIndex < patternEntries.arraySize; entryIndex++)
            {
                string patternId = GetString(patternEntries.GetArrayElementAtIndex(entryIndex), "patternId", string.Empty);
                if (string.IsNullOrWhiteSpace(patternId) || !patternBounds.TryGetValue(patternId, out Rect patternRect))
                {
                    continue;
                }

                bounds = hasBounds ? Encapsulate(bounds, patternRect) : patternRect;
                hasBounds = true;
            }

            if (!hasBounds)
            {
                continue;
            }

            float padding = PhaseFramePadding + phaseArrayIndex * 10f;
            Rect frameRect = Rect.MinMaxRect(
                bounds.xMin - padding,
                bounds.yMin - padding,
                bounds.xMax + padding,
                bounds.yMax + padding);
            int phaseIndex = GetInt(phase, "phaseIndex", phaseArrayIndex);
            phaseFrames.Add(new FrameLayout(frameRect, $"Phase {phaseIndex + 1}", GetPhaseFrameColor(phaseArrayIndex)));
        }
    }

    private static Rect Encapsulate(Rect first, Rect second)
    {
        return Rect.MinMaxRect(
            Mathf.Min(first.xMin, second.xMin),
            Mathf.Min(first.yMin, second.yMin),
            Mathf.Max(first.xMax, second.xMax),
            Mathf.Max(first.yMax, second.yMax));
    }

    private static Rect ExpandRect(Rect rect, float padding)
    {
        return Rect.MinMaxRect(
            rect.xMin - padding,
            rect.yMin - padding,
            rect.xMax + padding,
            rect.yMax + padding);
    }

    private static Color GetPatternFrameColor(int index)
    {
        return (index % 4) switch
        {
            0 => new Color(0.95f, 0.95f, 0.95f, 1f),
            1 => new Color(0.48f, 0.86f, 1f, 1f),
            2 => new Color(0.78f, 1f, 0.55f, 1f),
            _ => new Color(1f, 0.62f, 0.82f, 1f)
        };
    }

    private static Color GetPhaseFrameColor(int index)
    {
        return (index % 4) switch
        {
            0 => new Color(1f, 0.85f, 0.18f, 1f),
            1 => new Color(0.28f, 0.78f, 1f, 1f),
            2 => new Color(0.55f, 1f, 0.45f, 1f),
            _ => new Color(1f, 0.42f, 0.42f, 1f)
        };
    }

    private static Color GetNodeKindColor(BossGraphNodeKind nodeKind)
    {
        return nodeKind switch
        {
            BossGraphNodeKind.Attack => new Color(0.72f, 0.12f, 0.1f, 1f),
            BossGraphNodeKind.Move => new Color(0.12f, 0.38f, 0.78f, 1f),
            BossGraphNodeKind.Animation => new Color(0.45f, 0.22f, 0.72f, 1f),
            BossGraphNodeKind.Utility => new Color(0.24f, 0.42f, 0.34f, 1f),
            _ => new Color(0.25f, 0.25f, 0.25f, 1f)
        };
    }

    private static string GetUniqueElementId(SerializedProperty elements, string idPropertyName, string prefix)
    {
        HashSet<string> existingIds = new();
        if (elements != null)
        {
            for (int i = 0; i < elements.arraySize; i++)
            {
                string id = GetString(elements.GetArrayElementAtIndex(i), idPropertyName, string.Empty);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    existingIds.Add(id);
                }
            }
        }

        for (int i = 1; i < 1000; i++)
        {
            string candidate = $"{prefix}{i}";
            if (!existingIds.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"{prefix}{Guid.NewGuid():N}";
    }

    private static string GetString(SerializedProperty root, string childName, string fallback)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        return child != null && !string.IsNullOrWhiteSpace(child.stringValue) ? child.stringValue : fallback;
    }

    private static int GetInt(SerializedProperty root, string childName, int fallback)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        return child != null ? child.intValue : fallback;
    }

    private static int GetEnum(SerializedProperty root, string childName, int fallback)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        return child != null ? child.enumValueIndex : fallback;
    }

    private static float GetFloat(SerializedProperty root, string childName, float fallback)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        return child != null ? child.floatValue : fallback;
    }

    private static bool GetBool(SerializedProperty root, string childName, bool fallback)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        return child != null ? child.boolValue : fallback;
    }

    private static string GetEnumDisplayName(SerializedProperty root, string childName, string fallback)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child == null || child.enumDisplayNames == null || child.enumDisplayNames.Length == 0)
        {
            return fallback;
        }

        int index = Mathf.Clamp(child.enumValueIndex, 0, child.enumDisplayNames.Length - 1);
        return child.enumDisplayNames[index];
    }

    private static Vector2 GetVector2(SerializedProperty root, string childName, Vector2 fallback)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        return child != null ? child.vector2Value : fallback;
    }

    private static Vector3 GetVector3(SerializedProperty root, string childName, Vector3 fallback)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        return child != null ? child.vector3Value : fallback;
    }

    private static void SetString(SerializedProperty root, string childName, string value)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child != null)
        {
            child.stringValue = value;
        }
    }

    private static void SetInt(SerializedProperty root, string childName, int value)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child != null)
        {
            child.intValue = value;
        }
    }

    private static void SetEnum(SerializedProperty root, string childName, int value)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child != null)
        {
            child.enumValueIndex = value;
        }
    }

    private static void SetFloat(SerializedProperty root, string childName, float value)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child != null)
        {
            child.floatValue = value;
        }
    }

    private static void SetVector2(SerializedProperty root, string childName, Vector2 value)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child != null)
        {
            child.vector2Value = value;
        }
    }

    private readonly struct TransitionEndpoint
    {
        public TransitionEndpoint(string fromNodeId, string toNodeId)
        {
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
        }

        public string FromNodeId { get; }
        public string ToNodeId { get; }
    }

    private readonly struct TransitionValues
    {
        public static readonly TransitionValues Default = new(
            (int)BossTransitionConditionType.SequenceEnded,
            0f,
            0);

        public TransitionValues(int conditionType, float threshold, int phaseIndex)
        {
            ConditionType = conditionType;
            Threshold = threshold;
            PhaseIndex = phaseIndex;
        }

        public int ConditionType { get; }
        public float Threshold { get; }
        public int PhaseIndex { get; }
    }

    private readonly struct TransitionSnapshot
    {
        public TransitionSnapshot(TransitionEndpoint endpoint, TransitionValues values)
        {
            Endpoint = endpoint;
            Values = values;
        }

        public TransitionEndpoint Endpoint { get; }
        public TransitionValues Values { get; }
    }

    private sealed class PatternSnapshot
    {
        public PatternSnapshot(string patternId, List<string> nodeIds)
        {
            PatternId = patternId;
            NodeIds = nodeIds ?? new List<string>();
        }

        public string PatternId { get; }
        public List<string> NodeIds { get; }
    }

    private readonly struct PhasePatternEntrySnapshot
    {
        public PhasePatternEntrySnapshot(string patternId, int weight)
        {
            PatternId = patternId;
            Weight = weight;
        }

        public string PatternId { get; }
        public int Weight { get; }
    }

    private readonly struct FrameLayout
    {
        public FrameLayout(Rect rect, string label, Color color)
        {
            Rect = rect;
            Label = label;
            Color = color;
        }

        public Rect Rect { get; }
        public string Label { get; }
        public Color Color { get; }
    }

}

internal static class BossGraphDragKeys
{
    public const string NodeIds = "Week14.BossGraph.NodeIds";
    public const string BossChildPath = "Week14.BossGraph.BossChildPath";
}

internal sealed class BossGraphView : GraphView
{
    private readonly List<BossGraphNodeView> nodeViews = new();
    private readonly List<BossGraphFrameView> patternFrameViews = new();
    private readonly List<BossGraphFrameView> phaseFrameViews = new();
    private string runtimeHighlightedNodeId;
    private string runtimeHighlightedEdgeKey;

    public BossGraphView()
    {
        Insert(0, new GridBackground());
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
    }

    public IReadOnlyList<BossGraphNodeView> NodeViews => nodeViews;
    public List<Edge> EdgeViews => edges.ToList();

    public Vector2 LocalToContentPosition(Vector2 localPosition)
    {
        return this.ChangeCoordinatesTo(contentViewContainer, localPosition);
    }

    public void ClearGraph()
    {
        DeleteElements(graphElements.ToList());
        nodeViews.Clear();
        patternFrameViews.Clear();
        phaseFrameViews.Clear();
        runtimeHighlightedNodeId = null;
        runtimeHighlightedEdgeKey = null;
    }

    public void AddStateNode(BossGraphNodeView nodeView)
    {
        nodeViews.Add(nodeView);
        AddElement(nodeView);
    }

    public void AddTransitionEdge(BossGraphNodeView fromNode, BossGraphNodeView toNode, string label)
    {
        if (fromNode == null || toNode == null || fromNode.OutputPort == null || toNode.InputPort == null)
        {
            return;
        }

        BossGraphEdgeView edge = new(label)
        {
            output = fromNode.OutputPort,
            input = toNode.InputPort
        };
        edge.output.Connect(edge);
        edge.input.Connect(edge);
        AddElement(edge);
    }

    public void ReplacePatternFrames(IEnumerable<BossGraphFrameView> patternFrames)
    {
        for (int i = 0; i < patternFrameViews.Count; i++)
        {
            RemoveElement(patternFrameViews[i]);
        }

        patternFrameViews.Clear();
        if (patternFrames == null)
        {
            return;
        }

        foreach (BossGraphFrameView patternFrame in patternFrames)
        {
            AddPatternFrame(patternFrame);
        }
    }

    public void AddPatternFrame(BossGraphFrameView patternFrame)
    {
        if (patternFrame == null)
        {
            return;
        }

        patternFrameViews.Add(patternFrame);
        AddElement(patternFrame);
        patternFrame.SendToBack();
    }

    public void ReplacePhaseFrames(IEnumerable<BossGraphFrameView> phaseFrames)
    {
        for (int i = 0; i < phaseFrameViews.Count; i++)
        {
            RemoveElement(phaseFrameViews[i]);
        }

        phaseFrameViews.Clear();
        if (phaseFrames == null)
        {
            return;
        }

        foreach (BossGraphFrameView phaseFrame in phaseFrames)
        {
            AddPhaseFrame(phaseFrame);
        }
    }

    public void AddPhaseFrame(BossGraphFrameView phaseFrame)
    {
        if (phaseFrame == null)
        {
            return;
        }

        phaseFrameViews.Add(phaseFrame);
        AddElement(phaseFrame);
        phaseFrame.SendToBack();
    }

    public BossGraphNodeView FindNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        for (int i = 0; i < nodeViews.Count; i++)
        {
            if (nodeViews[i].NodeId == nodeId)
            {
                return nodeViews[i];
            }
        }

        return null;
    }

    public List<int> GetSelectedNodeIndexes()
    {
        List<int> indexes = new();
        foreach (ISelectable selectable in selection)
        {
            if (selectable is BossGraphNodeView nodeView)
            {
                indexes.Add(nodeView.NodeIndex);
            }
        }

        return indexes;
    }

    public List<Edge> GetSelectedEdges()
    {
        return selection.OfType<Edge>().ToList();
    }

    public void DeleteEdges(IReadOnlyList<Edge> selectedEdges)
    {
        if (selectedEdges == null || selectedEdges.Count == 0)
        {
            return;
        }

        DeleteElements(selectedEdges);
    }

    public List<BossGraphNodeView> GetSelectedNodeViews()
    {
        return selection
            .OfType<BossGraphNodeView>()
            .OrderBy(nodeView => nodeView.NodeIndex)
            .ToList();
    }

    public List<string> GetNodeIdsForDrag(BossGraphNodeView draggedNode)
    {
        if (draggedNode == null)
        {
            return new List<string>();
        }

        List<BossGraphNodeView> selectedNodes = selection
            .OfType<BossGraphNodeView>()
            .OrderBy(nodeView => nodeView.NodeIndex)
            .ToList();
        if (!selectedNodes.Contains(draggedNode))
        {
            return new List<string> { draggedNode.NodeId };
        }

        return selectedNodes
            .Select(nodeView => nodeView.NodeId)
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .ToList();
    }

    public void SelectNode(string nodeId)
    {
        BossGraphNodeView nodeView = FindNode(nodeId);
        if (nodeView == null)
        {
            return;
        }

        ClearSelection();
        AddToSelection(nodeView);
        nodeView.BringToFront();
    }

    public void SetRuntimeHighlight(string nodeId, string edgeFromNodeId, string edgeToNodeId)
    {
        string edgeKey = !string.IsNullOrWhiteSpace(edgeFromNodeId) && !string.IsNullOrWhiteSpace(edgeToNodeId)
            ? $"{edgeFromNodeId}->{edgeToNodeId}"
            : null;
        if (runtimeHighlightedNodeId == nodeId && runtimeHighlightedEdgeKey == edgeKey)
        {
            return;
        }

        runtimeHighlightedNodeId = nodeId;
        runtimeHighlightedEdgeKey = edgeKey;

        for (int i = 0; i < nodeViews.Count; i++)
        {
            BossGraphNodeView nodeView = nodeViews[i];
            nodeView.SetRuntimeActive(nodeView.NodeId == nodeId);
        }

        foreach (Edge edge in EdgeViews)
        {
            if (edge is not BossGraphEdgeView edgeView)
            {
                continue;
            }

            edgeView.SetRuntimeActive(IsRuntimeEdge(edgeView, edgeFromNodeId, edgeToNodeId));
        }
    }

    public void ClearRuntimeHighlight()
    {
        SetRuntimeHighlight(null, null, null);
    }

    private static bool IsRuntimeEdge(Edge edge, string fromNodeId, string toNodeId)
    {
        if (string.IsNullOrWhiteSpace(fromNodeId)
            || string.IsNullOrWhiteSpace(toNodeId)
            || edge?.output?.node is not BossGraphNodeView fromNode
            || edge.input?.node is not BossGraphNodeView toNode)
        {
            return false;
        }

        return fromNode.NodeId == fromNodeId && toNode.NodeId == toNodeId;
    }

    public ISelectable GetPrimarySelection()
    {
        return selection.Count > 0 ? selection[0] : null;
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        List<Port> compatiblePorts = new();
        List<Port> allPorts = ports.ToList();
        for (int i = 0; i < allPorts.Count; i++)
        {
            Port port = allPorts[i];
            if (port == startPort || port.node == startPort.node || port.direction == startPort.direction)
            {
                continue;
            }

            compatiblePorts.Add(port);
        }

        return compatiblePorts;
    }
}

internal sealed class BossGraphFrameView : GraphElement
{
    private const float DotSize = 6f;
    private readonly Color color;
    private readonly bool dottedBorder;
    private readonly IMGUIContainer border;

    public BossGraphFrameView(string label, Color color, Rect position, bool dottedBorder)
    {
        this.color = color;
        this.dottedBorder = dottedBorder;
        pickingMode = PickingMode.Ignore;
        capabilities = 0;

        style.position = Position.Absolute;
        style.backgroundColor = new Color(color.r, color.g, color.b, 0.035f);

        border = new IMGUIContainer(DrawBorder)
        {
            pickingMode = PickingMode.Ignore
        };
        border.style.position = Position.Absolute;
        border.style.left = 0f;
        border.style.top = 0f;
        border.style.right = 0f;
        border.style.bottom = 0f;
        Add(border);

        Label title = new(label)
        {
            pickingMode = PickingMode.Ignore
        };
        title.style.position = Position.Absolute;
        title.style.left = 10f;
        title.style.top = 6f;
        title.style.fontSize = 12f;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color = color;
        title.style.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.78f);
        title.style.paddingLeft = 5f;
        title.style.paddingRight = 5f;
        title.style.paddingTop = 2f;
        title.style.paddingBottom = 2f;
        Add(title);

        SetPosition(position);
    }

    public override void SetPosition(Rect newPos)
    {
        base.SetPosition(newPos);
        style.left = newPos.x;
        style.top = newPos.y;
        style.width = newPos.width;
        style.height = newPos.height;
        border?.MarkDirtyRepaint();
    }

    private void DrawBorder()
    {
        Rect rect = border.contentRect;
        if (rect.width <= 1f || rect.height <= 1f)
        {
            return;
        }

        rect.xMin += 1f;
        rect.yMin += 1f;
        rect.xMax -= 1f;
        rect.yMax -= 1f;

        Handles.BeginGUI();
        Color previousColor = Handles.color;
        Handles.color = color;
        if (dottedBorder)
        {
            Handles.DrawDottedLine(new Vector3(rect.xMin, rect.yMin), new Vector3(rect.xMax, rect.yMin), DotSize);
            Handles.DrawDottedLine(new Vector3(rect.xMax, rect.yMin), new Vector3(rect.xMax, rect.yMax), DotSize);
            Handles.DrawDottedLine(new Vector3(rect.xMax, rect.yMax), new Vector3(rect.xMin, rect.yMax), DotSize);
            Handles.DrawDottedLine(new Vector3(rect.xMin, rect.yMax), new Vector3(rect.xMin, rect.yMin), DotSize);
        }
        else
        {
            Handles.DrawLine(new Vector3(rect.xMin, rect.yMin), new Vector3(rect.xMax, rect.yMin));
            Handles.DrawLine(new Vector3(rect.xMax, rect.yMin), new Vector3(rect.xMax, rect.yMax));
            Handles.DrawLine(new Vector3(rect.xMax, rect.yMax), new Vector3(rect.xMin, rect.yMax));
            Handles.DrawLine(new Vector3(rect.xMin, rect.yMax), new Vector3(rect.xMin, rect.yMin));
        }

        Handles.color = previousColor;
        Handles.EndGUI();
    }
}

internal sealed class BossGraphEdgeView : Edge
{
    private static readonly Color RuntimeEdgeColor = new(1f, 0.78f, 0.08f, 1f);
    private static readonly Color DefaultEdgeColor = new(0.68f, 0.68f, 0.68f, 1f);
    private bool runtimeActive;

    public BossGraphEdgeView(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        Label conditionLabel = new(string.IsNullOrWhiteSpace(label) ? "Transition" : label)
        {
            pickingMode = PickingMode.Ignore
        };
        conditionLabel.style.position = Position.Absolute;
        conditionLabel.style.left = 12f;
        conditionLabel.style.top = -10f;
        conditionLabel.style.fontSize = 10f;
        conditionLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        conditionLabel.style.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.72f);
        conditionLabel.style.color = Color.white;
        conditionLabel.style.paddingLeft = 4f;
        conditionLabel.style.paddingRight = 4f;
        Add(conditionLabel);
    }

    public void SetRuntimeActive(bool active)
    {
        if (runtimeActive == active)
        {
            return;
        }

        runtimeActive = active;
        if (edgeControl == null)
        {
            return;
        }

        edgeControl.inputColor = active ? RuntimeEdgeColor : DefaultEdgeColor;
        edgeControl.outputColor = active ? RuntimeEdgeColor : DefaultEdgeColor;
        edgeControl.edgeWidth = active ? 5 : 2;
        edgeControl.MarkDirtyRepaint();
    }
}

internal sealed class BossGraphNodeView : Node
{
    private const float DragStartDistance = 5f;
    private static readonly Color RuntimeNodeOutlineColor = new(1f, 0.78f, 0.08f, 1f);

    private readonly VisualElement dragHandle;
    private Func<IReadOnlyList<string>> dragNodeIdsProvider;
    private Vector2 dragStartMousePosition;
    private bool dragCandidate;
    private bool runtimeActive;

    public BossGraphNodeView(int nodeIndex, string nodeId)
    {
        NodeIndex = nodeIndex;
        NodeId = nodeId;

        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        InputPort.portName = "In";
        inputContainer.Add(InputPort);

        OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
        OutputPort.portName = "Out";
        outputContainer.Add(OutputPort);

        dragHandle = new VisualElement
        {
            tooltip = "패턴에 드래그"
        };
        dragHandle.style.height = 12f;
        dragHandle.style.marginTop = 4f;
        dragHandle.style.backgroundColor = new Color(1f, 1f, 1f, 0.04f);
        dragHandle.RegisterCallback<MouseDownEvent>(OnDragMouseDown);
        dragHandle.RegisterCallback<MouseMoveEvent>(OnDragMouseMove);
        dragHandle.RegisterCallback<MouseUpEvent>(OnDragMouseUp);
        dragHandle.RegisterCallback<MouseLeaveEvent>(OnDragMouseLeave);
        extensionContainer.Add(dragHandle);

        RefreshExpandedState();
        RefreshPorts();
    }

    public int NodeIndex { get; }
    public string NodeId { get; }
    public Port InputPort { get; }
    public Port OutputPort { get; }

    public void SetDragNodeIdsProvider(Func<IReadOnlyList<string>> provider)
    {
        dragNodeIdsProvider = provider;
    }

    public void SetNodeKind(BossGraphNodeKind nodeKind, Color color)
    {
        titleContainer.style.backgroundColor = color;
        mainContainer.style.borderTopColor = color;
        mainContainer.style.borderBottomColor = color;
        mainContainer.style.borderLeftColor = color;
        mainContainer.style.borderRightColor = color;
    }

    public void SetRuntimeActive(bool active)
    {
        if (runtimeActive == active)
        {
            return;
        }

        runtimeActive = active;
        float width = active ? 3f : 0f;
        style.borderTopWidth = width;
        style.borderBottomWidth = width;
        style.borderLeftWidth = width;
        style.borderRightWidth = width;
        style.borderTopColor = RuntimeNodeOutlineColor;
        style.borderBottomColor = RuntimeNodeOutlineColor;
        style.borderLeftColor = RuntimeNodeOutlineColor;
        style.borderRightColor = RuntimeNodeOutlineColor;
    }

    private void OnDragMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 0)
        {
            return;
        }

        dragCandidate = true;
        dragStartMousePosition = evt.mousePosition;
    }

    private void OnDragMouseMove(MouseMoveEvent evt)
    {
        if (!dragCandidate || (evt.pressedButtons & 1) == 0)
        {
            return;
        }

        if ((evt.mousePosition - dragStartMousePosition).sqrMagnitude < DragStartDistance * DragStartDistance)
        {
            return;
        }

        IReadOnlyList<string> nodeIds = dragNodeIdsProvider?.Invoke();
        if (nodeIds == null || nodeIds.Count == 0)
        {
            return;
        }

        DragAndDrop.PrepareStartDrag();
        DragAndDrop.SetGenericData(BossGraphDragKeys.NodeIds, nodeIds.ToArray());
        DragAndDrop.objectReferences = Array.Empty<UnityEngine.Object>();
        DragAndDrop.StartDrag(nodeIds.Count == 1 ? nodeIds[0] : $"{nodeIds.Count} Boss Graph Nodes");
        dragCandidate = false;
        evt.StopPropagation();
    }

    private void OnDragMouseUp(MouseUpEvent evt)
    {
        dragCandidate = false;
    }

    private void OnDragMouseLeave(MouseLeaveEvent evt)
    {
        dragCandidate = false;
    }
}
