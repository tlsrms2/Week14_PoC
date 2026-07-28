using UnityEditor;
using UnityEngine;
using Week14.Combat;
using Week14.Enemy;

[CustomEditor(typeof(ConductorExecutionSequence))]
public sealed class ConductorExecutionSequenceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(iterator);
                }

                continue;
            }

            EditorGUILayout.PropertyField(iterator, true);
        }

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        DrawValidation();
    }

    private void DrawValidation()
    {
        ConductorExecutionSequence sequence =
            (ConductorExecutionSequence)target;

        SerializedProperty stageProperty =
            serializedObject.FindProperty("stage");
        BossExecutionStage stage =
            stageProperty?.objectReferenceValue as BossExecutionStage;

        if (stage == null)
        {
            EditorGUILayout.HelpBox(
                "BossExecutionStage가 연결되지 않았습니다.",
                MessageType.Error);
            return;
        }

        if (!stage.HasRequiredAnchors)
        {
            EditorGUILayout.HelpBox(
                "BossExecutionStage에 필요한 앵커(PlayerStart, " +
                "BossPosition, PlayerRollEnd, ProjectileFocusProxy)가 " +
                "비어 있습니다.",
                MessageType.Error);
            return;
        }

        SerializedProperty dronePrefabProperty =
            serializedObject.FindProperty("dronePrefab");
        if (dronePrefabProperty != null
            && dronePrefabProperty.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox(
                "드론 프리팹이 비어 있습니다. " +
                "빈 오브젝트가 사용되므로 화면에 보이지 않습니다.",
                MessageType.Warning);
        }

        Conductor conductor =
            Object.FindFirstObjectByType<Conductor>(FindObjectsInactive.Include);
        if (conductor == null)
        {
            EditorGUILayout.HelpBox(
                "씬에서 Conductor 보스를 찾을 수 없습니다.",
                MessageType.Warning);
        }

        if (sequence.CanPlay)
        {
            EditorGUILayout.HelpBox(
                "처형 시퀀스를 실행할 준비가 되었습니다.",
                MessageType.Info);
        }
    }
}
