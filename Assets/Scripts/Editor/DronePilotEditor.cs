using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Week14.Enemy;

[CustomEditor(typeof(DronePilot))]
[CanEditMultipleObjects]
public sealed class DronePilotEditor : Editor
{
    private static readonly string[] Tabs =
    {
        "공통",
        "보스 공격",
        "드론 소환",
        "드론 1",
        "드론 2",
        "드론 3",
        "드론 4",
        "드론 5"
    };

    private static readonly HashSet<string> DronePilotFields = new()
    {
        "bossBurst",
        "summon",
        "dronePattern1",
        "dronePattern2",
        "dronePattern3",
        "dronePattern4",
        "dronePattern5",
        "patternSequence",
        "randomizePatterns",
        "minPatternRecoverySeconds",
        "maxPatternRecoverySeconds"
    };

    private static readonly Dictionary<string, bool> ToggleStates = new();

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
                DrawBossBurstTab();
                break;
            case 2:
                DrawSummonTab();
                break;
            case 3:
                DrawDronePattern1Tab();
                break;
            case 4:
                DrawDronePattern2Tab();
                break;
            case 5:
                DrawDronePattern3Tab();
                break;
            case 6:
                DrawDronePattern4Tab();
                break;
            case 7:
                DrawDronePattern5Tab();
                break;
            default:
                DrawCommonTab();
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawCommonTab()
    {
        DrawHeader("공통", "패턴 순서, 랜덤 실행, 패턴 사이 대기 시간과 BossAI 공통 설정을 관리합니다.");

        DrawToggleSection("패턴 선택", "common.pattern", () =>
        {
            DrawProperty("patternSequence");
            DrawProperty("randomizePatterns");
        });

        DrawToggleSection("패턴 사이 대기", "common.recovery", () =>
        {
            DrawProperty("minPatternRecoverySeconds");
            DrawProperty("maxPatternRecoverySeconds");
        });

        showBossBase = EditorGUILayout.ToggleLeft("BossAI 공통 설정 보기", showBossBase, EditorStyles.boldLabel);
        if (showBossBase)
        {
            DrawBaseProperties();
        }
    }

