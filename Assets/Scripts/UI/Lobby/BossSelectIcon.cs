using UnityEngine;
using UnityEngine.EventSystems;
using Week14.Save;

namespace Week14.UI
{
    [RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
    public sealed class BossSelectIcon : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPanelGatedInteractable
    {
        [SerializeField] private BossPanelController panelController;
        [SerializeField] private BossData bossData;
        [Tooltip("이 아이콘에 마우스를 올렸을 때 보여줄, 이 아이콘 전용 디테일 패널입니다.")]
        [SerializeField] private BossDetailPanel detailPanel;
        [Tooltip("이 아이콘에 마우스를 올렸을 때 이 보스의 챌린지 목록을 보여줄 패널입니다. 비워두면 표시하지 않습니다.")]
        [SerializeField] private BossChallengePanel challengePanel;
        [Tooltip("누르고 있는 동안 적용할 색상입니다. 기본 이미지 색과 구분되는 색으로 설정하세요.")]
        [SerializeField] private Color selectedColor = new(1f, 0.85f, 0.3f);
        [Tooltip("이 아이콘과 같이 숨겨질 아웃라인 SpriteRenderer입니다. 잠겨있으면 아이콘과 함께 꺼집니다.")]
        [SerializeField] private SpriteRenderer outlineRenderer;

        private SpriteRenderer iconRenderer;
        private Collider2D iconCollider;
        private Color baseColor;
        private bool panelOpen;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (iconRenderer != null)
            {
                return;
            }

            iconRenderer = GetComponent<SpriteRenderer>();
            iconCollider = GetComponent<Collider2D>();
            baseColor = iconRenderer.color;
        }

        private void OnEnable()
        {
            RefreshLockState();
            SetSelected(false);
        }

        private void OnDisable()
        {
            SetSelected(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsUnlocked())
            {
                return;
            }

            detailPanel?.Show(bossData);
            challengePanel?.Show(bossData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            detailPanel?.Hide();
            challengePanel?.Hide();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsUnlocked())
            {
                return;
            }

            SelectAndEnterBoss();
        }

        public void SetSelected(bool selected)
        {
            iconRenderer.color = selected ? selectedColor : baseColor;
        }

        public void SetPanelOpen(bool open)
        {
            EnsureInitialized();
            panelOpen = open;
            UpdateColliderEnabled();
        }

        private void RefreshLockState()
        {
            EnsureInitialized();
            bool unlocked = IsUnlocked();
            iconRenderer.enabled = unlocked;

            if (outlineRenderer != null)
            {
                outlineRenderer.enabled = unlocked;
            }

            UpdateColliderEnabled();
        }

        private void UpdateColliderEnabled()
        {
            // LobbyMenuController(Awake)와 이 컴포넌트(OnEnable)는 어느 쪽이 먼저 실행될지 보장되지 않아서,
            // 한쪽이 Collider2D.enabled를 직접 덮어쓰면 다른 쪽이 나중에 실행되며 그 값을 다시 뒤집어버린다.
            // 두 조건(잠금 해제 여부 / 패널이 열려 있는지)을 항상 같이 계산해서 순서와 무관하게 일치시킨다.
            iconCollider.enabled = IsUnlocked() && panelOpen;
        }

        private bool IsUnlocked()
        {
            return bossData != null && GameSaveManager.IsUnlocked(bossData.Id);
        }

        private void SelectAndEnterBoss()
        {
            if (panelController == null || bossData == null || !IsUnlocked())
            {
                return;
            }

            panelController.SelectBoss(this, bossData);
            panelController.EnterSelectedBoss();
        }
    }
}
