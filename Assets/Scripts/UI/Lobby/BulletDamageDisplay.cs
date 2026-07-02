using System;
using TMPro;
using UnityEngine;
using Week14.Weapons;

namespace Week14.UI
{
    public sealed class BulletDamageDisplay : MonoBehaviour
    {
        [Serializable]
        private struct BulletSlot
        {
            [Tooltip("탄 1개를 표시하는 오브젝트입니다. 활성/비활성으로 탄 개수를 표시합니다.")]
            public GameObject root;
            [Tooltip("이 탄의 데미지 숫자를 표시할 텍스트입니다.")]
            public TMP_Text damageText;
        }

        [Tooltip("꽉 찬 탄창(첫 발)부터 마지막 1발 순서로 넣어주세요. 무기의 MaxAmmo보다 슬롯 개수가 많으면 앞쪽(0번)부터 비활성화되고, 맨 뒤 슬롯은 항상 마지막 1발로 유지됩니다.")]
        [SerializeField] private BulletSlot[] slots = Array.Empty<BulletSlot>();

        public void Refresh(BaseWeaponSO weapon)
        {
            if (weapon == null)
            {
                Clear();
                return;
            }

            int maxAmmo = weapon.MaxAmmo;
            int[] damagePerAmmoStep = weapon.DamagePerAmmoStep;

            // 슬롯 수가 무기의 MaxAmmo보다 많으면 마지막 1발 슬롯(맨 뒤)은 항상 고정해두고
            // 남는 만큼 앞쪽(0번)부터 꺼서, 어떤 무기든 맨 뒤 슬롯이 "마지막 1발"을 가리키게 한다.
            int offset = Mathf.Max(0, slots.Length - maxAmmo);

            for (int i = 0; i < slots.Length; i++)
            {
                BulletSlot slot = slots[i];
                bool active = i >= offset;

                if (slot.root != null)
                {
                    slot.root.SetActive(active);
                }

                if (!active || slot.damageText == null)
                {
                    continue;
                }

                int stepIndex = slots.Length - 1 - i;
                int damage = damagePerAmmoStep != null && stepIndex >= 0 && stepIndex < damagePerAmmoStep.Length
                    ? damagePerAmmoStep[stepIndex]
                    : 0;
                slot.damageText.text = damage.ToString();
            }
        }

        public void Clear()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].root != null)
                {
                    slots[i].root.SetActive(false);
                }
            }
        }
    }
}
