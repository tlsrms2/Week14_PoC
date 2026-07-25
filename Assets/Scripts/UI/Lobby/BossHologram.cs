using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BossHologram : MonoBehaviour
    {
        private const float VisibleAlpha = 150f / 255f;

        [Header("Floating")]
        [Tooltip("Seconds for one full up-and-down floating cycle.")]
        [SerializeField, Min(0.01f)] private float floatPeriod = 2f;
        [Tooltip("Vertical distance from the starting local position.")]
        [SerializeField, Min(0f)] private float floatAmplitude = 0.08f;

        [Header("Boss Sprite")]
        [Tooltip("SpriteRenderer to fade and swap. If empty, this object's SpriteRenderer is used.")]
        [SerializeField] private SpriteRenderer targetRenderer;
        [Tooltip("Boss sprites picked randomly during hologram swaps.")]
        [SerializeField] private List<Sprite> bossSprites = new();
        [Tooltip("Base seconds between sprite swaps.")]
        [SerializeField, Min(0.01f)] private float spriteSwapPeriod = 3f;
        [Tooltip("Random seconds added to or removed from the base swap period.")]
        [SerializeField, Min(0f)] private float spriteSwapJitter = 0.75f;
        [Tooltip("Seconds used for each fade-out and fade-in.")]
        [SerializeField, Min(0f)] private float fadeSeconds = 0.35f;

        private Coroutine spriteSwapRoutine;
        private Vector3 baseLocalPosition;
        private float floatElapsedSeconds;

        private void Awake()
        {
            ResolveRenderer();
        }

        private void OnEnable()
        {
            ResolveRenderer();

            baseLocalPosition = transform.localPosition;
            floatElapsedSeconds = 0f;

            if (targetRenderer != null && targetRenderer.sprite == null)
            {
                targetRenderer.sprite = PickRandomBossSprite(null);
            }

            SetAlpha(VisibleAlpha);
            spriteSwapRoutine = StartCoroutine(SpriteSwapRoutine());
        }

        private void OnDisable()
        {
            if (spriteSwapRoutine != null)
            {
                StopCoroutine(spriteSwapRoutine);
                spriteSwapRoutine = null;
            }

            transform.localPosition = baseLocalPosition;
        }

        private void Update()
        {
            if (floatAmplitude <= 0f)
            {
                return;
            }

            floatElapsedSeconds += Time.deltaTime;
            float radians = floatElapsedSeconds / Mathf.Max(0.01f, floatPeriod) * Mathf.PI * 2f;
            Vector3 nextPosition = baseLocalPosition;
            nextPosition.y += Mathf.Sin(radians) * floatAmplitude;
            transform.localPosition = nextPosition;
        }

        private IEnumerator SpriteSwapRoutine()
        {
            while (enabled)
            {
                yield return new WaitForSeconds(GetNextSwapDelay());

                if (!CanSwapSprite())
                {
                    continue;
                }

                yield return FadeAlphaRoutine(0f);

                Sprite nextSprite = PickRandomBossSprite(targetRenderer.sprite);
                if (nextSprite != null)
                {
                    targetRenderer.sprite = nextSprite;
                }

                yield return FadeAlphaRoutine(VisibleAlpha);
            }
        }

        private IEnumerator FadeAlphaRoutine(float targetAlpha)
        {
            if (targetRenderer == null)
            {
                yield break;
            }

            float seconds = Mathf.Max(0f, fadeSeconds);
            if (seconds <= 0f)
            {
                SetAlpha(targetAlpha);
                yield break;
            }

            float startAlpha = targetRenderer.color.a;
            float elapsedSeconds = 0f;

            while (elapsedSeconds < seconds)
            {
                elapsedSeconds += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsedSeconds / seconds);
                SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, progress));
                yield return null;
            }

            SetAlpha(targetAlpha);
        }

        private float GetNextSwapDelay()
        {
            float jitter = Mathf.Max(0f, spriteSwapJitter);
            float min = Mathf.Max(0.01f, spriteSwapPeriod - jitter);
            float max = Mathf.Max(min, spriteSwapPeriod + jitter);
            return Random.Range(min, max);
        }

        private bool CanSwapSprite()
        {
            return targetRenderer != null && CountUsableSprites(targetRenderer.sprite) > 0;
        }

        private Sprite PickRandomBossSprite(Sprite currentSprite)
        {
            int eligibleCount = CountUsableSprites(currentSprite);
            if (eligibleCount <= 0)
            {
                return FirstUsableSprite();
            }

            int selectedIndex = Random.Range(0, eligibleCount);
            for (int i = 0; i < bossSprites.Count; i++)
            {
                Sprite sprite = bossSprites[i];
                if (sprite == null || sprite == currentSprite)
                {
                    continue;
                }

                if (selectedIndex == 0)
                {
                    return sprite;
                }

                selectedIndex--;
            }

            return FirstUsableSprite();
        }

        private int CountUsableSprites(Sprite currentSprite)
        {
            int count = 0;
            for (int i = 0; i < bossSprites.Count; i++)
            {
                Sprite sprite = bossSprites[i];
                if (sprite != null && sprite != currentSprite)
                {
                    count++;
                }
            }

            return count;
        }

        private Sprite FirstUsableSprite()
        {
            for (int i = 0; i < bossSprites.Count; i++)
            {
                if (bossSprites[i] != null)
                {
                    return bossSprites[i];
                }
            }

            return null;
        }

        private void SetAlpha(float alpha)
        {
            if (targetRenderer == null)
            {
                return;
            }

            Color color = targetRenderer.color;
            color.a = Mathf.Clamp01(alpha);
            targetRenderer.color = color;
        }

        private void ResolveRenderer()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private void OnValidate()
        {
            floatPeriod = Mathf.Max(0.01f, floatPeriod);
            floatAmplitude = Mathf.Max(0f, floatAmplitude);
            spriteSwapPeriod = Mathf.Max(0.01f, spriteSwapPeriod);
            spriteSwapJitter = Mathf.Max(0f, spriteSwapJitter);
            fadeSeconds = Mathf.Max(0f, fadeSeconds);
        }
    }
}
