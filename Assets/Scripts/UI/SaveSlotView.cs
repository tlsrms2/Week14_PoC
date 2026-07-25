using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using Week14.Challenge;
using Week14.Save;

namespace Week14.UI
{
    // 세이브 슬롯 패널의 슬롯 카드 하나입니다. 비어있으면 "새 게임", 저장되어 있으면 "이어하기"를 표시하고
    // 삭제 버튼은 비어있을 때 숨깁니다.
    public sealed class SaveSlotView : MonoBehaviour
    {
        [SerializeField] private int slotIndex;
        [SerializeField] private Button selectButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private TMP_Text stateText;
        [Tooltip("로컬라이징 문구가 비어있을 때 쓸 기본 문구입니다.")]
        [SerializeField] private string emptyLabel = "새 게임";
        [SerializeField] private string continueLabel = "이어하기";
        [Header("로컬라이징 (비워두면 위 emptyLabel/continueLabel을 그대로 씀)")]
        [SerializeField] private LocalizedString localizedEmptyLabel;
        [SerializeField] private LocalizedString localizedContinueLabel;

        [Header("삭제 버튼")]
        [SerializeField] private TMP_Text deleteButtonText;
        [Tooltip("로컬라이징 문구가 비어있을 때 쓸 기본 문구입니다.")]
        [SerializeField] private string deleteButtonLabel = "삭제";
        [SerializeField] private LocalizedString localizedDeleteButtonLabel;

        [Header("진행도 표시 (클리어한 챌린지 / 전체 챌린지 %)")]
        [Tooltip("전체 챌린지 개수를 계산할 데이터베이스입니다. 비워두면 진행도를 표시하지 않습니다.")]
        [SerializeField] private ChallengeDatabaseSO challengeDatabase;
        [SerializeField] private TMP_Text progressText;
        [Tooltip("로컬라이징 문구가 비어있을 때 쓸 기본 포맷입니다. {0}에 퍼센트 정수가 들어갑니다.")]
        [SerializeField] private string progressFormat = "{0}%";
        [SerializeField] private LocalizedString localizedProgressFormat;

        [Header("100% 달성 시 선택 버튼 강조")]
        [Tooltip("선택 버튼의 배경 이미지입니다. 100% 달성 시 아래 색으로 바뀝니다. 비워두면 강조하지 않습니다.")]
        [SerializeField] private Image selectButtonImage;
        [Tooltip("100% 달성 시 적용할 색입니다.")]
        [SerializeField] private Color completedSelectButtonColor = new(1f, 0.85f, 0.3f);

        public event Action<int> SelectRequested;
        public event Action<int> DeleteRequested;

        // 현재 stateText에 바인딩되어 있는 로컬라이징 문구가 "이어하기" 쪽인지 추적합니다.
        // 같은 상태로 Refresh()가 반복 호출돼도 중복 구독하지 않기 위함입니다.
        private bool hasBoundLocalizedLabel;
        private bool boundLocalizedIsContinue;

        // selectButtonImage에 원래 지정돼 있던 색(=100% 아닐 때 되돌아갈 색)입니다. Awake에서 한 번만 캡처합니다.
        private Color defaultSelectButtonColor;
        private bool hasCapturedDefaultSelectButtonColor;

        private void Awake()
        {
            if (selectButtonImage != null)
            {
                defaultSelectButtonColor = selectButtonImage.color;
                hasCapturedDefaultSelectButtonColor = true;
            }

            if (selectButton != null)
            {
                selectButton.onClick.AddListener(HandleSelectClicked);
            }

            if (deleteButton != null)
            {
                deleteButton.onClick.AddListener(HandleDeleteClicked);
            }

            bool hasDeleteLabel = LoadoutSelectedSkillPanelLocalization.HasLocalizedString(localizedDeleteButtonLabel);
            LoadoutSelectedSkillPanelLocalization.BindLocalizedString(localizedDeleteButtonLabel, hasDeleteLabel, SetDeleteButtonText);
            if (!hasDeleteLabel)
            {
                SetDeleteButtonText(deleteButtonLabel);
            }
        }

