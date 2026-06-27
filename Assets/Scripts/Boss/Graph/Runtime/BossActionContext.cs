using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Combat;

namespace Week14.Enemy
{
    public sealed class BossActionContext
    {
        private readonly Action stop;
        private readonly Func<bool> isExecutionPaused;
        private Animator animator;
        private BossAnimationEventBridge animationEventBridge;
        private bool hasBodyRootLocalBase;
        private bool hasPlayerRelativeMoveIntent;
        private float playerRelativeMoveDirectionSign = 1f;
        private float playerRelativeMoveSpeedMultiplier = 1f;
        private float playerRelativeMoveElapsedSeconds;
        private float playerRelativeMoveDurationSeconds;
        private AnimationCurve playerRelativeMoveSpeedCurve;
        private Vector3 bodyRootLocalBase;
        private readonly Dictionary<Transform, Vector3> transformBaseScales = new();
        private readonly Dictionary<string, BossChildAimState> bossChildAimStates = new();
        private readonly Dictionary<string, string> bossChildAimStartNodePaths = new();
        private readonly Dictionary<string, EnemyProjectile> projectileHandles = new();
        private string currentNodeId;

        public BossActionContext(
            BossAI boss,
            Action stop,
            Func<bool> isExecutionPaused)
        {
            Boss = boss;
            this.stop = stop;
            this.isExecutionPaused = isExecutionPaused;
        }

        public BossAI Boss { get; }
        public bool IsExecutionPaused => isExecutionPaused?.Invoke() == true;
        public Vector3 OriginPosition
        {
            get
            {
                if (Boss == null)
                {
                    return Vector3.zero;
                }

                return Boss.BodyRoot != null ? Boss.BodyRoot.position : Boss.transform.position;
            }
        }

        public void Stop()
        {
            UpdateBossChildAims();
            stop?.Invoke();
        }

        public void SetCurrentNodeId(string nodeId)
        {
            currentNodeId = nodeId;
        }

        public void PlayAnimationTrigger(string triggerName)
        {
            if (string.IsNullOrWhiteSpace(triggerName))
            {
                return;
            }

            Animator targetAnimator = GetAnimator();
            if (targetAnimator != null)
            {
                targetAnimator.SetTrigger(triggerName);
            }
        }

        public void PlayAnimationState(string stateName, int layer, float normalizedTime)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            Animator targetAnimator = GetAnimator();
            if (targetAnimator != null)
            {
                targetAnimator.Play(stateName, layer, normalizedTime);
            }
        }

        public IEnumerator WaitForAnimationEvent(string eventId, float timeoutSeconds)
        {
            BossAnimationEventBridge bridge = GetAnimationEventBridge();
            if (bridge == null || string.IsNullOrWhiteSpace(eventId))
            {
                yield break;
            }

            int startVersion = bridge.GetVersion(eventId);
            float remaining = timeoutSeconds;
            while (bridge.GetVersion(eventId) <= startVersion)
            {
                if (IsExecutionPaused)
                {
                    Stop();
                    yield return null;
                    continue;
                }

                if (timeoutSeconds > 0f)
                {
                    remaining -= Time.deltaTime;
                    if (remaining <= 0f)
                    {
                        yield break;
                    }
                }

                UpdateContinuousActions();
                yield return null;
            }
        }

        public void MoveTowardPlayer(float speedMultiplier)
        {
            UpdateBossChildAims();
            ApplyPlayerRelativeMove(1f, speedMultiplier);
        }

        public void MoveTowardPlayer(AnimationCurve speedCurve, float elapsedSeconds)
        {
            MoveTowardPlayer(1f, speedCurve, elapsedSeconds, 0f);
        }

        public void MoveTowardPlayer(
            float speedMultiplier,
            AnimationCurve speedCurve,
            float elapsedSeconds,
            float durationSeconds)
        {
            UpdateBossChildAims();
            ApplyPlayerRelativeMove(1f, speedMultiplier, speedCurve, elapsedSeconds, durationSeconds);
        }

