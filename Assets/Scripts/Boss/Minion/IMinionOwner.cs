using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public interface IMinionOwner
    {
        Transform MinionOwnerTransform { get; }
        Transform MinionTarget { get; }

        EnemyProjectile FireMinionProjectile(
            Minion source,
            BossProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            bool playMuzzleFlash);
    }
}
