using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Week14.Audio;
using Week14.Enemy;

[CustomEditor(typeof(BossGraphActionAsset), true)]
public sealed class BossGraphActionAssetEditor : Editor
{
    private SerializedProperty actions;

    private void OnEnable()
    {
        actions = serializedObject.FindProperty("actions");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        TrimToSingleAction();
        DrawSingleAction();
        serializedObject.ApplyModifiedProperties();
    }

    private void TrimToSingleAction()
    {
        if (actions == null || actions.arraySize <= 1)
        {
            return;
        }

        Undo.RecordObject(target, "Trim Boss Graph Action");
        while (actions.arraySize > 1)
        {
            actions.DeleteArrayElementAtIndex(actions.arraySize - 1);
        }

        EditorUtility.SetDirty(target);
    }

    private void DrawSingleAction()
    {
        if (actions == null || actions.arraySize == 0)
        {
            return;
        }

        SerializedProperty action = actions.GetArrayElementAtIndex(0);
        EditorGUILayout.PropertyField(action, new GUIContent(GetActionLabel(action)), true);
    }

    private static string GetActionLabel(SerializedProperty action)
    {
        object value = action.managedReferenceValue;
        return value == null
            ? "Action"
            : ObjectNames.NicifyVariableName(value.GetType().Name);
    }
}

internal readonly struct BossGraphActionMenuItem
{
    public BossGraphActionMenuItem(string menuPath, Type actionType, Func<BossAction> create)
    {
        MenuPath = menuPath;
        ActionType = actionType;
        Create = create;
    }

    public string MenuPath { get; }
    public Type ActionType { get; }
    public Func<BossAction> Create { get; }
}

internal static class BossGraphActionEditorUtility
{
    public static readonly IReadOnlyList<BossGraphActionMenuItem> ActionMenuItems = new List<BossGraphActionMenuItem>
    {
        new("Utility/Wait", typeof(WaitAction), () => new WaitAction()),
        new("Utility/Windup", typeof(WindupAction), () => new WindupAction()),
        new("Animation/Play Animation", typeof(PlayAnimationAction), () => new PlayAnimationAction()),
        new("Animation/Wait For Event", typeof(WaitForAnimationEventAction), () => new WaitForAnimationEventAction()),
        new("Move/Move Toward Player", typeof(MoveTowardPlayerAction), () => new MoveTowardPlayerAction()),
        new("Move/Start Move Toward Player", typeof(StartMoveTowardPlayerAction), () => new StartMoveTowardPlayerAction()),
        new("Move/Stop Movement", typeof(StopMovementAction), () => new StopMovementAction()),
        new("Move/Move Body Root Local", typeof(MoveBodyRootLocalAction), () => new MoveBodyRootLocalAction()),
        new("Move/Reset Body Root Local", typeof(ResetBodyRootLocalAction), () => new ResetBodyRootLocalAction()),
        new("Projectile/Fire Projectile", typeof(FireProjectileAction), () => new FireProjectileAction()),
        new("Projectile/Spawn Charged Projectile", typeof(SpawnChargedProjectileAction), () => new SpawnChargedProjectileAction()),
        new("Projectile/Configure Projectile Growth", typeof(ConfigureProjectileGrowthAction), () => new ConfigureProjectileGrowthAction()),
        new("Projectile/Configure Radial Split", typeof(ConfigureRadialSplitAction), () => new ConfigureRadialSplitAction()),
        new("Projectile/Wait Projectile Charge End", typeof(WaitProjectileChargeEndAction), () => new WaitProjectileChargeEndAction()),
        new("Projectile/Fire Projectile Burst", typeof(FireProjectileBurstAction), () => new FireProjectileBurstAction()),
        new("Projectile/Fire Radial Emission", typeof(FireRadialEmissionAction), () => new FireRadialEmissionAction()),
        new("Projectile/Fire Sweep Emission", typeof(FireSweepEmissionAction), () => new FireSweepEmissionAction()),
        new("Projectile/Fire Fan Emission", typeof(FireFanEmissionAction), () => new FireFanEmissionAction()),
        new("Utility/Aim Boss Child At Player", typeof(AimBossChildAtPlayerAction), () => new AimBossChildAtPlayerAction()),
        new("Event/Custom Event", typeof(CustomEventAction), () => new CustomEventAction()),
        new("Spawn/Spawn Prefab", typeof(SpawnPrefabAction), () => new SpawnPrefabAction())
    };

