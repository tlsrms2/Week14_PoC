using UnityEngine;

namespace Week14.Challenge
{
    public abstract class ChallengeDefinitionSO : ScriptableObject
    {
        [Tooltip("챌린지 고유 식별자입니다. 저장 데이터에서 이 값으로 완료 여부를 기록합니다.")]
        [SerializeField] private string challengeId;
        [Tooltip("달성 시 지급되는 챌린지 포인트입니다.")]
        [SerializeField] private int rewardPoint;

        public string ChallengeId => challengeId;
        public int RewardPoint => rewardPoint;

        public abstract ChallengeType Kind { get; }

        public abstract ChallengeRunState CreateRunState();
    }
}
