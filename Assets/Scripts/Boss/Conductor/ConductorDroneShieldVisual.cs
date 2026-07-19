using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Week14/Boss/Conductor Drone Shield Visual")]
    public sealed class ConductorDroneShieldVisual : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private sealed class SpriteTarget
        {
            public SpriteRenderer Renderer;
            public Color Color;
        }

        private sealed class RendererTarget
        {
            public Renderer Renderer;
            public MaterialPropertyBlock Properties;
            public bool HasColor;
            public Color Color;
            public bool HasBaseColor;
            public Color BaseColor;
        }

        private sealed class ParticleTarget
        {
            public ParticleSystem Particles;
            public bool ColorOverLifetimeEnabled;
            public ParticleSystem.MinMaxGradient ColorOverLifetime;
        }

        private readonly List<SpriteTarget> sprites = new();
        private readonly List<RendererTarget> renderers = new();
        private readonly List<ParticleTarget> particles = new();

        private Coroutine fadeRoutine;
        private float fadeOutSeconds;
        private float visibleAlpha = 1f;
        private float alpha = 1f;
        private bool initialized;
        private bool isFadingOut;

        public void Initialize(
            float fadeInSeconds,
            float nextFadeOutSeconds,
            float nextVisibleAlpha,
            int sortingOrder)
        {
            if (!initialized)
            {
                CaptureVisuals();
                initialized = true;
            }

            fadeOutSeconds = Mathf.Max(0f, nextFadeOutSeconds);
            visibleAlpha = Mathf.Clamp01(nextVisibleAlpha);
            isFadingOut = false;
            StopFadeRoutine();
            ApplySortingOrder(sortingOrder);

            float duration = Mathf.Max(0f, fadeInSeconds);
            if (duration <= 0f)
            {
                ApplyAlpha(visibleAlpha);
                return;
            }

            ApplyAlpha(0f);
            fadeRoutine = StartCoroutine(Fade(0f, visibleAlpha, duration, false));
        }

        public void FadeOutAndDestroy()
        {
            if (isFadingOut)
            {
                return;
            }

            isFadingOut = true;
            StopFadeRoutine();
            if (fadeOutSeconds <= 0f)
            {
                ApplyAlpha(0f);
                Destroy(gameObject);
                return;
            }

            fadeRoutine = StartCoroutine(Fade(alpha, 0f, fadeOutSeconds, true));
        }

        private IEnumerator Fade(float from, float to, float duration, bool destroyAfterFade)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += EnemyTimeScale.DeltaTime;
                ApplyAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            ApplyAlpha(to);
            fadeRoutine = null;
            if (destroyAfterFade)
            {
                Destroy(gameObject);
            }
        }

        private void CaptureVisuals()
        {
            SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer sprite = spriteRenderers[i];
                sprites.Add(new SpriteTarget { Renderer = sprite, Color = sprite.color });
            }

            Renderer[] childRenderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < childRenderers.Length; i++)
            {
                Renderer renderer = childRenderers[i];
                if (renderer is SpriteRenderer
                    || renderer is ParticleSystemRenderer
                    || renderer.sharedMaterial == null)
                {
                    continue;
                }

                Material material = renderer.sharedMaterial;
                RendererTarget target = new()
                {
                    Renderer = renderer,
                    Properties = new MaterialPropertyBlock(),
                    HasColor = material.HasProperty(ColorId),
                    HasBaseColor = material.HasProperty(BaseColorId)
                };
                renderer.GetPropertyBlock(target.Properties);
                if (target.HasColor)
                {
                    target.Color = material.GetColor(ColorId);
                }

                if (target.HasBaseColor)
                {
                    target.BaseColor = material.GetColor(BaseColorId);
                }

                if (target.HasColor || target.HasBaseColor)
                {
                    renderers.Add(target);
                }
            }

            ParticleSystem[] childParticles = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < childParticles.Length; i++)
            {
                ParticleSystem particle = childParticles[i];
                ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particle.colorOverLifetime;
                particles.Add(new ParticleTarget
                {
                    Particles = particle,
                    ColorOverLifetimeEnabled = colorOverLifetime.enabled,
                    ColorOverLifetime = colorOverLifetime.color
                });
            }
        }

        private void ApplyAlpha(float value)
        {
            alpha = Mathf.Clamp01(value);

            for (int i = 0; i < sprites.Count; i++)
            {
                SpriteTarget target = sprites[i];
                if (target.Renderer == null)
                {
                    continue;
                }

                Color color = target.Color;
                color.a *= alpha;
                target.Renderer.color = color;
            }

            for (int i = 0; i < renderers.Count; i++)
            {
                RendererTarget target = renderers[i];
                if (target.Renderer == null)
                {
                    continue;
                }

                if (target.HasColor)
                {
                    Color color = target.Color;
                    color.a *= alpha;
                    target.Properties.SetColor(ColorId, color);
                }

                if (target.HasBaseColor)
                {
                    Color color = target.BaseColor;
                    color.a *= alpha;
                    target.Properties.SetColor(BaseColorId, color);
                }

                target.Renderer.SetPropertyBlock(target.Properties);
            }

            for (int i = 0; i < particles.Count; i++)
            {
                ParticleTarget target = particles[i];
                if (target.Particles == null)
                {
                    continue;
                }

                ParticleSystem.ColorOverLifetimeModule module = target.Particles.colorOverLifetime;
                module.enabled = true;
                module.color = target.ColorOverLifetimeEnabled
                    ? ScaleAlpha(target.ColorOverLifetime, alpha)
                    : new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, alpha));
            }
        }

        private void ApplySortingOrder(int baseSortingOrder)
        {
            Renderer[] childRenderers = GetComponentsInChildren<Renderer>(true);
            if (childRenderers.Length == 0)
            {
                return;
            }

            int minimumOrder = int.MaxValue;
            for (int i = 0; i < childRenderers.Length; i++)
            {
                if (childRenderers[i] != null)
                {
                    minimumOrder = Mathf.Min(minimumOrder, childRenderers[i].sortingOrder);
                }
            }

            for (int i = 0; i < childRenderers.Length; i++)
            {
                Renderer renderer = childRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                int relativeOrder = minimumOrder != int.MaxValue
                    ? renderer.sortingOrder - minimumOrder
                    : 0;
                BossSorting.Apply(renderer);
                renderer.sortingOrder = baseSortingOrder + relativeOrder;
            }
        }

        private static ParticleSystem.MinMaxGradient ScaleAlpha(
            ParticleSystem.MinMaxGradient source,
            float multiplier)
        {
            switch (source.mode)
            {
                case ParticleSystemGradientMode.Color:
                {
                    Color color = source.color;
                    color.a *= multiplier;
                    return new ParticleSystem.MinMaxGradient(color);
                }
                case ParticleSystemGradientMode.TwoColors:
                {
                    Color min = source.colorMin;
                    Color max = source.colorMax;
                    min.a *= multiplier;
                    max.a *= multiplier;
                    return new ParticleSystem.MinMaxGradient(min, max);
                }
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(
                        ScaleGradientAlpha(source.gradientMin, multiplier),
                        ScaleGradientAlpha(source.gradientMax, multiplier));
                case ParticleSystemGradientMode.RandomColor:
                {
                    ParticleSystem.MinMaxGradient result = new(
                        ScaleGradientAlpha(source.gradient, multiplier));
                    result.mode = ParticleSystemGradientMode.RandomColor;
                    return result;
                }
                default:
                    return new ParticleSystem.MinMaxGradient(
                        ScaleGradientAlpha(source.gradient, multiplier));
            }
        }

        private static Gradient ScaleGradientAlpha(Gradient source, float multiplier)
        {
            if (source == null)
            {
                return new Gradient();
            }

            GradientAlphaKey[] alphaKeys = source.alphaKeys;
            for (int i = 0; i < alphaKeys.Length; i++)
            {
                alphaKeys[i].alpha *= multiplier;
            }

            Gradient result = new();
            result.SetKeys(source.colorKeys, alphaKeys);
            result.mode = source.mode;
            return result;
        }

        private void StopFadeRoutine()
        {
            if (fadeRoutine == null)
            {
                return;
            }

            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
    }
}