    private void DrawBossBurstTab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("bossBurst");
        DrawHeader("보스 공격", "정해진 간격으로 정해진 수만큼 보스가 탄환을 발사합니다.");
        DrawProjectile(pattern.FindPropertyRelative("projectile"), "보스 탄환");
        DrawChildSection("타이밍", pattern, "windupSeconds", "bulletCount", "fireInterval");
        DrawChildSection("배치", pattern, "spawnSpacing");
    }

    private void DrawSummonTab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("summon");
        DrawHeader("드론 소환", "드론 프리팹 생성과 씬 배치 드론 소유권 설정을 관리합니다.");
        DrawChildSection("소환 대상", pattern, "prefab", "claimSceneDrones");
        DrawChildSection("소환 수", pattern, "maxOwnedDrones", "summonCount");
        DrawChildSection("소환 위치", pattern, "spawnRadius", "summonInterval");
        DrawChildSection("등장 연출", pattern, "introSeconds", "introStartScale");
        DrawChildSection("자동 소환 간격", pattern, "minAutoSummonInterval", "maxAutoSummonInterval");
    }

    private void DrawDronePattern1Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("dronePattern1");
        if (pattern != null)
        {
            DrawHeader("드론 패턴 1", "보스 발사 타이밍에 맞춰 드론이 함께 쏠 전용 투사체를 설정합니다.");
            DrawProjectile(pattern.FindPropertyRelative("droneProjectile"), "드론 패턴1 전용 탄환");
            return;
        }

        bool useDedicatedPattern1Inspector = true;
        if (useDedicatedPattern1Inspector)
        {
            DrawHeader("드론 패턴 1", "드론 패턴 1에서 사용할 탄환과 발사 수를 보스와 별도로 설정합니다.");
            DrawChildSection("발사 설정", pattern, "bulletCount", "fireInterval");
            DrawProjectile(pattern.FindPropertyRelative("droneProjectile"), "드론 패턴1 전용 탄환");
            return;
        }
        DrawHeader("드론 패턴 1", "보스가 공격할 때 드론들이 같은 타이밍으로 함께 발사합니다.");
        DrawChildSection("연동", pattern, "useBossProjectile");

        SerializedProperty useBossProjectile = pattern.FindPropertyRelative("useBossProjectile");
        if (useBossProjectile != null && !useBossProjectile.boolValue)
        {
            DrawProjectile(pattern.FindPropertyRelative("droneProjectile"), "드론 전용 탄환");
        }
        else
        {
            EditorGUILayout.HelpBox("보스 공격 탭의 탄환 설정을 드론도 그대로 사용합니다.", MessageType.Info);
        }
    }

    private void DrawDronePattern2Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("dronePattern2");
        DrawHeader("드론 패턴 2", "드론 하나는 플레이어 주변을 한 바퀴 돌며 발사하고, 나머지는 제자리에서 연속 발사합니다.");
        DrawProjectile(pattern.FindPropertyRelative("orbitProjectile"), "회전 드론 탄환");
        DrawChildSection("회전 이동", pattern, "orbitRadius", "orbitSeconds", "fireAngleStepDegrees");
        DrawProjectile(pattern.FindPropertyRelative("stationaryProjectile"), "제자리 드론 탄환");
        DrawChildSection("제자리 발사", pattern, "stationaryBulletCount", "stationaryFireInterval");
    }

    private void DrawDronePattern3Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("dronePattern3");
        DrawHeader("드론 패턴 3", "모든 드론이 멈춘 뒤 플레이어 방향 기준 n방향 탄환을 발사합니다.");
        DrawProjectile(pattern.FindPropertyRelative("projectile"), "n방향 탄환");
        DrawChildSection("발사 방식", pattern, "volleyCount", "directionCount", "volleyInterval", "spreadDegrees");
    }

    private void DrawDronePattern4Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("dronePattern4");
        DrawHeader("드론 패턴 4", "드론들이 플레이어 방향에서 조금 틀어진 각도로 돌진하며 양쪽으로 탄환을 뿌립니다.");
        DrawProjectile(pattern.FindPropertyRelative("projectile"), "돌진 중 양옆 탄환");
        DrawChildSection("돌진", pattern, "chargeSeconds", "chargeSpeed", "aimOffsetDegrees");
        DrawChildSection("양옆 발사", pattern, "sideFireInterval", "sideFireAngleDegrees");
    }

    private void DrawDronePattern5Tab()
    {
        SerializedProperty pattern = serializedObject.FindProperty("dronePattern5");
        DrawHeader("드론 패턴 5", "플레이어를 보스와 드론 사이에 두는 대형을 만든 뒤 보스와 드론이 함께 발사합니다.");
        DrawProjectile(pattern.FindPropertyRelative("bossProjectile"), "패턴5 보스 탄환");
        DrawChildSection("패턴5 보스 발사", pattern, "bossBulletCount", "bossFireInterval", "bossSpawnSpacing");
        DrawProjectile(pattern.FindPropertyRelative("droneProjectile"), "패턴5 드론 탄환");
        DrawChildSection("패턴5 드론 독립 발사", pattern, "droneFireCount", "droneFireInterval");
        DrawChildSection("드론 대형", pattern, "formationRadius", "formationAngleSpacingDegrees", "settleSeconds", "formationSpeedMultiplier");
    }

    private static void DrawProjectile(SerializedProperty projectile, string title)
    {
        if (projectile == null)
        {
            return;
        }

        DrawToggleSection(title, projectile.propertyPath, () =>
        {
            DrawChildSection("기본", projectile, "prefab", "bulletDamage", "radius");
            DrawChildSection("충전", projectile, "chargeSeconds", "chargeDriftSpeed", "aimAtPlayerWhileCharging", "aimAtPlayerOnLaunch");
            DrawChildSection("이동", projectile, "speed", "lifetime", "homingSeconds", "homingTurnDegreesPerSecond");
            DrawChildSection("색/궤적", projectile, "chargingColor", "launchedColor", "trailSeconds", "trailWidthMultiplier");
        });
    }

    private static void DrawChildSection(string title, SerializedProperty root, params string[] childNames)
    {
        DrawToggleSection(title, $"{root.propertyPath}.{title}", () =>
        {
            for (int i = 0; i < childNames.Length; i++)
            {
                DrawChild(root, childNames[i]);
            }
        });
    }

    private static void DrawToggleSection(string title, string key, System.Action drawContent)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        bool expanded = GetToggleState(key, true);
        expanded = EditorGUILayout.ToggleLeft(title, expanded, EditorStyles.boldLabel);
        ToggleStates[key] = expanded;

        if (expanded)
        {
            EditorGUI.indentLevel++;
            drawContent?.Invoke();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private static bool GetToggleState(string key, bool defaultValue)
    {
        if (!ToggleStates.TryGetValue(key, out bool value))
        {
            value = defaultValue;
        }

        return value;
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
            if (property.propertyPath == "m_Script" || DronePilotFields.Contains(property.propertyPath))
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
