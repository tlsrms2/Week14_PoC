using UnityEngine;
using Week14.Audio;
using Week14.Combat;
using Week14.Enemy;

namespace Week14.Skills
{
    [CreateAssetMenu(menuName = "Week14/Skills/Roll Skill", fileName = "RollSkill")]
    public sealed class RollSkillSO : BaseSkillSO
    {
        [Tooltip("구를 거리(미터)입니다.")]
        [SerializeField, Min(0.1f)] private float rollDistance = 3f;
        [Tooltip("구르기 동작에 걸리는 시간(초)입니다. 이 시간 동안 적의 공격을 회피합니다.")]
        [SerializeField, Min(0.05f)] private float rollDuration = 0.3f;
        [Tooltip("구르기 도중 플레이어 주변의 투사체를 자동으로 패링하는 범위(반지름)입니다.")]
        [SerializeField, Min(0f)] private float autoParryRadius = 2f;
        [Header("Sound")]
        [Tooltip("구르기가 실제로 시작됐을 때 재생할 SFX의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphSfxId]
        [SerializeField] private string rollSfxId = "Dash";
        [Header("VFX")]
        [Tooltip("구르기 잔상이 생성되는 간격입니다.")]
        [SerializeField, Min(0.01f)] private float afterimageInterval = 0.045f;
        [Tooltip("구르기 잔상이 사라지는 데 걸리는 시간입니다. 0이면 잔상을 끕니다.")]
        [SerializeField, Min(0f)] private float afterimageDuration = 0.18f;
        [Tooltip("구르기 잔상 색상입니다. 알파로 투명도를 조절합니다.")]
        [SerializeField] private Color afterimageColor = new Color(0.55f, 0.95f, 1f, 0.38f);
        [Tooltip("자동 패링된 투사체가 플레이어에게 흡수되는 연출 시간입니다. 0이면 흡수 연출을 끕니다.")]
        [SerializeField, Min(0f)] private float autoParryAbsorbDuration = 0.18f;
        [Tooltip("자동 패링 투사체 흡수 연출 색상입니다. 알파로 투명도를 조절합니다.")]
        [SerializeField] private Color autoParryAbsorbColor = new Color(0.45f, 0.95f, 1f, 0.85f);

        public override void Execute(GameObject user)
        {
            PlayerCombatController controller = ResolvePlayerController(user);
            if (controller == null)
            {
                return;
            }

            if (controller.TryDash(rollDistance, rollDuration, autoParryRadius, CreateVfxSettings())
                && !string.IsNullOrEmpty(rollSfxId))
            {
                SoundManager.PlaySfx(rollSfxId);
            }
        }

        private RollSkillVfxSettings CreateVfxSettings()
        {
            return new RollSkillVfxSettings(
                afterimageInterval,
                afterimageDuration,
                afterimageColor,
                autoParryAbsorbDuration,
                autoParryAbsorbColor);
        }
    }
}
