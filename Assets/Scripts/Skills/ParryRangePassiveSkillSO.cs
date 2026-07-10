using UnityEngine;
using Week14.Combat;

namespace Week14.Skills
{
    [CreateAssetMenu(menuName = "Week14/Skills/Passive/Parry Range", fileName = "ParryRangePassiveSkill")]
    public sealed class ParryRangePassiveSkillSO : BasePassiveSkillSO
    {
        [Tooltip("패링 범위(ParryingRange)에 더해줄 값입니다. 예: 0.2면 무기 패링 범위가 +0.2 됩니다.")]
        [SerializeField, Min(0f)] private float parryRangeBonus = 0.2f;

        public override void ApplyPassive(GameObject player)
        {
            ParryRangeBonus.Set(parryRangeBonus);
        }

        public override void RemovePassive(GameObject player)
        {
            ParryRangeBonus.Set(0f);
        }
    }
}
