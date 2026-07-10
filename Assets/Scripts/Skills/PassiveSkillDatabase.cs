using System.Collections.Generic;
using UnityEngine;

namespace Week14.Skills
{
    [CreateAssetMenu(menuName = "Week14/Skills/Passive Skill Database", fileName = "PassiveSkillDatabase")]
    public sealed class PassiveSkillDatabase : ScriptableObject
    {
        [Tooltip("게임에 존재하는 모든 패시브 스킬 에셋입니다. Skill ID로 검색됩니다.")]
        [SerializeField] private List<BasePassiveSkillSO> skills = new();

        public IReadOnlyList<BasePassiveSkillSO> AllSkills => skills;

        public BasePassiveSkillSO FindById(string skillId)
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
