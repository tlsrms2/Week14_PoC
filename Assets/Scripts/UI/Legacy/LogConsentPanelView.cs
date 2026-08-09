using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using Week14.Analytics;
using Week14.Save;

namespace Week14.UI
{
    // 로그 수집 동의 기능 자체를 뺴면서 더 이상 사용하지 않음. 참고용으로 보관.
    // 씬에서 이 컴포넌트가 붙은 오브젝트(LogConsentRoot)는 삭제됨 — LanguageSettingPanelView가
    // 대신 TitleFadeInPanel.BeginFadeOut()을 직접 호출하도록 바뀌었습니다.
    // 앱 실행 직후 뜨는 로그 수집 동의 패널입니다. 응답 파일(LogConsentManager)이 없을 때만 표시됩니다.
    // 동의를 누르면 동의를 저장하고 로그 수집을 시작하며, 거부를 누르면 거부를 저장하고 로그 수집 없이 패널만 닫힙니다.
    // 이미 응답한 적이 있는 재실행에서는 패널을 띄우지 않고, 과거에 동의했었다면 그대로 로그 수집을 시작합니다.
    // 하이어라키(Canvas/ScrollRect/Viewport/Content/Scrollbar/버튼 배치)는 에디터에서 직접 구성한 뒤
    // 아래 필드들을 인스펙터에서 연결해서 씁니다.
    public sealed class LogConsentPanelView : MonoBehaviour
    {
        [Tooltip("표시/숨김 대상이 되는 루트입니다. 비워두면 이 오브젝트를 사용합니다.")]
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [Tooltip("ScrollRect의 Content 아래에 배치된 본문 텍스트입니다.")]
        [SerializeField] private TMP_Text contentText;
        [SerializeField] private Button agreeButton;
        [SerializeField] private TMP_Text agreeButtonText;
        [SerializeField] private Button disagreeButton;
        [SerializeField] private TMP_Text disagreeButtonText;

        [Tooltip("이 패널(및 그 앞의 언어 설정 패널)이 응답 없이 넘어가거나 응답을 마치면 페이드아웃을 시작할 타이틀 화면 패널입니다.")]
        [SerializeField] private TitleFadeInPanel titleFadeInPanel;

        [Header("기본 문구 (실제 동의 문구로 교체 예정인 placeholder, 로컬라이징 문구가 비어있을 때 씀)")]
        [SerializeField] private string titleLabel = "로그 수집 동의";
        [SerializeField, TextArea(5, 20)]
        private string contentLabel =
            "이 게임은 서비스 개선을 위해 아래와 같은 로그 데이터를 수집할 수 있습니다.\n\n" +
            "- 예: 플레이 시간, 진행 상황, 오류 발생 정보 등 (placeholder, 추후 실제 문구로 교체)\n\n" +
            "수집된 데이터는 게임 개선 목적 외에는 사용되지 않습니다.";
        [SerializeField] private string agreeButtonLabel = "동의";
        [SerializeField] private string disagreeButtonLabel = "거부";

        [Header("로컬라이징 (비워두면 위 기본 문구를 그대로 씀)")]
        [SerializeField] private LocalizedString localizedTitleLabel;
        [SerializeField] private LocalizedString localizedContentLabel;
        [SerializeField] private LocalizedString localizedAgreeButtonLabel;
        [SerializeField] private LocalizedString localizedDisagreeButtonLabel;

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }

            if (agreeButton != null)
            {
                agreeButton.onClick.AddListener(HandleAgreeClicked);
            }

            if (disagreeButton != null)
            {
                disagreeButton.onClick.AddListener(HandleDisagreeClicked);
            }

            BindLabel(localizedTitleLabel, titleLabel, SetTitleText);
            BindLabel(localizedContentLabel, contentLabel, SetContentText);
            BindLabel(localizedAgreeButtonLabel, agreeButtonLabel, SetAgreeButtonText);
            BindLabel(localizedDisagreeButtonLabel, disagreeButtonLabel, SetDisagreeButtonText);

            if (LogConsentManager.HasDecided())
            {
                root.SetActive(false);
                if (LogConsentManager.HasAgreed())
                {
                    AnalyticsManager.StartDataCollectionAfterConsent();
                }

                if (titleFadeInPanel != null)
                {
                    titleFadeInPanel.BeginFadeOut();
                }

                return;
            }

            root.SetActive(true);
        }

        private void OnDestroy()
        {
            if (agreeButton != null)
            {
                agreeButton.onClick.RemoveListener(HandleAgreeClicked);
            }

            if (disagreeButton != null)
            {
                disagreeButton.onClick.RemoveListener(HandleDisagreeClicked);
            }

            UnbindLabel(localizedTitleLabel, SetTitleText);
            UnbindLabel(localizedContentLabel, SetContentText);
            UnbindLabel(localizedAgreeButtonLabel, SetAgreeButtonText);
            UnbindLabel(localizedDisagreeButtonLabel, SetDisagreeButtonText);
        }

        private void HandleAgreeClicked()
        {
            LogConsentManager.SaveDecision(true);
            root.SetActive(false);
            AnalyticsManager.StartDataCollectionAfterConsent();

            if (titleFadeInPanel != null)
            {
                titleFadeInPanel.BeginFadeOut();
            }
        }

        private void HandleDisagreeClicked()
        {
            LogConsentManager.SaveDecision(false);
            root.SetActive(false);

            if (titleFadeInPanel != null)
            {
                titleFadeInPanel.BeginFadeOut();
            }
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

        private void UnbindLabel(LocalizedString localizedString, LocalizedString.ChangeHandler handler)
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

        private void SetContentText(string value)
        {
            if (contentText != null)
            {
                contentText.text = value;
            }
        }

        private void SetAgreeButtonText(string value)
        {
            if (agreeButtonText != null)
            {
                agreeButtonText.text = value;
            }
        }

        private void SetDisagreeButtonText(string value)
        {
            if (disagreeButtonText != null)
            {
                disagreeButtonText.text = value;
            }
        }
    }
}
