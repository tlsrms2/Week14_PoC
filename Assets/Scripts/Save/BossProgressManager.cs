using System;
using System.IO;
using UnityEngine;

namespace Week14.Save
{
    public static class BossProgressManager
    {
        private const string SaveFileName = "boss_progress.json";
        private const string FirstBossId = "1";

        private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

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
            try
            {
                data = File.Exists(SavePath) ? JsonUtility.FromJson<BossSaveData>(File.ReadAllText(SavePath)) : new BossSaveData();
            }
            catch (Exception)
            {
                data = new BossSaveData();
            }

            UnlockBoss(FirstBossId);
        }

        public static void Save()
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(Data));
        }
    }
}
