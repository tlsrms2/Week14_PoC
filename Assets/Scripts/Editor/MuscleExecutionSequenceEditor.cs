using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Week14.Combat;
using Week14.Enemy;

[CustomEditor(typeof(MuscleExecutionSequence))]
public sealed class MuscleExecutionSequenceEditor : Editor
{
    private const string PatternSourcePropertyName = "patternSourceBoss";
    private const string PatternIdPropertyName = "executionPatternId";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(iterator);
                }

                continue;
            }

            if (iterator.propertyPath == PatternIdPropertyName)
            {
                DrawPatternPopup(iterator);
                continue;
            }

            EditorGUILayout.PropertyField(iterator, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPatternPopup(SerializedProperty patternIdProperty)
    {
        SerializedProperty sourceProperty =
            serializedObject.FindProperty(PatternSourcePropertyName);
        MuscleBossAI sourceBoss =
            sourceProperty?.objectReferenceValue as MuscleBossAI;
        sourceBoss ??=
            Object.FindFirstObjectByType<MuscleBossAI>(FindObjectsInactive.Include);

        if (sourceBoss == null || sourceBoss.ConfiguredGraphAsset == null)
        {
            EditorGUILayout.PropertyField(
                patternIdProperty,
                new GUIContent("Execution Pattern ID"));
            EditorGUILayout.HelpBox(
                "Muscle Boss AI와 Boss Graph를 먼저 연결하세요.",
                MessageType.Warning);
            return;
        }

        BossGraphAsset graph = sourceBoss.ConfiguredGraphAsset;
        List<string> patternIds = CollectRunnablePatternIds(graph);
        List<string> options = new() { "<선택 안 함>" };
        options.AddRange(patternIds);

        string currentId = patternIdProperty.stringValue;
        int currentIndex = string.IsNullOrWhiteSpace(currentId)
            ? 0
            : patternIds.IndexOf(currentId) + 1;
        bool missingCurrentPattern =
            !string.IsNullOrWhiteSpace(currentId) && currentIndex <= 0;
        if (missingCurrentPattern)
        {
            options.Add($"<찾을 수 없음> {currentId}");
            currentIndex = options.Count - 1;
        }

        int nextIndex = EditorGUILayout.Popup(
            new GUIContent(
                "Execution Pattern",
                "Muscle Boss Graph에서 마무리 처형에 실행할 패턴입니다."),
            currentIndex,
            options.ToArray());
        if (nextIndex == 0)
        {
            patternIdProperty.stringValue = string.Empty;
        }
        else if (nextIndex <= patternIds.Count)
        {
            patternIdProperty.stringValue = patternIds[nextIndex - 1];
        }

        if (sourceProperty != null && sourceProperty.objectReferenceValue == null)
        {
            if (GUILayout.Button("씬의 Muscle 보스 연결"))
            {
                sourceProperty.objectReferenceValue = sourceBoss;
            }
        }

        BossGraphPattern selectedPattern =
            graph.GetPattern(patternIdProperty.stringValue);
        if (selectedPattern != null)
        {
            DrawExecutionPatternValidation(graph, selectedPattern);
        }
        else if (missingCurrentPattern)
        {
            EditorGUILayout.HelpBox(
                $"Pattern ID '{currentId}'를 Boss Graph에서 찾을 수 없습니다.",
                MessageType.Error);
        }
    }

    private static void DrawExecutionPatternValidation(
        BossGraphAsset graph,
        BossGraphPattern pattern)
    {
        int dashCount = 0;
        int formationCount = 0;
        IReadOnlyList<string> nodeKeys = pattern.NodeKeys;
        for (int i = 0; i < nodeKeys.Count; i++)
        {
            BossAction action = graph.GetNode(nodeKeys[i])?.Action;
            if (action is BossDashAction)
            {
                dashCount++;
            }
            else if (action is FireDashFormationAction)
            {
                formationCount++;
            }
        }

        MessageType messageType =
            dashCount == 3 && formationCount == 1
                ? MessageType.Info
                : MessageType.Warning;
        EditorGUILayout.HelpBox(
            $"선택 패턴 구성: Boss Dash {dashCount}개 / " +
            $"Fire Dash Formation {formationCount}개\n" +
            "권장 구성은 Dash 3개와 Formation 1개입니다.",
            messageType);
    }

    private static List<string> CollectRunnablePatternIds(BossGraphAsset graph)
    {
        List<string> ids = new();
        IReadOnlyList<BossGraphPattern> patterns = graph.Patterns;
        for (int i = 0; i < patterns.Count; i++)
        {
            BossGraphPattern pattern = patterns[i];
            if (pattern != null
                && !string.IsNullOrWhiteSpace(pattern.PatternId)
                && pattern.NodeKeys != null
                && pattern.NodeKeys.Count > 0)
            {
                ids.Add(pattern.PatternId);
            }
        }

        return ids;
    }
}
