using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Week14.Skills;
using Week14.UI;
using Week14.Weapons;

namespace Week14.Save
{
    public static class GameSaveManager
    {
        private const string SaveFolderName = "Saves";
        private const string ConfigResourcePath = "GameSaveConfig";
        private const string FallbackFirstBossId = "1";

        public const int SlotCount = 3;

        // 슬롯 선택 패널을 거치지 않고 씬을 바로 재생하는 에디터 디버그 상황을 위한 기본값입니다.
        private static int currentSlot;

        public static int CurrentSlot => currentSlot;

        // 슬롯이 실제로 바뀔 때만 발생합니다. WeaponLoadoutManager/SkillLoadoutManager처럼 세이브를
        // 앱 생애주기 중 한 번만 읽고 DontDestroyOnLoad로 메모리에 캐시해두는 매니저들이, 슬롯 전환
        // 시 이전 슬롯의 장비/스킬을 그대로 들고 새 슬롯(새 게임 튜토리얼 포함)에 넘겨버리는 문제를
        // 막기 위한 훅입니다 — 구독자는 이 이벤트를 받으면 세이브에서 다시 로드해야 합니다.
        public static event Action SlotChanged;

        private static string GetSlotPath(int slot)
        {
            return Path.Combine(Application.persistentDataPath, SaveFolderName, $"game_data_{slot}.json");
        }

        private static string SavePath => GetSlotPath(currentSlot);

        // 슬롯을 전환하고 캐시를 무효화합니다. 다음 Data 접근 시 해당 슬롯 파일을 새로 Load()합니다.
        // 슬롯 번호가 같아도 data가 이미 null(=DeleteSlot 등으로 무효화된 상태)이면 스킵하지 않습니다.
        // 그래야 "같은 슬롯을 지운 뒤 그 슬롯으로 곧장 새 게임 시작" 케이스에서도 SlotChanged가 발생해
        // WeaponLoadoutManager 등의 캐시가 옛 장착 상태를 그대로 들고 가지 않습니다.
        public static void SelectSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount)
            {
                return;
            }

            if (slot == currentSlot && data != null)
            {
                return;
            }

