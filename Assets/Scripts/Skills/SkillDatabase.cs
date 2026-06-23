using System.Collections.Generic;
using UnityEngine;

namespace Week14.Skills
{
    [CreateAssetMenu(menuName = "Week14/Skills/Skill Database", fileName = "SkillDatabase")]
    public sealed class SkillDatabase : ScriptableObject
    {
        [Tooltip("게임에 존재하는 모든 스킬 에셋입니다. Skill ID로 검색됩니다.")]
        [SerializeField] private List<BaseSkillSO> skills = new();

        public IReadOnlyList<BaseSkillSO> AllSkills => skills;

        public BaseSkillSO FindById(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                return null;
            }

            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i] != null && skills[i].SkillId == skillId)
                {
                    return skills[i];
                }
            }

            return null;
        }
    }
}