    public static string GetActionLabel(Type actionType)
    {
        if (actionType == null)
        {
            return "Unknown Action";
        }

        for (int i = 0; i < ActionMenuItems.Count; i++)
        {
            BossGraphActionMenuItem item = ActionMenuItems[i];
            if (item.ActionType == actionType)
            {
                return item.MenuPath;
            }
        }

        return ObjectNames.NicifyVariableName(actionType.Name);
    }
}

internal static class BossGraphActionFilterContext
{
    public static bool HasFilter { get; private set; }
    public static BossGraphNodeKind NodeKind { get; private set; }
    public static BossGraphActionCategoryAsset Categories { get; private set; }

    public static void Set(BossGraphNodeKind nodeKind, BossGraphActionCategoryAsset categories)
    {
        HasFilter = true;
        NodeKind = nodeKind;
        Categories = categories;
    }

    public static void Clear()
    {
        HasFilter = false;
        Categories = null;
    }

    public static bool IsAllowed(Type actionType)
    {
        if (!HasFilter)
        {
            return true;
        }

        BossGraphNodeKind actionNodeKind = Categories != null
            ? Categories.GetNodeKind(actionType)
            : BossGraphActionCategoryAsset.GetDefaultNodeKind(actionType);
        return actionNodeKind == NodeKind;
    }
}

internal static class BossGraphAimStartNodeOptions
{
    private static readonly List<string> startNodeIds = new();
    private static bool hasContext;

    public static IReadOnlyList<string> StartNodeIds => startNodeIds;

    public static void Set(SerializedObject graphObject)
    {
        startNodeIds.Clear();
        hasContext = graphObject != null;
        if (graphObject == null)
        {
            return;
        }

        SerializedProperty stateNodes = graphObject.FindProperty("stateNodes");
        if (stateNodes == null)
        {
            return;
        }

        for (int i = 0; i < stateNodes.arraySize; i++)
        {
            SerializedProperty node = stateNodes.GetArrayElementAtIndex(i);
            string nodeId = node.FindPropertyRelative("nodeId")?.stringValue;
            if (string.IsNullOrWhiteSpace(nodeId) || !IsAimStartNode(node))
            {
                continue;
            }

            if (!startNodeIds.Contains(nodeId))
            {
                startNodeIds.Add(nodeId);
            }
        }
    }

    public static void Clear()
    {
        startNodeIds.Clear();
        hasContext = false;
    }

    public static bool ContainsStartNode(string nodeId)
    {
        return string.IsNullOrWhiteSpace(nodeId)
            || !hasContext
            || startNodeIds.Contains(nodeId);
    }

    private static bool IsAimStartNode(SerializedProperty node)
    {
        SerializedProperty sequences = node.FindPropertyRelative("sequences");
        if (sequences == null || sequences.arraySize == 0)
        {
            return false;
        }

        BossGraphActionAsset actionAsset = sequences.GetArrayElementAtIndex(0)
            .FindPropertyRelative("sequence")?.objectReferenceValue as BossGraphActionAsset;
        BossAction action = actionAsset?.Action;
        return action is AimBossChildAtPlayerAction aimAction
            && aimAction.Mode == BossChildAimActionMode.Start;
    }
}

internal static class BossGraphProjectileNameOptions
{
    private static readonly List<string> names = new();

    public static IReadOnlyList<string> Names => names;

    public static void Set(IEnumerable<string> projectileNames)
    {
        names.Clear();
        if (projectileNames == null)
        {
            return;
        }

        foreach (string projectileName in projectileNames)
        {
            if (!string.IsNullOrWhiteSpace(projectileName) && !names.Contains(projectileName))
            {
                names.Add(projectileName);
            }
        }
    }

    public static void Clear()
    {
        names.Clear();
    }
}

