using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class HackerScatterWireNodeAction : BossAction, IBossActionDurationProvider
    {
        [SerializeField, BossGraphProjectileName] private string nodeProjectileName = "WireNode";
        [SerializeField, HideInInspector] private BossProjectileSettings nodeProjectile = new();
        [SerializeField, BossGraphBossChildPath] private string launchOriginPath;
        [SerializeField] private string animationTriggerName = "ScatterWireNode";
        [SerializeField, Min(0f)] private float windupSeconds = 0.35f;
        [SerializeField, Min(1)] private int nodeCount = 6;
        [SerializeField] private float startAngleOffset;
        [SerializeField] private bool randomizeStartAngle;
        [SerializeField, Min(0f)] private float fireInterval = 0.08f;
        [SerializeField, Min(1)] private int hackingPerHit = 1;
        [SerializeField, Min(0f)] private float attachedNodeLifetimeSeconds = 10f;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.25f;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss == null)
            {
                yield break;
            }

            BossProjectileSettings settings = context.ResolveGraphProjectileSettings(nodeProjectileName) ?? nodeProjectile;
            if (settings?.Prefab == null)
            {
                yield break;
            }

            context.PlayAnimationTrigger(animationTriggerName);
            yield return HackerMeleeAttackAction.Wait(context, windupSeconds);

            Vector3 origin = context.GetBossChildPosition(launchOriginPath);
            int count = Mathf.Max(1, nodeCount);
            float firstAngle = randomizeStartAngle
                ? UnityEngine.Random.Range(0f, 360f)
                : startAngleOffset;
            float angleStep = 360f / count;
            for (int i = 0; i < count; i++)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    i--;
                    continue;
                }

                Vector2 direction = BossActionContext.AngleToDirection(firstAngle + angleStep * i);
                EnemyProjectile projectile = context.FireProjectile(settings, origin, direction, 0f, projectileName: nodeProjectileName);
                if (projectile is HackerWireNodeProjectile wireNode)
                {
                    wireNode.ConfigureWireHacking(context.Boss as HackerBossAI, hackingPerHit);
                    wireNode.ConfigureAttachedLifetime(attachedNodeLifetimeSeconds);
                }

                if (i < count - 1)
                {
                    yield return HackerMeleeAttackAction.Wait(context, fireInterval);
                }
            }

            yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, windupSeconds)
                + Mathf.Max(0f, fireInterval) * Mathf.Max(0, nodeCount - 1)
                + Mathf.Max(0f, recoverySeconds);
            return true;
        }
    }
}
