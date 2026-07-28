using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Week14.Combat;
using Week14.Enemy;

public static class HackerExecutionSceneSetup
{
    private const string ScenePath =
        "Assets/Scenes/Boss/Boss_Hacker.unity";
    private const string AutoSetupSessionKey =
        "Week14.HackerExecutionSceneSetup.V7";

    [InitializeOnLoadMethod]
    private static void RegisterAutoSetup()
    {
        EditorApplication.delayCall += TryConfigureOnce;
    }

    [MenuItem(
        "Tools/Week14/Hacker Execution/Configure Boss_Hacker Scene")]
    public static void ConfigureFromMenu()
    {
        ConfigureSceneAsset(true);
    }

    private static void TryConfigureOnce()
    {
        if (SessionState.GetBool(AutoSetupSessionKey, false)
            || EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling
            || EditorApplication.isUpdating)
        {
            return;
        }

        if (ConfigureSceneAsset(false))
        {
            SessionState.SetBool(AutoSetupSessionKey, true);
        }
    }

    private static bool ConfigureSceneAsset(bool showDialogOnDirty)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
        if (alreadyLoaded && scene.isDirty)
        {
            if (showDialogOnDirty)
            {
                EditorUtility.DisplayDialog(
                    "Hacker 처형 씬 설정",
                    "Boss_Hacker 씬에 저장하지 않은 변경이 있습니다. " +
                    "씬을 저장한 뒤 다시 실행하세요.",
                    "확인");
            }

            return false;
        }

