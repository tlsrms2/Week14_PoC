using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using Week14.Save;

namespace Week14.UI
{
    // 앱 실행 직후 뜨는 언어 설정 패널입니다. SettingsManager.LanguageSetupCompleted가 아직 false인
    // 최초 실행에서만 표시되고, 드롭다운에서 언어를 고르면 즉시 적용/저장되며, 확인 버튼을 누르면
    // 이 플래그를 true로 저장하고 패널이 닫히며 titleFadeInPanel의 페이드아웃을 시작시킵니다.
    // (해상도 등 다른 설정값이 부팅 중에 먼저 저장돼도 이 플래그와는 무관하므로 오탐하지 않습니다.)
    // 이미 완료 처리된 재실행에서는 패널을 띄우지 않고 곧바로 페이드아웃을 시작시킵니다.
    // 하이어라키(Canvas/드롭다운/버튼 배치)는 에디터에서 직접 구성한 뒤 아래 필드들을 인스펙터에서 연결해서 씁니다.
    public sealed class LanguageSettingPanelView : MonoBehaviour
    {
        [Tooltip("표시/숨김 대상이 되는 루트입니다. 비워두면 이 오브젝트를 사용합니다.")]
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Dropdown languageDropdown;
        [Tooltip("languageDropdown의 Options 순서와 반드시 1:1로 일치해야 합니다.")]
        [SerializeField] private string[] languageLocaleCodes = { "ko-KR", "en" };
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmButtonText;

        [Tooltip("확인(또는 스킵) 시 페이드아웃을 시작시킬 타이틀 화면 패널입니다.")]
        [SerializeField] private TitleFadeInPanel titleFadeInPanel;

        [Header("기본 문구 (로컬라이징 문구가 비어있을 때 씀)")]
        [SerializeField] private string titleLabel = "언어설정";
        [SerializeField] private string confirmButtonLabel = "확인";

        [Header("로컬라이징 (비워두면 위 기본 문구를 그대로 씀)")]
        [SerializeField] private LocalizedString localizedTitleLabel;
        [SerializeField] private LocalizedString localizedConfirmButtonLabel;

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(HandleConfirmClicked);
            }

            BindLabel(localizedTitleLabel, titleLabel, SetTitleText);
            BindLabel(localizedConfirmButtonLabel, confirmButtonLabel, SetConfirmButtonText);

            if (SettingsManager.LanguageSetupCompleted)
            {
                root.SetActive(false);
                BeginTitleFadeOut();
                return;
            }

            if (languageDropdown != null)
            {
                languageDropdown.onValueChanged.AddListener(HandleLanguageSelected);
                StartCoroutine(SyncDropdownToCurrentLocaleRoutine());
            }

            root.SetActive(true);
        }

        private void OnDestroy()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(HandleConfirmClicked);
            }

            if (languageDropdown != null)
            {
                languageDropdown.onValueChanged.RemoveListener(HandleLanguageSelected);
            }

            UnbindLabel(localizedTitleLabel, SetTitleText);
            UnbindLabel(localizedConfirmButtonLabel, SetConfirmButtonText);
        }

        private static void BindLabel(LocalizedString localizedString, string fallback, LocalizedString.ChangeHandler handler)
        {
            bool hasLocalizedString = LoadoutSelectedSkillPanelLocalization.HasLocalizedString(localizedString);
            LoadoutSelectedSkillPanelLocalization.BindLocalizedString(localizedString, hasLocalizedString, handler);
            if (!hasLocalizedString)
            {
                handler(fallback);
            }
        }

        private static void UnbindLabel(LocalizedString localizedString, LocalizedString.ChangeHandler handler)
        {
            LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(
                localizedString,
                LoadoutSelectedSkillPanelLocalization.HasLocalizedString(localizedString),
                handler);
        }

        private void SetTitleText(string value)
        {
            if (titleText != null)
            {
                titleText.text = value;
            }
        }

        private void SetConfirmButtonText(string value)
        {
            if (confirmButtonText != null)
            {
                confirmButtonText.text = value;
            }
        }

        private void HandleLanguageSelected(int dropdownIndex)
        {
            if (dropdownIndex < 0 || dropdownIndex >= languageLocaleCodes.Length)
            {
                return;
            }

            StartCoroutine(ApplyLanguageRoutine(languageLocaleCodes[dropdownIndex]));
        }

        private void HandleConfirmClicked()
        {
            SettingsManager.MarkLanguageSetupCompleted();
            root.SetActive(false);
            BeginTitleFadeOut();
        }

        private void BeginTitleFadeOut()
        {
            if (titleFadeInPanel != null)
            {
                titleFadeInPanel.BeginFadeOut();
            }
        }

        private IEnumerator ApplyLanguageRoutine(string localeCode)
        {
            yield return LocalizationSettings.InitializationOperation;

            Locale locale = FindLocale(localeCode);
            if (locale == null)
            {
                Debug.LogWarning($"{nameof(LanguageSettingPanelView)}: Locale '{localeCode}' not found.", this);
                yield break;
            }

            LocalizationSettings.SelectedLocale = locale;
            SettingsManager.SetLanguageCode(localeCode);
        }

        private IEnumerator SyncDropdownToCurrentLocaleRoutine()
        {
            yield return LocalizationSettings.InitializationOperation;

            string localeCode = LocalizationSettings.SelectedLocale != null
                ? LocalizationSettings.SelectedLocale.Identifier.Code
                : SettingsManager.LanguageCode;

            int index = FindLanguageIndex(localeCode);
            if (index >= 0 && languageDropdown != null)
            {
                languageDropdown.SetValueWithoutNotify(index);
                languageDropdown.RefreshShownValue();
            }
        }

        private static Locale FindLocale(string localeCode)
        {
            var locales = LocalizationSettings.AvailableLocales.Locales;
            for (int i = 0; i < locales.Count; i++)
            {
                if (locales[i].Identifier.Code == localeCode)
                {
                    return locales[i];
                }
            }

            return null;
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
    }
}