[CustomPropertyDrawer(typeof(AimBossChildAtPlayerAction))]
internal sealed class AimBossChildAtPlayerActionDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        AddHeight(ref height, property.FindPropertyRelative("mode"));

        if (IsEndMode(property.FindPropertyRelative("mode")))
        {
            AddHeight(ref height, property.FindPropertyRelative("startNodeId"));
            AddHeight(ref height, property.FindPropertyRelative("targetPath"));
            AddHeight(ref height, property.FindPropertyRelative("activateOnStart"));
            AddHeight(ref height, property.FindPropertyRelative("flipYByFacing"));
            AddHeight(ref height, property.FindPropertyRelative("deactivateOnEnd"));
            AddHeight(ref height, property.FindPropertyRelative("deactivateOnPatternEnd"));
            return height;
        }

        AddHeight(ref height, property.FindPropertyRelative("targetPath"));
        AddHeight(ref height, property.FindPropertyRelative("activateOnStart"));
        AddHeight(ref height, property.FindPropertyRelative("flipYByFacing"));
        AddHeight(ref height, property.FindPropertyRelative("deactivateOnPatternEnd"));
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect lineRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.LabelField(lineRect, label, EditorStyles.boldLabel);

        EditorGUI.indentLevel++;
        lineRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        SerializedProperty mode = property.FindPropertyRelative("mode");
        DrawProperty(ref lineRect, mode, new GUIContent("Mode"), false);

        bool isEndMode = IsEndMode(mode);
        if (isEndMode)
        {
            DrawProperty(ref lineRect, property.FindPropertyRelative("startNodeId"), new GUIContent("Start Node"), false);
            DrawProperty(ref lineRect, property.FindPropertyRelative("targetPath"), new GUIContent("Target Path"), true);
            DrawProperty(ref lineRect, property.FindPropertyRelative("activateOnStart"), new GUIContent("Activate On Start"), true);
            DrawProperty(ref lineRect, property.FindPropertyRelative("flipYByFacing"), new GUIContent("Flip Y By Facing"), true);
            DrawProperty(ref lineRect, property.FindPropertyRelative("deactivateOnEnd"), new GUIContent("Deactivate On End"), true);
            DrawProperty(ref lineRect, property.FindPropertyRelative("deactivateOnPatternEnd"), new GUIContent("Deactivate On Pattern End"), true);
        }
        else
        {
            DrawProperty(ref lineRect, property.FindPropertyRelative("targetPath"), new GUIContent("Target Path"), false);
            DrawProperty(ref lineRect, property.FindPropertyRelative("activateOnStart"), new GUIContent("Activate On Start"), false);
            DrawProperty(ref lineRect, property.FindPropertyRelative("flipYByFacing"), new GUIContent("Flip Y By Facing"), false);
            DrawProperty(ref lineRect, property.FindPropertyRelative("deactivateOnPatternEnd"), new GUIContent("Deactivate On Pattern End"), false);
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    private static void AddHeight(ref float height, SerializedProperty property)
    {
        if (property == null)
        {
            return;
        }

        height += EditorGUI.GetPropertyHeight(property, true) + EditorGUIUtility.standardVerticalSpacing;
    }

    private static void DrawProperty(ref Rect lineRect, SerializedProperty property, GUIContent label, bool disabled)
    {
        if (property == null)
        {
            return;
        }

        lineRect.height = EditorGUI.GetPropertyHeight(property, true);
        using (new EditorGUI.DisabledScope(disabled))
        {
            EditorGUI.PropertyField(lineRect, property, label, true);
        }

        lineRect.y += lineRect.height + EditorGUIUtility.standardVerticalSpacing;
    }

    private static bool IsEndMode(SerializedProperty mode)
    {
        if (mode == null)
        {
            return false;
        }

        int endNameIndex = Array.IndexOf(mode.enumNames, nameof(BossChildAimActionMode.End));
        return mode.intValue == (int)BossChildAimActionMode.End || mode.enumValueIndex == endNameIndex;
    }
}

