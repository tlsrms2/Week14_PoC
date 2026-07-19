using UnityEngine;
using Week14.Audio;

namespace Week14.Enemy
{
    public sealed partial class HogBossAI : GraphBossAI
    {
        private static readonly int StunParameter = Animator.StringToHash("Stun");
        private static readonly int EndStunParameter = Animator.StringToHash("EndStun");

        [SerializeField] private Animator groggyAnimator;

        [Header("BGM")]
        [Tooltip("전투 시작 시 재생할 BGM의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphBgmId]
        [SerializeField] private string bgmId = "HogBgm";

        protected override GameObject BossMuzzleFlashVfxPrefab => EffectData != null
            ? EffectData.HogMuzzleFlashVfxPrefab
            : null;
        protected override bool RotatesBodyToPlayer => false;

        protected override void OnCombatStarted()
        {
            if (!string.IsNullOrWhiteSpace(bgmId))
            {
                SoundManager.PlayBgm(bgmId);
            }
        }

        protected override void OnHpEmptyBegan()
        {
            SetAnimatorTrigger(StunParameter);
        }

        protected override void OnHpEmptyRecovered()
        {
            SetAnimatorTrigger(EndStunParameter);
        }

        private void SetAnimatorTrigger(int parameter)
        {
            Animator targetAnimator = ResolveGroggyAnimator();
            if (targetAnimator == null)
            {
                return;
            }

            targetAnimator.SetTrigger(parameter);
        }

        private Animator ResolveGroggyAnimator()
        {
            if (groggyAnimator != null)
            {
                return groggyAnimator;
            }

            groggyAnimator = BodyRoot != null
                ? BodyRoot.GetComponentInChildren<Animator>(true)
                : GetComponentInChildren<Animator>(true);
            return groggyAnimator;
        }
    }
}
