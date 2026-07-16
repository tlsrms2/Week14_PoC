using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    internal static class BossDeathSequencePlayer
    {
        public static IEnumerator Play(BossAI boss)
        {
            yield return PlayFinalDeathExplosions(boss);

            float delaySeconds = boss.DeathExplosionToAnimationDelaySecondsForSequence;
            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }

            yield return PlayDeathAnimation(boss);
        }

        private static IEnumerator PlayFinalDeathExplosions(BossAI boss)
        {
            int explosionCount = Mathf.Max(1, boss.FinalDeathExplosionCountForSequence);
            float interval = explosionCount > 1
                ? Mathf.Max(0f, boss.FinalDeathExplosionSecondsForSequence) / (explosionCount - 1)
                : 0f;

            for (int i = 0; i < explosionCount; i++)
            {
                Vector3 position = GetRandomDeathExplosionPosition(boss);
                SpawnDeathExplosionVisual(boss, position);

                Vector2 impactDirection = UnityEngine.Random.insideUnitCircle;
                BossAI.PlayEnemyHitCameraImpactForSequence(
                    impactDirection.sqrMagnitude > 0.0001f ? impactDirection.normalized : Vector2.right,
                    0.12f,
                    0.1f,
                    0.04f);

                if (i >= explosionCount - 1)
                {
                    continue;
                }

                if (interval > 0f)
                {
                    yield return new WaitForSeconds(interval);
                }
                else
                {
                    yield return null;
                }
            }
        }

        private static IEnumerator PlayDeathAnimation(BossAI boss)
        {
            Animator animator = boss.DeathAnimatorForSequence;
            string triggerName = boss.DeathTriggerNameForSequence;
            if (animator == null || !animator.isActiveAndEnabled || string.IsNullOrWhiteSpace(triggerName))
            {
                yield return WaitDeathAnimationFallback(boss);
                yield break;
            }

            int triggerHash = Animator.StringToHash(triggerName);
            if (!HasAnimatorTrigger(animator, triggerHash))
            {
                yield return WaitDeathAnimationFallback(boss);
                yield break;
            }

            animator.SetTrigger(triggerHash);
            yield return null;

            float startedAt = Time.time;
            float fallbackSeconds = Mathf.Max(0.01f, boss.DeathAnimationFallbackSecondsForSequence);
            while (animator != null
                && animator.isActiveAndEnabled
                && animator.IsInTransition(0)
                && Time.time - startedAt < fallbackSeconds)
            {
                yield return null;
            }

            if (animator == null || !animator.isActiveAndEnabled)
            {
                yield break;
            }

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            float animatorSpeed = Mathf.Abs(animator.speed);
            float animationSeconds = state.length > 0f && animatorSpeed > 0.01f
                ? state.length / animatorSpeed
                : fallbackSeconds;
            float waitLimit = Mathf.Max(fallbackSeconds, animationSeconds);

            while (Time.time - startedAt < waitLimit)
            {
                if (animator == null || !animator.isActiveAndEnabled)
                {
                    yield break;
                }

                if (!animator.IsInTransition(0))
                {
                    state = animator.GetCurrentAnimatorStateInfo(0);
                    if (!state.loop && state.normalizedTime >= 1f)
                    {
                        yield break;
                    }
                }

                yield return null;
            }
        }

        private static IEnumerator WaitDeathAnimationFallback(BossAI boss)
        {
            float fallbackSeconds = Mathf.Max(0f, boss.DeathAnimationFallbackSecondsForSequence);
            if (fallbackSeconds > 0f)
            {
                yield return new WaitForSeconds(fallbackSeconds);
            }
        }

        private static void SpawnDeathExplosionVisual(BossAI boss, Vector3 position)
        {
            GameObject prefab = boss.FinalDeathExplosionPrefabForSequence;
            if (prefab != null)
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
                BossSorting.ApplyToChildren(instance);
                UnityEngine.Object.Destroy(instance, boss.FinalDeathExplosionPrefabLifetimeSecondsForSequence);
                return;
            }

        }

        private static Vector3 GetRandomDeathExplosionPosition(BossAI boss)
        {
            float areaRadius = boss.FinalDeathExplosionAreaRadiusForSequence;
            if (areaRadius > 0f)
            {
                Transform areaCenter = boss.FinalDeathExplosionAreaCenterForSequence;
                Vector3 areaCenterPos = areaCenter != null ? areaCenter.position : boss.transform.position;
                areaCenterPos.z = 0f;
                return areaCenterPos + (Vector3)(UnityEngine.Random.insideUnitCircle * areaRadius);
            }

            SpriteRenderer renderer = GetRandomDeathExplosionRenderer(boss);
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;
                Vector3 position = new(
                    UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                    UnityEngine.Random.Range(bounds.min.y, bounds.max.y),
                    0f);
                return position;
            }

            Vector2 offset = UnityEngine.Random.insideUnitCircle * GetFallbackDeathExplosionRadius(boss);
            Vector3 center = boss.BodyRoot != null ? boss.BodyRoot.position : boss.transform.position;
            center.z = 0f;
            return center + (Vector3)offset;
        }

        private static SpriteRenderer GetRandomDeathExplosionRenderer(BossAI boss)
        {
            SpriteRenderer[] renderers = boss.RenderersForSequence;
            if (renderers == null)
            {
                return null;
            }

            int validCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (boss.CanUseDeathExplosionRendererForSequence(renderers[i]))
                {
                    validCount++;
                }
            }

            if (validCount <= 0)
            {
                return null;
            }

            int selectedIndex = UnityEngine.Random.Range(0, validCount);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!boss.CanUseDeathExplosionRendererForSequence(renderers[i]))
                {
                    continue;
                }

                if (selectedIndex == 0)
                {
                    return renderers[i];
                }

                selectedIndex--;
            }

            return null;
        }

        private static float GetFallbackDeathExplosionRadius(BossAI boss)
        {
            SpriteRenderer[] renderers = boss.RenderersForSequence;
            if (renderers == null)
            {
                return 0.35f;
            }

            Bounds bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!boss.CanUseDeathExplosionRendererForSequence(renderers[i]))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            if (!hasBounds)
            {
                return 0.35f;
            }

            return Mathf.Max(Mathf.Max(bounds.extents.x, bounds.extents.y), 0.35f);
        }

        private static bool HasAnimatorTrigger(Animator animator, int triggerHash)
        {
            if (animator == null)
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.type == AnimatorControllerParameterType.Trigger
                    && parameter.nameHash == triggerHash)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
