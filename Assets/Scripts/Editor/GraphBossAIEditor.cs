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
        "bossLivesView",
        "bossNameText"
    };

    private static readonly HashSet<string> CombatEffectFields = new()
    {
        "hitFlashColor",
        "hitFlashSeconds"
    };

    private static readonly HashSet<string> MetaFields = new()
    {
        "displayName"
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

    private int mainTabIndex;
    private bool showBossSpecific = true;
    private bool showMinionSettings = true;
    private bool showBossBase;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawScriptField();
        DrawBossGraphSection();

        mainTabIndex = Mathf.Clamp(mainTabIndex, 0, MainTabs.Length - 1);
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
            EditorGUILayout.HelpBox("Boss Graph가 비어 있으면 패턴이 실행되지 않습니다.", MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(graph == null))
        {
            if (GUILayout.Button("Graph Editor 열기"))
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
        DrawConductorConductingPatternSection();
        DrawAdditionalSettingsSections();
        DrawMinionSettingsSection();

        EditorGUILayout.Space(6f);
        showBossBase = EditorGUILayout.Foldout(showBossBase, "보스 기본 설정", true);
        if (showBossBase)
        {
            DrawBaseProperties();
        }
    }

    protected virtual void DrawAdditionalSettingsSections()
    {
    }

    private void DrawConductorConductingPatternSection()
    {
        SerializedProperty patterns = FindSerializedProperty("conductingPatterns");
        if (patterns == null)
        {
            return;
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Conductor 지휘 모양 데이터", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("여기에는 획 순서와 모양만 저장합니다. Graph Action에서 Pattern ID를 선택하고 색/크기/속도 같은 연출 설정을 정하세요.", MessageType.Info);

        if (patterns.hasMultipleDifferentValues)
        {
            EditorGUILayout.HelpBox("여러 Conductor를 동시에 선택한 상태에서는 지휘 패턴을 편집하지 않습니다.", MessageType.Info);
            EditorGUILayout.PropertyField(patterns, true);
            EditorGUILayout.EndVertical();
            return;
        }

        DrawConductingPatternList(patterns);
        EditorGUILayout.EndVertical();
    }

    private void DrawReferencesTab()
    {
        DrawGraphReferences();
        DrawPropertiesBox("Boss References", "effectData", "colorSettings");
        DrawPropertiesBox("Scene References", "bodyRoot", "body", "statusView", "obstacleMask", "lockOnIndicator", "executionIndicator");
        DrawPropertiesBox("Boss Name", "displayName");
        DrawPropertiesBox("Boss Combat UI", "bossCombatUiRoot", "bossHpBarView", "bossLivesView", "bossNameText");
        DrawPropertiesBox("Hit Flash", "hitFlashColor", "hitFlashSeconds");
    }

    private void DrawGraphReferences()
    {
        SerializedProperty bossGraph = FindSerializedProperty("bossGraph");
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
            EditorGUILayout.Space(6f);
            return;
        }

        BossGraphAsset graph = bossGraph.objectReferenceValue as BossGraphAsset;
        if (graph == null)
        {
            EditorGUILayout.HelpBox("Boss Graph가 비어 있습니다.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6f);
            return;
        }

        SerializedObject graphObject = new SerializedObject(graph);
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
        EditorGUILayout.Space(6f);
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
        showMinionSettings = EditorGUILayout.Foldout(showMinionSettings, "미니언 설정", true);
        if (!showMinionSettings)
        {
            EditorGUILayout.EndVertical();
            return;
        }

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

            DrawCommonProjectileSettings(projectile);
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

    private static void DrawCommonProjectileSettings(SerializedProperty projectile)
    {
        if (projectile == null)
        {
            return;
        }

        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("공통 설정", EditorStyles.boldLabel);
        DrawChild(projectile, "prefab", "Prefab");
        DrawChild(projectile, "bulletDamage");
        DrawChild(projectile, "chargeSeconds");
        DrawChild(projectile, "chargeDriftSpeed");
        DrawChild(projectile, "aimAtPlayerWhileCharging");
        DrawChild(projectile, "aimAtPlayerOnLaunch");
        DrawChild(projectile, "speed");
        DrawChild(projectile, "lifetime");
        DrawChild(projectile, "radius");
        DrawChild(projectile, "trailSeconds");
        DrawChild(projectile, "trailWidthMultiplier");
    }

    private void DrawProjectileWarnings(SerializedProperty projectiles)
    {
        if (projectiles.arraySize == 0)
        {
            EditorGUILayout.HelpBox("기본 투사체가 없습니다. Boss Graph 발사 액션이 실행되지 않을 수 있습니다.", MessageType.Warning);
            return;
        }

        HashSet<string> names = new(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < projectiles.arraySize; i++)
        {
            SerializedProperty entry = projectiles.GetArrayElementAtIndex(i);
            SerializedProperty projectileNameProperty = entry.FindPropertyRelative("projectileName");
            string projectileName = projectileNameProperty?.stringValue?.Trim();
            if (string.IsNullOrWhiteSpace(projectileName))
            {
                EditorGUILayout.HelpBox($"#{i + 1} 투사체 Name이 비어 있습니다. 그래프에서 이름 없이 참조하면 첫 항목이 사용됩니다.", MessageType.Info);
            }
            else if (!names.Add(projectileName))
            {
                EditorGUILayout.HelpBox($"중복 투사체 Name '{projectileName}'이 있습니다. 먼저 발견된 항목이 사용됩니다.", MessageType.Warning);
            }

            SerializedProperty projectile = entry.FindPropertyRelative("projectile");
            SerializedProperty prefab = projectile?.FindPropertyRelative("prefab");
            if (prefab != null && prefab.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox($"#{i + 1} 투사체 Prefab이 비어 있습니다.", MessageType.Warning);
            }
        }
    }

    private static void DrawConductingPatternList(SerializedProperty patterns)
    {
        for (int i = 0; i < patterns.arraySize; i++)
        {
            SerializedProperty pattern = patterns.GetArrayElementAtIndex(i);
            SerializedProperty patternId = pattern.FindPropertyRelative("patternId");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Shape {i + 1}", GUILayout.Width(64f));
                if (patternId != null)
                {
                    EditorGUILayout.PropertyField(patternId, GUIContent.none);
                }

                if (GUILayout.Button("Draw", GUILayout.Width(48f)))
                {
                    ConductorConductingPatternEditorWindow.Open(patterns.serializedObject.targetObject, i);
                }

                DrawConductingMoveButtons(patterns, i);
                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    patterns.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndVertical();
                    break;
                }
            }

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("지휘 패턴 추가"))
        {
            AddConductingPattern(patterns);
        }

        DrawConductingPatternWarnings(patterns);
    }

    private static void DrawConductingMoveButtons(SerializedProperty array, int index)
    {
        using (new EditorGUI.DisabledScope(index <= 0))
        {
            if (GUILayout.Button("Up", GUILayout.Width(32f)))
            {
                array.MoveArrayElement(index, index - 1);
            }
        }

        using (new EditorGUI.DisabledScope(index >= array.arraySize - 1))
        {
            if (GUILayout.Button("Dn", GUILayout.Width(32f)))
            {
                array.MoveArrayElement(index, index + 1);
            }
        }
    }

    private static void DrawConductingPatternWarnings(SerializedProperty patterns)
    {
        HashSet<string> ids = new(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < patterns.arraySize; i++)
        {
            string id = patterns.GetArrayElementAtIndex(i)
                .FindPropertyRelative("patternId")?.stringValue?.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                EditorGUILayout.HelpBox($"Pattern {i + 1}: Pattern ID가 비어 있습니다.", MessageType.Warning);
            }
            else if (!ids.Add(id))
            {
                EditorGUILayout.HelpBox($"Pattern ID '{id}'가 중복됩니다.", MessageType.Warning);
            }
        }
    }

    private static void AddConductingPattern(SerializedProperty patterns)
    {
        int index = patterns.arraySize;
        patterns.InsertArrayElementAtIndex(index);
        SerializedProperty pattern = patterns.GetArrayElementAtIndex(index);
        pattern.isExpanded = true;

        SetChildString(pattern, "patternId", GetUniqueConductingPatternId(patterns, index));

        SerializedProperty strokes = pattern.FindPropertyRelative("strokes");
        if (strokes != null)
        {
            strokes.ClearArray();
            AddConductingStroke(strokes);
        }
    }

    private static void AddConductingStroke(SerializedProperty strokes)
    {
        int index = strokes.arraySize;
        strokes.InsertArrayElementAtIndex(index);
        SerializedProperty stroke = strokes.GetArrayElementAtIndex(index);
        stroke.isExpanded = true;

        SerializedProperty points = stroke.FindPropertyRelative("points");
        if (points != null)
        {
            points.ClearArray();
            AddConductingPoint(points, new Vector2(-0.25f, 0f));
            AddConductingPoint(points, new Vector2(0.25f, 0f));
        }
    }

    private static void AddConductingPoint(SerializedProperty points, Vector2 value)
    {
        int index = points.arraySize;
        points.InsertArrayElementAtIndex(index);
        points.GetArrayElementAtIndex(index).vector2Value = value;
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
        if (!ContainsPropertyName(propertyNames))
        {
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        for (int i = 0; i < propertyNames.Length; i++)
        {
            DrawProperty(propertyNames[i]);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6f);
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
        return IsAlwaysHiddenProperty(property)
            || GraphFields.Contains(property.name)
            || MinionFields.Contains(property.name)
            || ReferenceFields.Contains(property.name)
            || CombatEffectFields.Contains(property.name)
            || MetaFields.Contains(property.name)
            || LegacyColorFields.Contains(property.name)
            || !IsBossBaseProperty(property);
    }

    private bool ShouldSkipKnownProperty(SerializedProperty property)
    {
        return IsAlwaysHiddenProperty(property)
            || GraphFields.Contains(property.name)
            || MinionFields.Contains(property.name)
            || ReferenceFields.Contains(property.name)
            || CombatEffectFields.Contains(property.name)
            || MetaFields.Contains(property.name)
            || LegacyColorFields.Contains(property.name)
            || IsBossBaseProperty(property);
    }

    private static bool IsAlwaysHiddenProperty(SerializedProperty property)
    {
        return property.propertyPath == "m_Script";
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
            || path == "finalDeathExplosionPrefab"
            || path == "finalDeathExplosionPrefabLifetimeSeconds"
            || path == "finalDeathExplosionAreaCenter"
            || path == "finalDeathExplosionAreaRadius"
            || path == "deathExplosionToAnimationDelaySeconds"
            || path == "deathAnimationFallbackSeconds"
            || path == "bossData"
            || path == "displayName"
            || path == "maxHp"
            || path == "hpEmptyExecutionSeconds"
            || path == "detectionRange"
            || path == "moveSpeed";
    }

    private void DrawProperty(string propertyName)
    {
        SerializedProperty property = FindSerializedProperty(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, true);
        }
    }

    private bool ContainsPropertyName(params string[] propertyNames)
    {
        for (int i = 0; i < propertyNames.Length; i++)
        {
            if (FindSerializedProperty(propertyNames[i]) != null)
            {
                return true;
            }
        }

        return false;
    }

    private static void DrawChild(SerializedProperty parent, string childName, string label = null)
    {
        SerializedProperty child = parent.FindPropertyRelative(childName);
        if (child == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(label))
        {
            EditorGUILayout.PropertyField(child, true);
        }
        else
        {
            EditorGUILayout.PropertyField(child, new GUIContent(label), true);
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

    private static string GetUniqueProjectileName(SerializedProperty projectiles, int currentIndex)
    {
        string baseName = currentIndex == 0 ? "Default" : $"Projectile{currentIndex + 1}";
        string nextName = baseName;
        int suffix = 2;
        while (HasProjectileName(projectiles, nextName, currentIndex))
        {
            nextName = $"{baseName}_{suffix}";
            suffix++;
        }

        return nextName;
    }

    private static bool HasProjectileName(SerializedProperty projectiles, string projectileName, int exceptIndex)
    {
        for (int i = 0; i < projectiles.arraySize; i++)
        {
            if (i == exceptIndex)
            {
                continue;
            }

            SerializedProperty entry = projectiles.GetArrayElementAtIndex(i);
            string value = entry.FindPropertyRelative("projectileName")?.stringValue?.Trim();
            if (string.Equals(value, projectileName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetUniqueConductingPatternId(SerializedProperty patterns, int currentIndex)
    {
        string baseId = $"Pattern{currentIndex + 1}";
        string nextId = baseId;
        int suffix = 2;
        while (HasConductingPatternId(patterns, nextId, currentIndex))
        {
            nextId = $"{baseId}_{suffix}";
            suffix++;
        }

        return nextId;
    }

    private static bool HasConductingPatternId(SerializedProperty patterns, string patternId, int exceptIndex)
    {
        for (int i = 0; i < patterns.arraySize; i++)
        {
            if (i == exceptIndex)
            {
                continue;
            }

            string value = patterns.GetArrayElementAtIndex(i)
                .FindPropertyRelative("patternId")?.stringValue?.Trim();
            if (string.Equals(value, patternId, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void SetChildString(SerializedProperty root, string childName, string value)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child != null)
        {
            child.stringValue = value;
        }
    }

}
