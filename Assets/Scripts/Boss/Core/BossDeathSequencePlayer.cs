using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    internal static class BossDeathSequencePlayer
    {
        public static IEnumerator Play(BossAI boss, bool playFinalDeathExplosions)
        {
            if (playFinalDeathExplosions)
            {
                yield return PlayFinalDeathExplosions(boss);

                float delaySeconds = boss.DeathExplosionToAnimationDelaySecondsForSequence;
                if (delaySeconds > 0f)
                {
                    yield return new WaitForSeconds(delaySeconds);
                }
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

        // 활성 애니메이터가 여러 개면 트리거는 전부에 보내고, 타이밍은 첫 번째 유효 애니메이터로 잰다.
        // Hacker처럼 페이즈 비주얼을 교체하는 보스는 계층상 첫 Animator가 비활성 상태일 수 있다.
        private static IEnumerator PlayDeathAnimation(BossAI boss)
        {
            Animator[] animators = boss.DeathAnimatorsForSequence;
            string triggerName = boss.DeathTriggerNameForSequence;
            if (animators == null || string.IsNullOrWhiteSpace(triggerName))
            {
                yield return WaitDeathAnimationFallback(boss);
                yield break;
            }

            // 트리거 이름과 실제 스테이트 이름이 다른 보스(예: Hacker "Die" -> "1w-die")를 위해
            // BossAI.DeathStateNameOverride가 있으면 그걸 스테이트 검색에 우선 사용한다.
            string stateName = !string.IsNullOrWhiteSpace(boss.DeathStateNameForSequence)
                ? boss.DeathStateNameForSequence
                : triggerName;

            int triggerHash = Animator.StringToHash(triggerName);
            Animator primary = FindPrimaryDeathAnimator(
                animators,
                triggerHash,
                stateName);
            if (primary == null)
            {
                yield return WaitDeathAnimationFallback(boss);
                yield break;
            }

            AnimatorStateInfo initialState =
                primary.GetCurrentAnimatorStateInfo(0);
            int initialStateHash = initialState.fullPathHash;
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator != null && animator.isActiveAndEnabled && HasAnimatorTrigger(animator, triggerHash))
                {
                    animator.SetTrigger(triggerHash);
                }
            }

            float fallbackSeconds = Mathf.Max(0.01f, boss.DeathAnimationFallbackSecondsForSequence);
            int deathStateHash = 0;
            AnimatorStateInfo deathState = default;
            if (TryResolveDeathStateHash(
                    primary,
                    stateName,
                    out deathStateHash))
            {
                RestartDeathStatesAtBeginning(
                    animators,
                    stateName,
                    triggerHash);
                deathState =
                    primary.GetCurrentAnimatorStateInfo(0);
            }
            else
            {
                // 이름으로 못 찾았을 때만 쓰는 폴백: 트리거 세팅 직후 진입하는 아무 스테이트나
                // 사망 스테이트로 추정한다. 처형 컷신 막바지에 아직 소모되지 않은 다른 트리거의
                // 전이를 잘못 붙잡을 수 있으니, 이름을 아는 보스는 DeathStateNameOverride를 반드시
                // 지정해 이 분기를 타지 않게 하는 게 안전하다.
                float stateEntryStartedAt = Time.unscaledTime;
                float stateEntryTimeout = Mathf.Max(
                    0.5f,
                    fallbackSeconds);
                while (primary != null
                    && primary.isActiveAndEnabled
                    && Time.unscaledTime - stateEntryStartedAt
                        < stateEntryTimeout)
                {
                    if (TryGetEnteredDeathState(
                            primary,
                            initialStateHash,
                            out deathState))
                    {
                        deathStateHash = deathState.fullPathHash;
                        break;
                    }

                    yield return null;
                }
            }

            if (primary == null
                || !primary.isActiveAndEnabled
                || deathStateHash == 0)
            {
                yield return WaitDeathAnimationFallback(boss);
                yield break;
            }

            float animatorSpeed = Mathf.Abs(primary.speed);
            float timeScale = primary.updateMode
                    == AnimatorUpdateMode.UnscaledTime
                ? 1f
                : Mathf.Max(0.01f, Time.timeScale);
            float animationSeconds =
                deathState.length > 0f && animatorSpeed > 0.01f
                ? deathState.length / (animatorSpeed * timeScale)
                : fallbackSeconds;
            float waitLimit = Mathf.Max(
                5f,
                fallbackSeconds * 4f,
                animationSeconds * 2f + 0.5f);
            float animationStartedAt = Time.unscaledTime;
            bool observedDeathState = false;

            while (Time.unscaledTime - animationStartedAt < waitLimit)
            {
                if (primary == null || !primary.isActiveAndEnabled)
                {
                    yield break;
                }

                if (TryGetAnimatorState(
                        primary,
                        deathStateHash,
                        out AnimatorStateInfo currentDeathState))
                {
                    observedDeathState = true;
                    if (!currentDeathState.loop
                        && currentDeathState.normalizedTime >= 1f)
                    {
                        HoldDeathAnimationFinalFrame(
                            animators,
                            triggerHash,
                            deathStateHash);
                        yield break;
                    }
                }
                else if (observedDeathState
                    && !primary.IsInTransition(0))
                {
                    HoldDeathAnimationFinalFrame(
                        animators,
                        triggerHash,
                        deathStateHash);
                    yield break;
                }

                yield return null;
            }

            HoldDeathAnimationFinalFrame(
                animators,
                triggerHash,
                deathStateHash);
        }

        private static Animator FindPrimaryDeathAnimator(
            Animator[] animators,
            int triggerHash,
            string stateName)
        {
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator != null
                    && animator.isActiveAndEnabled
                    && TryResolveDeathStateHash(
                        animator,
                        stateName,
                        out _))
                {
                    return animator;
                }
            }

            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator != null
                    && animator.isActiveAndEnabled
                    && HasAnimatorTrigger(animator, triggerHash))
                {
                    return animator;
                }
            }

            return null;
        }

        private static bool TryGetEnteredDeathState(
            Animator animator,
            int initialStateHash,
            out AnimatorStateInfo state)
        {
            state = default;
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return false;
            }

            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next =
                    animator.GetNextAnimatorStateInfo(0);
                if (next.fullPathHash != 0)
                {
                    state = next;
                    return true;
                }
            }

            AnimatorStateInfo current =
                animator.GetCurrentAnimatorStateInfo(0);
            if (current.fullPathHash != 0
                && current.fullPathHash != initialStateHash)
            {
                state = current;
                return true;
            }

            return false;
        }

        private static void RestartDeathStatesAtBeginning(
            Animator[] animators,
            string stateName,
            int triggerHash)
        {
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null
                    || !animator.isActiveAndEnabled
                    || !TryResolveDeathStateHash(
                        animator,
                        stateName,
                        out int stateHash))
                {
                    continue;
                }

                if (HasAnimatorTrigger(animator, triggerHash))
                {
                    animator.ResetTrigger(triggerHash);
                }

                animator.Play(stateHash, 0, 0f);
                animator.Update(0f);
            }
        }

        private static bool TryResolveDeathStateHash(
            Animator animator,
            string configuredName,
            out int stateHash)
        {
            stateHash = 0;
            if (animator == null
                || !animator.isActiveAndEnabled
                || animator.layerCount <= 0)
            {
                return false;
            }

            string layerName = animator.GetLayerName(0);
            if (TryResolveStateHash(
                    animator,
                    layerName,
                    configuredName,
                    out stateHash))
            {
                return true;
            }

            if (!string.Equals(
                    configuredName,
                    "Die",
                    StringComparison.Ordinal)
                && TryResolveStateHash(
                    animator,
                    layerName,
                    "Die",
                    out stateHash))
            {
                return true;
            }

            return !string.Equals(
                    configuredName,
                    "Death",
                    StringComparison.Ordinal)
                && TryResolveStateHash(
                    animator,
                    layerName,
                    "Death",
                    out stateHash);
        }

        private static bool TryResolveStateHash(
            Animator animator,
            string layerName,
            string stateName,
            out int stateHash)
        {
            stateHash = 0;
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            int fullPathHash = Animator.StringToHash(
                $"{layerName}.{stateName.Trim()}");
            if (animator.HasState(0, fullPathHash))
            {
                stateHash = fullPathHash;
                return true;
            }

            int shortNameHash = Animator.StringToHash(
                stateName.Trim());
            if (!animator.HasState(0, shortNameHash))
            {
                return false;
            }

            stateHash = shortNameHash;
            return true;
        }

        private static bool TryGetAnimatorState(
            Animator animator,
            int stateHash,
            out AnimatorStateInfo state)
        {
            state = default;
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return false;
            }

            AnimatorStateInfo current =
                animator.GetCurrentAnimatorStateInfo(0);
            if (current.fullPathHash == stateHash)
            {
                state = current;
                return true;
            }

            if (!animator.IsInTransition(0))
            {
                return false;
            }

            AnimatorStateInfo next =
                animator.GetNextAnimatorStateInfo(0);
            if (next.fullPathHash != stateHash)
            {
                return false;
            }

            state = next;
            return true;
        }

        private static void HoldDeathAnimationFinalFrame(
            Animator[] animators,
            int triggerHash,
            int deathStateHash)
        {
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null
                    || !animator.isActiveAndEnabled
                    || (!HasAnimatorTrigger(animator, triggerHash)
                        && !animator.HasState(0, deathStateHash)))
                {
                    continue;
                }

                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                int stateHash = animator.HasState(0, deathStateHash)
                    ? deathStateHash
                    : state.fullPathHash;
                animator.Play(stateHash, 0, 1f);
                animator.Update(0f);
                animator.speed = 0f;
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
