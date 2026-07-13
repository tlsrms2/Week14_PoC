using System;
using UnityEngine;
using Week14.Audio;
using Week14.Combat;
using Week14.Enemy;

namespace Week14.Skills
{
    [CreateAssetMenu(menuName = "Week14/Skills/Active/Clone Attack Skill", fileName = "CloneAttackSkill")]
    public sealed class CloneAttackSkillSO : BaseSkillSO
    {
        [Tooltip("분신이 유지되는 시간(초)입니다.")]
        [SerializeField, Min(0.1f)] private float durationSeconds = 10f;
        [Tooltip("플레이어 공격 피해량에 곱할 분신 피해 배율입니다. 0.5 = 50% 감소.")]
        [SerializeField, Range(0f, 1f)] private float damageMultiplier = 0.5f;
        [Tooltip("분신 비주얼 색상입니다. 알파로 투명도를 조절합니다.")]
        [SerializeField] private Color cloneTint = new Color(0.45f, 0.9f, 1f, 0.55f);
        [Tooltip("스킬 발동 시 재생할 SFX의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphSfxId]
        [SerializeField] private string activateSfxId;

        private event Action effectEnd;

        public override bool HasDelayedCooldownStart => true;

        public override void SubscribeEffectEnd(Action onEffectEnd) => effectEnd += onEffectEnd;
        public override void UnsubscribeEffectEnd(Action onEffectEnd) => effectEnd -= onEffectEnd;

        public override void Execute(GameObject user)
        {
            PlayerCombatController controller = ResolvePlayerController(user);
            if (controller == null)
            {
                effectEnd?.Invoke();
                return;
            }

            PlayerCloneAttackEcho echo = controller.GetComponent<PlayerCloneAttackEcho>();
            if (echo == null)
            {
                echo = controller.gameObject.AddComponent<PlayerCloneAttackEcho>();
            }

            echo.Activate(controller, durationSeconds, damageMultiplier, cloneTint, NotifyCloneEnded);

            if (!string.IsNullOrEmpty(activateSfxId))
            {
                SoundManager.PlaySfx(activateSfxId);
            }
        }

        private void NotifyCloneEnded()
        {
            effectEnd?.Invoke();
        }
    }
}