        public void MaintainPlayerDistance(
            float targetDistance,
            float tolerance,
            float speedMultiplier,
            AnimationCurve speedCurve,
            float elapsedSeconds,
            float durationSeconds)
        {
            UpdateBossChildAims();
            ApplyMaintainPlayerDistance(
                targetDistance,
                tolerance,
                Mathf.Max(0f, speedMultiplier) * EvaluateMoveSpeedCurve(speedCurve, elapsedSeconds, durationSeconds));
        }

        public void StartMoveTowardPlayer(float speedMultiplier)
        {
            StartPlayerRelativeMove(1f, speedMultiplier, BossMoveSpeedCurve.CreateConstant(), 0f);
        }

        public void StartMoveTowardPlayer(AnimationCurve speedCurve)
        {
            StartMoveTowardPlayer(1f, speedCurve, 0f);
        }

        public void StartMoveTowardPlayer(float speedMultiplier, AnimationCurve speedCurve, float durationSeconds)
        {
            StartPlayerRelativeMove(1f, speedMultiplier, speedCurve, durationSeconds);
        }

        public void StartMoveAwayFromPlayer(AnimationCurve speedCurve)
        {
            StartMoveAwayFromPlayer(1f, speedCurve, 0f);
        }

        public void StartMoveAwayFromPlayer(float speedMultiplier, AnimationCurve speedCurve, float durationSeconds)
        {
            StartPlayerRelativeMove(-1f, speedMultiplier, speedCurve, durationSeconds);
        }

        public void StopMoveTowardPlayer()
        {
            ClearPlayerRelativeMove();
            Stop();
        }

        private void UpdateContinuousActions()
        {
            UpdateBossChildAims();
            if (hasPlayerRelativeMoveIntent)
            {
                ApplyPlayerRelativeMove(
                    playerRelativeMoveDirectionSign,
                    playerRelativeMoveSpeedMultiplier,
                    playerRelativeMoveSpeedCurve,
                    playerRelativeMoveElapsedSeconds,
                    playerRelativeMoveDurationSeconds);
                playerRelativeMoveElapsedSeconds += Time.deltaTime;
                if (playerRelativeMoveDurationSeconds > 0f
                    && playerRelativeMoveElapsedSeconds >= playerRelativeMoveDurationSeconds)
                {
                    ClearPlayerRelativeMove();
                    Stop();
                }
            }
        }

        private void StartPlayerRelativeMove(
            float directionSign,
            float speedMultiplier,
            AnimationCurve speedCurve,
            float durationSeconds)
        {
            hasPlayerRelativeMoveIntent = true;
            playerRelativeMoveDirectionSign = directionSign < 0f ? -1f : 1f;
            playerRelativeMoveSpeedMultiplier = Mathf.Max(0f, speedMultiplier);
            playerRelativeMoveElapsedSeconds = 0f;
            playerRelativeMoveDurationSeconds = Mathf.Max(0f, durationSeconds);
            playerRelativeMoveSpeedCurve = speedCurve;
            ApplyPlayerRelativeMove(
                playerRelativeMoveDirectionSign,
                playerRelativeMoveSpeedMultiplier,
                speedCurve,
                0f,
                playerRelativeMoveDurationSeconds);
        }

        private void ClearPlayerRelativeMove()
        {
            hasPlayerRelativeMoveIntent = false;
            playerRelativeMoveDirectionSign = 1f;
            playerRelativeMoveSpeedMultiplier = 1f;
            playerRelativeMoveElapsedSeconds = 0f;
            playerRelativeMoveDurationSeconds = 0f;
            playerRelativeMoveSpeedCurve = null;
        }

        private void ApplyPlayerRelativeMove(
            float directionSign,
            float speedMultiplier,
            AnimationCurve speedCurve,
            float elapsedSeconds,
            float durationSeconds)
        {
            ApplyPlayerRelativeMove(
                directionSign,
                Mathf.Max(0f, speedMultiplier) * EvaluateMoveSpeedCurve(speedCurve, elapsedSeconds, durationSeconds));
        }

        private void ApplyPlayerRelativeMove(float directionSign, float speedMultiplier)
        {
            if (Boss == null || Boss.Body == null || Boss.Player == null)
            {
                return;
            }

            Vector2 direction = (Vector2)Boss.Player.position - (Vector2)Boss.transform.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                Stop();
                return;
            }

