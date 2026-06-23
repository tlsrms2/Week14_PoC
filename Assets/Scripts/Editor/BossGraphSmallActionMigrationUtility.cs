using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Week14.Enemy;

#pragma warning disable CS0618

internal static class BossGraphSmallActionMigrationUtility
{
    private const float NewNodeSpacingX = 260f;
    private static readonly BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [MenuItem("Tools/Boss Graph/Create Small Action Copy From Selection", true)]
    private static bool CanCreateSmallActionCopy()
    {
        return Selection.activeObject is BossGraphAsset;
    }

    [MenuItem("Tools/Boss Graph/Migrate Hog Graph To Small Actions", true)]
    private static bool CanMigrateHogGraphToSmallActions()
    {
        return CanCreateSmallActionCopy();
    }

    [MenuItem("Tools/Boss Graph/Migrate Hog Graph To Small Actions")]
    private static void MigrateHogGraphToSmallActions()
    {
        CreateSmallActionCopy();
    }

    [MenuItem("Tools/Boss Graph/Create Small Action Copy From Selection")]
    private static void CreateSmallActionCopy()
    {
        BossGraphAsset sourceGraph = Selection.activeObject as BossGraphAsset;
        if (sourceGraph == null)
        {
            EditorUtility.DisplayDialog("Boss Graph", "BossGraphAsset을 선택하세요.", "OK");
            return;
        }

        string sourcePath = AssetDatabase.GetAssetPath(sourceGraph);
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            EditorUtility.DisplayDialog("Boss Graph", "선택한 BossGraphAsset의 경로를 찾을 수 없습니다.", "OK");
            return;
        }

        string targetPath = GetMigrationCopyPath(sourcePath);
        if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
        {
            EditorUtility.DisplayDialog("Boss Graph", "BossGraphAsset 복사본 생성에 실패했습니다.", "OK");
            return;
        }

        AssetDatabase.ImportAsset(targetPath);
        BossGraphAsset targetGraph = AssetDatabase.LoadAssetAtPath<BossGraphAsset>(targetPath);
        if (targetGraph == null)
        {
            EditorUtility.DisplayDialog("Boss Graph", "복사된 BossGraphAsset을 불러오지 못했습니다.", "OK");
            return;
        }

