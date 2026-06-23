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
        private readonly Func<IEnumerator> applyPendingEnrage;
        private Animator animator;
        private BossAnimationEventBridge animationEventBridge;
        private bool hasBodyRootLocalBase;
        private Vector3 bodyRootLocalBase;
        private readonly Dictionary<Transform, Vector3> transformBaseScales = new();

        public BossActionContext(
            BossAI boss,
            Action stop,
            Func<bool> isExecutionPaused,
            Func<IEnumerator> applyPendingEnrage)
        {
            Boss = boss;
            this.stop = stop;
            this.isExecutionPaused = isExecutionPaused;
            this.applyPendingEnrage = applyPendingEnrage;
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
            stop?.Invoke();
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

                yield return null;
            }
        }

        public void MoveTowardPlayer(float speedMultiplier)
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

            Boss.Body.linearVelocity = direction.normalized * (Boss.MoveSpeed * Mathf.Max(0f, speedMultiplier));
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
            Transform target = FindBossChild(childPath);
            return target != null ? target.position : OriginPosition;
        }

        public Transform GetBossChildTransform(string childPath)
        {
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

        public ProjectileVfx.TelegraphLine CreateProjectileTelegraphLine(string projectileName, float width = 0.055f)
        {
            BossProjectileSettings settings = ResolveGraphProjectileSettings(projectileName);
            Color color = settings != null ? settings.LaunchedColor : new Color(1f, 0.25f, 0.15f, 1f);
            color.a = 0.62f;
            return ProjectileVfx.CreateTelegraphLine(color, width);
        }

        public void SetProjectileTelegraphLine(ProjectileVfx.TelegraphLine line, Vector3 origin, Vector2 direction)
        {
            if (line == null || direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 normalized = direction.normalized;
            line.Set(origin, origin + (Vector3)(normalized * GetProjectileTelegraphLength()));
        }

        public void PlayProjectileTelegraphLine(
            string projectileName,
            Vector3 origin,
            Vector2 direction,
            float seconds = 0.12f,
            float width = 0.045f)
        {
            if (direction.sqrMagnitude <= 0.0001f || seconds <= 0f)
            {
                return;
            }

            ProjectileVfx.TelegraphLine line = CreateProjectileTelegraphLine(projectileName, width);
            SetProjectileTelegraphLine(line, origin, direction);
            line.Destroy(seconds);
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

                remaining -= Time.deltaTime;
                yield return null;
            }
        }

        public IEnumerator ApplyPendingEnrageIfAny()
        {
            if (applyPendingEnrage == null)
            {
                yield break;
            }

            yield return applyPendingEnrage();
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

        private float GetProjectileTelegraphLength()
        {
            float fallback = Boss != null ? Boss.DetectionRange : 9f;
            return Mathf.Max(1f, fallback);
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
    }
}
