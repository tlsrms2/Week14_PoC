using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Week14.Enemy;

[CustomEditor(typeof(HogBossAI))]
[CanEditMultipleObjects]
public sealed class HogBossAIEditor : Editor
{
    private static readonly string[] Tabs =
    {
        "공통",
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
        "phasePatterns",
        "minPatternRecoverySeconds",
        "maxPatternRecoverySeconds",
        "randomizePatterns",
        "preventRandomRepeatPattern",
        "debugUseFixedPattern",
        "debugPattern"
    };

    private static readonly Dictionary<string, bool> FoldoutStates = new();

    private int tabIndex;
    private bool showBossBase;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawScriptField();
        tabIndex = GUILayout.Toolbar(tabIndex, Tabs);
        EditorGUILayout.Space(6f);

        switch (tabIndex)
        {
            case 1:
                DrawPattern1Tab();
                break;
            case 2:
                DrawPattern2Tab();
                break;
            case 3:
                DrawPattern3Tab();
                break;
            case 4:
                DrawPattern4Tab();
                break;
            case 5:
                DrawPattern5Tab();
                break;
            case 6:
                DrawPattern6Tab();
                break;
            case 7:
                DrawPattern7Tab();
                break;
            default:
                DrawCommonTab();
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawCommonTab()
    {
        DrawHeader("패턴 흐름", "패턴 선택 방식과 패턴 사이 대기 타이머를 조율합니다.");
        DrawProperty("randomizePatterns");
        DrawProperty("preventRandomRepeatPattern");
        DrawPhasePatterns();
        DrawProperty("minPatternRecoverySeconds");
        DrawProperty("maxPatternRecoverySeconds");

        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("디버그 패턴 고정", EditorStyles.boldLabel);
        DrawProperty("debugUseFixedPattern");
        DrawProperty("debugPattern");
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6f);
        DrawEnrageSection();

        EditorGUILayout.Space(6f);
        showBossBase = EditorGUILayout.Foldout(showBossBase, "보스 공통/참조 설정", true);
        if (showBossBase)
        {
            DrawBaseProperties();
        }
    }

    private void DrawEnrageSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        bool expanded = DrawFoldout("common.enrage", "광폭화 / 진입 연출");
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
        EditorGUILayout.LabelField("진입 전 떨림 (Windup)", EditorStyles.boldLabel);
        DrawProperty("enrageWindupSeconds");
        DrawProperty("enrageWindupShakeDistance");
        DrawProperty("enrageWindupShakeFrequency");

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("진입 연출 (스폰 이미지/스케일/카메라 쉐이크)", EditorStyles.boldLabel);
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
        EditorGUILayout.LabelField("페이즈별 패턴 구성", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("각 요소가 보스 목숨 순서의 페이즈입니다. Patterns 목록에 넣은 패턴만 해당 페이즈에서 선택됩니다. 비워두면 모든 패턴을 사용합니다.", MessageType.Info);

        SerializedProperty maxLives = serializedObject.FindProperty("maxLives");
        int desiredCount = maxLives != null ? Mathf.Max(1, maxLives.intValue) : phasePatterns.arraySize;
        if (phasePatterns.arraySize < desiredCount)
        {
            EditorGUILayout.HelpBox("페이즈 수보다 패턴 구성이 적습니다. 게임 실행 또는 값 변경 시 부족한 페이즈가 자동 추가됩니다.", MessageType.Warning);
        }

        EditorGUILayout.PropertyField(phasePatterns, true);
        EditorGUILayout.EndVertical();
    }

