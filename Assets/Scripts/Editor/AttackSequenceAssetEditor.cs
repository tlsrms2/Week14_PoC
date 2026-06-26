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
        Type actionType = action.managedReferenceValue?.GetType();
        string description = BossGraphActionEditorUtility.GetActionDescription(actionType);
        if (!BossGraphDrawerDescriptionGui.SuppressDescriptions && !string.IsNullOrWhiteSpace(description))
        {
            EditorGUILayout.HelpBox(description, MessageType.Info);
        }

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
        new("Utility/Custom Event", typeof(CustomEventAction), () => new CustomEventAction()),
        new("Utility/Spawn Prefab", typeof(SpawnPrefabAction), () => new SpawnPrefabAction()),
        new("Minion/Spawn/Summon", typeof(MinionSummonAction), () => new MinionSummonAction()),
        new("Minion/Spawn/Ensure Count", typeof(MinionEnsureCountAction), () => new MinionEnsureCountAction()),
        new("Minion/Spawn/Auto Summon If Needed", typeof(MinionAutoSummonIfNeededAction), () => new MinionAutoSummonIfNeededAction()),
        new("Minion/Fire/Fire All", typeof(MinionFireAllAction), () => new MinionFireAllAction()),
        new("Minion/Fire/Boss Burst", typeof(MinionBossBurstAction), () => new MinionBossBurstAction()),
        new("Minion/Fire/Synchronized Burst", typeof(MinionSynchronizedBurstAction), () => new MinionSynchronizedBurstAction()),
        new("Minion/Fire/Stop And Fire", typeof(MinionStopAndFireAction), () => new MinionStopAndFireAction()),
        new("Minion/Fire/Radial Burst", typeof(MinionRadialBurstAction), () => new MinionRadialBurstAction()),
        new("Minion/Movement/Orbit Fire", typeof(MinionOrbitFireAction), () => new MinionOrbitFireAction()),
        new("Minion/Movement/Orbit Crossfire", typeof(MinionOrbitCrossfireAction), () => new MinionOrbitCrossfireAction()),
        new("Minion/Movement/Charge Side Fire", typeof(MinionChargeSideFireAction), () => new MinionChargeSideFireAction()),
        new("Minion/Movement/Formation", typeof(MinionFormationAction), () => new MinionFormationAction()),
        new("Minion/Movement/Formation Barrage", typeof(MinionFormationBarrageAction), () => new MinionFormationBarrageAction()),
        new("Minion/Control/Command", typeof(MinionCommandAction), () => new MinionCommandAction()),
        new("Minion/Control/Wait Commands", typeof(MinionWaitCommandsAction), () => new MinionWaitCommandsAction()),
        new("Minion/Control/Clear Synchronized Fire", typeof(MinionClearSynchronizedFireAction), () => new MinionClearSynchronizedFireAction()),
        new("Minion/Control/Pattern Cleanup", typeof(MinionPatternCleanupAction), () => new MinionPatternCleanupAction()),
        new("Minion/Control/Stop All", typeof(MinionStopAllAction), () => new MinionStopAllAction()),
        new("Minion/Control/Resume Idle", typeof(MinionResumeIdleAction), () => new MinionResumeIdleAction())
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

    public static string GetActionDescription(Type actionType)
    {
        if (actionType == typeof(WaitAction))
        {
            return "지정 시간 동안 대기합니다. 지속 이동이 켜져 있으면 대기 중에도 이동 갱신이 유지됩니다.";
        }

        if (actionType == typeof(WindupAction))
        {
            return "공격 전 준비 시간을 담당합니다. 준비 중 이동 정지와 연기 이펙트를 처리할 수 있습니다.";
        }

        if (actionType == typeof(PlayAnimationAction))
        {
            return "Animator Trigger를 실행하거나 Animator State를 직접 재생합니다.";
        }

        if (actionType == typeof(WaitForAnimationEventAction))
        {
            return "지정한 애니메이션 이벤트 ID가 들어올 때까지 대기합니다.";
        }

        if (actionType == typeof(MoveTowardPlayerAction))
        {
            return "지정 시간 동안 플레이어 방향으로 이동합니다.";
        }

        if (actionType == typeof(StartMoveTowardPlayerAction))
        {
            return "이후 대기나 발사 액션이 실행되는 동안 플레이어 방향 이동을 계속 켭니다.";
        }

        if (actionType == typeof(StopMovementAction))
        {
            return "Start Move Toward Player로 켠 지속 이동을 끕니다.";
        }

        if (actionType == typeof(MoveBodyRootLocalAction))
        {
            return "보스 BodyRoot의 로컬 오프셋을 목표값까지 이동시킵니다.";
        }

        if (actionType == typeof(ResetBodyRootLocalAction))
        {
            return "보스 BodyRoot의 로컬 오프셋을 초기화합니다.";
        }

        if (actionType == typeof(FireProjectileAction))
        {
            return "단발 투사체를 발사합니다. 간단한 1발 발사용 액션입니다.";
        }

        if (actionType == typeof(FireProjectileBurstAction))
        {
            return "한 방향으로 여러 발을 연속 발사합니다. 머신건류 패턴의 발사 부분을 담당합니다.";
        }

        if (actionType == typeof(FireRadialEmissionAction))
        {
            return "원형 또는 부채꼴로 투사체를 방사합니다.";
        }

        if (actionType == typeof(FireSweepEmissionAction))
        {
            return "기준 방향을 좌우로 흔들며 연속 발사합니다. 준비 시간은 Windup과 분리해서 구성합니다.";
        }

        if (actionType == typeof(FireFanEmissionAction))
        {
            return "부채꼴 발리를 여러 번 발사합니다. 준비 시간은 Windup과 분리해서 구성합니다.";
        }

        if (actionType == typeof(SpawnChargedProjectileAction))
        {
            return "차징 투사체를 생성하고 핸들에 저장합니다. 이후 성장, 분열, 차징 종료 대기 액션이 같은 핸들을 사용합니다.";
        }

        if (actionType == typeof(ConfigureProjectileGrowthAction))
        {
            return "핸들에 저장된 차징 투사체의 크기 성장을 설정합니다.";
        }

        if (actionType == typeof(ConfigureRadialSplitAction))
        {
            return "핸들에 저장된 차징 투사체가 Launch 이후 방사형으로 분열되도록 설정합니다.";
        }

        if (actionType == typeof(WaitProjectileChargeEndAction))
        {
            return "핸들에 저장된 차징 투사체가 차징을 끝낼 때까지 대기합니다.";
        }

        if (actionType == typeof(AimBossChildAtPlayerAction))
        {
            return "보스 자식 오브젝트가 플레이어를 바라보도록 켜거나 끕니다. Start와 End 노드 한 쌍으로 사용합니다.";
        }

        if (actionType == typeof(CustomEventAction))
        {
            return "보스 오브젝트에 SendMessage 또는 BroadcastMessage 방식으로 커스텀 이벤트를 보냅니다.";
        }

        if (actionType == typeof(SpawnPrefabAction))
        {
            return "프리팹을 보스 기준 위치에 생성합니다. 필요하면 보스 자식으로 붙이고 일정 시간 뒤 제거합니다.";
        }

        if (actionType == typeof(MinionSummonAction))
        {
            return "호스트 보스가 관리하는 미니언을 지정 수만큼 소환합니다.";
        }

        if (actionType == typeof(MinionEnsureCountAction))
        {
            return "패턴 시작에 필요한 미니언 수를 보장합니다.";
        }

        if (actionType == typeof(MinionAutoSummonIfNeededAction))
        {
            return "미니언이 부족할 때만 자동 보충 소환을 실행합니다.";
        }

        if (actionType == typeof(MinionFireAllAction))
        {
            return "현재 관리 중인 모든 미니언이 같은 타이밍에 발사합니다.";
        }

        if (actionType == typeof(MinionBossBurstAction))
        {
            return "보스 본체 발사와 미니언 보조 발사를 하나의 액션에서 함께 실행합니다.";
        }

        if (actionType == typeof(MinionSynchronizedBurstAction))
        {
            return "보스와 미니언의 동기화 발사 패턴을 실행합니다.";
        }

        if (actionType == typeof(MinionCommandAction))
        {
            return "미니언에게 선택한 명령 타입과 투사체 설정을 전달합니다.";
        }

        if (actionType == typeof(MinionStopAndFireAction))
        {
            return "미니언을 정지시키고 지정 투사체를 발사하게 합니다.";
        }

        if (actionType == typeof(MinionOrbitFireAction))
        {
            return "미니언이 궤도 이동 중 발사하는 패턴을 실행합니다.";
        }

        if (actionType == typeof(MinionOrbitCrossfireAction))
        {
            return "궤도 미니언과 고정 미니언의 교차 사격을 실행합니다.";
        }

        if (actionType == typeof(MinionRadialBurstAction))
        {
            return "미니언 기준 방사형 발사를 실행합니다.";
        }

        if (actionType == typeof(MinionChargeSideFireAction))
        {
            return "차지 후 양측 미니언 발사를 실행합니다.";
        }

        if (actionType == typeof(MinionFormationAction))
        {
            return "미니언을 지정 반경과 각도 간격의 진형으로 이동시킵니다.";
        }

        if (actionType == typeof(MinionFormationBarrageAction))
        {
            return "진형 배치와 반복 발사를 묶은 미니언 포격 패턴을 실행합니다.";
        }

        if (actionType == typeof(MinionWaitCommandsAction))
        {
            return "현재 미니언 명령이 끝날 때까지 대기합니다.";
        }

        if (actionType == typeof(MinionClearSynchronizedFireAction))
        {
            return "동기화 발사 상태를 명시적으로 비웁니다.";
        }

        if (actionType == typeof(MinionPatternCleanupAction))
        {
            return "미니언 명령, 동기화 발사, 대기 복귀를 패턴 종료용으로 정리합니다.";
        }

        if (actionType == typeof(MinionStopAllAction))
        {
            return "모든 미니언의 현재 명령을 중지합니다.";
        }

        if (actionType == typeof(MinionResumeIdleAction))
        {
            return "미니언을 기본 대기 이동 상태로 되돌립니다.";
        }

        return string.Empty;
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

        BossGraphNodeKind defaultKind = BossGraphActionCategoryAsset.GetDefaultNodeKind(actionType);
        if (defaultKind == BossGraphNodeKind.Utility || defaultKind == BossGraphNodeKind.Minion)
        {
            return NodeKind == defaultKind;
        }

        BossGraphNodeKind actionNodeKind = Categories != null
            ? Categories.GetNodeKind(actionType)
            : defaultKind;
        if (actionNodeKind == BossGraphNodeKind.Utility || actionNodeKind == BossGraphNodeKind.Minion)
        {
            return defaultKind == actionNodeKind && NodeKind == actionNodeKind;
        }

        return actionNodeKind == NodeKind;
    }
}

