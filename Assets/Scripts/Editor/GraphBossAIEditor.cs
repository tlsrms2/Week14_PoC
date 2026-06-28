using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Week14.Enemy;

[CustomEditor(typeof(GraphBossAI), true)]
[CanEditMultipleObjects]
public class GraphBossAIEditor : Editor
{
    private static readonly string[] MainTabs =
    {
        "투사체",
        "설정",
        "참조"
    };

    private static readonly HashSet<string> GraphFields = new()
    {
        "bossGraph",
        "graphProjectiles"
    };

    private static readonly HashSet<string> MinionFields = new()
    {
        "minionPatternEnabled",
        "minionProjectileOrigin",
        "minionSummon",
        "releaseMinionsOnDisable",
        "killSpawnedMinionsOnOwnerDeath"
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
        "bossLivesView"
    };

    private static readonly HashSet<string> HiddenBaseColorFields = new()
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

    private int mainTabIndex;
    private bool showBossBase;
    private bool showBossSpecific = true;

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
                DrawSettingsTab();
                break;
            case 2:
                DrawReferencesTab();
                break;
            default:
                DrawProjectilesTab();
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawBossGraphSection()
    {
        SerializedProperty bossGraph = FindSerializedProperty("bossGraph");
        if (bossGraph == null)
        {
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Boss Graph", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(bossGraph);

        bool hasMultipleValues = bossGraph.hasMultipleDifferentValues;
        BossGraphAsset graph = !hasMultipleValues ? bossGraph.objectReferenceValue as BossGraphAsset : null;
        if (graph == null && !hasMultipleValues)
        {
            EditorGUILayout.HelpBox("Boss Graph가 비어 있으면 패턴을 실행하지 않습니다.", MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(graph == null))
        {
            if (GUILayout.Button("Boss Graph 열기"))
            {
                BossGraphEditorWindow.Open(graph, GetGraphProjectileNames(), GetBossHierarchyRoot());
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6f);
    }

    private void DrawProjectilesTab()
    {
        SerializedProperty projectiles = FindSerializedProperty("graphProjectiles");
        if (projectiles == null)
        {
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Boss Graph 투사체", EditorStyles.boldLabel);
        DrawProjectileList(projectiles);
        DrawProjectileWarnings(projectiles);
        EditorGUILayout.EndVertical();
    }

    private void DrawSettingsTab()
    {
        DrawBossSpecificSection();
        DrawMinionSettingsSection();

        EditorGUILayout.Space(6f);
        showBossBase = EditorGUILayout.Foldout(showBossBase, "보스 기본 설정", true);
        if (showBossBase)
        {
            DrawBaseProperties();
        }
    }

    private void DrawReferencesTab()
    {
        DrawPropertiesBox(
            "Graph References",
            "effectData",
            "colorSettings");

        EditorGUILayout.Space(6f);
        DrawPropertiesBox(
            "Scene References",
            "bodyRoot",
            "body",
            "statusView",
            "obstacleMask",
            "lockOnIndicator",
            "executionIndicator");

        EditorGUILayout.Space(6f);
        DrawPropertiesBox("Boss Combat UI", "bossCombatUiRoot", "bossHpBarView", "bossLivesView");
    }

    private void DrawBossSpecificSection()
    {
        List<SerializedProperty> properties = GetBossSpecificProperties();
        if (properties.Count == 0)
        {
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showBossSpecific = EditorGUILayout.Foldout(showBossSpecific, "보스 전용 설정", true);
        if (showBossSpecific)
        {
            for (int i = 0; i < properties.Count; i++)
            {
                EditorGUILayout.PropertyField(properties[i], true);
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6f);
    }

    private void DrawMinionSettingsSection()
    {
        SerializedProperty enabled = FindSerializedProperty("minionPatternEnabled");
        if (enabled == null)
        {
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("미니언 설정", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(enabled, new GUIContent("미니언 사용"));
        using (new EditorGUI.DisabledScope(!enabled.boolValue && !enabled.hasMultipleDifferentValues))
        {
            DrawProperty("minionProjectileOrigin");
            SerializedProperty summon = FindSerializedProperty("minionSummon");
            if (summon != null)
            {
                DrawChild(summon, "prefab");
                DrawChild(summon, "claimSceneMinions");
                DrawChild(summon, "maxOwnedMinions");
                DrawChild(summon, "summonCount");
                DrawChild(summon, "spawnRadius");
                DrawChild(summon, "summonInterval");
                DrawChild(summon, "introSeconds");
                DrawChild(summon, "introStartScale");
                DrawChild(summon, "minAutoSummonInterval");
                DrawChild(summon, "maxAutoSummonInterval");
            }

            DrawProperty("releaseMinionsOnDisable");
            DrawProperty("killSpawnedMinionsOnOwnerDeath");
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawProjectileList(SerializedProperty projectiles)
    {
        for (int i = 0; i < projectiles.arraySize; i++)
        {
            SerializedProperty entry = projectiles.GetArrayElementAtIndex(i);
            SerializedProperty projectileName = entry.FindPropertyRelative("projectileName");
            SerializedProperty projectile = entry.FindPropertyRelative("projectile");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(28f));
                if (projectileName != null)
                {
                    EditorGUILayout.PropertyField(projectileName, GUIContent.none);
                }

                using (new EditorGUI.DisabledScope(i <= 0))
                {
                    if (GUILayout.Button("Up", GUILayout.Width(36f)))
                    {
                        projectiles.MoveArrayElement(i, i - 1);
                        EditorGUILayout.EndVertical();
                        return;
                    }
                }

                using (new EditorGUI.DisabledScope(i >= projectiles.arraySize - 1))
                {
                    if (GUILayout.Button("Down", GUILayout.Width(52f)))
                    {
                        projectiles.MoveArrayElement(i, i + 1);
                        EditorGUILayout.EndVertical();
                        return;
                    }
                }

                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    projectiles.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndVertical();
                    return;
                }
            }

            if (projectile != null)
            {
                EditorGUILayout.PropertyField(projectile, new GUIContent("Settings"), true);
            }

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("투사체 추가"))
        {
            int index = projectiles.arraySize;
            projectiles.InsertArrayElementAtIndex(index);
            SerializedProperty entry = projectiles.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("projectileName").stringValue = index == 0 ? "Default" : $"Projectile{index + 1}";
        }
    }

    private void DrawProjectileWarnings(SerializedProperty projectiles)
    {
        if (projectiles.arraySize == 0)
        {
            EditorGUILayout.HelpBox("기본 투사체가 없습니다. BossGraph 발사 액션이 실행되지 않을 수 있습니다.", MessageType.Warning);
            return;
        }

        HashSet<string> names = new();
        for (int i = 0; i < projectiles.arraySize; i++)
        {
            SerializedProperty entry = projectiles.GetArrayElementAtIndex(i);
            string projectileName = entry.FindPropertyRelative("projectileName")?.stringValue?.Trim();
            if (string.IsNullOrWhiteSpace(projectileName))
            {
                EditorGUILayout.HelpBox($"#{i + 1} 투사체 이름이 비어 있습니다. 이름 없는 액션은 첫 항목만 기본값으로 사용합니다.", MessageType.Info);
                continue;
            }

            if (!names.Add(projectileName))
            {
                EditorGUILayout.HelpBox($"중복 투사체 이름 '{projectileName}'이 있습니다. 먼저 발견된 항목이 사용됩니다.", MessageType.Warning);
            }
        }
    }

    private void DrawBaseProperties()
    {
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (ShouldSkipBaseProperty(iterator))
            {
                continue;
            }

            EditorGUILayout.PropertyField(iterator, true);
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

    private List<SerializedProperty> GetBossSpecificProperties()
    {
        List<SerializedProperty> properties = new();
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (ShouldSkipKnownProperty(iterator))
            {
                continue;
            }

            properties.Add(iterator.Copy());
        }

        return properties;
    }

    private bool ShouldSkipBaseProperty(SerializedProperty property)
    {
        return property.propertyPath == "m_Script"
            || GraphFields.Contains(property.name)
            || MinionFields.Contains(property.name)
            || ReferenceFields.Contains(property.name)
            || HiddenBaseColorFields.Contains(property.name)
            || IsBossSpecificProperty(property);
    }

    private bool ShouldSkipKnownProperty(SerializedProperty property)
    {
        return property.propertyPath == "m_Script"
            || GraphFields.Contains(property.name)
            || MinionFields.Contains(property.name)
            || ReferenceFields.Contains(property.name)
            || HiddenBaseColorFields.Contains(property.name)
            || IsBossBaseProperty(property);
    }

    private static bool IsBossBaseProperty(SerializedProperty property)
    {
        string path = property.propertyPath;
        return path == "maxLives"
            || path == "phaseTransitionWaitSeconds"
            || path == "deathAnimator"
            || path == "deathTriggerName"
            || path == "finalDeathExplosionSeconds"
            || path == "finalDeathExplosionCount"
            || path == "finalDeathExplosionScale"
            || path == "finalDeathExplosionSparkCount"
            || path == "finalDeathExplosionColor"
            || path == "deathAnimationFallbackSeconds"
            || path == "bossData"
            || path == "displayName"
            || path == "maxHp"
            || path == "hpEmptyExecutionSeconds"
            || path == "detectionRange"
            || path == "moveSpeed";
    }

    private static bool IsBossSpecificProperty(SerializedProperty property)
    {
        return !IsBossBaseProperty(property);
    }

    private void DrawProperty(string propertyName)
    {
        SerializedProperty property = FindSerializedProperty(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, true);
        }
    }

    private static void DrawChild(SerializedProperty parent, string childName)
    {
        SerializedProperty child = parent.FindPropertyRelative(childName);
        if (child != null)
        {
            EditorGUILayout.PropertyField(child, true);
        }
    }

    private void DrawScriptField()
    {
        SerializedProperty script = serializedObject.FindProperty("m_Script");
        if (script == null)
        {
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(script);
        }
    }

    private List<string> GetGraphProjectileNames()
    {
        List<string> names = new();
        SerializedProperty projectiles = FindSerializedProperty("graphProjectiles");
        if (projectiles == null || !projectiles.isArray)
        {
            return names;
        }

        for (int i = 0; i < projectiles.arraySize; i++)
        {
            SerializedProperty entry = projectiles.GetArrayElementAtIndex(i);
            string value = entry.FindPropertyRelative("projectileName")?.stringValue;
            if (!string.IsNullOrWhiteSpace(value) && !names.Contains(value))
            {
                names.Add(value);
            }
        }

        return names;
    }

    private Transform GetBossHierarchyRoot()
    {
        return targets.Length == 1 && target is GraphBossAI boss ? boss.transform : null;
    }

    private SerializedProperty FindSerializedProperty(string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            return property;
        }

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.name == propertyName)
            {
                return iterator.Copy();
            }
        }

        return null;
    }
}
