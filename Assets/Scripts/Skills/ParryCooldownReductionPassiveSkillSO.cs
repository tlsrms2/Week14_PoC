using UnityEngine;
using Week14.Combat;

namespace Week14.Skills
{
    [CreateAssetMenu(menuName = "Week14/Skills/Passive/Parry Cooldown Reduction", fileName = "ParryCooldownReductionPassiveSkill")]
    public sealed class ParryCooldownReductionPassiveSkillSO : BasePassiveSkillSO
    {
        [Tooltip("투사체를 요격(패링)할 때마다 장착 중인 액티브 스킬의 남은 쿨타임에서 줄여줄 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float cooldownReductionSeconds = 1f;

        public override void ApplyPassive(GameObject player)
        {
            PlayerParryController.ProjectileParried -= HandleProjectileParried;
            PlayerParryController.ProjectileParried += HandleProjectileParried;
        }

        public override void RemovePassive(GameObject player)
        {
            PlayerParryController.ProjectileParried -= HandleProjectileParried;
        }

        private void HandleProjectileParried(EnemyProjectile _)
        {
            SkillLoadoutManager.Instance?.ReduceCooldown(cooldownReductionSeconds);
        }
    }
}
