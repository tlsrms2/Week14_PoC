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
        "기본",
        "패턴1",
        "패턴2",
        "패턴3",
        "패턴4"
    };

    private static readonly HashSet<string> HogFields = new()
    {
        "basicAttack",
        "pattern1",
        "pattern2",
        "pattern3",
        "pattern4",
        "minPatternRecoverySeconds",
        "maxPatternRecoverySeconds",
        "randomizePatterns",
        "hogEffectColor",
        "bubbleEffectScale"
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
                DrawBasicAttackTab();
                break;
            case 2:
                DrawPattern1Tab();
                break;
            case 3:
                DrawPattern2Tab();
                break;
            case 4:
                DrawPattern3Tab();
                break;
            case 5:
                DrawPattern4Tab();
                break;
            default:
                DrawCommonTab();
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawCommonTab()
    {
        DrawHeader("패턴 흐름", "패턴 선택 방식, 패턴 사이 일반공격, 대기 타이머와 보글 이펙트를 조율합니다.");
        DrawProperty("randomizePatterns");
        DrawProperty("minPatternRecoverySeconds");
        DrawProperty("maxPatternRecoverySeconds");
        DrawProperty("hogEffectColor");
        DrawProperty("bubbleEffectScale");

        EditorGUILayout.Space(6f);
        showBossBase = EditorGUILayout.Foldout(showBossBase, "보스 공통/참조 설정", true);
        if (showBossBase)
        {
            DrawBaseProperties();
        }
    }

    private void DrawBasicAttackTab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("basicAttack");
        DrawHeader("기본공격", "다른 패턴과 같은 로테이션에 포함되는 일반공격입니다.");
        DrawProjectile(pattern.FindPropertyRelative("projectile"), "기본공격 투사체");
        DrawSection("공격", pattern, "duration", "firstShotDelay", "shotInterval", "bulletCount", "moveSpeedMultiplier");
    }

    private void DrawPattern1Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("pattern1");
        DrawHeader("패턴1", "사방 랜덤 방향으로 일반 탄환을 만들고, 대기 시간이 끝나면 플레이어를 향해 날아갑니다.");
        DrawProjectile(pattern.FindPropertyRelative("projectile"), "패턴1 투사체");
        DrawSection("추격", pattern, "duration", "initialChaseSpeedMultiplier", "finalChaseSpeedMultiplier");
        DrawSection("발사", pattern, "radialBulletCount", "burstInterval", "finalBurstIntervalMultiplier", "spawnRadius");
    }

    private void DrawPattern2Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("pattern2");
        DrawHeader("패턴2", "느려진 상태로 플레이어를 향해 특수 탄환을 머신건처럼 발사합니다.");
        DrawProjectile(pattern.FindPropertyRelative("projectile"), "패턴2 투사체");
        DrawSection("이동", pattern, "moveSpeedMultiplier");
        DrawSection("발사 묶음", pattern, "volleys", "spawnSpacing");
    }

    private void DrawPattern3Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("pattern3");
        DrawHeader("패턴3", "보스에게 붙어 커진 뒤 플레이어 근처 방향으로 날아가고, 첫 분열 전까지 요격 불가입니다.");
        DrawProjectile(pattern.FindPropertyRelative("projectile"), "패턴3 투사체");
        DrawSection("준비/조준", pattern, "windupSeconds", "aimSpreadDegrees");
        DrawSection("크기", pattern, "projectileRadiusMultiplier", "finalScaleMultiplier", "startScaleRatio", "launchBubbleScale");
        DrawSection("전방위 분열", pattern, "splitDelaySeconds", "radialSplitBulletCount", "radialSplitStartAngleOffset", "splitSpeedMultiplier", "splitRadiusMultiplier", "splitLifetimeMultiplier");
    }

    private void DrawPattern4Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("pattern4");
        DrawHeader("패턴4", "전방위 방향을 섞은 뒤 무작위 순서로 하나씩 특수 탄환을 생성합니다.");
        DrawProjectile(pattern.FindPropertyRelative("projectile"), "패턴4 투사체");
        DrawSection("전방위 발사", pattern, "bulletCount", "waveCount", "waveInterval", "shotInterval", "startAngleOffset", "spawnRadius");
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
