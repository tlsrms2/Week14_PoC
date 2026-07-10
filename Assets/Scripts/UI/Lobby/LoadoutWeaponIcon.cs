using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Week14.Save;
using Week14.Weapons;

namespace Week14.UI
{
    // 무기 로스터의 무기칸 하나입니다. 액티브/패시브 스킬칸(LoadoutSkillIcon/PassiveLoadoutSkillIcon)과
    // 완전히 동일한 미해금/잠김/구매 로직 및 좌우클릭 규칙을 따릅니다.
    [RequireComponent(typeof(Image))]
    public sealed class LoadoutWeaponIcon : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private BaseWeaponSO weapon;
        [Tooltip("잠김(해금O 구매X) 상태일 때 무기 이미지 위에 표시할 잠금 마크입니다.")]
        [SerializeField] private GameObject lockMarkOverlay;
        [Tooltip("미해금 상태일 때 무기 이미지 대신 표시할 스프라이트입니다. 비워두면 그냥 아이콘을 숨깁니다.")]
        [SerializeField] private Sprite lockedPlaceholderSprite;
        [Tooltip("지금 장착되어 있는 무기일 때 적용할 색상입니다. 기본 이미지 색과 구분되는 색으로 설정하세요.")]
        [SerializeField] private Color equippedTintColor = new(1f, 0.85f, 0.3f);
        [Tooltip("이 무기가 마지막으로 호버되었을 때 켤 테두리 오브젝트입니다. 장착 강조와는 별개로 동작합니다.")]
        [SerializeField] private GameObject hoverOutline;
        [Tooltip("잠김 또는 구매 상태일 때만(=미해금이 아닐 때만) 표시할 이미지 패널입니다.")]
        [SerializeField] private GameObject unlockedInfoPanel;

        private Image iconImage;
        private Color baseColor;
        private bool subscribedToWeaponChanged;

