using System;
using UnityEngine;
using Week14.Audio;
using Week14.Combat;
using Week14.Enemy;

namespace Week14.Skills
{
    [CreateAssetMenu(menuName = "Week14/Skills/Active/Invulnerable Ammo Refill Skill", fileName = "InvulnerableAmmoRefillSkill")]
    public sealed class InvulnerableAmmoRefillSkillSO : BaseSkillSO
    {
        [Tooltip("무적 상태가 유지되는 지속시간(초)입니다.")]
        [SerializeField, Min(0.1f)] private float durationSeconds = 3f;
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

            if (!string.IsNullOrEmpty(activateSfxId))
            {
                SoundManager.PlaySfx(activateSfxId);
            }

            controller.BeginInvulnerableAmmoRefill(durationSeconds, () => effectEnd?.Invoke());
        }
    }
}
