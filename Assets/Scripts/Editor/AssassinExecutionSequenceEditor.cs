using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Week14.Combat;
using Week14.Enemy;

[CustomEditor(typeof(AssassinExecutionSequence))]
public sealed class AssassinExecutionSequenceEditor : Editor
{
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
            }
            else if (iterator.propertyPath == "executionPatternId")
            {
                DrawPatternPopup(iterator);
            }
            else
            {
                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPatternPopup(SerializedProperty patternProperty)
    {
        SerializedProperty sourceProperty =
            serializedObject.FindProperty("patternSourceBoss");
        AssassinBossAI source =
            sourceProperty?.objectReferenceValue as AssassinBossAI;
        source ??= Object.FindFirstObjectByType<AssassinBossAI>(
            FindObjectsInactive.Include);
        BossGraphAsset graph = source != null
            ? source.ConfiguredStealthGraphAsset
            : null;
        if (graph == null)
        {
            EditorGUILayout.PropertyField(
                patternProperty,
                new GUIContent("Execution Pattern ID"));
            EditorGUILayout.HelpBox(
                "Assassin Boss의 Stealth Graph를 먼저 연결하세요.",
                MessageType.Warning);
            return;
        }

        List<string> patternIds = CollectPatternIds(graph);
        List<string> options = new() { "<선택 안 함>" };
        options.AddRange(patternIds);
        int currentIndex = string.IsNullOrWhiteSpace(
            patternProperty.stringValue)
                ? 0
                : patternIds.IndexOf(patternProperty.stringValue) + 1;
        int nextIndex = EditorGUILayout.Popup(
            new GUIContent(
                "Execution Pattern",
                "Assassin Stealth Graph에서 실행할 처형 패턴입니다."),
            Mathf.Max(0, currentIndex),
            options.ToArray());
        patternProperty.stringValue = nextIndex > 0
            ? patternIds[nextIndex - 1]
            : string.Empty;

        if (sourceProperty != null
            && sourceProperty.objectReferenceValue == null
            && GUILayout.Button("씬의 Assassin 보스 연결"))
        {
            sourceProperty.objectReferenceValue = source;
        }

        DrawPatternValidation(
            graph,
            graph.GetPattern(patternProperty.stringValue));
    }

    private static void DrawPatternValidation(
        BossGraphAsset graph,
        BossGraphPattern pattern)
    {
        if (pattern == null)
        {
            return;
        }

        int radialCount = 0;
        int cloneSpawnCount = 0;
        int cloneFireCount = 0;
        IReadOnlyList<string> nodeKeys = pattern.NodeKeys;
        for (int i = 0; i < nodeKeys.Count; i++)
        {
            BossAction action = graph.GetNode(nodeKeys[i])?.Action;
            if (action is FireRadialEmissionAction)
            {
                radialCount++;
            }
            else if (action is AssassinSpawnCloneShootersAction)
            {
                cloneSpawnCount++;
            }
            else if (action is AssassinFireNextCloneShooterAtk2Action)
            {
                cloneFireCount++;
            }
        }

        MessageType type =
            radialCount == 2
            && cloneSpawnCount >= 1
            && cloneFireCount >= 1
                ? MessageType.Info
                : MessageType.Warning;
        EditorGUILayout.HelpBox(
            $"패턴 구성: 휩뿌리기 {radialCount}회 / " +
            $"분신 생성 {cloneSpawnCount}회 / " +
            $"분신 발사 {cloneFireCount}회",
            type);
    }

    private static List<string> CollectPatternIds(
        BossGraphAsset graph)
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
