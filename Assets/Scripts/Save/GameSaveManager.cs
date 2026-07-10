using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Week14.Save
{
    public static class GameSaveManager
    {
        private const string SaveFileName = "game_data.json";
        private const string FirstBossId = "1";

        private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        private static GameSaveData data;

        private static GameSaveData Data
        {
            get
            {
                if (data == null)
                {
                    Load();
                }

                return data;
            }
        }

        public static bool IsUnlocked(string bossId)
        {
            return !string.IsNullOrEmpty(bossId) && Data.unlockedBossIds.Contains(bossId);
        }

        public static bool IsCleared(string bossId)
        {
            return !string.IsNullOrEmpty(bossId) && Data.clearedBossIds.Contains(bossId);
        }

        public static void UnlockDefaultBoss()
        {
            UnlockBoss(FirstBossId);
        }

        public static void UnlockBoss(string bossId)
        {
            if (string.IsNullOrEmpty(bossId) || Data.unlockedBossIds.Contains(bossId))
            {
                return;
            }

            Data.unlockedBossIds.Add(bossId);
            Save();
        }

        public static void LockBoss(string bossId)
        {
            if (string.IsNullOrEmpty(bossId) || !Data.unlockedBossIds.Remove(bossId))
            {
                return;
            }

            Save();
        }

        public static void ClearBoss(string bossId)
        {
            if (string.IsNullOrEmpty(bossId) || Data.clearedBossIds.Contains(bossId))
            {
                return;
            }

            Data.clearedBossIds.Add(bossId);
            Save();
        }

        public static void UnclearBoss(string bossId)
        {
            if (string.IsNullOrEmpty(bossId) || !Data.clearedBossIds.Remove(bossId))
            {
                return;
            }

            Save();
        }

        public static bool HasSeenPrologue => Data.hasSeenPrologue || Data.hasSeenSynopsis;
        public static bool HasSeenPast => Data.hasSeenPast;
        public static bool HasCompletedTutorial => Data.hasCompletedTutorial;
        public static bool HasSeenEnding => Data.hasSeenEnding;
        public static bool HasSeenSynopsis => HasSeenPrologue;

        public static bool HasSeenStoryEpisode(string episodeId)
        {
            return !string.IsNullOrEmpty(episodeId)
                && Data.seenStoryEpisodeIds.Contains(episodeId);
        }

        public static void MarkStoryEpisodeSeen(string episodeId)
        {
            if (string.IsNullOrEmpty(episodeId)
                || Data.seenStoryEpisodeIds.Contains(episodeId))
            {
                return;
            }

            Data.seenStoryEpisodeIds.Add(episodeId);
            Save();
        }

        public static void SetStoryEpisodeSeen(string episodeId, bool seen)
        {
            if (string.IsNullOrEmpty(episodeId))
            {
                return;
            }

            bool changed = seen
                ? AddIfMissing(Data.seenStoryEpisodeIds, episodeId)
                : Data.seenStoryEpisodeIds.Remove(episodeId);

            if (changed)
            {
                Save();
            }
        }

        public static void MarkPrologueSeen()
        {
            if (Data.hasSeenPrologue && Data.hasSeenSynopsis)
            {
                return;
            }

            Data.hasSeenPrologue = true;
            Data.hasSeenSynopsis = true;
            Save();
        }

        public static void SetPrologueSeen(bool seen)
        {
            if (Data.hasSeenPrologue == seen && Data.hasSeenSynopsis == seen)
            {
                return;
            }

            Data.hasSeenPrologue = seen;
            Data.hasSeenSynopsis = seen;
            Save();
        }

        public static void MarkPastSeen()
        {
            if (Data.hasSeenPast)
            {
                return;
            }

            Data.hasSeenPast = true;
            Save();
        }

        public static void SetPastSeen(bool seen)
        {
            if (Data.hasSeenPast == seen)
            {
                return;
            }

            Data.hasSeenPast = seen;
            Save();
        }

        public static void MarkSynopsisSeen()
        {
            MarkPrologueSeen();
        }

        public static void SetSynopsisSeen(bool seen)
        {
            SetPrologueSeen(seen);
        }

        public static void MarkTutorialCompleted()
        {
            bool changed = !Data.hasCompletedTutorial;
            if (!Data.hasCompletedTutorial)
            {
                Data.hasCompletedTutorial = true;
            }

            if (!Data.unlockedBossIds.Contains(FirstBossId))
            {
                Data.unlockedBossIds.Add(FirstBossId);
                changed = true;
            }

            if (changed)
            {
                Save();
            }
        }

        public static void SetTutorialCompleted(bool completed)
        {
            if (completed)
            {
                MarkTutorialCompleted();
                return;
            }

            if (!Data.hasCompletedTutorial)
            {
                return;
            }

            Data.hasCompletedTutorial = false;
            Save();
        }

        public static void MarkEndingSeen()
        {
            if (Data.hasSeenEnding)
            {
                return;
            }

            Data.hasSeenEnding = true;
            Save();
        }

        public static void SetEndingSeen(bool seen)
        {
            if (Data.hasSeenEnding == seen)
            {
                return;
            }

            Data.hasSeenEnding = seen;
            Save();
        }

        public static void ResetStoryProgress()
        {
            bool changed = Data.hasSeenPrologue
                || Data.hasSeenPast
                || Data.hasSeenSynopsis
                || Data.hasCompletedTutorial
                || Data.hasSeenEnding
                || Data.seenStoryEpisodeIds.Count > 0;

            if (!changed)
            {
                return;
            }

            Data.hasSeenPrologue = false;
            Data.hasSeenPast = false;
            Data.hasSeenSynopsis = false;
            Data.hasCompletedTutorial = false;
            Data.hasSeenEnding = false;
            Data.seenStoryEpisodeIds.Clear();
            Save();
        }

        public static void ResetPrologueSeen()
        {
            if (!Data.hasSeenPrologue && !Data.hasSeenSynopsis)
            {
                return;
            }

            Data.hasSeenPrologue = false;
            Data.hasSeenSynopsis = false;
            Save();
        }

        public static void ResetPastSeen()
        {
            if (!Data.hasSeenPast)
            {
                return;
            }

            Data.hasSeenPast = false;
            Save();
        }

        public static void ResetSynopsisSeen()
        {
            ResetPrologueSeen();
        }

        public static void ResetTutorialCompleted()
        {
            if (!Data.hasCompletedTutorial)
            {
                return;
            }

            Data.hasCompletedTutorial = false;
            Save();
        }

        public static void ResetEndingSeen()
        {
            if (!Data.hasSeenEnding)
            {
                return;
            }

            Data.hasSeenEnding = false;
            Save();
        }

        public static IReadOnlyList<string> UnlockedSkillIds => Data.unlockedSkillIds;

        public static bool IsSkillUnlocked(string skillId)
        {
            return !string.IsNullOrEmpty(skillId) && Data.unlockedSkillIds.Contains(skillId);
        }

        public static void UnlockSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId) || Data.unlockedSkillIds.Contains(skillId))
            {
                return;
            }

            Data.unlockedSkillIds.Add(skillId);
            Save();
        }

        public static void LockSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId) || !Data.unlockedSkillIds.Remove(skillId))
            {
                return;
            }

            Save();
        }

        public static string GetEquippedSkillId(int slot)
        {
            List<SkillSlotData> equippedSkills = Data.equippedSkills;
            for (int i = 0; i < equippedSkills.Count; i++)
            {
                if (equippedSkills[i].slot == slot)
                {
                    return equippedSkills[i].skillId;
                }
            }

            return null;
        }

        public static void SetEquippedSkillId(int slot, string skillId)
        {
            List<SkillSlotData> equippedSkills = Data.equippedSkills;
            for (int i = 0; i < equippedSkills.Count; i++)
            {
                if (equippedSkills[i].slot == slot)
                {
                    equippedSkills[i].skillId = skillId;
                    Save();
                    return;
                }
            }

            equippedSkills.Add(new SkillSlotData { slot = slot, skillId = skillId });
            Save();
        }

        public static IReadOnlyList<string> UnlockedWeaponIds => Data.unlockedWeaponIds;

        public static bool IsWeaponUnlocked(string weaponId)
        {
            return !string.IsNullOrEmpty(weaponId) && Data.unlockedWeaponIds.Contains(weaponId);
        }

        public static void UnlockWeapon(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId) || Data.unlockedWeaponIds.Contains(weaponId))
            {
                return;
            }

            Data.unlockedWeaponIds.Add(weaponId);
            Save();
        }

        public static void LockWeapon(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId) || !Data.unlockedWeaponIds.Remove(weaponId))
            {
                return;
            }

            Save();
        }

        public static string GetEquippedWeaponId()
        {
            return Data.equippedWeaponId;
        }

        public static void SetEquippedWeaponId(string weaponId)
        {
            Data.equippedWeaponId = weaponId;
            Save();
        }

        public static string BuildChallengeSaveKey(string bossId, string challengeId)
        {
            return $"{bossId}:{challengeId}";
        }

        public static bool IsChallengeCompleted(string challengeId)
        {
            return !string.IsNullOrEmpty(challengeId) && Data.completedChallengeIds.Contains(challengeId);
        }

        public static void CompleteChallenge(string challengeId, int rewardPoint)
        {
            if (string.IsNullOrEmpty(challengeId) || Data.completedChallengeIds.Contains(challengeId))
            {
                return;
            }

            Data.completedChallengeIds.Add(challengeId);
            Data.challengePoints += rewardPoint;
            Save();
        }

        public static int GetChallengeCounter(string challengeId)
        {
            if (string.IsNullOrEmpty(challengeId))
            {
                return 0;
            }

            List<ChallengeCounterEntry> counters = Data.challengeCounters;
            for (int i = 0; i < counters.Count; i++)
            {
                if (counters[i].challengeId == challengeId)
                {
                    return counters[i].count;
                }
            }

            return 0;
        }

        public static int IncrementChallengeCounter(string challengeId)
        {
            if (string.IsNullOrEmpty(challengeId))
            {
                return 0;
            }

            List<ChallengeCounterEntry> counters = Data.challengeCounters;
            for (int i = 0; i < counters.Count; i++)
            {
                if (counters[i].challengeId == challengeId)
                {
                    counters[i].count++;
                    Save();
                    return counters[i].count;
                }
            }

            counters.Add(new ChallengeCounterEntry { challengeId = challengeId, count = 1 });
            Save();
            return 1;
        }

        public static int ChallengePoints => Data.challengePoints;

        public static void Load()
        {
            try
            {
                data = File.Exists(SavePath) ? JsonUtility.FromJson<GameSaveData>(File.ReadAllText(SavePath)) : new GameSaveData();
            }
            catch (Exception)
            {
                data = new GameSaveData();
            }

            NormalizeLoadedData();
            UnlockBoss(FirstBossId);
        }

        private static void NormalizeLoadedData()
        {
            data ??= new GameSaveData();
            data.unlockedBossIds ??= new List<string>();
            data.clearedBossIds ??= new List<string>();
            data.unlockedSkillIds ??= new List<string>();
            data.unlockedWeaponIds ??= new List<string>();
            data.equippedSkills ??= new List<SkillSlotData>();
            data.seenStoryEpisodeIds ??= new List<string>();
            data.completedChallengeIds ??= new List<string>();
            data.challengeCounters ??= new List<ChallengeCounterEntry>();
        }

        private static bool AddIfMissing(List<string> list, string value)
        {
            if (list == null || string.IsNullOrEmpty(value) || list.Contains(value))
            {
                return false;
            }

            list.Add(value);
            return true;
        }

        public static void Save()
        {
            string tempPath = SavePath + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(Data, true));

            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }

            File.Move(tempPath, SavePath);
        }
    }
}
