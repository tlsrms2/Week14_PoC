using UnityEngine;
using Week14.Combat;

namespace Week14.Challenge
{
    [CreateAssetMenu(menuName = "Week14/Challenge/Parry Clear Challenge", fileName = "ParryClearChallenge")]
    public sealed class ParryClearChallengeSO : ChallengeDefinitionSO
    {
        [Tooltip("한 판 안에서 이 횟수 이상 패링한 채로 보스를 클리어해야 달성됩니다 (ParryCountChallengeSO와 달리 여러 판에 걸쳐 누적되지 않습니다).")]
        [SerializeField, Min(1)] private int minParries = 3;
        [Tooltip("비워두면 모든 탄을 카운트합니다. 채워두면 이 배열에 있는 프리팹 중 하나에서 나온 탄을 패링했을 때만 카운트됩니다.")]
        [SerializeField] private EnemyProjectile[] targetProjectilePrefabs;

        public override ChallengeType Kind => ChallengeType.ParryClear;

        // 설명 텍스트의 {0} 자리에 목표 패링 횟수가 꽂힙니다.
        public override object[] LocalizedDescriptionArguments => new object[] { minParries };

        public override ChallengeRunState CreateRunState()
        {
            return new RunState(minParries, targetProjectilePrefabs);
        }

        private sealed class RunState : ChallengeRunState
        {
            private readonly int minParries;
            private readonly EnemyProjectile[] targetProjectilePrefabs;
            private int matchedParriesThisRun;

            public RunState(int minParries, EnemyProjectile[] targetProjectilePrefabs)
            {
                this.minParries = minParries;
                this.targetProjectilePrefabs = targetProjectilePrefabs;
            }

            public override void OnParried(EnemyProjectile projectile)
            {
                if (!MatchesFilter(projectile))
                {
                    return;
                }

                matchedParriesThisRun++;
            }

            private bool MatchesFilter(EnemyProjectile projectile)
            {
                if (targetProjectilePrefabs == null || targetProjectilePrefabs.Length == 0)
                {
                    return true;
                }

                for (int i = 0; i < targetProjectilePrefabs.Length; i++)
                {
                    if (targetProjectilePrefabs[i] != null && projectile.SourcePrefab == targetProjectilePrefabs[i])
                    {
                        return true;
                    }
                }

                return false;
            }

            public override bool TryFinalize(bool victory, string challengeId)
            {
                return victory && matchedParriesThisRun >= minParries;
            }
        }
    }
}