        MigrationResult result = MigrateGraph(targetGraph);
        EditorUtility.SetDirty(targetGraph);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(targetPath);
        Selection.activeObject = targetGraph;
        LogMigrationResult(targetGraph, result);
        EditorUtility.DisplayDialog(
            "Boss Graph",
            $"{Path.GetFileName(targetPath)} 생성 완료\n직접 Action으로 변환된 노드: {result.MigratedNodeCount}\n정리된 미참조 Action: {result.RemovedUnusedActionCount}",
            "OK");
    }

    private static string GetMigrationCopyPath(string sourcePath)
    {
        string directory = Path.GetDirectoryName(sourcePath);
        string filename = Path.GetFileNameWithoutExtension(sourcePath);
        return AssetDatabase.GenerateUniqueAssetPath(Path.Combine(directory ?? "Assets", $"{filename}_SmallActions.asset"));
    }

    private static MigrationResult MigrateGraph(BossGraphAsset graph)
    {
        SerializedObject graphObject = new(graph);
        graphObject.Update();
        SerializedProperty stateNodes = graphObject.FindProperty("stateNodes");
        if (stateNodes == null)
        {
            return new MigrationResult();
        }

        HashSet<string> existingNodeIds = CollectExistingNodeIds(stateNodes);
        List<NodeMigrationPlan> plans = new();
        int originalNodeCount = stateNodes.arraySize;
        for (int i = 0; i < originalNodeCount; i++)
        {
            SerializedProperty node = stateNodes.GetArrayElementAtIndex(i);
            BossAction directAction = GetDirectAction(node);
            BossAction sourceAction = directAction ?? GetSingleAction(GetNodeActionAsset(node));
            if (sourceAction == null)
            {
                continue;
            }

            if (!TryBuildMigrationSteps(sourceAction, GetString(node, "nodeId", $"Node{i + 1}"), out List<ActionStep> steps))
            {
                if (directAction != null)
                {
                    continue;
                }

                BossAction clonedAction = CloneAction(sourceAction);
                if (clonedAction == null)
                {
                    continue;
                }

                BossGraphNodeKind nodeKind = (BossGraphNodeKind)GetEnum(node, "nodeKind", (int)BossGraphNodeKind.Attack);
                steps = new List<ActionStep> { new("Action", nodeKind, clonedAction) };
            }

            string nodeId = GetString(node, "nodeId", $"Node{i + 1}");
            List<string> replacementIds = new() { nodeId };
            for (int stepIndex = 1; stepIndex < steps.Count; stepIndex++)
            {
                replacementIds.Add(GetUniqueNodeId(existingNodeIds, $"{nodeId}_{steps[stepIndex].NodeIdSuffix}"));
            }

            plans.Add(new NodeMigrationPlan(i, nodeId, GetVector2(node, "editorPosition"), replacementIds, steps));
        }

        for (int i = 0; i < plans.Count; i++)
        {
            ApplyNodePlan(stateNodes, plans[i]);
        }

        UpdatePatternNodeIds(graphObject.FindProperty("patterns"), plans);
        UpdateTransitions(graphObject.FindProperty("transitions"), plans);
        SyncGuidReferences(graphObject);
        graphObject.ApplyModifiedProperties();
        int removedActionCount = RemoveUnreferencedActionAssets(graph, graphObject);
        return new MigrationResult(plans, removedActionCount);
    }

    private static void LogMigrationResult(BossGraphAsset graph, MigrationResult result)
    {
        if (graph == null || result == null)
        {
            return;
        }

        List<string> lines = new()
        {
            $"Boss Graph small-action migration: {graph.name}",
            $"Converted direct-action nodes: {result.MigratedNodeCount}",
            $"Removed unused Action sub-assets: {result.RemovedUnusedActionCount}"
        };

        for (int i = 0; i < result.Plans.Count; i++)
        {
            NodeMigrationPlan plan = result.Plans[i];
            lines.Add($"- {plan.OriginalNodeId} -> {string.Join(", ", plan.ReplacementNodeIds)}");
        }

        Debug.Log(string.Join("\n", lines), graph);
    }

    private static int RemoveUnreferencedActionAssets(BossGraphAsset graph, SerializedObject graphObject)
    {
        string graphPath = AssetDatabase.GetAssetPath(graph);
        if (string.IsNullOrWhiteSpace(graphPath))
        {
            return 0;
        }

        graphObject.Update();
        HashSet<BossGraphActionAsset> referencedActions = CollectReferencedActionAssets(graphObject.FindProperty("stateNodes"));
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(graphPath);
        int removedCount = 0;
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is not BossGraphActionAsset actionAsset
                || referencedActions.Contains(actionAsset)
                || AssetDatabase.GetAssetPath(actionAsset) != graphPath)
            {
                continue;
            }

            UnityEngine.Object.DestroyImmediate(actionAsset, true);
            removedCount++;
        }

        if (removedCount > 0)
        {
            EditorUtility.SetDirty(graph);
        }

        return removedCount;
    }

    private static HashSet<BossGraphActionAsset> CollectReferencedActionAssets(SerializedProperty stateNodes)
    {
        HashSet<BossGraphActionAsset> referencedActions = new();
        if (stateNodes == null)
        {
            return referencedActions;
        }

        for (int nodeIndex = 0; nodeIndex < stateNodes.arraySize; nodeIndex++)
        {
            SerializedProperty sequences = stateNodes.GetArrayElementAtIndex(nodeIndex).FindPropertyRelative("sequences");
            if (sequences == null)
            {
                continue;
            }

            for (int entryIndex = 0; entryIndex < sequences.arraySize; entryIndex++)
            {
                BossGraphActionAsset actionAsset = sequences.GetArrayElementAtIndex(entryIndex)
                    .FindPropertyRelative("sequence")?.objectReferenceValue as BossGraphActionAsset;
                if (actionAsset != null)
                {
                    referencedActions.Add(actionAsset);
                }
            }
        }

        return referencedActions;
    }

    private static void ApplyNodePlan(
        SerializedProperty stateNodes,
        NodeMigrationPlan plan)
    {
        SerializedProperty sourceNode = stateNodes.GetArrayElementAtIndex(plan.NodeIndex);
        int phaseIndex = GetInt(sourceNode, "phaseIndex", 0);
        int selectionMode = GetEnum(sourceNode, "selectionMode", 0);
        ApplyStepToNode(sourceNode, plan.Steps[0]);

        for (int i = 1; i < plan.Steps.Count; i++)
        {
            int index = stateNodes.arraySize;
            stateNodes.InsertArrayElementAtIndex(index);
            SerializedProperty node = stateNodes.GetArrayElementAtIndex(index);
            ClearStateNode(node);
            SetString(node, "nodeId", plan.ReplacementNodeIds[i]);
            SetString(node, "nodeGuid", Guid.NewGuid().ToString("N"));
            SetInt(node, "phaseIndex", phaseIndex);
            SetEnum(node, "selectionMode", selectionMode);
            SetVector2(node, "editorPosition", plan.EditorPosition + new Vector2(NewNodeSpacingX * i, 0f));
            ApplyStepToNode(node, plan.Steps[i]);
        }

    }

    private static void ApplyStepToNode(SerializedProperty node, ActionStep step)
    {
        SetEnum(node, "nodeKind", (int)step.NodeKind);
        SerializedProperty action = node.FindPropertyRelative("action");
        if (action != null)
        {
            action.managedReferenceValue = step.Action;
        }

        SerializedProperty sequences = node.FindPropertyRelative("sequences");
        sequences?.ClearArray();
    }

    private static bool TryBuildMigrationSteps(BossAction sourceAction, string nodeId, out List<ActionStep> steps)
    {
        steps = sourceAction switch
        {
            FireRotatingProjectilesAction action => BuildRotatingSteps(action),
            FireRadialProjectilesAction action => BuildRadialSteps(action),
            FireMachinegunProjectilesAction action => BuildMachinegunSteps(action),
            FireChargedRadialSplitProjectileAction action => BuildChargedSplitSteps(action, nodeId),
            FireSweepProjectilesAction action => BuildSweepSteps(action),
            FireFanVolleyProjectilesAction action => BuildFanVolleySteps(action),
            _ => null
        };

        return steps != null && steps.Count > 0;
    }

    private static List<ActionStep> BuildRadialSteps(FireRadialProjectilesAction source)
    {
        FireRadialEmissionAction action = new();
        CopyFields(source, action, "projectileName", "projectile", "bulletCount", "arcDegrees", "spawnRadius", "fireInterval", "fireSfxId", "effects", "cameraShakeDirection");
        if (!GetBoolField(source, "centerOnPlayer"))
        {
            SetAimFixedAngle(action, "aim", 0f);
        }

        return new List<ActionStep> { new("FireRadialEmission", BossGraphNodeKind.Attack, action) };
    }

    private static List<ActionStep> BuildRotatingSteps(FireRotatingProjectilesAction source)
    {
        FireRadialEmissionAction action = new();
        CopyFields(source, action, "projectileName", "projectile", "bulletCount", "spawnRadius", "fireInterval", "fireSfxId", "launchSfxId", "effects");
        SetFieldValue(action, "arcDegrees", 360f);
        SetFieldValue(action, "randomizeStartAngle", true);
        SetOriginFromTwoPaths(action, "origin", GetStringField(source, "firstProjectileOriginPath"), GetStringField(source, "secondProjectileOriginPath"), 0f);
        return new List<ActionStep> { new("FireRadialEmission", BossGraphNodeKind.Attack, action) };
    }

    private static List<ActionStep> BuildMachinegunSteps(FireMachinegunProjectilesAction source)
    {
        StartMoveTowardPlayerAction startMove = new();
        SetFieldValue(startMove, "speedMultiplier", GetFloatField(source, "moveSpeedMultiplier"));

        FireProjectileBurstAction burst = new();
        CopyFields(source, burst, "projectileName", "projectile", "fireSfxId", "launchSfxId", "effects");
        SetBurstVolleysFromMachinegun(source, burst);
        SetOriginFromTwoPaths(
            burst,
            "origin",
            GetStringField(source, "firstProjectileOriginPath"),
            GetStringField(source, "secondProjectileOriginPath"),
            GetFloatField(source, "spawnSpacing"));

        return new List<ActionStep>
        {
            new("StartMove", BossGraphNodeKind.Move, startMove),
            new("FireBurst", BossGraphNodeKind.Attack, burst),
            new("StopMove", BossGraphNodeKind.Move, new StopMovementAction())
        };
    }

    private static List<ActionStep> BuildChargedSplitSteps(FireChargedRadialSplitProjectileAction source, string nodeId)
    {
        string handleKey = $"{nodeId}_Projectile";

        SpawnChargedProjectileAction spawn = new();
        SetFieldValue(spawn, "handleKey", handleKey);
        CopyFields(source, spawn, "projectileName", "projectile", "projectileOriginPath", "projectileRadiusMultiplier", "aimSpreadDegrees", "launchSfxId", "effects");
        CopyField(source, spawn, "windupSeconds", "chargeSeconds");

        ConfigureProjectileGrowthAction growth = new();
        SetFieldValue(growth, "handleKey", handleKey);
        CopyFields(source, growth, "startScaleMultiplier", "finalScaleMultiplier");

        ConfigureRadialSplitAction split = new();
        SetFieldValue(split, "handleKey", handleKey);
        CopyFields(source, split, "radialSplitBulletCount", "radialSplitStartAngleOffset", "splitDelaySeconds", "splitSpeedMultiplier", "splitRadiusMultiplier", "splitLifetimeMultiplier");
        CopyField(source, split, "bombSfxLeadSeconds", "splitSfxLeadSeconds");
        CopyField(source, split, "radialSplitImminentSfxId", "splitImminentSfxId");

        WaitProjectileChargeEndAction wait = new();
        SetFieldValue(wait, "handleKey", handleKey);
        CopyFields(source, wait, "projectileOriginPath", "aimTrackingSeconds", "aimSpreadDegrees", "effects");

        return new List<ActionStep>
        {
            new("SpawnCharged", BossGraphNodeKind.Attack, spawn),
            new("Growth", BossGraphNodeKind.Attack, growth),
            new("RadialSplit", BossGraphNodeKind.Attack, split),
            new("WaitCharge", BossGraphNodeKind.Attack, wait)
        };
    }

    private static List<ActionStep> BuildSweepSteps(FireSweepProjectilesAction source)
    {
        WindupAction windup = new();
        CopyField(source, windup, "windupSeconds", "seconds");
        CopyField(source, windup, "effects");
        SetOriginSinglePath(windup, "effectOrigin", GetStringField(source, "projectileOriginPath"));

        FireSweepEmissionAction sweep = new();
        CopyFields(source, sweep, "projectileName", "projectile", "bulletCount", "fireInterval", "spawnSpacing", "sweepStepDegrees", "maxSweepAngle", "fireSfxId", "effects");
        SetOriginSinglePath(sweep, "origin", GetStringField(source, "projectileOriginPath"));

        return new List<ActionStep>
        {
            new("Windup", BossGraphNodeKind.Utility, windup),
            new("FireSweep", BossGraphNodeKind.Attack, sweep)
        };
    }

    private static List<ActionStep> BuildFanVolleySteps(FireFanVolleyProjectilesAction source)
    {
        WindupAction windup = new();
        CopyField(source, windup, "windupSeconds", "seconds");
        CopyField(source, windup, "effects");
        SetOriginSinglePath(windup, "effectOrigin", GetStringField(source, "projectileOriginPath"));

        FireFanEmissionAction fan = new();
        CopyField(source, fan, "normalProjectileName", "projectileName");
        CopyField(source, fan, "normalProjectile", "projectile");
        CopyField(source, fan, "normalVolleyCount", "volleyCount");
        CopyField(source, fan, "normalVolleyInterval", "volleyInterval");
        CopyField(source, fan, "fanAngleDegrees");
        CopyField(source, fan, "normalSpawnSpacing", "spawnSpacing");
        CopyField(source, fan, "normalVolleySfxId", "fireSfxId");
        CopyField(source, fan, "effects");
        SetFieldValue(fan, "projectilesPerVolley", 3);
        SetOriginSinglePath(fan, "origin", GetStringField(source, "projectileOriginPath"));

        List<ActionStep> steps = new()
        {
            new("Windup", BossGraphNodeKind.Utility, windup),
            new("FireFan", BossGraphNodeKind.Attack, fan)
        };

        int secondaryBulletCount = GetIntField(source, "secondaryBulletCount");
        if (secondaryBulletCount <= 0)
        {
            return steps;
        }

        FireProjectileBurstAction secondary = new();
        CopyField(source, secondary, "secondaryProjectileName", "projectileName");
        CopyField(source, secondary, "secondaryProjectile", "projectile");
        CopyField(source, secondary, "secondaryFireSfxId", "fireSfxId");
        CopyField(source, secondary, "secondaryLaunchSfxId", "launchSfxId");
        CopyField(source, secondary, "secondarySpawnForwardOffset", "spawnForwardOffset");
        CopyField(source, secondary, "effects");
        SetOriginList(secondary, "origin", GetStringListField(source, "secondaryOriginPaths"), GetStringField(source, "projectileOriginPath"));
        SetSingleVolley(secondary, secondaryBulletCount, 0f, 0f);
        steps.Add(new ActionStep("FireSecondary", BossGraphNodeKind.Attack, secondary));
        return steps;
    }

    private static void UpdatePatternNodeIds(SerializedProperty patterns, IReadOnlyList<NodeMigrationPlan> plans)
    {
        if (patterns == null || plans.Count == 0)
        {
            return;
        }

        Dictionary<string, List<string>> replacements = BuildReplacementMap(plans);
        for (int patternIndex = 0; patternIndex < patterns.arraySize; patternIndex++)
        {
            SerializedProperty nodeIds = patterns.GetArrayElementAtIndex(patternIndex).FindPropertyRelative("nodeIds");
            if (nodeIds == null)
            {
                continue;
            }

            List<string> nextNodeIds = new();
            for (int nodeIndex = 0; nodeIndex < nodeIds.arraySize; nodeIndex++)
            {
                string nodeId = nodeIds.GetArrayElementAtIndex(nodeIndex).stringValue;
                if (replacements.TryGetValue(nodeId, out List<string> replacementIds))
                {
                    nextNodeIds.AddRange(replacementIds);
                }
                else
                {
                    nextNodeIds.Add(nodeId);
                }
            }

            nodeIds.ClearArray();
            for (int i = 0; i < nextNodeIds.Count; i++)
            {
                nodeIds.InsertArrayElementAtIndex(i);
                nodeIds.GetArrayElementAtIndex(i).stringValue = nextNodeIds[i];
            }
        }
    }

    private static void UpdateTransitions(SerializedProperty transitions, IReadOnlyList<NodeMigrationPlan> plans)
    {
        if (transitions == null || plans.Count == 0)
        {
            return;
        }

        Dictionary<string, List<string>> replacements = BuildReplacementMap(plans);
        for (int i = 0; i < transitions.arraySize; i++)
        {
            SerializedProperty transition = transitions.GetArrayElementAtIndex(i);
            string fromNodeId = GetString(transition, "fromNodeId", string.Empty);
            if (replacements.TryGetValue(fromNodeId, out List<string> replacementIds))
            {
                SetString(transition, "fromNodeId", replacementIds[^1]);
            }
        }

        for (int planIndex = 0; planIndex < plans.Count; planIndex++)
        {
            List<string> nodeIds = plans[planIndex].ReplacementNodeIds;
            for (int i = 0; i < nodeIds.Count - 1; i++)
            {
                AddTransition(transitions, nodeIds[i], nodeIds[i + 1]);
            }
        }
    }

    private static void AddTransition(SerializedProperty transitions, string fromNodeId, string toNodeId)
    {
        int index = transitions.arraySize;
        transitions.InsertArrayElementAtIndex(index);
        SerializedProperty transition = transitions.GetArrayElementAtIndex(index);
        SetString(transition, "fromNodeId", fromNodeId);
        SetString(transition, "toNodeId", toNodeId);
        SetEnum(transition, "conditionType", (int)BossTransitionConditionType.SequenceEnded);
        SetFloat(transition, "threshold", 0f);
        SetInt(transition, "phaseIndex", 0);
    }

    private static void SyncGuidReferences(SerializedObject graphObject)
    {
        Dictionary<string, string> nodeIdToGuid = BuildNodeIdToGuidMap(graphObject.FindProperty("stateNodes"));
        if (nodeIdToGuid.Count == 0)
        {
            return;
        }

        SerializedProperty transitions = graphObject.FindProperty("transitions");
        if (transitions != null)
        {
            for (int i = 0; i < transitions.arraySize; i++)
            {
                SerializedProperty transition = transitions.GetArrayElementAtIndex(i);
                SetGuidFromNodeId(transition, "fromNodeId", "fromNodeGuid", nodeIdToGuid);
                SetGuidFromNodeId(transition, "toNodeId", "toNodeGuid", nodeIdToGuid);
            }
        }

        SerializedProperty patterns = graphObject.FindProperty("patterns");
        if (patterns == null)
        {
            return;
        }

        for (int patternIndex = 0; patternIndex < patterns.arraySize; patternIndex++)
        {
            SerializedProperty pattern = patterns.GetArrayElementAtIndex(patternIndex);
            SerializedProperty nodeIds = pattern.FindPropertyRelative("nodeIds");
            SerializedProperty nodeGuids = pattern.FindPropertyRelative("nodeGuids");
            if (nodeIds == null || nodeGuids == null)
            {
                continue;
            }

            nodeGuids.ClearArray();
            for (int nodeIndex = 0; nodeIndex < nodeIds.arraySize; nodeIndex++)
            {
                string nodeId = nodeIds.GetArrayElementAtIndex(nodeIndex).stringValue;
                if (string.IsNullOrWhiteSpace(nodeId) || !nodeIdToGuid.TryGetValue(nodeId, out string nodeGuid))
                {
                    continue;
                }

                int guidIndex = nodeGuids.arraySize;
                nodeGuids.InsertArrayElementAtIndex(guidIndex);
                nodeGuids.GetArrayElementAtIndex(guidIndex).stringValue = nodeGuid;
            }
        }
    }

    private static Dictionary<string, string> BuildNodeIdToGuidMap(SerializedProperty stateNodes)
    {
        Dictionary<string, string> nodeIdToGuid = new(StringComparer.Ordinal);
        if (stateNodes == null)
        {
            return nodeIdToGuid;
        }

        for (int i = 0; i < stateNodes.arraySize; i++)
        {
            SerializedProperty node = stateNodes.GetArrayElementAtIndex(i);
            string nodeId = GetString(node, "nodeId", string.Empty);
            string nodeGuid = GetString(node, "nodeGuid", string.Empty);
            if (!string.IsNullOrWhiteSpace(nodeId) && !string.IsNullOrWhiteSpace(nodeGuid))
            {
                nodeIdToGuid[nodeId] = nodeGuid;
            }
        }

        return nodeIdToGuid;
    }

    private static void SetGuidFromNodeId(
        SerializedProperty root,
        string nodeIdPropertyName,
        string nodeGuidPropertyName,
        IReadOnlyDictionary<string, string> nodeIdToGuid)
    {
        string nodeId = GetString(root, nodeIdPropertyName, string.Empty);
        SerializedProperty nodeGuid = root.FindPropertyRelative(nodeGuidPropertyName);
        if (nodeGuid != null && !string.IsNullOrWhiteSpace(nodeId) && nodeIdToGuid.TryGetValue(nodeId, out string nextGuid))
        {
            nodeGuid.stringValue = nextGuid;
        }
    }

    private static Dictionary<string, List<string>> BuildReplacementMap(IReadOnlyList<NodeMigrationPlan> plans)
    {
        Dictionary<string, List<string>> replacements = new();
        for (int i = 0; i < plans.Count; i++)
        {
            replacements[plans[i].OriginalNodeId] = plans[i].ReplacementNodeIds;
        }

        return replacements;
    }

    private static void SetSingleVolley(FireProjectileBurstAction action, int bulletCount, float fireInterval, float restSeconds)
    {
        Type volleyType = typeof(FireProjectileBurstAction).GetNestedType("Volley", BindingFlags.Public);
        object volley = Activator.CreateInstance(volleyType);
        SetFieldValue(volley, "bulletCount", bulletCount);
        SetFieldValue(volley, "fireInterval", fireInterval);
        SetFieldValue(volley, "restSeconds", restSeconds);
        IList volleys = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(volleyType));
        volleys.Add(volley);
        SetFieldValue(action, "volleys", volleys);
    }

    private static void SetBurstVolleysFromMachinegun(FireMachinegunProjectilesAction source, FireProjectileBurstAction target)
    {
        Type targetVolleyType = typeof(FireProjectileBurstAction).GetNestedType("Volley", BindingFlags.Public);
        IList targetVolleys = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(targetVolleyType));
        if (GetFieldValue(source, "volleys") is IList sourceVolleys)
        {
            for (int i = 0; i < sourceVolleys.Count; i++)
            {
                object sourceVolley = sourceVolleys[i];
                if (sourceVolley == null)
                {
                    continue;
                }

                object targetVolley = Activator.CreateInstance(targetVolleyType);
                SetFieldValue(targetVolley, "bulletCount", GetIntField(sourceVolley, "bulletCount"));
                SetFieldValue(targetVolley, "fireInterval", GetFloatField(sourceVolley, "fireInterval"));
                SetFieldValue(targetVolley, "restSeconds", GetFloatField(sourceVolley, "restSeconds"));
                targetVolleys.Add(targetVolley);
            }
        }

        SetFieldValue(target, "volleys", targetVolleys);
    }

    private static void SetOriginSinglePath(object action, string fieldName, string path)
    {
        object origin = GetFieldValue(action, fieldName) ?? new BossGraphProjectileOriginSpec();
        if (string.IsNullOrWhiteSpace(path))
        {
            SetFieldValue(origin, "mode", BossGraphProjectileOriginMode.BossOrigin);
        }
        else
        {
            SetFieldValue(origin, "mode", BossGraphProjectileOriginMode.BossChild);
            SetFieldValue(origin, "bossChildPath", path);
        }

        SetFieldValue(action, fieldName, origin);
    }

    private static void SetAimFixedAngle(object action, string fieldName, float angleDegrees)
    {
        object aim = GetFieldValue(action, fieldName) ?? new BossGraphProjectileAimSpec();
        SetFieldValue(aim, "mode", BossGraphProjectileAimMode.FixedAngle);
        SetFieldValue(aim, "angleDegrees", angleDegrees);
        SetFieldValue(action, fieldName, aim);
    }

    private static void SetOriginFromTwoPaths(object action, string fieldName, string firstPath, string secondPath, float fallbackSpacing)
    {
        object origin = GetFieldValue(action, fieldName) ?? new BossGraphProjectileOriginSpec();
        bool hasFirst = !string.IsNullOrWhiteSpace(firstPath);
        bool hasSecond = !string.IsNullOrWhiteSpace(secondPath);
        if (hasFirst && hasSecond)
        {
            SetFieldValue(origin, "mode", BossGraphProjectileOriginMode.AlternatingBossChildren);
            SetFieldValue(origin, "firstBossChildPath", firstPath);
            SetFieldValue(origin, "secondBossChildPath", secondPath);
        }
        else if (hasFirst || hasSecond)
        {
            SetFieldValue(origin, "mode", BossGraphProjectileOriginMode.BossChild);
            SetFieldValue(origin, "bossChildPath", hasFirst ? firstPath : secondPath);
        }
        else
        {
            SetFieldValue(origin, "mode", BossGraphProjectileOriginMode.BossOrigin);
        }

        SetFieldValue(origin, "fallbackSpacing", fallbackSpacing);
        SetFieldValue(action, fieldName, origin);
    }

    private static void SetOriginList(object action, string fieldName, List<string> paths, string fallbackPath)
    {
        object origin = GetFieldValue(action, fieldName) ?? new BossGraphProjectileOriginSpec();
        if (paths != null && paths.Count > 0)
        {
            SetFieldValue(origin, "mode", BossGraphProjectileOriginMode.BossChildList);
            SetFieldValue(origin, "bossChildPaths", new List<string>(paths));
        }
        else
        {
            SetFieldValue(origin, "mode", BossGraphProjectileOriginMode.BossChild);
            SetFieldValue(origin, "bossChildPath", fallbackPath);
        }

        SetFieldValue(action, fieldName, origin);
    }

    private static void CopyFields(object source, object target, params string[] fieldNames)
    {
        for (int i = 0; i < fieldNames.Length; i++)
        {
            CopyField(source, target, fieldNames[i]);
        }
    }

    private static void CopyField(object source, object target, string sourceFieldName, string targetFieldName = null)
    {
        FieldInfo sourceField = GetField(source.GetType(), sourceFieldName);
        if (sourceField == null)
        {
            return;
        }

        SetFieldValue(target, targetFieldName ?? sourceFieldName, CloneValue(sourceField.GetValue(source)));
    }

    private static object CloneValue(object value)
    {
        if (value == null)
        {
            return null;
        }

        Type type = value.GetType();
        if (type.IsValueType || value is string || value is UnityEngine.Object)
        {
            return value;
        }

        if (value is IList list)
        {
            IList clone = (IList)Activator.CreateInstance(type);
            for (int i = 0; i < list.Count; i++)
            {
                clone.Add(CloneValue(list[i]));
            }

            return clone;
        }

        object instance = Activator.CreateInstance(type);
        EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(value), instance);
        return instance;
    }

    private static FieldInfo GetField(Type type, string fieldName)
    {
        for (Type current = type; current != null; current = current.BaseType)
        {
            FieldInfo field = current.GetField(fieldName, FieldFlags);
            if (field != null)
            {
                return field;
            }
        }

        return null;
    }

    private static object GetFieldValue(object target, string fieldName)
    {
        return target == null ? null : GetField(target.GetType(), fieldName)?.GetValue(target);
    }

    private static void SetFieldValue(object target, string fieldName, object value)
    {
        FieldInfo field = target == null ? null : GetField(target.GetType(), fieldName);
        field?.SetValue(target, value);
    }

    private static string GetStringField(object target, string fieldName)
    {
        return GetFieldValue(target, fieldName) as string;
    }

    private static int GetIntField(object target, string fieldName)
    {
        return GetFieldValue(target, fieldName) is int value ? value : 0;
    }

    private static float GetFloatField(object target, string fieldName)
    {
        return GetFieldValue(target, fieldName) is float value ? value : 0f;
    }

    private static bool GetBoolField(object target, string fieldName)
    {
        return GetFieldValue(target, fieldName) is bool value && value;
    }

    private static List<string> GetStringListField(object target, string fieldName)
    {
        return GetFieldValue(target, fieldName) is List<string> list ? list : null;
    }

    private static BossGraphActionAsset GetNodeActionAsset(SerializedProperty node)
    {
        SerializedProperty sequences = node.FindPropertyRelative("sequences");
        if (sequences == null || sequences.arraySize == 0)
        {
            return null;
        }

        return sequences.GetArrayElementAtIndex(0)
            .FindPropertyRelative("sequence")?.objectReferenceValue as BossGraphActionAsset;
    }

    private static BossAction GetSingleAction(BossGraphActionAsset actionAsset)
    {
        return actionAsset?.Action;
    }

    private static BossAction GetDirectAction(SerializedProperty node)
    {
        return node.FindPropertyRelative("action")?.managedReferenceValue as BossAction;
    }

    private static BossAction CloneAction(BossAction action)
    {
        return CloneValue(action) as BossAction;
    }

    private static void ClearStateNode(SerializedProperty node)
    {
        SerializedProperty action = node.FindPropertyRelative("action");
        if (action != null)
        {
            action.managedReferenceValue = null;
        }

        SerializedProperty sequences = node.FindPropertyRelative("sequences");
        sequences?.ClearArray();

        SetString(node, "nodeId", string.Empty);
        SetString(node, "nodeGuid", string.Empty);
        SetEnum(node, "nodeKind", (int)BossGraphNodeKind.Attack);
        SetInt(node, "phaseIndex", 0);
        SetEnum(node, "selectionMode", 0);
    }

    private static HashSet<string> CollectExistingNodeIds(SerializedProperty stateNodes)
    {
        HashSet<string> nodeIds = new(StringComparer.Ordinal);
        for (int i = 0; i < stateNodes.arraySize; i++)
        {
            string nodeId = GetString(stateNodes.GetArrayElementAtIndex(i), "nodeId", string.Empty);
            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                nodeIds.Add(nodeId);
            }
        }

        return nodeIds;
    }

    private static string GetUniqueNodeId(HashSet<string> existingNodeIds, string baseNodeId)
    {
        string nodeId = baseNodeId;
        int suffix = 2;
        while (!existingNodeIds.Add(nodeId))
        {
            nodeId = $"{baseNodeId}_{suffix:00}";
            suffix++;
        }

        return nodeId;
    }

    private static string GetString(SerializedProperty root, string childName, string fallback)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        return child != null && !string.IsNullOrWhiteSpace(child.stringValue) ? child.stringValue : fallback;
    }

    private static int GetInt(SerializedProperty root, string childName, int fallback)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        return child != null ? child.intValue : fallback;
    }

    private static int GetEnum(SerializedProperty root, string childName, int fallback)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        return child != null ? child.enumValueIndex : fallback;
    }

    private static Vector2 GetVector2(SerializedProperty root, string childName)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        return child != null ? child.vector2Value : Vector2.zero;
    }

    private static void SetString(SerializedProperty root, string childName, string value)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child != null)
        {
            child.stringValue = value;
        }
    }

    private static void SetInt(SerializedProperty root, string childName, int value)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child != null)
        {
            child.intValue = value;
        }
    }

    private static void SetFloat(SerializedProperty root, string childName, float value)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child != null)
        {
            child.floatValue = value;
        }
    }

    private static void SetEnum(SerializedProperty root, string childName, int value)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child != null)
        {
            child.enumValueIndex = value;
        }
    }

    private static void SetVector2(SerializedProperty root, string childName, Vector2 value)
    {
        SerializedProperty child = root.FindPropertyRelative(childName);
        if (child != null)
        {
            child.vector2Value = value;
        }
    }

    private sealed class NodeMigrationPlan
    {
        public NodeMigrationPlan(int nodeIndex, string originalNodeId, Vector2 editorPosition, List<string> replacementNodeIds, List<ActionStep> steps)
        {
            NodeIndex = nodeIndex;
            OriginalNodeId = originalNodeId;
            EditorPosition = editorPosition;
            ReplacementNodeIds = replacementNodeIds;
            Steps = steps;
        }

        public int NodeIndex { get; }
        public string OriginalNodeId { get; }
        public Vector2 EditorPosition { get; }
        public List<string> ReplacementNodeIds { get; }
        public List<ActionStep> Steps { get; }
    }

    private sealed class MigrationResult
    {
        public MigrationResult()
            : this(new List<NodeMigrationPlan>(), 0)
        {
        }

        public MigrationResult(List<NodeMigrationPlan> plans, int removedUnusedActionCount)
        {
            Plans = plans ?? new List<NodeMigrationPlan>();
            RemovedUnusedActionCount = Mathf.Max(0, removedUnusedActionCount);
        }

        public List<NodeMigrationPlan> Plans { get; }
        public int MigratedNodeCount => Plans.Count;
        public int RemovedUnusedActionCount { get; }
    }

    private readonly struct ActionStep
    {
        public ActionStep(string nodeIdSuffix, BossGraphNodeKind nodeKind, BossAction action)
        {
            NodeIdSuffix = nodeIdSuffix;
            NodeKind = nodeKind;
            Action = action;
        }

        public string NodeIdSuffix { get; }
        public BossGraphNodeKind NodeKind { get; }
        public BossAction Action { get; }
    }
}
