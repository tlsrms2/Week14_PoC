using System.Collections.Generic;
using UnityEngine;

namespace Week14.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    [AddComponentMenu("Week14/Combat/Hacker Execution Particle Gather VFX")]
    public sealed class HackerExecutionParticleGatherVfx : MonoBehaviour
    {
        private sealed class DustParticle
        {
            internal Vector3 StartPosition;
            internal float Size;
            internal float Delay;
            internal int ArmIndex;
            internal float PhaseOffset;
            internal Color StartColor;
        }

        [Header("Appearance")]
        [SerializeField] private Material particleMaterial;
        [SerializeField, Min(0.005f)] private float minParticleSize = 0.025f;
        [SerializeField, Min(0.005f)] private float maxParticleSize = 0.075f;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 78;

        [Header("Burst")]
        [SerializeField, Range(1, 20)] private int particlesPerProjectile = 10;
        [SerializeField, Min(0f)] private float spawnRadius = 0.18f;

        [Header("Gather")]
        [SerializeField, Range(2, 6)] private int spiralArmCount = 4;
        [SerializeField, Range(0.5f, 6f)] private float spiralTurns = 1.35f;
        [SerializeField, Min(0f)] private float spiralRadius = 0.65f;
        [SerializeField, Range(0f, 0.8f)] private float arrivalStagger = 0.24f;
        [SerializeField] private AnimationCurve gatherCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Solid Charge Circle")]
        [SerializeField] private bool showSolidChargeCircle = true;
        [SerializeField, Min(0.001f)] private float startCircleRadius = 0.02f;
        [SerializeField, Min(0.01f)] private float maxCircleRadius = 0.18f;
        [SerializeField, Range(0f, 1f)] private float circleAlpha = 0.82f;

        private readonly List<DustParticle> particles = new();
        private ParticleSystem particleSystemComponent;
        private ParticleSystem.Particle[] renderParticles;
        private Material runtimeMaterial;
        private GameObject chargeCircleObject;
        private SpriteRenderer chargeCircleRenderer;
        private Texture2D chargeCircleTexture;
        private Sprite chargeCircleSprite;
        private Transform targetMuzzle;
        private Color targetColor = Color.white;
        private float gatherStartedAt;
        private float gatherSeconds;
        private int capturedParticleCount;
        private bool gathering;
        private bool holdingChargeCircle;

        private void Awake()
        {
            ConfigureParticleSystem();
        }

        private void LateUpdate()
        {
            if (targetMuzzle == null)
            {
                return;
            }

            if (!gathering)
            {
                UpdateSolidChargeCircle(1f);
                return;
            }

            float progress = Mathf.Clamp01(
                (Time.unscaledTime - gatherStartedAt)
                / Mathf.Max(0.05f, gatherSeconds));
            UpdateParticles(progress);
            UpdateSolidChargeCircle(progress);
            if (progress >= 1f)
            {
                gathering = false;
                holdingChargeCircle = showSolidChargeCircle;
                particles.Clear();
                particleSystemComponent.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void OnDisable()
        {
            Clear();
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }

            if (chargeCircleSprite != null)
            {
                Destroy(chargeCircleSprite);
            }

            if (chargeCircleTexture != null)
            {
                Destroy(chargeCircleTexture);
            }
        }

        public void CaptureBurst(Vector3 worldPosition, Color color)
        {
            ConfigureParticleSystem();
            int count = Mathf.Max(1, particlesPerProjectile);
            float minSize = Mathf.Min(minParticleSize, maxParticleSize);
            float maxSize = Mathf.Max(minParticleSize, maxParticleSize);
            int armCount = Mathf.Max(2, spiralArmCount);
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle
                    * Mathf.Max(0f, spawnRadius);
                int particleIndex = capturedParticleCount++;
                particles.Add(new DustParticle
                {
                    StartPosition = worldPosition + (Vector3)offset,
                    Size = Random.Range(minSize, maxSize),
                    Delay = count <= 1
                        ? 0f
                        : i / (float)(count - 1),
                    ArmIndex = particleIndex % armCount,
                    PhaseOffset = Random.Range(-0.14f, 0.14f),
                    StartColor = Color.Lerp(Color.white, color, 0.35f)
                });
            }
        }

        public void BeginGather(
            Transform muzzle,
            float durationSeconds,
            Color gatheredColor)
        {
            if (muzzle == null || particles.Count == 0)
            {
                Clear();
                return;
            }

            ConfigureParticleSystem();
            EnsureSolidChargeCircle();
            ResolveRuntimeSorting(muzzle);
            targetMuzzle = muzzle;
            targetColor = gatheredColor;
            gatherSeconds = Mathf.Max(0.05f, durationSeconds);
            gatherStartedAt = Time.unscaledTime;
            gathering = true;
            holdingChargeCircle = showSolidChargeCircle;
            EnsureRenderBuffer();
            particleSystemComponent.Play(true);
            SetSolidChargeCircleActive(showSolidChargeCircle);
            UpdateParticles(0f);
            UpdateSolidChargeCircle(0f);
        }

        public void Clear()
        {
            gathering = false;
            holdingChargeCircle = false;
            targetMuzzle = null;
            particles.Clear();
            capturedParticleCount = 0;
            SetSolidChargeCircleActive(false);
            if (particleSystemComponent != null)
            {
                particleSystemComponent.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void ConfigureParticleSystem()
        {
            if (particleSystemComponent != null)
            {
                return;
            }

            particleSystemComponent = GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particleSystemComponent.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            main.maxParticles = 512;
            main.startLifetime = 2f;
            ParticleSystem.EmissionModule emission =
                particleSystemComponent.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particleSystemComponent.shape;
            shape.enabled = false;

            ParticleSystemRenderer renderer =
                particleSystemComponent.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;
            renderer.sharedMaterial = ResolveMaterial();
        }

        private void EnsureRenderBuffer()
        {
            if (renderParticles == null
                || renderParticles.Length < particles.Count)
            {
                renderParticles =
                    new ParticleSystem.Particle[Mathf.NextPowerOfTwo(
                        Mathf.Max(1, particles.Count))];
            }
        }

        private void UpdateParticles(float progress)
        {
            EnsureRenderBuffer();
            Vector3 target = targetMuzzle.position;
            float stagger = Mathf.Clamp01(arrivalStagger);
            int armCount = Mathf.Max(2, spiralArmCount);
            for (int i = 0; i < particles.Count; i++)
            {
                DustParticle dust = particles[i];
                float delay = dust.Delay * stagger;
                float localProgress = Mathf.Clamp01(
                    (progress - delay) / Mathf.Max(0.01f, 1f - delay));
                float travel = gatherCurve != null
                    ? gatherCurve.Evaluate(localProgress)
                    : localProgress;
                float startDistance = Vector2.Distance(
                    dust.StartPosition,
                    target);
                float radius = Mathf.Max(
                        Mathf.Max(0f, spiralRadius),
                        startDistance)
                    * (1f - travel);
                float angle = dust.ArmIndex
                        * Mathf.PI * 2f / armCount
                    + dust.PhaseOffset
                    + spiralTurns * Mathf.PI * 2f * (1f - travel);
                Vector3 spiralPosition = target + new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f);
                float alignToArm = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(localProgress / 0.2f));
                Vector3 position = Vector3.LerpUnclamped(
                    dust.StartPosition,
                    spiralPosition,
                    alignToArm);

                float fade = localProgress < 0.9f
                    ? 1f
                    : 1f - Mathf.InverseLerp(0.9f, 1f, localProgress);
                Color color = Color.Lerp(
                    dust.StartColor,
                    targetColor,
                    travel);
                color.a *= fade;
                renderParticles[i].position = position;
                renderParticles[i].startColor = color;
                renderParticles[i].startSize =
                    Mathf.Lerp(dust.Size, dust.Size * 0.35f, travel);
                renderParticles[i].startLifetime = 2f;
                renderParticles[i].remainingLifetime = 1f;
            }

            particleSystemComponent.SetParticles(
                renderParticles,
                particles.Count);
        }

        private void EnsureSolidChargeCircle()
        {
            if (chargeCircleRenderer != null)
            {
                return;
            }

            const int textureSize = 64;
            chargeCircleTexture = new Texture2D(
                textureSize,
                textureSize,
                TextureFormat.RGBA32,
                false)
            {
                name = "HackerExecutionChargeCircle_Runtime",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[textureSize * textureSize];
            Vector2 center = new((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
            float radius = textureSize * 0.5f;
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float normalizedDistance =
                        Vector2.Distance(new Vector2(x, y), center)
                        / radius;
                    float edge = Mathf.InverseLerp(
                        0.9f,
                        1f,
                        normalizedDistance);
                    byte alpha = (byte)Mathf.RoundToInt(
                        (1f - Mathf.SmoothStep(0f, 1f, edge)) * 255f);
                    pixels[y * textureSize + x] =
                        new Color32(255, 255, 255, alpha);
                }
            }

            chargeCircleTexture.SetPixels32(pixels);
            chargeCircleTexture.Apply(false, true);
            chargeCircleSprite = Sprite.Create(
                chargeCircleTexture,
                new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f),
                textureSize);
            chargeCircleSprite.name =
                "HackerExecutionChargeCircle_Runtime";
            chargeCircleSprite.hideFlags = HideFlags.HideAndDontSave;

            chargeCircleObject = new GameObject("SolidChargeCircle");
            chargeCircleObject.transform.SetParent(transform, false);
            chargeCircleRenderer =
                chargeCircleObject.AddComponent<SpriteRenderer>();
            chargeCircleRenderer.sprite = chargeCircleSprite;
            chargeCircleRenderer.sortingLayerName = sortingLayerName;
            chargeCircleRenderer.sortingOrder = sortingOrder + 2;
            SetSolidChargeCircleActive(false);
        }

        private void UpdateSolidChargeCircle(float progress)
        {
            if (!holdingChargeCircle
                || chargeCircleRenderer == null
                || targetMuzzle == null)
            {
                return;
            }

            float radius = Mathf.Lerp(
                Mathf.Max(0.001f, startCircleRadius),
                Mathf.Max(startCircleRadius, maxCircleRadius),
                Mathf.Clamp01(progress));
            chargeCircleObject.transform.position =
                targetMuzzle.position;
            chargeCircleObject.transform.localScale =
                Vector3.one * (radius * 2f);
            Color color = Color.Lerp(
                Color.white,
                targetColor,
                Mathf.Clamp01(progress));
            color.a = Mathf.Clamp01(circleAlpha);
            chargeCircleRenderer.color = color;
        }

        private void ResolveRuntimeSorting(Transform muzzle)
        {
            int layerId = SortingLayer.NameToID(sortingLayerName);
            int order = sortingOrder;
            PlayerCombatController owner =
                muzzle.GetComponentInParent<PlayerCombatController>();
            Renderer[] ownerRenderers = owner != null
                ? owner.GetComponentsInChildren<Renderer>(true)
                : null;
            if (ownerRenderers != null)
            {
                int bestLayerValue = int.MinValue;
                for (int i = 0; i < ownerRenderers.Length; i++)
                {
                    Renderer ownerRenderer = ownerRenderers[i];
                    if (ownerRenderer == null)
                    {
                        continue;
                    }

                    int layerValue = SortingLayer.GetLayerValueFromID(
                        ownerRenderer.sortingLayerID);
                    if (layerValue > bestLayerValue)
                    {
                        bestLayerValue = layerValue;
                        layerId = ownerRenderer.sortingLayerID;
                        order = ownerRenderer.sortingOrder;
                    }
                    else if (layerValue == bestLayerValue)
                    {
                        order = Mathf.Max(
                            order,
                            ownerRenderer.sortingOrder);
                    }
                }
            }

            ParticleSystemRenderer particleRenderer =
                particleSystemComponent.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sortingLayerID = layerId;
            particleRenderer.sortingOrder = order + 20;
            if (chargeCircleRenderer != null)
            {
                chargeCircleRenderer.sortingLayerID = layerId;
                chargeCircleRenderer.sortingOrder = order + 22;
            }
        }

        private void SetSolidChargeCircleActive(bool active)
        {
            if (chargeCircleObject != null)
            {
                chargeCircleObject.SetActive(active);
            }
        }

        private Material ResolveMaterial()
        {
            if (particleMaterial != null)
            {
                return particleMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return null;
            }

            runtimeMaterial = new Material(shader)
            {
                name = "HackerExecutionDust_Runtime",
                hideFlags = HideFlags.HideAndDontSave
            };
            return runtimeMaterial;
        }
    }
}
