#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Week14.Weapons;

[CustomEditor(typeof(BaseballBatWeaponSO))]
[CanEditMultipleObjects]
public sealed class BaseballBatWeaponSOEditor : Editor
{
    private static readonly string[] TabLabels = { "기본", "공격", "이펙트", "타이밍" };

    private int selectedTab;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        }

        EditorGUILayout.Space(4f);
        selectedTab = GUILayout.Toolbar(selectedTab, TabLabels);
        EditorGUILayout.Space(8f);

        switch (selectedTab)
        {
            case 0:
                DrawBasicTab();
                break;
            case 1:
                DrawAttackTab();
                break;
            case 2:
                DrawVfxTab();
                break;
            case 3:
                DrawTimingTab();
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawBasicTab()
    {
        DrawProperties(
            "weaponId",
            "displayName",
            "localizedDisplayName",
            "icon",
            "outlineIcon",
            "description",
            "localizedDescription",
            "price",
            "inGameSprite",
            "projectilePrefab",
            "leftArmController",
            "maxAmmo",
            "parryingRange",
            "damagePerAmmoStep",
            "maxAmmoTooltipTextFormat",
            "localizedMaxAmmoTooltipText",
            "parryingRangeTooltipTextFormat",
            "localizedParryingRangeTooltipText",
            "bulletDamageTooltipTextFormat",
            "localizedBulletDamageTooltipText");
    }

    private void DrawAttackTab()
    {
        DrawProperties(
            "attackCooldownSeconds",
            "minAttackRange",
            "maxAttackRange",
            "maxChargeSeconds",
            "reflectedDamage",
            "reflectedProjectileSpeed",
            "moveSpeedMultiplier");

    }

    private void DrawVfxTab()
    {
        SerializedProperty settings = serializedObject.FindProperty("vfxSettings");
        EditorGUILayout.LabelField("차징 단계별 프리팹", EditorStyles.boldLabel);
        DrawRelative(settings, "swingVfxPrefab");
        DrawRelative(settings, "halfChargeSwingVfxPrefab");
        DrawRelative(settings, "fullChargeSwingVfxPrefab");
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("오른쪽 공격 기준 Transform", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            DrawRelative(settings, "rightFacingLocalOffset");
        }
        DrawRelative(settings, "maxRightFacingXOffset");
        DrawRelative(settings, "rotationOffsetDegrees");
        DrawRelative(settings, "localScale");

        SerializedProperty scaleWithRange = settings.FindPropertyRelative("scaleWithAttackRange");
        EditorGUILayout.PropertyField(scaleWithRange);
        if (scaleWithRange.hasMultipleDifferentValues || scaleWithRange.boolValue)
        {
            DrawRelative(settings, "referenceAttackRange");
        }

        DrawRelative(settings, "sortingOrder");
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("실제 범위 표시", EditorStyles.boldLabel);
        DrawRelative(settings, "rangeIndicatorColor");
        DrawRelative(settings, "previewRangeColor");
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("배트 표시 스프라이트(대기/차징/스윙)", EditorStyles.boldLabel);
        DrawRelative(settings, "displayOffsetDistance");
        DrawRelative(settings, "displayWindUpDegrees");
        DrawRelative(settings, "windUpSnapRatio");
        DrawRelative(settings, "windUpSnapSeconds");
        DrawRelative(settings, "swingHoldSeconds");
        DrawRelative(settings, "swingReturnSeconds");
        DrawRelative(settings, "displaySpriteRotationOffsetDegrees");
        DrawRelative(settings, "displayScale");
        DrawRelative(settings, "maxChargeScale");
        DrawRelative(settings, "displaySortingOrder");
        DrawRelative(settings, "displayColorDefault");
        DrawRelative(settings, "displayColorHalfCharge");
        DrawRelative(settings, "displayColorFullCharge");
    }

    private void DrawTimingTab()
    {
        SerializedProperty settings = serializedObject.FindProperty("vfxSettings");
        DrawRelative(settings, "playbackSpeed");
        DrawRelative(settings, "attackHitDelaySeconds");
        DrawRelative(settings, "attackActiveSeconds");
        DrawRelative(settings, "rangeIndicatorSeconds");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("효과음 타이밍", EditorStyles.boldLabel);
        DrawProperties("chargingSfxStartSeconds");

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "Attack Hit Delay Seconds가 지난 뒤 Attack Active Seconds 동안 매 프레임 반사 판정을 수행합니다. " +
            "애니메이션 배속을 바꾸면 두 시간을 함께 조정해 실제 스윙 구간에 맞추세요.\n" +
            "Charging Sfx Start Seconds가 지나면 차징음이 한 번 재생되며, 공격 버튼을 놓거나 차징이 취소되면 즉시 정지합니다.",
            MessageType.Info);
    }

    private void DrawProperties(params string[] propertyNames)
    {
        for (int i = 0; i < propertyNames.Length; i++)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyNames[i]);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, true);
            }
        }
    }

    private static void DrawRelative(SerializedProperty parent, string propertyName)
    {
        if (parent == null)
        {
            return;
        }

        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, true);
        }
    }
}
#endif
