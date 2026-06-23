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
            position.z = 0f;
            PlayBidirectionalSpark(position, Vector2.right, sparkColor, sparkCount, duration);
            int flameCount = Mathf.Max(8, sparkCount / 2);
            PlayFlameBurst(position, Vector2.right, sparkColor, Color.white, flameCount, duration * 1.15f);
            PlayFlameBurst(position, Vector2.left, sparkColor, Color.white, flameCount, duration * 1.15f);
            PlayRing(position, ringColor, duration);
        }

        public static void PlayParry(Vector3 position, Vector2 direction, Color sparkColor, Color ringColor, int sparkCount, float duration)
        {
            position.z = 0f;
            PlayBidirectionalSpark(position, direction, sparkColor, sparkCount, duration);
            int flameCount = Mathf.Max(8, sparkCount / 2);
            PlayFlameBurst(position, direction, sparkColor, Color.white, flameCount, duration * 1.15f);
            PlayFlameBurst(position, -direction, sparkColor, Color.white, flameCount, duration * 1.15f);
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
            float glitterSeconds,
            int flameCount = -1,
            float effectScale = 1f)
        {
            position.z = 0f;
            float scale = Mathf.Max(0.25f, effectScale);
            int nextSparkCount = Mathf.Max(8, sparkCount);
            int nextGlitterCount = Mathf.Max(4, glitterCount);
            int nextFlameCount = flameCount > 0 ? flameCount : Mathf.Max(10, nextSparkCount / 2 + nextGlitterCount);
            float nextSparkSeconds = Mathf.Max(0.12f, sparkSeconds);
            float nextRingSeconds = Mathf.Max(0.16f, ringSeconds);
            float nextGlitterSeconds = Mathf.Max(0.1f, glitterSeconds);

            PlayBidirectionalSpark(position, direction, sparkColor, nextSparkCount, nextSparkSeconds, scale);
            PlayFlameBurst(
                position,
                direction,
                sparkColor,
                Color.Lerp(glitterColor, Color.white, 0.35f),
                nextFlameCount,
                Mathf.Max(nextSparkSeconds, nextGlitterSeconds) * 1.2f,
                scale);
            PlayFlameBurst(
                position,
                -direction,
                sparkColor,
                Color.Lerp(glitterColor, Color.white, 0.35f),
                nextFlameCount,
                Mathf.Max(nextSparkSeconds, nextGlitterSeconds) * 1.2f,
                scale);
            PlayRing(position, ringColor, nextRingSeconds, scale);
            PlayRingGlitter(position, direction, glitterColor, nextGlitterCount, nextGlitterSeconds, scale);
        }

        public static void PlayShotLine(Vector3 start, Vector3 end, Color color, float seconds, float width = 0.035f)
        {
            start.z = 0f;
            end.z = 0f;
            GameObject lineObject = new GameObject("ShotLineVfx");
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = width;
            line.endWidth = width * 0.45f;
            line.startColor = color;
            line.endColor = color;
            line.numCapVertices = 2;
            line.material = GetSpriteMaterial();
            line.sortingOrder = 73;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            Object.Destroy(lineObject, Mathf.Max(0.04f, seconds));
        }

        public static void PlayBulletImpact(Vector3 position, Vector2 direction, Color color)
        {
            PlayDirectionalSpark(position, direction, color, 10, 0.16f, 36f, 2.5f, 6f);
        }

        public static void PlayHogSmokeBurst(Vector3 position, Color baseColor, float effectScale = 1f, int smokeCount = 12)
        {
            position.z = 0f;
            float scale = Mathf.Max(0.15f, effectScale);
            GameObject smokeObject = new GameObject("HogSmokeVfx");
            smokeObject.transform.position = position;

            ParticleSystem particles = smokeObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.duration = 1.1f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.48f, 1.15f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f * scale, 0.28f * scale);
            main.startColor = WithAlpha(baseColor, Mathf.Min(baseColor.a, 0.72f));
            main.gravityModifier = -0.025f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.13f * scale;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new();
            Color startColor = WithAlpha(Color.Lerp(baseColor, Color.gray, 0.25f), Mathf.Min(baseColor.a, 0.58f));
            Color endColor = WithAlpha(startColor, 0f);
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(startColor, 0f),
                    new GradientColorKey(Color.Lerp(startColor, Color.white, 0.08f), 0.55f),
                    new GradientColorKey(startColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(startColor.a, 0f),
                    new GradientAlphaKey(startColor.a * 0.6f, 0.55f),
                    new GradientAlphaKey(endColor.a, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new(
                new Keyframe(0f, 0.65f),
                new Keyframe(0.45f, 1.1f),
                new Keyframe(1f, 1.45f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 73;
            renderer.sharedMaterial = GetSpriteMaterial();

            particles.Play();
            Color darkColor = WithAlpha(Color.Lerp(baseColor, Color.black, 0.35f), Mathf.Min(baseColor.a, 0.58f));
            Color lightColor = WithAlpha(Color.Lerp(baseColor, Color.gray, 0.45f), Mathf.Min(baseColor.a, 0.48f));
            int count = Mathf.Max(0, smokeCount);
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * Random.Range(0.02f, 0.2f) * scale;
                Vector2 velocity = new(Random.Range(-0.18f, 0.18f), Random.Range(0.16f, 0.62f));
                velocity *= scale;
                Color smokeColor = Color.Lerp(darkColor, lightColor, Random.Range(0.1f, 0.9f));

                ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
                {
                    position = position + (Vector3)offset,
                    velocity = velocity,
                    startLifetime = Random.Range(0.48f, 1.15f),
                    startSize = Random.Range(0.09f, 0.28f) * scale,
                    startColor = smokeColor
                };
                particles.Emit(emitParams, 1);
            }
        }

        public static void PlayHogExplosion(Vector3 position, Color color, float effectScale = 1f, int sparkCount = 18)
        {
            position.z = 0f;
            float scale = Mathf.Max(0.1f, effectScale);
            int count = Mathf.Max(0, sparkCount);
            Color coreColor = Color.Lerp(color, Color.white, 0.45f);
            coreColor.a = color.a;
            Color emberColor = Color.Lerp(color, new Color(1f, 0.32f, 0.05f, 1f), 0.45f);
            emberColor.a = color.a;
            Color ringColor = coreColor;
            ringColor.a *= 0.55f;

            PlayDirectionalSpark(position, Vector2.right, coreColor, count, 0.22f, 360f, 3.6f, 9.5f, scale);
            PlayFlameBurst(position, Vector2.up, emberColor, coreColor, Mathf.Max(6, count / 2), 0.2f, scale);
            PlayRing(position, ringColor, 0.16f, scale);
        }

        public static void PlayMuzzleFlash(Vector3 position, Vector2 direction, Color color, float effectScale = 1f)
        {
            position.z = 0f;
            float scale = Mathf.Max(0.25f, effectScale);
            Vector2 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            Color coreColor = Color.Lerp(color, Color.white, 0.62f);
            coreColor.a = 1f;
            Color flameColor = Color.Lerp(color, new Color(1f, 0.58f, 0.08f, 1f), 0.35f);
            flameColor.a = 1f;

            PlayDirectionalSpark(position, forward, coreColor, 5, 0.08f, 22f, 2.2f, 5.4f, 0.75f * scale);
            PlayFlameBurst(
                position + (Vector3)(forward * 0.06f * scale),
                forward,
                flameColor,
                coreColor,
                9,
                0.1f,
                0.7f * scale);
        }

        public static void PlayPlayerAttackImpact(
            Vector3 position,
            Vector2 direction,
            Color color,
            int sparkCount = 14,
            int backSparkCount = 6,
            int flameCount = 8,
            float effectScale = 0.65f)
        {
            Color flashColor = Color.Lerp(color, Color.white, 0.35f);
            flashColor.a = color.a;
            Color emberColor = Color.Lerp(color, new Color(1f, 0.72f, 0.12f, 1f), 0.55f);
            emberColor.a = color.a;
            Color ringColor = flashColor;
            ringColor.a *= 0.72f;

            PlayPlayerAttackImpact(
                position,
                direction,
                flashColor,
                emberColor,
                emberColor,
                ringColor,
                sparkCount,
                backSparkCount,
                flameCount,
                effectScale);
        }

        public static void PlayPlayerAttackImpact(
            Vector3 position,
            Vector2 direction,
            Color sparkColor,
            Color backSparkColor,
            Color flameColor,
            Color ringColor,
            int sparkCount = 14,
            int backSparkCount = 6,
            int flameCount = 8,
            float effectScale = 0.65f)
        {
            position.z = 0f;
            float scale = Mathf.Max(0f, effectScale);

            PlayDirectionalSpark(position, direction, sparkColor, sparkCount, 0.26f, 72f, 4.5f, 13f, scale);
            PlayDirectionalSpark(position, -direction, backSparkColor, backSparkCount, 0.22f, 112f, 1.8f, 6.5f, scale);
            PlayFlameBurst(position, direction, flameColor, sparkColor, flameCount, 0.24f, scale);
            PlayRing(position, ringColor, 0.18f, scale);
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

            trail.time = Mathf.Max(0.025f, seconds);
            trail.startWidth = Mathf.Max(0.01f, radius * Mathf.Clamp(widthMultiplier * 0.35f, 0.35f, 1.1f));
            trail.endWidth = 0f;
            trail.startColor = trailColor;
            trail.endColor = trailEndColor;
            trail.minVertexDistance = 0.01f;
            trail.numCornerVertices = 0;
            trail.numCapVertices = 0;
            trail.autodestruct = false;
            trail.emitting = true;
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
            float maxSpeed,
            float sizeScale = 1f)
        {
            float scale = Mathf.Max(0f, sizeScale);
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
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f * scale, 0.055f * scale);
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
                float speed = Random.Range(minSpeed, maxSpeed) * scale;
                ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * speed,
                    startLifetime = Random.Range(0.08f, Mathf.Max(0.09f, duration)),
                    startSize = Random.Range(0.018f, 0.055f) * scale,
                    startColor = color
                };
                particles.Emit(emitParams, 1);
            }
        }

        private static void PlayBidirectionalSpark(Vector3 position, Vector2 direction, Color color, int count, float duration, float sizeScale = 1f)
        {
            int forwardCount = Mathf.CeilToInt(Mathf.Max(0, count) * 0.5f);
            int backwardCount = Mathf.Max(0, count) - forwardCount;
            PlayDirectionalSpark(position, direction, color, forwardCount, duration, 38f, 4.6f, 10.5f, sizeScale);
            PlayDirectionalSpark(position, -direction, color, backwardCount, duration, 38f, 4.6f, 10.5f, sizeScale);
        }

        private static void PlayFlameBurst(
            Vector3 position,
            Vector2 direction,
            Color baseColor,
            Color coreColor,
            int count,
            float duration,
            float sizeScale = 1f)
        {
            float scale = Mathf.Max(0f, sizeScale);
            Vector2 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            GameObject flameObject = new GameObject("ParryFlameVfx");
            flameObject.transform.position = position;

            ParticleSystem particles = flameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.duration = Mathf.Max(0.01f, duration);
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, Mathf.Max(0.1f, duration));
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f * scale, 0.13f * scale);
            main.startColor = baseColor;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 72;
            renderer.sharedMaterial = GetSpriteMaterial();

            particles.Play();
            float baseAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
            int emitCount = Mathf.Max(0, count);
            for (int i = 0; i < emitCount; i++)
            {
                float angle = (baseAngle + Random.Range(-92f, 92f)) * Mathf.Deg2Rad;
                float speed = Random.Range(1.2f, 7.8f) * scale;
                Color particleColor = Color.Lerp(baseColor, coreColor, Random.Range(0.2f, 0.85f));
                particleColor.a *= Random.Range(0.72f, 1f);
                ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
                {
                    position = position + (Vector3)(Random.insideUnitCircle * 0.035f * scale),
                    velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * speed,
                    startLifetime = Random.Range(0.08f, Mathf.Max(0.1f, duration)),
                    startSize = Random.Range(0.035f, 0.13f) * scale,
                    startColor = particleColor
                };
                particles.Emit(emitParams, 1);
            }
        }

        private static void PlayRing(Vector3 position, Color color, float duration, float sizeScale = 1f)
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
            ring.Play(line, duration, color, sizeScale);
        }

        private static void PlayRingGlitter(Vector3 position, Vector2 direction, Color color, int count, float duration, float sizeScale = 1f)
        {
            float scale = Mathf.Max(0f, sizeScale);
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
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f * scale, 0.11f * scale);
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
                float radius = Random.Range(0.16f, 0.52f) * scale;
                Vector2 radial = (side * sideSign + forward * Random.Range(-0.35f, 0.35f)).normalized;
                Color particleColor = color;
                particleColor.a *= Random.Range(0.65f, 1f);
                ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
                {
                    position = position + (Vector3)(radial * radius),
                    velocity = radial * Random.Range(0.15f, 0.85f) * scale,
                    startLifetime = Random.Range(0.06f, Mathf.Max(0.07f, duration)),
                    startSize = Random.Range(0.035f, 0.11f) * scale,
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

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
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