[CustomPropertyDrawer(typeof(BossGraphNodeIdAttribute))]
internal sealed class BossGraphNodeIdDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        IReadOnlyList<string> startNodeIds = BossGraphAimStartNodeOptions.StartNodeIds;
        List<string> values = new() { string.Empty };
        List<GUIContent> labels = new()
        {
            new(startNodeIds.Count == 0 ? "<Start 노드 없음>" : "<선택>")
        };

        for (int i = 0; i < startNodeIds.Count; i++)
        {
            values.Add(startNodeIds[i]);
            labels.Add(new GUIContent(startNodeIds[i]));
        }

        if (!string.IsNullOrWhiteSpace(property.stringValue) && !values.Contains(property.stringValue))
        {
            values.Add(property.stringValue);
            labels.Add(new GUIContent($"{property.stringValue} (Start 노드 아님)"));
        }

        int currentIndex = Mathf.Max(0, values.IndexOf(property.stringValue));
        int nextIndex = EditorGUI.Popup(position, label, currentIndex, labels.ToArray());
        if (nextIndex >= 0 && nextIndex < values.Count)
        {
            property.stringValue = values[nextIndex];
        }

        EditorGUI.EndProperty();
    }
}

[CustomPropertyDrawer(typeof(BossGraphProjectileNameAttribute))]
internal sealed class BossGraphProjectileNameDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        IReadOnlyList<string> names = BossGraphProjectileNameOptions.Names;
        List<string> values = new() { string.Empty };
        List<GUIContent> labels = new() { new GUIContent("<기본>") };
        for (int i = 0; i < names.Count; i++)
        {
            values.Add(names[i]);
            labels.Add(new GUIContent(names[i]));
        }

        if (!string.IsNullOrWhiteSpace(property.stringValue) && !values.Contains(property.stringValue))
        {
            values.Add(property.stringValue);
            labels.Add(new GUIContent($"{property.stringValue} (목록 없음)"));
        }

        int currentIndex = Mathf.Max(0, values.IndexOf(property.stringValue));
        int nextIndex = EditorGUI.Popup(position, label, currentIndex, labels.ToArray());
        property.stringValue = values[nextIndex];
    }
}

internal static class BossGraphSfxIdOptions
{
    private const double RefreshIntervalSeconds = 1d;

    private static readonly List<string> ids = new();
    private static double nextRefreshAt;

    public static IReadOnlyList<string> Ids
    {
        get
        {
            RefreshIfNeeded();
            return ids;
        }
    }

    private static void RefreshIfNeeded()
    {
        if (EditorApplication.timeSinceStartup < nextRefreshAt)
        {
            return;
        }

        nextRefreshAt = EditorApplication.timeSinceStartup + RefreshIntervalSeconds;
        ids.Clear();

        string[] guids = AssetDatabase.FindAssets("t:SoundLibrary");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(path);
            if (library == null)
            {
                continue;
            }

            foreach (string id in library.SfxIds)
            {
                if (!string.IsNullOrWhiteSpace(id) && !ids.Contains(id))
                {
                    ids.Add(id);
                }
            }
        }

        ids.Sort(StringComparer.OrdinalIgnoreCase);
    }
}

[CustomPropertyDrawer(typeof(BossGraphBossChildPathAttribute))]
internal sealed class BossGraphBossChildPathDrawer : PropertyDrawer
{
    private const float ClearButtonWidth = 22f;
    private const float DeleteButtonWidth = 24f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        if (property.propertyType == SerializedPropertyType.String)
        {
            return lineHeight;
        }

