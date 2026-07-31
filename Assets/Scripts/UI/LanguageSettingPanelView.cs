using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using Week14.Save;

namespace Week14.UI
{
    // 앱 실행 직후 뜨는 언어 설정 패널입니다. 저장된 설정(SettingsManager)이 없는 최초 실행에서만 표시되고,
    // 드롭다운에서 언어를 고르면 즉시 적용/저장되며, 확인 버튼을 누르면 패널이 닫히고 nextPanel을 활성화합니다.
    // 이미 설정이 저장되어 있는 재실행에서는 패널을 띄우지 않고 곧바로 nextPanel을 활성화합니다.
    // 하이어라키(Canvas/드롭다운/버튼 배치)는 에디터에서 직접 구성한 뒤 아래 필드들을 인스펙터에서 연결해서 씁니다.
    //
    // 주의: 이 컴포넌트가 붙은 GameObject는 씬에서 처음부터 활성 상태여야 Awake가 씬 로드 시 바로 실행됩니다.
    // 반대로 nextPanel(예: LogConsentPanelView)은 처음엔 비활성 상태여야, 이 패널이 SetActive(true)로
    // 켜주기 전까지 자기 Awake(및 그 안의 표시 로직)가 실행되지 않습니다.
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

        [Tooltip("확인(또는 스킵) 시 활성화할 다음 패널입니다. 처음엔 비활성 상태로 씬에 배치해두세요.")]
        [SerializeField] private GameObject nextPanel;

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

            if (SettingsManager.HasSavedSettings())
            {
                root.SetActive(false);
                ActivateNextPanel();
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
            root.SetActive(false);
            ActivateNextPanel();
        }

        private void ActivateNextPanel()
        {
            if (nextPanel != null)
            {
                nextPanel.SetActive(true);
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
