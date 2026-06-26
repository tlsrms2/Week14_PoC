using UnityEngine;
using UnityEngine.EventSystems;
using Week14.Save;
using Week14.Weapons;

namespace Week14.UI
{
    [RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
    public sealed class LoadoutWeaponIcon : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPanelGatedInteractable
    {
        [SerializeField] private BaseWeaponSO weapon;
        [Tooltip("장착됐을 때 적용할 색상입니다. 기본 이미지 색과 구분되는 색으로 설정하세요.")]
        [SerializeField] private Color selectedColor = new(1f, 0.85f, 0.3f);

        private SpriteRenderer iconRenderer;
        private Collider2D iconCollider;
        private Color baseColor;
        private bool subscribedToWeaponChanged;
        private bool panelOpen;

        public BaseWeaponSO Weapon => weapon;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            RefreshLockState();
            RefreshSelected();
            TrySubscribe();
        }

        private void Start()
        {
            // WeaponLoadoutManager가 씬에 늦게 추가된 오브젝트라 OnEnable 시점엔
            // Instance가 아직 null일 수 있다. Start는 모든 Awake 이후에 실행되니 여기서 재시도한다.
            // (UnlockDefaultWeapon도 WeaponLoadoutManager.Awake에서 처리되므로 잠금 상태도 같이 재확인해야 한다.)
            RefreshLockState();
            RefreshSelected();
            TrySubscribe();
        }

        private void OnDisable()
        {
            if (subscribedToWeaponChanged && WeaponLoadoutManager.Instance != null)
            {
                WeaponLoadoutManager.Instance.WeaponChanged -= HandleWeaponChanged;
            }

            subscribedToWeaponChanged = false;
        }

        private void TrySubscribe()
        {
            if (subscribedToWeaponChanged || WeaponLoadoutManager.Instance == null)
            {
                return;
            }

            WeaponLoadoutManager.Instance.WeaponChanged += HandleWeaponChanged;
            subscribedToWeaponChanged = true;
        }

        private void OnValidate()
        {
            if (weapon == null)
            {
                return;
            }

            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sprite = weapon.Icon;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsUnlocked())
            {
                return;
            }

            WeaponTooltipPanel.Instance?.Show(weapon, transform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            WeaponTooltipPanel.Instance?.Hide();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (weapon == null || WeaponLoadoutManager.Instance == null || !IsUnlocked())
            {
                return;
            }

            WeaponLoadoutManager.Instance.EquipWeapon(weapon.WeaponId);
        }

        private void HandleWeaponChanged(BaseWeaponSO _)
        {
            RefreshSelected();
        }

        private void RefreshSelected()
        {
            bool selected = weapon != null
                && WeaponLoadoutManager.Instance != null
                && WeaponLoadoutManager.Instance.CurrentWeapon == weapon;
            SetSelected(selected);
        }

        private void SetSelected(bool selected)
        {
            EnsureInitialized();
            iconRenderer.color = selected ? selectedColor : baseColor;
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

            if (weapon != null)
            {
                iconRenderer.sprite = weapon.Icon;
            }
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
            iconRenderer.enabled = IsUnlocked();
            UpdateColliderEnabled();
        }

        private void UpdateColliderEnabled()
        {
            // LobbyMenuController(Awake)와 이 컴포넌트(OnEnable/Start)는 어느 쪽이 먼저 실행될지 보장되지 않아서,
            // 한쪽이 Collider2D.enabled를 직접 덮어쓰면 다른 쪽이 나중에 실행되며 그 값을 다시 뒤집어버린다.
            // 두 조건(잠금 해제 여부 / 패널이 열려 있는지)을 항상 같이 계산해서 순서와 무관하게 일치시킨다.
            iconCollider.enabled = IsUnlocked() && panelOpen;
        }

        private bool IsUnlocked()
        {
            return weapon != null && GameSaveManager.IsWeaponUnlocked(weapon.WeaponId);
        }
    }
}
