using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [AddComponentMenu("Week14/Boss/Assassin Clone")]
    public sealed class AssassinClone : MonoBehaviour
    {
        [Tooltip("플레이어 쪽으로 좌우 반전(flipX)시킬 첫 번째 대상 스프라이트입니다. 비워두면 반전하지 않습니다.")]
        [SerializeField] private SpriteRenderer facingSpriteTargetA;
        [Tooltip("플레이어 쪽으로 좌우 반전(flipX)시킬 두 번째 대상 스프라이트입니다. 비워두면 반전하지 않습니다.")]
        [SerializeField] private SpriteRenderer facingSpriteTargetB;

        private SpriteRenderer[] renderers;
        private readonly AssassinFacingMirrorCache facingMirrorCacheA = new();
        private readonly AssassinFacingMirrorCache facingMirrorCacheB = new();

        private void Awake()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            SetAlpha(0f);
        }

        private void LateUpdate()
        {
            UpdateFacingSprite();
        }

        private void UpdateFacingSprite()
        {
            PlayerCombatController player = PlayerCombatController.Active;
            if (player == null)
            {
                return;
            }

            bool flip = player.transform.position.x > transform.position.x;
            ApplyFacing(facingSpriteTargetA, flip, facingMirrorCacheA);
            ApplyFacing(facingSpriteTargetB, flip, facingMirrorCacheB);
        }

        private static void ApplyFacing(SpriteRenderer renderer, bool flip, AssassinFacingMirrorCache mirrorCache)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.flipX = flip;
            mirrorCache.Apply(renderer.transform, flip);
        }

        internal void PlayIntro(float introSeconds, int targetAlpha255)
        {
            StartCoroutine(FadeRoutine(0f, Mathf.Clamp01(targetAlpha255 / 255f), introSeconds));
        }

        internal void PlayDespawn(float despawnSeconds)
        {
            StartCoroutine(DespawnRoutine(despawnSeconds));
        }

        private IEnumerator DespawnRoutine(float despawnSeconds)
        {
            float startAlpha = renderers.Length > 0 && renderers[0] != null ? renderers[0].color.a : 1f;
            yield return FadeRoutine(startAlpha, 0f, despawnSeconds);
            Destroy(gameObject);
        }

        private IEnumerator FadeRoutine(float fromAlpha, float toAlpha, float seconds)
        {
            float duration = Mathf.Max(0.01f, seconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                SetAlpha(Mathf.Lerp(fromAlpha, toAlpha, elapsed / duration));
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            SetAlpha(toAlpha);
        }

        private void SetAlpha(float alpha)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Color color = renderer.color;
                color.a = alpha;
                renderer.color = color;
            }
        }
    }
}
