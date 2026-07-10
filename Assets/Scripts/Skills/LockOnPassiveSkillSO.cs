using UnityEngine;
using Week14.Combat;

namespace Week14.Skills
{
    [CreateAssetMenu(menuName = "Week14/Skills/Passive/Lock On", fileName = "LockOnPassiveSkill")]
    public sealed class LockOnPassiveSkillSO : BasePassiveSkillSO
    {
        public override void ApplyPassive(GameObject player)
        {
            ResolvePlayerController(player)?.SetLockOnSuppressed(false);
        }

        public override void RemovePassive(GameObject player)
        {
            ResolvePlayerController(player)?.SetLockOnSuppressed(true);
        }
    }
}
