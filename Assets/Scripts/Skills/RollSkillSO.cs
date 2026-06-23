using UnityEngine;
using Week14.Combat;

namespace Week14.Skills
{
    [CreateAssetMenu(menuName = "Week14/Skills/Roll Skill", fileName = "RollSkill")]
    public sealed class RollSkillSO : BaseSkillSO
    {
        [Tooltip("구를 거리(미터)입니다.")]
        [SerializeField, Min(0.1f)] private float rollDistance = 3f;
        [Tooltip("구르기 동작에 걸리는 시간(초)입니다. 이 시간 동안 적의 공격을 회피합니다.")]
        [SerializeField, Min(0.05f)] private float rollDuration = 0.3f;

        public override void Execute(GameObject user)
        {
            PlayerCombatController controller = ResolvePlayerController(user);
            if (controller == null)
            {
                return;
            }

            controller.TryDash(rollDistance, rollDuration);
        }
    }
}
