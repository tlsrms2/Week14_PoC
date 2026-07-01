using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Week14.Save;

namespace Week14.UI
{
    [RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
    public sealed class BossSelectIcon : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, IPanelGatedInteractable
    {
        [SerializeField] private BossPanelController panelController;
        [SerializeField] private BossData bossData;
        [Tooltip("이 아이콘에 마우스를 올렸을 때 보여줄, 이 아이콘 전용 디테일 패널입니다.")]
        [SerializeField] private BossDetailPanel detailPanel;
        [Tooltip("누르고 있는 동안 적용할 색상입니다. 기본 이미지 색과 구분되는 색으로 설정하세요.")]
        [SerializeField] private Color selectedColor = new(1f, 0.85f, 0.3f);
        [Tooltip("이 아이콘을 누르고 있어야 하는 시간(초)입니다. 다 차면 해당 보스전이 시작됩니다.")]
        [SerializeField, Min(0.1f)] private float holdSecondsToStart = 1f;
        [Tooltip("누르고 있는 동안 차오르는 진행 바 이미지입니다. Image Type이 Filled여야 합니다.")]
        [SerializeField] private Image holdFillImage;
        [Tooltip("진행 바가 채워지는 감속 곡선의 강도입니다. 클수록 처음에 더 빠르게 차오르고 끝에 갈수록 더 느려집니다. 1 = 일정한 속도.")]
        [SerializeField, Min(1f)] private float holdFillEaseExponent = 2f;
        [Tooltip("이 아이콘과 같이 숨겨질 아웃라인 SpriteRenderer입니다. 잠겨있으면 아이콘과 함께 꺼집니다.")]
        [SerializeField] private SpriteRenderer outlineRenderer;

        private SpriteRenderer iconRenderer;
        private Collider2D iconCollider;
        private Color baseColor;
        private Coroutine holdRoutine;
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

            detailPanel?.Show(bossData);
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

        private IEnumerator HoldRoutine()
        {
            float elapsed = 0f;
            while (elapsed < holdSecondsToStart)
            {
                elapsed += Time.deltaTime;
                float ratio = Mathf.Clamp01(elapsed / holdSecondsToStart);
                SetHoldFillAmount(1f - Mathf.Pow(1f - ratio, holdFillEaseExponent));
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
