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
        "투사체",
        "설정",
        "참조"
    };

    private static readonly HashSet<string> HogFields = new()
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
        "bossLivesView",
        "bossNameText"
    };

    private static readonly HashSet<string> CombatEffectFields = new()
    {
        "hitFlashColor",
        "hitFlashSeconds"
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
                BossGraphEditorWindow.Open(graph, GetGraphProjectileNames(), GetBossHierarchyRoot());
            }
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6f);
    }

    private void DrawSettingsTab()
    {
        DrawMinionSettingsSection();

        EditorGUILayout.Space(6f);
        showBossBase = EditorGUILayout.Foldout(showBossBase, "보스 설정", true);
        if (showBossBase)
        {
            DrawBaseProperties();
        }
    }

    private void DrawMinionSettingsSection()
    {
        SerializedProperty enabled = FindSerializedProperty("minionPatternEnabled");
        if (enabled == null)
        {
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        bool expanded = DrawFoldout("settings.minions", "소환수 설정");
        if (!expanded)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.PropertyField(enabled, new GUIContent("소환수 사용"));
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

    private void DrawProjectilesTab()
    {
        SerializedProperty projectiles = serializedObject.FindProperty("graphProjectiles");
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

    private void DrawProjectileList(SerializedProperty projectiles)
    {
        for (int i = 0; i < projectiles.arraySize; i++)
        {
            SerializedProperty entry = projectiles.GetArrayElementAtIndex(i);
            SerializedProperty projectileName = entry.FindPropertyRelative("projectileName");
            SerializedProperty projectile = entry.FindPropertyRelative("projectile");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            bool remove = false;
            bool stopDrawingList = false;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(28f));
                if (projectileName != null)
                {
                    EditorGUILayout.PropertyField(projectileName, new GUIContent("Name"));
                }

                using (new EditorGUI.DisabledScope(i <= 0))
                {
                    if (GUILayout.Button("Up", GUILayout.Width(32f)))
                    {
                        projectiles.MoveArrayElement(i, i - 1);
                        stopDrawingList = true;
                    }
                }

                using (new EditorGUI.DisabledScope(i >= projectiles.arraySize - 1))
                {
                    if (GUILayout.Button("Dn", GUILayout.Width(32f)))
                    {
                        projectiles.MoveArrayElement(i, i + 1);
                        stopDrawingList = true;
                    }
                }

                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    remove = true;
                }
            }

            if (remove)
            {
                projectiles.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndVertical();
                break;
            }

            if (stopDrawingList)
            {
                EditorGUILayout.EndVertical();
                break;
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
            SerializedProperty projectileName = entry.FindPropertyRelative("projectileName");
            if (projectileName != null)
            {
                projectileName.stringValue = GetUniqueProjectileName(projectiles, index);
            }
        }
    }

    private static void DrawProjectileWarnings(SerializedProperty projectiles)
    {
        if (projectiles.arraySize == 0)
        {
            EditorGUILayout.HelpBox("Boss Graph에서 사용할 투사체를 하나 이상 추가하세요.", MessageType.Warning);
            return;
        }

        HashSet<string> names = new(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < projectiles.arraySize; i++)
        {
            SerializedProperty entry = projectiles.GetArrayElementAtIndex(i);
            string projectileName = entry.FindPropertyRelative("projectileName")?.stringValue;
            if (string.IsNullOrWhiteSpace(projectileName))
            {
                EditorGUILayout.HelpBox($"투사체 #{i + 1}: 이름이 비어 있습니다.", MessageType.Warning);
                continue;
            }

            if (!names.Add(projectileName))
            {
                EditorGUILayout.HelpBox($"투사체 이름 '{projectileName}'이 중복됩니다.", MessageType.Warning);
            }

            SerializedProperty prefab = entry.FindPropertyRelative("projectile")?.FindPropertyRelative("prefab");
            if (prefab != null && prefab.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox($"투사체 '{projectileName}': Prefab을 설정하세요.", MessageType.Warning);
            }
        }
    }

    private void DrawReferencesTab()
    {
        DrawGraphReferences();

        EditorGUILayout.Space(6f);
        DrawPropertiesBox("Scene References", "bodyRoot", "body", "statusView", "obstacleMask", "lockOnIndicator", "executionIndicator");

        EditorGUILayout.Space(6f);
        DrawPropertiesBox("Boss Combat UI", "bossCombatUiRoot", "bossHpBarView", "bossLivesView", "bossNameText");

        EditorGUILayout.Space(6f);
        DrawPropertiesBox("피격 플래시", "hitFlashColor", "hitFlashSeconds");
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
            EditorGUILayout.HelpBox("Boss Graph가 비어 있습니다.", MessageType.Warning);
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
        EditorGUILayout.EndVertical();
    }

    private List<string> GetGraphProjectileNames()
    {
        List<string> names = new();
        SerializedProperty projectiles = serializedObject.FindProperty("graphProjectiles");
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

    private Transform GetBossHierarchyRoot()
    {
        return targets.Length == 1 && target is HogBossAI boss ? boss.transform : null;
    }

    private static string GetUniqueProjectileName(SerializedProperty projectiles, int currentIndex)
    {
        const string baseName = "Projectile";
        int suffix = currentIndex + 1;
        string candidate = $"{baseName}{suffix}";
        while (HasProjectileName(projectiles, candidate, currentIndex))
        {
            suffix++;
            candidate = $"{baseName}{suffix}";
        }

        return candidate;
    }

    private static bool HasProjectileName(SerializedProperty projectiles, string name, int ignoreIndex)
    {
        for (int i = 0; i < projectiles.arraySize; i++)
        {
            if (i == ignoreIndex)
            {
                continue;
            }

            string existing = projectiles.GetArrayElementAtIndex(i)
                .FindPropertyRelative("projectileName")?.stringValue;
            if (existing == name)
            {
                return true;
            }
        }

        return false;
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
        SerializedProperty property = FindSerializedProperty(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, true);
        }
    }

    private static void DrawChild(SerializedProperty root, string childName)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child != null)
        {
            EditorGUILayout.PropertyField(child, true);
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
            if (IsPropertyNamed(property, "m_Script")
                || ContainsPropertyName(HogFields, property)
                || ContainsPropertyName(MinionFields, property)
                || ContainsPropertyName(ReferenceFields, property)
                || ContainsPropertyName(CombatEffectFields, property)
                || ContainsPropertyName(LegacyColorFields, property))
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

    private SerializedProperty FindSerializedProperty(string propertyName)
    {
        SerializedProperty directProperty = serializedObject.FindProperty(propertyName);
        if (directProperty != null)
        {
            return directProperty;
        }

        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (IsPropertyNamed(property, propertyName))
            {
                return property.Copy();
            }
        }

        return null;
    }

    private static bool ContainsPropertyName(ISet<string> propertyNames, SerializedProperty property)
    {
        if (propertyNames == null || property == null)
        {
            return false;
        }

        foreach (string propertyName in propertyNames)
        {
            if (IsPropertyNamed(property, propertyName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPropertyNamed(SerializedProperty property, string propertyName)
    {
        if (property == null || string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        return property.name == propertyName
            || property.propertyPath == propertyName
            || property.propertyPath.EndsWith($".{propertyName}", System.StringComparison.Ordinal);
    }
}
