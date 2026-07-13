using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Week14.UI
{
    public sealed class ChallengeRewardPopupView : MonoBehaviour
    {
        [Tooltip("페이드 인/아웃 대상이 되는 표시 루트입니다. 비워두면 이 오브젝트를 사용합니다.")]
        [SerializeField] private GameObject root;
        [Tooltip("페이드 인/아웃에 사용할 CanvasGroup입니다. 비워두면 root에서 찾거나 추가합니다.")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("획득 포인트를 표시할 TMP 텍스트입니다.")]
        [SerializeField] private TMP_Text pointsText;
        [Tooltip("포인트 텍스트 포맷입니다. {0}에 획득 포인트가 들어갑니다.")]
        [SerializeField] private string format = "+{0}P";
        [Tooltip("포인트 텍스트의 로컬라이징 포맷 문자열입니다. {0}에 획득 포인트가 들어갑니다. 비워두면 format 필드를 그대로 사용합니다.")]
        [SerializeField] private LocalizedString localizedFormat;
        [Tooltip("페이드 인에 걸리는 시간(초, 언스케일드)입니다.")]
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.25f;
        [Tooltip("페이드 인 직후, 닫기 버튼 클릭을 받기 시작하기 전까지 최소로 유지되는 시간(초, 언스케일드)입니다.")]
        [SerializeField, Min(0f)] private float holdSeconds = 1.2f;
        [Tooltip("페이드 아웃에 걸리는 시간(초, 언스케일드)입니다.")]
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.35f;

        [Header("닫기 입력")]
        [Tooltip("누르면(holdSeconds가 지난 뒤부터) 팝업이 페이드아웃되며 닫히는 버튼입니다.")]
        [SerializeField] private Button closeButton;
        [Tooltip("닫기 버튼을 안내할 TMP 텍스트입니다. 비워두면 표시하지 않습니다.")]
        [SerializeField] private TMP_Text closeHintText;
        [Tooltip("닫기 안내 문구의 로컬라이징 문자열입니다. 비워두면 closeHintText에 에디터에서 지정해둔 텍스트를 그대로 사용합니다.")]
        [SerializeField] private LocalizedString localizedCloseHintText;

        private Coroutine playRoutine;
        private bool closeRequested;

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }

            if (canvasGroup == null)
            {
                canvasGroup = root.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = root.AddComponent<CanvasGroup>();
                }
            }

            LoadoutSelectedSkillPanelLocalization.BindLocalizedString(
                localizedCloseHintText,
                LoadoutSelectedSkillPanelLocalization.HasLocalizedString(localizedCloseHintText),
                SetCloseHintText);

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HandleCloseButtonClicked);
            }

            Hide();
        }

        private void OnDestroy()
        {
            LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(
                localizedCloseHintText,
                LoadoutSelectedSkillPanelLocalization.HasLocalizedString(localizedCloseHintText),
                SetCloseHintText);

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseButtonClicked);
            }
        }

        private void HandleCloseButtonClicked()
        {
            closeRequested = true;
        }

        private void SetCloseHintText(string value)
        {
            if (closeHintText != null)
            {
                closeHintText.text = value;
            }
        }

        // 이번 전투에서 새로 획득한 포인트를 보여줍니다. 0 이하면 표시하지 않습니다.
        public void Show(int earnedPoints)
        {
            if (earnedPoints <= 0)
            {
                return;
            }

            if (pointsText != null)
            {
                pointsText.text = LoadoutSelectedSkillPanelLocalization.ResolveLocalizedFormat(localizedFormat, format, earnedPoints);
            }

            StopPlayRoutine();
            closeRequested = false;
            root.SetActive(true);
            playRoutine = StartCoroutine(PlayRoutine());
        }

        public void Hide()
        {
            StopPlayRoutine();
            root.SetActive(false);
            canvasGroup.alpha = 0f;
        }

        private IEnumerator PlayRoutine()
        {
            yield return Fade(0f, 1f, fadeInSeconds);
            yield return new WaitForSecondsRealtime(holdSeconds);
            yield return WaitForCloseButton();
            yield return Fade(1f, 0f, fadeOutSeconds);

            root.SetActive(false);
            playRoutine = null;
        }

        private IEnumerator WaitForCloseButton()
        {
            while (!closeRequested)
            {
                yield return null;
            }
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            if (seconds <= 0f)
            {
                canvasGroup.alpha = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }

            canvasGroup.alpha = to;
        }

        private void StopPlayRoutine()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }
        }
    }
}
