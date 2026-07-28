using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Week14.Combat;
using Week14.Enemy;

[CustomEditor(typeof(HackerExecutionSequence))]
public sealed class HackerExecutionSequenceEditor : Editor
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
        HackerBossAI source =
            sourceProperty?.objectReferenceValue as HackerBossAI;
        source ??= Object.FindFirstObjectByType<HackerBossAI>(
            FindObjectsInactive.Include);
        BossGraphAsset graph = source != null
            ? source.ConfiguredGraphAsset
            : null;
        if (graph == null)
        {
            EditorGUILayout.PropertyField(
                patternProperty,
                new GUIContent("Execution Pattern ID"));
            EditorGUILayout.HelpBox(
                "Hacker Boss와 Boss Graph를 먼저 연결하세요.",
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
                "Hacker Boss Graph에서 실행할 처형 패턴입니다."),
            Mathf.Max(0, currentIndex),
            options.ToArray());
        patternProperty.stringValue = nextIndex > 0
            ? patternIds[nextIndex - 1]
            : string.Empty;

        if (sourceProperty != null
            && sourceProperty.objectReferenceValue == null
            && GUILayout.Button("씬의 Hacker 보스 연결"))
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

        int scatterCount = 0;
        int throwCount = 0;
        int sniperCount = 0;
        int orbitCount = 0;
        int slamCount = 0;
        int barrageCount = 0;
        IReadOnlyList<string> nodeKeys = pattern.NodeKeys;
        for (int i = 0; i < nodeKeys.Count; i++)
        {
            BossAction action = graph.GetNode(nodeKeys[i])?.Action;
            switch (action)
            {
                case HackerScatterWireNodeAction:
                    scatterCount++;
                    break;
                case HackerWeaponThrowAction:
                    throwCount++;
                    break;
                case HackerSnipingFireAction:
                    sniperCount++;
                    break;
                case HackerWeaponWireOrbitAction:
                    orbitCount++;
                    break;
                case HackerMeleeAttackAction:
                    slamCount++;
                    break;
                case HackerSequentialSweepFireAction:
                    barrageCount++;
                    break;
            }
        }

        bool valid = scatterCount == 1
            && throwCount == 1
            && sniperCount == 2
            && orbitCount == 1
            && slamCount == 1
            && barrageCount == 1;
        EditorGUILayout.HelpBox(
            $"패턴 구성: 와이어 {scatterCount} / 무기 투척 {throwCount} / " +
            $"저격 {sniperCount} / 무기 회전 {orbitCount} / " +
            $"Slam {slamCount} / 방사 탄막 {barrageCount}",
            valid ? MessageType.Info : MessageType.Warning);
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
