using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Week14.Save;

namespace Week14.UI
{
    [RequireComponent(typeof(Image))]
    public sealed class BossSelectIcon : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private BossPanelController panelController;
        [SerializeField] private BossData bossData;
        [Tooltip("이 아이콘에 마우스를 올렸을 때 보여줄, 이 아이콘 전용 디테일 패널입니다.")]
        [SerializeField] private BossDetailPanel detailPanel;
        [Tooltip("클릭으로 선택(강조)됐을 때 적용할 색상입니다. 기본 이미지 색과 구분되는 색으로 설정하세요.")]
        [SerializeField] private Color selectedColor = new(1f, 0.85f, 0.3f);

        private Image iconImage;
        private Color baseColor;

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
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Debug.Log($"[BossSelectIcon] 클릭됨: {(bossData != null ? bossData.BossName : "Unknown")} (locked={!IsUnlocked()})", this);

            if (panelController == null || bossData == null || !IsUnlocked())
            {
                return;
            }

            panelController.SelectBoss(this, bossData);
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
    }
}
