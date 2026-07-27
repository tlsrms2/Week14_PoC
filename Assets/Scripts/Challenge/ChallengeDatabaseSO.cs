using System;
using System.Collections.Generic;
using UnityEngine;
using Week14.UI;

namespace Week14.Challenge
{
    [Serializable]
    public sealed class BossChallengeGroup
    {
        [Tooltip("이 그룹의 챌린지들이 속한 보스입니다.")]
        [SerializeField] private BossData boss;
        [Tooltip("이 보스에 연결된 챌린지 목록입니다.")]
        [SerializeField] private List<ChallengeDefinitionSO> challenges = new();
        [Tooltip("도전과제 연동용 중간점검 기준 개수입니다. 달성한 챌린지 수가 이 값에 도달하면 중간 단계로 취급합니다.")]
        [SerializeField] private int checkpointChallengeCount;
        [Tooltip("이 보스의 챌린지를 처음 하나 완료했을 때 전송할 Steam 업적 API Name입니다.")]
        [SerializeField] private string firstClearAchievementId;
        [Tooltip("완료한 챌린지 수가 중간점검 기준을 넘었을 때 전송할 Steam 업적 API Name입니다.")]
        [SerializeField] private string checkpointAchievementId;
        [Tooltip("이 보스의 챌린지를 전부 완료했을 때 전송할 Steam 업적 API Name입니다.")]
        [SerializeField] private string allClearAchievementId;

        public BossData Boss => boss;
        public IReadOnlyList<ChallengeDefinitionSO> Challenges => challenges;
        public int TotalChallengeCount => challenges.Count;
        public int CheckpointChallengeCount => checkpointChallengeCount;
        public string FirstClearAchievementId => firstClearAchievementId;
        public string CheckpointAchievementId => checkpointAchievementId;
        public string AllClearAchievementId => allClearAchievementId;
    }

    [CreateAssetMenu(menuName = "Week14/Challenge/Challenge Database", fileName = "ChallengeDatabase")]
    public sealed class ChallengeDatabaseSO : ScriptableObject
    {
        [Tooltip("보스별로 묶인 챌린지 그룹 목록입니다.")]
        [SerializeField] private List<BossChallengeGroup> bossGroups = new();

        public IReadOnlyList<BossChallengeGroup> BossGroups => bossGroups;

        // 모든 보스를 통틀어 등록된 챌린지 총 개수입니다(세이브 슬롯 진행도 퍼센트 계산 등에 씁니다).
        public int TotalChallengeCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < bossGroups.Count; i++)
                {
                    total += bossGroups[i].Challenges.Count;
                }

                return total;
            }
        }

        public ChallengeDefinitionSO FindById(string challengeId)
        {
            if (string.IsNullOrEmpty(challengeId))
            {
                return null;
            }

            for (int i = 0; i < bossGroups.Count; i++)
            {
                IReadOnlyList<ChallengeDefinitionSO> challenges = bossGroups[i].Challenges;
                for (int j = 0; j < challenges.Count; j++)
                {
                    if (challenges[j] != null && challenges[j].ChallengeId == challengeId)
                    {
                        return challenges[j];
                    }
                }
            }

            return null;
        }

        public BossChallengeGroup GetGroup(string bossId)
        {
            if (string.IsNullOrEmpty(bossId))
            {
                return null;
            }

            for (int i = 0; i < bossGroups.Count; i++)
            {
                if (bossGroups[i].Boss != null && bossGroups[i].Boss.Id == bossId)
                {
                    return bossGroups[i];
                }
            }

            return null;
        }

        public IEnumerable<ChallengeDefinitionSO> ForBoss(string bossId)
        {
            if (string.IsNullOrEmpty(bossId))
            {
                yield break;
            }

            for (int i = 0; i < bossGroups.Count; i++)
            {
                BossChallengeGroup group = bossGroups[i];
                if (group.Boss == null || group.Boss.Id != bossId)
                {
                    continue;
                }

                IReadOnlyList<ChallengeDefinitionSO> challenges = group.Challenges;
                for (int j = 0; j < challenges.Count; j++)
                {
                    if (challenges[j] != null)
                    {
                        yield return challenges[j];
                    }
                }
            }
        }
    }
}
