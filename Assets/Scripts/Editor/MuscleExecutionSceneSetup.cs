using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Week14.Combat;
using Week14.Enemy;

public static class MuscleExecutionSceneSetup
{
    private const string ScenePath = "Assets/Scenes/Boss/Boss_Muscle.unity";
    private const string AutoSetupSessionKey =
        "Week14.MuscleExecutionSceneSetup.V1";

    [InitializeOnLoadMethod]
    private static void RegisterAutoSetup()
    {
        EditorApplication.delayCall += TryConfigureOnce;
    }

    [MenuItem("Tools/Week14/Muscle Execution/Configure Boss_Muscle Scene")]
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
        Scene targetScene = SceneManager.GetSceneByPath(ScenePath);
        bool wasAlreadyLoaded = targetScene.IsValid() && targetScene.isLoaded;
        if (wasAlreadyLoaded && targetScene.isDirty)
        {
            if (showDialogOnDirty)
            {
                EditorUtility.DisplayDialog(
                    "Muscle 처형 씬 설정",
                    "Boss_Muscle 씬에 저장하지 않은 변경이 있습니다. " +
                    "씬을 저장한 뒤 다시 실행하세요.",
                    "확인");
            }

            return false;
        }

        if (!wasAlreadyLoaded)
        {
            targetScene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Additive);
        }

        try
        {
            ConfigureLoadedScene(targetScene);
            EditorSceneManager.MarkSceneDirty(targetScene);
            return EditorSceneManager.SaveScene(targetScene);
        }
        finally
        {
            if (!wasAlreadyLoaded && targetScene.IsValid() && targetScene.isLoaded)
            {
                EditorSceneManager.CloseScene(targetScene, true);
            }
        }
    }

    private static void ConfigureLoadedScene(Scene scene)
    {
        MuscleBossAI muscle = FindComponentInScene<MuscleBossAI>(scene);
        if (muscle == null)
        {
            throw new InvalidOperationException(
                "Boss_Muscle 씬에서 MuscleBossAI를 찾을 수 없습니다.");
        }

        BossExecutionStage stage = FindComponentInScene<BossExecutionStage>(scene);
        bool createdStage = stage == null;
        if (createdStage)
        {
            GameObject stageObject = new("MuscleExecutionStage");
            SceneManager.MoveGameObjectToScene(stageObject, scene);
            stage = stageObject.AddComponent<BossExecutionStage>();
        }

        Transform playerStart = GetOrCreateAnchor(stage.transform, "PlayerStart");
        Transform bossPosition = GetOrCreateAnchor(stage.transform, "BossPosition");
        Transform playerRollEnd = GetOrCreateAnchor(stage.transform, "PlayerRollEnd");
        Transform wideCameraFocus =
            GetOrCreateAnchor(stage.transform, "WideCameraFocus");
        Transform projectileFocusProxy =
            GetOrCreateAnchor(stage.transform, "ProjectileFocusProxy");

        if (createdStage)
        {
            playerStart.localPosition = new Vector3(-2f, -1.1f, 0f);
            Vector2 diagonal = Vector2.one.normalized * 3f;
            bossPosition.localPosition =
                playerStart.localPosition + (Vector3)diagonal;
            playerRollEnd.localPosition = playerStart.localPosition;
            wideCameraFocus.localPosition =
                Vector3.Lerp(playerStart.localPosition, bossPosition.localPosition, 0.5f);
            projectileFocusProxy.localPosition = Vector3.zero;
        }

        SerializedObject serializedStage = new(stage);
        serializedStage.FindProperty("playerStart").objectReferenceValue = playerStart;
        serializedStage.FindProperty("bossPosition").objectReferenceValue = bossPosition;
        serializedStage.FindProperty("playerRollEnd").objectReferenceValue = playerRollEnd;
        serializedStage.FindProperty("wideCameraFocus").objectReferenceValue =
            wideCameraFocus;
        serializedStage.FindProperty("projectileFocusProxy").objectReferenceValue =
            projectileFocusProxy;
        serializedStage.ApplyModifiedPropertiesWithoutUndo();

        MuscleExecutionSequence sequence =
            GetOrAddComponent<MuscleExecutionSequence>(stage.gameObject);
        ExecutionChargeGatherVfx chargeVfx =
            GetOrAddComponent<ExecutionChargeGatherVfx>(stage.gameObject);

        SerializedObject serializedSequence = new(sequence);
        serializedSequence.FindProperty("stage").objectReferenceValue = stage;
        serializedSequence.FindProperty("aimChargeGatherVfx").objectReferenceValue =
            chargeVfx;
        serializedSequence.FindProperty("patternSourceBoss").objectReferenceValue =
            muscle;
        serializedSequence.FindProperty("executionPatternId").stringValue = "Execution";
        serializedSequence.ApplyModifiedPropertiesWithoutUndo();

        ApplySharedChargeVfxSettings(chargeVfx);
        EditorUtility.SetDirty(stage);
        EditorUtility.SetDirty(sequence);
        EditorUtility.SetDirty(chargeVfx);
    }

    private static void ApplySharedChargeVfxSettings(
        ExecutionChargeGatherVfx chargeVfx)
    {
        SerializedObject serializedVfx = new(chargeVfx);
        SetColor(serializedVfx, "gatheredColor", new Color(0.2f, 0.6509804f, 1f, 1f));
        SetFloat(serializedVfx, "lineWidth", 0.035f);
        SetInt(serializedVfx, "sortingOrder", 75);
        SetBool(serializedVfx, "sortAboveMuzzleOwner", true);
        SetInt(serializedVfx, "ownerSortingOrderOffset", 30);
        SetBool(serializedVfx, "showMuzzleCircle", true);
        SetFloat(serializedVfx, "startCircleRadius", 0.015f);
        SetFloat(serializedVfx, "maxCircleRadius", 0.04f);
        SetFloat(serializedVfx, "circleLineWidth", 0.085f);
        SetInt(serializedVfx, "circleSegments", 40);
        SetInt(serializedVfx, "lineCount", 18);
        SetFloat(serializedVfx, "emissionSeconds", 1.5f);
        SetFloat(serializedVfx, "travelSeconds", 0.24f);
        SetFloat(serializedVfx, "minSpawnDistance", 0.45f);
        SetFloat(serializedVfx, "maxSpawnDistance", 1.8f);
        SetFloat(serializedVfx, "minLineLength", 0.22f);
        SetFloat(serializedVfx, "maxLineLength", 0.68f);
        SetFloat(serializedVfx, "endLengthMultiplier", 0.35f);
        serializedVfx.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T FindComponentInScene<T>(Scene scene)
        where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component = roots[i].GetComponentInChildren<T>(true);
            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    private static Transform GetOrCreateAnchor(Transform parent, string name)
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
        return component != null ? component : owner.AddComponent<T>();
    }

    private static void SetFloat(
        SerializedObject serializedObject,
        string propertyName,
        float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
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
        SerializedProperty property = serializedObject.FindProperty(propertyName);
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
        SerializedProperty property = serializedObject.FindProperty(propertyName);
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
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.colorValue = value;
        }
    }
}
