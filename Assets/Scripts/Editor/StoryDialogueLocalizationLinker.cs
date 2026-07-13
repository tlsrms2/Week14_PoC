using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using Week14.Cutscene;
using Week14.Story;
using Week14.Tutorial;
using SharedTableEntry = UnityEngine.Localization.Tables.SharedTableData.SharedTableEntry;

namespace Week14.EditorTools
{
    // Assets/Data/Dialogue 아래의 대사 SO들을 Story 문자열 테이블에 연결하는 1회성 도구입니다.
    // - 본문(text)/설명(title, text)은 줄마다 고유한 항목을 새로 만듭니다.
    // - 화자/이름(speaker, name)은 같은 이름이 이미 나온 적 있으면 그 항목을 재사용합니다(중복 생성 방지).
    // - 이미 로컬라이징이 연결된 필드(LocalizedReference.IsEmpty == false)는 건드리지 않아 재실행해도 안전합니다.
    // - 영어(en) 로케일 값은 채우지 않습니다(번역 전이라 비워둠).
    internal static class StoryDialogueLocalizationLinker
    {
        private const string StoryTableName = "Story";
        private const string KoreanLocaleCode = "ko-KR";

        private static readonly (string path, string keyPrefix)[] CutsceneAssets =
        {
            ("Assets/Data/Dialogue/Cutscene/01-Prologue.asset", "Cutscene_01-Prologue"),
            ("Assets/Data/Dialogue/Cutscene/02-Record.asset", "Cutscene_02-Record"),
            ("Assets/Data/Dialogue/Cutscene/07-Epilogue.asset", "Cutscene_07-Epilogue"),
        };

        private static readonly (string path, string keyPrefix)[] InGameStoryAssets =
        {
            ("Assets/Data/Dialogue/InGame/03-Act1.asset", "InGame_03-Act1"),
            ("Assets/Data/Dialogue/InGame/04-Act2.asset", "InGame_04-Act2"),
            ("Assets/Data/Dialogue/InGame/05-Act3.asset", "InGame_05-Act3"),
            ("Assets/Data/Dialogue/03-1-LobbyTutorial-Boss.asset", "03-1-LobbyTutorial-Boss"),
            ("Assets/Data/Dialogue/03-2-LobbyTutorial-Skill.asset", "03-2-LobbyTutorial-Skill"),
        };

        private const string TutorialDialogueSetPath = "Assets/Data/Dialogue/TutorialDialogueSet.asset";
        private const string TutorialDialogueSetKeyPrefix = "TutorialDialogueSet";

        [MenuItem("Tools/Localization/Link Story Dialogue To Story Table")]
        private static void Link()
        {
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(StoryTableName);
            if (collection == null)
            {
                Debug.LogError($"'{StoryTableName}' 문자열 테이블 컬렉션을 찾을 수 없습니다.");
                return;
            }

            StringTable koreanTable = collection.StringTables.FirstOrDefault(t => t != null && t.LocaleIdentifier.Code == KoreanLocaleCode);
            if (koreanTable == null)
            {
                Debug.LogError($"'{StoryTableName}' 테이블에서 {KoreanLocaleCode} 로케일 테이블을 찾을 수 없습니다.");
                return;
            }

            SharedTableData sharedData = collection.SharedData;
            Dictionary<string, SharedTableEntry> speakerEntries = new();
            int linkedCount = 0;

            foreach ((string path, string keyPrefix) in CutsceneAssets)
            {
                LinkCutsceneAsset(path, keyPrefix, collection, sharedData, koreanTable, speakerEntries, ref linkedCount);
            }

            foreach ((string path, string keyPrefix) in InGameStoryAssets)
            {
                LinkInGameStoryAsset(path, keyPrefix, collection, sharedData, koreanTable, speakerEntries, ref linkedCount);
            }

            LinkTutorialDialogueSet(TutorialDialogueSetPath, TutorialDialogueSetKeyPrefix, collection, sharedData, koreanTable, speakerEntries, ref linkedCount);

            EditorUtility.SetDirty(sharedData);
            EditorUtility.SetDirty(koreanTable);
            AssetDatabase.SaveAssets();
            Debug.Log($"[StoryDialogueLocalizationLinker] Story 테이블 연결 완료 — 새로 연결된 필드 {linkedCount}개.");
        }

        private static void LinkCutsceneAsset(
            string path,
            string keyPrefix,
            StringTableCollection collection,
            SharedTableData sharedData,
            StringTable koreanTable,
            Dictionary<string, SharedTableEntry> speakerEntries,
            ref int linkedCount)
        {
            CutsceneDefinition cutscene = AssetDatabase.LoadAssetAtPath<CutsceneDefinition>(path);
            if (cutscene == null)
            {
                Debug.LogWarning($"[StoryDialogueLocalizationLinker] {path} 를 찾을 수 없습니다.");
                return;
            }

            int index = 0;
            foreach (CutsceneStep step in cutscene.Steps)
            {
                if (step == null)
                {
                    continue;
                }

                foreach (CutsceneDialogue dialogue in step.Dialogues)
                {
                    if (dialogue == null)
                    {
                        continue;
                    }

                    index++;
                    LinkSpeaker(dialogue.Name, dialogue.LocalizedName, collection, sharedData, koreanTable, speakerEntries, ref linkedCount);
                    LinkUnique($"{keyPrefix}_{index:00}_Text", dialogue.Text, dialogue.LocalizedText, collection, sharedData, koreanTable, ref linkedCount);
                }
            }

            EditorUtility.SetDirty(cutscene);
        }

