using UnityEngine;

namespace Week14.Enemy
{
    public sealed partial class HogBossAI : GraphBossAI
    {
        private static readonly int StunParameter = Animator.StringToHash("Stun");
        private static readonly int EndStunParameter = Animator.StringToHash("EndStun");

        [SerializeField] private Animator groggyAnimator;

        protected override GameObject BossMuzzleFlashVfxPrefab => EffectData != null
            ? EffectData.HogMuzzleFlashVfxPrefab
            : null;
        protected override bool RotatesBodyToPlayer => false;
        protected override string PostExplosionDeathSfxId => Week14.Audio.GameplaySfxIds.HogDeath;

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
