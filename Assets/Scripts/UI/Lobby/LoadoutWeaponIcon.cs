using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Week14.Save;
using Week14.Weapons;

namespace Week14.UI
{
    [RequireComponent(typeof(Image))]
    public sealed class LoadoutWeaponIcon : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private BaseWeaponSO weapon;
        [Tooltip("장착됐을 때 적용할 색상입니다. 기본 이미지 색과 구분되는 색으로 설정하세요.")]
        [SerializeField] private Color selectedColor = new(1f, 0.85f, 0.3f);

        private Image iconImage;
        private RectTransform rectTransform;
        private Color baseColor;
        private bool subscribedToWeaponChanged;

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

            Image image = GetComponent<Image>();
            if (image != null)
            {
                image.sprite = weapon.Icon;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsUnlocked())
            {
                return;
            }

            WeaponTooltipPanel.Instance?.Show(weapon, rectTransform);
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

            if (iconImage != null)
            {
                iconImage.color = selected ? selectedColor : baseColor;
            }
        }

        private void EnsureInitialized()
        {
            if (iconImage != null)
            {
                return;
            }

            iconImage = GetComponent<Image>();
            rectTransform = transform as RectTransform;

            if (iconImage != null)
            {
                baseColor = iconImage.color;

                if (weapon != null)
                {
                    iconImage.sprite = weapon.Icon;
                }
            }
        }

        private void RefreshLockState()
        {
            EnsureInitialized();
            bool unlocked = IsUnlocked();

            if (iconImage != null)
            {
                iconImage.enabled = unlocked;
                iconImage.raycastTarget = unlocked;
            }
        }

        private bool IsUnlocked()
        {
            return weapon != null && GameSaveManager.IsWeaponUnlocked(weapon.WeaponId);
        }
    }
}