        public BaseWeaponSO Weapon => weapon;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            RefreshVisualState();
            RefreshEquippedTint();
            SetActiveSafe(hoverOutline, false);
            TrySubscribe();
            LoadoutHoverHighlight.HoveredSkillIdChanged += HandleHoveredSkillChanged;
        }

        private void Start()
        {
            // WeaponLoadoutManager는 씬에 늦게 생기는 DontDestroyOnLoad 싱글턴이라
            // OnEnable 시점엔 Instance가 아직 null일 수 있다. Start에서 한 번 더 시도한다.
            RefreshEquippedTint();
            TrySubscribe();
        }

        private void OnDisable()
        {
            if (subscribedToWeaponChanged && WeaponLoadoutManager.Instance != null)
            {
                WeaponLoadoutManager.Instance.WeaponChanged -= HandleWeaponChanged;
            }

            subscribedToWeaponChanged = false;
            LoadoutHoverHighlight.HoveredSkillIdChanged -= HandleHoveredSkillChanged;
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

        private void TrySubscribe()
        {
            if (subscribedToWeaponChanged || WeaponLoadoutManager.Instance == null)
            {
                return;
            }

            WeaponLoadoutManager.Instance.WeaponChanged += HandleWeaponChanged;
            subscribedToWeaponChanged = true;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (GetState() == LoadoutSkillLockState.NotUnlocked)
            {
                return;
            }

            LoadoutSelectedSkillPanel.Instance?.Show(weapon);
            LoadoutHoverHighlight.SetHovered(weapon.WeaponId);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            LoadoutSelectedSkillPanel.Instance?.Hide();
            LoadoutHoverHighlight.ClearHovered();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (weapon == null || WeaponLoadoutManager.Instance == null)
            {
                return;
            }

            LoadoutSkillLockState state = GetState();
            bool isEquipped = WeaponLoadoutManager.Instance.CurrentWeapon == weapon;

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (state == LoadoutSkillLockState.Locked)
                {
                    if (GameSaveManager.PurchaseWeapon(weapon.WeaponId, weapon.Price))
                    {
                        RefreshVisualState();
                        LoadoutSelectedSkillPanel.Instance?.Show(weapon);
                    }
                }
                else if (state == LoadoutSkillLockState.Purchased && !isEquipped)
                {
                    WeaponLoadoutManager.Instance.EquipWeapon(weapon.WeaponId);
                    LoadoutSelectedSkillPanel.Instance?.Show(weapon);
                }
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (state == LoadoutSkillLockState.Purchased)
                {
                    // 무기는 항상 하나는 장착돼 있어야 하므로, 장착 중인 무기는 우클릭으로 장착 해제할 수 없습니다.
                    // (다른 무기를 좌클릭해서 교체하는 것만 가능합니다.)
                    if (!isEquipped && WeaponLoadoutManager.Instance.RefundWeapon(weapon.WeaponId))
                    {
                        RefreshVisualState();
                        LoadoutSelectedSkillPanel.Instance?.Show(weapon);
                    }
                }
            }
        }

        private void HandleWeaponChanged(BaseWeaponSO _)
        {
            RefreshEquippedTint();
        }

        private void HandleHoveredSkillChanged(string hoveredSkillId)
        {
            if (hoverOutline != null)
            {
                hoverOutline.SetActive(weapon != null && hoveredSkillId == weapon.WeaponId);
            }
        }

        private void RefreshEquippedTint()
        {
            EnsureInitialized();

            if (iconImage == null)
            {
                return;
            }

            bool isEquipped = weapon != null && WeaponLoadoutManager.Instance != null && WeaponLoadoutManager.Instance.CurrentWeapon == weapon;
            iconImage.color = isEquipped ? equippedTintColor : baseColor;
        }

        private void EnsureInitialized()
        {
            if (iconImage != null)
            {
                return;
            }

            iconImage = GetComponent<Image>();

            if (iconImage != null)
            {
                baseColor = iconImage.color;
            }
        }

        private LoadoutSkillLockState GetState()
        {
            if (weapon == null || !GameSaveManager.IsWeaponUnlocked(weapon.WeaponId))
            {
                return LoadoutSkillLockState.NotUnlocked;
            }

            return GameSaveManager.IsWeaponPurchased(weapon.WeaponId)
                ? LoadoutSkillLockState.Purchased
                : LoadoutSkillLockState.Locked;
        }

        private void RefreshVisualState()
        {
            EnsureInitialized();

            if (iconImage == null)
            {
                return;
            }

            if (weapon == null)
            {
                iconImage.enabled = false;
                iconImage.raycastTarget = false;
                SetActiveSafe(lockMarkOverlay, false);
                SetActiveSafe(unlockedInfoPanel, false);
                return;
            }

            LoadoutSkillLockState state = GetState();

            switch (state)
            {
                case LoadoutSkillLockState.NotUnlocked:
                    iconImage.sprite = lockedPlaceholderSprite;
                    iconImage.enabled = lockedPlaceholderSprite != null;
                    iconImage.raycastTarget = false;
                    SetActiveSafe(lockMarkOverlay, false);
                    SetActiveSafe(unlockedInfoPanel, false);
                    break;
                case LoadoutSkillLockState.Locked:
                    iconImage.sprite = weapon.Icon;
                    iconImage.enabled = true;
                    iconImage.raycastTarget = true;
                    SetActiveSafe(lockMarkOverlay, true);
                    SetActiveSafe(unlockedInfoPanel, true);
                    break;
                case LoadoutSkillLockState.Purchased:
                    iconImage.sprite = weapon.Icon;
                    iconImage.enabled = true;
                    iconImage.raycastTarget = true;
                    SetActiveSafe(lockMarkOverlay, false);
                    SetActiveSafe(unlockedInfoPanel, true);
                    break;
            }
        }

        private static void SetActiveSafe(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
