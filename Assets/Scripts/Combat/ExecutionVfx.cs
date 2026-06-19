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
