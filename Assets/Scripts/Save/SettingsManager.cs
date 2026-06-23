using System;
using System.IO;
using UnityEngine;

namespace Week14.Save
{
    public static class SettingsManager
    {
        private const string SaveFileName = "settings.json";

        private static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        private static SettingsData data;

        private static SettingsData Data
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

        public static float BgmVolume => Data.bgmVolume;
        public static float SfxVolume => Data.sfxVolume;
        public static bool BgmMuted => Data.bgmMuted;
        public static bool SfxMuted => Data.sfxMuted;

        public static void SetBgmVolume(float volume)
        {
            Data.bgmVolume = volume;
            Save();
        }

        public static void SetSfxVolume(float volume)
        {
            Data.sfxVolume = volume;
            Save();
        }

        public static void SetBgmMuted(bool muted)
        {
            Data.bgmMuted = muted;
            Save();
        }

        public static void SetSfxMuted(bool muted)
        {
            Data.sfxMuted = muted;
            Save();
        }

        public static void Load()
        {
            try
            {
                data = File.Exists(SavePath) ? JsonUtility.FromJson<SettingsData>(File.ReadAllText(SavePath)) : new SettingsData();
            }
            catch (Exception)
            {
                data = new SettingsData();
            }
        }

        public static void Save()
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(Data));
        }
    }
}
