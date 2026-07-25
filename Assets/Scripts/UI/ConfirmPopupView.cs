using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Week14.UI
{
    // 범용 예/아니오 확인 팝업입니다. Show(message, onConfirmed)로 띄우고,
    // 확인 버튼을 눌러야만 콜백이 실행됩니다(취소/뒤로가기는 그냥 닫힘).
    public sealed class ConfirmPopupView : MonoBehaviour, IBackClosable
    {
        [Tooltip("표시/숨김 대상이 되는 루트입니다. 비워두면 이 오브젝트를 사용합니다.")]
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmButtonText;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text cancelButtonText;

        [Header("버튼 로컬라이징 (비워두면 아래 기본 문구를 그대로 씀)")]
        [Tooltip("로컬라이징 문구가 비어있을 때 쓸 기본 문구입니다.")]
        [SerializeField] private string confirmButtonLabel = "확인";
        [SerializeField] private string cancelButtonLabel = "취소";
        [SerializeField] private LocalizedString localizedConfirmButtonLabel;
        [SerializeField] private LocalizedString localizedCancelButtonLabel;

        private Action onConfirm;

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

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(Hide);
            }

            bool hasConfirmLabel = LoadoutSelectedSkillPanelLocalization.HasLocalizedString(localizedConfirmButtonLabel);
            LoadoutSelectedSkillPanelLocalization.BindLocalizedString(localizedConfirmButtonLabel, hasConfirmLabel, SetConfirmButtonText);
            if (!hasConfirmLabel)
            {
                SetConfirmButtonText(confirmButtonLabel);
            }

            bool hasCancelLabel = LoadoutSelectedSkillPanelLocalization.HasLocalizedString(localizedCancelButtonLabel);
            LoadoutSelectedSkillPanelLocalization.BindLocalizedString(localizedCancelButtonLabel, hasCancelLabel, SetCancelButtonText);
            if (!hasCancelLabel)
            {
                SetCancelButtonText(cancelButtonLabel);
            }
        }

        private void OnDestroy()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(HandleConfirmClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(Hide);
            }

            LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(
                localizedConfirmButtonLabel,
                LoadoutSelectedSkillPanelLocalization.HasLocalizedString(localizedConfirmButtonLabel),
                SetConfirmButtonText);
            LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(
                localizedCancelButtonLabel,
                LoadoutSelectedSkillPanelLocalization.HasLocalizedString(localizedCancelButtonLabel),
                SetCancelButtonText);
        }

        private void SetConfirmButtonText(string value)
        {
            if (confirmButtonText != null)
            {
                confirmButtonText.text = value;
            }
        }

        private void SetCancelButtonText(string value)
        {
            if (cancelButtonText != null)
            {
                cancelButtonText.text = value;
            }
        }

        private void OnEnable()
        {
            UIBackStack.Push(this);
        }

        private void OnDisable()
        {
            UIBackStack.Remove(this);
        }

        public bool CloseByBack()
        {
            Hide();
            return true;
        }

        public void Show(string message, Action onConfirmed)
        {
            onConfirm = onConfirmed;
            if (messageText != null)
            {
                messageText.text = message;
            }

            root.SetActive(true);
        }

        public void Hide()
        {
            onConfirm = null;
            root.SetActive(false);
        }

        private void HandleConfirmClicked()
        {
            Action callback = onConfirm;
            onConfirm = null;
            root.SetActive(false);
            callback?.Invoke();
        }
    }
}
