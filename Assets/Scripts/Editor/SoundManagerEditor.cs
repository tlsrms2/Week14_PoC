using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Week14.Audio;

[CustomEditor(typeof(SoundManager))]
public sealed class SoundManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.propertyPath == "m_Script")
            {
                continue;
            }

            if (property.propertyPath == "testSfxId")
            {
                DrawTestSfxIdField(property);
                continue;
            }

            EditorGUILayout.PropertyField(property, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawTestSfxIdField(SerializedProperty property)
    {
        SoundLibrary library = serializedObject.FindProperty("library").objectReferenceValue as SoundLibrary;
        List<string> ids = library != null ? new List<string>(library.SfxIds) : new List<string>();

        if (ids.Count == 0)
        {
            EditorGUILayout.PropertyField(property);
            return;
        }

        int currentIndex = Mathf.Max(0, ids.IndexOf(property.stringValue));
        int selectedIndex = EditorGUILayout.Popup("Test Sfx Id", currentIndex, ids.ToArray());
        property.stringValue = ids[selectedIndex];
    }
}
