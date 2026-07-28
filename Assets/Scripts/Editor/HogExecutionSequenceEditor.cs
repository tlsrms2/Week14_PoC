using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Week14.Combat;
using Week14.Enemy;

[CustomEditor(typeof(HogExecutionSequence))]
public sealed class HogExecutionSequenceEditor : Editor
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
        HogBossAI sourceBoss = sourceProperty?.objectReferenceValue as HogBossAI;
        sourceBoss ??= Object.FindFirstObjectByType<HogBossAI>(FindObjectsInactive.Include);

        if (sourceBoss == null)
        {
            EditorGUILayout.PropertyField(
                patternIdProperty,
                new GUIContent("Execution Pattern ID"));
            EditorGUILayout.HelpBox(
                "Pattern Source Boss를 지정해야 Boss Graph 패턴 목록을 표시할 수 있습니다.",
                MessageType.Warning);
            return;
        }

        BossGraphAsset graph = sourceBoss.ConfiguredGraphAsset;
        if (graph == null)
        {
            EditorGUILayout.PropertyField(
                patternIdProperty,
                new GUIContent("Execution Pattern ID"));
            EditorGUILayout.HelpBox(
                "Hog 보스의 Boss Graph가 비어 있습니다. 먼저 Hog Boss AI에서 그래프를 지정하세요.",
                MessageType.Warning);
            return;
        }

        List<string> patternIds = CollectRunnablePatternIds(graph);
        List<string> options = new() { "<선택 안 함>" };
        options.AddRange(patternIds);

        string currentId = patternIdProperty.stringValue;
        int currentIndex = string.IsNullOrWhiteSpace(currentId)
            ? 0
            : patternIds.IndexOf(currentId) + 1;
        bool missingCurrentPattern = !string.IsNullOrWhiteSpace(currentId)
            && currentIndex <= 0;
        if (missingCurrentPattern)
        {
            options.Add($"<찾을 수 없음> {currentId}");
            currentIndex = options.Count - 1;
        }

        int nextIndex = EditorGUILayout.Popup(
            new GUIContent(
                "Execution Pattern",
                "Hog Boss Graph 에디터에서 만든 패턴 중 처형 연출에 사용할 패턴입니다."),
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
            EditorGUILayout.HelpBox(
                $"씬에서 '{sourceBoss.name}'을 찾았습니다. 아래 버튼으로 참조를 저장하세요.",
                MessageType.Info);
            if (GUILayout.Button("발견한 Hog 보스 연결"))
            {
                sourceProperty.objectReferenceValue = sourceBoss;
            }
        }

        if (patternIds.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Boss Graph에 실행 가능한 노드가 포함된 패턴이 없습니다.",
                MessageType.Warning);
        }
        else if (missingCurrentPattern)
        {
            EditorGUILayout.HelpBox(
                $"Pattern ID '{currentId}'을 현재 Boss Graph에서 찾을 수 없습니다.",
                MessageType.Error);
        }
    }

    private static List<string> CollectRunnablePatternIds(BossGraphAsset graph)
    {
        List<string> ids = new();
        IReadOnlyList<BossGraphPattern> patterns = graph.Patterns;
        for (int i = 0; i < patterns.Count; i++)
        {
            BossGraphPattern pattern = patterns[i];
            if (pattern == null
                || string.IsNullOrWhiteSpace(pattern.PatternId)
                || pattern.NodeKeys == null
                || pattern.NodeKeys.Count == 0)
            {
                continue;
            }

            ids.Add(pattern.PatternId);
        }

        return ids;
    }
}
