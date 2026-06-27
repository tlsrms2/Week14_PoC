using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    internal sealed class MinionPatternContext
    {
        internal delegate EnemyProjectile BossProjectileFire(
            BossProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            Vector3 muzzleOrigin);

        private readonly IMinionOwner owner;
        private readonly MinionSummonSettings summon;
        private readonly BossPatternMovement patternMovement;
        private readonly List<Minion> controlledMinions;
        private readonly List<Minion> spawnedMinions;
        private readonly Func<bool> isBossExecutionPaused;
        private readonly Action stopBoss;
        private readonly Func<Vector3> getProjectileOrigin;
        private readonly Func<Vector3, Vector2> getDirectionToPlayer;
        private readonly BossProjectileFire fireBossProjectile;

        internal MinionPatternContext(
            IMinionOwner owner,
            MinionSummonSettings summon,
            BossPatternMovement patternMovement,
            List<Minion> controlledMinions,
            List<Minion> spawnedMinions,
            Func<bool> isBossExecutionPaused,
            Action stopBoss,
            Func<Vector3> getProjectileOrigin,
            Func<Vector3, Vector2> getDirectionToPlayer,
            BossProjectileFire fireBossProjectile)
        {
            this.owner = owner;
            this.summon = summon;
            this.patternMovement = patternMovement;
            this.controlledMinions = controlledMinions;
            this.spawnedMinions = spawnedMinions;
            this.isBossExecutionPaused = isBossExecutionPaused;
            this.stopBoss = stopBoss;
            this.getProjectileOrigin = getProjectileOrigin;
            this.getDirectionToPlayer = getDirectionToPlayer;
            this.fireBossProjectile = fireBossProjectile;
        }

        internal int ControlledMinionCount => controlledMinions.Count;

        internal IEnumerator WaitWhileExecutionPaused()
        {
            yield return patternMovement.WaitWhileExecutionPaused(isBossExecutionPaused, stopBoss);
        }

        internal IEnumerator WaitStoppedSeconds(float seconds)
        {
            yield return patternMovement.WaitStoppedSeconds(seconds, isBossExecutionPaused, stopBoss);
        }

        internal IEnumerator WaitForSummonIntros(IReadOnlyList<Minion> minions, bool stopBossWhileWaiting)
        {
            while (HasSummoningMinion(minions))
            {
                if (stopBossWhileWaiting)
                {
                    stopBoss();
                }

                yield return null;
            }
        }

        internal IEnumerator WaitPatternSeconds(float seconds)
        {
            yield return patternMovement.WaitStoppedSeconds(seconds, isBossExecutionPaused, stopBoss);
        }

        internal IEnumerator WaitMinionPatternSeconds(float seconds)
        {
            yield return patternMovement.WaitSeconds(seconds, null, isBossExecutionPaused, stopBoss);
        }

        internal Vector3 GetProjectileOrigin()
        {
            return getProjectileOrigin();
        }

        internal Vector2 GetDirectionToPlayer(Vector3 origin)
        {
            return getDirectionToPlayer(origin);
        }

        internal EnemyProjectile FireBossProjectile(
            BossProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            Vector3 muzzleOrigin)
        {
            return fireBossProjectile(settings, origin, direction, muzzleOrigin);
        }

        internal void StopBoss()
        {
            stopBoss();
        }

        internal Minion SpawnMinion(int index, int totalCount)
        {
            float angle = totalCount <= 0 ? UnityEngine.Random.Range(0f, 360f) : 360f * index / Mathf.Max(1, totalCount);
            Transform ownerTransform = owner?.MinionOwnerTransform;
            Vector3 startPosition = ownerTransform != null ? ownerTransform.position : Vector3.zero;
            Vector3 position = startPosition + (Vector3)(AngleToDirection(angle) * Mathf.Max(0f, summon.SpawnRadius));
            Minion minion = UnityEngine.Object.Instantiate(summon.Prefab, startPosition, Quaternion.identity);
            if (minion == null)
            {
                return null;
            }

            minion.SetOwner(owner);
            minion.BeginSummonIntro(startPosition, position, summon.IntroSeconds, summon.IntroStartScale);
            if (!controlledMinions.Contains(minion))
            {
                controlledMinions.Add(minion);
            }

            if (!spawnedMinions.Contains(minion))
            {
                spawnedMinions.Add(minion);
            }

            return minion;
        }

        internal List<Minion> GetControlledMinions()
        {
            RefreshControlledMinions();
            return controlledMinions;
        }

        internal void RefreshControlledMinions()
        {
            for (int i = controlledMinions.Count - 1; i >= 0; i--)
            {
                if (controlledMinions[i] == null || controlledMinions[i].Owner != owner)
                {
                    controlledMinions.RemoveAt(i);
                }
            }

            IReadOnlyList<Minion> allMinions = Minion.All;
            for (int i = 0; i < allMinions.Count; i++)
            {
                Minion minion = allMinions[i];
                if (minion == null)
                {
                    continue;
                }

                bool canClaim = minion.Owner == owner || (summon.ClaimSceneMinions && minion.Owner == null);
                if (!canClaim)
                {
                    continue;
                }

                minion.SetOwner(owner);
                if (!controlledMinions.Contains(minion))
                {
                    controlledMinions.Add(minion);
                }
            }
        }

        internal bool EnsureAnyMinion(List<Minion> minions)
        {
            return minions != null && minions.Count > 0;
        }

        internal MinionGraphProjectileFireSpec ResolveSharedMinionAim(
            MinionGraphProjectileFireSpec fireSpec,
            IReadOnlyList<Minion> minions)
        {
            if (!fireSpec.UsesClosestMinionAim || minions == null || minions.Count == 0)
            {
                return fireSpec;
            }

            return fireSpec.WithSharedMinionAimDirectionProvider(() => GetClosestMinionAimDirection(fireSpec, minions));
        }

        private Vector2 GetClosestMinionAimDirection(
            MinionGraphProjectileFireSpec fireSpec,
            IReadOnlyList<Minion> minions)
        {
            Minion closestMinion = FindClosestMinionToTarget(minions);
            if (closestMinion == null)
            {
                return Vector2.zero;
            }

            Vector3 aimOrigin = fireSpec.GetAimOrigin(closestMinion, 0);
            return closestMinion.GetGraphDirectionToPlayer(aimOrigin);
        }

        private Minion FindClosestMinionToTarget(IReadOnlyList<Minion> minions)
        {
            Transform target = owner?.MinionTarget;
            if (target == null)
            {
                return null;
            }

            Minion closest = null;
            float closestSqrDistance = float.PositiveInfinity;
            Vector2 targetPosition = target.position;
            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion == null)
                {
                    continue;
                }

                float sqrDistance = ((Vector2)minion.transform.position - targetPosition).sqrMagnitude;
                if (sqrDistance >= closestSqrDistance)
                {
                    continue;
                }

                closest = minion;
                closestSqrDistance = sqrDistance;
            }

            return closest;
        }

        internal void StopAllMinions()
        {
            List<Minion> minions = GetControlledMinions();
            for (int i = 0; i < minions.Count; i++)
            {
                minions[i]?.StopCommand();
            }
        }

        internal void ResumeAllMinions()
        {
            List<Minion> minions = GetControlledMinions();
            for (int i = 0; i < minions.Count; i++)
            {
                minions[i]?.ResumeIdle();
            }
        }

        internal void ReleaseMinions()
        {
            for (int i = controlledMinions.Count - 1; i >= 0; i--)
            {
                if (controlledMinions[i] != null)
                {
                    controlledMinions[i].ClearOwner(owner);
                }
            }

            controlledMinions.Clear();
        }

        internal void KillSpawnedMinions()
        {
            for (int i = spawnedMinions.Count - 1; i >= 0; i--)
            {
                Minion minion = spawnedMinions[i];
                if (minion != null && minion.Health != null && !minion.Health.IsDead)
                {
                    minion.Health.Kill();
                }
            }

            spawnedMinions.Clear();
        }

        internal float GetFormationAngle(int index, float spacingDegrees)
        {
            if (index <= 0)
            {
                return 0f;
            }

            int ring = (index + 1) / 2;
            float sign = index % 2 == 1 ? 1f : -1f;
            return sign * ring * Mathf.Max(1f, spacingDegrees);
        }

        internal float GetSideBySideFormationAngle(int index, float spacingDegrees)
        {
            int ring = Mathf.Max(0, index) / 2 + 1;
            float sign = index % 2 == 0 ? 1f : -1f;
            return sign * ring * Mathf.Max(1f, spacingDegrees);
        }

        internal float GetAlternatingOffset(int index, float spacing)
        {
            if (index <= 0 || spacing <= 0f)
            {
                return 0f;
            }

            int ring = (index + 1) / 2;
            float sign = index % 2 == 0 ? -1f : 1f;
            return ring * spacing * sign;
        }

        private static Vector2 AngleToDirection(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static bool HasSummoningMinion(IReadOnlyList<Minion> minions)
        {
            if (minions == null)
            {
                return false;
            }

            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion != null && minion.IsSummoning)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
