using System.Collections.Generic;
using UnityEngine;

namespace Week14.Weapons
{
    [CreateAssetMenu(menuName = "Week14/Weapons/Weapon Database", fileName = "WeaponDatabase")]
    public sealed class WeaponDatabase : ScriptableObject
    {
        [Tooltip("게임에 존재하는 모든 총기 에셋입니다. Weapon ID로 검색됩니다.")]
        [SerializeField] private List<BaseWeaponSO> weapons = new();

        public IReadOnlyList<BaseWeaponSO> AllWeapons => weapons;

        public BaseWeaponSO FindById(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId))
            {
                return null;
            }

            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] != null && weapons[i].WeaponId == weaponId)
                {
                    return weapons[i];
                }
            }

            return null;
        }
    }
}
