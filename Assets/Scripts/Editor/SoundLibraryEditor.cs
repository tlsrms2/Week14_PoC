using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Week14.Audio;

[CustomEditor(typeof(SoundLibrary))]
public sealed class SoundLibraryEditor : Editor
{
    private const string SoundLibraryAssetPath =
        "Assets/Data/SoundLibrary/SoundLibrary.asset";
    private const string NewSoundsFolderPath = "Assets/Sounds/New";

    private static readonly Dictionary<string, string> replacementClipPathsBySfxId =
        new(StringComparer.Ordinal)
        {
            ["PlayerShot"] = "Assets/Sounds/Sfx_new/PlayerShot.wav",
            ["PlayerPowerShot"] = "Assets/Sounds/Sfx_new/PlayerPowerShot.wav",
            ["Smash"] = "Assets/Sounds/Sfx_new/Smash.wav",
            ["Parry"] = "Assets/Sounds/Sfx_new/Parry.wav",
            ["Execute"] = "Assets/Sounds/Sfx_new/Execute.wav",
            ["BulletLoss"] = "Assets/Sounds/Sfx_new/BulletLoss.wav",
            ["BossNormalShot"] = "Assets/Sounds/Sfx_new/BossNormalShot.wav",
            ["BossSpecialShot"] = "Assets/Sounds/Sfx_new/BossSpecialShot.wav",
            ["BossBomb"] = "Assets/Sounds/Sfx_new/Explosion_mp3cut.wav",
            ["PlayerDash"] = "Assets/Sounds/Sfx_new/Dash-01.wav",
            ["Title_Button"] = "Assets/Sounds/Sfx_new/Title_Button.wav",
            ["Panel_Hover"] = "Assets/Sounds/Sfx_new/Panel_Hover.wav",
            ["UI_Hover"] = "Assets/Sounds/Sfx_new/MouseOverBeep.wav",
            ["Gun_Equip"] = "Assets/Sounds/Sfx_new/GunEquip.wav",
            ["LightOn"] = "Assets/Sounds/Sfx_new/Lighton.wav",
            ["ShotgunFire"] = "Assets/Sounds/Sfx_new/ShotgunFire.wav",
            ["DaggerThrow"] = "Assets/Sounds/Sfx_new/AssassinThrow.wav",
            ["AssassinDash"] = "Assets/Sounds/Sfx_new/AssassinDash.wav",
            ["UI_Swoosh"] = "Assets/Sounds/Sfx_new/UI_Swoosh.wav",
            ["BaseballBatSwing"] = "Assets/Sounds/New/BaseballBatSwing.wav",
            ["BaseballBatCharging"] = "Assets/Sounds/New/BaseballBatCharge.wav",
            ["Walk"] = "Assets/Sounds/Sfx_new/Walk.wav",
            ["ChallengeResult"] = "Assets/Sounds/Sfx_new/ChallengeResult.wav",
            ["GetPoint"] = "Assets/Sounds/Sfx_new/GetPoint.wav",
            ["Whip"] = "Assets/Sounds/Sfx_new/whip.wav",
            ["Victory"] = "Assets/Sounds/Sfx_new/GameVictory.wav",
            ["Defeat"] = "Assets/Sounds/Sfx_new/GameOver.wav",
            ["PlayerDeath"] = "Assets/Sounds/Sfx_new/PlayerDeath.wav",
            ["BossCollapse"] = "Assets/Sounds/Sfx_new/BossCollapse.wav",
            ["BossCollapseBoom"] = "Assets/Sounds/Sfx_new/BossCollapseBoom.wav",
            ["goal complete"] = "Assets/Sounds/Sfx_new/튜토리얼 목표 완료.wav",
            ["robot rising"] = "Assets/Sounds/Sfx_new/로봇 올라올 때.wav",
            ["PausePanel"] = "Assets/Sounds/Sfx_new/일시정지 열기닫기.wav",
            ["PlayerDeath2"] = "Assets/Sounds/Sfx_new/플레이어 사망 직후.wav",
            ["TimeSlowSkill"] = "Assets/Sounds/Sfx_new/산데비스탄.wav",
            ["railgun1~4"] = "Assets/Sounds/Sfx_new/레일건_1~4.wav",
            ["railgun5"] = "Assets/Sounds/Sfx_new/레일건_5.wav",
            ["ButtonClick"] = "Assets/Sounds/New/ButtonClick (mp3cut.net).wav",
            ["Intro_Set"] = "Assets/Sounds/New/Intro_Set.wav",
            ["Intro_ZoomIn"] = "Assets/Sounds/New/Intro_ZoomIn.wav",
            ["Intro_Shutter"] = "Assets/Sounds/New/Intro_Shutter.wav",
            ["ResultPanelPopup"] = "Assets/Sounds/New/ResultPanelPopup.wav",
            ["TalkERIS"] = "Assets/Sounds/New/TalkERIS.wav",
            ["TalkLeeOn"] = "Assets/Sounds/New/TalkLeeOn.wav",
            ["TalkDad"] = "Assets/Sounds/New/TalkDad.wav",
            ["BaseballBatCharge"] = "Assets/Sounds/New/BaseballBatCharge.wav",
            ["BaseballBatHit"] = "Assets/Sounds/New/BaseballBatHit.wav",
            ["Boss_CreateParrySuppressionBait"] = "Assets/Sounds/New/Boss_CreateParrySuppressionBait.wav",
            ["Boss_Step"] = "Assets/Sounds/New/Boss_Step.wav",
            ["Boss_BeforeDash"] = "Assets/Sounds/New/Boss_BeforeDash.wav",
            ["Boss_CantParryingShot"] = "Assets/Sounds/New/Boss_CantParryingShot.wav",
            ["Module_Emergency"] = "Assets/Sounds/New/Module_Emergency.wav",
            ["Module_Magnet"] = "Assets/Sounds/New/Module_Magnet.wav",
            ["Module_Parrying"] = "Assets/Sounds/New/Module_Parrying.wav",
            ["Hog_MachinegunCharge"] = "Assets/Sounds/New/Hog_MachinegunCharge.wav",
            ["Hog_Death"] = "Assets/Sounds/New/Hog_Death.wav",
            ["Muscle_LineFire"] = "Assets/Sounds/New/Muscle_LineFire.wav",
            ["Muscle_Death"] = "Assets/Sounds/New/Muscle_Death.wav",
            ["Conductor_Draw"] = "Assets/Sounds/New/Conductor_Draw.wav",
            ["Conductor_DrawComplete"] = "Assets/Sounds/New/Conductor_DrawComplete.wav",
            ["Conductor_DroneIdle"] = "Assets/Sounds/New/Conductor_DroneIdle.wav",
            ["Conductor_PlaceStaff"] = "Assets/Sounds/New/Conductor_PlaceStaff.wav",
            ["Conductor_Death"] = "Assets/Sounds/New/Conductor_Death.wav",
            ["Assassin_Stealth"] = "Assets/Sounds/New/Assassin_Stealth.wav",
            ["Assassin_Mine"] = "Assets/Sounds/New/Assassin_Mine.wav",
            ["Assassin_Death"] = "Assets/Sounds/New/Assassin_Death.wav",
            ["Hacker_Walk"] = "Assets/Sounds/Real_New_Hacker/Hacker_Walk.wav",
            ["Hacker_Slash"] = "Assets/Sounds/Real_New_Hacker/Hacker_Slash.wav",
            ["Hacker_Lift"] = "Assets/Sounds/Real_New_Hacker/Hacker_Lift.wav",
            ["Hacker_Slam"] = "Assets/Sounds/Real_New_Hacker/Hacker_Slam.wav",
            ["Hacker_SlashMiss"] = "Assets/Sounds/Real_New_Hacker/Hacker_SlashMiss.wav",
            ["Hacker_SnipierCharge"] = "Assets/Sounds/Real_New_Hacker/Hacker_SnipierCharge_1.wav",
            ["Hacker_SnipierCharge_1"] = "Assets/Sounds/Real_New_Hacker/Hacker_SnipierCharge_1.wav",
            ["Hacker_SnipierCharge_1-5"] = "Assets/Sounds/Real_New_Hacker/Hacker_SnipierCharge_1-5.wav",
            ["Hacker_SnipierCharge_3-35"] = "Assets/Sounds/Real_New_Hacker/Hacker_SnipierCharge_3-35.wav",
            ["Hacker_ChargeDash"] = "Assets/Sounds/Real_New_Hacker/Hacker_ChargeDash.wav",
            ["Hacker_DropWeapon"] = "Assets/Sounds/Real_New_Hacker/Hacker_DropWeapon.wav",
            ["Hacker_OrbitSweep"] = "Assets/Sounds/Hacker_new/OrbitSweep.wav",
            ["Hacker_WireNode"] = "Assets/Sounds/Real_New_Hacker/Hacker_WireNode.wav",
            ["BigSlash"] = "Assets/Sounds/Real_New_Hacker/Hacker_Slam.wav",
            ["Slash"] = "Assets/Sounds/Real_New_Hacker/Hacker_Slash.wav",
            ["MissSlash"] = "Assets/Sounds/Real_New_Hacker/Hacker_SlashMiss.wav",
            ["Dash"] = "Assets/Sounds/Hacker_new/Dash.wav",
            ["ChargeDash"] = "Assets/Sounds/Real_New_Hacker/Hacker_ChargeDash.wav",
            ["OrbitSweep"] = "Assets/Sounds/Hacker_new/OrbitSweep.wav",
            ["FireWire"] = "Assets/Sounds/Hacker_new/FireWire.wav",
            ["WireFlight"] = "Assets/Sounds/Hacker_new/WireFlight.wav",
            ["Wire"] = "Assets/Sounds/Real_New_Hacker/Hacker_WireNode.wav",
            ["Hologram"] = "Assets/Sounds/Hacker_new/Hologram.wav",
            ["GunCharge"] = "Assets/Sounds/Real_New_Hacker/Hacker_SnipierCharge_1.wav",
            ["BeforeAttack"] = "Assets/Sounds/Hacker_new/BeforeAttack.wav",
            ["SnipierShot"] = "Assets/Sounds/Hacker_new/SnipierShot.wav",
            ["PutTurret"] = "Assets/Sounds/Real_New_Hacker/Hacker_DropWeapon.wav"
        };

