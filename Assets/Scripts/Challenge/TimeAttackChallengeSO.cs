using UnityEngine;

namespace Week14.Challenge
{
    [CreateAssetMenu(menuName = "Week14/Challenge/Time Attack Challenge", fileName = "TimeAttackChallenge")]
    public sealed class TimeAttackChallengeSO : ChallengeDefinitionSO
    {
        [Tooltip("이 시간(초) 안에 전투를 승리해야 달성됩니다.")]
        [SerializeField, Min(0f)] private float targetSeconds = 60f;

        public override ChallengeType Kind => ChallengeType.TimeAttack;

        public override ChallengeRunState CreateRunState()
        {
            return new RunState(targetSeconds);
        }

        private sealed class RunState : ChallengeRunState
        {
            private readonly float targetSeconds;

            public RunState(float targetSeconds)
            {
                this.targetSeconds = targetSeconds;
            }

            public override void OnTick(float elapsedSeconds)
            {
                if (State == ChallengeState.Active && elapsedSeconds > targetSeconds)
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
