using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [AddComponentMenu("Week14/Boss/Assassin Clone")]
    public sealed class AssassinClone : MonoBehaviour
    {
        private SpriteRenderer[] renderers;

        private void Awake()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            SetAlpha(0f);
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
