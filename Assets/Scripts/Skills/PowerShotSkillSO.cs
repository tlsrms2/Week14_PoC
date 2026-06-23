using UnityEngine;
using Week14.Combat;

namespace Week14.Skills
{
    [CreateAssetMenu(menuName = "Week14/Skills/Power Shot Skill", fileName = "PowerShotSkill")]
    public sealed class PowerShotSkillSO : BaseSkillSO
    {
        [Tooltip("스킬 사용 시 발사할 강력 탄환의 피해량입니다.")]
        [SerializeField, Min(1)] private int damage = 10;
        [Tooltip("일반 탄환 크기 대비 몇 배로 키울지입니다.")]
        [SerializeField, Min(0.1f)] private float sizeMultiplier = 3f;
        [Tooltip("발사할 강력 탄환의 색상입니다.")]
        [SerializeField] private Color projectileColor = Color.red;

        public override void Execute(GameObject user)
        {
            PlayerCombatController controller = ResolvePlayerController(user);
            if (controller == null)
            {
                return;
            }

            controller.FireSkillProjectile(damage, sizeMultiplier, projectileColor);
        }
    }
}
