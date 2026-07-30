using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Week14.Audio;

[CustomEditor(typeof(SoundLibrary))]
public sealed class SoundLibraryEditor : Editor
{
    private readonly Dictionary<string, bool> categoryFoldouts =
        new(StringComparer.OrdinalIgnoreCase);

    private SerializedProperty bgmEntries;
    private SerializedProperty sfxEntries;
    private string searchText = string.Empty;

    private void OnEnable()
    {
        bgmEntries = serializedObject.FindProperty("bgmEntries");
        sfxEntries = serializedObject.FindProperty("sfxEntries");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        }

        if (GUILayout.Button("전용 SFX 관리 창 열기", GUILayout.Height(26f)))
        {
            Selection.activeObject = target;
            EditorApplication.ExecuteMenuItem(
                "Tools/Week14/Audio/Sound Library Manager");
        }

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(bgmEntries, new GUIContent("Background Music"), true);
        EditorGUILayout.Space();
        DrawSfxInspector();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSfxInspector()
    {
        EditorGUILayout.LabelField("Sound Effects", EditorStyles.boldLabel);
        DrawSearchToolbar();
        DrawValidationWarnings();

        Dictionary<string, List<int>> groups = BuildCategoryGroups();
        List<string> categories = new(groups.Keys);
        categories.Sort(CompareCategories);

        int visibleCount = CountVisibleEntries(groups);
        EditorGUILayout.LabelField(
            $"전체 {sfxEntries.arraySize}개 / 표시 {visibleCount}개 / 카테고리 {categories.Count}개",
            EditorStyles.miniLabel);

        for (int i = 0; i < categories.Count; i++)
        {
            string category = categories[i];
            DrawCategory(category, groups[category]);
        }

        if (GUILayout.Button("SFX 추가"))
        {
            AddSfx(SoundLibrary.UncategorizedSfxCategory);
        }
    }

