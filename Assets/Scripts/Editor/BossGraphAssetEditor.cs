using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Week14.Enemy;

[CustomEditor(typeof(BossGraphAsset))]
public sealed class BossGraphAssetEditor : Editor
{
    private SerializedProperty startNodeId;
    private SerializedProperty references;
    private SerializedProperty stateNodes;
    private SerializedProperty transitions;
    private ReorderableList stateNodeList;
    private ReorderableList transitionList;

    private void OnEnable()
    {
        startNodeId = serializedObject.FindProperty("startNodeId");
        references = serializedObject.FindProperty("references");
        stateNodes = serializedObject.FindProperty("stateNodes");
        transitions = serializedObject.FindProperty("transitions");

        stateNodeList = new ReorderableList(serializedObject, stateNodes, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "State Nodes"),
            elementHeightCallback = GetStateNodeHeight,
            drawElementCallback = DrawStateNode,
            onAddCallback = AddStateNode
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
        EditorGUILayout.PropertyField(startNodeId);
        EditorGUILayout.Space(6f);
        stateNodeList.DoLayoutList();
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
        SetChildString(element, "nodeId", $"Phase{index + 1}");
        SetChildInt(element, "phaseIndex", index);
        SetChildEnum(element, "selectionMode", 0);
        SetChildFloat(element, "minRecoverySeconds", 0.5f);
        SetChildFloat(element, "maxRecoverySeconds", 0.9f);
        SerializedProperty sequences = element.FindPropertyRelative("sequences");
        if (sequences != null)
        {
            sequences.ClearArray();
        }

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
        return $"{id} (Phase {phase})";
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

    private static void SetChildEnum(SerializedProperty root, string childName, int enumValueIndex)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child != null)
        {
            child.enumValueIndex = enumValueIndex;
        }
    }
}
