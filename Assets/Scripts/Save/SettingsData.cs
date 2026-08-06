using System;

namespace Week14.Save
{
    [Serializable]
    public sealed class SettingsData
    {
        public float bgmVolume = 0.7f;
        public float sfxVolume = 0.7f;
        public float mouseSensitivity = 1f;
        public bool bgmMuted;
        public bool sfxMuted;
        public string languageCode = "en";
        public int resolutionWidth;
        public int resolutionHeight;
        public int fullScreenMode = -1;
        public bool languageSetupCompleted;
    }
}