    private void DrawPattern1Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("pattern1");
        DrawSection("Origins", pattern, "projectileOrigins");
        DrawHeader("패턴1", "일정 각도씩 회전하며 일반 탄환을 만들고, 대기 시간이 끝나면 플레이어를 향해 날아갑니다.");
        DrawProjectile(pattern.FindPropertyRelative("projectile"), "패턴1 투사체");
        DrawSection("추격", pattern, "initialChaseSpeedMultiplier", "finalChaseSpeedMultiplier");
        DrawSection("발사", pattern, "radialBulletCount", "burstInterval", "spawnRadius", "angleStepDegrees");
        DrawSection("이펙트", pattern, "effects");
    }

    private void DrawPattern2Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("pattern2");
        DrawSection("Origins", pattern, "projectileOrigins");
        DrawHeader("패턴2", "느려진 상태로 플레이어를 향해 특수 탄환을 머신건처럼 발사합니다.");
        DrawProjectile(pattern.FindPropertyRelative("projectile"), "패턴2 투사체");
        DrawSection("이동", pattern, "moveSpeedMultiplier");
        DrawSection("발사 묶음", pattern, "volleys", "spawnSpacing");
        DrawSection("이펙트", pattern, "effects");
    }

    private void DrawPattern3Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("pattern3");
        DrawSection("Origins", pattern, "firePoint");
        DrawHeader("패턴3", "보스에게 붙어 커지며 조준하다가, 멈춘 뒤 대기하고 날아갑니다. 첫 분열 전까지 요격 불가입니다.");
        DrawProjectile(pattern.FindPropertyRelative("projectile"), "패턴3 투사체");
        DrawSection("준비/조준", pattern, "windupSeconds", "aimTrackingSeconds", "aimSpreadDegrees");
        DrawSection("크기", pattern, "projectileRadiusMultiplier", "finalScaleMultiplier", "startScaleRatio");
        DrawSection("전방위 분열", pattern, "splitDelaySeconds", "bombSfxLeadSeconds", "radialSplitBulletCount", "radialSplitStartAngleOffset", "splitSpeedMultiplier", "splitRadiusMultiplier", "splitLifetimeMultiplier");
        DrawSection("이펙트", pattern, "effects");
    }

    private void DrawPattern4Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("pattern4");
        DrawSection("Origins / Slam", pattern, "projectileOrigin", "slamUpOffset", "slamDownOffset", "slamRiseSeconds", "slamDropSeconds", "slamRecoverSeconds");
        DrawHeader("패턴4", "보스 위치를 기준으로 360도 원형의 파동처럼 여러 차례 탄환을 발사합니다.");
        DrawProjectile(pattern.FindPropertyRelative("projectile"), "패턴4 투사체");
        DrawSection("전방위 발사", pattern, "bulletCount", "waveCount", "waveInterval", "startAngleOffset", "spawnRadius");
        DrawSection("이펙트", pattern, "effects");
    }

    private void DrawPattern5Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("pattern5");
        DrawSection("Origins", pattern, "firePoint");
        DrawHeader("패턴5", "제자리에서 기를 모은 뒤 플레이어 방향을 기준으로 부채꼴 모양으로 훑으며 탄막을 발사합니다.");
        DrawProjectile(pattern.FindPropertyRelative("projectile"), "패턴5 투사체");
        DrawSection("기 모으기", pattern, "windupSeconds");
        DrawSection("미니건 발사", pattern, "bulletCount", "fireInterval", "spawnSpacing", "sweepStepDegrees", "maxSweepAngle");
        DrawSection("이펙트", pattern, "effects");
    }

    private void DrawPattern6Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("pattern6");
        DrawSection("Origins / Slam", pattern, "projectileOrigin", "slamUpOffset", "slamDownOffset", "slamRiseSeconds", "slamDropSeconds", "slamRecoverSeconds");
        DrawHeader("패턴6", "패턴4와 동일하게 보스 위치를 기준으로 360도 원형의 파동처럼 여러 차례 탄환을 발사합니다.");
        DrawProjectile(pattern.FindPropertyRelative("projectile"), "패턴6 투사체");
        DrawSection("전방위 발사", pattern, "bulletCount", "waveCount", "waveInterval", "startAngleOffset", "spawnRadius");
        DrawSection("이펙트", pattern, "effects");
    }

    private void DrawPattern7Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("pattern7");
        DrawSection("Origins", pattern, "firePoint", "specialProjectileOrigins");
        DrawHeader("패턴7", "발사 직전 플레이어 방향으로 조준을 고정한 뒤 전방 세 갈래 일반 탄환을 여러 번 쏘고 특수 탄환을 함께 소환합니다.");
        DrawProjectile(pattern.FindPropertyRelative("normalProjectile"), "패턴7 일반 탄환");
        DrawProjectile(pattern.FindPropertyRelative("specialProjectile"), "패턴7 특수 탄환");
        DrawSection("기 모으기", pattern, "windupSeconds");
        DrawSection("세 갈래 반복 발사", pattern, "normalVolleyCount", "normalVolleyInterval", "fanAngleDegrees", "normalSpawnSpacing", "specialSpawnForwardOffset");
        DrawSection("특수 탄환", pattern, "specialBulletCount");
        DrawSection("이펙트", pattern, "effects");
    }

    private void DrawProjectile(SerializedProperty projectile, string title)
    {
        if (projectile == null)
        {
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        string key = $"{projectile.propertyPath}.{title}";
        bool expanded = DrawFoldout(key, title);
        if (!expanded)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        DrawChild(projectile, "prefab");
        DrawChild(projectile, "bulletDamage");
        DrawChild(projectile, "chargeSeconds");
        DrawChild(projectile, "chargeDriftSpeed");
        DrawChild(projectile, "speed");
        DrawChild(projectile, "lifetime");
        DrawChild(projectile, "radius");
        DrawChild(projectile, "chargingColor");
        DrawChild(projectile, "launchedColor");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        bool advancedExpanded = DrawFoldout($"{key}.advanced", "투사체 고급값");
        if (advancedExpanded)
        {
            DrawChild(projectile, "aimAtPlayerWhileCharging");
            DrawChild(projectile, "trailSeconds");
            DrawChild(projectile, "trailWidthMultiplier");
            DrawChild(projectile, "homingEnabled");
            DrawChild(projectile, "homingSeconds");
            DrawChild(projectile, "homingTurnDegreesPerSecond");
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndVertical();
    }

    private static void DrawSection(string title, SerializedProperty root, params string[] childNames)
    {
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
        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child != null)
        {
            EditorGUILayout.PropertyField(child, true);
        }
    }

    private void DrawProperty(string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, true);
        }
    }

    private void DrawBaseProperties()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.propertyPath == "m_Script" || HogFields.Contains(property.propertyPath))
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

    private static void DrawHeader(string title, string description)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(description, MessageType.None);
        EditorGUILayout.EndVertical();
    }
}
