using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class HackerWalkFireCounterGrabAction : BossAction, IBossActionDurationProvider
    {
        [Header("Walk Fire")]
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField, BossGraphBossChildPath] private string fireOriginPath;
        [SerializeField] private string walkFireTriggerName = "WalkFire";
        [SerializeField, Min(0f)] private float windupSeconds = 0.25f;
        [SerializeField, Min(0.1f)] private float activeSeconds = 3f;
        [SerializeField, Min(0f)] private float walkSpeed = 2.5f;
        [SerializeField, Min(0.1f)] private float wanderRadius = 3.5f;
        [SerializeField, Min(0.1f)] private float wanderChangeSeconds = 0.8f;
        [SerializeField, Min(0.01f)] private float fireInterval = 0.35f;
        [SerializeField, Min(1)] private int maxShotCount = 8;

        [Header("Counter Grab")]
        [SerializeField] private string swordParryTriggerName = "SwordParry";
        [SerializeField, BossGraphBossChildPath] private string wireOriginPath;
        [SerializeField, Min(0.01f)] private float wireTravelSeconds = 0.12f;
        [SerializeField, Min(0.05f)] private float grabSeconds = 0.65f;
        [SerializeField, Min(1)] private int hackingPerHit = 1;

        [Header("Recovery")]
        [SerializeField, Min(0f)] private float recoverySeconds = 0.25f;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is not HackerBossAI hacker)
            {
                yield break;
            }

            context.PlayAnimationTrigger(walkFireTriggerName);
            yield return HackerMeleeAttackAction.Wait(context, windupSeconds);

            hacker.BeginGunWalkCounterParry();
            try
            {
                yield return WalkAndFire(context, hacker);
                if (hacker.IsGunWalkCounterParryTriggered)
                {
                    yield return CounterGrab(context, hacker);
                }
            }
            finally
            {
                hacker.EndGunWalkCounterParry();
                context.Stop();
            }

            yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, windupSeconds)
                + Mathf.Max(0.1f, activeSeconds)
                + Mathf.Max(0f, wireTravelSeconds)
                + Mathf.Max(0f, grabSeconds)
                + Mathf.Max(0f, recoverySeconds);
            return true;
        }

        private IEnumerator WalkAndFire(BossActionContext context, HackerBossAI hacker)
        {
            float elapsed = 0f;
            float nextDestinationAt = 0f;
            float nextFireAt = 0f;
            int shotCount = 0;
            Vector2 destination = context.Boss.transform.position;
            while (elapsed < activeSeconds && !hacker.IsGunWalkCounterParryTriggered)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                if (elapsed >= nextDestinationAt)
                {
                    Vector2 playerPosition = context.GetPlayerPosition();
                    destination = playerPosition + UnityEngine.Random.insideUnitCircle.normalized * wanderRadius;
                    nextDestinationAt += wanderChangeSeconds;
                }

                Vector2 toDestination = destination - (Vector2)context.Boss.transform.position;
                if (toDestination.sqrMagnitude > 0.01f)
                {
                    context.Boss.SetMovementVelocity(toDestination.normalized * walkSpeed);
                }
                else
                {
                    context.Stop();
                }

                if (shotCount < maxShotCount && elapsed >= nextFireAt)
                {
                    FireProjectile(context);
                    shotCount++;
                    nextFireAt += fireInterval;
                }

                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }
        }

        private IEnumerator CounterGrab(BossActionContext context, HackerBossAI hacker)
        {
            PlayerCombatController player = PlayerCombatController.Active;
            if (player == null)
            {
                yield break;
            }

            context.PlayAnimationTrigger(swordParryTriggerName);
            Transform wireOrigin = context.GetBossChildTransform(wireOriginPath) ?? context.Boss.transform;
            HackerWireSettings wireSettings = hacker.WireSettings;
            HackerWire wire = HackerWire.CreateGuaranteedGrab(
                hacker,
                wireOrigin,
                player,
                wireTravelSeconds,
                grabSeconds,
                hackingPerHit,
                wireSettings.PullSpeed,
                wireSettings.PullStopDistance,
                wireSettings.Width,
                wireSettings.HitRadius,
                wireSettings.Color);
            try
            {
                yield return HackerMeleeAttackAction.Wait(context, wireTravelSeconds);
                yield return HackerMeleeAttackAction.Wait(context, grabSeconds);
            }
            finally
            {
                if (wire != null)
                {
                    wire.BeginDissolve();
                }
            }
        }

        private void FireProjectile(BossActionContext context)
        {
            BossProjectileSettings settings = context.ResolveGraphProjectileSettings(projectileName) ?? projectile;
            if (settings?.Prefab == null)
            {
                return;
            }

            Vector3 origin = context.GetBossChildPosition(fireOriginPath);
            context.FireProjectile(
                settings,
                origin,
                context.GetDirectionToPlayer(origin),
                0f,
                projectileName: projectileName);
        }
    }
}
