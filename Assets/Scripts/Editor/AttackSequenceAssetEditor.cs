using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Week14.Enemy;

[CustomEditor(typeof(AttackSequenceAsset))]
public sealed class AttackSequenceAssetEditor : Editor
{
    private SerializedProperty actions;
    private ReorderableList actionList;

    private void OnEnable()
    {
        actions = serializedObject.FindProperty("actions");
        actionList = new ReorderableList(serializedObject, actions, true, true, true, true)
        {
            drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Actions"),
            elementHeightCallback = GetActionHeight,
            drawElementCallback = DrawAction,
            onAddDropdownCallback = ShowAddActionMenu
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        actionList.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }

    private float GetActionHeight(int index)
    {
        SerializedProperty element = actions.GetArrayElementAtIndex(index);
        return EditorGUI.GetPropertyHeight(element, true) + EditorGUIUtility.standardVerticalSpacing;
    }

    private void DrawAction(Rect rect, int index, bool isActive, bool isFocused)
    {
        SerializedProperty element = actions.GetArrayElementAtIndex(index);
        rect.y += 1f;
        rect.height = EditorGUI.GetPropertyHeight(element, true);
        EditorGUI.PropertyField(rect, element, new GUIContent(GetActionLabel(element, index)), true);
    }

    private void ShowAddActionMenu(Rect buttonRect, ReorderableList list)
    {
        GenericMenu menu = new();
        menu.AddItem(new GUIContent("Wait"), false, () => AddAction(new WaitAction()));
        menu.AddItem(new GUIContent("Animation/Play Animation"), false, () => AddAction(new PlayAnimationAction()));
        menu.AddItem(new GUIContent("Animation/Wait For Event"), false, () => AddAction(new WaitForAnimationEventAction()));
        menu.AddItem(new GUIContent("Telegraph/Set Telegraph"), false, () => AddAction(new SetTelegraphAction()));
        menu.AddItem(new GUIContent("Move/Move Toward Player"), false, () => AddAction(new MoveTowardPlayerAction()));
        menu.AddItem(new GUIContent("Move/Move Body Root Local"), false, () => AddAction(new MoveBodyRootLocalAction()));
        menu.AddItem(new GUIContent("Move/Reset Body Root Local"), false, () => AddAction(new ResetBodyRootLocalAction()));
        menu.AddItem(new GUIContent("Projectile/Fire Projectile"), false, () => AddAction(new FireProjectileAction()));
        menu.AddItem(new GUIContent("Projectile/Fire Charged Radial Split Projectile"), false, () => AddAction(new FireChargedRadialSplitProjectileAction()));
        menu.AddItem(new GUIContent("Projectile/Fire Radial Projectiles"), false, () => AddAction(new FireRadialProjectilesAction()));
        menu.AddItem(new GUIContent("Projectile/Fire Rotating Projectiles"), false, () => AddAction(new FireRotatingProjectilesAction()));
        menu.AddItem(new GUIContent("Projectile/Fire Machinegun Projectiles"), false, () => AddAction(new FireMachinegunProjectilesAction()));
        menu.AddItem(new GUIContent("Projectile/Fire Sweep Projectiles"), false, () => AddAction(new FireSweepProjectilesAction()));
        menu.AddItem(new GUIContent("Projectile/Fire Fan Volley Projectiles"), false, () => AddAction(new FireFanVolleyProjectilesAction()));
        menu.AddItem(new GUIContent("Event/Custom Event"), false, () => AddAction(new CustomEventAction()));
        menu.AddItem(new GUIContent("Spawn/Spawn Prefab"), false, () => AddAction(new SpawnPrefabAction()));
        menu.DropDown(buttonRect);
    }

    private void AddAction(BossAction action)
    {
        actions.arraySize++;
        SerializedProperty element = actions.GetArrayElementAtIndex(actions.arraySize - 1);
        element.managedReferenceValue = action;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static string GetActionLabel(SerializedProperty element, int index)
    {
        object action = element.managedReferenceValue;
        if (action == null)
        {
            return $"{index + 1}. Empty Action";
        }

        return $"{index + 1}. {ObjectNames.NicifyVariableName(action.GetType().Name)}";
    }
}
