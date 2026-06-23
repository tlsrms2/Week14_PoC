using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Week14.Enemy;

[CustomEditor(typeof(HogBossAI))]
[CanEditMultipleObjects]
public sealed class HogBossAIEditor : Editor
{
    private static readonly string[] MainTabs =
    {
        "설정",
        "참조"
    };

    private static readonly HashSet<string> HogFields = new()
    {
        "bossGraph",
        "enragePhase1Seconds",
        "enragePhase1MaxBullets",
        "enragePhase2Seconds",
        "enragePhase2MaxBullets",
        "enrageWindupSeconds",
        "enrageWindupShakeDistance",
        "enrageWindupShakeFrequency",
        "enrageBurstSprite",
        "enrageBurstTargetScale",
        "enrageBurstGrowSeconds",
        "enrageBurstHoldSeconds",
        "enrageBurstFadeSeconds",
        "enrageBurstColor",
        "enrageShakeAmplitude",
        "enrageShakeSeconds",
        "enrageShakeZoom"
    };

    private static readonly HashSet<string> ReferenceFields = new()
    {
        "effectData",
        "colorSettings",
        "bodyRoot",
        "body",
        "statusView",
        "obstacleMask",
        "lockOnIndicator",
        "executionIndicator",
        "bossCombatUiRoot",
        "bossHpBarView",
        "bossLivesView",
        "bossEnrageBarView"
    };

    private static readonly HashSet<string> LegacyColorFields = new()
    {
        "normalColor",
        "hpEmptyColor",
        "staggeredColor",
        "statusBarBackgroundColor",
        "hpBarColor",
        "emptyHpBarColor",
        "lockOnIndicatorColor",
        "executionIndicatorColor"
    };

    private static readonly Dictionary<string, bool> FoldoutStates = new();

    private int mainTabIndex;
    private bool showBossBase;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawScriptField();
        DrawBossGraphSection();
        mainTabIndex = GUILayout.SelectionGrid(mainTabIndex, MainTabs, MainTabs.Length);
        EditorGUILayout.Space(6f);

