using System;
using UnityEngine;
using Week14.Audio;
using Week14.Combat;

namespace Week14.Skills
{
    [CreateAssetMenu(menuName = "Week14/Skills/Active/Invulnerable Ammo Refill Skill", fileName = "InvulnerableAmmoRefillSkill")]
    public sealed class InvulnerableAmmoRefillSkillSO : BaseSkillSO
    {
        [Tooltip("무적 상태가 유지되는 지속시간(초)입니다.")]
        [SerializeField, Min(0.1f)] private float durationSeconds = 3f;
        [Tooltip("무적 상태에서 패링(피격 무효화)이 성공했을 때, 플레이어 주변 이 반경(미터) 안의 적 투사체를 제거합니다. 0이면 끕니다.")]
        [SerializeField, Min(0f)] private float parryClearRadius = 2f;
        [Tooltip("패링이 성공해 이 스킬의 무적이 즉시 종료된 직후, 이어서 부여할 짧은 일반 무적 시간(초)입니다. " +
            "무적이 꺼지는 순간 같은 프레임에 몰린 다른 공격에 바로 맞는 걸 막아줍니다. 0이면 추가 무적을 주지 않습니다.")]
        [SerializeField, Min(0f)] private float postParryInvulnerabilitySeconds = 0.3f;
        [Header("VFX")]
        [Tooltip("무적 패링으로 주변 탄막을 제거했을 때 재생할 이펙트 프리팹입니다. 비워두면 표시하지 않습니다.")]
        [SerializeField] private GameObject blankVfxPrefab;

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

            SoundManager.PlaySfx(SoundEvent.Skill_Parrying);

            controller.BeginInvulnerableAmmoRefill(
                durationSeconds,
                parryClearRadius,
                postParryInvulnerabilitySeconds,
                blankVfxPrefab,
                () => effectEnd?.Invoke());
        }
    }
}
