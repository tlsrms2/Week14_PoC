using UnityEngine;
using Week14.Combat;

namespace Week14.Skills
{
    [CreateAssetMenu(menuName = "Week14/Skills/Reload Skill", fileName = "ReloadSkill")]
    public sealed class ReloadSkillSO : BaseSkillSO
    {
        [Tooltip("스킬 사용 시 즉시 회복시킬 탄환 수입니다.")]
        [SerializeField, Min(1)] private int reloadAmount = 10;

        public override void Execute(GameObject user)
        {
            PlayerCombatController controller = ResolvePlayerController(user);
            if (controller == null)
            {
                return;
            }

            if (controller.Bullets != null && controller.Bullets.Restore(reloadAmount, BulletChangeSource.Generic))
            {
                PlayerBulletAudio.PlayBulletRestoreSfx(controller.Bullets.CurrentBullets, controller.Bullets.MaxBullets);
            }

            controller.PlayReloadAnimation();
        }
    }
}
