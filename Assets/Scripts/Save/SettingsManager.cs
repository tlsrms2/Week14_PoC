using System;
using System.IO;
using UnityEngine;

namespace Week14.Save
{
    public static class SettingsManager
    {
        private const string SaveFileName = "settings.json";
        private const string DefaultLanguageCode = "ko-KR";

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
        public static string LanguageCode => string.IsNullOrWhiteSpace(Data.languageCode) ? DefaultLanguageCode : Data.languageCode;

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

        public static void SetLanguageCode(string languageCode)
        {
            Data.languageCode = string.IsNullOrWhiteSpace(languageCode) ? DefaultLanguageCode : languageCode;
            Save();
        }

        public static void Load()
        {
            try
            {
                data = File.Exists(SavePath) ? JsonUtility.FromJson<SettingsData>(File.ReadAllText(SavePath)) : new SettingsData();
                data ??= new SettingsData();
                if (string.IsNullOrWhiteSpace(data.languageCode))
                {
                    data.languageCode = DefaultLanguageCode;
                }
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