        if (!alreadyLoaded)
        {
            scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Additive);
        }

        try
        {
            ConfigureLoadedScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            return EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (!alreadyLoaded && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void ConfigureLoadedScene(Scene scene)
    {
        HackerBossAI hacker =
            FindComponentInScene<HackerBossAI>(scene);
        if (hacker == null)
        {
            throw new InvalidOperationException(
                "Boss_Hacker 씬에서 HackerBossAI를 찾지 못했습니다.");
        }

        BossExecutionStage stage =
            FindComponentInScene<BossExecutionStage>(scene);
        bool createdStage = stage == null;
        if (createdStage)
        {
            GameObject stageObject =
                new("HackerExecutionStage");
            SceneManager.MoveGameObjectToScene(stageObject, scene);
            stage = stageObject.AddComponent<BossExecutionStage>();
        }

        Transform playerStart =
            GetOrCreateAnchor(stage.transform, "PlayerStart");
        Transform bossPosition =
            GetOrCreateAnchor(stage.transform, "BossPosition");
        Transform playerRollEnd =
            GetOrCreateAnchor(stage.transform, "PlayerRollEnd");
        Transform wideCameraFocus =
            GetOrCreateAnchor(stage.transform, "WideCameraFocus");
        Transform projectileFocusProxy =
            GetOrCreateAnchor(stage.transform, "ProjectileFocusProxy");
        if (createdStage)
        {
            playerStart.localPosition = Vector3.zero;
            bossPosition.localPosition =
                new Vector3(6.2f, 0f, 0f);
            playerRollEnd.localPosition =
                new Vector3(3.3f, 0f, 0f);
            wideCameraFocus.localPosition =
                new Vector3(3.1f, 0f, 0f);
            projectileFocusProxy.localPosition =
                Vector3.zero;
        }

        SerializedObject serializedStage = new(stage);
        SetObject(serializedStage, "playerStart", playerStart);
        SetObject(serializedStage, "bossPosition", bossPosition);
        SetObject(serializedStage, "playerRollEnd", playerRollEnd);
        SetObject(serializedStage, "wideCameraFocus", wideCameraFocus);
        SetObject(
            serializedStage,
            "projectileFocusProxy",
            projectileFocusProxy);
        serializedStage.ApplyModifiedPropertiesWithoutUndo();

        HackerExecutionSequence sequence =
            FindComponentInScene<HackerExecutionSequence>(scene);
        sequence ??=
            GetOrAddComponent<HackerExecutionSequence>(
                stage.gameObject);
        ExecutionChargeGatherVfx chargeVfx =
            GetOrAddComponent<ExecutionChargeGatherVfx>(
                stage.gameObject);
        HackerExecutionParticleGatherVfx particleVfx =
            GetOrAddComponent<HackerExecutionParticleGatherVfx>(
                stage.gameObject);

        SerializedObject serializedSequence = new(sequence);
        SetObject(serializedSequence, "stage", stage);
        SetObject(
            serializedSequence,
            "patternSourceBoss",
            hacker);
        SetObject(
            serializedSequence,
            "aimChargeGatherVfx",
            chargeVfx);
        SetObject(
            serializedSequence,
            "particleGatherVfx",
            particleVfx);
        serializedSequence.FindProperty(
            "executionPatternId").stringValue = "Execution";
        serializedSequence.ApplyModifiedPropertiesWithoutUndo();

        ApplySequenceSettings(sequence);
        ApplyChargeVfxSettings(chargeVfx);
        ApplyParticleVfxSettings(particleVfx);
        EditorUtility.SetDirty(stage);
        EditorUtility.SetDirty(sequence);
        EditorUtility.SetDirty(chargeVfx);
        EditorUtility.SetDirty(particleVfx);
    }

    private static void ApplyChargeVfxSettings(
        ExecutionChargeGatherVfx chargeVfx)
    {
        SerializedObject serializedVfx = new(chargeVfx);
        SetColor(
            serializedVfx,
            "gatheredColor",
            new Color(0.2f, 0.6509804f, 1f, 1f));
        SetFloat(serializedVfx, "lineWidth", 0.055f);
        SetInt(serializedVfx, "sortingOrder", 76);
        SetBool(serializedVfx, "sortAboveMuzzleOwner", true);
        SetInt(serializedVfx, "ownerSortingOrderOffset", 30);
        SetBool(serializedVfx, "showMuzzleCircle", false);
        SetFloat(serializedVfx, "startCircleRadius", 0.025f);
        SetFloat(serializedVfx, "maxCircleRadius", 0.42f);
        SetFloat(serializedVfx, "circleLineWidth", 0.09f);
        SetInt(serializedVfx, "circleSegments", 40);
        SetInt(serializedVfx, "lineCount", 26);
        SetFloat(serializedVfx, "emissionSeconds", 2.4f);
        SetFloat(serializedVfx, "travelSeconds", 0.65f);
        SetFloat(serializedVfx, "minSpawnDistance", 0.55f);
        SetFloat(serializedVfx, "maxSpawnDistance", 2f);
        SetFloat(serializedVfx, "minLineLength", 0.18f);
        SetFloat(serializedVfx, "maxLineLength", 0.62f);
        SetFloat(serializedVfx, "endLengthMultiplier", 0.3f);
        serializedVfx.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyParticleVfxSettings(
        HackerExecutionParticleGatherVfx particleVfx)
    {
        SerializedObject serializedVfx = new(particleVfx);
        SetFloat(serializedVfx, "minParticleSize", 0.025f);
        SetFloat(serializedVfx, "maxParticleSize", 0.075f);
        SetInt(serializedVfx, "sortingOrder", 78);
        SetInt(serializedVfx, "particlesPerProjectile", 10);
        SetFloat(serializedVfx, "spawnRadius", 0.18f);
        SetInt(serializedVfx, "spiralArmCount", 4);
        SetFloat(serializedVfx, "spiralTurns", 1.35f);
        SetFloat(serializedVfx, "spiralRadius", 0.65f);
        SetFloat(serializedVfx, "arrivalStagger", 0.24f);
        SetBool(serializedVfx, "showSolidChargeCircle", true);
        SetFloat(serializedVfx, "startCircleRadius", 0.02f);
        SetFloat(serializedVfx, "maxCircleRadius", 0.18f);
        SetFloat(serializedVfx, "circleAlpha", 0.82f);
        serializedVfx.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplySequenceSettings(
        HackerExecutionSequence sequence)
    {
        SerializedObject serializedSequence = new(sequence);
        SetFloat(
            serializedSequence,
            "preludeTempoMultiplier",
            0.78f);
        SetFloat(
            serializedSequence,
            "preludeAnimatorMultiplier",
            0.82f);
        SetFloat(
            serializedSequence,
            "preludeTempoSafetySeconds",
            30f);
        SetFloat(
            serializedSequence,
            "firstShotParryTravelRatio",
            0.52f);
        SetFloat(
            serializedSequence,
            "firstShotCameraZoomMultiplier",
            0.42f);
        SetFloat(
            serializedSequence,
            "firstShotCameraBlendSmoothTime",
            0.07f);
        SetFloat(
            serializedSequence,
            "firstShotCameraHoldSeconds",
            0.2f);
        SetFloat(
            serializedSequence,
            "firstShotParrySlowMultiplier",
            0.08f);
        SetFloat(
            serializedSequence,
            "firstShotParrySlowSeconds",
            0.5f);
        SetFloat(
            serializedSequence,
            "secondShotCameraZoomMultiplier",
            0.46f);
        SetFloat(
            serializedSequence,
            "secondShotCameraBlendSmoothTime",
            0.07f);
        SetFloat(
            serializedSequence,
            "secondShotParryTravelRatio",
            0.5f);
        SetFloat(
            serializedSequence,
            "secondShotParryHoldSeconds",
            0.18f);
        SetFloat(
            serializedSequence,
            "sweepRollWaitTimeoutSeconds",
            3f);
        SetFloat(
            serializedSequence,
            "sweepRollStartOffsetSeconds",
            0f);
        SetFloat(
            serializedSequence,
            "sweepRollSeconds",
            0.45f);
        SetFloat(
            serializedSequence,
            "rollBossClearanceDistance",
            2.35f);
        SetFloat(
            serializedSequence,
            "rollDistance",
            2.55f);
        SetFloat(
            serializedSequence,
            "rollCameraZoomMultiplier",
            0.64f);
        SetFloat(
            serializedSequence,
            "rollCameraBlendSmoothTime",
            0.07f);
        SetFloat(
            serializedSequence,
            "rollCameraShakeAmplitude",
            0.11f);
        SetFloat(
            serializedSequence,
            "rollCameraShakeSeconds",
            0.16f);
        SetFloat(
            serializedSequence,
            "slamPostFireDelaySeconds",
            0.3f);
        SetFloat(
            serializedSequence,
            "parryBossCameraZoomMultiplier",
            0.62f);
        SetFloat(
            serializedSequence,
            "parryBossFocusHoldSeconds",
            0.35f);
        SetFloat(
            serializedSequence,
            "aimChargeSeconds",
            3f);
        SetFloat(
            serializedSequence,
            "chargeCameraZoomMultiplier",
            0.704f);
        SetFloat(
            serializedSequence,
            "chargeCameraBlendSmoothTime",
            1.3f);
        SetFloat(
            serializedSequence,
            "chargeCameraShakeAmplitude",
            0.025f);
        SetFloat(
            serializedSequence,
            "chargeCameraShakeInterval",
            0.16f);
        SetFloat(
            serializedSequence,
            "chargeCameraShakeSeconds",
            0.12f);
        SetVector2(
            serializedSequence,
            "fallbackBossPosition",
            new Vector2(6.2f, 0f));
        SetFloat(
            serializedSequence,
            "wideCameraZoomMultiplier",
            1.08f);
        SetFloat(
            serializedSequence,
            "wideCameraBlendSmoothTime",
            0.2f);
        serializedSequence.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T FindComponentInScene<T>(Scene scene)
        where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component =
                roots[i].GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static Transform GetOrCreateAnchor(
        Transform parent,
        string name)
    {
        Transform anchor = parent.Find(name);
        if (anchor != null)
        {
            return anchor;
        }

        GameObject anchorObject = new(name);
        anchor = anchorObject.transform;
        anchor.SetParent(parent, false);
        return anchor;
    }

    private static T GetOrAddComponent<T>(GameObject owner)
        where T : Component
    {
        T component = owner.GetComponent<T>();
        return component != null
            ? component
            : owner.AddComponent<T>();
    }

    private static void SetObject(
        SerializedObject serializedObject,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetFloat(
        SerializedObject serializedObject,
        string propertyName,
        float value)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetInt(
        SerializedObject serializedObject,
        string propertyName,
        int value)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.intValue = value;
        }
    }

    private static void SetBool(
        SerializedObject serializedObject,
        string propertyName,
        bool value)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetColor(
        SerializedObject serializedObject,
        string propertyName,
        Color value)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.colorValue = value;
        }
    }

    private static void SetVector2(
        SerializedObject serializedObject,
        string propertyName,
        Vector2 value)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.vector2Value = value;
        }
    }
}