        private static void LinkInGameStoryAsset(
            string path,
            string keyPrefix,
            StringTableCollection collection,
            SharedTableData sharedData,
            StringTable koreanTable,
            Dictionary<string, SharedTableEntry> speakerEntries,
            ref int linkedCount)
        {
            InGameStorySequenceSO sequence = AssetDatabase.LoadAssetAtPath<InGameStorySequenceSO>(path);
            if (sequence == null)
            {
                Debug.LogWarning($"[StoryDialogueLocalizationLinker] {path} 를 찾을 수 없습니다.");
                return;
            }

            int index = 0;
            foreach (InGameDialogueLine line in sequence.Dialogues)
            {
                if (line == null)
                {
                    continue;
                }

                index++;
                LinkSpeaker(line.Speaker, line.LocalizedSpeaker, collection, sharedData, koreanTable, speakerEntries, ref linkedCount);
                LinkUnique($"{keyPrefix}_{index:00}_Text", line.Text, line.LocalizedText, collection, sharedData, koreanTable, ref linkedCount);
            }

            EditorUtility.SetDirty(sequence);
        }

        private static void LinkTutorialDialogueSet(
            string path,
            string keyPrefix,
            StringTableCollection collection,
            SharedTableData sharedData,
            StringTable koreanTable,
            Dictionary<string, SharedTableEntry> speakerEntries,
            ref int linkedCount)
        {
            TutorialDialogueSetSO tutorialSet = AssetDatabase.LoadAssetAtPath<TutorialDialogueSetSO>(path);
            if (tutorialSet == null)
            {
                Debug.LogWarning($"[StoryDialogueLocalizationLinker] {path} 를 찾을 수 없습니다.");
                return;
            }

            int index = 0;
            foreach (TutorialStepContent step in tutorialSet.Steps)
            {
                if (step == null)
                {
                    continue;
                }

                foreach (TutorialDialogueLine line in step.Dialogues)
                {
                    if (line == null)
                    {
                        continue;
                    }

                    index++;
                    LinkSpeaker(line.Speaker, line.LocalizedSpeaker, collection, sharedData, koreanTable, speakerEntries, ref linkedCount);
                    LinkUnique($"{keyPrefix}_{index:00}_Text", line.Text, line.LocalizedText, collection, sharedData, koreanTable, ref linkedCount);

                    TutorialExplanationContent explanation = line.Explanation;
                    if (explanation != null && explanation.HasContent)
                    {
                        LinkUnique($"{keyPrefix}_{index:00}_ExplanationTitle", explanation.Title, explanation.LocalizedTitle, collection, sharedData, koreanTable, ref linkedCount);
                        LinkUnique($"{keyPrefix}_{index:00}_ExplanationText", explanation.Text, explanation.LocalizedText, collection, sharedData, koreanTable, ref linkedCount);
                    }
                }
            }

            EditorUtility.SetDirty(tutorialSet);
        }

        // 줄마다 고유한 항목(본문/설명)을 새로 만들어 연결합니다.
        private static void LinkUnique(
            string key,
            string sourceValue,
            LocalizedString target,
            StringTableCollection collection,
            SharedTableData sharedData,
            StringTable koreanTable,
            ref int linkedCount)
        {
            if (target == null || !target.IsEmpty || string.IsNullOrEmpty(sourceValue))
            {
                return;
            }

            SharedTableEntry entry = sharedData.GetEntry(key) ?? sharedData.AddKey(key);
            koreanTable.AddEntry(entry.Id, sourceValue);
            target.SetReference(collection.TableCollectionNameReference, entry.Id);
            linkedCount++;
        }

        // 화자/이름은 같은 값이 이미 등록됐으면 그 항목을 재사용합니다.
        private static void LinkSpeaker(
            string speakerName,
            LocalizedString target,
            StringTableCollection collection,
            SharedTableData sharedData,
            StringTable koreanTable,
            Dictionary<string, SharedTableEntry> speakerEntries,
            ref int linkedCount)
        {
            if (target == null || !target.IsEmpty || string.IsNullOrWhiteSpace(speakerName))
            {
                return;
            }

            if (!speakerEntries.TryGetValue(speakerName, out SharedTableEntry entry))
            {
                string key = $"Speaker_{speakerName.Trim().Replace(' ', '_')}";
                entry = sharedData.GetEntry(key) ?? sharedData.AddKey(key);
                koreanTable.AddEntry(entry.Id, speakerName);
                speakerEntries[speakerName] = entry;
            }

            target.SetReference(collection.TableCollectionNameReference, entry.Id);
            linkedCount++;
        }
    }
}
