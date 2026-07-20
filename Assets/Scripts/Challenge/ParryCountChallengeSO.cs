using UnityEngine;
using Week14.Combat;
using Week14.Save;

namespace Week14.Challenge
{
    [CreateAssetMenu(menuName = "Week14/Challenge/Parry Count Challenge", fileName = "ParryCountChallenge")]
    public sealed class ParryCountChallengeSO : ChallengeDefinitionSO
    {
        [Tooltip("이 보스를 상대로 누적으로 이 횟수 이상 패링해야 달성됩니다 (여러 판에 걸쳐 합산).")]
        [SerializeField, Min(1)] private int minParries = 3;
        [Tooltip("비워두면 모든 탄을 카운트합니다. 채워두면 이 프리팹에서 나온 탄을 패링했을 때만 카운트됩니다.")]
        [SerializeField] private EnemyProjectile targetProjectilePrefab;

        public override ChallengeType Kind => ChallengeType.ParryCount;

        // 설명 텍스트의 {0} 자리에 목표 패링 횟수가 꽂힙니다.
        public override object[] LocalizedDescriptionArguments => new object[] { minParries };

        public override int MaxProgress => minParries;

        public override int GetCurrentProgress(string bossId)
        {
            string saveKey = GameSaveManager.BuildChallengeSaveKey(bossId, ChallengeId);
            return Mathf.Min(minParries, GameSaveManager.GetChallengeCounter(saveKey));
        }

        public override ChallengeRunState CreateRunState()
        {
            return new RunState(minParries, targetProjectilePrefab);
        }

        private sealed class RunState : ChallengeRunState
        {
            private readonly int minParries;
            private readonly EnemyProjectile targetProjectilePrefab;
            private int matchedParriesThisRun;

            public RunState(int minParries, EnemyProjectile targetProjectilePrefab)
            {
                this.minParries = minParries;
                this.targetProjectilePrefab = targetProjectilePrefab;
            }

            public override void OnParried(EnemyProjectile projectile)
            {
                if (targetProjectilePrefab != null && projectile.SourcePrefab != targetProjectilePrefab)
                {
                    return;
                }

                matchedParriesThisRun++;
            }

            // 승패와 무관하게 이번 판에서 패링한 만큼 누적 저장값에 더하고, 누적치가 목표치를 넘으면 달성 처리합니다.
            public override bool TryFinalize(bool victory, string challengeId)
            {
                if (matchedParriesThisRun <= 0)
                {
                    return false;
                }

                int accumulated = GameSaveManager.AddToChallengeCounter(challengeId, matchedParriesThisRun);
                return accumulated >= minParries;
            }
        }
    }
}