    private readonly Dictionary<string, bool> categoryFoldouts =
        new(StringComparer.OrdinalIgnoreCase);

    private SerializedProperty bgmEntries;
    private SerializedProperty sfxEntries;
    private string searchText = string.Empty;

    [MenuItem("Tools/Week14/Audio/Sounds New 적용")]
    public static void ApplyNewSoundsFromMenu()
    {
        ApplyReplacementSounds(true);
    }

    [MenuItem("Tools/Week14/Audio/신규 사운드 리소스 적용")]
    public static void ApplyReplacementSoundsFromMenu()
    {
        ApplyReplacementSounds(true);
    }

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

        if (GUILayout.Button("신규 사운드 리소스 적용"))
        {
            serializedObject.ApplyModifiedProperties();
            ApplyReplacementSounds(true);
            serializedObject.Update();
            GUIUtility.ExitGUI();
        }

        if (GUILayout.Button("Sfx_new, Hacker_new 교체 (볼륨 1)"))
        {
            serializedObject.ApplyModifiedProperties();
            ApplyReplacementSounds(true);
            serializedObject.Update();
            GUIUtility.ExitGUI();
        }
    }

    private static void ApplyReplacementSounds(bool showResult)
    {
        SoundLibrary library =
            AssetDatabase.LoadAssetAtPath<SoundLibrary>(SoundLibraryAssetPath);
        if (library == null)
        {
            Debug.LogError($"SoundLibrary를 찾을 수 없습니다: {SoundLibraryAssetPath}");
            return;
        }

        SerializedObject libraryObject = new(library);
        SerializedProperty entries = libraryObject.FindProperty("sfxEntries");
        List<string> missingIds = new();
        List<string> missingClips = new();
        int changedCount = 0;

        foreach (KeyValuePair<string, string> pair in replacementClipPathsBySfxId)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(pair.Value);
            if (clip == null)
            {
                missingClips.Add(pair.Value);
                continue;
            }

            SerializedProperty entry = FindSfxEntry(entries, pair.Key);
            if (entry == null)
            {
                if (UpsertSfxEntry(entries, pair.Key, ResolveCategory(pair.Key), clip, 1f))
                {
                    changedCount++;
                }
                continue;
            }

            SerializedProperty clipProperty = entry.FindPropertyRelative("clip");
            SerializedProperty volumeProperty = entry.FindPropertyRelative("volume");
            if (clipProperty.objectReferenceValue != clip)
            {
                clipProperty.objectReferenceValue = clip;
                changedCount++;
            }

            if (!Mathf.Approximately(volumeProperty.floatValue, 1f))
            {
                volumeProperty.floatValue = 1f;
                changedCount++;
            }
        }

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty volumeProperty = entries
                .GetArrayElementAtIndex(i)
                .FindPropertyRelative("volume");
            if (Mathf.Approximately(volumeProperty.floatValue, 1f))
            {
                continue;
            }

            volumeProperty.floatValue = 1f;
            changedCount++;
        }

        if (changedCount > 0)
        {
            libraryObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }

        if (missingIds.Count > 0)
        {
            Debug.LogWarning($"SoundLibrary에 없는 ID: {string.Join(", ", missingIds)}");
        }

        if (missingClips.Count > 0)
        {
            Debug.LogWarning($"불러오지 못한 새 SFX: {string.Join(", ", missingClips)}");
        }

        if (showResult)
        {
            Selection.activeObject = library;
            EditorGUIUtility.PingObject(library);
            Debug.Log(
                $"Sfx_new/Hacker_new 교체 완료: " +
                $"{replacementClipPathsBySfxId.Count - missingIds.Count - missingClips.Count}개 매칭, " +
                $"{changedCount}개 값 변경");
        }
    }

    private static void ApplyNewSounds(bool showResult)
    {
        SoundLibrary library =
            AssetDatabase.LoadAssetAtPath<SoundLibrary>(SoundLibraryAssetPath);
        if (library == null)
        {
            Debug.LogError($"SoundLibrary를 찾을 수 없습니다: {SoundLibraryAssetPath}");
            return;
        }

        string[] clipGuids = AssetDatabase.FindAssets(
            "t:AudioClip",
            new[] { NewSoundsFolderPath });
        Array.Sort(clipGuids, StringComparer.Ordinal);

        SerializedObject libraryObject = new(library);
        SerializedProperty entries = libraryObject.FindProperty("sfxEntries");
        int changedCount = 0;

        for (int i = 0; i < clipGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(clipGuids[i]);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null)
            {
                continue;
            }

            if (UpsertSfxEntry(
                    entries,
                    clip.name,
                    ResolveCategory(clip.name),
                    clip,
                    ResolveDefaultVolume(clip.name)))
            {
                changedCount++;
            }
        }

        if (changedCount > 0)
        {
            libraryObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }

        if (showResult)
        {
            Selection.activeObject = library;
            EditorGUIUtility.PingObject(library);
            Debug.Log(
                $"Sounds/New 적용 완료: {clipGuids.Length}개 파일, " +
                $"{changedCount}개 SoundLibrary 항목 변경");
        }
    }

    private static bool UpsertSfxEntry(
        SerializedProperty entries,
        string id,
        string category,
        AudioClip clip,
        float defaultVolume)
    {
        SerializedProperty entry = FindSfxEntry(entries, id);
        bool created = entry == null;
        if (created)
        {
            int index = entries.arraySize;
            entries.InsertArrayElementAtIndex(index);
            entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("category").stringValue = category;
            entry.FindPropertyRelative("id").stringValue = id;
            entry.FindPropertyRelative("volume").floatValue = defaultVolume;
            entry.FindPropertyRelative("pitch").floatValue = 1f;
        }

        SerializedProperty clipProperty = entry.FindPropertyRelative("clip");
        if (!created && clipProperty.objectReferenceValue == clip)
        {
            return false;
        }

        clipProperty.objectReferenceValue = clip;
        return true;
    }

    private static SerializedProperty FindSfxEntry(
        SerializedProperty entries,
        string id)
    {
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            if (string.Equals(
                    entry.FindPropertyRelative("id").stringValue,
                    id,
                    StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }

    private static string ResolveCategory(string id)
    {
        if (id.StartsWith("Talk", StringComparison.Ordinal))
        {
            return "dialogue";
        }

        if (id.StartsWith("Hacker_", StringComparison.Ordinal))
        {
            return "hacker";
        }

        if (id.StartsWith("Hog_", StringComparison.Ordinal))
        {
            return "hog";
        }

        if (id.StartsWith("Muscle_", StringComparison.Ordinal))
        {
            return "muscle";
        }

        if (id.StartsWith("Conductor_", StringComparison.Ordinal))
        {
            return "conductor";
        }

        if (id.StartsWith("Assassin_", StringComparison.Ordinal))
        {
            return "assassin";
        }

        if (id.StartsWith("Boss_", StringComparison.Ordinal))
        {
            return "boss";
        }

        return id.StartsWith("BaseballBat", StringComparison.Ordinal)
            || id.StartsWith("Module_", StringComparison.Ordinal)
                ? "player"
                : "ui";
    }

    private static float ResolveDefaultVolume(string id)
    {
        return 1f;
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

            float availableWidth = Mathf.Max(240f, EditorGUIUtility.currentViewWidth - 52f);
            float columnWidth = (availableWidth - 4f) * 0.5f;

            for (int i = 0; i < visibleIndices.Count; i += 2)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawSfxEntry(visibleIndices[i], columnWidth);
                    if (i + 1 < visibleIndices.Count)
                    {
                        DrawSfxEntry(visibleIndices[i + 1], columnWidth);
                    }
                    else
                    {
                        GUILayout.Space(columnWidth);
                    }
                }
            }

            if (GUILayout.Button($"{category}에 SFX 추가"))
            {
                AddSfx(category);
            }
        }

        EditorGUILayout.Space(2f);
    }

    private void DrawSfxEntry(int index, float width)
    {
        SerializedProperty entry = sfxEntries.GetArrayElementAtIndex(index);
        SerializedProperty id = entry.FindPropertyRelative("id");
        string label = string.IsNullOrWhiteSpace(id.stringValue)
            ? $"#{index + 1} <ID 없음>"
            : $"#{index + 1} {id.stringValue}";

        using (new EditorGUILayout.VerticalScope(
                   EditorStyles.helpBox,
                   GUILayout.Width(width)))
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

            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(width * 0.38f, 58f, 90f);
            try
            {
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
            finally
            {
                EditorGUIUtility.labelWidth = previousLabelWidth;
            }
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
