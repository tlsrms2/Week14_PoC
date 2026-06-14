using UnityEngine;

namespace Week14.Combat
{
    public static class ExecutionVfx
    {
        private static Material particleMaterial;

        public static void PlayImpact(Vector3 position, Color color, int count, float duration)
        {
            PlayImpact(position, Vector2.right, color, count, duration);
        }

        public static void PlayImpact(Vector3 position, Vector2 direction, Color color, int count, float duration)
        {
            Vector2 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            Vector3 emitPosition = position + (Vector3)(forward * 0.08f);
            ParticleSystem particles = CreateParticleSystem("ExecutionImpactVfx", emitPosition, duration);
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.22f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.08f);
            main.startColor = color;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.03f;

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 60;
            AssignParticleMaterial(renderer);

            int particleCount = Mathf.Max(0, count);
            for (int i = 0; i < particleCount; i++)
            {
                float angle = Random.Range(-12f, 12f);
                float speed = Random.Range(5f, 8f);
                Vector3 velocity = Quaternion.Euler(0f, 0f, angle) * (Vector3)forward * speed;
                ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
                {
                    position = emitPosition,
                    velocity = velocity,
                    startLifetime = Random.Range(0.22f, 0.48f),
                    startSize = Random.Range(0.055f, 0.1f),
                    startColor = color
                };
                particles.Emit(emitParams, 1);
            }

            particles.Play();
        }

        public static void PlayAbsorb(Vector3 from, Transform to, Color color, int count, float duration)
        {
            if (to == null)
            {
                return;
            }

            ParticleSystem particles = CreateParticleSystem("ExecutionAbsorbVfx", from, duration);
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.48f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.14f;

            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = false;
            particles.gameObject.AddComponent<AbsorbToTarget>().Initialize(particles, to);

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 61;
            AssignParticleMaterial(renderer);

            int particleCount = Mathf.Max(0, count);
            for (int i = 0; i < particleCount; i++)
            {
                Vector2 spreadDirection = Random.insideUnitCircle.normalized;
                if (spreadDirection.sqrMagnitude <= 0.0001f)
                {
                    spreadDirection = Vector2.right;
                }

                ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
                {
                    position = from,
                    velocity = (Vector3)(spreadDirection * Random.Range(1.4f, 3.6f)),
                    startLifetime = Random.Range(0.36f, 0.72f),
                    startSize = Random.Range(0.03f, 0.1f),
                    startColor = color
                };
                particles.Emit(emitParams, 1);
            }

            particles.Play();
        }

        private sealed class AbsorbToTarget : MonoBehaviour
        {
            private const float ArriveDistance = 0.01f;
            private const float PullStrength = 12f;
            private const float SpreadSeconds = 0.14f;

            private ParticleSystem particles;
            private Transform target;
            private ParticleSystem.Particle[] particleBuffer;

            public void Initialize(ParticleSystem nextParticles, Transform nextTarget)
            {
                particles = nextParticles;
                target = nextTarget;
            }

            private void LateUpdate()
            {
                if (particles == null || target == null)
                {
                    return;
                }

                int maxParticles = Mathf.Max(1, particles.main.maxParticles);
                if (particleBuffer == null || particleBuffer.Length < maxParticles)
                {
                    particleBuffer = new ParticleSystem.Particle[maxParticles];
                }

                int count = particles.GetParticles(particleBuffer);
                for (int i = 0; i < count; i++)
                {
                    float age = particleBuffer[i].startLifetime - particleBuffer[i].remainingLifetime;
                    if (age < SpreadSeconds)
                    {
                        continue;
                    }

                    Vector3 toTarget = target.position - particleBuffer[i].position;
                    float distance = toTarget.magnitude;
                    if (distance <= ArriveDistance)
                    {
                        particleBuffer[i].remainingLifetime = 0f;
                        continue;
                    }

                    Vector3 desiredVelocity = toTarget / distance * Mathf.Lerp(7f, 2.5f, Mathf.Clamp01(distance / 3f));
                    particleBuffer[i].velocity = Vector3.Lerp(
                        particleBuffer[i].velocity,
                        desiredVelocity,
                        Time.deltaTime * PullStrength);
                }

                particles.SetParticles(particleBuffer, count);
            }
        }

        private static ParticleSystem CreateParticleSystem(string name, Vector3 position, float duration)
        {
            GameObject particleObject = new GameObject(name);
            particleObject.transform.position = position;

            ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.duration = Mathf.Max(0.01f, duration);
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Destroy;

            return particles;
        }

        private static void AssignParticleMaterial(ParticleSystemRenderer renderer)
        {
            if (particleMaterial != null)
            {
                renderer.sharedMaterial = particleMaterial;
                return;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                particleMaterial = new Material(shader);
                renderer.sharedMaterial = particleMaterial;
            }
        }
    }
}
