using UnityEngine;

namespace Week14.Enemy
{
    [AddComponentMenu("Week14/Boss/Arsonist/Oil Spray Projectile")]
    public sealed class ArsonistOilSprayProjectile : ArsonistLineSprayProjectile
    {
        [SerializeField] private Color oilColor = new(0.12f, 0.09f, 0.04f, 0.75f);

        protected override void PlaceHazard(Vector3 position, float radius, float duration)
        {
            Owner?.CreateOilPatch(position, radius, duration, oilColor);
        }
    }
}
