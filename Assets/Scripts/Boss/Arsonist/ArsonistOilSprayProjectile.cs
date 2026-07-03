using UnityEngine;

namespace Week14.Enemy
{
    [AddComponentMenu("Week14/Boss/Arsonist/Oil Spray Projectile")]
    public sealed class ArsonistOilSprayProjectile : ArsonistLineSprayProjectile
    {
        protected override void PlaceHazard(Vector3 position, float radius, float duration)
        {
            Owner?.CreateOilPatch(position, radius, duration);
        }
    }
}
