using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public interface IMinionOwner
    {
        Transform MinionOwnerTransform { get; }
        Transform MinionTarget { get; }
        bool MinionsCanFlyOverGround { get; }

        EnemyProjectile FireMinionProjectile(
            Minion source,
            BossProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            bool playMuzzleFlash);
    }

    public interface IMinionPlayerHitHandler
    {
        bool TryHandleMinionPlayerHit(
            Minion minion,
            int bulletDamage,
            bool strongHit,
            Vector3 hitPosition,
            Vector2 hitDirection,
            Color hitColor);
    }
}