    private void DrawSearchToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUIStyle searchStyle =
                GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField;
            searchText = GUILayout.TextField(searchText, searchStyle);
            if (GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(24f)))
            {
                searchText = string.Empty;
                GUI.FocusControl(null);
            }
        }
    }

    private void DrawValidationWarnings()
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        List<string> duplicates = new();
        int emptyIdCount = 0;

        for (int i = 0; i < sfxEntries.arraySize; i++)
        {
            SerializedProperty entry = sfxEntries.GetArrayElementAtIndex(i);
            string id = entry.FindPropertyRelative("id").stringValue;
            if (string.IsNullOrWhiteSpace(id))
            {
                emptyIdCount++;
                continue;
            }

            if (!ids.Add(id) && !duplicates.Contains(id))
            {
                duplicates.Add(id);
            }
        }

        if (emptyIdCount > 0)
        {
            EditorGUILayout.HelpBox($"ID가 비어 있는 SFX가 {emptyIdCount}개 있습니다.", MessageType.Warning);
        }

        if (duplicates.Count > 0)
        {
            EditorGUILayout.HelpBox(
                $"중복 SFX ID: {string.Join(", ", duplicates)}",
                MessageType.Error);
        }

    }

    private Dictionary<string, List<int>> BuildCategoryGroups()
    {
        Dictionary<string, List<int>> groups = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < sfxEntries.arraySize; i++)
        {
            SerializedProperty entry = sfxEntries.GetArrayElementAtIndex(i);
            string category = GetCategory(entry);
            if (!groups.TryGetValue(category, out List<int> indices))
            {
                indices = new List<int>();
                groups.Add(category, indices);
            }

            indices.Add(i);
        }

        return groups;
    }

    private int CountVisibleEntries(Dictionary<string, List<int>> groups)
    {
        int count = 0;
        foreach (KeyValuePair<string, List<int>> group in groups)
        {
            for (int i = 0; i < group.Value.Count; i++)
            {
                if (MatchesSearch(group.Value[i], group.Key))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private void DrawCategory(string category, List<int> indices)
    {
        List<int> visibleIndices = new();
        for (int i = 0; i < indices.Count; i++)
        {
            if (MatchesSearch(indices[i], category))
            {
                visibleIndices.Add(indices[i]);
            }
        }

        if (visibleIndices.Count == 0)
        {
            return;
        }

        bool isSearching = !string.IsNullOrWhiteSpace(searchText);
        bool expanded = isSearching || GetCategoryFoldout(category);
        expanded = EditorGUILayout.Foldout(
            expanded,
            $"{category} ({visibleIndices.Count}/{indices.Count})",
            true,
            EditorStyles.foldoutHeader);

        if (!isSearching)
        {
            categoryFoldouts[category] = expanded;
        }

        if (!expanded)
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            string currentName = category == SoundLibrary.UncategorizedSfxCategory
                ? string.Empty
                : category;
            string nextName = EditorGUILayout.DelayedTextField("카테고리 이름", currentName);
            string normalizedNextName = SoundLibrary.NormalizeSfxCategory(nextName);
            if (!string.Equals(category, normalizedNextName, StringComparison.Ordinal))
            {
                RenameCategory(indices, normalizedNextName);
            }

            for (int i = 0; i < visibleIndices.Count; i++)
            {
                DrawSfxEntry(visibleIndices[i]);
            }

            if (GUILayout.Button($"{category}에 SFX 추가"))
            {
                AddSfx(category);
            }
        }

        EditorGUILayout.Space(2f);
    }

    private void DrawSfxEntry(int index)
    {
        SerializedProperty entry = sfxEntries.GetArrayElementAtIndex(index);
        SerializedProperty id = entry.FindPropertyRelative("id");
        string label = string.IsNullOrWhiteSpace(id.stringValue)
            ? $"#{index + 1} <ID 없음>"
            : $"#{index + 1} {id.stringValue}";

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, label, true);
                if (GUILayout.Button("삭제", EditorStyles.miniButton, GUILayout.Width(42f)))
                {
                    RemoveSfx(index);
                }
            }

            if (!entry.isExpanded)
            {
                return;
            }

            SerializedProperty category = entry.FindPropertyRelative("category");
            string nextCategory = EditorGUILayout.DelayedTextField("카테고리", category.stringValue);
            if (!string.Equals(category.stringValue, nextCategory, StringComparison.Ordinal))
            {
                category.stringValue = nextCategory.Trim();
            }

            EditorGUILayout.PropertyField(id, new GUIContent("ID"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("clip"), new GUIContent("Audio Clip"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("volume"), new GUIContent("볼륨"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("pitch"), new GUIContent("피치"));
        }
    }

    private bool MatchesSearch(int index, string category)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        SerializedProperty entry = sfxEntries.GetArrayElementAtIndex(index);
        string id = entry.FindPropertyRelative("id").stringValue;
        AudioClip clip = entry.FindPropertyRelative("clip").objectReferenceValue as AudioClip;
        string query = searchText.Trim();

        return category.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            || (!string.IsNullOrEmpty(id) && id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            || (clip != null && clip.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private bool GetCategoryFoldout(string category)
    {
        if (!categoryFoldouts.TryGetValue(category, out bool expanded))
        {
            expanded = category == SoundLibrary.UncategorizedSfxCategory;
            categoryFoldouts.Add(category, expanded);
        }

        return expanded;
    }

    private static string GetCategory(SerializedProperty entry)
    {
        return SoundLibrary.NormalizeSfxCategory(
            entry.FindPropertyRelative("category").stringValue);
    }

    private static int CompareCategories(string left, string right)
    {
        bool leftIsUncategorized = left == SoundLibrary.UncategorizedSfxCategory;
        bool rightIsUncategorized = right == SoundLibrary.UncategorizedSfxCategory;
        if (leftIsUncategorized != rightIsUncategorized)
        {
            return leftIsUncategorized ? -1 : 1;
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private void RenameCategory(List<int> indices, string category)
    {
        string storedCategory = category == SoundLibrary.UncategorizedSfxCategory
            ? string.Empty
            : category.Trim();
        for (int i = 0; i < indices.Count; i++)
        {
            sfxEntries
                .GetArrayElementAtIndex(indices[i])
                .FindPropertyRelative("category")
                .stringValue = storedCategory;
        }

        serializedObject.ApplyModifiedProperties();
        categoryFoldouts[category] = true;
        EditorUtility.SetDirty(target);
        GUIUtility.ExitGUI();
    }

    private void AddSfx(string category)
    {
        int index = sfxEntries.arraySize;
        sfxEntries.InsertArrayElementAtIndex(index);

        SerializedProperty entry = sfxEntries.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("category").stringValue =
            category == SoundLibrary.UncategorizedSfxCategory ? string.Empty : category;
        entry.FindPropertyRelative("id").stringValue = string.Empty;
        entry.FindPropertyRelative("clip").objectReferenceValue = null;
        entry.FindPropertyRelative("volume").floatValue = 1f;
        entry.FindPropertyRelative("pitch").floatValue = 1f;
        entry.FindPropertyRelative("usages").ClearArray();
        entry.isExpanded = true;

        serializedObject.ApplyModifiedProperties();
        categoryFoldouts[category] = true;
        EditorUtility.SetDirty(target);
        GUIUtility.ExitGUI();
    }

    private void RemoveSfx(int index)
    {
        sfxEntries.DeleteArrayElementAtIndex(index);
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        GUIUtility.ExitGUI();
    }
}