        private void OnDestroy()
        {
            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(HandleSelectClicked);
            }

            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveListener(HandleDeleteClicked);
            }

            LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(
                localizedDeleteButtonLabel,
                LoadoutSelectedSkillPanelLocalization.HasLocalizedString(localizedDeleteButtonLabel),
                SetDeleteButtonText);
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnDisable()
        {
            UnbindLocalizedLabel();
        }

        // 삭제 등으로 슬롯 상태가 바뀐 뒤 외부에서 강제로 표시를 갱신할 때 씁니다.
        public void Refresh()
        {
            bool exists = GameSaveManager.DoesSlotExist(slotIndex);
            int percent = ComputeProgressPercent(exists);

            ApplyStateLabel(exists);
            ApplyProgressText(exists, percent);
            ApplySelectButtonColor(exists, percent);

            if (deleteButton != null)
            {
                deleteButton.gameObject.SetActive(exists);
            }
        }

        private int ComputeProgressPercent(bool exists)
        {
            if (!exists || challengeDatabase == null)
            {
                return 0;
            }

            int completed = GameSaveManager.GetSlotCompletedChallengeCount(slotIndex);
            int total = challengeDatabase.TotalChallengeCount;
            return total > 0 ? Mathf.RoundToInt(completed * 100f / total) : 0;
        }

        // 퍼센트는 볼 때마다 새로 계산해서 넣는다(LoadoutSelectedSkillPanelLocalization의 FormatPoints 등과 동일한 방식).
        private void ApplyProgressText(bool exists, int percent)
        {
            if (progressText == null)
            {
                return;
            }

            if (!exists || challengeDatabase == null)
            {
                progressText.text = string.Empty;
                return;
            }

            progressText.text = LoadoutSelectedSkillPanelLocalization.ResolveLocalizedFormat(localizedProgressFormat, progressFormat, percent);
        }

        private void ApplySelectButtonColor(bool exists, int percent)
        {
            if (selectButtonImage == null || !hasCapturedDefaultSelectButtonColor)
            {
                return;
            }

            selectButtonImage.color = exists && percent >= 100 ? completedSelectButtonColor : defaultSelectButtonColor;
        }

        private void ApplyStateLabel(bool exists)
        {
            if (hasBoundLocalizedLabel && boundLocalizedIsContinue == exists)
            {
                return;
            }

            UnbindLocalizedLabel();

            LocalizedString target = exists ? localizedContinueLabel : localizedEmptyLabel;
            bool hasTarget = LoadoutSelectedSkillPanelLocalization.HasLocalizedString(target);

            if (hasTarget)
            {
                boundLocalizedIsContinue = exists;
                hasBoundLocalizedLabel = true;
                LoadoutSelectedSkillPanelLocalization.BindLocalizedString(target, true, SetStateText);
                return;
            }

            SetStateText(exists ? continueLabel : emptyLabel);
        }

        private void UnbindLocalizedLabel()
        {
            if (!hasBoundLocalizedLabel)
            {
                return;
            }

            LocalizedString target = boundLocalizedIsContinue ? localizedContinueLabel : localizedEmptyLabel;
            LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(target, true, SetStateText);
            hasBoundLocalizedLabel = false;
        }

        private void SetStateText(string value)
        {
            if (stateText != null)
            {
                stateText.text = value;
            }
        }

        private void SetDeleteButtonText(string value)
        {
            if (deleteButtonText != null)
            {
                deleteButtonText.text = value;
            }
        }

        private void HandleSelectClicked()
        {
            SelectRequested?.Invoke(slotIndex);
        }

        private void HandleDeleteClicked()
        {
            DeleteRequested?.Invoke(slotIndex);
        }
    }
}
