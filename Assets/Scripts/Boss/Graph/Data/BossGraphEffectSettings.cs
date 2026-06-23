using System;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class BossGraphParticleEffectSettings
    {
        [SerializeField] private bool enabled;
        [SerializeField] private Color color = Color.white;
        [SerializeField, Min(0.1f)] private float scale = 1f;
        [SerializeField, Min(0)] private int count = 12;

        public bool Enabled => enabled;
        public Color Color => color;
        public float Scale => scale;
        public int Count => count;
    }

    [Serializable]
    public sealed class BossGraphCameraShakeSettings
    {
        [SerializeField] private bool enabled;
        [SerializeField, Min(0f)] private float seconds = 0.14f;
        [SerializeField, Min(0f)] private float distance = 0.22f;
        [SerializeField, Min(0f)] private float frequency = 0.12f;

        public bool Enabled => enabled;
        public float Seconds => seconds;
        public float Distance => distance;
        public float Frequency => frequency;
    }

    [Serializable]
    public sealed class BossGraphEffectSettings
    {
        [SerializeField] private BossGraphParticleEffectSettings explosion = new();
        [SerializeField] private BossGraphParticleEffectSettings smoke = new();
        [SerializeField, Min(0.01f)] private float smokeInterval = 0.12f;
        [SerializeField] private BossGraphParticleEffectSettings muzzleFlash = new();
        [SerializeField] private BossGraphCameraShakeSettings cameraShake = new();

        public BossGraphParticleEffectSettings Explosion => explosion;
        public BossGraphParticleEffectSettings Smoke => smoke;
        public float SmokeInterval => smokeInterval;
        public BossGraphParticleEffectSettings MuzzleFlash => muzzleFlash;
        public BossGraphCameraShakeSettings CameraShake => cameraShake;
    }
}
