using System;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField, Min(0f)] private float minAngleDistanceDegrees = 25f;
        [SerializeField, Min(0f)] private float fireInterval = 0.08f;
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

            if (context.Boss is HackerBossAI hacker)
            {
                Vector2 facingDirection = context.GetDirectionToPlayer(hacker.transform.position);
                hacker.FaceHorizontalDirection(facingDirection.x);
            }

            using IDisposable facingLock = context.AcquireFacingLock();
            context.PlayAnimationTrigger(animationTriggerName);
            yield return HackerMeleeAttackAction.Wait(context, windupSeconds);

            Vector3 origin = context.GetBossChildPosition(launchOriginPath);
            int count = Mathf.Max(1, nodeCount);
            float firstAngle = randomizeStartAngle
                ? UnityEngine.Random.Range(0f, 360f)
                : startAngleOffset;
            List<float> fireAngles = CreateScatterAngles(count, firstAngle);
            for (int i = 0; i < count; i++)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    i--;
                    continue;
                }

                Vector2 direction = BossActionContext.AngleToDirection(fireAngles[i]);
                EnemyProjectile projectile = context.FireProjectile(settings, origin, direction, 0f, projectileName: nodeProjectileName);
                if (projectile is HackerWireNodeProjectile wireNode)
                {
                    wireNode.ConfigureWireOwner(context.Boss as HackerBossAI);
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

        private List<float> CreateScatterAngles(int count, float firstAngle)
        {
            float minimumDistance = Mathf.Min(
                Mathf.Max(0f, minAngleDistanceDegrees),
                360f / Mathf.Max(1, count));
            float remainingAngle = Mathf.Max(0f, 360f - minimumDistance * count);
            float[] gapWeights = new float[count];
            float totalWeight = 0f;
            for (int i = 0; i < count; i++)
            {
                gapWeights[i] = UnityEngine.Random.value;
                totalWeight += gapWeights[i];
            }

            List<float> angles = new(count);
            float angle = firstAngle;
            for (int i = 0; i < count; i++)
            {
                angles.Add(angle);
                float randomGap = totalWeight > 0.0001f
                    ? remainingAngle * gapWeights[i] / totalWeight
                    : remainingAngle / count;
                angle += minimumDistance + randomGap;
            }

            Shuffle(angles);
            return angles;
        }

        private static void Shuffle(List<float> values)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
            }
        }
    }
}
