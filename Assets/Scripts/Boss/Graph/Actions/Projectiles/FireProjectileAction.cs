using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    public enum BossGraphAimMode
    {
        Player,
        Angle
    }

    [Serializable]
    public sealed class FireProjectileAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField] private BossGraphAimMode aimMode;
        [SerializeField] private float angleDegrees;
        [SerializeField, Min(0f)] private float spawnRadius;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            Vector3 origin = context.OriginPosition;
            Vector2 direction = aimMode == BossGraphAimMode.Player
                ? context.GetDirectionToPlayer(origin)
                : BossActionContext.AngleToDirection(angleDegrees);

            if (spawnRadius > 0f)
            {
                origin += (Vector3)(direction.normalized * spawnRadius);
            }

            context.FireProjectile(projectile, origin, direction, 0.9f, projectileName: projectileName);
            yield break;
        }
    }
}
