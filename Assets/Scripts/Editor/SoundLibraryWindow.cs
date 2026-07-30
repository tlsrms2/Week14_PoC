#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Week14.Audio;

public sealed class SoundLibraryWindow : EditorWindow
{
    private const string LastLibraryPathKey = "Week14.SoundLibraryWindow.LastLibraryPath";
    private static readonly string[] FilterLabels =
    {
        "전체",
        "고정 ID",
        "기타 ID",
        "클립 누락",
        "문제"
    };

    private enum EntryFilter
    {
        All,
        Defined,
        Custom,
        MissingClip,
        Problem
    }

    private enum ViewMode
    {
        Sounds,
        Usages
    }

    private SoundLibrary library;
    private SerializedObject serializedLibrary;
    private SerializedProperty sfxEntries;
    private Vector2 scrollPosition;
    private string searchText = string.Empty;
    private string selectedCategory = "전체";
    private EntryFilter filter;
    private ViewMode viewMode;

    [MenuItem("Tools/Week14/Audio/Sound Library Manager")]
    private static void Open()
    {
        GetWindow<SoundLibraryWindow>("Sound Library");
    }

    private void OnEnable()
    {
        minSize = new Vector2(900f, 520f);
        Undo.undoRedoPerformed += HandleUndoRedo;
        TryRestoreLibrary();
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= HandleUndoRedo;
        ApplyPendingChanges();
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is SoundLibrary selectedLibrary)
        {
            SetLibrary(selectedLibrary);
            Repaint();
        }
    }

    private void OnGUI()
    {
        DrawLibraryPicker();
        if (library == null)
        {
            EditorGUILayout.HelpBox(
                "관리할 SoundLibrary 에셋을 선택하세요.",
                MessageType.Info);
            return;
        }

        EnsureSerializedLibrary();
        serializedLibrary.Update();

        DrawSummary();
        viewMode = (ViewMode)GUILayout.Toolbar(
            (int)viewMode,
            new[] { "사운드 목록", "사용 위치 연결" });
        EditorGUILayout.Space(4f);

        if (viewMode == ViewMode.Sounds)
        {
            DrawToolbar();
            DrawDropArea();
            DrawEntries();
            DrawBottomActions();
        }
        else
        {
            DrawUsageMap();
        }

        if (serializedLibrary.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(library);
        }
    }

    private void DrawLibraryPicker()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            SoundLibrary nextLibrary = EditorGUILayout.ObjectField(
                library,
                typeof(SoundLibrary),
                false) as SoundLibrary;
            if (nextLibrary != library)
            {
                SetLibrary(nextLibrary);
            }

            using (new EditorGUI.DisabledScope(library == null))
            {
                if (GUILayout.Button("선택", EditorStyles.toolbarButton, GUILayout.Width(42f)))
                {
                    Selection.activeObject = library;
                    EditorGUIUtility.PingObject(library);
                }

                if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(42f)))
                {
                    SaveLibrary();
                }
            }
        }
    }

    private void DrawSummary()
    {
        GetStatistics(
            out int total,
            out int definedCount,
            out int missingClipCount,
            out int problemCount);
        GetUsageStatistics(out int assignedUsageCount, out int duplicateUsageCount);

        EditorGUILayout.LabelField("사운드 현황", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            DrawStat("전체", total);
            DrawStat("고정 ID", definedCount);
            DrawStat("클립 누락", missingClipCount);
            DrawStat("문제", problemCount);
            DrawStat(
                "사용 위치",
                assignedUsageCount,
                Enum.GetValues(typeof(SoundEvent)).Length);
            if (duplicateUsageCount > 0)
            {
                DrawStat("중복 연결", duplicateUsageCount);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("고정 ID 선택 추가", GUILayout.Height(25f)))
            {
                ShowAddSoundIdMenu();
            }

            if (GUILayout.Button("기타 ID 추가", GUILayout.Width(110f), GUILayout.Height(25f)))
            {
                AddEmptyEntry();
            }
        }
    }

    private static void DrawStat(string label, int count)
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(70f)))
        {
            GUILayout.Label(label, EditorStyles.miniLabel);
            GUILayout.Label(count.ToString(), EditorStyles.boldLabel);
        }
    }

    private static void DrawStat(string label, int count, int total)
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(90f)))
        {
            GUILayout.Label(label, EditorStyles.miniLabel);
            GUILayout.Label($"{count} / {total}", EditorStyles.boldLabel);
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.Space(4f);
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

        filter = (EntryFilter)GUILayout.Toolbar((int)filter, FilterLabels);

        List<string> categories = BuildCategoryOptions();
        int categoryIndex = Mathf.Max(0, categories.IndexOf(selectedCategory));
        int nextCategoryIndex = EditorGUILayout.Popup("카테고리", categoryIndex, categories.ToArray());
        selectedCategory = categories[Mathf.Clamp(nextCategoryIndex, 0, categories.Count - 1)];
    }

    private void DrawDropArea()
    {
        Rect dropArea = GUILayoutUtility.GetRect(0f, 46f, GUILayout.ExpandWidth(true));
        GUI.Box(
            dropArea,
            "AudioClip을 여기에 드롭\n파일명과 같은 ID가 비어 있으면 할당하고, 없으면 새 항목을 만듭니다.",
            EditorStyles.helpBox);

        Event current = Event.current;
        if (!dropArea.Contains(current.mousePosition))
        {
            return;
        }

        if (current.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = HasAudioClipInDrag()
                ? DragAndDropVisualMode.Copy
                : DragAndDropVisualMode.Rejected;
            current.Use();
        }
        else if (current.type == EventType.DragPerform && HasAudioClipInDrag())
        {
            DragAndDrop.AcceptDrag();
            AddDroppedClips();
            current.Use();
            GUIUtility.ExitGUI();
        }
    }

    private void DrawUsageMap()
    {
        EditorGUILayout.HelpBox(
            "게임의 재생 위치마다 사용할 SFX를 지정합니다. 같은 사용 위치는 한 SFX에만 연결됩니다.",
            MessageType.Info);

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

        string[] entryOptions = BuildUsageEntryOptions();
        List<SoundEvent> visibleEvents = new();
        foreach (SoundEvent soundEvent in Enum.GetValues(typeof(SoundEvent)))
        {
            if (string.IsNullOrWhiteSpace(searchText)
                || GetUsageLabel(soundEvent).IndexOf(
                    searchText.Trim(),
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                visibleEvents.Add(soundEvent);
            }
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        float columnWidth = Mathf.Max(360f, (position.width - 34f) * 0.5f);
        for (int i = 0; i < visibleEvents.Count; i += 2)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(
                           EditorStyles.helpBox,
                           GUILayout.Width(columnWidth)))
                {
                    DrawUsageRow(visibleEvents[i], entryOptions);
                }

                if (i + 1 < visibleEvents.Count)
                {
                    using (new EditorGUILayout.VerticalScope(
                               EditorStyles.helpBox,
                               GUILayout.Width(columnWidth)))
                    {
                        DrawUsageRow(visibleEvents[i + 1], entryOptions);
                    }
                }
                else
                {
                    GUILayout.Space(columnWidth);
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawUsageRow(SoundEvent soundEvent, string[] entryOptions)
    {
        int ownerIndex = FindUsageOwnerIndex(soundEvent);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(
                GetUsageLabel(soundEvent),
                EditorStyles.miniLabel,
                GUILayout.Width(190f));
            int nextOption = EditorGUILayout.Popup(ownerIndex + 1, entryOptions);
            if (nextOption != ownerIndex + 1)
            {
                SetUsageOwner(soundEvent, nextOption - 1);
                GUIUtility.ExitGUI();
            }
        }
    }

    private string[] BuildUsageEntryOptions()
    {
        string[] options = new string[sfxEntries.arraySize + 1];
        options[0] = "미지정";
        for (int i = 0; i < sfxEntries.arraySize; i++)
        {
            string id = sfxEntries
                .GetArrayElementAtIndex(i)
                .FindPropertyRelative("id")
                .stringValue;
            options[i + 1] = string.IsNullOrWhiteSpace(id)
                ? $"#{i + 1} (ID 없음)"
                : id;
        }

        return options;
    }

    private void SetUsageOwner(SoundEvent soundEvent, int ownerIndex)
    {
        Undo.RecordObject(library, "SFX 사용 위치 변경");
        for (int i = 0; i < sfxEntries.arraySize; i++)
        {
            RemoveUsage(
                sfxEntries.GetArrayElementAtIndex(i).FindPropertyRelative("usages"),
                soundEvent);
        }

        if (ownerIndex >= 0 && ownerIndex < sfxEntries.arraySize)
        {
            SerializedProperty usages = sfxEntries
                .GetArrayElementAtIndex(ownerIndex)
                .FindPropertyRelative("usages");
            int newIndex = usages.arraySize;
            usages.InsertArrayElementAtIndex(newIndex);
            usages.GetArrayElementAtIndex(newIndex).intValue = (int)soundEvent;
        }

        ApplyAndRefresh();
        Repaint();
    }

    private int FindUsageOwnerIndex(SoundEvent soundEvent)
    {
        for (int i = 0; i < sfxEntries.arraySize; i++)
        {
            SerializedProperty usages = sfxEntries
                .GetArrayElementAtIndex(i)
                .FindPropertyRelative("usages");
            if (ContainsUsage(usages, soundEvent))
            {
                return i;
            }
        }

        return -1;
    }

    private void DrawEntries()
    {
        Dictionary<string, int> idCounts = BuildIdCounts();
        Dictionary<SoundEvent, int> usageCounts = BuildUsageCounts();
        List<int> visibleIndices = new();
        for (int i = 0; i < sfxEntries.arraySize; i++)
        {
            SerializedProperty entry = sfxEntries.GetArrayElementAtIndex(i);
            if (MatchesFilter(entry, idCounts, usageCounts))
            {
                visibleIndices.Add(i);
            }
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        float columnWidth = Mathf.Max(360f, (position.width - 34f) * 0.5f);
        float previousLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 72f;
        try
        {
            for (int i = 0; i < visibleIndices.Count; i += 2)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(columnWidth)))
                    {
                        int leftIndex = visibleIndices[i];
                        DrawEntry(
                            leftIndex,
                            sfxEntries.GetArrayElementAtIndex(leftIndex),
                            idCounts,
                            usageCounts);
                    }

                    if (i + 1 < visibleIndices.Count)
                    {
                        using (new EditorGUILayout.VerticalScope(GUILayout.Width(columnWidth)))
                        {
                            int rightIndex = visibleIndices[i + 1];
                            DrawEntry(
                                rightIndex,
                                sfxEntries.GetArrayElementAtIndex(rightIndex),
                                idCounts,
                                usageCounts);
                        }
                    }
                    else
                    {
                        GUILayout.Space(columnWidth);
                    }
                }
            }
        }
        finally
        {
            EditorGUIUtility.labelWidth = previousLabelWidth;
        }

        if (visibleIndices.Count == 0)
        {
            EditorGUILayout.HelpBox("현재 조건에 맞는 SFX가 없습니다.", MessageType.Info);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawEntry(
        int index,
        SerializedProperty entry,
        IReadOnlyDictionary<string, int> idCounts,
        IReadOnlyDictionary<SoundEvent, int> usageCounts)
    {
        SerializedProperty id = entry.FindPropertyRelative("id");
        SerializedProperty category = entry.FindPropertyRelative("category");
        SerializedProperty clip = entry.FindPropertyRelative("clip");
        SerializedProperty volume = entry.FindPropertyRelative("volume");
        SerializedProperty pitch = entry.FindPropertyRelative("pitch");
        SerializedProperty usages = entry.FindPropertyRelative("usages");

        string currentId = id.stringValue;
        bool isDefined = IsDefinedSoundId(currentId);
        bool isDuplicate = !string.IsNullOrWhiteSpace(currentId)
            && idCounts.TryGetValue(currentId, out int count)
            && count > 1;
        bool hasDuplicateUsage = HasDuplicateUsage(usages, usageCounts);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(
                    isDefined ? "고정" : "기타",
                    EditorStyles.miniBoldLabel,
                    GUILayout.Width(42f));

                if (isDefined)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(id, GUIContent.none);
                    }
                }
                else
                {
                    EditorGUILayout.PropertyField(id, GUIContent.none);
                }

                if (GUILayout.Button("복제", EditorStyles.miniButton, GUILayout.Width(42f)))
                {
                    DuplicateEntry(index);
                }

                if (GUILayout.Button("삭제", EditorStyles.miniButton, GUILayout.Width(42f)))
                {
                    DeleteEntry(index, currentId);
                }
            }

            EditorGUILayout.PropertyField(category, new GUIContent("카테고리"));
            EditorGUILayout.PropertyField(clip, new GUIContent("Audio Clip"));
            EditorGUILayout.Slider(volume, 0f, 2f, new GUIContent("볼륨"));
            EditorGUILayout.Slider(pitch, 0.5f, 2f, new GUIContent("피치"));
            DrawEntryUsages(index, usages);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(clip.objectReferenceValue == null))
                {
                    if (GUILayout.Button("클립 선택"))
                    {
                        Selection.activeObject = clip.objectReferenceValue;
                        EditorGUIUtility.PingObject(clip.objectReferenceValue);
                    }

                    if (GUILayout.Button("클립 제거"))
                    {
                        clip.objectReferenceValue = null;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(currentId))
            {
                EditorGUILayout.HelpBox("ID가 비어 있습니다.", MessageType.Error);
            }
            else if (isDuplicate)
            {
                EditorGUILayout.HelpBox("같은 ID가 두 개 이상 있습니다.", MessageType.Error);
            }
            else if (hasDuplicateUsage)
            {
                EditorGUILayout.HelpBox(
                    "같은 사용 위치가 여러 SFX에 연결되어 있습니다.",
                    MessageType.Error);
            }
            else if (clip.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("AudioClip이 지정되지 않았습니다.", MessageType.Warning);
            }
        }
    }

    private void DrawEntryUsages(int entryIndex, SerializedProperty usages)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel("사용 위치");
            if (GUILayout.Button(
                    usages.arraySize > 0 ? $"설정 ({usages.arraySize})" : "설정",
                    EditorStyles.miniButton))
            {
                ShowUsageMenu(entryIndex);
            }
        }

        if (usages.arraySize > 0)
        {
            EditorGUILayout.LabelField(
                BuildUsageSummary(usages),
                EditorStyles.wordWrappedMiniLabel);
        }
    }

    private void ShowUsageMenu(int entryIndex)
    {
        GenericMenu menu = new();
        SerializedProperty usages = sfxEntries
            .GetArrayElementAtIndex(entryIndex)
            .FindPropertyRelative("usages");
        foreach (SoundEvent soundEvent in Enum.GetValues(typeof(SoundEvent)))
        {
            bool assignedHere = ContainsUsage(usages, soundEvent);
            int ownerIndex = FindUsageOwnerIndex(soundEvent);
            string ownerSuffix = ownerIndex >= 0 && ownerIndex != entryIndex
                ? $" (현재: {GetEntryId(ownerIndex)})"
                : string.Empty;
            SoundEvent capturedEvent = soundEvent;
            menu.AddItem(
                new GUIContent($"{GetUsageMenuPath(soundEvent)}{ownerSuffix}"),
                assignedHere,
                () => SetUsageOwner(
                    capturedEvent,
                    assignedHere ? -1 : entryIndex));
        }

        menu.ShowAsContext();
    }

    private void DrawBottomActions()
    {
        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("고정 ID 선택 추가", GUILayout.Height(26f)))
            {
                ShowAddSoundIdMenu();
            }

            if (GUILayout.Button("기타 ID 추가", GUILayout.Height(26f)))
            {
                AddEmptyEntry();
            }

            if (GUILayout.Button("저장", GUILayout.Width(90f), GUILayout.Height(26f)))
            {
                SaveLibrary();
            }
        }
    }

    private void ShowAddSoundIdMenu()
    {
        GenericMenu menu = new();
        foreach (SoundId soundId in Enum.GetValues(typeof(SoundId)))
        {
            string id = soundId.ToLibraryId();
            if (FindEntryIndex(id) >= 0)
            {
                menu.AddDisabledItem(new GUIContent($"{id} (등록됨)"));
            }
            else
            {
                menu.AddItem(
                    new GUIContent(id),
                    false,
                    () => AddDefinedEntry(soundId));
            }
        }

        menu.ShowAsContext();
    }

    private void AddDefinedEntry(SoundId soundId)
    {
        string id = soundId.ToLibraryId();
        if (FindEntryIndex(id) >= 0)
        {
            searchText = id;
            filter = EntryFilter.All;
            selectedCategory = "전체";
            Repaint();
            return;
        }

        Undo.RecordObject(library, "고정 ID SFX 추가");
        int index = sfxEntries.arraySize;
        sfxEntries.InsertArrayElementAtIndex(index);
        ResetEntry(
            sfxEntries.GetArrayElementAtIndex(index),
            id,
            string.Empty,
            null,
            1f,
            1f);
        ApplyAndRefresh();
        searchText = id;
        filter = EntryFilter.All;
        selectedCategory = "전체";
        scrollPosition.y = float.MaxValue;
        Repaint();
    }

    private void AddEmptyEntry()
    {
        Undo.RecordObject(library, "SFX 추가");
        int index = sfxEntries.arraySize;
        sfxEntries.InsertArrayElementAtIndex(index);
        SerializedProperty entry = sfxEntries.GetArrayElementAtIndex(index);
        ResetEntry(
            entry,
            CreateUniqueId("NewSfx"),
            string.Empty,
            null,
            1f,
            1f);
        ApplyAndRefresh();
        scrollPosition.y = float.MaxValue;
        GUIUtility.ExitGUI();
    }

    private void DuplicateEntry(int sourceIndex)
    {
        Undo.RecordObject(library, "SFX 복제");
        SerializedProperty source = sfxEntries.GetArrayElementAtIndex(sourceIndex);
        string sourceId = source.FindPropertyRelative("id").stringValue;
        string sourceCategory = source.FindPropertyRelative("category").stringValue;
        AudioClip sourceClip =
            source.FindPropertyRelative("clip").objectReferenceValue as AudioClip;
        float sourceVolume = source.FindPropertyRelative("volume").floatValue;
        float sourcePitch = source.FindPropertyRelative("pitch").floatValue;

        int newIndex = sourceIndex + 1;
        sfxEntries.InsertArrayElementAtIndex(newIndex);
        SerializedProperty copy = sfxEntries.GetArrayElementAtIndex(newIndex);
        ResetEntry(
            copy,
            CreateUniqueId(
                string.IsNullOrWhiteSpace(sourceId)
                    ? "NewSfx"
                    : $"{sourceId}_Copy"),
            sourceCategory,
            sourceClip,
            sourceVolume,
            sourcePitch);
        ApplyAndRefresh();
        GUIUtility.ExitGUI();
    }

    private void DeleteEntry(int index, string id)
    {
        string displayId = string.IsNullOrWhiteSpace(id) ? "ID 없음" : id;
        if (!EditorUtility.DisplayDialog(
                "SFX 삭제",
                $"'{displayId}' 항목을 삭제할까요?",
                "삭제",
                "취소"))
        {
            return;
        }

        Undo.RecordObject(library, "SFX 삭제");
        sfxEntries.DeleteArrayElementAtIndex(index);
        ApplyAndRefresh();
        GUIUtility.ExitGUI();
    }

    private void AddDroppedClips()
    {
        Undo.RecordObject(library, "AudioClip 일괄 추가");

        for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
        {
            if (DragAndDrop.objectReferences[i] is not AudioClip clip)
            {
                continue;
            }

            int existingIndex = FindEntryIndex(clip.name);
            if (existingIndex >= 0)
            {
                SerializedProperty existing = sfxEntries.GetArrayElementAtIndex(existingIndex);
                SerializedProperty existingClip = existing.FindPropertyRelative("clip");
                if (existingClip.objectReferenceValue == null)
                {
                    existingClip.objectReferenceValue = clip;
                    continue;
                }

                if (existingClip.objectReferenceValue == clip)
                {
                    continue;
                }
            }

            string id = existingIndex < 0
                ? clip.name
                : CreateUniqueId($"{clip.name}_2");

            int newIndex = sfxEntries.arraySize;
            sfxEntries.InsertArrayElementAtIndex(newIndex);
            ResetEntry(
                sfxEntries.GetArrayElementAtIndex(newIndex),
                id,
                string.Empty,
                clip,
                1f,
                1f);
        }

        ApplyAndRefresh();
    }

    private bool MatchesFilter(
        SerializedProperty entry,
        IReadOnlyDictionary<string, int> idCounts,
        IReadOnlyDictionary<SoundEvent, int> usageCounts)
    {
        string id = entry.FindPropertyRelative("id").stringValue;
        string category = SoundLibrary.NormalizeSfxCategory(
            entry.FindPropertyRelative("category").stringValue);
        AudioClip clip = entry.FindPropertyRelative("clip").objectReferenceValue as AudioClip;
        bool isDefined = IsDefinedSoundId(id);
        bool isDuplicate = !string.IsNullOrWhiteSpace(id)
            && idCounts.TryGetValue(id, out int count)
            && count > 1;
        SerializedProperty usages = entry.FindPropertyRelative("usages");
        bool isProblem = string.IsNullOrWhiteSpace(id)
            || clip == null
            || isDuplicate
            || HasDuplicateUsage(usages, usageCounts);

        if (selectedCategory != "전체"
            && !string.Equals(selectedCategory, category, StringComparison.Ordinal))
        {
            return false;
        }

        bool filterMatches = filter switch
        {
            EntryFilter.Defined => isDefined,
            EntryFilter.Custom => !isDefined,
            EntryFilter.MissingClip => clip == null,
            EntryFilter.Problem => isProblem,
            _ => true
        };
        if (!filterMatches)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        string query = searchText.Trim();
        return (!string.IsNullOrEmpty(id)
                && id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            || category.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            || (clip != null
                && clip.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            || UsageMatchesSearch(usages, query);
    }

    private void GetStatistics(
        out int total,
        out int definedCount,
        out int missingClipCount,
        out int problemCount)
    {
        total = sfxEntries.arraySize;
        definedCount = 0;
        missingClipCount = 0;
        problemCount = 0;
        Dictionary<string, int> counts = BuildIdCounts();
        Dictionary<SoundEvent, int> usageCounts = BuildUsageCounts();

        for (int i = 0; i < sfxEntries.arraySize; i++)
        {
            SerializedProperty entry = sfxEntries.GetArrayElementAtIndex(i);
            string id = entry.FindPropertyRelative("id").stringValue;
            bool missingClip =
                entry.FindPropertyRelative("clip").objectReferenceValue == null;
            bool duplicate = !string.IsNullOrWhiteSpace(id)
                && counts.TryGetValue(id, out int count)
                && count > 1;
            bool duplicateUsage = HasDuplicateUsage(
                entry.FindPropertyRelative("usages"),
                usageCounts);

            if (IsDefinedSoundId(id))
            {
                definedCount++;
            }

            if (missingClip)
            {
                missingClipCount++;
            }

            if (string.IsNullOrWhiteSpace(id)
                || missingClip
                || duplicate
                || duplicateUsage)
            {
                problemCount++;
            }
        }
    }

    private void GetUsageStatistics(
        out int assignedUsageCount,
        out int duplicateUsageCount)
    {
        assignedUsageCount = 0;
        duplicateUsageCount = 0;
        Dictionary<SoundEvent, int> counts = BuildUsageCounts();
        foreach (SoundEvent soundEvent in Enum.GetValues(typeof(SoundEvent)))
        {
            if (!counts.TryGetValue(soundEvent, out int count))
            {
                continue;
            }

            assignedUsageCount++;
            if (count > 1)
            {
                duplicateUsageCount++;
            }
        }
    }

    private List<string> BuildCategoryOptions()
    {
        HashSet<string> unique = new(StringComparer.Ordinal)
        {
            "전체"
        };
        for (int i = 0; i < sfxEntries.arraySize; i++)
        {
            SerializedProperty entry = sfxEntries.GetArrayElementAtIndex(i);
            unique.Add(SoundLibrary.NormalizeSfxCategory(
                entry.FindPropertyRelative("category").stringValue));
        }

        List<string> categories = new(unique);
        categories.Sort((left, right) =>
        {
            if (left == "전체")
            {
                return -1;
            }

            if (right == "전체")
            {
                return 1;
            }

            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        });
        return categories;
    }

    private Dictionary<string, int> BuildIdCounts()
    {
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        for (int i = 0; i < sfxEntries.arraySize; i++)
        {
            string id = sfxEntries
                .GetArrayElementAtIndex(i)
                .FindPropertyRelative("id")
                .stringValue;
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            counts.TryGetValue(id, out int count);
            counts[id] = count + 1;
        }

        return counts;
    }

    private Dictionary<SoundEvent, int> BuildUsageCounts()
    {
        Dictionary<SoundEvent, int> counts = new();
        for (int i = 0; i < sfxEntries.arraySize; i++)
        {
            SerializedProperty usages = sfxEntries
                .GetArrayElementAtIndex(i)
                .FindPropertyRelative("usages");
            for (int usageIndex = 0; usageIndex < usages.arraySize; usageIndex++)
            {
                SoundEvent soundEvent =
                    (SoundEvent)usages.GetArrayElementAtIndex(usageIndex).intValue;
                counts.TryGetValue(soundEvent, out int count);
                counts[soundEvent] = count + 1;
            }
        }

        return counts;
    }

    private static bool HasDuplicateUsage(
        SerializedProperty usages,
        IReadOnlyDictionary<SoundEvent, int> usageCounts)
    {
        for (int i = 0; i < usages.arraySize; i++)
        {
            SoundEvent soundEvent =
                (SoundEvent)usages.GetArrayElementAtIndex(i).intValue;
            if (usageCounts.TryGetValue(soundEvent, out int count) && count > 1)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsUsage(
        SerializedProperty usages,
        SoundEvent soundEvent)
    {
        for (int i = 0; i < usages.arraySize; i++)
        {
            if (usages.GetArrayElementAtIndex(i).intValue == (int)soundEvent)
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveUsage(
        SerializedProperty usages,
        SoundEvent soundEvent)
    {
        for (int i = usages.arraySize - 1; i >= 0; i--)
        {
            if (usages.GetArrayElementAtIndex(i).intValue == (int)soundEvent)
            {
                usages.DeleteArrayElementAtIndex(i);
            }
        }
    }

    private static bool UsageMatchesSearch(
        SerializedProperty usages,
        string query)
    {
        for (int i = 0; i < usages.arraySize; i++)
        {
            SoundEvent soundEvent =
                (SoundEvent)usages.GetArrayElementAtIndex(i).intValue;
            if (GetUsageLabel(soundEvent).IndexOf(
                    query,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildUsageSummary(SerializedProperty usages)
    {
        List<string> labels = new();
        for (int i = 0; i < usages.arraySize; i++)
        {
            labels.Add(GetUsageLabel(
                (SoundEvent)usages.GetArrayElementAtIndex(i).intValue));
        }

        return string.Join(", ", labels);
    }

    private string GetEntryId(int index)
    {
        if (index < 0 || index >= sfxEntries.arraySize)
        {
            return "미지정";
        }

        string id = sfxEntries
            .GetArrayElementAtIndex(index)
            .FindPropertyRelative("id")
            .stringValue;
        return string.IsNullOrWhiteSpace(id) ? $"#{index + 1} ID 없음" : id;
    }

    private static string GetUsageLabel(SoundEvent soundEvent)
    {
        return soundEvent.ToString().Replace("_", " / ");
    }

    private static string GetUsageMenuPath(SoundEvent soundEvent)
    {
        string value = soundEvent.ToString();
        int separatorIndex = value.IndexOf('_');
        return separatorIndex < 0
            ? value
            : $"{value[..separatorIndex]}/{value[(separatorIndex + 1)..]}";
    }

    private int FindEntryIndex(string id)
    {
        for (int i = 0; i < sfxEntries.arraySize; i++)
        {
            string candidate = sfxEntries
                .GetArrayElementAtIndex(i)
                .FindPropertyRelative("id")
                .stringValue;
            if (string.Equals(candidate, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private string CreateUniqueId(string baseId)
    {
        string normalized = string.IsNullOrWhiteSpace(baseId) ? "NewSfx" : baseId.Trim();
        if (FindEntryIndex(normalized) < 0)
        {
            return normalized;
        }

        for (int suffix = 2; ; suffix++)
        {
            string candidate = $"{normalized}_{suffix}";
            if (FindEntryIndex(candidate) < 0)
            {
                return candidate;
            }
        }
    }

    private static bool IsDefinedSoundId(string id)
    {
        return Enum.TryParse(id, out SoundId soundId)
            && string.Equals(soundId.ToLibraryId(), id, StringComparison.Ordinal);
    }

    private static void ResetEntry(
        SerializedProperty entry,
        string id,
        string category,
        AudioClip clip,
        float volume,
        float pitch)
    {
        entry.FindPropertyRelative("id").stringValue = id;
        entry.FindPropertyRelative("category").stringValue = category;
        entry.FindPropertyRelative("clip").objectReferenceValue = clip;
        entry.FindPropertyRelative("volume").floatValue = volume;
        entry.FindPropertyRelative("pitch").floatValue = pitch;
        entry.FindPropertyRelative("usages").ClearArray();
    }

    private static bool HasAudioClipInDrag()
    {
        for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
        {
            if (DragAndDrop.objectReferences[i] is AudioClip)
            {
                return true;
            }
        }

        return false;
    }

    private void TryRestoreLibrary()
    {
        if (Selection.activeObject is SoundLibrary selectedLibrary)
        {
            SetLibrary(selectedLibrary);
            return;
        }

        string savedPath = EditorPrefs.GetString(LastLibraryPathKey, string.Empty);
        SoundLibrary savedLibrary = string.IsNullOrEmpty(savedPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<SoundLibrary>(savedPath);
        if (savedLibrary != null)
        {
            SetLibrary(savedLibrary);
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:SoundLibrary");
        if (guids.Length > 0)
        {
            SetLibrary(AssetDatabase.LoadAssetAtPath<SoundLibrary>(
                AssetDatabase.GUIDToAssetPath(guids[0])));
        }
    }

    private void SetLibrary(SoundLibrary nextLibrary)
    {
        ApplyPendingChanges();
        library = nextLibrary;
        RefreshSerializedLibrary();

        string path = library != null ? AssetDatabase.GetAssetPath(library) : string.Empty;
        EditorPrefs.SetString(LastLibraryPathKey, path);
    }

    private void EnsureSerializedLibrary()
    {
        if (serializedLibrary == null || serializedLibrary.targetObject != library)
        {
            RefreshSerializedLibrary();
        }
    }

    private void RefreshSerializedLibrary()
    {
        serializedLibrary = library != null ? new SerializedObject(library) : null;
        sfxEntries = serializedLibrary?.FindProperty("sfxEntries");
    }

    private void ApplyPendingChanges()
    {
        if (serializedLibrary != null && serializedLibrary.targetObject != null)
        {
            serializedLibrary.ApplyModifiedProperties();
        }
    }

    private void ApplyAndRefresh()
    {
        serializedLibrary.ApplyModifiedProperties();
        EditorUtility.SetDirty(library);
        RefreshSerializedLibrary();
    }

    private void SaveLibrary()
    {
        ApplyPendingChanges();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssetIfDirty(library);
        ShowNotification(new GUIContent("SoundLibrary 저장 완료"));
    }

    private void HandleUndoRedo()
    {
        RefreshSerializedLibrary();
        Repaint();
    }
}
#endif
