using UnityEngine;
using Week14.Combat;
using Week14.Save;

namespace Week14.Challenge
{
    [CreateAssetMenu(menuName = "Week14/Challenge/Destroy Object Count Challenge", fileName = "DestroyObjectCountChallenge")]
    public sealed class DestroyObjectCountChallengeSO : ChallengeDefinitionSO
    {
        [Tooltip("이 보스를 상대로 누적으로 이 횟수 이상 파괴해야 달성됩니다 (여러 판에 걸쳐 합산).")]
        [SerializeField, Min(1)] private int minDestructions = 3;
        [Tooltip("파괴 카운트를 인정할 프리팹 목록입니다. 이 중 하나에서 나온 오브젝트를 플레이어가 파괴했을 때만 카운트됩니다.")]
        [SerializeField] private EnemyProjectile[] targetPrefabs;

        public override ChallengeType Kind => ChallengeType.DestroyObjectCount;

        // 설명 텍스트의 {0} 자리에 목표 파괴 횟수가 꽂힙니다.
        public override object[] LocalizedDescriptionArguments => new object[] { minDestructions };

        public override int MaxProgress => minDestructions;

        public override int GetCurrentProgress(string bossId)
        {
            string saveKey = GameSaveManager.BuildChallengeSaveKey(bossId, ChallengeId);
            return Mathf.Min(minDestructions, GameSaveManager.GetChallengeCounter(saveKey));
        }

        public override ChallengeRunState CreateRunState()
        {
            return new RunState(minDestructions, targetPrefabs);
        }

        private sealed class RunState : ChallengeRunState
        {
            private readonly int minDestructions;
            private readonly EnemyProjectile[] targetPrefabs;
            private int matchedDestructionsThisRun;

            public RunState(int minDestructions, EnemyProjectile[] targetPrefabs)
            {
                this.minDestructions = minDestructions;
                this.targetPrefabs = targetPrefabs;
            }

            public override void OnObjectDestroyed(EnemyProjectile projectile)
            {
                if (!MatchesFilter(projectile))
                {
                    return;
                }

                matchedDestructionsThisRun++;
            }

            private bool MatchesFilter(EnemyProjectile projectile)
            {
                if (targetPrefabs == null || targetPrefabs.Length == 0)
                {
                    return true;
                }

                for (int i = 0; i < targetPrefabs.Length; i++)
                {
                    if (targetPrefabs[i] != null && projectile.SourcePrefab == targetPrefabs[i])
                    {
                        return true;
                    }
                }

                return false;
            }

            // 승패와 무관하게 이번 판에서 파괴한 만큼 누적 저장값에 더하고, 누적치가 목표치를 넘으면 달성 처리합니다.
            public override bool TryFinalize(bool victory, string challengeId)
            {
                if (matchedDestructionsThisRun <= 0)
                {
                    return false;
                }

                int accumulated = GameSaveManager.AddToChallengeCounter(challengeId, matchedDestructionsThisRun);
                return accumulated >= minDestructions;
            }
        }
    }
}
