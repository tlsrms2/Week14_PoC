using UnityEditor;
using UnityEngine;
using Week14.Combat;

[CustomEditor(typeof(CommonExecutionPresentationProfile))]
public sealed class CommonExecutionPresentationProfileEditor : Editor
{
    private SerializedProperty cuesProperty;

    private void OnEnable()
    {
        cuesProperty = serializedObject.FindProperty("cues");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "중간 생명 처형의 고정 시점에 실행할 연출 Cue입니다. " +
            "Delay와 Hold는 Time.timeScale의 영향을 받지 않는 실제 시간입니다.",
            MessageType.Info);

        DrawAddButtons();
        EditorGUILayout.Space(6f);

        for (int i = 0; i < cuesProperty.arraySize; i++)
        {
            DrawCue(i);
        }

        if (cuesProperty.arraySize == 0)
        {
            EditorGUILayout.HelpBox(
                "Cue가 없습니다. 위 버튼으로 Sound, Slow Motion, Camera Focus Cue를 추가하세요.",
                MessageType.None);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawAddButtons()
    {
        EditorGUILayout.LabelField("Add Cue", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Sound"))
            {
                AddCue(CommonExecutionCueType.Sound);
            }

            if (GUILayout.Button("Slow Motion"))
            {
                AddCue(CommonExecutionCueType.SlowMotion);
            }

            if (GUILayout.Button("Camera Focus"))
            {
                AddCue(CommonExecutionCueType.CameraFocus);
            }
        }
    }

    private void DrawCue(int index)
    {
        SerializedProperty cue = cuesProperty.GetArrayElementAtIndex(index);
        SerializedProperty enabled = cue.FindPropertyRelative("enabled");
        SerializedProperty cuePoint = cue.FindPropertyRelative("cuePoint");
        SerializedProperty cueType = cue.FindPropertyRelative("cueType");

        string title = $"{index + 1}. {cuePoint.enumDisplayNames[cuePoint.enumValueIndex]}"
            + $" / {cueType.enumDisplayNames[cueType.enumValueIndex]}";

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                cue.isExpanded = EditorGUILayout.Foldout(cue.isExpanded, title, true);
                enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(18f));
                if (GUILayout.Button("▲", GUILayout.Width(26f)) && index > 0)
                {
                    cuesProperty.MoveArrayElement(index, index - 1);
                    return;
                }

                if (GUILayout.Button("▼", GUILayout.Width(26f))
                    && index < cuesProperty.arraySize - 1)
                {
                    cuesProperty.MoveArrayElement(index, index + 1);
                    return;
                }

                if (GUILayout.Button("X", GUILayout.Width(26f)))
                {
                    cuesProperty.DeleteArrayElementAtIndex(index);
                    return;
                }
            }

            if (!cue.isExpanded)
            {
                return;
            }

            EditorGUILayout.PropertyField(cuePoint, new GUIContent("Trigger Point"));
            EditorGUILayout.PropertyField(cueType, new GUIContent("Cue Type"));
            EditorGUILayout.PropertyField(
                cue.FindPropertyRelative("delaySeconds"),
                new GUIContent("Delay Seconds"));

            if ((CommonExecutionCuePoint)cuePoint.enumValueIndex
                == CommonExecutionCuePoint.FlourishShot)
            {
                EditorGUILayout.PropertyField(
                    cue.FindPropertyRelative("flourishShotIndex"),
                    new GUIContent("Shot Index", "-1이면 모든 Flourish Shot에 실행합니다. 0부터 시작합니다."));
            }

