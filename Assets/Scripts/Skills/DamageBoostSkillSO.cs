using UnityEngine;
using Week14.Audio;
using Week14.Combat;
using Week14.Enemy;

namespace Week14.Skills
{
    [CreateAssetMenu(menuName = "Week14/Skills/Active/Damage Boost Skill", fileName = "DamageBoostSkill")]
    public sealed class DamageBoostSkillSO : BaseSkillSO
    {
        [Tooltip("다음 공격 1회(현재 장착한 무기 기준: 권총 한 발 / 샷건 한 발의 전체 펠릿 / 스나이퍼 차지샷 1회)의 피해량에 곱할 배율입니다. 예: 3 = 3배.")]
        [SerializeField, Min(1f)] private float damageMultiplier = 3f;
        [Tooltip("스킬을 사용했을 때 재생할 SFX의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphSfxId]
        [SerializeField] private string activateSfxId;

        public override void Execute(GameObject user)
        {
            PlayerCombatController controller = ResolvePlayerController(user);
            if (controller == null)
            {
                return;
            }

            controller.ArmNextAttackDamageMultiplier(damageMultiplier);

            if (!string.IsNullOrEmpty(activateSfxId))
            {
                SoundManager.PlaySfx(activateSfxId);
            }
        }
    }
}
