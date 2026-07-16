using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Week14.Combat;
using Week14.Save;

namespace Week14.Weapons
{
    public sealed class WeaponLoadoutManager : MonoBehaviour
    {
        [Tooltip("Weapon ID와 실제 총기 에셋을 연결하는 데이터베이스입니다.")]
        [SerializeField] private WeaponDatabase database;
        [Tooltip("세이브 파일에 장착 기록이 없을 때(첫 실행) 자동으로 장착할 기본 총기입니다. " +
            "해금+무료 구매는 GameSaveConfig(Resources/GameSaveConfig.asset)의 기본 해금 총기 목록에서 처리되므로, " +
            "실제로 장착되게 하려면 그 목록에도 같은 총기를 등록해야 합니다. 비워두면 자동 장착하지 않습니다.")]
        [SerializeField] private BaseWeaponSO defaultWeapon;
        [Tooltip("디버그용: 체크하면 세이브 파일의 장착 총기를 무시하고 시작 시 항상 defaultWeapon을 장착합니다.")]
        [SerializeField] private bool forceDefaultWeapon;

        private static WeaponLoadoutManager instance;

        private BaseWeaponSO currentWeapon;

        public static WeaponLoadoutManager Instance => instance;

        public event Action<BaseWeaponSO> WeaponChanged;

        public BaseWeaponSO CurrentWeapon => currentWeapon;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            LoadEquippedWeapon();
            EquipDefaultWeaponIfNeeded();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        public bool EquipWeapon(string weaponId)
        {
            BaseWeaponSO weapon = database != null ? database.FindById(weaponId) : null;
            if (weapon == null
                || !GameSaveManager.IsWeaponUnlocked(weaponId)
                || !GameSaveManager.IsWeaponPurchased(weaponId))
            {
                return false;
            }

            GameObject playerObject = PlayerCombatController.Active != null ? PlayerCombatController.Active.gameObject : null;

            currentWeapon?.RemoveWeaponTrait(playerObject);
            currentWeapon = weapon;
            currentWeapon.ApplyWeaponTrait(playerObject);

            GameSaveManager.SetEquippedWeaponId(weaponId);
            ApplyAmmoConfig(currentWeapon);
            WeaponChanged?.Invoke(currentWeapon);
            return true;
        }

        public bool IsDefaultWeapon(string weaponId)
        {
            return defaultWeapon != null && defaultWeapon.WeaponId == weaponId;
        }

        public bool RefundWeapon(string weaponId)
        {
            BaseWeaponSO weapon = database != null ? database.FindById(weaponId) : null;
            if (weapon == null || weapon == defaultWeapon)
            {
                return false;
            }

            if (currentWeapon == weapon)
            {
                return false;
            }

            return GameSaveManager.RefundWeapon(weaponId, weapon.Price);
        }

        public void UnequipWeapon()
        {
            if (currentWeapon == null)
            {
                return;
            }

            GameObject playerObject = PlayerCombatController.Active != null ? PlayerCombatController.Active.gameObject : null;
            currentWeapon.RemoveWeaponTrait(playerObject);
            currentWeapon = null;

            GameSaveManager.SetEquippedWeaponId(null);
            WeaponChanged?.Invoke(null);
        }

        public int GetDamageForCurrentAmmo(int ammo)
        {
            return currentWeapon != null ? currentWeapon.GetDamageForAmmo(ammo) : 0;
        }

        private void ApplyAmmoConfig(BaseWeaponSO weapon)
        {
            if (weapon == null)
            {
                return;
            }

            BulletGauge bullets = PlayerCombatController.Active != null ? PlayerCombatController.Active.Bullets : null;
            bullets?.Configure(weapon.MaxAmmo, true, BulletChangeSource.WeaponSwitch);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            GameObject playerObject = PlayerCombatController.Active != null ? PlayerCombatController.Active.gameObject : null;
            currentWeapon?.ApplyWeaponTrait(playerObject);
            ApplyAmmoConfig(currentWeapon);
        }

        private void LoadEquippedWeapon()
        {
            currentWeapon = null;

            if (forceDefaultWeapon)
            {
                return;
            }

            string weaponId = GameSaveManager.GetEquippedWeaponId();
            currentWeapon = database != null ? database.FindById(weaponId) : null;
        }

        private void EquipDefaultWeaponIfNeeded()
        {
            if (defaultWeapon == null)
            {
                return;
            }

            if (!forceDefaultWeapon && currentWeapon != null)
            {
                return;
            }

            GameObject playerObject = PlayerCombatController.Active != null ? PlayerCombatController.Active.gameObject : null;
            currentWeapon = defaultWeapon;
            currentWeapon.ApplyWeaponTrait(playerObject);
            WeaponChanged?.Invoke(currentWeapon);
        }
    }
}