            DrawTypeFields(cue, (CommonExecutionCueType)cueType.enumValueIndex);
        }
    }

    private static void DrawTypeFields(
        SerializedProperty cue,
        CommonExecutionCueType cueType)
    {
        EditorGUILayout.Space(3f);
        switch (cueType)
        {
            case CommonExecutionCueType.Sound:
                EditorGUILayout.HelpBox(
                    "Cue Point에 해당하는 사운드는 Sound Library의 사용 위치 연결에서 지정합니다.",
                    MessageType.Info);
                break;

            case CommonExecutionCueType.SlowMotion:
                EditorGUILayout.PropertyField(
                    cue.FindPropertyRelative("slowTimeScale"),
                    new GUIContent("Time Scale"));
                EditorGUILayout.PropertyField(
                    cue.FindPropertyRelative("slowDurationSeconds"),
                    new GUIContent("Duration Seconds"));
                break;

            case CommonExecutionCueType.CameraFocus:
            {
                SerializedProperty focusTarget = cue.FindPropertyRelative("focusTarget");
                EditorGUILayout.PropertyField(focusTarget, new GUIContent("Focus Target"));
                if ((CommonExecutionFocusTarget)focusTarget.enumValueIndex
                    == CommonExecutionFocusTarget.BossOffset)
                {
                    EditorGUILayout.PropertyField(
                        cue.FindPropertyRelative("bossFocusOffset"),
                        new GUIContent("Boss World Offset"));
                }

                EditorGUILayout.PropertyField(
                    cue.FindPropertyRelative("cameraFocusWeight"),
                    new GUIContent("Focus Weight"));
                EditorGUILayout.PropertyField(
                    cue.FindPropertyRelative("cameraZoomMultiplier"),
                    new GUIContent("Zoom Multiplier", "작을수록 더 가까이 확대됩니다."));
                EditorGUILayout.PropertyField(
                    cue.FindPropertyRelative("cameraBlendSmoothTime"),
                    new GUIContent(
                        "Blend Smooth Time",
                        "카메라가 새 포커스로 자연스럽게 이동하는 시간입니다."));
                if ((CommonExecutionFocusTarget)focusTarget.enumValueIndex
                    == CommonExecutionFocusTarget.FinalShotProjectile)
                {
                    EditorGUILayout.PropertyField(
                        cue.FindPropertyRelative("finalShotTravelSeconds"),
                        new GUIContent(
                            "Shot Travel Seconds",
                            "카메라가 총구에서 피격 지점까지 탄환 경로를 따라가는 시간입니다."));
                }

                EditorGUILayout.PropertyField(
                    cue.FindPropertyRelative("cameraHoldSeconds"),
                    new GUIContent(
                        "Hold Seconds",
                        "탄환 추적이면 피격 지점 도착 후 유지하는 시간입니다."));
                EditorGUILayout.PropertyField(
                    cue.FindPropertyRelative("restoreDefaultCameraAfterHold"),
                    new GUIContent("Restore Default After Hold"));
                break;
            }
        }
    }

    private void AddCue(CommonExecutionCueType cueType)
    {
        int index = cuesProperty.arraySize;
        cuesProperty.InsertArrayElementAtIndex(index);
        SerializedProperty cue = cuesProperty.GetArrayElementAtIndex(index);
        cue.FindPropertyRelative("enabled").boolValue = true;
        cue.FindPropertyRelative("cuePoint").enumValueIndex =
            (int)CommonExecutionCuePoint.AfterLetterbox;
        cue.FindPropertyRelative("cueType").enumValueIndex = (int)cueType;
        cue.FindPropertyRelative("delaySeconds").floatValue = 0f;
        cue.FindPropertyRelative("flourishShotIndex").intValue = -1;
        cue.FindPropertyRelative("slowTimeScale").floatValue = 0.15f;
        cue.FindPropertyRelative("slowDurationSeconds").floatValue = 0.25f;
        cue.FindPropertyRelative("focusTarget").enumValueIndex =
            (int)CommonExecutionFocusTarget.Midpoint;
        cue.FindPropertyRelative("bossFocusOffset").vector2Value = Vector2.zero;
        cue.FindPropertyRelative("cameraFocusWeight").floatValue = 1f;
        cue.FindPropertyRelative("cameraZoomMultiplier").floatValue = 0.5f;
        cue.FindPropertyRelative("cameraBlendSmoothTime").floatValue = 0.25f;
        cue.FindPropertyRelative("finalShotTravelSeconds").floatValue = 0.3f;
        cue.FindPropertyRelative("cameraHoldSeconds").floatValue = 0.2f;
        cue.FindPropertyRelative("restoreDefaultCameraAfterHold").boolValue = true;
        cue.isExpanded = true;
    }
}
