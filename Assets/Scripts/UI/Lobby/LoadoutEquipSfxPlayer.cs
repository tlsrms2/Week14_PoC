using UnityEngine;
using Week14.Audio;
using Week14.Skills;
using Week14.Weapons;

namespace Week14.UI
{
    [DisallowMultipleComponent]
    public sealed class LoadoutEquipSfxPlayer : MonoBehaviour
    {
        [Tooltip("같은 프레임이나 아주 짧은 간격에 여러 장착 이벤트가 겹칠 때 중복 재생을 막는 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float debounceSeconds = 0.05f;

        private WeaponLoadoutManager subscribedWeaponManager;
        private SkillLoadoutManager subscribedSkillManager;
        private PassiveSkillLoadoutManager subscribedPassiveSkillManager;
        private float lastPlayTime = float.NegativeInfinity;

        private void OnEnable()
        {
            TrySubscribeManagers();
        }

        private void Update()
        {
            TrySubscribeManagers();
        }

        private void OnDisable()
        {
            UnsubscribeManagers();
        }

        private void TrySubscribeManagers()
        {
            if (subscribedWeaponManager == null && WeaponLoadoutManager.Instance != null)
            {
                subscribedWeaponManager = WeaponLoadoutManager.Instance;
                subscribedWeaponManager.WeaponEquippedByUser += HandleWeaponChanged;
            }

            if (subscribedSkillManager == null && SkillLoadoutManager.Instance != null)
            {
                subscribedSkillManager = SkillLoadoutManager.Instance;
                subscribedSkillManager.SkillEquippedByUser += HandleSkillEquipped;
            }

            if (subscribedPassiveSkillManager == null && PassiveSkillLoadoutManager.Instance != null)
            {
                subscribedPassiveSkillManager = PassiveSkillLoadoutManager.Instance;
                subscribedPassiveSkillManager.SkillEquippedByUser += HandlePassiveSkillEquipped;
            }
        }

        private void UnsubscribeManagers()
        {
            if (subscribedWeaponManager != null)
            {
                subscribedWeaponManager.WeaponEquippedByUser -= HandleWeaponChanged;
                subscribedWeaponManager = null;
            }

            if (subscribedSkillManager != null)
            {
                subscribedSkillManager.SkillEquippedByUser -= HandleSkillEquipped;
                subscribedSkillManager = null;
            }

            if (subscribedPassiveSkillManager != null)
            {
                subscribedPassiveSkillManager.SkillEquippedByUser -= HandlePassiveSkillEquipped;
                subscribedPassiveSkillManager = null;
            }
        }

        private void HandleWeaponChanged(BaseWeaponSO weapon)
        {
            if (weapon != null)
            {
                PlayEquipSfx();
            }
        }

        private void HandleSkillEquipped(SkillSlot slot, BaseSkillSO skill)
        {
            if (skill != null)
            {
                PlayEquipSfx();
            }
        }

        private void HandlePassiveSkillEquipped(PassiveSkillSlot slot, BasePassiveSkillSO skill)
        {
            if (skill != null)
            {
                PlayEquipSfx();
            }
        }

        private void PlayEquipSfx()
        {
            float now = Time.unscaledTime;
            if (now - lastPlayTime < debounceSeconds)
            {
                return;
            }

            lastPlayTime = now;
            SoundManager.PlaySfx(SoundEvent.UI_LobbyLoadoutEquip);
        }
    }
}
