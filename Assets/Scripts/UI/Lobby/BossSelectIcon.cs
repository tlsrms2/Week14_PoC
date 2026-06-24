using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Week14.Save;

namespace Week14.UI
{
    [RequireComponent(typeof(Image))]
    public sealed class BossSelectIcon : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private BossPanelController panelController;
        [SerializeField] private BossData bossData;
        [Tooltip("이 아이콘에 마우스를 올렸을 때 보여줄, 이 아이콘 전용 디테일 패널입니다.")]
        [SerializeField] private BossDetailPanel detailPanel;
        [Tooltip("누르고 있는 동안 적용할 색상입니다. 기본 이미지 색과 구분되는 색으로 설정하세요.")]
        [SerializeField] private Color selectedColor = new(1f, 0.85f, 0.3f);
        [Tooltip("이 아이콘을 누르고 있어야 하는 시간(초)입니다. 다 차면 해당 보스전이 시작됩니다.")]
        [SerializeField, Min(0.1f)] private float holdSecondsToStart = 1.5f;
        [Tooltip("누르고 있는 동안 차오르는 진행 바 이미지입니다. Image Type이 Filled여야 합니다.")]
        [SerializeField] private Image holdFillImage;

        private Image iconImage;
        private Color baseColor;
        private Coroutine holdRoutine;

        private void Awake()
        {
            iconImage = GetComponent<Image>();

            if (iconImage != null)
            {
                baseColor = iconImage.color;
            }
        }

        private void OnEnable()
        {
            RefreshLockState();
            SetSelected(false);
            SetHoldFillAmount(0f);
        }

        private void OnDisable()
        {
            CancelHold();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsUnlocked())
            {
                return;
            }

            detailPanel?.Show(bossData.BossName, bossData.Crime, bossData.Description, bossData.Icon);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            detailPanel?.Hide();
            CancelHold();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsUnlocked())
            {
                return;
            }

            CancelHold();
            SetSelected(true);
            holdRoutine = StartCoroutine(HoldRoutine());
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            CancelHold();
        }

        public void SetSelected(bool selected)
        {
            if (iconImage != null)
            {
                iconImage.color = selected ? selectedColor : baseColor;
            }
        }

        private void RefreshLockState()
        {
            bool unlocked = IsUnlocked();

            if (iconImage != null)
            {
                iconImage.enabled = unlocked;
                iconImage.raycastTarget = unlocked;
            }
        }

        private bool IsUnlocked()
        {
            return bossData != null && GameSaveManager.IsUnlocked(bossData.Id);
        }

        private IEnumerator HoldRoutine()
        {
            float elapsed = 0f;
            while (elapsed < holdSecondsToStart)
            {
                elapsed += Time.deltaTime;
                SetHoldFillAmount(Mathf.Clamp01(elapsed / holdSecondsToStart));
                yield return null;
            }

            holdRoutine = null;
            CompleteHold();
        }

        private void CompleteHold()
        {
            SetHoldFillAmount(0f);

            if (panelController == null || bossData == null || !IsUnlocked())
            {
                return;
            }

            panelController.SelectBoss(this, bossData);
            panelController.EnterSelectedBoss();
        }

        private void CancelHold()
        {
            if (holdRoutine != null)
            {
                StopCoroutine(holdRoutine);
                holdRoutine = null;
            }

            SetSelected(false);
            SetHoldFillAmount(0f);
        }

        private void SetHoldFillAmount(float value)
        {
            if (holdFillImage != null)
            {
                holdFillImage.fillAmount = value;
            }
        }
    }
}
