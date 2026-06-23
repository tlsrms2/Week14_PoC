using UnityEngine;
using UnityEngine.UI;
using Week14.Audio;

namespace Week14.UI
{
    public sealed class OptionsPanelView : MonoBehaviour
    {
        [SerializeField] private Slider bgmVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Toggle bgmMuteToggle;
        [SerializeField] private Toggle sfxMuteToggle;

        private void OnEnable()
        {
            if (bgmVolumeSlider != null)
            {
                bgmVolumeSlider.SetValueWithoutNotify(SoundManager.BgmVolume);
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.SetValueWithoutNotify(SoundManager.SfxVolume);
            }

            if (bgmMuteToggle != null)
            {
                bgmMuteToggle.SetIsOnWithoutNotify(SoundManager.IsBgmMuted);
            }

            if (sfxMuteToggle != null)
            {
                sfxMuteToggle.SetIsOnWithoutNotify(SoundManager.IsSfxMuted);
            }
        }

        public void SetBgmVolume(float volume)
        {
            SoundManager.SetBgmVolume(volume);
        }

        public void SetSfxVolume(float volume)
        {
            SoundManager.SetSfxVolume(volume);
        }

        public void SetBgmMuted(bool muted)
        {
            SoundManager.SetBgmMuted(muted);
        }

        public void SetSfxMuted(bool muted)
        {
            SoundManager.SetSfxMuted(muted);
        }
    }
}
