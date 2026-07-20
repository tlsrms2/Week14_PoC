using UnityEngine;

namespace Week14.Challenge
{
    [CreateAssetMenu(menuName = "Week14/Challenge/Time Attack Challenge", fileName = "TimeAttackChallenge")]
    public sealed class TimeAttackChallengeSO : ChallengeDefinitionSO
    {
        [Tooltip("이 시간(초) 안에 전투를 승리해야 달성됩니다.")]
        [SerializeField, Min(0f)] private float targetSeconds = 60f;

        public override ChallengeType Kind => ChallengeType.TimeAttack;

        // 설명 텍스트의 {0} 자리에 "MM:SS" 형식으로 꽂힙니다. targetSeconds가 바뀌어도
        // 로컬라이징 테이블 문구를 따로 고칠 필요가 없도록 여기서 값을 만들어 넘깁니다.
        public override object[] LocalizedDescriptionArguments => new object[] { FormattedTargetTime };

        private string FormattedTargetTime
        {
            get
            {
                int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(targetSeconds));
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                return $"{minutes}:{seconds:00}";
            }
        }

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