            Boss.Body.linearVelocity = direction.normalized
                * (directionSign < 0f ? -1f : 1f)
                * (Boss.MoveSpeed * Mathf.Max(0f, speedMultiplier));
        }

        private void ApplyMaintainPlayerDistance(float targetDistance, float tolerance, float speedMultiplier)
        {
            if (Boss == null || Boss.Body == null || Boss.Player == null)
            {
                return;
            }

            Vector2 toPlayer = (Vector2)Boss.Player.position - (Vector2)Boss.transform.position;
            float currentDistance = toPlayer.magnitude;
            if (currentDistance <= 0.0001f)
            {
                Stop();
                return;
            }

            float safeDistance = Mathf.Max(0.1f, targetDistance);
            float safeTolerance = Mathf.Max(0f, tolerance);
            float distanceDelta = currentDistance - safeDistance;
            float absoluteDelta = Mathf.Abs(distanceDelta);
            if (absoluteDelta <= 0.01f)
            {
                Stop();
                return;
            }

            float directionSign = distanceDelta > 0f ? 1f : -1f;
            float smoothingRange = Mathf.Max(0.01f, safeTolerance);
            float speedScale = Mathf.Clamp01(absoluteDelta / smoothingRange);
            Boss.Body.linearVelocity = toPlayer.normalized
                * directionSign
                * (Boss.MoveSpeed * Mathf.Max(0f, speedMultiplier) * speedScale);
        }

        private static float EvaluateMoveSpeedCurve(
            AnimationCurve speedCurve,
            float elapsedSeconds,
            float durationSeconds)
        {
            if (speedCurve == null || speedCurve.length == 0)
            {
                return 1f;
            }

            float normalizedTime = durationSeconds > 0f
                ? Mathf.Clamp01(Mathf.Max(0f, elapsedSeconds) / durationSeconds)
                : Mathf.Max(0f, elapsedSeconds);
            return Mathf.Max(0f, speedCurve.Evaluate(normalizedTime));
        }

        public IEnumerator MoveBodyRootLocalOffset(Vector3 targetLocalOffset, float seconds, bool releaseBaseAfterMove)
        {
            Transform target = Boss != null ? Boss.BodyRoot : null;
            if (target == null || Boss == null || target == Boss.transform)
            {
                yield break;
            }

            if (!hasBodyRootLocalBase)
            {
                bodyRootLocalBase = target.localPosition;
                hasBodyRootLocalBase = true;
            }

            Vector3 from = target.localPosition;
            Vector3 to = bodyRootLocalBase + targetLocalOffset;
            float duration = Mathf.Max(0.01f, seconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (target == null)
                {
                    yield break;
                }

                if (IsExecutionPaused)
                {
                    Stop();
                    yield return null;
                    continue;
                }

                Stop();
                elapsed += Time.deltaTime;
                target.localPosition = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            if (target != null)
            {
                target.localPosition = to;
            }

            if (releaseBaseAfterMove)
            {
                ResetBodyRootLocalOffset();
            }
        }

        public void ResetBodyRootLocalOffset()
        {
            if (!hasBodyRootLocalBase)
            {
                return;
            }

            Transform target = Boss != null ? Boss.BodyRoot : null;
            if (target != null)
            {
                target.localPosition = bodyRootLocalBase;
            }

            hasBodyRootLocalBase = false;
        }

        public Vector3 GetBossChildPosition(string childPath)
        {
            UpdateBossChildAims();
            Transform target = FindBossChild(childPath);
            return target != null ? target.position : OriginPosition;
        }

        public Transform GetBossChildTransform(string childPath)
        {
            UpdateBossChildAims();
            return FindBossChild(childPath);
        }

        public void SetBossChildActive(string childPath, bool active)
        {
            Transform target = FindBossChild(childPath);
            if (target != null)
            {
                target.gameObject.SetActive(active);
            }
        }

        public void RotateBossChildRight(string childPath, Vector2 direction, bool flipYByFacing)
        {
            Transform target = FindBossChild(childPath);
            if (target == null || direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            if (!transformBaseScales.TryGetValue(target, out Vector3 baseLocalScale))
            {
                baseLocalScale = target.localScale;
                transformBaseScales[target] = baseLocalScale;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            target.rotation = Quaternion.Euler(0f, 0f, angle);

            if (!flipYByFacing)
            {
                return;
            }

            bool flipY = angle < -90f || angle > 90f;
            Vector3 nextScale = baseLocalScale;
            float authoredSign = baseLocalScale.y < 0f ? -1f : 1f;
            nextScale.y = Mathf.Abs(baseLocalScale.y) * authoredSign * (flipY ? -1f : 1f);
            target.localScale = nextScale;
        }

        public void StartBossChildAimAtPlayer(
            string childPath,
            bool activateOnStart,
            bool flipYByFacing,
            bool deactivateOnPatternEnd)
        {
            if (string.IsNullOrWhiteSpace(childPath))
            {
                return;
            }

            if (activateOnStart)
            {
                SetBossChildActive(childPath, true);
            }

            bossChildAimStates[childPath] = new BossChildAimState(childPath, flipYByFacing, deactivateOnPatternEnd);
            if (!string.IsNullOrWhiteSpace(currentNodeId))
            {
                bossChildAimStartNodePaths[currentNodeId] = childPath;
            }

            UpdateBossChildAim(childPath, flipYByFacing);
        }

        public bool StopBossChildAimAtPlayerStartedByNode(string startNodeId, bool deactivate)
        {
            if (string.IsNullOrWhiteSpace(startNodeId)
                || !bossChildAimStartNodePaths.TryGetValue(startNodeId, out string childPath))
            {
                return false;
            }

            StopBossChildAimAtPlayer(childPath, deactivate);
            return true;
        }

        public void StopBossChildAimAtPlayer(string childPath, bool deactivate)
        {
            if (string.IsNullOrWhiteSpace(childPath))
            {
                return;
            }

            bossChildAimStates.Remove(childPath);
            RemoveBossChildAimStartNodePaths(childPath);
            if (deactivate)
            {
                SetBossChildActive(childPath, false);
            }
        }

        public void ClearPatternScopedBossChildAims()
        {
            ClearPlayerRelativeMove();
            projectileHandles.Clear();
            if (bossChildAimStates.Count == 0)
            {
                bossChildAimStartNodePaths.Clear();
                return;
            }

            List<BossChildAimState> states = new(bossChildAimStates.Values);
            bossChildAimStates.Clear();
            bossChildAimStartNodePaths.Clear();
            for (int i = 0; i < states.Count; i++)
            {
                BossChildAimState state = states[i];
                if (state.DeactivateOnPatternEnd)
                {
                    SetBossChildActive(state.ChildPath, false);
                }
            }
        }

        public void UpdateBossChildAims()
        {
            if (bossChildAimStates.Count == 0)
            {
                return;
            }

            foreach (BossChildAimState state in bossChildAimStates.Values)
            {
                UpdateBossChildAim(state.ChildPath, state.FlipYByFacing);
            }
        }

        private void RemoveBossChildAimStartNodePaths(string childPath)
        {
            if (bossChildAimStartNodePaths.Count == 0)
            {
                return;
            }

            List<string> startNodeIds = new();
            foreach (KeyValuePair<string, string> pair in bossChildAimStartNodePaths)
            {
                if (pair.Value == childPath)
                {
                    startNodeIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < startNodeIds.Count; i++)
            {
                bossChildAimStartNodePaths.Remove(startNodeIds[i]);
            }
        }

        public Vector2 GetDirectionToPlayer(Vector3 origin)
        {
            if (Boss == null || Boss.Player == null)
            {
                return Vector2.left;
            }

            Vector2 direction = (Vector2)Boss.Player.position - (Vector2)origin;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
        }

        public static Vector2 AngleToDirection(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        public EnemyProjectile FireProjectile(
            BossProjectileSettings projectileSettings,
            Vector3 origin,
            Vector2 direction,
            float muzzleFlashScale,
            bool? aimAtPlayerWhileChargingOverride = null,
            bool? aimAtPlayerOnLaunchOverride = null,
            float chargeSecondsOverride = -1f,
            float radiusOverride = -1f,
            bool suppressHoming = false,
            string projectileName = null)
        {
            UpdateBossChildAims();
            BossProjectileSettings resolvedSettings = !string.IsNullOrWhiteSpace(projectileName)
                ? ResolveGraphProjectileSettings(projectileName)
                : ResolveGraphProjectileSettings(null) ?? projectileSettings;
            if (Boss == null || resolvedSettings == null || direction.sqrMagnitude <= 0.0001f)
            {
                return null;
            }

            return Boss.FireGraphProjectile(
                resolvedSettings,
                origin,
                direction.normalized,
                muzzleFlashScale,
                aimAtPlayerWhileChargingOverride,
                aimAtPlayerOnLaunchOverride,
                chargeSecondsOverride,
                radiusOverride,
                suppressHoming);
        }

        public BossProjectileSettings ResolveGraphProjectileSettings(string projectileName)
        {
            return Boss != null ? Boss.ResolveGraphProjectileSettingsForActions(projectileName) : null;
        }

        public void SetProjectileHandle(string handleKey, EnemyProjectile projectile)
        {
            if (string.IsNullOrWhiteSpace(handleKey) || projectile == null)
            {
                return;
            }

            projectileHandles[handleKey] = projectile;
        }

        public EnemyProjectile GetProjectileHandle(string handleKey)
        {
            if (string.IsNullOrWhiteSpace(handleKey)
                || !projectileHandles.TryGetValue(handleKey, out EnemyProjectile projectile))
            {
                return null;
            }

            if (projectile != null)
            {
                return projectile;
            }

            projectileHandles.Remove(handleKey);
            return null;
        }

        public void PlaySfx(string sfxId)
        {
            if (string.IsNullOrWhiteSpace(sfxId))
            {
                return;
            }

            SoundManager.PlaySfx(sfxId);
        }

        public void PlaySfxOnLaunch(EnemyProjectile projectile, string sfxId)
        {
            if (projectile == null || string.IsNullOrWhiteSpace(sfxId))
            {
                return;
            }

            void HandleLaunched(EnemyProjectile launchedProjectile)
            {
                launchedProjectile.Launched -= HandleLaunched;
                PlaySfx(sfxId);
            }

            projectile.Launched += HandleLaunched;
        }

        public void PlaySfxOnRadialSplitImminent(EnemyProjectile projectile, string sfxId)
        {
            if (projectile == null || string.IsNullOrWhiteSpace(sfxId))
            {
                return;
            }

            void HandleRadialSplitImminent(EnemyProjectile splitProjectile)
            {
                splitProjectile.RadialSplitImminent -= HandleRadialSplitImminent;
                PlaySfx(sfxId);
            }

            projectile.RadialSplitImminent += HandleRadialSplitImminent;
        }

        public void PlayOriginBurst(BossGraphEffectSettings effects, Vector3 position)
        {
            PlayExplosionIfEnabled(effects, position);
            PlaySmokeIfEnabled(effects, position);
        }

        public void PlaySmokeIfDue(ref float nextSmokeAt, BossGraphEffectSettings effects, Vector3 position)
        {
            if (effects == null || Time.time < nextSmokeAt)
            {
                return;
            }

            PlaySmokeIfEnabled(effects, position);
            nextSmokeAt = Time.time + Mathf.Max(0.01f, effects.SmokeInterval);
        }

        public void PlayMuzzleFlashIfEnabled(BossGraphEffectSettings effects, Vector3 origin, Vector2 direction)
        {
            BossGraphParticleEffectSettings muzzleFlash = effects?.MuzzleFlash;
            if (muzzleFlash == null || !muzzleFlash.Enabled)
            {
                return;
            }

            ProjectileVfx.PlayMuzzleFlash(origin, direction, muzzleFlash.Color, muzzleFlash.Scale);
        }

        public void PlayCameraShakeIfEnabled(BossGraphEffectSettings effects, Vector2 direction)
        {
            BossGraphCameraShakeSettings shake = effects?.CameraShake;
            if (shake == null || !shake.Enabled)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            CameraFollow2D cameraFollow = mainCamera.GetComponent<CameraFollow2D>();
            cameraFollow?.PlayImpact(direction, shake.Seconds, shake.Distance, shake.Frequency);
        }

        public void SendCustomEvent(string methodName, bool broadcastToChildren)
        {
            if (Boss == null || string.IsNullOrWhiteSpace(methodName))
            {
                return;
            }

            if (broadcastToChildren)
            {
                Boss.gameObject.BroadcastMessage(methodName, SendMessageOptions.DontRequireReceiver);
                return;
            }

            Boss.gameObject.SendMessage(methodName, SendMessageOptions.DontRequireReceiver);
        }

        public GameObject SpawnPrefab(GameObject prefab, Vector3 offset, Vector3 rotationEuler, bool parentToBoss)
        {
            if (prefab == null)
            {
                return null;
            }

            Transform parent = parentToBoss && Boss != null ? Boss.transform : null;
            return UnityEngine.Object.Instantiate(
                prefab,
                OriginPosition + offset,
                Quaternion.Euler(rotationEuler),
                parent);
        }

        public IEnumerator WaitSeconds(float seconds)
        {
            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                if (IsExecutionPaused)
                {
                    Stop();
                    yield return null;
                    continue;
                }

                UpdateContinuousActions();
                remaining -= Time.deltaTime;
                yield return null;
            }
        }

        private Animator GetAnimator()
        {
            if (animator != null)
            {
                return animator;
            }

            if (Boss == null)
            {
                return null;
            }

            animator = Boss.BodyRoot != null
                ? Boss.BodyRoot.GetComponentInChildren<Animator>(true)
                : Boss.GetComponentInChildren<Animator>(true);
            return animator;
        }

        private BossAnimationEventBridge GetAnimationEventBridge()
        {
            if (animationEventBridge != null)
            {
                return animationEventBridge;
            }

            Animator targetAnimator = GetAnimator();
            GameObject targetObject = targetAnimator != null
                ? targetAnimator.gameObject
                : Boss != null ? Boss.gameObject : null;
            if (targetObject == null)
            {
                return null;
            }

            animationEventBridge = targetObject.GetComponent<BossAnimationEventBridge>();
            if (animationEventBridge == null)
            {
                animationEventBridge = targetObject.AddComponent<BossAnimationEventBridge>();
            }

            return animationEventBridge;
        }

        private static void PlayExplosionIfEnabled(BossGraphEffectSettings effects, Vector3 position)
        {
            BossGraphParticleEffectSettings explosion = effects?.Explosion;
            if (explosion == null || !explosion.Enabled)
            {
                return;
            }

            ProjectileVfx.PlayHogExplosion(position, explosion.Color, explosion.Scale, explosion.Count);
        }

        private static void PlaySmokeIfEnabled(BossGraphEffectSettings effects, Vector3 position)
        {
            BossGraphParticleEffectSettings smoke = effects?.Smoke;
            if (smoke == null || !smoke.Enabled)
            {
                return;
            }

            ProjectileVfx.PlayHogSmokeBurst(position, smoke.Color, smoke.Scale, smoke.Count);
        }

        private Transform FindBossChild(string childPath)
        {
            if (Boss == null || string.IsNullOrWhiteSpace(childPath))
            {
                return null;
            }

            Transform found = Boss.transform.Find(childPath);
            return found != null ? found : FindChildRecursive(Boss.transform, childPath);
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void UpdateBossChildAim(string childPath, bool flipYByFacing)
        {
            Transform target = FindBossChild(childPath);
            if (target == null)
            {
                return;
            }

            RotateBossChildRight(childPath, GetDirectionToPlayer(target.position), flipYByFacing);
        }

        private readonly struct BossChildAimState
        {
            public BossChildAimState(string childPath, bool flipYByFacing, bool deactivateOnPatternEnd)
            {
                ChildPath = childPath;
                FlipYByFacing = flipYByFacing;
                DeactivateOnPatternEnd = deactivateOnPatternEnd;
            }

            public string ChildPath { get; }
            public bool FlipYByFacing { get; }
            public bool DeactivateOnPatternEnd { get; }
        }
    }
}
