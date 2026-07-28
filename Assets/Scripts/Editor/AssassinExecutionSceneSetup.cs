using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Week14.Combat;
using Week14.Enemy;

public static class AssassinExecutionSceneSetup
{
    private const string ScenePath =
        "Assets/Scenes/Boss/Boss_Assassin.unity";
    private const string AutoSetupSessionKey =
        "Week14.AssassinExecutionSceneSetup.V1";

    [InitializeOnLoadMethod]
    private static void RegisterAutoSetup()
    {
        EditorApplication.delayCall += TryConfigureOnce;
    }

    [MenuItem(
        "Tools/Week14/Assassin Execution/Configure Boss_Assassin Scene")]
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
                    "Assassin 처형 씬 설정",
                    "Boss_Assassin 씬에 저장하지 않은 변경이 있습니다. " +
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
        AssassinBossAI assassin =
            FindComponentInScene<AssassinBossAI>(scene);
        if (assassin == null)
        {
            throw new InvalidOperationException(
                "Boss_Assassin 씬에서 AssassinBossAI를 찾지 못했습니다.");
        }

        BossExecutionStage stage =
            FindComponentInScene<BossExecutionStage>(scene);
        bool createdStage = stage == null;
        if (createdStage)
        {
            GameObject stageObject =
                new("AssassinExecutionStage");
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
                new Vector3(0f, 4.2f, 0f);
            playerRollEnd.localPosition =
                playerStart.localPosition;
            wideCameraFocus.localPosition =
                new Vector3(0f, 2.1f, 0f);
            projectileFocusProxy.localPosition =
                Vector3.zero;
        }

        SerializedObject serializedStage = new(stage);
        SetObject(
            serializedStage,
            "playerStart",
            playerStart);
        SetObject(
            serializedStage,
            "bossPosition",
            bossPosition);
        SetObject(
            serializedStage,
            "playerRollEnd",
            playerRollEnd);
        SetObject(
            serializedStage,
            "wideCameraFocus",
            wideCameraFocus);
        SetObject(
            serializedStage,
            "projectileFocusProxy",
            projectileFocusProxy);
        serializedStage.ApplyModifiedPropertiesWithoutUndo();

        AssassinExecutionSequence sequence =
            FindComponentInScene<AssassinExecutionSequence>(scene);
        sequence ??=
            GetOrAddComponent<AssassinExecutionSequence>(
                stage.gameObject);
        ExecutionChargeGatherVfx chargeVfx =
            GetOrAddComponent<ExecutionChargeGatherVfx>(
                stage.gameObject);
        SerializedObject serializedSequence = new(sequence);
        SetObject(serializedSequence, "stage", stage);
        SetObject(
            serializedSequence,
            "patternSourceBoss",
            assassin);
        SetObject(
            serializedSequence,
            "aimChargeGatherVfx",
            chargeVfx);
        serializedSequence.FindProperty(
            "executionPatternId").stringValue = "Execution";
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
        SetColor(
            serializedVfx,
            "gatheredColor",
            new Color(0.2f, 0.6509804f, 1f, 1f));
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
}
