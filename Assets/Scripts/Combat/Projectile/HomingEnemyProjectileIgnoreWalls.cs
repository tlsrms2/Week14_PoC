using UnityEngine;

namespace Week14.Combat
{
    [AddComponentMenu("Week14/Combat/Homing Enemy Projectile (Ignore Walls)")]
    public sealed class HomingEnemyProjectileIgnoreWalls : HomingEnemyProjectile
    {
        protected override bool IgnoresWalls => true;
    }
}
