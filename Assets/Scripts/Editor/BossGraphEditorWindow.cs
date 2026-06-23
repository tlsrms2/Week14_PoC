using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Week14.Enemy;

public sealed class BossGraphEditorWindow : EditorWindow
{
    private BossGraphAsset graphAsset;
    private SerializedObject graphObject;
    private BossGraphView graphView;
    private ObjectField graphField;
    private IMGUIContainer detailsPanel;
    private bool rebuildQueued;

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
        window.SetGraph(asset);
    }

    private void CreateGUI()
    {
        DrawToolbar();
        VisualElement content = new()
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                flexGrow = 1f
            }
        };

        graphView = new BossGraphView
        {
            name = "Boss Graph View"
        };
        graphView.style.flexGrow = 1f;
        graphView.RegisterCallback<MouseUpEvent>(_ => detailsPanel?.MarkDirtyRepaint());
        graphView.RegisterCallback<KeyUpEvent>(_ => detailsPanel?.MarkDirtyRepaint());
        content.Add(graphView);

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

    private void DrawToolbar()
    {
        Toolbar toolbar = new();

        graphField = new ObjectField("Graph")
        {
            objectType = typeof(BossGraphAsset),
            allowSceneObjects = false,
            value = graphAsset
        };
        graphField.RegisterValueChangedCallback(evt => SetGraph(evt.newValue as BossGraphAsset));
        toolbar.Add(graphField);

        Button openSelectedButton = new(OpenSelectedGraph)
        {
            text = "Open Selected"
        };
        toolbar.Add(openSelectedButton);

        Button addNodeButton = new(AddStateNode)
        {
            text = "Add Node"
        };
        toolbar.Add(addNodeButton);

        Button deleteNodeButton = new(DeleteSelectedNodes)
        {
            text = "Delete Selected"
        };
        toolbar.Add(deleteNodeButton);

        Button saveButton = new(SaveGraph)
        {
            text = "Save"
        };
        toolbar.Add(saveButton);

        rootVisualElement.Add(toolbar);
    }

    private void SetGraph(BossGraphAsset asset)
    {
        graphAsset = asset;
        graphObject = graphAsset != null ? new SerializedObject(graphAsset) : null;

        if (graphField != null && graphField.value != graphAsset)
        {
            graphField.SetValueWithoutNotify(graphAsset);
        }

        RebuildGraph();
        detailsPanel?.MarkDirtyRepaint();
    }

    private void OpenSelectedGraph()
    {
        if (Selection.activeObject is BossGraphAsset selectedGraph)
        {
            SetGraph(selectedGraph);
        }
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
    }

    private BossGraphNodeView CreateNodeView(SerializedProperty node, int index)
    {
        string nodeId = GetString(node, "nodeId", $"Node {index + 1}");
        int phaseIndex = GetInt(node, "phaseIndex", index);
        string selectionMode = GetEnumDisplayName(node, "selectionMode", "Sequential");
        int sequenceCount = node.FindPropertyRelative("sequences")?.arraySize ?? 0;
        float minRecoverySeconds = GetFloat(node, "minRecoverySeconds", 0f);
        float maxRecoverySeconds = GetFloat(node, "maxRecoverySeconds", minRecoverySeconds);
        Vector2 position = GetVector2(node, "editorPosition", new Vector2(80f + index * 260f, 120f));
        if (position == Vector2.zero)
        {
            position = new Vector2(80f + index * 260f, 120f);
        }

        BossGraphNodeView nodeView = new(index, nodeId)
        {
            title = $"{nodeId} (Phase {phaseIndex + 1})"
        };
        nodeView.SetPosition(new Rect(position, new Vector2(220f, 140f)));
        nodeView.SetSummary(selectionMode, sequenceCount, minRecoverySeconds, maxRecoverySeconds);
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

    private void AddStateNode()
    {
        if (graphObject == null)
        {
            return;
        }

        SaveGraph();
        Undo.RecordObject(graphAsset, "Add Boss Graph Node");
        graphObject.Update();
        SerializedProperty stateNodes = graphObject.FindProperty("stateNodes");
        stateNodes.arraySize++;

        int index = stateNodes.arraySize - 1;
        SerializedProperty node = stateNodes.GetArrayElementAtIndex(index);
        SetString(node, "nodeId", $"Phase{index + 1}");
        SetInt(node, "phaseIndex", index);
        SetEnum(node, "selectionMode", 0);
        SetFloat(node, "minRecoverySeconds", 0.5f);
        SetFloat(node, "maxRecoverySeconds", 0.9f);
        SetVector2(node, "editorPosition", new Vector2(80f + index * 260f, 120f));

        SerializedProperty sequences = node.FindPropertyRelative("sequences");
        sequences?.ClearArray();

        graphObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(graphAsset);
        RebuildGraph();
        detailsPanel?.MarkDirtyRepaint();
    }

    private void DeleteSelectedNodes()
    {
        if (graphObject == null || graphView == null)
        {
            return;
        }

        List<int> indexes = graphView.GetSelectedNodeIndexes();
        if (indexes.Count == 0)
        {
            return;
        }

        indexes.Sort((a, b) => b.CompareTo(a));
        Undo.RecordObject(graphAsset, "Delete Boss Graph Node");
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
        graphObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(graphAsset);
        RebuildGraph();
        detailsPanel?.MarkDirtyRepaint();
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

    private void DrawDetailsPanel()
    {
        if (graphObject == null)
        {
            EditorGUILayout.HelpBox("BossGraphAsset을 선택하세요.", MessageType.Info);
            return;
        }

        graphObject.Update();
        EditorGUILayout.LabelField("Details", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);
        DrawGraphReferences();
        EditorGUILayout.Space(8f);
        DrawGraphValidation();
        EditorGUILayout.Space(8f);

        ISelectable selection = graphView?.GetPrimarySelection();
        if (selection is BossGraphNodeView nodeView)
        {
            DrawNodeDetails(nodeView);
        }
        else if (selection is Edge edge)
        {
            DrawTransitionDetails(edge);
        }
        else
        {
            EditorGUILayout.HelpBox("노드나 연결선을 선택하세요.", MessageType.Info);
        }
    }

    private void DrawGraphReferences()
    {
        SerializedProperty references = graphObject.FindProperty("references");
        if (references == null)
        {
            return;
        }

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(references, new GUIContent("참조"), true);
        if (!EditorGUI.EndChangeCheck())
        {
            return;
        }

        graphObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(graphAsset);
    }

    private void DrawGraphValidation()
    {
        EditorGUILayout.LabelField("검증", EditorStyles.boldLabel);
        BossGraphValidationUtility.DrawMessages(BossGraphValidationUtility.Validate(graphObject), 5);
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
        int oldPhaseIndex = GetInt(node, "phaseIndex", nodeView.NodeIndex);

        EditorGUI.BeginChangeCheck();
        DrawNodeCoreFields(node);
        bool sequenceStructureChanged = DrawSequenceList(node);
        bool nodeChanged = EditorGUI.EndChangeCheck();
        if (!nodeChanged && !sequenceStructureChanged)
        {
            return;
        }

        string newNodeId = GetString(node, "nodeId", string.Empty);
        int newPhaseIndex = GetInt(node, "phaseIndex", nodeView.NodeIndex);
        if (!string.IsNullOrWhiteSpace(oldNodeId) && !string.IsNullOrWhiteSpace(newNodeId) && oldNodeId != newNodeId)
        {
            UpdateTransitionNodeIds(oldNodeId, newNodeId);
        }

        graphObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(graphAsset);
        if (sequenceStructureChanged)
        {
            AssetDatabase.SaveAssets();
        }

        if (oldNodeId != newNodeId || oldPhaseIndex != newPhaseIndex)
        {
            ScheduleRebuildGraph();
        }
        else
        {
            nodeView.SetSummary(
                GetEnumDisplayName(node, "selectionMode", "Sequential"),
                node.FindPropertyRelative("sequences")?.arraySize ?? 0,
                GetFloat(node, "minRecoverySeconds", 0f),
                GetFloat(node, "maxRecoverySeconds", GetFloat(node, "minRecoverySeconds", 0f)));
            detailsPanel?.MarkDirtyRepaint();
        }
    }

    private static void DrawNodeCoreFields(SerializedProperty node)
    {
        EditorGUILayout.PropertyField(node.FindPropertyRelative("nodeId"));
        EditorGUILayout.PropertyField(node.FindPropertyRelative("phaseIndex"));
        EditorGUILayout.PropertyField(node.FindPropertyRelative("selectionMode"));
        EditorGUILayout.PropertyField(node.FindPropertyRelative("minRecoverySeconds"));
        EditorGUILayout.PropertyField(node.FindPropertyRelative("maxRecoverySeconds"));

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(node.FindPropertyRelative("editorPosition"));
        }
    }

    private bool DrawSequenceList(SerializedProperty node)
    {
        SerializedProperty sequences = node.FindPropertyRelative("sequences");
        if (sequences == null)
        {
            return false;
        }

        bool changed = false;
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Sequences", EditorStyles.boldLabel);

        for (int i = 0; i < sequences.arraySize; i++)
        {
            SerializedProperty entry = sequences.GetArrayElementAtIndex(i);
            SerializedProperty sequenceProperty = entry.FindPropertyRelative("sequence");
            SerializedProperty weightProperty = entry.FindPropertyRelative("weight");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField((i + 1).ToString(), GUILayout.Width(22f));
                EditorGUILayout.PropertyField(sequenceProperty, GUIContent.none);
                EditorGUILayout.LabelField("W", GUILayout.Width(14f));
                EditorGUILayout.PropertyField(weightProperty, GUIContent.none, GUILayout.Width(46f));

                using (new EditorGUI.DisabledScope(sequenceProperty?.objectReferenceValue == null))
                {
                    if (GUILayout.Button("Open", GUILayout.Width(46f)))
                    {
                        Selection.activeObject = sequenceProperty.objectReferenceValue;
                        EditorGUIUtility.PingObject(sequenceProperty.objectReferenceValue);
                    }
                }

                using (new EditorGUI.DisabledScope(i <= 0))
                {
                    if (GUILayout.Button("Up", GUILayout.Width(32f)))
                    {
                        Undo.RecordObject(graphAsset, "Move Boss Graph Sequence Entry");
                        sequences.MoveArrayElement(i, i - 1);
                        changed = true;
                        break;
                    }
                }

                using (new EditorGUI.DisabledScope(i >= sequences.arraySize - 1))
                {
                    if (GUILayout.Button("Dn", GUILayout.Width(32f)))
                    {
                        Undo.RecordObject(graphAsset, "Move Boss Graph Sequence Entry");
                        sequences.MoveArrayElement(i, i + 1);
                        changed = true;
                        break;
                    }
                }

                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    Undo.RecordObject(graphAsset, "Remove Boss Graph Sequence Entry");
                    sequences.DeleteArrayElementAtIndex(i);
                    changed = true;
                    break;
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("빈 슬롯 추가"))
            {
                Undo.RecordObject(graphAsset, "Add Boss Graph Sequence Entry");
                InsertSequenceEntry(sequences, null);
                changed = true;
            }

            if (GUILayout.Button("새 Sequence 생성 후 연결"))
            {
                changed |= CreateAndAppendSequence(node, sequences);
            }
        }

        return changed;
    }

    private bool CreateAndAppendSequence(SerializedProperty node, SerializedProperty sequences)
    {
        string nodeId = GetString(node, "nodeId", "Node");
        AttackSequenceAsset sequence = CreateSequenceAsset(graphAsset, nodeId, sequences.arraySize);
        if (sequence == null)
        {
            return false;
        }

        Undo.RecordObject(graphAsset, "Create Boss Graph Sequence Entry");
        InsertSequenceEntry(sequences, sequence);
        EditorGUIUtility.PingObject(sequence);
        return true;
    }

    private static void InsertSequenceEntry(SerializedProperty sequences, AttackSequenceAsset sequence)
    {
        int index = sequences.arraySize;
        sequences.InsertArrayElementAtIndex(index);

        SerializedProperty entry = sequences.GetArrayElementAtIndex(index);
        SerializedProperty sequenceProperty = entry.FindPropertyRelative("sequence");
        SerializedProperty weightProperty = entry.FindPropertyRelative("weight");
        if (sequenceProperty != null)
        {
            sequenceProperty.objectReferenceValue = sequence;
        }

        if (weightProperty != null)
        {
            weightProperty.intValue = 1;
        }
    }

    private void DrawTransitionDetails(Edge edge)
    {
        if (!TryGetEdgeNodeIds(edge, out string fromNodeId, out string toNodeId))
        {
            EditorGUILayout.HelpBox("선택한 연결선 데이터를 찾을 수 없습니다.", MessageType.Warning);
            return;
        }

        SerializedProperty transition = FindOrCreateTransition(fromNodeId, toNodeId);
        if (transition == null)
        {
            EditorGUILayout.HelpBox("Transition 데이터를 생성할 수 없습니다.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField($"{fromNodeId} -> {toNodeId}", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(transition.FindPropertyRelative("fromNodeId"));
            EditorGUILayout.PropertyField(transition.FindPropertyRelative("toNodeId"));
        }

        EditorGUI.BeginChangeCheck();
        SerializedProperty conditionType = transition.FindPropertyRelative("conditionType");
        EditorGUILayout.PropertyField(conditionType);
        DrawTransitionConditionFields(transition, conditionType);
        if (!EditorGUI.EndChangeCheck())
        {
            return;
        }

        graphObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(graphAsset);
        ScheduleRebuildGraph();
    }

    private static void DrawTransitionConditionFields(
        SerializedProperty transition,
        SerializedProperty conditionType)
    {
        BossTransitionConditionType type = conditionType != null
            ? (BossTransitionConditionType)conditionType.enumValueIndex
            : BossTransitionConditionType.SequenceEnded;

        switch (type)
        {
            case BossTransitionConditionType.HpRatioLessOrEqual:
                EditorGUILayout.PropertyField(transition.FindPropertyRelative("threshold"), new GUIContent("HP Ratio"));
                break;
            case BossTransitionConditionType.PhaseIndexEquals:
                EditorGUILayout.PropertyField(transition.FindPropertyRelative("phaseIndex"), new GUIContent("Phase Index"));
                break;
            case BossTransitionConditionType.EnragePhaseEquals:
                EditorGUILayout.PropertyField(transition.FindPropertyRelative("phaseIndex"), new GUIContent("Enrage Phase"));
                break;
            case BossTransitionConditionType.LivesLessOrEqual:
                EditorGUILayout.PropertyField(transition.FindPropertyRelative("phaseIndex"), new GUIContent("Lives"));
                break;
        }
    }

    private SerializedProperty FindOrCreateTransition(string fromNodeId, string toNodeId)
    {
        SerializedProperty transitions = graphObject.FindProperty("transitions");
        if (transitions == null)
        {
            return null;
        }

        SerializedProperty transition = FindTransition(transitions, fromNodeId, toNodeId);
        if (transition != null)
        {
            return transition;
        }

        int index = transitions.arraySize;
        transitions.InsertArrayElementAtIndex(index);
        transition = transitions.GetArrayElementAtIndex(index);
        SetString(transition, "fromNodeId", fromNodeId);
        SetString(transition, "toNodeId", toNodeId);
        ApplyTransitionValues(transition, TransitionValues.Default);
        graphObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(graphAsset);

        graphObject.Update();
        transitions = graphObject.FindProperty("transitions");
        return transitions != null ? transitions.GetArrayElementAtIndex(index) : null;
    }

    private static SerializedProperty FindTransition(SerializedProperty transitions, string fromNodeId, string toNodeId)
    {
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

    private void UpdateTransitionNodeIds(string oldNodeId, string newNodeId)
    {
        SerializedProperty transitions = graphObject.FindProperty("transitions");
        if (transitions == null)
        {
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
    }

    private static bool TryGetEdgeNodeIds(Edge edge, out string fromNodeId, out string toNodeId)
    {
        fromNodeId = string.Empty;
        toNodeId = string.Empty;

        if (edge?.output?.node is not BossGraphNodeView fromNode
            || edge.input?.node is not BossGraphNodeView toNode)
        {
            return false;
        }

        fromNodeId = fromNode.NodeId;
        toNodeId = toNode.NodeId;
        return !string.IsNullOrWhiteSpace(fromNodeId) && !string.IsNullOrWhiteSpace(toNodeId);
    }

    private void ScheduleRebuildGraph()
    {
        if (rebuildQueued)
        {
            return;
        }

        rebuildQueued = true;
        EditorApplication.delayCall += () =>
        {
            rebuildQueued = false;
            if (this == null)
            {
                return;
            }

            RebuildGraph();
            detailsPanel?.MarkDirtyRepaint();
        };
    }

    private void SaveTransitions()
    {
        SerializedProperty transitions = graphObject.FindProperty("transitions");
        if (transitions == null)
        {
            return;
        }

        Dictionary<string, TransitionValues> existingValues = ReadTransitionValues(transitions);
        List<Edge> edgeViews = graphView.EdgeViews;
        transitions.ClearArray();

        for (int i = 0; i < edgeViews.Count; i++)
        {
            Edge edge = edgeViews[i];
            if (edge.output?.node is not BossGraphNodeView fromNode
                || edge.input?.node is not BossGraphNodeView toNode
                || fromNode == toNode)
            {
                continue;
            }

            int index = transitions.arraySize;
            transitions.InsertArrayElementAtIndex(index);
            SerializedProperty transition = transitions.GetArrayElementAtIndex(index);
            SetString(transition, "fromNodeId", fromNode.NodeId);
            SetString(transition, "toNodeId", toNode.NodeId);

            string key = MakeTransitionKey(fromNode.NodeId, toNode.NodeId);
            TransitionValues values = existingValues.TryGetValue(key, out TransitionValues existing)
                ? existing
                : TransitionValues.Default;
            ApplyTransitionValues(transition, values);
        }
    }

    private Dictionary<string, TransitionValues> ReadTransitionValues(SerializedProperty transitions)
    {
        Dictionary<string, TransitionValues> values = new();
        for (int i = 0; i < transitions.arraySize; i++)
        {
            SerializedProperty transition = transitions.GetArrayElementAtIndex(i);
            string fromNodeId = GetString(transition, "fromNodeId", string.Empty);
            string toNodeId = GetString(transition, "toNodeId", string.Empty);
            if (string.IsNullOrWhiteSpace(fromNodeId) || string.IsNullOrWhiteSpace(toNodeId))
            {
                continue;
            }

            values[MakeTransitionKey(fromNodeId, toNodeId)] = new TransitionValues(
                GetEnum(transition, "conditionType", 0),
                GetFloat(transition, "threshold", 0f),
                GetInt(transition, "phaseIndex", 0));
        }

        return values;
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

    private static void ApplyTransitionValues(SerializedProperty transition, TransitionValues values)
    {
        SetEnum(transition, "conditionType", values.ConditionType);
        SetFloat(transition, "threshold", values.Threshold);
        SetInt(transition, "phaseIndex", values.PhaseIndex);
    }

    private static string MakeTransitionKey(string fromNodeId, string toNodeId)
    {
        return $"{fromNodeId}->{toNodeId}";
    }

    private static string GetTransitionLabel(SerializedProperty transition)
    {
        string conditionName = GetEnumDisplayName(transition, "conditionType", "Sequence Ended");
        int conditionType = GetEnum(transition, "conditionType", 0);
        return conditionType switch
        {
            1 => $"{conditionName} <= {GetFloat(transition, "threshold", 0f):0.##}",
            2 => $"{conditionName}: {GetInt(transition, "phaseIndex", 0)}",
            _ => conditionName
        };
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

    private static AttackSequenceAsset CreateSequenceAsset(BossGraphAsset graph, string nodeId, int sequenceIndex)
    {
        string path = GetSequenceAssetPath(graph, nodeId, sequenceIndex);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        AttackSequenceAsset sequence = ScriptableObject.CreateInstance<AttackSequenceAsset>();
        AssetDatabase.CreateAsset(sequence, path);
        Undo.RegisterCreatedObjectUndo(sequence, "Create Boss Graph Sequence Asset");
        AssetDatabase.SaveAssets();
        return sequence;
    }

    private static string GetSequenceAssetPath(BossGraphAsset graph, string nodeId, int sequenceIndex)
    {
        string graphName = SanitizeFileName(graph != null ? graph.name : "BossGraph");
        string nodeName = SanitizeFileName(nodeId);
        string fileName = $"{graphName}_{nodeName}_Sequence{sequenceIndex + 1}.asset";
        string graphPath = graph != null ? AssetDatabase.GetAssetPath(graph) : string.Empty;
        if (string.IsNullOrWhiteSpace(graphPath))
        {
            return EditorUtility.SaveFilePanelInProject(
                "Attack Sequence 생성",
                fileName,
                "asset",
                "AttackSequenceAsset 저장 위치를 선택하세요.");
        }

        string folder = Path.GetDirectoryName(graphPath);
        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = "Assets";
        }

        string path = Path.Combine(folder, fileName).Replace("\\", "/");
        return AssetDatabase.GenerateUniqueAssetPath(path);
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Node";
        }

        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string sanitized = new(value.Select(character => invalidCharacters.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Node" : sanitized;
    }

    private readonly struct TransitionValues
    {
        public static readonly TransitionValues Default = new(0, 0f, 0);

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
}

internal sealed class BossGraphView : GraphView
{
    private readonly List<BossGraphNodeView> nodeViews = new();

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

    public void ClearGraph()
    {
        DeleteElements(graphElements.ToList());
        nodeViews.Clear();
    }

    public void AddStateNode(BossGraphNodeView nodeView)
    {
        nodeViews.Add(nodeView);
        AddElement(nodeView);
    }

    public void AddTransitionEdge(BossGraphNodeView fromNode, BossGraphNodeView toNode, string label)
    {
        BossGraphEdgeView edge = new(label)
        {
            output = fromNode.OutputPort,
            input = toNode.InputPort
        };
        edge.output.Connect(edge);
        edge.input.Connect(edge);
        AddElement(edge);
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

internal sealed class BossGraphNodeView : Node
{
    private readonly Label summaryLabel;

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

        summaryLabel = new Label();
        summaryLabel.style.whiteSpace = WhiteSpace.Normal;
        summaryLabel.style.fontSize = 11f;
        summaryLabel.style.marginTop = 4f;
        extensionContainer.Add(summaryLabel);

        RefreshExpandedState();
        RefreshPorts();
    }

    public int NodeIndex { get; }
    public string NodeId { get; }
    public Port InputPort { get; }
    public Port OutputPort { get; }

    public void SetSummary(string selectionMode, int sequenceCount, float minRecoverySeconds, float maxRecoverySeconds)
    {
        summaryLabel.text = $"Sequences: {sequenceCount}\nSelect: {selectionMode}\nRecovery: {minRecoverySeconds:0.##}-{maxRecoverySeconds:0.##}s";
        RefreshExpandedState();
    }
}

internal sealed class BossGraphEdgeView : Edge
{
    public BossGraphEdgeView(string label)
    {
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
}
