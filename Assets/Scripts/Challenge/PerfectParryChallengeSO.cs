using UnityEngine;
using Week14.Combat;

namespace Week14.Challenge
{
    [CreateAssetMenu(menuName = "Week14/Challenge/Perfect Parry Challenge", fileName = "PerfectParryChallenge")]
    public sealed class PerfectParryChallengeSO : ChallengeDefinitionSO
    {
        [Tooltip("허용할 패링 실패 횟수입니다. 0이면 한 번이라도 놓치면 바로 실패, N이면 N번까지는 놓쳐도 괜찮고 N+1번째부터 실패합니다.")]
        [SerializeField, Min(0)] private int maxAllowedMisses;

        public override ChallengeType Kind => ChallengeType.PerfectParry;

        // 설명 텍스트의 {0} 자리에 허용 실패 횟수가 꽂힙니다.
        public override object[] LocalizedDescriptionArguments => new object[] { maxAllowedMisses };

        public override ChallengeRunState CreateRunState()
        {
            return new RunState(maxAllowedMisses);
        }

        private sealed class RunState : ChallengeRunState
        {
            private readonly int maxAllowedMisses;
            private int missCountThisRun;

            public RunState(int maxAllowedMisses)
            {
                this.maxAllowedMisses = maxAllowedMisses;
            }

            public override void OnParryFailed(ParryBaitRewardProjectile bait)
            {
                if (State != ChallengeState.Active)
                {
                    return;
                }

                missCountThisRun++;
                if (missCountThisRun > maxAllowedMisses)
                {
                    State = ChallengeState.Failed;
                }
            }

            public override bool TryFinalize(bool victory, string challengeId)
            {
                return victory && State != ChallengeState.Failed;
            }
        }
    }
}