internal static class BossGraphAimStartNodeOptions
{
    private static readonly List<string> startNodeIds = new();
    private static readonly Dictionary<string, string> startNodeLabels = new();
    private static bool hasContext;

    public static IReadOnlyList<string> StartNodeIds => startNodeIds;

    public static void Set(SerializedObject graphObject)
    {
        startNodeIds.Clear();
        startNodeLabels.Clear();
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
                startNodeLabels[nodeId] = GetNodeDisplayName(stateNodes, node, i);
            }
        }
    }

    public static void Clear()
    {
        startNodeIds.Clear();
        startNodeLabels.Clear();
        hasContext = false;
    }

    public static string GetStartNodeLabel(string nodeId)
    {
        return !string.IsNullOrWhiteSpace(nodeId) && startNodeLabels.TryGetValue(nodeId, out string label)
            ? label
            : nodeId;
    }

    public static bool ContainsStartNode(string nodeId)
    {
        return string.IsNullOrWhiteSpace(nodeId)
            || !hasContext
            || startNodeIds.Contains(nodeId);
    }

    private static bool IsAimStartNode(SerializedProperty node)
    {
        BossAction directAction = node.FindPropertyRelative("action")?.managedReferenceValue as BossAction;
        if (directAction is AimBossChildAtPlayerAction directAimAction)
        {
            return directAimAction.Mode == BossChildAimActionMode.Start;
        }

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

    private static string GetNodeDisplayName(SerializedProperty stateNodes, SerializedProperty node, int nodeIndex)
    {
        string baseName = GetNodeActionDisplayName(node);
        int totalCount = CountNodeDisplayNames(stateNodes, baseName);
        if (totalCount <= 1)
        {
            return baseName;
        }

        int occurrence = GetNodeDisplayNameOccurrence(stateNodes, baseName, nodeIndex);
        return $"{baseName} {Mathf.Max(1, occurrence):00}";
    }

    private static int CountNodeDisplayNames(SerializedProperty stateNodes, string baseName)
    {
        if (stateNodes == null || string.IsNullOrWhiteSpace(baseName))
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < stateNodes.arraySize; i++)
        {
            SerializedProperty node = stateNodes.GetArrayElementAtIndex(i);
            if (GetNodeActionDisplayName(node) == baseName)
            {
                count++;
            }
        }

        return count;
    }

    private static int GetNodeDisplayNameOccurrence(SerializedProperty stateNodes, string baseName, int nodeIndex)
    {
        if (stateNodes == null || string.IsNullOrWhiteSpace(baseName))
        {
            return 1;
        }

        int maxIndex = Mathf.Min(nodeIndex, stateNodes.arraySize - 1);
        int occurrence = 0;
        for (int i = 0; i <= maxIndex; i++)
        {
            SerializedProperty node = stateNodes.GetArrayElementAtIndex(i);
            if (GetNodeActionDisplayName(node) == baseName)
            {
                occurrence++;
            }
        }

        return occurrence;
    }

    private static string GetNodeActionDisplayName(SerializedProperty node)
    {
        BossAction action = node.FindPropertyRelative("action")?.managedReferenceValue as BossAction;
        Type actionType = action?.GetType() ?? GetLegacyActionType(node);
        if (actionType == null)
        {
            return "Empty Action";
        }

        string label = BossGraphActionEditorUtility.GetActionLabel(actionType);
        int slashIndex = label.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < label.Length - 1)
        {
            label = label.Substring(slashIndex + 1);
        }

        const string actionSuffix = " Action";
        if (label.EndsWith(actionSuffix, StringComparison.Ordinal))
        {
            label = label.Substring(0, label.Length - actionSuffix.Length);
        }

        return string.IsNullOrWhiteSpace(label) ? "Empty Action" : label;
    }

    private static Type GetLegacyActionType(SerializedProperty node)
    {
        SerializedProperty sequences = node.FindPropertyRelative("sequences");
        if (sequences == null || sequences.arraySize == 0)
        {
            return null;
        }

        BossGraphActionAsset actionAsset = sequences.GetArrayElementAtIndex(0)
            .FindPropertyRelative("sequence")?.objectReferenceValue as BossGraphActionAsset;
        return actionAsset?.Action?.GetType();
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

internal static class BossGraphDrawerDescriptionGui
{
    public const float HelpBoxHeight = 38f;
    private static int suppressDescriptionsDepth;

    public static float Spacing => EditorGUIUtility.standardVerticalSpacing;
    public static bool SuppressDescriptions => suppressDescriptionsDepth > 0;
    public static float FoldoutDescriptionHeight => SuppressDescriptions ? Spacing : Spacing + HelpBoxHeight + Spacing;
    public static float InlineDescriptionHeight => SuppressDescriptions ? 0f : HelpBoxHeight + Spacing;

    public static IDisposable SuppressDescriptionsScope()
    {
        return new SuppressDescriptionScope();
    }

    public static float GetPropertyHeight(SerializedProperty property)
    {
        if (property == null)
        {
            return 0f;
        }

        return EditorGUI.GetPropertyHeight(property, true) + Spacing;
    }

    public static void DrawDescription(ref Rect lineRect, string text)
    {
        if (SuppressDescriptions)
        {
            return;
        }

        Rect descriptionRect = new(lineRect.x, lineRect.y, lineRect.width, HelpBoxHeight);
        EditorGUI.HelpBox(descriptionRect, text, MessageType.None);
        lineRect.y += HelpBoxHeight + Spacing;
    }

    public static void DrawProperty(ref Rect lineRect, SerializedProperty property)
    {
        if (property == null)
        {
            return;
        }

        float height = EditorGUI.GetPropertyHeight(property, true);
        Rect propertyRect = new(lineRect.x, lineRect.y, lineRect.width, height);
        EditorGUI.PropertyField(propertyRect, property, true);
        lineRect.y += height + Spacing;
    }

    private sealed class SuppressDescriptionScope : IDisposable
    {
        private bool disposed;

        public SuppressDescriptionScope()
        {
            suppressDescriptionsDepth++;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            suppressDescriptionsDepth = Mathf.Max(0, suppressDescriptionsDepth - 1);
        }
    }
}

[CustomPropertyDrawer(typeof(BossGraphProjectileOriginSpec))]
internal sealed class BossGraphProjectileOriginSpecDrawer : PropertyDrawer
{
    private static readonly string[] PropertyNames =
    {
        "mode",
        "bossChildPath",
        "bossChildPaths",
        "firstBossChildPath",
        "secondBossChildPath",
        "fallbackSpacing"
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        height += BossGraphDrawerDescriptionGui.FoldoutDescriptionHeight;
        foreach (string propertyName in PropertyNames)
        {
            height += BossGraphDrawerDescriptionGui.GetPropertyHeight(property.FindPropertyRelative(propertyName));
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect lineRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        lineRect.y += EditorGUIUtility.singleLineHeight + BossGraphDrawerDescriptionGui.Spacing;
        BossGraphDrawerDescriptionGui.DrawDescription(ref lineRect, "투사체나 이펙트가 생성될 기준 위치를 정합니다. 보스 원점, 특정 자식, 자식 목록, 두 자식 교대를 사용할 수 있습니다.");
        foreach (string propertyName in PropertyNames)
        {
            BossGraphDrawerDescriptionGui.DrawProperty(ref lineRect, property.FindPropertyRelative(propertyName));
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}

[CustomPropertyDrawer(typeof(MinionGraphProjectileOriginSpec))]
internal sealed class MinionGraphProjectileOriginSpecDrawer : PropertyDrawer
{
    private static readonly string[] PropertyNames =
    {
        "mode",
        "minionChildPath",
        "minionChildPaths",
        "firstMinionChildPath",
        "secondMinionChildPath",
        "fallbackSpacing"
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        height += BossGraphDrawerDescriptionGui.FoldoutDescriptionHeight;
        foreach (string propertyName in PropertyNames)
        {
            height += BossGraphDrawerDescriptionGui.GetPropertyHeight(property.FindPropertyRelative(propertyName));
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect lineRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        lineRect.y += EditorGUIUtility.singleLineHeight + BossGraphDrawerDescriptionGui.Spacing;
        BossGraphDrawerDescriptionGui.DrawDescription(ref lineRect, "미니언 투사체가 생성될 기준 위치를 정합니다. 미니언의 Projectile Origin, 루트, 특정 자식, 자식 목록, 두 자식 교대를 사용할 수 있습니다.");
        foreach (string propertyName in PropertyNames)
        {
            BossGraphDrawerDescriptionGui.DrawProperty(ref lineRect, property.FindPropertyRelative(propertyName));
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}

[CustomPropertyDrawer(typeof(BossGraphProjectileAimSpec))]
internal sealed class BossGraphProjectileAimSpecDrawer : PropertyDrawer
{
    private static readonly string[] PropertyNames =
    {
        "mode",
        "angleDegrees"
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        height += BossGraphDrawerDescriptionGui.FoldoutDescriptionHeight;
        foreach (string propertyName in PropertyNames)
        {
            height += BossGraphDrawerDescriptionGui.GetPropertyHeight(property.FindPropertyRelative(propertyName));
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect lineRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        lineRect.y += EditorGUIUtility.singleLineHeight + BossGraphDrawerDescriptionGui.Spacing;
        BossGraphDrawerDescriptionGui.DrawDescription(ref lineRect, "투사체가 향할 방향을 정합니다. 플레이어 조준 또는 고정 각도를 사용할 수 있습니다.");
        foreach (string propertyName in PropertyNames)
        {
            BossGraphDrawerDescriptionGui.DrawProperty(ref lineRect, property.FindPropertyRelative(propertyName));
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}

[CustomPropertyDrawer(typeof(BossGraphParticleEffectSettings))]
internal sealed class BossGraphParticleEffectSettingsDrawer : PropertyDrawer
{
    private static readonly string[] PropertyNames =
    {
        "enabled",
        "color",
        "scale",
        "count"
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        height += BossGraphDrawerDescriptionGui.FoldoutDescriptionHeight;
        foreach (string propertyName in PropertyNames)
        {
            height += BossGraphDrawerDescriptionGui.GetPropertyHeight(property.FindPropertyRelative(propertyName));
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect lineRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        lineRect.y += EditorGUIUtility.singleLineHeight + BossGraphDrawerDescriptionGui.Spacing;
        BossGraphDrawerDescriptionGui.DrawDescription(ref lineRect, GetDescription(property.name));
        foreach (string propertyName in PropertyNames)
        {
            BossGraphDrawerDescriptionGui.DrawProperty(ref lineRect, property.FindPropertyRelative(propertyName));
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    private static string GetDescription(string propertyName)
    {
        return propertyName switch
        {
            "explosion" => "폭발 파티클 설정입니다. 켜면 발사/생성 지점에서 폭발 파티클을 재생합니다.",
            "smoke" => "연기 파티클 설정입니다. Smoke Interval은 이 연기가 반복 재생되는 간격입니다.",
            "muzzleFlash" => "총구 섬광 설정입니다. 켜면 발사 위치와 방향에 맞춰 섬광을 재생합니다.",
            _ => "파티클 이펙트의 사용 여부, 색, 크기, 개수를 정합니다."
        };
    }
}

[CustomPropertyDrawer(typeof(BossGraphCameraShakeSettings))]
internal sealed class BossGraphCameraShakeSettingsDrawer : PropertyDrawer
{
    private static readonly string[] PropertyNames =
    {
        "enabled",
        "seconds",
        "distance",
        "frequency"
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        height += BossGraphDrawerDescriptionGui.FoldoutDescriptionHeight;
        foreach (string propertyName in PropertyNames)
        {
            height += BossGraphDrawerDescriptionGui.GetPropertyHeight(property.FindPropertyRelative(propertyName));
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect lineRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        lineRect.y += EditorGUIUtility.singleLineHeight + BossGraphDrawerDescriptionGui.Spacing;
        BossGraphDrawerDescriptionGui.DrawDescription(ref lineRect, "카메라 흔들림 설정입니다. 켜면 지정 시간, 거리, 주기로 충격감을 줍니다.");
        foreach (string propertyName in PropertyNames)
        {
            BossGraphDrawerDescriptionGui.DrawProperty(ref lineRect, property.FindPropertyRelative(propertyName));
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}

[CustomPropertyDrawer(typeof(BossGraphEffectSettings))]
internal sealed class BossGraphEffectSettingsDrawer : PropertyDrawer
{
    private const string ExplosionProperty = "explosion";
    private const string SmokeProperty = "smoke";
    private const string SmokeIntervalProperty = "smokeInterval";
    private const string MuzzleFlashProperty = "muzzleFlash";
    private const string CameraShakeProperty = "cameraShake";

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        height += EditorGUIUtility.standardVerticalSpacing;
        height += BossGraphDrawerDescriptionGui.InlineDescriptionHeight;
        height += GetDefaultPropertyHeight(property.FindPropertyRelative(ExplosionProperty));
        height += GetSmokePropertyHeight(property);
        height += GetDefaultPropertyHeight(property.FindPropertyRelative(MuzzleFlashProperty));
        height += GetDefaultPropertyHeight(property.FindPropertyRelative(CameraShakeProperty));
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect lineRect = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        lineRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        BossGraphDrawerDescriptionGui.DrawDescription(ref lineRect, "액션과 함께 재생할 공통 이펙트 묶음입니다. SFX는 각 액션 필드에 그대로 둡니다.");
        DrawDefaultProperty(ref lineRect, property.FindPropertyRelative(ExplosionProperty));
        DrawSmokeProperty(ref lineRect, property);
        DrawDefaultProperty(ref lineRect, property.FindPropertyRelative(MuzzleFlashProperty));
        DrawDefaultProperty(ref lineRect, property.FindPropertyRelative(CameraShakeProperty));
        EditorGUI.indentLevel--;

        EditorGUI.EndProperty();
    }

    private static float GetDefaultPropertyHeight(SerializedProperty property)
    {
        if (property == null)
        {
            return 0f;
        }

        return EditorGUI.GetPropertyHeight(property, true) + EditorGUIUtility.standardVerticalSpacing;
    }

    private static float GetSmokePropertyHeight(SerializedProperty property)
    {
        SerializedProperty smoke = property.FindPropertyRelative(SmokeProperty);
        if (smoke == null)
        {
            return 0f;
        }

        float height = EditorGUI.GetPropertyHeight(smoke, true) + EditorGUIUtility.standardVerticalSpacing;
        if (smoke.isExpanded)
        {
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        return height;
    }

    private static void DrawDefaultProperty(ref Rect lineRect, SerializedProperty property)
    {
        if (property == null)
        {
            return;
        }

        float height = EditorGUI.GetPropertyHeight(property, true);
        Rect propertyRect = new(lineRect.x, lineRect.y, lineRect.width, height);
        EditorGUI.PropertyField(propertyRect, property, true);
        lineRect.y += height + EditorGUIUtility.standardVerticalSpacing;
    }

    private static void DrawSmokeProperty(ref Rect lineRect, SerializedProperty property)
    {
        SerializedProperty smoke = property.FindPropertyRelative(SmokeProperty);
        if (smoke == null)
        {
            return;
        }

        DrawDefaultProperty(ref lineRect, smoke);
        if (!smoke.isExpanded)
        {
            return;
        }

        SerializedProperty smokeInterval = property.FindPropertyRelative(SmokeIntervalProperty);
        if (smokeInterval == null)
        {
            return;
        }

        EditorGUI.indentLevel++;
        Rect intervalRect = new(lineRect.x, lineRect.y, lineRect.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(intervalRect, smokeInterval);
        lineRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.indentLevel--;
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
            labels.Add(new GUIContent(BossGraphAimStartNodeOptions.GetStartNodeLabel(startNodeIds[i])));
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
            GUI.changed = true;
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
                GUI.changed = true;
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

        if (TryGetDroppedPath(fieldRect, out string path, true))
        {
            property.stringValue = path;
            GUI.changed = true;
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

        if (!TryResolveDroppedPath(out string droppedPath))
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

    private static bool TryResolveDroppedPath(out string path)
    {
        path = string.Empty;
        object data = DragAndDrop.GetGenericData(BossGraphDragKeys.BossChildPath);
        if (data is string droppedPath && !string.IsNullOrWhiteSpace(droppedPath))
        {
            path = droppedPath;
            return true;
        }

        UnityEngine.Object[] objectReferences = DragAndDrop.objectReferences;
        if (objectReferences == null)
        {
            return false;
        }

        for (int i = 0; i < objectReferences.Length; i++)
        {
            if (BossGraphBossHierarchyOptions.TryGetPath(objectReferences[i], out path))
            {
                return true;
            }
        }

        return false;
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
        GUI.changed = true;
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

[CustomPropertyDrawer(typeof(BossGraphMinionChildPathAttribute))]
internal sealed class BossGraphMinionChildPathDrawer : PropertyDrawer
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
            GUI.changed = true;
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
            GUI.Box(dropRect, "Drop Minion Hierarchy Item To Add", EditorStyles.helpBox);
            if (TryGetDroppedPath(dropRect, out string path, true))
            {
                AddPath(property, path);
                GUI.changed = true;
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
        string displayValue = string.IsNullOrWhiteSpace(value) ? "<미니언 하이어러키에서 드롭>" : value;
        if (!string.IsNullOrWhiteSpace(value) && !BossGraphMinionHierarchyOptions.ContainsPath(value))
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

        if (TryGetDroppedPath(fieldRect, out string path, true))
        {
            property.stringValue = path;
            GUI.changed = true;
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

        if (!TryResolveDroppedPath(out string droppedPath))
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

    private static bool TryResolveDroppedPath(out string path)
    {
        path = string.Empty;
        object data = DragAndDrop.GetGenericData(BossGraphDragKeys.MinionChildPath);
        if (data is string droppedPath && !string.IsNullOrWhiteSpace(droppedPath))
        {
            path = droppedPath;
            return true;
        }

        UnityEngine.Object[] objectReferences = DragAndDrop.objectReferences;
        if (objectReferences == null)
        {
            return false;
        }

        for (int i = 0; i < objectReferences.Length; i++)
        {
            if (BossGraphMinionHierarchyOptions.TryGetPath(objectReferences[i], out path))
            {
                return true;
            }
        }

        return false;
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
        GUI.changed = true;
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

    public static bool TryGetPath(UnityEngine.Object source, out string path)
    {
        path = string.Empty;
        Transform transform = source switch
        {
            GameObject gameObject => gameObject.transform,
            Transform sourceTransform => sourceTransform,
            _ => null
        };

        if (transform == null)
        {
            return false;
        }

        if (root == null)
        {
            path = transform.name;
            return !string.IsNullOrWhiteSpace(path);
        }

        if (transform == root)
        {
            return false;
        }

        if (!transform.IsChildOf(root))
        {
            return false;
        }

        path = GetRelativePath(root, transform);
        return !string.IsNullOrWhiteSpace(path);
    }

    private static string GetRelativePath(Transform parent, Transform child)
    {
        List<string> names = new();
        Transform current = child;
        while (current != null && current != parent)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
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

internal static class BossGraphMinionHierarchyOptions
{
    private static Transform root;

    public static void Set(Transform minionRoot)
    {
        root = minionRoot;
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

    public static bool TryGetPath(UnityEngine.Object source, out string path)
    {
        path = string.Empty;
        Transform transform = source switch
        {
            GameObject gameObject => gameObject.transform,
            Transform sourceTransform => sourceTransform,
            _ => null
        };

        if (transform == null)
        {
            return false;
        }

        if (root == null)
        {
            path = transform.name;
            return !string.IsNullOrWhiteSpace(path);
        }

        if (transform == root)
        {
            return false;
        }

        if (!transform.IsChildOf(root))
        {
            return false;
        }

        path = GetRelativePath(root, transform);
        return !string.IsNullOrWhiteSpace(path);
    }

    private static string GetRelativePath(Transform parent, Transform child)
    {
        List<string> names = new();
        Transform current = child;
        while (current != null && current != parent)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
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

internal static class BossGraphBgmIdOptions
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

            foreach (string id in library.BgmIds)
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

[CustomPropertyDrawer(typeof(BossGraphBgmIdAttribute))]
internal sealed class BossGraphBgmIdDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        IReadOnlyList<string> ids = BossGraphBgmIdOptions.Ids;
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
