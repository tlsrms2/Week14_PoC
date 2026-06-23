using UnityEngine;

namespace Week14.Save
{
    public static class BossProgressManager
    {
        private const string SaveKey = "BossProgress";
        private const string FirstBossId = "1";

        private static BossSaveData data;

        private static BossSaveData Data
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

        public static void UnlockBoss(string bossId)
        {
            if (string.IsNullOrEmpty(bossId) || Data.unlockedBossIds.Contains(bossId))
            {
                return;
            }

            Data.unlockedBossIds.Add(bossId);
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

        public static void Load()
        {
            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            data = string.IsNullOrEmpty(json) ? new BossSaveData() : JsonUtility.FromJson<BossSaveData>(json);
            UnlockBoss(FirstBossId);
        }

        public static void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
        }
    }
}
