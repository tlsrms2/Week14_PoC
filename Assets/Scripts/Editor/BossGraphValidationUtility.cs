using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Week14.Enemy;

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
        SerializedProperty transitions = graphObject.FindProperty("transitions");
        SerializedProperty startNodeId = graphObject.FindProperty("startNodeId");
        SerializedProperty references = graphObject.FindProperty("references");

        ValidateReferences(references, messages);
        Dictionary<string, int> nodeIdCounts = CollectNodeIds(stateNodes, messages);
        ValidateStartNode(startNodeId, nodeIdCounts, messages);
        ValidateStateNodes(stateNodes, transitions, messages);
        ValidateTransitions(transitions, nodeIdCounts, messages);
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

    private static void ValidateReferences(
        SerializedProperty references,
        List<BossGraphValidationMessage> messages)
    {
        if (references == null)
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Warning, "Graph References가 없습니다."));
            return;
        }

        RequireReference(references, "effectData", "Graph References", messages);
        RequireReference(references, "colorSettings", "Graph References", messages);
    }

    private static Dictionary<string, int> CollectNodeIds(
        SerializedProperty stateNodes,
        List<BossGraphValidationMessage> messages)
    {
        Dictionary<string, int> nodeIdCounts = new();
        if (stateNodes == null || stateNodes.arraySize == 0)
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Error, "State Node가 없습니다."));
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

    private static void ValidateStartNode(
        SerializedProperty startNodeId,
        Dictionary<string, int> nodeIdCounts,
        List<BossGraphValidationMessage> messages)
    {
        string id = startNodeId != null ? startNodeId.stringValue : string.Empty;
        if (string.IsNullOrWhiteSpace(id))
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Warning, "startNodeId가 비어 있습니다. 첫 노드로 fallback됩니다."));
            return;
        }

        if (!nodeIdCounts.ContainsKey(id))
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Error, $"startNodeId '{id}'와 일치하는 노드가 없습니다."));
        }
    }

    private static void ValidateStateNodes(
        SerializedProperty stateNodes,
        SerializedProperty transitions,
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
            SerializedProperty sequences = node.FindPropertyRelative("sequences");
            if (sequences == null || sequences.arraySize == 0)
            {
                if (HasOutgoingTransition(transitions, GetString(node, "nodeId")))
                {
                    continue;
                }

                messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"{nodeLabel}: Sequence가 없습니다."));
                continue;
            }

            ValidateSequenceSelectionSettings(node, sequences, nodeLabel, messages);

            for (int entryIndex = 0; entryIndex < sequences.arraySize; entryIndex++)
            {
                SerializedProperty entry = sequences.GetArrayElementAtIndex(entryIndex);
                SerializedProperty sequenceProperty = entry.FindPropertyRelative("sequence");
                AttackSequenceAsset sequence = sequenceProperty?.objectReferenceValue as AttackSequenceAsset;
                if (sequence == null)
                {
                    messages.Add(new BossGraphValidationMessage(MessageType.Error, $"{nodeLabel}: Sequence {entryIndex + 1} 참조가 비어 있습니다."));
                    continue;
                }

                ValidateSequence(sequence, $"{nodeLabel}/{sequence.name}", messages);
            }
        }
    }

    private static void ValidateSequenceSelectionSettings(
        SerializedProperty node,
        SerializedProperty sequences,
        string nodeLabel,
        List<BossGraphValidationMessage> messages)
    {
        BossSequenceSelectionMode selectionMode = (BossSequenceSelectionMode)GetEnum(node, "selectionMode");
        if (selectionMode == BossSequenceSelectionMode.RandomNoRepeat && sequences.arraySize < 2)
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"{nodeLabel}: RandomNoRepeat은 Sequence가 2개 이상일 때 의미가 있습니다."));
        }

        if (selectionMode == BossSequenceSelectionMode.WeightedRandom && !HasPositiveSequenceWeight(sequences))
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"{nodeLabel}: WeightedRandom은 weight가 1 이상인 Sequence가 필요합니다."));
        }
    }

    private static bool HasPositiveSequenceWeight(SerializedProperty sequences)
    {
        if (sequences == null)
        {
            return false;
        }

        for (int i = 0; i < sequences.arraySize; i++)
        {
            SerializedProperty entry = sequences.GetArrayElementAtIndex(i);
            if (GetInt(entry, "weight") > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateSequence(
        AttackSequenceAsset sequence,
        string label,
        List<BossGraphValidationMessage> messages)
    {
        SerializedObject sequenceObject = new(sequence);
        SerializedProperty actions = sequenceObject.FindProperty("actions");
        if (actions == null || actions.arraySize == 0)
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"{label}: Action이 없습니다."));
            return;
        }

        for (int actionIndex = 0; actionIndex < actions.arraySize; actionIndex++)
        {
            SerializedProperty action = actions.GetArrayElementAtIndex(actionIndex);
            object actionValue = action.managedReferenceValue;
            string actionLabel = $"{label}/Action {actionIndex + 1}";
            if (actionValue == null)
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Error, $"{actionLabel}: Action 타입이 비어 있습니다."));
                continue;
            }

            ValidateAction(action, actionValue, actionLabel, messages);
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
            case SetTelegraphAction:
                RequirePath(action, "telegraphPath", label, messages);
                break;
            case FireFanVolleyProjectilesAction:
                RequirePath(action, "firePointPath", label, messages);
                RequirePath(action, "projectileOriginPath", label, messages);
                RequireProjectile(action, "normalProjectile", label, messages);
                if (GetInt(action, "secondaryBulletCount") > 0)
                {
                    RequireProjectile(action, "secondaryProjectile", label, messages);
                }
                break;
            case FireChargedRadialSplitProjectileAction:
            case FireSweepProjectilesAction:
                RequirePath(action, "firePointPath", label, messages);
                RequirePath(action, "projectileOriginPath", label, messages);
                RequireProjectile(action, "projectile", label, messages);
                break;
            case FireMachinegunProjectilesAction:
                RequireArrayNotEmpty(action, "volleys", label, messages);
                RequireProjectile(action, "projectile", label, messages);
                break;
            case FireProjectileAction:
            case FireRadialProjectilesAction:
            case FireRotatingProjectilesAction:
                RequireProjectile(action, "projectile", label, messages);
                break;
            case SpawnPrefabAction:
                RequireObject(action, "prefab", label, messages);
                break;
            case CustomEventAction:
                RequireString(action, "methodName", label, messages);
                break;
        }
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

    private static void ValidateTransitions(
        SerializedProperty transitions,
        Dictionary<string, int> nodeIdCounts,
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
            string key = $"{fromNodeId}->{toNodeId}";

            if (string.IsNullOrWhiteSpace(fromNodeId) || !nodeIdCounts.ContainsKey(fromNodeId))
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Error, $"Transition {i + 1}: From Node '{fromNodeId}'를 찾을 수 없습니다."));
            }

            if (string.IsNullOrWhiteSpace(toNodeId) || !nodeIdCounts.ContainsKey(toNodeId))
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Error, $"Transition {i + 1}: To Node '{toNodeId}'를 찾을 수 없습니다."));
            }

            if (!transitionKeys.Add(key))
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"Transition {i + 1}: '{key}' 연결이 중복됩니다."));
            }

            BossTransitionConditionType conditionType = (BossTransitionConditionType)GetEnum(transition, "conditionType");
            if (fromNodeId == toNodeId && conditionType != BossTransitionConditionType.SequenceEnded)
            {
                messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"Transition {i + 1}: 같은 노드로 돌아가는 즉시 전환은 루프를 만들 수 있습니다."));
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

    private static void RequireProjectile(
        SerializedProperty action,
        string propertyName,
        string label,
        List<BossGraphValidationMessage> messages)
    {
        SerializedProperty projectile = action.FindPropertyRelative(propertyName);
        RequireObject(projectile, "prefab", $"{label}/{propertyName}", messages);
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

    private static void RequireReference(
        SerializedProperty root,
        string propertyName,
        string label,
        List<BossGraphValidationMessage> messages)
    {
        SerializedProperty property = root?.FindPropertyRelative(propertyName);
        if (property == null || property.objectReferenceValue == null)
        {
            messages.Add(new BossGraphValidationMessage(MessageType.Warning, $"{label}: {propertyName} 참조가 비어 있습니다. 보스 컴포넌트 fallback 값이 사용될 수 있습니다."));
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
