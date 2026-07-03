using UnityEngine;

namespace Week14.Enemy
{
    [AddComponentMenu("Week14/Boss/Arsonist/Flame Spray Projectile")]
    public sealed class ArsonistFlameSprayProjectile : ArsonistLineSprayProjectile
    {
        protected override void PlaceHazard(Vector3 position, float radius, float duration)
        {
            Owner?.CreateFireArea(position, radius, duration);
        }
    }
}