            currentSlot = slot;
            data = null;
            SlotChanged?.Invoke();
        }

        // Data/Load()를 거치지 않고 파일 존재 여부만 확인합니다.
        // Data에 접근하면 기본 해금 로직이 돌며 파일이 즉시 생성되므로, "슬롯이 비어있는지" 판정에는
        // 반드시 이 메서드를 써야 합니다.
        public static bool DoesSlotExist(int slot)
        {
            return slot >= 0 && slot < SlotCount && File.Exists(GetSlotPath(slot));
        }

        // 해당 슬롯의 세이브 파일을 삭제합니다. 삭제 대상이 현재 활성 슬롯이면 메모리 캐시도 무효화합니다.
        // 여기서는 SlotChanged를 쏘지 않습니다 — 쏘면 구독자들이 Data(=GetEquippedWeaponId 등)에
        // 접근하면서 Load()의 기본 해금 로직이 파일을 즉시 재생성해버려, 삭제 확인 직후 슬롯 목록이
        // "새 게임"이 아니라 "이어하기"로 보이는 부작용이 생깁니다. 리로드는 이 슬롯이 실제로 다시
        // 선택될 때(SelectSlot)까지 미룹니다.
        public static void DeleteSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount)
            {
                return;
            }

            string path = GetSlotPath(slot);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            string tempPath = path + ".tmp";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            if (slot == currentSlot)
            {
                data = null;
            }
        }

        // Data/Load()를 거치지 않고 파일에서 완료한 챌린지 개수만 직접 읽습니다(슬롯 진행도 미리보기용).
        // DoesSlotExist와 마찬가지로 Data에 접근하지 않으므로 다른 슬롯의 기본 해금/저장을 유발하지 않습니다.
        public static int GetSlotCompletedChallengeCount(int slot)
        {
            if (slot < 0 || slot >= SlotCount)
            {
                return 0;
            }

            string path = GetSlotPath(slot);
            if (!File.Exists(path))
            {
                return 0;
            }

            try
            {
                GameSaveData preview = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path));
                return preview?.completedChallengeIds?.Count ?? 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static bool ShouldShowBossRushUnlockNotice(int slot)
        {
            if (slot < 0 || slot >= SlotCount || !File.Exists(GetSlotPath(slot)))
            {
                return false;
            }

            try
            {
                GameSaveData preview = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(GetSlotPath(slot)));
                return preview != null
                    && preview.hasSeenEnding
                    && !preview.hasAcknowledgedBossRushUnlock;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static GameSaveConfigSO cachedConfig;
        private static bool configLoadAttempted;

        // Assets/Resources/GameSaveConfig.asset(GameSaveConfigSO)을 읽습니다. 없으면 null.
        private static GameSaveConfigSO Config
        {
            get
            {
                if (!configLoadAttempted)
                {
                    configLoadAttempted = true;
                    cachedConfig = Resources.Load<GameSaveConfigSO>(ConfigResourcePath);
                }

                return cachedConfig;
            }
        }

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

        // 처치한 보스 "종류" 수입니다(같은 보스를 여러 번 잡아도 1로 집계).
        public static int ClearedBossCount => Data.clearedBossIds.Count;

        public static void UnlockDefaultBoss()
        {
            IReadOnlyList<BossData> bosses = Config != null ? Config.DefaultUnlockedBosses : null;
            if (bosses == null || bosses.Count == 0)
            {
                UnlockBoss(FallbackFirstBossId);
                return;
            }

            for (int i = 0; i < bosses.Count; i++)
            {
                if (bosses[i] != null)
                {
                    UnlockBoss(bosses[i].Id);
                }
            }
        }

        public static void UnlockDefaultWeapons()
        {
            IReadOnlyList<BaseWeaponSO> weapons = Config != null ? Config.DefaultUnlockedWeapons : null;
            if (weapons == null)
            {
                return;
            }

            for (int i = 0; i < weapons.Count; i++)
            {
                BaseWeaponSO weapon = weapons[i];
                if (weapon == null)
                {
                    continue;
                }

                UnlockWeapon(weapon.WeaponId);
                PurchaseWeapon(weapon.WeaponId, 0);
            }
        }

        public static void UnlockDefaultSkills()
        {
            IReadOnlyList<BaseSkillSO> skills = Config != null ? Config.DefaultUnlockedSkills : null;
            if (skills == null)
            {
                return;
            }

            for (int i = 0; i < skills.Count; i++)
            {
                BaseSkillSO skill = skills[i];
                if (skill == null)
                {
                    continue;
                }

                UnlockSkill(skill.SkillId);
                PurchaseSkill(skill.SkillId, 0);
            }
        }

        public static void UnlockDefaultPassiveSkills()
        {
            IReadOnlyList<BasePassiveSkillSO> skills = Config != null ? Config.DefaultUnlockedPassiveSkills : null;
            if (skills == null)
            {
                return;
            }

            for (int i = 0; i < skills.Count; i++)
            {
                BasePassiveSkillSO skill = skills[i];
                if (skill == null)
                {
                    continue;
                }

                UnlockPassiveSkill(skill.SkillId);
                PurchasePassiveSkill(skill.SkillId, 0);
            }
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

        public static bool HasBestClearTime(string bossId)
        {
            return FindBossClearTimeEntry(bossId) != null;
        }

        public static float GetBestClearTime(string bossId)
        {
            BossClearTimeEntry entry = FindBossClearTimeEntry(bossId);
            return entry != null ? entry.seconds : -1f;
        }

        // 기록이 없거나 기존 값보다 짧을 때만 갱신합니다. 갱신했다면 true를 반환합니다.
        public static bool TrySetBestClearTime(string bossId, float seconds)
        {
            if (string.IsNullOrEmpty(bossId))
            {
                return false;
            }

            BossClearTimeEntry entry = FindBossClearTimeEntry(bossId);
            if (entry == null)
            {
                Data.bossClearTimes.Add(new BossClearTimeEntry { bossId = bossId, seconds = seconds });
                Save();
                return true;
            }

            if (seconds < entry.seconds)
            {
                entry.seconds = seconds;
                Save();
                return true;
            }

            return false;
        }

        public static bool HasBossRushBestTime => Data.hasBossRushBestTime;

        public static float BossRushBestTime => Data.hasBossRushBestTime
            ? Data.bossRushBestTime
            : -1f;

        public static bool TrySetBestBossRushTime(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            {
                return false;
            }

            if (Data.hasBossRushBestTime && seconds >= Data.bossRushBestTime)
            {
                return false;
            }

            Data.hasBossRushBestTime = true;
            Data.bossRushBestTime = seconds;
            Save();
            return true;
        }

        public static bool HasBossRushBossBestTime(string bossId)
        {
            return FindBossRushBossClearTimeEntry(bossId) != null;
        }

        public static float GetBossRushBossBestTime(string bossId)
        {
            BossClearTimeEntry entry = FindBossRushBossClearTimeEntry(bossId);
            return entry != null ? entry.seconds : -1f;
        }

        public static void TrySetBestBossRushBossTimes(
            IReadOnlyList<string> bossIds,
            IReadOnlyList<float> seconds)
        {
            if (bossIds == null || seconds == null || bossIds.Count != seconds.Count)
            {
                return;
            }

            bool changed = false;
            for (int i = 0; i < bossIds.Count; i++)
            {
                string bossId = bossIds[i];
                float clearSeconds = seconds[i];
                if (string.IsNullOrWhiteSpace(bossId)
                    || float.IsNaN(clearSeconds)
                    || float.IsInfinity(clearSeconds)
                    || clearSeconds < 0f)
                {
                    continue;
                }

                BossClearTimeEntry entry = FindBossRushBossClearTimeEntry(bossId);
                if (entry == null)
                {
                    Data.bossRushBossClearTimes.Add(new BossClearTimeEntry
                    {
                        bossId = bossId,
                        seconds = clearSeconds
                    });
                    changed = true;
                }
                else if (clearSeconds < entry.seconds)
                {
                    entry.seconds = clearSeconds;
                    changed = true;
                }
            }

            if (changed)
            {
                Save();
            }
        }

        private static BossClearTimeEntry FindBossRushBossClearTimeEntry(string bossId)
        {
            if (string.IsNullOrWhiteSpace(bossId))
            {
                return null;
            }

            List<BossClearTimeEntry> entries = Data.bossRushBossClearTimes;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].bossId == bossId)
                {
                    return entries[i];
                }
            }

            return null;
        }

        // 테스트/디버그용: 특정 보스의 최고 클리어 기록만 지웁니다.
        public static void ResetBossClearTime(string bossId)
        {
            BossClearTimeEntry entry = FindBossClearTimeEntry(bossId);
            if (entry == null)
            {
                return;
            }

            Data.bossClearTimes.Remove(entry);
            Save();
        }

        // 테스트/디버그용: 모든 보스의 최고 클리어 기록을 지웁니다.
        public static void ResetAllBossClearTimes()
        {
            if (Data.bossClearTimes.Count == 0)
            {
                return;
            }

            Data.bossClearTimes.Clear();
            Save();
        }

        private static BossClearTimeEntry FindBossClearTimeEntry(string bossId)
        {
            if (string.IsNullOrEmpty(bossId))
            {
                return null;
            }

            List<BossClearTimeEntry> entries = Data.bossClearTimes;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].bossId == bossId)
                {
                    return entries[i];
                }
            }

            return null;
        }

        public static bool HasSeenPrologue => Data.hasSeenPrologue || Data.hasSeenSynopsis;
        public static bool HasSeenPast => Data.hasSeenPast;
        public static bool HasCompletedTutorial => Data.hasCompletedTutorial;
        public static bool HasSeenEnding => Data.hasSeenEnding;
        public static bool HasSeenEpilogue => HasSeenEnding;
        public static bool HasSeenSynopsis => HasSeenPrologue;

        public static void AcknowledgeBossRushUnlock()
        {
            if (!Data.hasSeenEnding || Data.hasAcknowledgedBossRushUnlock)
            {
                return;
            }

            Data.hasAcknowledgedBossRushUnlock = true;
            Save();
        }

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
            if (!Data.hasCompletedTutorial)
            {
                Data.hasCompletedTutorial = true;
                Save();
            }

            UnlockDefaultBoss();
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

        public static void MarkEpilogueSeen()
        {
            MarkEndingSeen();
        }

        public static void SetEndingSeen(bool seen)
        {
            if (Data.hasSeenEnding == seen)
            {
                return;
            }

            Data.hasSeenEnding = seen;
            if (!seen)
            {
                Data.hasAcknowledgedBossRushUnlock = false;
            }
            Save();
        }

        public static void SetEpilogueSeen(bool seen)
        {
            SetEndingSeen(seen);
        }

        public static void ResetStoryProgress()
        {
            bool changed = Data.hasSeenPrologue
                || Data.hasSeenPast
                || Data.hasSeenSynopsis
                || Data.hasCompletedTutorial
                || Data.hasSeenEnding
                || Data.hasAcknowledgedBossRushUnlock
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
            Data.hasAcknowledgedBossRushUnlock = false;
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

        public static void ResetEpilogueSeen()
        {
            ResetEndingSeen();
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

        // 해당 슬롯에 대한 저장 기록이 존재하는지(장착이든 명시적 해제든 한 번이라도 SetEquippedSkillId가 호출됐는지) 반환합니다.
        // GetEquippedSkillId는 "한 번도 기록된 적 없음"과 "명시적으로 해제됨(null 저장)"을 둘 다 null로 반환해 구분할 수 없기 때문에 별도로 둡니다.
        public static bool HasEquippedSkillEntry(int slot)
        {
            List<SkillSlotData> equippedSkills = Data.equippedSkills;
            for (int i = 0; i < equippedSkills.Count; i++)
            {
                if (equippedSkills[i].slot == slot)
                {
                    return true;
                }
            }

            return false;
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

        // 테스트/디버그용: 장착된 액티브 스킬 슬롯 기록을 전부 지웁니다(장착 안 한 최초 상태로 되돌림).
        public static void ResetEquippedSkills()
        {
            if (Data.equippedSkills.Count == 0)
            {
                return;
            }

            Data.equippedSkills.Clear();
            Save();
        }

        public static IReadOnlyList<string> UnlockedPassiveSkillIds => Data.unlockedPassiveSkillIds;

        public static bool IsPassiveSkillUnlocked(string skillId)
        {
            return !string.IsNullOrEmpty(skillId) && Data.unlockedPassiveSkillIds.Contains(skillId);
        }

        public static void UnlockPassiveSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId) || Data.unlockedPassiveSkillIds.Contains(skillId))
            {
                return;
            }

            Data.unlockedPassiveSkillIds.Add(skillId);
            Save();
        }

        public static void LockPassiveSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId) || !Data.unlockedPassiveSkillIds.Remove(skillId))
            {
                return;
            }

            Save();
        }

        public static string GetEquippedPassiveSkillId(int slot)
        {
            List<SkillSlotData> equippedPassiveSkills = Data.equippedPassiveSkills;
            for (int i = 0; i < equippedPassiveSkills.Count; i++)
            {
                if (equippedPassiveSkills[i].slot == slot)
                {
                    return equippedPassiveSkills[i].skillId;
                }
            }

            return null;
        }

        // HasEquippedSkillEntry와 동일한 이유로 존재합니다: "기록 없음"과 "명시적으로 해제됨(null 저장)"을 구분합니다.
        public static bool HasEquippedPassiveSkillEntry(int slot)
        {
            List<SkillSlotData> equippedPassiveSkills = Data.equippedPassiveSkills;
            for (int i = 0; i < equippedPassiveSkills.Count; i++)
            {
                if (equippedPassiveSkills[i].slot == slot)
                {
                    return true;
                }
            }

            return false;
        }

        public static void SetEquippedPassiveSkillId(int slot, string skillId)
        {
            List<SkillSlotData> equippedPassiveSkills = Data.equippedPassiveSkills;
            for (int i = 0; i < equippedPassiveSkills.Count; i++)
            {
                if (equippedPassiveSkills[i].slot == slot)
                {
                    equippedPassiveSkills[i].skillId = skillId;
                    Save();
                    return;
                }
            }

            equippedPassiveSkills.Add(new SkillSlotData { slot = slot, skillId = skillId });
            Save();
        }

        // 테스트/디버그용: 장착된 패시브 스킬 슬롯 기록을 전부 지웁니다(장착 안 한 최초 상태로 되돌림).
        public static void ResetEquippedPassiveSkills()
        {
            if (Data.equippedPassiveSkills.Count == 0)
            {
                return;
            }

            Data.equippedPassiveSkills.Clear();
            Save();
        }

        public static bool IsSkillPurchased(string skillId)
        {
            return !string.IsNullOrEmpty(skillId) && Data.purchasedSkillIds.Contains(skillId);
        }

        public static bool PurchaseSkill(string skillId, int price)
        {
            if (string.IsNullOrEmpty(skillId) || Data.purchasedSkillIds.Contains(skillId)
                || !IsSkillUnlocked(skillId) || Data.challengePoints < price)
            {
                return false;
            }

            SetChallengePoints(Data.challengePoints - price);
            Data.purchasedSkillIds.Add(skillId);
            Save();
            ItemPurchased?.Invoke();
            return true;
        }

        public static bool RefundSkill(string skillId, int price)
        {
            if (string.IsNullOrEmpty(skillId) || !Data.purchasedSkillIds.Remove(skillId))
            {
                return false;
            }

            SetChallengePoints(Data.challengePoints + price);
            Save();
            return true;
        }

        // 테스트/디버그용: 포인트 환불 없이 액티브 스킬 구매 기록만 전부 지웁니다.
        public static void ResetPurchasedSkills()
        {
            if (Data.purchasedSkillIds.Count == 0)
            {
                return;
            }

            Data.purchasedSkillIds.Clear();
            Save();
        }

        public static bool IsPassiveSkillPurchased(string skillId)
        {
            return !string.IsNullOrEmpty(skillId) && Data.purchasedPassiveSkillIds.Contains(skillId);
        }

        public static bool PurchasePassiveSkill(string skillId, int price)
        {
            if (string.IsNullOrEmpty(skillId) || Data.purchasedPassiveSkillIds.Contains(skillId)
                || !IsPassiveSkillUnlocked(skillId) || Data.challengePoints < price)
            {
                return false;
            }

            SetChallengePoints(Data.challengePoints - price);
            Data.purchasedPassiveSkillIds.Add(skillId);
            Save();
            ItemPurchased?.Invoke();
            return true;
        }

        public static bool RefundPassiveSkill(string skillId, int price)
        {
            if (string.IsNullOrEmpty(skillId) || !Data.purchasedPassiveSkillIds.Remove(skillId))
            {
                return false;
            }

            SetChallengePoints(Data.challengePoints + price);
            Save();
            return true;
        }

        // 테스트/디버그용: 포인트 환불 없이 패시브 스킬 구매 기록만 전부 지웁니다.
        public static void ResetPurchasedPassiveSkills()
        {
            if (Data.purchasedPassiveSkillIds.Count == 0)
            {
                return;
            }

            Data.purchasedPassiveSkillIds.Clear();
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

        // 테스트/디버그용: 장착 총기 기록을 지웁니다(장착 안 한 최초 상태로 되돌림).
        public static void ResetEquippedWeapon()
        {
            if (Data.equippedWeaponId == null)
            {
                return;
            }

            Data.equippedWeaponId = null;
            Save();
        }

        public static bool IsWeaponPurchased(string weaponId)
        {
            return !string.IsNullOrEmpty(weaponId) && Data.purchasedWeaponIds.Contains(weaponId);
        }

        public static bool PurchaseWeapon(string weaponId, int price)
        {
            if (string.IsNullOrEmpty(weaponId) || Data.purchasedWeaponIds.Contains(weaponId)
                || !IsWeaponUnlocked(weaponId) || Data.challengePoints < price)
            {
                return false;
            }

            SetChallengePoints(Data.challengePoints - price);
            Data.purchasedWeaponIds.Add(weaponId);
            Save();
            ItemPurchased?.Invoke();
            return true;
        }

        public static bool RefundWeapon(string weaponId, int price)
        {
            if (string.IsNullOrEmpty(weaponId) || !Data.purchasedWeaponIds.Remove(weaponId))
            {
                return false;
            }

            SetChallengePoints(Data.challengePoints + price);
            Save();
            return true;
        }

        // 테스트/디버그용: 포인트 환불 없이 총기 구매 기록만 전부 지웁니다.
        public static void ResetPurchasedWeapons()
        {
            if (Data.purchasedWeaponIds.Count == 0)
            {
                return;
            }

            Data.purchasedWeaponIds.Clear();
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
            SetChallengePoints(Data.challengePoints + rewardPoint);
            Save();
        }

        // 테스트/디버그용: 포인트 회수 없이 특정 챌린지 하나의 완료 기록과 누적 카운터를 지웁니다.
        public static void ResetChallenge(string challengeId)
        {
            if (string.IsNullOrEmpty(challengeId))
            {
                return;
            }

            bool changed = Data.completedChallengeIds.Remove(challengeId);
            List<ChallengeCounterEntry> counters = Data.challengeCounters;
            for (int i = 0; i < counters.Count; i++)
            {
                if (counters[i].challengeId == challengeId)
                {
                    counters.RemoveAt(i);
                    changed = true;
                    break;
                }
            }

            if (changed)
            {
                Save();
            }
        }

        // 테스트/디버그용: 모든 챌린지 완료 기록을 지웁니다(포인트는 회수하지 않음).
        public static void ResetCompletedChallenges()
        {
            if (Data.completedChallengeIds.Count == 0)
            {
                return;
            }

            Data.completedChallengeIds.Clear();
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

        // IncrementChallengeCounter와 달리 한 번에 임의의 양(예: 이번 판에 패링한 횟수)을 더합니다.
        public static int AddToChallengeCounter(string challengeId, int amount)
        {
            if (string.IsNullOrEmpty(challengeId) || amount == 0)
            {
                return GetChallengeCounter(challengeId);
            }

            List<ChallengeCounterEntry> counters = Data.challengeCounters;
            for (int i = 0; i < counters.Count; i++)
            {
                if (counters[i].challengeId == challengeId)
                {
                    counters[i].count += amount;
                    Save();
                    return counters[i].count;
                }
            }

            counters.Add(new ChallengeCounterEntry { challengeId = challengeId, count = amount });
            Save();
            return amount;
        }

        // 테스트/디버그용: 모든 챌린지 누적 카운터를 지웁니다.
        public static void ResetChallengeCounters()
        {
            if (Data.challengeCounters.Count == 0)
            {
                return;
            }

            Data.challengeCounters.Clear();
            Save();
        }

        public static int ChallengePoints => Data.challengePoints;

        public static event Action<int> ChallengePointsChanged;
        // 총기/액티브/패시브 스킬을 챌린지 포인트로 구매(해금)하는 데 성공했을 때 발생합니다. 어떤 종류인지는 넘기지 않습니다.
        public static event Action ItemPurchased;

        private static void SetChallengePoints(int value)
        {
            Data.challengePoints = value;
            ChallengePointsChanged?.Invoke(value);
        }

        // 테스트/디버그용: 챌린지 완료 없이 포인트만 지급합니다.
        public static void AddDebugChallengePoints(int amount)
        {
            SetChallengePoints(Data.challengePoints + amount);
            Save();
        }

        // 테스트/디버그용: 포인트를 정확히 이 값으로 맞춥니다(음수는 0으로 고정).
        public static void SetDebugChallengePoints(int amount)
        {
            SetChallengePoints(Mathf.Max(0, amount));
            Save();
        }

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
            UnlockDefaultBoss();
            UnlockDefaultWeapons();
            UnlockDefaultSkills();
            UnlockDefaultPassiveSkills();
        }

        // 테스트/디버그용: 세이브 데이터를 전부 지우고 기본 해금 상태(기본 보스/무기/스킬/패시브)로 되돌립니다.
        public static void ResetEverything()
        {
            data = new GameSaveData();
            NormalizeLoadedData();
            UnlockDefaultBoss();
            UnlockDefaultWeapons();
            UnlockDefaultSkills();
            UnlockDefaultPassiveSkills();
            Save();
        }

        private static void NormalizeLoadedData()
        {
            data ??= new GameSaveData();
            data.unlockedBossIds ??= new List<string>();
            data.clearedBossIds ??= new List<string>();
            data.bossClearTimes ??= new List<BossClearTimeEntry>();
            data.bossRushBossClearTimes ??= new List<BossClearTimeEntry>();
            data.unlockedSkillIds ??= new List<string>();
            data.unlockedPassiveSkillIds ??= new List<string>();
            data.unlockedWeaponIds ??= new List<string>();
            data.equippedSkills ??= new List<SkillSlotData>();
            data.equippedPassiveSkills ??= new List<SkillSlotData>();
            data.seenStoryEpisodeIds ??= new List<string>();
            data.completedChallengeIds ??= new List<string>();
            data.challengeCounters ??= new List<ChallengeCounterEntry>();
            data.purchasedSkillIds ??= new List<string>();
            data.purchasedPassiveSkillIds ??= new List<string>();
            data.purchasedWeaponIds ??= new List<string>();
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
            string savePath = SavePath;
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));

            string tempPath = savePath + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(Data, true));

            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }

            File.Move(tempPath, savePath);
        }
    }
}
