using UnityEngine;

namespace Week14.Challenge
{
    [CreateAssetMenu(menuName = "Week14/Challenge/Boss Clear Challenge", fileName = "BossClearChallenge")]
    public sealed class BossClearChallengeSO : ChallengeDefinitionSO
    {
        public override ChallengeType Kind => ChallengeType.BossClear;

        public override ChallengeRunState CreateRunState()
        {
            return new RunState();
        }

        private sealed class RunState : ChallengeRunState
        {
            public override bool TryFinalize(bool victory, string challengeId)
            {
                return victory;
            }
        }
    }
}
