using UnityEngine;

namespace Week14.Enemy
{
    [AddComponentMenu("Week14/Boss/Arsonist/Flame Spray Projectile")]
    public sealed class ArsonistFlameSprayProjectile : ArsonistLineSprayProjectile
    {
        [SerializeField] private Color fireColor = new(1f, 0.35f, 0.05f, 0.9f);

        protected override void PlaceHazard(Vector3 position, float radius, float duration)
        {
            Owner?.CreateFireArea(position, radius, duration, fireColor);
        }
    }
}
