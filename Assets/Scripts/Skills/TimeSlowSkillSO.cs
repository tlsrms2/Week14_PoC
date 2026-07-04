using UnityEngine;
using Week14.Audio;
using Week14.Enemy;

namespace Week14.Skills
{
    [CreateAssetMenu(menuName = "Week14/Skills/Time Slow Skill", fileName = "TimeSlowSkill")]
    public sealed class TimeSlowSkillSO : BaseSkillSO
    {
        [Tooltip("적/적탄에게 적용할 속도 배율입니다. 0.2 = 80% 감소.")]
        [SerializeField, Range(0f, 1f)] private float slowMultiplier = 0.2f;
        [Tooltip("느려진 상태가 유지되는 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float durationSeconds = 5f;
        [Tooltip("스킬 발동 시 재생할 SFX의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [SerializeField] private string sfxId;

        public override void Execute(GameObject user)
        {
            EnemyTimeScale.SetTemporary(slowMultiplier, durationSeconds);

            if (!string.IsNullOrEmpty(sfxId))
            {
                SoundManager.PlaySfx(sfxId);
            }
        }
    }
}
