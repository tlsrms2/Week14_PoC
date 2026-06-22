using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    internal sealed class DronePatternContext
    {
        internal delegate EnemyProjectile BossProjectileFire(
            DronePilot.ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            Vector3 muzzleOrigin);

        private readonly DronePilot pilot;
        private readonly DronePilot.SummonSettings summon;
        private readonly BossPatternMovement patternMovement;
        private readonly List<Drone> controlledDrones;
        private readonly List<Drone> spawnedDrones;
        private readonly Func<bool> isBossExecutionPaused;
        private readonly Action stopBoss;
        private readonly Func<Vector3> getProjectileOrigin;
        private readonly Func<Vector3, Vector2> getDirectionToPlayer;
        private readonly BossProjectileFire fireBossProjectile;
        private DronePilot.ProjectileSettings synchronizedDroneProjectile;
        private int synchronizedDroneShotsRemaining;
        private int synchronizedDroneSyncVersion;

        internal DronePatternContext(
            DronePilot pilot,
            DronePilot.SummonSettings summon,
            BossPatternMovement patternMovement,
            List<Drone> controlledDrones,
            List<Drone> spawnedDrones,
            Func<bool> isBossExecutionPaused,
            Action stopBoss,
            Func<Vector3> getProjectileOrigin,
            Func<Vector3, Vector2> getDirectionToPlayer,
            BossProjectileFire fireBossProjectile)
        {
            this.pilot = pilot;
            this.summon = summon;
            this.patternMovement = patternMovement;
            this.controlledDrones = controlledDrones;
            this.spawnedDrones = spawnedDrones;
            this.isBossExecutionPaused = isBossExecutionPaused;
            this.stopBoss = stopBoss;
            this.getProjectileOrigin = getProjectileOrigin;
            this.getDirectionToPlayer = getDirectionToPlayer;
            this.fireBossProjectile = fireBossProjectile;
        }

        internal int ControlledDroneCount => controlledDrones.Count;

        internal IEnumerator WaitWhileExecutionPaused()
        {
            yield return patternMovement.WaitWhileExecutionPaused(isBossExecutionPaused, stopBoss);
        }

        internal IEnumerator WaitStoppedSeconds(float seconds)
        {
            yield return patternMovement.WaitStoppedSeconds(seconds, isBossExecutionPaused, stopBoss);
        }

        internal IEnumerator WaitPatternSeconds(float seconds)
        {
            yield return patternMovement.WaitStoppedSeconds(seconds, isBossExecutionPaused, stopBoss);
        }

        internal IEnumerator WaitDronePatternSeconds(float seconds)
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
            DronePilot.ProjectileSettings settings,
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

        internal float SpawnDrone(int index, int totalCount)
        {
            float angle = totalCount <= 0 ? UnityEngine.Random.Range(0f, 360f) : 360f * index / Mathf.Max(1, totalCount);
            Vector3 startPosition = pilot.transform.position;
            Vector3 position = startPosition + (Vector3)(AngleToDirection(angle) * Mathf.Max(0f, summon.SpawnRadius));
            Drone drone = UnityEngine.Object.Instantiate(summon.Prefab, startPosition, Quaternion.identity);
            if (drone == null)
            {
                return 0f;
            }

            drone.SetOwner(pilot);
            float introSeconds = drone.BeginSummonIntro(startPosition, position, summon.IntroSeconds, summon.IntroStartScale);
            if (!controlledDrones.Contains(drone))
            {
                controlledDrones.Add(drone);
            }

            if (!spawnedDrones.Contains(drone))
            {
                spawnedDrones.Add(drone);
            }

            return introSeconds;
        }

        internal List<Drone> GetControlledDrones()
        {
            RefreshControlledDrones();
            return controlledDrones;
        }

        internal void RefreshControlledDrones()
        {
            for (int i = controlledDrones.Count - 1; i >= 0; i--)
            {
                if (controlledDrones[i] == null || controlledDrones[i].Owner != pilot)
                {
                    controlledDrones.RemoveAt(i);
                }
            }

            IReadOnlyList<Drone> allDrones = Drone.All;
            for (int i = 0; i < allDrones.Count; i++)
            {
                Drone drone = allDrones[i];
                if (drone == null)
                {
                    continue;
                }

                bool canClaim = drone.Owner == pilot || (summon.ClaimSceneDrones && drone.Owner == null);
                if (!canClaim)
                {
                    continue;
                }

                drone.SetOwner(pilot);
                if (!controlledDrones.Contains(drone))
                {
                    controlledDrones.Add(drone);
                }
            }
        }

        internal bool EnsureAnyDrone(List<Drone> drones)
        {
            return drones != null && drones.Count > 0;
        }

        internal void FireAllDrones(DronePilot.ProjectileSettings projectile)
        {
            if (projectile == null)
            {
                return;
            }

            List<Drone> drones = GetControlledDrones();
            for (int i = 0; i < drones.Count; i++)
            {
                drones[i]?.FireOnceAtPlayer(projectile);
            }
        }

        internal int BeginSynchronizedDroneFire(DronePilot.ProjectileSettings projectile, int shotCount)
        {
            synchronizedDroneProjectile = projectile;
            synchronizedDroneShotsRemaining = projectile != null ? Mathf.Max(0, shotCount) : 0;
            synchronizedDroneSyncVersion++;
            return synchronizedDroneSyncVersion;
        }

        internal IEnumerator WaitSynchronizedDroneFire(int syncVersion)
        {
            while (syncVersion == synchronizedDroneSyncVersion && synchronizedDroneShotsRemaining > 0)
            {
                if (isBossExecutionPaused())
                {
                    stopBoss();
                    yield return null;
                    continue;
                }

                yield return null;
            }
        }

        internal void TryFireSynchronizedDrones()
        {
            if (synchronizedDroneProjectile == null || synchronizedDroneShotsRemaining <= 0)
            {
                return;
            }

            FireAllDrones(synchronizedDroneProjectile);
            synchronizedDroneShotsRemaining--;
            if (synchronizedDroneShotsRemaining <= 0)
            {
                ClearSynchronizedDroneFire();
            }
        }

        internal void ClearSynchronizedDroneFire()
        {
            synchronizedDroneProjectile = null;
            synchronizedDroneShotsRemaining = 0;
            synchronizedDroneSyncVersion++;
        }

        internal void StopAllDrones()
        {
            List<Drone> drones = GetControlledDrones();
            for (int i = 0; i < drones.Count; i++)
            {
                drones[i]?.StopCommand();
            }
        }

        internal void ResumeAllDrones()
        {
            List<Drone> drones = GetControlledDrones();
            for (int i = 0; i < drones.Count; i++)
            {
                drones[i]?.ResumeIdle();
            }
        }

        internal void ReleaseDrones()
        {
            for (int i = controlledDrones.Count - 1; i >= 0; i--)
            {
                if (controlledDrones[i] != null)
                {
                    controlledDrones[i].ClearOwner(pilot);
                }
            }

            controlledDrones.Clear();
        }

        internal void KillSpawnedDrones()
        {
            for (int i = spawnedDrones.Count - 1; i >= 0; i--)
            {
                Drone drone = spawnedDrones[i];
                if (drone != null && drone.Health != null && !drone.Health.IsDead)
                {
                    drone.Health.Kill();
                }
            }

            spawnedDrones.Clear();
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
    }
}
