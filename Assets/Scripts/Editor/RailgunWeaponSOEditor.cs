#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Week14.Weapons;

[CustomEditor(typeof(RailgunWeaponSO))]
[CanEditMultipleObjects]
public sealed class RailgunWeaponSOEditor : Editor
{
    private static readonly string[] TabLabels = { "기본", "공격", "이펙트" };

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
            "laserSpeed",
            "laserLifetimeSeconds",
            "beamWidth");

        SerializedProperty speed = serializedObject.FindProperty("laserSpeed");
        SerializedProperty lifetime = serializedObject.FindProperty("laserLifetimeSeconds");
        if (speed != null
            && lifetime != null
            && !speed.hasMultipleDifferentValues
            && !lifetime.hasMultipleDifferentValues)
        {
            float attackRange = Mathf.Max(0f, speed.floatValue * lifetime.floatValue);
            EditorGUILayout.HelpBox($"실제 레이저 사거리: {attackRange:0.###}", MessageType.Info);
        }
    }

    private void DrawVfxTab()
    {
        SerializedProperty settings = serializedObject.FindProperty("vfxSettings");
        EditorGUILayout.LabelField("탄 소모량별 프리팹", EditorStyles.boldLabel);
        DrawRelative(settings, "oneToTwoAmmoBeamPrefab");
        DrawRelative(settings, "threeToFourAmmoBeamPrefab");
        DrawRelative(settings, "fiveAmmoBeamPrefab");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("총구 섬광", EditorStyles.boldLabel);
        DrawRelative(settings, "muzzleFlashPrefab");
        DrawRelative(settings, "fiveAmmoMuzzleFlashPrefab");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("총구 정렬 및 재생", EditorStyles.boldLabel);
        DrawRelative(settings, "beamLengthAxis");
        DrawRelative(settings, "muzzleOffset");
        DrawRelative(settings, "rotationOffsetDegrees");
        DrawRelative(settings, "playbackSpeed");
        DrawRelative(settings, "sortingOrder");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("프리팹 미지정 시 대체 LineRenderer", EditorStyles.boldLabel);
        DrawProperties("beamVisualSeconds", "beamColor");
        EditorGUILayout.HelpBox(
            "프리팹 렌더러의 원본 길이를 자동 측정해 실제 사거리까지 길이축만 확대합니다. " +
            "현재 1~2발/3~4발/5발 프리팹은 Beam Length Axis를 Local Y로 두세요. 프리팹 자체의 가로 두께와 구성은 유지됩니다.",
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
