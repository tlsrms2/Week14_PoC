using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Week14.Enemy;

[CustomEditor(typeof(BossGraphAsset))]
public sealed class BossGraphAssetEditor : Editor
{
    private SerializedProperty references;
    private SerializedProperty stateNodes;
    private SerializedProperty patterns;
    private SerializedProperty phases;
    private SerializedProperty transitions;
    private ReorderableList stateNodeList;
    private ReorderableList patternList;
    private ReorderableList phaseList;
    private ReorderableList transitionList;

    private void OnEnable()
    {
        references = serializedObject.FindProperty("references");
        stateNodes = serializedObject.FindProperty("stateNodes");
        patterns = serializedObject.FindProperty("patterns");
        phases = serializedObject.FindProperty("phases");
        transitions = serializedObject.FindProperty("transitions");

        stateNodeList = new ReorderableList(serializedObject, stateNodes, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Action Nodes"),
            elementHeightCallback = GetStateNodeHeight,
            drawElementCallback = DrawStateNode,
            onAddCallback = AddStateNode
        };

        patternList = new ReorderableList(serializedObject, patterns, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Patterns"),
            elementHeightCallback = index => EditorGUI.GetPropertyHeight(patterns.GetArrayElementAtIndex(index), true)
                + EditorGUIUtility.standardVerticalSpacing,
            drawElementCallback = DrawPattern,
            onAddCallback = AddPattern
        };

        phaseList = new ReorderableList(serializedObject, phases, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Phases"),
            elementHeightCallback = index => EditorGUI.GetPropertyHeight(phases.GetArrayElementAtIndex(index), true)
                + EditorGUIUtility.standardVerticalSpacing,
            drawElementCallback = DrawPhase,
            onAddCallback = AddPhase
        };

        transitionList = new ReorderableList(serializedObject, transitions, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Transitions"),
            elementHeightCallback = GetTransitionHeight,
            drawElementCallback = DrawTransition
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawScriptField();
        if (GUILayout.Button("Boss Graph Editor 열기"))
        {
            BossGraphEditorWindow.Open((BossGraphAsset)target);
        }

        EditorGUILayout.Space(6f);
        DrawReferencesSection();
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("검증", EditorStyles.boldLabel);
        BossGraphValidationUtility.DrawMessages(BossGraphValidationUtility.Validate(serializedObject), 6);

        EditorGUILayout.Space(6f);
        stateNodeList.DoLayoutList();
        EditorGUILayout.Space(6f);
        patternList.DoLayoutList();
        EditorGUILayout.Space(6f);
        phaseList.DoLayoutList();
        EditorGUILayout.Space(6f);
        transitionList.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawReferencesSection()
    {
        if (references == null)
        {
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("참조", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(references, true);
        EditorGUILayout.EndVertical();
    }

    private float GetStateNodeHeight(int index)
    {
        SerializedProperty element = stateNodes.GetArrayElementAtIndex(index);
        return EditorGUI.GetPropertyHeight(element, true) + EditorGUIUtility.standardVerticalSpacing;
    }

    private void DrawStateNode(Rect rect, int index, bool isActive, bool isFocused)
    {
        SerializedProperty element = stateNodes.GetArrayElementAtIndex(index);
        rect.y += 1f;
        rect.height = EditorGUI.GetPropertyHeight(element, true);
        EditorGUI.PropertyField(rect, element, new GUIContent(GetStateNodeLabel(element, index)), true);
    }

    private void AddStateNode(ReorderableList list)
    {
        stateNodes.arraySize++;
        int index = stateNodes.arraySize - 1;
        SerializedProperty element = stateNodes.GetArrayElementAtIndex(index);
        SetChildString(element, "nodeId", $"Node{index + 1}");
        SetChildEnum(element, "nodeKind", (int)BossGraphNodeKind.Attack);
        SetChildInt(element, "phaseIndex", 0);
        SetChildEnum(element, "selectionMode", 0);
        SetChildVector2(element, "editorPosition", new Vector2(80f + index * 260f, 120f));
        SerializedProperty sequences = element.FindPropertyRelative("sequences");
        if (sequences != null)
        {
            sequences.ClearArray();
        }

        list.index = index;
    }

    private void DrawPattern(Rect rect, int index, bool isActive, bool isFocused)
    {
        SerializedProperty element = patterns.GetArrayElementAtIndex(index);
        rect.y += 1f;
        rect.height = EditorGUI.GetPropertyHeight(element, true);
        EditorGUI.PropertyField(rect, element, new GUIContent($"Pattern {index + 1}"), true);
    }

    private void AddPattern(ReorderableList list)
    {
        patterns.arraySize++;
        int index = patterns.arraySize - 1;
        SerializedProperty element = patterns.GetArrayElementAtIndex(index);
        SetChildString(element, "patternId", $"Pattern{index + 1}");
        SerializedProperty nodeIds = element.FindPropertyRelative("nodeIds");
        nodeIds?.ClearArray();
        list.index = index;
    }

    private void DrawPhase(Rect rect, int index, bool isActive, bool isFocused)
    {
        SerializedProperty element = phases.GetArrayElementAtIndex(index);
        rect.y += 1f;
        rect.height = EditorGUI.GetPropertyHeight(element, true);
        EditorGUI.PropertyField(rect, element, new GUIContent($"Phase {index + 1}"), true);
    }

    private void AddPhase(ReorderableList list)
    {
        phases.arraySize++;
        int index = phases.arraySize - 1;
        SerializedProperty element = phases.GetArrayElementAtIndex(index);
        SetChildInt(element, "phaseIndex", index);
        SetChildEnum(element, "selectionMode", (int)BossSequenceSelectionMode.WeightedRandom);
        SerializedProperty patternEntries = element.FindPropertyRelative("patterns");
        patternEntries?.ClearArray();
        list.index = index;
    }

    private float GetTransitionHeight(int index)
    {
        SerializedProperty element = transitions.GetArrayElementAtIndex(index);
        return EditorGUI.GetPropertyHeight(element, true) + EditorGUIUtility.standardVerticalSpacing;
    }

    private void DrawTransition(Rect rect, int index, bool isActive, bool isFocused)
    {
        SerializedProperty element = transitions.GetArrayElementAtIndex(index);
        rect.y += 1f;
        rect.height = EditorGUI.GetPropertyHeight(element, true);
        EditorGUI.PropertyField(rect, element, new GUIContent($"Transition {index + 1}"), true);
    }

    private void DrawScriptField()
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        }
    }

    private static string GetStateNodeLabel(SerializedProperty element, int index)
    {
        SerializedProperty nodeId = element.FindPropertyRelative("nodeId");
        SerializedProperty phaseIndex = element.FindPropertyRelative("phaseIndex");
        string id = nodeId != null && !string.IsNullOrWhiteSpace(nodeId.stringValue)
            ? nodeId.stringValue
            : $"Node {index + 1}";
        int phase = phaseIndex != null ? phaseIndex.intValue + 1 : index + 1;
        return $"{id} (Legacy Phase {phase})";
    }

    private static void SetChildString(SerializedProperty root, string childName, string value)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child != null)
        {
            child.stringValue = value;
        }
    }

    private static void SetChildInt(SerializedProperty root, string childName, int value)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child != null)
        {
            child.intValue = value;
        }
    }

    private static void SetChildFloat(SerializedProperty root, string childName, float value)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child != null)
        {
            child.floatValue = value;
        }
    }

    private static void SetChildVector2(SerializedProperty root, string childName, Vector2 value)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child != null)
        {
            child.vector2Value = value;
        }
    }

    private static void SetChildEnum(SerializedProperty root, string childName, int enumValueIndex)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child != null)
        {
            child.enumValueIndex = enumValueIndex;
        }
    }
}
