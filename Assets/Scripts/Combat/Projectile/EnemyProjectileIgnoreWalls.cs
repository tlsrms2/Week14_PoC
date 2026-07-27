using UnityEngine;

namespace Week14.Combat
{
    [AddComponentMenu("Week14/Combat/Enemy Projectile (Ignore Walls)")]
    public sealed class EnemyProjectileIgnoreWalls : EnemyProjectile
    {
        protected override bool IgnoresWalls => true;
    }
}
