using System;

namespace Week14.Save
{
    [Serializable]
    public sealed class SettingsData
    {
        public float bgmVolume = 1f;
        public float sfxVolume = 1f;
        public bool bgmMuted;
        public bool sfxMuted;
    }
}
