using UnityEngine;

namespace Week14.Combat
{
    public static class ProjectileVfx
    {
        private const int RingSegments = 48;
        private static Material spriteMaterial;

        public static void ApplyVisibility(GameObject owner, Color color, float radius, float trailSeconds, float trailWidthMultiplier)
        {
            if (owner == null)
            {
                return;
            }

            RemoveFireballGlow(owner.transform);
            EnsureTrail(owner, color, radius, trailSeconds, trailWidthMultiplier);
        }

        public static void PlayParry(Vector3 position, Color sparkColor, Color ringColor, int sparkCount, float duration)
        {
            PlayBidirectionalSpark(position, Vector2.right, sparkColor, sparkCount, duration);
            PlayRing(position, ringColor, duration);
        }

        public static void PlayParry(Vector3 position, Vector2 direction, Color sparkColor, Color ringColor, int sparkCount, float duration)
        {
            PlayBidirectionalSpark(position, direction, sparkColor, sparkCount, duration);
            PlayRing(position, ringColor, duration);
        }

        public static void PlayParry(
            Vector3 position,
            Vector2 direction,
            Color sparkColor,
            Color ringColor,
            Color glitterColor,
            int sparkCount,
            int glitterCount,
            float sparkSeconds,
            float ringSeconds,
            float glitterSeconds)
        {
            PlayBidirectionalSpark(position, direction, sparkColor, sparkCount, sparkSeconds);
            PlayRing(position, ringColor, ringSeconds);
            PlayRingGlitter(position, direction, glitterColor, glitterCount, glitterSeconds);
        }

        public static void PlayBulletImpact(Vector3 position, Vector2 direction, Color color)
        {
            PlayDirectionalSpark(position, direction, color, 10, 0.16f, 36f, 2.5f, 6f);
        }

        private static void RemoveFireballGlow(Transform owner)
        {
            Transform glow = owner.Find("ProjectileGlow");
            if (glow != null)
            {
                Object.Destroy(glow.gameObject);
            }
        }

        private static void EnsureTrail(GameObject owner, Color color, float radius, float seconds, float widthMultiplier)
        {
            TrailRenderer trail = owner.GetComponent<TrailRenderer>();
            if (trail == null)
            {
                trail = owner.AddComponent<TrailRenderer>();
            }

            Color trailColor = new Color(1f, 0.82f, 0.18f, 0.55f);
            Color trailEndColor = trailColor;
            trailEndColor.a = 0f;

            trail.time = Mathf.Clamp(seconds, 0.025f, 0.12f);
            trail.startWidth = Mathf.Max(0.01f, radius * Mathf.Clamp(widthMultiplier * 0.35f, 0.35f, 1.1f));
            trail.endWidth = 0f;
            trail.startColor = trailColor;
            trail.endColor = trailEndColor;
            trail.numCornerVertices = 0;
            trail.numCapVertices = 0;
            trail.autodestruct = false;
            trail.material = GetSpriteMaterial();
            trail.sortingOrder = 18;
        }

        private static void PlayDirectionalSpark(
            Vector3 position,
            Vector2 direction,
            Color color,
            int count,
            float duration,
            float spreadDegrees,
            float minSpeed,
            float maxSpeed)
        {
            Vector2 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            GameObject sparkObject = new GameObject("ParrySparkVfx");
            sparkObject.transform.position = position;

            ParticleSystem particles = sparkObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.duration = Mathf.Max(0.01f, duration);
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.055f);
            main.startColor = color;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.05f;

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 70;
            renderer.sharedMaterial = GetSpriteMaterial();

            particles.Play();
            int emitCount = Mathf.Max(0, count);
            float baseAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
            for (int i = 0; i < emitCount; i++)
            {
                float angle = (baseAngle + Random.Range(-spreadDegrees * 0.5f, spreadDegrees * 0.5f)) * Mathf.Deg2Rad;
                float speed = Random.Range(minSpeed, maxSpeed);
                ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * speed,
                    startLifetime = Random.Range(0.08f, Mathf.Max(0.09f, duration)),
                    startSize = Random.Range(0.018f, 0.055f),
                    startColor = color
                };
                particles.Emit(emitParams, 1);
            }
        }

        private static void PlayBidirectionalSpark(Vector3 position, Vector2 direction, Color color, int count, float duration)
        {
            int forwardCount = Mathf.CeilToInt(Mathf.Max(0, count) * 0.5f);
            int backwardCount = Mathf.Max(0, count) - forwardCount;
            PlayDirectionalSpark(position, direction, color, forwardCount, duration, 38f, 4.6f, 10.5f);
            PlayDirectionalSpark(position, -direction, color, backwardCount, duration, 38f, 4.6f, 10.5f);
        }

        private static void PlayRing(Vector3 position, Color color, float duration)
        {
            GameObject ringObject = new GameObject("ParryRingVfx");
            ringObject.transform.position = position;
            LineRenderer line = ringObject.AddComponent<LineRenderer>();
            line.loop = true;
            line.positionCount = RingSegments;
            line.useWorldSpace = false;
            line.material = GetSpriteMaterial();
            line.startColor = color;
            line.endColor = color;
            line.startWidth = 0.035f;
            line.endWidth = 0.035f;
            line.sortingOrder = 69;

            for (int i = 0; i < RingSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / RingSegments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
            }

            ParryRingVfx ring = ringObject.AddComponent<ParryRingVfx>();
            ring.Play(line, duration, color);
        }

        private static void PlayRingGlitter(Vector3 position, Vector2 direction, Color color, int count, float duration)
        {
            Vector2 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            Vector2 side = new Vector2(-forward.y, forward.x);
            GameObject glitterObject = new GameObject("ParryRingGlitterVfx");
            glitterObject.transform.position = position;

            ParticleSystem particles = glitterObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.duration = Mathf.Max(0.01f, duration);
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, Mathf.Max(0.07f, duration));
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.11f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 71;
            renderer.sharedMaterial = GetSpriteMaterial();

            particles.Play();
            int emitCount = Mathf.Max(0, count);
            for (int i = 0; i < emitCount; i++)
            {
                float sideSign = i % 2 == 0 ? 1f : -1f;
                float radius = Random.Range(0.16f, 0.52f);
                Vector2 radial = (side * sideSign + forward * Random.Range(-0.35f, 0.35f)).normalized;
                Color particleColor = color;
                particleColor.a *= Random.Range(0.65f, 1f);
                ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
                {
                    position = position + (Vector3)(radial * radius),
                    velocity = radial * Random.Range(0.15f, 0.85f),
                    startLifetime = Random.Range(0.06f, Mathf.Max(0.07f, duration)),
                    startSize = Random.Range(0.035f, 0.11f),
                    startColor = particleColor
                };
                particles.Emit(emitParams, 1);
            }
        }

        private static Material GetSpriteMaterial()
        {
            if (spriteMaterial != null)
            {
                return spriteMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            spriteMaterial = shader != null ? new Material(shader) : null;
            return spriteMaterial;
        }

        private static Sprite CreateCircleSprite()
        {
            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(1f - distance / radius);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