        if (IsStringList(property))
        {
            if (!property.isExpanded)
            {
                return lineHeight;
            }

            return lineHeight + spacing + property.arraySize * (lineHeight + spacing) + lineHeight;
        }

        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType == SerializedPropertyType.String)
        {
            EditorGUI.BeginProperty(position, label, property);
            DrawPathField(position, property, label, null);
            EditorGUI.EndProperty();
            return;
        }

        if (IsStringList(property))
        {
            DrawPathList(position, property, label);
            return;
        }

        EditorGUI.PropertyField(position, property, label, true);
    }

    private static void DrawPathList(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        Rect lineRect = new(position.x, position.y, position.width, lineHeight);
        property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);
        if (TryGetDroppedPath(lineRect, out string headerPath, true))
        {
            AddPath(property, headerPath);
        }

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            for (int i = 0; i < property.arraySize; i++)
            {
                lineRect.y += lineHeight + spacing;
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                DrawPathField(lineRect, element, new GUIContent($"Element {i}"), () => property.DeleteArrayElementAtIndex(i));
            }

            lineRect.y += lineHeight + spacing;
            Rect dropRect = EditorGUI.IndentedRect(lineRect);
            GUI.Box(dropRect, "Drop Hierarchy Item To Add", EditorStyles.helpBox);
            if (TryGetDroppedPath(dropRect, out string path, true))
            {
                AddPath(property, path);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    private static void DrawPathField(Rect position, SerializedProperty property, GUIContent label, Action onDelete)
    {
        Rect fieldRect = position;
        float buttonWidth = onDelete != null ? DeleteButtonWidth : ClearButtonWidth;
        fieldRect.width -= buttonWidth + 2f;
        Rect buttonRect = position;
        buttonRect.xMin = fieldRect.xMax + 2f;

        Rect valueRect = EditorGUI.PrefixLabel(fieldRect, label);
        string value = property.stringValue;
        string displayValue = string.IsNullOrWhiteSpace(value) ? "<하이어러키에서 드롭>" : value;
        if (!string.IsNullOrWhiteSpace(value) && !BossGraphBossHierarchyOptions.ContainsPath(value))
        {
            displayValue = $"{value} (없음)";
        }

        GUI.Box(valueRect, displayValue, EditorStyles.textField);
        if (GUI.Button(buttonRect, onDelete != null ? "-" : "X"))
        {
            if (onDelete != null)
            {
                onDelete();
            }
            else
            {
                property.stringValue = string.Empty;
            }
        }

        if (TryGetDroppedPath(valueRect, out string path, true))
        {
            property.stringValue = path;
        }
    }

    private static bool TryGetDroppedPath(Rect dropRect, out string path, bool acceptOnPerform)
    {
        path = string.Empty;
        Event currentEvent = Event.current;
        if (!dropRect.Contains(currentEvent.mousePosition))
        {
            return false;
        }

        object data = DragAndDrop.GetGenericData(BossGraphDragKeys.BossChildPath);
        if (data is not string droppedPath || string.IsNullOrWhiteSpace(droppedPath))
        {
            return false;
        }

        if (currentEvent.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            currentEvent.Use();
            return false;
        }

        if (currentEvent.type != EventType.DragPerform)
        {
            return false;
        }

        if (acceptOnPerform)
        {
            DragAndDrop.AcceptDrag();
        }

        path = droppedPath;
        currentEvent.Use();
        return true;
    }

    private static void AddPath(SerializedProperty property, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        for (int i = 0; i < property.arraySize; i++)
        {
            if (property.GetArrayElementAtIndex(i).stringValue == path)
            {
                return;
            }
        }

        int index = property.arraySize;
        property.InsertArrayElementAtIndex(index);
        property.GetArrayElementAtIndex(index).stringValue = path;
    }

    private static bool IsStringList(SerializedProperty property)
    {
        if (!property.isArray || property.propertyType == SerializedPropertyType.String)
        {
            return false;
        }

        return property.arraySize == 0 || property.GetArrayElementAtIndex(0).propertyType == SerializedPropertyType.String;
    }
}

internal static class BossGraphBossHierarchyOptions
{
    private static Transform root;

    public static void Set(Transform bossRoot)
    {
        root = bossRoot;
    }

    public static void Clear()
    {
        root = null;
    }

    public static bool ContainsPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        return root == null || root.Find(path) != null || FindChildRecursive(root, path) != null;
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, name);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}

[CustomPropertyDrawer(typeof(BossGraphSfxIdAttribute))]
internal sealed class BossGraphSfxIdDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        IReadOnlyList<string> ids = BossGraphSfxIdOptions.Ids;
        if (property.propertyType != SerializedPropertyType.String || ids.Count == 0)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        List<string> values = new() { string.Empty };
        List<GUIContent> labels = new() { new GUIContent("<없음>") };
        for (int i = 0; i < ids.Count; i++)
        {
            values.Add(ids[i]);
            labels.Add(new GUIContent(ids[i]));
        }

        if (!string.IsNullOrWhiteSpace(property.stringValue) && !values.Contains(property.stringValue))
        {
            values.Add(property.stringValue);
            labels.Add(new GUIContent($"{property.stringValue} (목록 없음)"));
        }

        int currentIndex = Mathf.Max(0, values.IndexOf(property.stringValue));
        int nextIndex = EditorGUI.Popup(position, label, currentIndex, labels.ToArray());
        property.stringValue = values[nextIndex];
    }
}
