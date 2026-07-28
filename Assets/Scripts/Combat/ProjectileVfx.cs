using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    public static class ProjectileVfx
    {
        private const float DefaultPrefabLifetimeSeconds = 1f;
        private const float MinimumPrefabLifetimeSeconds = 0.05f;
        private static Material spriteMaterial;
        private static Sprite circleSprite;

        public static void ApplyVisibility(GameObject owner, Color color, float radius, float trailSeconds, float trailWidthMultiplier)
        {
            if (owner == null)
            {
                return;
            }

            RemoveFireballGlow(owner.transform);
            EnsureTrail(owner, color, radius, trailSeconds, trailWidthMultiplier);
        }

        public static GameObject PlayPrefab(
            GameObject prefab,
            Vector3 position,
            Vector2 direction,
            float scale = 1f)
        {
            return PlayPrefab(prefab, position, direction, null, scale);
        }

        public static GameObject PlayPrefab(
            GameObject prefab,
            Vector3 position,
            Vector2 direction,
            Transform followTarget,
            float scale = 1f,
            bool followRotation = true)
        {
            Vector2 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            float angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
            return PlayPrefab(
                prefab,
                position,
                Quaternion.Euler(0f, 0f, angle),
                followTarget,
                scale,
                followRotation);
        }

        public static GameObject PlayPrefab(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Transform followTarget,
            float scale = 1f,
            bool followRotation = true,
            float playbackSpeed = 1f)
        {
            if (prefab == null || scale <= 0f)
            {
                return null;
            }

            float safePlaybackSpeed = Mathf.Max(0.01f, playbackSpeed);
            position.z = 0f;
            GameObject instance = Object.Instantiate(prefab, position, rotation);
            if (followTarget != null)
            {
                VfxTransformFollower follower = instance.AddComponent<VfxTransformFollower>();
                follower.Initialize(followTarget, followRotation);
            }

            instance.transform.localScale *= scale;
            BossSorting.ApplyToChildren(instance);

            ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            float lifetimeSeconds = 0f;
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem particle = particles[i];
                if (particle == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = particle.main;
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                main.loop = false;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.stopAction = ParticleSystemStopAction.None;
                main.simulationSpeed *= safePlaybackSpeed;
                float simulationSpeed = main.simulationSpeed;
                float particleLifetime = simulationSpeed > 0.0001f
                    ? (main.startDelay.constantMax + main.duration + main.startLifetime.constantMax)
                        / simulationSpeed
                    : DefaultPrefabLifetimeSeconds;
                lifetimeSeconds = Mathf.Max(lifetimeSeconds, particleLifetime);
            }

            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i] != null)
                {
                    particles[i].Play(false);
                }
            }

            Animator[] animators = instance.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                RuntimeAnimatorController controller = animator != null ? animator.runtimeAnimatorController : null;
                if (controller == null)
                {
                    continue;
                }

                animator.speed *= safePlaybackSpeed;
                float animatorSpeed = Mathf.Abs(animator.speed);
                float animatorLifetimeSeconds = 0f;
                if (animatorSpeed <= 0.0001f)
                {
                    lifetimeSeconds = Mathf.Max(lifetimeSeconds, DefaultPrefabLifetimeSeconds);
                    continue;
                }

                if (animator.isActiveAndEnabled)
                {
                    animator.Update(0f);
                    for (int layerIndex = 0; layerIndex < animator.layerCount; layerIndex++)
                    {
                        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);
                        float stateSpeed = Mathf.Abs(stateInfo.speed * stateInfo.speedMultiplier);
                        float effectiveSpeed = animatorSpeed * stateSpeed;
                        if (effectiveSpeed <= 0.0001f)
                        {
                            animatorLifetimeSeconds = Mathf.Max(
                                animatorLifetimeSeconds,
                                DefaultPrefabLifetimeSeconds);
                            continue;
                        }

                        AnimatorClipInfo[] clipInfos = animator.GetCurrentAnimatorClipInfo(layerIndex);
                        for (int clipIndex = 0; clipIndex < clipInfos.Length; clipIndex++)
                        {
                            AnimationClip clip = clipInfos[clipIndex].clip;
                            if (clip != null)
                            {
                                animatorLifetimeSeconds = Mathf.Max(
                                    animatorLifetimeSeconds,
                                    clip.length / effectiveSpeed);
                            }
                        }
                    }
                }

                if (animatorLifetimeSeconds <= 0f && controller.animationClips.Length > 0)
                {
                    AnimationClip fallbackClip = controller.animationClips[0];
                    animatorLifetimeSeconds = fallbackClip != null ? fallbackClip.length / animatorSpeed : 0f;
                }

                lifetimeSeconds = Mathf.Max(lifetimeSeconds, animatorLifetimeSeconds);
            }

            float destroyAfterSeconds = lifetimeSeconds > 0f
                ? Mathf.Max(MinimumPrefabLifetimeSeconds, lifetimeSeconds)
                : DefaultPrefabLifetimeSeconds;
            Object.Destroy(instance, destroyAfterSeconds);
            return instance;
        }

        public static GameObject PlayAnchoredPrefab(
            GameObject prefab,
            Transform spawnParent,
            Vector2 direction,
            Vector2 rightFacingLocalOffset,
            float rotationOffsetDegrees,
            Vector3 localScale,
            float playbackSpeed = 1f,
            int sortingOrder = 0)
        {
            if (prefab == null || spawnParent == null)
            {
                return null;
            }

            GameObject instance = PlayPrefab(
                prefab,
                spawnParent.position,
                Quaternion.identity,
                null,
                1f,
                true,
                playbackSpeed);
            if (instance == null)
            {
                return null;
            }

            instance.transform.SetParent(spawnParent, false);
            Vector2 worldForward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            Vector3 localForward3 = spawnParent.InverseTransformDirection(worldForward);
            Vector2 localForward = new Vector2(localForward3.x, localForward3.y).normalized;
            float localAngle = Mathf.Atan2(localForward.y, localForward.x) * Mathf.Rad2Deg;
            Quaternion directionRotation = Quaternion.Euler(0f, 0f, localAngle);
            instance.transform.localPosition = directionRotation * rightFacingLocalOffset;
            instance.transform.localRotation = Quaternion.Euler(0f, 0f, localAngle + rotationOffsetDegrees);
            instance.transform.localScale = localScale;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].sortingOrder = sortingOrder;
                }
            }

            return instance;
        }

        public static GameObject PlayAnchoredBeamPrefab(
            GameObject prefab,
            Transform spawnParent,
            Vector3 origin,
            Vector2 direction,
            float beamLength,
            bool lengthAlongLocalY,
            float muzzleOffset,
            float rotationOffsetDegrees,
            float playbackSpeed = 1f,
            int sortingOrder = 0)
        {
            if (prefab == null || spawnParent == null || beamLength <= 0f)
            {
                return null;
            }

            origin.z = 0f;
            Vector2 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            GameObject instance = PlayPrefab(
                prefab,
                origin,
                Quaternion.identity,
                null,
                1f,
                true,
                playbackSpeed);
            if (instance == null)
            {
                return null;
            }

            Vector2 sourceAxis = lengthAlongLocalY ? Vector2.up : Vector2.right;
            float sourceLength = TryGetRendererProjectionSpan(instance, sourceAxis, out float sourceMin, out float sourceMax)
                ? sourceMax - sourceMin
                : 1f;
            float lengthScale = Mathf.Max(0.01f, beamLength) / Mathf.Max(0.0001f, sourceLength);
            Vector3 stretchedScale = instance.transform.localScale;
            if (lengthAlongLocalY)
            {
                stretchedScale.y *= lengthScale;
            }
            else
            {
                stretchedScale.x *= lengthScale;
            }

            instance.transform.localScale = stretchedScale;
            float directionAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
            float axisCorrectionDegrees = lengthAlongLocalY ? -90f : 0f;
            instance.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                directionAngle + axisCorrectionDegrees + rotationOffsetDegrees);

            Vector3 desiredStart = origin + (Vector3)(forward * muzzleOffset);
            instance.transform.position = desiredStart;
            if (TryGetRendererProjectionSpan(instance, forward, out float stretchedMin, out _))
            {
                float desiredStartProjection = Vector2.Dot(desiredStart, forward);
                instance.transform.position += (Vector3)(forward * (desiredStartProjection - stretchedMin));
            }

            // 월드 길이와 총구 정렬을 먼저 확정한 뒤 부모를 연결해, 총의 반전/회전 상태와 무관하게
            // 발사 순간에는 실제 판정 사거리와 정확히 같은 길이로 보이게 합니다.
            instance.transform.SetParent(spawnParent, true);

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].sortingOrder = sortingOrder;
                }
            }

            return instance;
        }

        private static bool TryGetRendererProjectionSpan(
            GameObject owner,
            Vector2 axis,
            out float minProjection,
            out float maxProjection)
        {
            minProjection = float.PositiveInfinity;
            maxProjection = float.NegativeInfinity;
            if (owner == null || axis.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            Vector2 normalizedAxis = axis.normalized;
            Renderer[] renderers = owner.GetComponentsInChildren<Renderer>(true);
            bool foundRenderer = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                float projectedCenter = Vector2.Dot(bounds.center, normalizedAxis);
                float projectedRadius = Mathf.Abs(normalizedAxis.x) * bounds.extents.x
                    + Mathf.Abs(normalizedAxis.y) * bounds.extents.y;
                minProjection = Mathf.Min(minProjection, projectedCenter - projectedRadius);
                maxProjection = Mathf.Max(maxProjection, projectedCenter + projectedRadius);
                foundRenderer = true;
            }

            return foundRenderer && maxProjection > minProjection;
        }

        public static GameObject PlayShotLine(
            Vector3 start,
            Vector3 end,
            Color color,
            float seconds,
            float width = 0.035f,
            int sortingOrder = 73,
            int? sortingLayerId = null,
            bool autoDestroy = true)
        {
            start.z = 0f;
            end.z = 0f;
            GameObject lineObject = new GameObject("ShotLineVfx");
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            BossSorting.Apply(line);
            if (sortingLayerId.HasValue)
            {
                line.sortingLayerID = sortingLayerId.Value;
            }

            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = width;
            line.endWidth = width * 0.45f;
            line.startColor = color;
            line.endColor = color;
            line.numCapVertices = 2;
            line.material = GetSpriteMaterial();
            line.sortingOrder = sortingOrder;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            if (autoDestroy)
            {
                Object.Destroy(lineObject, Mathf.Max(0.04f, seconds));
            }

            return lineObject;
        }

        public static GameObject PlayCircleFlash(
            Vector3 position,
            float diameter,
            Color color,
            int sortingOrder = 74,
            int? sortingLayerId = null)
        {
            position.z = 0f;
            GameObject circleObject = new GameObject("CircleFlashVfx");
            circleObject.transform.position = position;
            circleObject.transform.localScale =
                Vector3.one * Mathf.Max(0.01f, diameter);

            SpriteRenderer renderer =
                circleObject.AddComponent<SpriteRenderer>();
            BossSorting.Apply(renderer);
            if (sortingLayerId.HasValue)
            {
                renderer.sortingLayerID = sortingLayerId.Value;
            }

            renderer.sprite = CreateCircleSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            renderer.sharedMaterial = GetSpriteMaterial();
            return circleObject;
        }

        // 총검 등 반원 범위 판정을 순간적으로 보여주는 꽉 찬(면이 채워진) 플래시입니다.
        // duration 동안 알파가 빠지며 사라집니다. 삼각팬(중심 + 호) 메쉬로 내부를 채웁니다.
        public static void PlaySemicircleFlash(Vector3 origin, Vector2 direction, float radius, Color color, float duration)
        {
            if (radius <= 0f)
            {
                return;
            }

            origin.z = 0f;
            Vector2 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            float baseAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;

            const int arcSegments = 20;
            GameObject flashObject = new GameObject("SemicircleFlashVfx");
            flashObject.transform.position = origin;

            MeshFilter meshFilter = flashObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = flashObject.AddComponent<MeshRenderer>();
            BossSorting.Apply(meshRenderer);
            meshRenderer.sharedMaterial = GetSpriteMaterial();
            meshRenderer.sortingOrder = 69;

            Vector3[] vertices = new Vector3[arcSegments + 2];
            vertices[0] = Vector3.zero;
            for (int i = 0; i <= arcSegments; i++)
            {
                float t = (float)i / arcSegments;
                float angleRad = (baseAngle - 90f + t * 180f) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f) * radius;
            }

            int[] triangles = new int[arcSegments * 3];
            for (int i = 0; i < arcSegments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            Mesh mesh = new Mesh { name = "SemicircleFlashMesh" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            meshFilter.mesh = mesh;

            SemicircleFlashVfx flash = flashObject.AddComponent<SemicircleFlashVfx>();
            flash.Play(mesh, duration, color);
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
            BossSorting.Apply(renderer);
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
            BossSorting.Apply(trail);
            trail.sortingOrder = 18;
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
            if (circleSprite != null)
            {
                return circleSprite;
            }

            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = distance <= radius ? 1f : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            circleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);
            return circleSprite;
        }
    }
}
