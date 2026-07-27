using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using Week14.GameFlow;
using Week14.Save;

namespace Week14.UI
{
    // "게임 시작" 버튼을 누르면 뜨는 세이브 슬롯 3개 선택 패널입니다.
    // 빈 슬롯을 고르면 새 게임, 저장된 슬롯을 고르면 이어하기로 GameFlowController.StartGame()에 진입합니다.
    public sealed class SaveSlotSelectPanelView : MonoBehaviour, IBackClosable
    {
        [SerializeField] private SaveSlotView[] slots;
        [SerializeField] private ConfirmPopupView deleteConfirmPopup;
        [Tooltip("로컬라이징 문구가 비어있을 때 쓸 기본 문구입니다.")]
        [SerializeField] private string deleteConfirmMessage = "정말 이 세이브 데이터를 삭제하시겠습니까?";
        [SerializeField] private LocalizedString localizedDeleteConfirmMessage;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text closeButtonText;
        [Tooltip("로컬라이징 문구가 비어있을 때 쓸 기본 문구입니다.")]
        [SerializeField] private string closeButtonLabel = "닫기";
        [SerializeField] private LocalizedString localizedCloseButtonLabel;

        private void Awake()
        {
            foreach (SaveSlotView slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }

                slot.SelectRequested += HandleSlotSelected;
                slot.DeleteRequested += HandleDeleteRequested;
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HandleCloseButtonClicked);
            }

            bool hasCloseLabel = LoadoutSelectedSkillPanelLocalization.HasLocalizedString(localizedCloseButtonLabel);
            LoadoutSelectedSkillPanelLocalization.BindLocalizedString(localizedCloseButtonLabel, hasCloseLabel, SetCloseButtonText);
            if (!hasCloseLabel)
            {
                SetCloseButtonText(closeButtonLabel);
            }
        }

        private void OnDestroy()
        {
            foreach (SaveSlotView slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }

                slot.SelectRequested -= HandleSlotSelected;
                slot.DeleteRequested -= HandleDeleteRequested;
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseButtonClicked);
            }

            LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(
                localizedCloseButtonLabel,
                LoadoutSelectedSkillPanelLocalization.HasLocalizedString(localizedCloseButtonLabel),
                SetCloseButtonText);
        }

        private void SetCloseButtonText(string value)
        {
            if (closeButtonText != null)
            {
                closeButtonText.text = value;
            }
        }

        private void OnEnable()
        {
            UIBackStack.Push(this);
            RefreshAllSlots();
        }

        private void OnDisable()
        {
            UIBackStack.Remove(this);
        }

        public bool CloseByBack()
        {
            gameObject.SetActive(false);
            return true;
        }

        private void HandleCloseButtonClicked()
        {
            gameObject.SetActive(false);
        }

        private void RefreshAllSlots()
        {
            foreach (SaveSlotView slot in slots)
            {
                if (slot != null)
                {
                    slot.Refresh();
                }
            }
        }

        private void HandleSlotSelected(int slotIndex)
        {
            GameSaveManager.SelectSlot(slotIndex);
            gameObject.SetActive(false);
            GameFlowController.StartGame();
        }

        private void HandleDeleteRequested(int slotIndex)
        {
            if (deleteConfirmPopup == null)
            {
                return;
            }

            deleteConfirmPopup.Show(ResolveDeleteConfirmMessage(), () =>
            {
                GameSaveManager.DeleteSlot(slotIndex);
                RefreshAllSlots();
            });
        }

        // 팝업 메시지는 뜰 때마다 한 번만 계산해서 넘긴다(ChallengeRewardPopupView의 pointsText와 동일한 방식).
        // 이 팝업이 떠있는 짧은 시간 동안 언어가 바뀌는 경우는 고려하지 않는다.
        private string ResolveDeleteConfirmMessage()
        {
            return LoadoutSelectedSkillPanelLocalization.HasLocalizedString(localizedDeleteConfirmMessage)
                ? localizedDeleteConfirmMessage.GetLocalizedString()
                : deleteConfirmMessage;
        }
    }
}
