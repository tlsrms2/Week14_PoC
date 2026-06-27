using System;

namespace Week14.Save
{
    [Serializable]
    public sealed class SettingsData
    {
        public float bgmVolume = 0.7f;
        public float sfxVolume = 0.7f;
        public bool bgmMuted;
        public bool sfxMuted;
    }
}
