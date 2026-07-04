using UnityEngine;

namespace Week14.Skills
{
    [CreateAssetMenu(menuName = "Week14/Skills/Dummy Passive Skill", fileName = "DummyPassiveSkill")]
    public sealed class DummyPassiveSkillSO : BasePassiveSkillSO
    {
        public override void ApplyPassive(GameObject player)
        {
        }

        public override void RemovePassive(GameObject player)
        {
        }
    }
}
