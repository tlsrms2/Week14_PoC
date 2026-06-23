using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Week14.Enemy;

#pragma warning disable CS0618

internal readonly struct BossGraphValidationMessage
{
    public BossGraphValidationMessage(MessageType type, string text)
    {
        Type = type;
        Text = text;
    }

    public MessageType Type { get; }
    public string Text { get; }
}

internal static class BossGraphValidationUtility
{
    public static List<BossGraphValidationMessage> Validate(SerializedObject graphObject)
    {
        List<BossGraphValidationMessage> messages = new();
        if (graphObject == null)
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Info, "BossGraphAsset을 선택하세요."));
            return messages;
        }

        graphObject.UpdateIfRequiredOrScript();
        SerializedProperty stateNodes = graphObject.FindProperty("stateNodes");
        SerializedProperty patterns = graphObject.FindProperty("patterns");
        SerializedProperty phases = graphObject.FindProperty("phases");
        SerializedProperty transitions = graphObject.FindProperty("transitions");
        SerializedProperty startNodeId = graphObject.FindProperty("startNodeId");
        SerializedProperty references = graphObject.FindProperty("referenceSettings");
        BossGraphActionCategoryAsset actionCategories = GetActionCategories(references);
        bool usesPhasePatternLayout = phases != null && phases.arraySize > 0;

        ValidateReferences(references, messages);
        Dictionary<string, int> nodeIdCounts = CollectNodeIds(stateNodes, messages);
        Dictionary<string, int> nodeGuidCounts = ValidateNodeGuids(stateNodes, messages);
        ValidateStartNode(startNodeId, nodeIdCounts, nodeGuidCounts, messages);
        ValidateStateNodes(stateNodes, transitions, actionCategories, usesPhasePatternLayout, messages);
        if (usesPhasePatternLayout)
        {
            Dictionary<string, int> patternIdCounts = ValidatePatterns(patterns, nodeIdCounts, nodeGuidCounts, messages);
            ValidatePhases(phases, patternIdCounts, messages);
        }
        else
        {
            ValidateTransitions(transitions, nodeIdCounts, nodeGuidCounts, messages);
        }

        return messages;
    }

    public static void DrawMessages(IReadOnlyList<BossGraphValidationMessage> messages, int maxVisible = 8)
    {
        if (messages == null || messages.Count == 0)
        {
            EditorGUILayout.HelpBox("그래프 검증 통과.", MessageType.Info);
            return;
        }

        int visibleCount = Mathf.Min(messages.Count, Mathf.Max(1, maxVisible));
        for (int i = 0; i < visibleCount; i++)
        {
            EditorGUILayout.HelpBox(messages[i].Text, messages[i].Type);
        }

        if (messages.Count > visibleCount)
        {
            EditorGUILayout.HelpBox($"검증 메시지 {messages.Count - visibleCount}개가 더 있습니다.", MessageType.Info);
        }
    }

    private static void ValidateReferences(SerializedProperty references, List<BossGraphValidationMessage> messages)
    {
        if (references == null)
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Warning, "Graph References가 없습니다."));
            return;
        }

        RequireReference(references, "effectData", "Graph References", messages);
        RequireReference(references, "colorSettings", "Graph References", messages);
        RequireReference(references, "actionCategories", "Graph References", messages);
    }

    private static Dictionary<string, int> CollectNodeIds(
        SerializedProperty stateNodes,
        List<BossGraphValidationMessage> messages)
    {
        Dictionary<string, int> nodeIdCounts = new();
        if (stateNodes == null || stateNodes.arraySize == 0)
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Error, "Action Node가 없습니다."));
            return nodeIdCounts;
        }

        for (int i = 0; i < stateNodes.arraySize; i++)
        {
            SerializedProperty node = stateNodes.GetArrayElementAtIndex(i);
            string nodeId = GetString(node, "nodeId");
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Error, $"Node {i + 1}: nodeId가 비어 있습니다."));
                continue;
            }

            nodeIdCounts.TryGetValue(nodeId, out int count);
            nodeIdCounts[nodeId] = count + 1;
        }

        foreach (KeyValuePair<string, int> entry in nodeIdCounts)
        {
            if (entry.Value > 1)
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Error, $"Node ID '{entry.Key}'가 {entry.Value}번 중복됩니다."));
            }
        }

        return nodeIdCounts;
    }

    private static Dictionary<string, int> ValidateNodeGuids(
        SerializedProperty stateNodes,
        List<BossGraphValidationMessage> messages)
    {
        Dictionary<string, int> guidCounts = new();
        if (stateNodes == null)
        {
            return guidCounts;
        }

        for (int i = 0; i < stateNodes.arraySize; i++)
        {
            SerializedProperty node = stateNodes.GetArrayElementAtIndex(i);
            string nodeGuid = GetString(node, "nodeGuid");
            if (string.IsNullOrWhiteSpace(nodeGuid))
            {
                messages.Add(new BossGraphValidationMessage(
                    MessageType.Warning,
                    $"Node {i + 1}: nodeGuid가 비어 있습니다. 에셋을 다시 저장해 자동 생성하세요."));
                continue;
            }

            guidCounts.TryGetValue(nodeGuid, out int count);
            guidCounts[nodeGuid] = count + 1;
        }

        foreach (KeyValuePair<string, int> entry in guidCounts)
        {
            if (entry.Value > 1)
            {
                messages.Add(new BossGraphValidationMessage(
                    MessageType.Error,
                    $"nodeGuid '{entry.Key}'가 {entry.Value}번 중복됩니다."));
            }
        }

        return guidCounts;
    }

    private static void ValidateStartNode(
        SerializedProperty startNodeId,
        Dictionary<string, int> nodeIdCounts,
        Dictionary<string, int> nodeGuidCounts,
        List<BossGraphValidationMessage> messages)
    {
        string id = startNodeId != null ? startNodeId.stringValue : string.Empty;
        if (string.IsNullOrWhiteSpace(id))
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Warning, "startNodeId가 비어 있습니다. 첫 노드로 fallback됩니다."));
            return;
        }

        if (!nodeIdCounts.ContainsKey(id) && !nodeGuidCounts.ContainsKey(id))
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Error, $"startNodeId '{id}'와 일치하는 노드가 없습니다."));
        }
    }

    private static void ValidateStateNodes(
        SerializedProperty stateNodes,
        SerializedProperty transitions,
        BossGraphActionCategoryAsset actionCategories,
        bool usesPhasePatternLayout,
        List<BossGraphValidationMessage> messages)
    {
        if (stateNodes == null)
        {
            return;
        }

        for (int nodeIndex = 0; nodeIndex < stateNodes.arraySize; nodeIndex++)
        {
            SerializedProperty node = stateNodes.GetArrayElementAtIndex(nodeIndex);
            string nodeLabel = GetNodeLabel(node, nodeIndex);
            BossGraphNodeKind nodeKind = (BossGraphNodeKind)GetEnum(node, "nodeKind");
            SerializedProperty action = node.FindPropertyRelative("action");
            object directAction = action?.managedReferenceValue;
            SerializedProperty sequences = node.FindPropertyRelative("sequences");
            if (directAction != null)
            {
                string actionLabel = $"{nodeLabel}/Action";
                ValidateActionCategory(directAction.GetType(), nodeKind, actionCategories, actionLabel, messages);
                ValidateAction(action, directAction, actionLabel, messages);
                if (sequences != null && sequences.arraySize > 0)
                {
                    messages.Add(new BossGraphValidationMessage(
                        MessageType.Warning,
                        $"{nodeLabel}: 직접 Action이 있어 기존 Action 참조는 런타임에서 무시됩니다."));
                }

                continue;
            }

            if (sequences == null || sequences.arraySize == 0)
            {
                if (!usesPhasePatternLayout && HasOutgoingTransition(transitions, GetString(node, "nodeId")))
                {
                    continue;
                }

                messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"{nodeLabel}: Action이 없습니다."));
                continue;
            }

            if (usesPhasePatternLayout && sequences.arraySize > 1)
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Error, $"{nodeLabel}: 하나의 노드에는 하나의 Action만 들어갈 수 있습니다."));
            }

            if (!usesPhasePatternLayout)
            {
                ValidateLegacySelectionSettings(node, sequences, nodeLabel, messages);
            }

            for (int entryIndex = 0; entryIndex < sequences.arraySize; entryIndex++)
            {
                SerializedProperty entry = sequences.GetArrayElementAtIndex(entryIndex);
                SerializedProperty actionProperty = entry.FindPropertyRelative("sequence");
                BossGraphActionAsset actionAsset = actionProperty?.objectReferenceValue as BossGraphActionAsset;
                if (actionAsset == null)
                {
                    messages.Add(new BossGraphValidationMessage(MessageType.Error, $"{nodeLabel}: Action 참조가 비어 있습니다."));
                    continue;
                }

                ValidateActionAsset(actionAsset, $"{nodeLabel}/{actionAsset.name}", nodeKind, actionCategories, usesPhasePatternLayout, messages);
            }
        }
    }

    private static void ValidateLegacySelectionSettings(
        SerializedProperty node,
        SerializedProperty sequences,
        string nodeLabel,
        List<BossGraphValidationMessage> messages)
    {
        BossSequenceSelectionMode selectionMode = (BossSequenceSelectionMode)GetEnum(node, "selectionMode");
        if (selectionMode == BossSequenceSelectionMode.RandomNoRepeat && sequences.arraySize < 2)
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"{nodeLabel}: RandomNoRepeat은 후보가 2개 이상일 때 의미가 있습니다."));
        }

        if (selectionMode == BossSequenceSelectionMode.WeightedRandom && !HasPositiveWeight(sequences))
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"{nodeLabel}: WeightedRandom에는 1 이상의 weight가 필요합니다."));
        }
    }

    private static bool HasPositiveWeight(SerializedProperty entries)
    {
        if (entries == null)
        {
            return false;
        }

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            if (GetInt(entry, "weight") > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateActionAsset(
        BossGraphActionAsset actionAsset,
        string label,
        BossGraphNodeKind nodeKind,
        BossGraphActionCategoryAsset actionCategories,
        bool requireSingleAction,
        List<BossGraphValidationMessage> messages)
    {
        SerializedObject actionObject = new(actionAsset);
        SerializedProperty actions = actionObject.FindProperty("actions");
        if (actions == null || actions.arraySize == 0)
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"{label}: Action 데이터가 없습니다."));
            return;
        }

        if (actions.arraySize > 1)
        {
            MessageType messageType = requireSingleAction ? MessageType.Error : MessageType.Warning;
            messages.Add(new BossGraphValidationMessage(
                messageType,
                $"{label}: Action Asset 안에는 하나의 Action만 있어야 합니다. 현재 런타임은 첫 번째 Action만 실행합니다."));
        }

        SerializedProperty action = actions.GetArrayElementAtIndex(0);
        object actionValue = action.managedReferenceValue;
        string actionLabel = $"{label}/Action 1";
        if (actionValue == null)
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Error, $"{actionLabel}: Action 타입이 비어 있습니다."));
            return;
        }

        ValidateActionCategory(actionValue.GetType(), nodeKind, actionCategories, actionLabel, messages);
        ValidateAction(action, actionValue, actionLabel, messages);
    }

    private static void ValidateActionCategory(
        Type actionType,
        BossGraphNodeKind nodeKind,
        BossGraphActionCategoryAsset actionCategories,
        string label,
        List<BossGraphValidationMessage> messages)
    {
        BossGraphNodeKind actionNodeKind = actionCategories != null
            ? actionCategories.GetNodeKind(actionType)
            : BossGraphActionCategoryAsset.GetDefaultNodeKind(actionType);
        if (actionNodeKind != nodeKind)
        {
            messages.Add(new BossGraphValidationMessage(
                MessageType.Error,
                $"{label}: {actionNodeKind} Action은 {nodeKind} 노드에 넣을 수 없습니다."));
        }
    }

    private static void ValidateAction(
        SerializedProperty action,
        object actionValue,
        string label,
        List<BossGraphValidationMessage> messages)
    {
        switch (actionValue)
        {
            case PlayAnimationAction:
                ValidatePlayAnimationAction(action, label, messages);
                break;
            case WaitForAnimationEventAction:
                RequireString(action, "eventId", label, messages);
                break;
            case WindupAction:
                ValidateOriginSpec(action.FindPropertyRelative("effectOrigin"), $"{label}/effectOrigin", messages);
                break;
            case FireFanVolleyProjectilesAction:
                WarnDeprecatedCompositeAction(label, "Windup + FireFanEmission + FireProjectileBurst", messages);
                RequirePath(action, "projectileOriginPath", label, messages);
                RequireString(action, "normalProjectileName", label, messages);
                if (GetInt(action, "secondaryBulletCount") > 0)
                {
                    RequireString(action, "secondaryProjectileName", label, messages);
                }
                break;
            case FireChargedRadialSplitProjectileAction:
                WarnDeprecatedCompositeAction(label, "SpawnChargedProjectile + ConfigureProjectileGrowth + ConfigureRadialSplit + WaitProjectileChargeEnd", messages);
                RequirePath(action, "projectileOriginPath", label, messages);
                RequireString(action, "projectileName", label, messages);
                break;
            case FireSweepProjectilesAction:
                WarnDeprecatedCompositeAction(label, "Windup + FireSweepEmission", messages);
                RequirePath(action, "projectileOriginPath", label, messages);
                RequireString(action, "projectileName", label, messages);
                break;
            case SpawnChargedProjectileAction:
                RequireString(action, "handleKey", label, messages);
                RequirePath(action, "projectileOriginPath", label, messages);
                RequireString(action, "projectileName", label, messages);
                break;
            case ConfigureProjectileGrowthAction:
            case ConfigureRadialSplitAction:
                RequireString(action, "handleKey", label, messages);
                break;
            case WaitProjectileChargeEndAction:
                RequireString(action, "handleKey", label, messages);
                RequirePath(action, "projectileOriginPath", label, messages);
                break;
            case FireMachinegunProjectilesAction:
                WarnDeprecatedCompositeAction(label, "StartMoveTowardPlayer + FireProjectileBurst + StopMovement", messages);
                RequireArrayNotEmpty(action, "volleys", label, messages);
                RequireString(action, "projectileName", label, messages);
                break;
            case FireProjectileBurstAction:
                RequireArrayNotEmpty(action, "volleys", label, messages);
                RequireString(action, "projectileName", label, messages);
                ValidateOriginSpec(action.FindPropertyRelative("origin"), $"{label}/origin", messages);
                break;
            case FireProjectileAction:
            case FireRadialProjectilesAction:
                if (actionValue is FireRadialProjectilesAction)
                {
                    WarnDeprecatedCompositeAction(label, "FireRadialEmission", messages);
                }

                RequireString(action, "projectileName", label, messages);
                break;
            case FireRotatingProjectilesAction:
                WarnDeprecatedCompositeAction(label, "FireRadialEmission", messages);
                RequireString(action, "projectileName", label, messages);
                break;
            case FireRadialEmissionAction:
            case FireSweepEmissionAction:
            case FireFanEmissionAction:
                RequireString(action, "projectileName", label, messages);
                ValidateOriginSpec(action.FindPropertyRelative("origin"), $"{label}/origin", messages);
                break;
            case SpawnPrefabAction:
                RequireObject(action, "prefab", label, messages);
                break;
            case AimBossChildAtPlayerAction:
                ValidateAimBossChildAtPlayerAction(action, label, messages);
                break;
            case CustomEventAction:
                RequireString(action, "methodName", label, messages);
                break;
        }
    }

    private static void WarnDeprecatedCompositeAction(
        string label,
        string replacement,
        List<BossGraphValidationMessage> messages)
    {
        messages.Add(new BossGraphValidationMessage(
            MessageType.Warning,
            $"{label}: 복합 액션입니다. 새 그래프에서는 {replacement} 조합으로 대체하세요."));
    }

    private static void ValidateAimBossChildAtPlayerAction(
        SerializedProperty action,
        string label,
        List<BossGraphValidationMessage> messages)
    {
        if (IsAimEndMode(action))
        {
            RequireString(action, "startNodeId", label, messages);
            return;
        }

        RequirePath(action, "targetPath", label, messages);
    }

    private static bool IsAimEndMode(SerializedProperty action)
    {
        SerializedProperty mode = action.FindPropertyRelative("mode");
        if (mode == null)
        {
            return false;
        }

        int endNameIndex = Array.IndexOf(mode.enumNames, nameof(BossChildAimActionMode.End));
        return mode.intValue == (int)BossChildAimActionMode.End || mode.enumValueIndex == endNameIndex;
    }

    private static void ValidatePlayAnimationAction(
        SerializedProperty action,
        string label,
        List<BossGraphValidationMessage> messages)
    {
        BossAnimationPlayMode playMode = (BossAnimationPlayMode)GetEnum(action, "playMode");
        if (playMode == BossAnimationPlayMode.Trigger)
        {
            RequireString(action, "triggerName", label, messages);
            return;
        }

        RequireString(action, "stateName", label, messages);
    }

    private static bool HasOutgoingTransition(SerializedProperty transitions, string nodeId)
    {
        if (transitions == null || string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        for (int i = 0; i < transitions.arraySize; i++)
        {
            SerializedProperty transition = transitions.GetArrayElementAtIndex(i);
            if (GetString(transition, "fromNodeId") == nodeId)
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, int> ValidatePatterns(
        SerializedProperty patterns,
        Dictionary<string, int> nodeIdCounts,
        Dictionary<string, int> nodeGuidCounts,
        List<BossGraphValidationMessage> messages)
    {
        Dictionary<string, int> patternIdCounts = new();
        if (patterns == null || patterns.arraySize == 0)
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Error, "Pattern 목록이 비어 있습니다."));
            return patternIdCounts;
        }

        for (int patternIndex = 0; patternIndex < patterns.arraySize; patternIndex++)
        {
            SerializedProperty pattern = patterns.GetArrayElementAtIndex(patternIndex);
            string patternId = GetString(pattern, "patternId");
            string patternLabel = string.IsNullOrWhiteSpace(patternId) ? $"Pattern {patternIndex + 1}" : patternId;
            if (string.IsNullOrWhiteSpace(patternId))
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Error, $"{patternLabel}: patternId가 비어 있습니다."));
            }
            else
            {
                patternIdCounts.TryGetValue(patternId, out int count);
                patternIdCounts[patternId] = count + 1;
            }

            SerializedProperty nodeKeys = GetPreferredNodeReferenceArray(pattern);
            if (nodeKeys == null || nodeKeys.arraySize == 0)
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"{patternLabel}: 노드 목록이 비어 있습니다."));
                continue;
            }

            bool usingGuids = nodeKeys.name == "nodeGuids";
            Dictionary<string, int> nodeCounts = usingGuids ? nodeGuidCounts : nodeIdCounts;
            string referenceLabel = usingGuids ? "nodeGuid" : "노드";
            for (int nodeIndex = 0; nodeIndex < nodeKeys.arraySize; nodeIndex++)
            {
                string nodeKey = nodeKeys.GetArrayElementAtIndex(nodeIndex).stringValue;
                if (string.IsNullOrWhiteSpace(nodeKey) || !nodeCounts.ContainsKey(nodeKey))
                {
                    messages.Add(new BossGraphValidationMessage(MessageType.Error, $"{patternLabel}: {referenceLabel} '{nodeKey}'를 찾을 수 없습니다."));
                }
            }
        }

        foreach (KeyValuePair<string, int> entry in patternIdCounts)
        {
            if (entry.Value > 1)
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Error, $"Pattern ID '{entry.Key}'가 {entry.Value}번 중복됩니다."));
            }
        }

        return patternIdCounts;
    }

    private static SerializedProperty GetPreferredNodeReferenceArray(SerializedProperty pattern)
    {
        SerializedProperty nodeGuids = pattern.FindPropertyRelative("nodeGuids");
        if (nodeGuids != null && nodeGuids.arraySize > 0)
        {
            return nodeGuids;
        }

        return pattern.FindPropertyRelative("nodeIds");
    }

    private static void ValidatePhases(
        SerializedProperty phases,
        Dictionary<string, int> patternIdCounts,
        List<BossGraphValidationMessage> messages)
    {
        if (phases == null || phases.arraySize == 0)
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Error, "Phase 목록이 비어 있습니다."));
            return;
        }

        HashSet<int> phaseIndexes = new();
        for (int phaseIndex = 0; phaseIndex < phases.arraySize; phaseIndex++)
        {
            SerializedProperty phase = phases.GetArrayElementAtIndex(phaseIndex);
            int index = GetInt(phase, "phaseIndex");
            string phaseLabel = $"Phase {index + 1}";
            if (!phaseIndexes.Add(index))
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Error, $"{phaseLabel}: phaseIndex가 중복됩니다."));
            }

            SerializedProperty patternEntries = phase.FindPropertyRelative("patterns");
            if (patternEntries == null || patternEntries.arraySize == 0)
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"{phaseLabel}: Pattern 목록이 비어 있습니다."));
                continue;
            }

            BossSequenceSelectionMode selectionMode = (BossSequenceSelectionMode)GetEnum(phase, "selectionMode");
            if (selectionMode == BossSequenceSelectionMode.RandomNoRepeat && patternEntries.arraySize < 2)
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"{phaseLabel}: RandomNoRepeat은 Pattern이 2개 이상일 때 의미가 있습니다."));
            }

            if (selectionMode == BossSequenceSelectionMode.WeightedRandom && !HasPositiveWeight(patternEntries))
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"{phaseLabel}: WeightedRandom에는 1 이상의 Pattern weight가 필요합니다."));
            }

            for (int entryIndex = 0; entryIndex < patternEntries.arraySize; entryIndex++)
            {
                SerializedProperty entry = patternEntries.GetArrayElementAtIndex(entryIndex);
                string patternId = GetString(entry, "patternId");
                if (string.IsNullOrWhiteSpace(patternId) || !patternIdCounts.ContainsKey(patternId))
                {
                    messages.Add(new BossGraphValidationMessage(MessageType.Error, $"{phaseLabel}: Pattern '{patternId}'를 찾을 수 없습니다."));
                }
            }
        }
    }

    private static void ValidateTransitions(
        SerializedProperty transitions,
        Dictionary<string, int> nodeIdCounts,
        Dictionary<string, int> nodeGuidCounts,
        List<BossGraphValidationMessage> messages)
    {
        if (transitions == null)
        {
            return;
        }

        HashSet<string> transitionKeys = new();
        for (int i = 0; i < transitions.arraySize; i++)
        {
            SerializedProperty transition = transitions.GetArrayElementAtIndex(i);
            string fromNodeId = GetString(transition, "fromNodeId");
            string toNodeId = GetString(transition, "toNodeId");
            string fromNodeGuid = GetString(transition, "fromNodeGuid");
            string toNodeGuid = GetString(transition, "toNodeGuid");
            string fromNodeKey = !string.IsNullOrWhiteSpace(fromNodeGuid) ? fromNodeGuid : fromNodeId;
            string toNodeKey = !string.IsNullOrWhiteSpace(toNodeGuid) ? toNodeGuid : toNodeId;
            Dictionary<string, int> fromNodeCounts = !string.IsNullOrWhiteSpace(fromNodeGuid) ? nodeGuidCounts : nodeIdCounts;
            Dictionary<string, int> toNodeCounts = !string.IsNullOrWhiteSpace(toNodeGuid) ? nodeGuidCounts : nodeIdCounts;
            string key = $"{fromNodeKey}->{toNodeKey}";

            if (string.IsNullOrWhiteSpace(fromNodeKey) || !fromNodeCounts.ContainsKey(fromNodeKey))
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Error, $"Transition {i + 1}: From Node '{fromNodeKey}'를 찾을 수 없습니다."));
            }

            if (string.IsNullOrWhiteSpace(toNodeKey) || !toNodeCounts.ContainsKey(toNodeKey))
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Error, $"Transition {i + 1}: To Node '{toNodeKey}'를 찾을 수 없습니다."));
            }

            if (!transitionKeys.Add(key))
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"Transition {i + 1}: '{key}' 연결이 중복됩니다."));
            }

            BossTransitionConditionType conditionType = (BossTransitionConditionType)GetEnum(transition, "conditionType");
            if (fromNodeKey == toNodeKey && conditionType != BossTransitionConditionType.SequenceEnded)
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"Transition {i + 1}: 자기 자신으로 즉시 전환하면 루프가 생길 수 있습니다."));
            }

            float threshold = GetFloat(transition, "threshold");
            int phaseIndex = GetInt(transition, "phaseIndex");
            if (conditionType == BossTransitionConditionType.HpRatioLessOrEqual
                && (threshold < 0f || threshold > 1f))
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"Transition {i + 1}: HP 비율 조건은 0~1 범위를 권장합니다."));
            }

            if ((conditionType == BossTransitionConditionType.PhaseIndexEquals
                    || conditionType == BossTransitionConditionType.EnragePhaseEquals
                    || conditionType == BossTransitionConditionType.LivesLessOrEqual)
                && phaseIndex < 0)
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"Transition {i + 1}: 정수 조건 값은 0 이상을 권장합니다."));
            }
        }
    }

    private static void RequirePath(
        SerializedProperty action,
        string propertyName,
        string label,
        List<BossGraphValidationMessage> messages)
    {
        if (string.IsNullOrWhiteSpace(GetString(action, propertyName)))
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"{label}: {propertyName}가 비어 있습니다."));
        }
    }

    private static void RequireString(
        SerializedProperty root,
        string propertyName,
        string label,
        List<BossGraphValidationMessage> messages)
    {
        if (string.IsNullOrWhiteSpace(GetString(root, propertyName)))
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"{label}: {propertyName}가 비어 있습니다."));
        }
    }

    private static void RequireArrayNotEmpty(
        SerializedProperty root,
        string propertyName,
        string label,
        List<BossGraphValidationMessage> messages)
    {
        SerializedProperty property = root?.FindPropertyRelative(propertyName);
        if (property == null || !property.isArray || property.arraySize == 0)
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"{label}: {propertyName}가 비어 있습니다."));
        }
    }

    private static void ValidateOriginSpec(
        SerializedProperty origin,
        string label,
        List<BossGraphValidationMessage> messages)
    {
        if (origin == null)
        {
            return;
        }

        BossGraphProjectileOriginMode mode = (BossGraphProjectileOriginMode)GetEnum(origin, "mode");
        switch (mode)
        {
            case BossGraphProjectileOriginMode.BossChild:
                RequirePath(origin, "bossChildPath", label, messages);
                break;
            case BossGraphProjectileOriginMode.BossChildList:
            case BossGraphProjectileOriginMode.AlternatingBossChildList:
                RequirePathList(origin.FindPropertyRelative("bossChildPaths"), $"{label}/bossChildPaths", messages);
                break;
            case BossGraphProjectileOriginMode.AlternatingBossChildren:
                RequireAnyPath(origin, "firstBossChildPath", "secondBossChildPath", label, messages);
                break;
        }
    }

    private static void RequireAnyPath(
        SerializedProperty root,
        string firstPropertyName,
        string secondPropertyName,
        string label,
        List<BossGraphValidationMessage> messages)
    {
        if (!string.IsNullOrWhiteSpace(GetString(root, firstPropertyName))
            || !string.IsNullOrWhiteSpace(GetString(root, secondPropertyName)))
        {
            return;
        }

        messages.Add(new BossGraphValidationMessage(
            MessageType.Warning,
            $"{label}: {firstPropertyName} 또는 {secondPropertyName} 중 하나가 필요합니다."));
    }

    private static void RequirePathList(
        SerializedProperty paths,
        string label,
        List<BossGraphValidationMessage> messages)
    {
        if (paths == null || !paths.isArray || paths.arraySize == 0)
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"{label}: 경로가 비어 있습니다."));
            return;
        }

        for (int i = 0; i < paths.arraySize; i++)
        {
            if (string.IsNullOrWhiteSpace(paths.GetArrayElementAtIndex(i).stringValue))
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"{label}[{i}]: 경로가 비어 있습니다."));
            }
        }
    }

    private static void RequireObject(
        SerializedProperty root,
        string propertyName,
        string label,
        List<BossGraphValidationMessage> messages)
    {
        SerializedProperty property = root?.FindPropertyRelative(propertyName);
        if (property == null || property.objectReferenceValue == null)
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Error, $"{label}: {propertyName} 참조가 비어 있습니다."));
        }
    }

    private static BossGraphActionCategoryAsset GetActionCategories(SerializedProperty references)
    {
        return references?.FindPropertyRelative("actionCategories")?.objectReferenceValue as BossGraphActionCategoryAsset;
    }

    private static void RequireReference(
        SerializedProperty root,
        string propertyName,
        string label,
        List<BossGraphValidationMessage> messages)
    {
        SerializedProperty property = root?.FindPropertyRelative(propertyName);
        if (property == null || property.objectReferenceValue == null)
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"{label}: {propertyName} 참조가 비어 있습니다."));
        }
    }

    private static string GetNodeLabel(SerializedProperty node, int index)
    {
        string nodeId = GetString(node, "nodeId");
        return string.IsNullOrWhiteSpace(nodeId) ? $"Node {index + 1}" : nodeId;
    }

    private static string GetString(SerializedProperty root, string childName)
    {
        SerializedProperty child = root?.FindPropertyRelative(childName);
        return child != null ? child.stringValue : string.Empty;
    }

    private static int GetInt(SerializedProperty root, string childName)
    {
        SerializedProperty child = root?.FindPropertyRelative(childName);
        return child != null ? child.intValue : 0;
    }

    private static int GetEnum(SerializedProperty root, string childName)
    {
        SerializedProperty child = root?.FindPropertyRelative(childName);
        return child != null ? child.enumValueIndex : 0;
    }

    private static float GetFloat(SerializedProperty root, string childName)
    {
        SerializedProperty child = root?.FindPropertyRelative(childName);
        return child != null ? child.floatValue : 0f;
    }
}
