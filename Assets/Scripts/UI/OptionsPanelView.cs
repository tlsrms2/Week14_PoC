using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using Week14.Audio;
using Week14.Save;

namespace Week14.UI
{
    public sealed class OptionsPanelView : MonoBehaviour
    {
        [SerializeField] private Slider bgmVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Toggle bgmMuteToggle;
        [SerializeField] private Toggle sfxMuteToggle;
        [SerializeField] private TMP_Dropdown languageDropdown;
        [SerializeField] private string[] languageLocaleCodes = { "ko-KR", "en" };

        private Coroutine languageRoutine;
        private Coroutine languageDropdownSyncRoutine;

        private void Awake()
        {
            languageDropdown ??= GetComponentInChildren<TMP_Dropdown>(true);
        }

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

            SyncLanguageDropdownToCurrentLocale();
        }

        private void OnDisable()
        {
            if (languageDropdownSyncRoutine != null)
            {
                StopCoroutine(languageDropdownSyncRoutine);
                languageDropdownSyncRoutine = null;
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

        public void SetLanguage(int dropdownIndex)
        {
            if (dropdownIndex < 0 || dropdownIndex >= languageLocaleCodes.Length)
            {
                return;
            }

            SetLanguage(languageLocaleCodes[dropdownIndex], true);
        }

        private void SetLanguage(string localeCode, bool save)
        {
            if (string.IsNullOrWhiteSpace(localeCode))
            {
                return;
            }

            if (languageRoutine != null)
            {
                StopCoroutine(languageRoutine);
            }

            languageRoutine = StartCoroutine(SetLanguageRoutine(localeCode, save));
        }

        private IEnumerator SetLanguageRoutine(string localeCode, bool save)
        {
            yield return LocalizationSettings.InitializationOperation;

            var locales = LocalizationSettings.AvailableLocales.Locales;
            for (int i = 0; i < locales.Count; i++)
            {
                if (locales[i].Identifier.Code != localeCode)
                {
                    continue;
                }

                LocalizationSettings.SelectedLocale = locales[i];
                SyncLanguageDropdownValue(localeCode);

                if (save)
                {
                    SettingsManager.SetLanguageCode(localeCode);
                }

                languageRoutine = null;
                yield break;
            }

            Debug.LogWarning($"{nameof(OptionsPanelView)}: Locale '{localeCode}' not found.", this);
            languageRoutine = null;
        }

        private void SyncLanguageDropdownValue(string localeCode)
        {
            if (languageDropdown == null)
            {
                return;
            }

            int languageIndex = FindLanguageIndex(localeCode);
            if (languageIndex < 0)
            {
                return;
            }

            languageDropdown.SetValueWithoutNotify(languageIndex);
            languageDropdown.RefreshShownValue();
        }

        private int FindLanguageIndex(string localeCode)
        {
            for (int i = 0; i < languageLocaleCodes.Length; i++)
            {
                if (languageLocaleCodes[i] == localeCode)
                {
                    return i;
                }
            }

            return -1;
        }

        private void SyncLanguageDropdownToCurrentLocale()
        {
            if (languageDropdownSyncRoutine != null)
            {
                StopCoroutine(languageDropdownSyncRoutine);
            }

            languageDropdownSyncRoutine = StartCoroutine(SyncLanguageDropdownToCurrentLocaleRoutine());
        }

        private IEnumerator SyncLanguageDropdownToCurrentLocaleRoutine()
        {
            yield return LocalizationSettings.InitializationOperation;

            string localeCode = LocalizationSettings.SelectedLocale != null
                ? LocalizationSettings.SelectedLocale.Identifier.Code
                : SettingsManager.LanguageCode;

            SyncLanguageDropdownValue(localeCode);
            languageDropdownSyncRoutine = null;
        }
    }
}
