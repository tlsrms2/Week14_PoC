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
        "패턴",
        "투사체",
        "설정",
        "참조"
    };

    private static readonly string[] PatternTabs =
    {
        "패턴1",
        "패턴2",
        "패턴3",
        "패턴4",
        "패턴5",
        "패턴6",
        "패턴7"
    };

    private static readonly HashSet<string> HogFields = new()
    {
        "pattern1",
        "pattern2",
        "pattern3",
        "pattern4",
        "pattern5",
        "pattern6",
        "pattern7",
        "projectiles",
        "phasePatterns",
        "minPatternRecoverySeconds",
        "maxPatternRecoverySeconds",
        "showPatternBulletPreview",
        "patternBulletPreviewFullHoldSeconds",
        "patternBulletPreviewSingleGroupFullHoldRatio",
        "randomizePatterns",
        "preventRandomRepeatPattern",
        "debugUseFixedPattern",
        "debugPattern",
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
    private int patternTabIndex;
    private bool showBossBase;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawScriptField();
        mainTabIndex = GUILayout.SelectionGrid(mainTabIndex, MainTabs, MainTabs.Length);
        EditorGUILayout.Space(6f);

        switch (mainTabIndex)
        {
            case 1:
                DrawProjectilesTab();
                break;
            case 2:
                DrawSettingsTab();
                break;
            case 3:
                DrawReferencesTab();
                break;
            default:
                DrawPatternTabs();
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPatternTabs()
    {
        patternTabIndex = GUILayout.SelectionGrid(patternTabIndex, PatternTabs, 4);
        EditorGUILayout.Space(6f);

        switch (patternTabIndex)
        {
            case 1:
                DrawPattern2Tab();
                break;
            case 2:
                DrawPattern3Tab();
                break;
            case 3:
                DrawPattern4Tab();
                break;
            case 4:
                DrawPattern5Tab();
                break;
            case 5:
                DrawPattern6Tab();
                break;
            case 6:
                DrawPattern7Tab();
                break;
            default:
                DrawPattern1Tab();
                break;
        }
    }

    private void DrawSettingsTab()
    {
        DrawProperty("randomizePatterns");
        DrawProperty("preventRandomRepeatPattern");
        DrawPhasePatterns();
        DrawProperty("minPatternRecoverySeconds");
        DrawProperty("maxPatternRecoverySeconds");

        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        bool previewExpanded = DrawFoldout("settings.preview", "프리뷰");
        if (previewExpanded)
        {
            DrawProperty("showPatternBulletPreview");
            DrawProperty("patternBulletPreviewFullHoldSeconds");
            DrawProperty("patternBulletPreviewSingleGroupFullHoldRatio");
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        bool debugExpanded = DrawFoldout("settings.debug", "디버그");
        if (debugExpanded)
        {
            DrawProperty("debugUseFixedPattern");
            DrawProperty("debugPattern");
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6f);
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
        DrawProperty("effectData");
        DrawProperty("colorSettings");

        EditorGUILayout.Space(6f);
        DrawPropertiesBox("Scene References", "bodyRoot", "body", "statusView", "obstacleMask", "lockOnIndicator", "executionIndicator");

        EditorGUILayout.Space(6f);
        DrawPropertiesBox("Boss Combat UI", "bossCombatUiRoot", "bossHpBarView", "bossLivesView", "bossEnrageBarView");
    }

    private void DrawProjectilesTab()
    {
        SerializedProperty projectiles = serializedObject.FindProperty("projectiles");
        if (projectiles == null)
        {
            return;
        }

        EnsureProjectileArray(projectiles);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("투사체 추가"))
        {
            projectiles.arraySize++;
        }

        using (new EditorGUI.DisabledScope(projectiles.arraySize <= 2))
        {
            if (GUILayout.Button("마지막 투사체 삭제"))
            {
                projectiles.DeleteArrayElementAtIndex(projectiles.arraySize - 1);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4f);
        for (int i = 0; i < projectiles.arraySize; i++)
        {
            DrawProjectile(projectiles.GetArrayElementAtIndex(i), $"투사체 {GetProjectileLabel(i)}");
        }
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

    private void DrawPhasePatterns()
    {
        SerializedProperty phasePatterns = serializedObject.FindProperty("phasePatterns");
        if (phasePatterns == null)
        {
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        SerializedProperty maxLives = serializedObject.FindProperty("maxLives");
        int desiredCount = maxLives != null ? Mathf.Max(1, maxLives.intValue) : phasePatterns.arraySize;
        if (phasePatterns.arraySize < desiredCount)
        {
            EditorGUILayout.HelpBox("페이즈 수보다 목록이 적습니다. 실행 또는 값 변경 시 부족한 페이즈가 자동 추가됩니다.", MessageType.Warning);
        }

        EditorGUILayout.PropertyField(phasePatterns, true);
        EditorGUILayout.EndVertical();
    }

    private void DrawPattern1Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("pattern1");
        DrawProjectileIndex(pattern, "projectileIndex", "투사체");
        DrawSection("발사 원점", pattern, "projectileOrigins");
        DrawSection("발사", pattern, "radialBulletCount", "burstInterval", "spawnRadius", "angleStepDegrees");
        DrawEffects(pattern.FindPropertyRelative("effects"));
    }

    private void DrawPattern2Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("pattern2");
        DrawProjectileIndex(pattern, "projectileIndex", "투사체");
        DrawSection("발사 원점", pattern, "projectileOrigins");
        DrawSection("이동", pattern, "moveSpeedMultiplier");
        DrawSection("발사 묶음", pattern, "volleys", "spawnSpacing");
        DrawEffects(pattern.FindPropertyRelative("effects"));
    }

    private void DrawPattern3Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("pattern3");
        DrawProjectileIndex(pattern, "projectileIndex", "투사체");
        DrawSection("발사 원점", pattern, "firePoint");
        DrawSection("준비/조준", pattern, "windupSeconds", "aimTrackingSeconds", "aimSpreadDegrees");
        DrawSection("크기", pattern, "projectileRadiusMultiplier", "finalScaleMultiplier", "startScaleRatio");
        DrawSection("전방위 분열", pattern, "splitDelaySeconds", "bombSfxLeadSeconds", "radialSplitBulletCount", "radialSplitStartAngleOffset", "splitSpeedMultiplier", "splitRadiusMultiplier", "splitLifetimeMultiplier");
        DrawEffects(pattern.FindPropertyRelative("effects"));
    }

    private void DrawPattern4Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("pattern4");
        DrawProjectileIndex(pattern, "projectileIndex", "투사체");
        DrawSection("내려찍기/원점", pattern, "projectileOrigin", "slamUpOffset", "slamDownOffset", "slamRiseSeconds", "slamDropSeconds", "slamRecoverSeconds");
        DrawSection("전방위 발사", pattern, "bulletCount", "waveCount", "waveInterval", "startAngleOffset", "spawnRadius");
        DrawEffects(pattern.FindPropertyRelative("effects"));
    }

    private void DrawPattern5Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("pattern5");
        DrawProjectileIndex(pattern, "projectileIndex", "투사체");
        DrawSection("발사 원점", pattern, "firePoint");
        DrawSection("기 모으기", pattern, "windupSeconds");
        DrawSection("미니건 발사", pattern, "bulletCount", "fireInterval", "spawnSpacing", "sweepStepDegrees", "maxSweepAngle");
        DrawEffects(pattern.FindPropertyRelative("effects"));
    }

    private void DrawPattern6Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("pattern6");
        DrawProjectileIndex(pattern, "projectileIndex", "투사체");
        DrawSection("내려찍기/원점", pattern, "projectileOrigin", "slamUpOffset", "slamDownOffset", "slamRiseSeconds", "slamDropSeconds", "slamRecoverSeconds");
        DrawSection("전방위 발사", pattern, "bulletCount", "waveCount", "waveInterval", "startAngleOffset", "spawnRadius");
        DrawEffects(pattern.FindPropertyRelative("effects"));
    }

    private void DrawPattern7Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("pattern7");
        DrawProjectileIndex(pattern, "normalProjectileIndex", "일반 투사체");
        DrawProjectileIndex(pattern, "secondaryProjectileIndex", "보조 투사체");
        DrawSection("발사 원점", pattern, "firePoint");
        DrawSection("기 모으기", pattern, "windupSeconds");
        DrawSection("세 갈래 반복 발사", pattern, "normalVolleyCount", "normalVolleyInterval", "fanAngleDegrees", "normalSpawnSpacing");
        DrawSection("보조 발사", pattern, "secondaryProjectileOrigins", "secondaryBulletCount", "secondarySpawnForwardOffset");
        DrawEffects(pattern.FindPropertyRelative("effects"));
    }

    private void DrawProjectileIndex(SerializedProperty pattern, string childName, string label)
    {
        if (pattern == null)
        {
            return;
        }

        SerializedProperty projectiles = serializedObject.FindProperty("projectiles");
        if (projectiles == null)
        {
            return;
        }

        EnsureProjectileArray(projectiles);

        SerializedProperty projectileIndex = pattern.FindPropertyRelative(childName);
        if (projectileIndex == null)
        {
            return;
        }

        string[] labels = GetProjectileLabels(projectiles.arraySize);
        projectileIndex.intValue = Mathf.Clamp(projectileIndex.intValue, 0, labels.Length - 1);
        projectileIndex.intValue = EditorGUILayout.Popup(label, projectileIndex.intValue, labels);
    }

    private static void EnsureProjectileArray(SerializedProperty projectiles)
    {
        if (projectiles == null)
        {
            return;
        }

        while (projectiles.arraySize < 2)
        {
            projectiles.arraySize++;
        }
    }

    private static string[] GetProjectileLabels(int count)
    {
        int safeCount = Mathf.Max(1, count);
        string[] labels = new string[safeCount];
        for (int i = 0; i < safeCount; i++)
        {
            labels[i] = $"투사체 {GetProjectileLabel(i)}";
        }

        return labels;
    }

    private static string GetProjectileLabel(int index)
    {
        int value = Mathf.Max(0, index);
        string label = string.Empty;
        do
        {
            label = (char)('A' + value % 26) + label;
            value = value / 26 - 1;
        }
        while (value >= 0);

        return label;
    }

    private void DrawProjectile(SerializedProperty projectile, string title)
    {
        if (projectile == null)
        {
            return;
        }

        ApplyHomingDefaults(projectile);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        string key = $"{projectile.propertyPath}.{title}";
        bool expanded = DrawFoldout(key, title);
        if (!expanded)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        DrawChild(projectile, "prefab");
        DrawChild(projectile, "homingChargePrefab");
        DrawChild(projectile, "bulletDamage");
        DrawChild(projectile, "chargeSeconds");
        DrawChild(projectile, "chargeDriftSpeed");
        DrawChild(projectile, "speed");
        DrawChild(projectile, "lifetime");
        DrawChild(projectile, "radius");
        DrawChild(projectile, "chargingColor");
        DrawChild(projectile, "launchedColor");
        DrawChild(projectile, "homingBlinkColor");
        DrawChild(projectile, "trailColor");
        DrawChild(projectile, "indicatorColor");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        bool advancedExpanded = DrawFoldout($"{key}.advanced", "투사체 고급값");
        if (advancedExpanded)
        {
            DrawChild(projectile, "aimAtPlayerWhileCharging");
            DrawChild(projectile, "trailSeconds");
            DrawChild(projectile, "trailWidthMultiplier");
            DrawChild(projectile, "homingEnabled");
            ApplyHomingDefaults(projectile);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndVertical();
    }

    private static void ApplyHomingDefaults(SerializedProperty projectile)
    {
        SerializedProperty homingEnabled = FindChild(projectile, "homingEnabled");
        if (homingEnabled == null || !homingEnabled.boolValue)
        {
            return;
        }

        SerializedProperty homingSeconds = FindChild(projectile, "homingSeconds");
        if (homingSeconds != null && homingSeconds.floatValue <= 0f)
        {
            homingSeconds.floatValue = 10f;
        }

        SerializedProperty homingTurnDegrees = FindChild(projectile, "homingTurnDegreesPerSecond");
        if (homingTurnDegrees != null && homingTurnDegrees.floatValue <= 0f)
        {
            homingTurnDegrees.floatValue = 540f;
        }
    }

    private static void DrawSection(string title, SerializedProperty root, params string[] childNames)
    {
        if (root == null)
        {
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        bool expanded = DrawFoldout($"{root.propertyPath}.{title}", title);
        if (!expanded)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        for (int i = 0; i < childNames.Length; i++)
        {
            DrawChild(root, childNames[i]);
        }

        EditorGUILayout.EndVertical();
    }

    private static void DrawEffects(SerializedProperty effects)
    {
        if (effects == null)
        {
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        bool expanded = DrawFoldout($"{effects.propertyPath}.effects", "이펙트");
        if (!expanded)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        SerializedProperty explosion = effects.FindPropertyRelative("explosion");
        DrawParticleEffect(explosion, "폭발", true);

        SerializedProperty smoke = effects.FindPropertyRelative("smoke");
        DrawParticleEffect(smoke, "연기", true);
        DrawNamedChild(effects, "smokeInterval", "연기 간격");

        SerializedProperty muzzleFlash = effects.FindPropertyRelative("muzzleFlash");
        DrawParticleEffect(muzzleFlash, "총구 화염", false);

        SerializedProperty cameraShake = effects.FindPropertyRelative("cameraShake");
        DrawCameraShake(cameraShake);

        EditorGUILayout.EndVertical();
    }

    private static void DrawParticleEffect(SerializedProperty effect, string labelPrefix, bool drawCount)
    {
        if (effect == null)
        {
            return;
        }

        DrawNamedChild(effect, "enabled", $"{labelPrefix} 사용");
        DrawNamedChild(effect, "color", $"{labelPrefix} 색");
        DrawNamedChild(effect, "scale", $"{labelPrefix} 크기");
        if (drawCount)
        {
            DrawNamedChild(effect, "count", $"{labelPrefix} 개수");
        }
    }

    private static void DrawCameraShake(SerializedProperty cameraShake)
    {
        if (cameraShake == null)
        {
            return;
        }

        DrawNamedChild(cameraShake, "enabled", "카메라 쉐이크 사용");
        DrawNamedChild(cameraShake, "seconds", "카메라 쉐이크 시간");
        DrawNamedChild(cameraShake, "distance", "카메라 쉐이크 거리");
        DrawNamedChild(cameraShake, "frequency", "카메라 쉐이크 빈도");
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

    private static void DrawChild(SerializedProperty root, string childName)
    {
        SerializedProperty child = FindChild(root, childName);
        if (child != null)
        {
            EditorGUILayout.PropertyField(child, true);
        }
    }

    private static void DrawNamedChild(SerializedProperty root, string childName, string label)
    {
        SerializedProperty child = FindChild(root, childName);
        if (child != null)
        {
            EditorGUILayout.PropertyField(child, new GUIContent(label), true);
        }
    }

    private static SerializedProperty FindChild(SerializedProperty root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child != null)
        {
            return child;
        }

        SerializedProperty iterator = root.Copy();
        SerializedProperty end = iterator.GetEndProperty();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
        {
            enterChildren = false;
            if (iterator.name == childName)
            {
                return iterator.Copy();
            }
        }

        return null;
    }

    private SerializedProperty FindPatternChild(string patternName, string childName)
    {
        SerializedProperty pattern = serializedObject.FindProperty(patternName);
        return pattern != null ? pattern.FindPropertyRelative(childName) : null;
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
