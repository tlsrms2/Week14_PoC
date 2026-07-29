using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Week14.Combat;
using Week14.Enemy;

// 기존 파일과 메뉴를 참조하는 외부 코드를 깨지 않기 위해 타입 이름은 유지합니다.
public static class HogExecutionTimelineBuilder
{
    private const string MainScenePath = "Assets/Scenes/Boss/MainScene.unity";

    [MenuItem("Tools/Week14/Hog Execution/Configure MainScene Sequence")]
    public static void ConfigureMainSceneSequence()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != MainScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        }

        BossExecutionStage stage =
            UnityEngine.Object.FindFirstObjectByType<BossExecutionStage>(
                FindObjectsInactive.Include);
        if (stage == null)
        {
            throw new InvalidOperationException(
                "MainScene에서 BossExecutionStage를 찾을 수 없습니다.");
        }

        GameObject stageObject = stage.gameObject;
        stageObject.name = "HogExecutionStage";
        RemoveLegacyTimelineComponents(stageObject);

        HogExecutionSequence sequence =
            GetOrAddComponent<HogExecutionSequence>(stageObject);
        ExecutionChargeGatherVfx chargeGatherVfx =
            GetOrAddComponent<ExecutionChargeGatherVfx>(stageObject);

        ConfigureSequence(sequence, stage, chargeGatherVfx);

        EditorUtility.SetDirty(stage);
        EditorUtility.SetDirty(sequence);
        EditorUtility.SetDirty(chargeGatherVfx);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("MainScene Hog 처형을 코드 기반 시퀀스로 설정했습니다.");
    }

    // 이전 메뉴를 호출하는 코드가 있다면 새 설정 방식으로 연결합니다.
    public static void BuildMainSceneTimeline()
    {
        ConfigureMainSceneSequence();
    }

    private static void RemoveLegacyTimelineComponents(GameObject stageObject)
    {
        Component[] components = stageObject.GetComponents<Component>();
        for (int i = components.Length - 1; i >= 0; i--)
        {
            Component component = components[i];
            if (component == null)
            {
                continue;
            }

            string typeName = component.GetType().FullName;
            if (typeName == "UnityEngine.Playables.PlayableDirector"
                || typeName == "UnityEngine.Timeline.SignalReceiver")
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
        }
    }

    private static void ConfigureSequence(
        HogExecutionSequence sequence,
        BossExecutionStage stage,
        ExecutionChargeGatherVfx chargeGatherVfx)
    {
        SerializedObject serializedSequence = new(sequence);
        serializedSequence.FindProperty("stage").objectReferenceValue = stage;
        serializedSequence.FindProperty("aimChargeGatherVfx").objectReferenceValue =
            chargeGatherVfx;

        SerializedProperty patternSourceBoss =
            serializedSequence.FindProperty("patternSourceBoss");
        if (patternSourceBoss.objectReferenceValue == null)
        {
            patternSourceBoss.objectReferenceValue =
                UnityEngine.Object.FindFirstObjectByType<HogBossAI>(
                    FindObjectsInactive.Include);
        }

        serializedSequence.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T GetOrAddComponent<T>(GameObject owner)
        where T : Component
    {
        T component = owner.GetComponent<T>();
        return component != null ? component : owner.AddComponent<T>();
    }
}