        switch (mainTabIndex)
        {
            case 1:
                DrawReferencesTab();
                break;
            default:
                DrawSettingsTab();
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawBossGraphSection()
    {
        SerializedProperty bossGraph = serializedObject.FindProperty("bossGraph");
        if (bossGraph == null)
        {
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("보스 그래프", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(bossGraph);

        bool hasMultipleValues = bossGraph.hasMultipleDifferentValues;
        BossGraphAsset graph = !hasMultipleValues ? bossGraph.objectReferenceValue as BossGraphAsset : null;
        if (graph != null)
        {
            EditorGUILayout.HelpBox("현재 런타임은 Boss Graph를 사용합니다.", MessageType.Info);
        }
        else if (!hasMultipleValues)
        {
            EditorGUILayout.HelpBox("Boss Graph가 비어 있으면 HogBossAI는 패턴을 실행하지 않습니다.", MessageType.Warning);
        }

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(graph == null))
        {
            if (GUILayout.Button("그래프 에디터 열기"))
            {
                BossGraphEditorWindow.Open(graph);
            }
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6f);
    }

    private void DrawSettingsTab()
    {
        DrawEnrageSection();

        EditorGUILayout.Space(6f);
        showBossBase = EditorGUILayout.Foldout(showBossBase, "보스 설정", true);
        if (showBossBase)
        {
            DrawBaseProperties();
        }
    }

    private void DrawReferencesTab()
    {
        DrawGraphReferences();

        EditorGUILayout.Space(6f);
        DrawPropertiesBox("Scene References", "bodyRoot", "body", "statusView", "obstacleMask", "lockOnIndicator", "executionIndicator");

        EditorGUILayout.Space(6f);
        DrawPropertiesBox("Boss Combat UI", "bossCombatUiRoot", "bossHpBarView", "bossLivesView", "bossEnrageBarView");

        EditorGUILayout.Space(6f);
        DrawFallbackReferences();
    }

    private void DrawGraphReferences()
    {
        SerializedProperty bossGraph = serializedObject.FindProperty("bossGraph");
        if (bossGraph == null)
        {
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Graph References", EditorStyles.boldLabel);
        if (bossGraph.hasMultipleDifferentValues)
        {
            EditorGUILayout.HelpBox("여러 Boss Graph가 선택되어 참조 설정을 표시할 수 없습니다.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        BossGraphAsset graph = bossGraph.objectReferenceValue as BossGraphAsset;
        if (graph == null)
        {
            EditorGUILayout.HelpBox("Boss Graph가 비어 있으면 로컬 fallback 참조를 사용합니다.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        SerializedObject graphObject = new(graph);
        graphObject.Update();
        SerializedProperty references = graphObject.FindProperty("references");
        if (references != null)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(references, true);
            if (EditorGUI.EndChangeCheck())
            {
                graphObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(graph);
            }
        }

        DrawCopyLocalFallbackButton(graphObject, graph);
        EditorGUILayout.EndVertical();
    }

    private void DrawCopyLocalFallbackButton(SerializedObject graphObject, BossGraphAsset graph)
    {
        SerializedProperty localEffectData = serializedObject.FindProperty("effectData");
        SerializedProperty localColorSettings = serializedObject.FindProperty("colorSettings");
        bool hasLocalReference =
            localEffectData?.objectReferenceValue != null
            || localColorSettings?.objectReferenceValue != null;

        using (new EditorGUI.DisabledScope(serializedObject.isEditingMultipleObjects || !hasLocalReference))
        {
            if (!GUILayout.Button("로컬 Fallback 참조를 Graph로 복사"))
            {
                return;
            }
        }

        SerializedProperty references = graphObject.FindProperty("references");
        if (references == null)
        {
            return;
        }

        Undo.RecordObject(graph, "Copy Boss Graph References");
        CopyObjectReference(localEffectData, references.FindPropertyRelative("effectData"));
        CopyObjectReference(localColorSettings, references.FindPropertyRelative("colorSettings"));
        graphObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(graph);
    }

    private static void CopyObjectReference(SerializedProperty source, SerializedProperty destination)
    {
        if (source == null || destination == null)
        {
            return;
        }

        destination.objectReferenceValue = source.objectReferenceValue;
    }

    private void DrawFallbackReferences()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        bool expanded = DrawFoldout("references.fallback", "Local Fallback References");
        if (expanded)
        {
            DrawProperty("effectData");
            DrawProperty("colorSettings");
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawEnrageSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        bool expanded = DrawFoldout("settings.enrage", "광폭화 / 진입 연출");
        if (!expanded)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        DrawProperty("enragePhase1Seconds");
        DrawProperty("enragePhase1MaxBullets");
        DrawProperty("enragePhase2Seconds");
        DrawProperty("enragePhase2MaxBullets");

        EditorGUILayout.Space(4f);
        DrawProperty("enrageWindupSeconds");
        DrawProperty("enrageWindupShakeDistance");
        DrawProperty("enrageWindupShakeFrequency");

        EditorGUILayout.Space(4f);
        DrawProperty("enrageBurstSprite");
        DrawProperty("enrageBurstTargetScale");
        DrawProperty("enrageBurstGrowSeconds");
        DrawProperty("enrageBurstHoldSeconds");
        DrawProperty("enrageBurstFadeSeconds");
        DrawProperty("enrageBurstColor");
        DrawProperty("enrageShakeAmplitude");
        DrawProperty("enrageShakeSeconds");
        DrawProperty("enrageShakeZoom");

        EditorGUILayout.EndVertical();
    }

    private static bool DrawFoldout(string key, string title)
    {
        if (!FoldoutStates.TryGetValue(key, out bool expanded))
        {
            expanded = true;
        }

        expanded = EditorGUILayout.Foldout(expanded, title, true);
        FoldoutStates[key] = expanded;
        return expanded;
    }

    private void DrawProperty(string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, true);
        }
    }

    private void DrawPropertiesBox(string title, params string[] propertyNames)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        for (int i = 0; i < propertyNames.Length; i++)
        {
            DrawProperty(propertyNames[i]);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBaseProperties()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.propertyPath == "m_Script"
                || HogFields.Contains(property.propertyPath)
                || ReferenceFields.Contains(property.propertyPath)
                || LegacyColorFields.Contains(property.propertyPath))
            {
                continue;
            }

            EditorGUILayout.PropertyField(property, true);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawScriptField()
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        }
    }
}
