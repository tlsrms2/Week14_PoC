using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Week14.UI;

namespace Week14.Combat
{
    public sealed class LowBulletVignetteEffect : MonoBehaviour
    {
        [SerializeField] private BulletGauge target;
        [SerializeField] private bool bindPlayerOnStart = true;
        [Tooltip("총알이 0일 때 표시할 비네트 색입니다.")]
        [SerializeField] private Color vignetteColor = new(0.95f, 0.05f, 0.05f, 1f);
        [Tooltip("비네트가 줄어들었을 때(가장 약할 때) 강도입니다.")]
        [SerializeField, Range(0f, 1f)] private float minIntensity = 0.25f;
        [Tooltip("비네트가 늘어났을 때(가장 강할 때) 강도입니다.")]
        [SerializeField, Range(0f, 1f)] private float maxIntensity = 0.55f;
        [Tooltip("비네트가 약->강->약으로 한 번 맥동하는 데 걸리는 시간입니다.")]
        [SerializeField, Min(0.05f)] private float pulseSeconds = 0.9f;
        [Tooltip("총알이 0이 되거나 회복될 때 비네트가 나타나고 사라지는 데 걸리는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.25f;
        [Tooltip("비네트 가장자리의 부드러움입니다.")]
        [SerializeField, Range(0f, 1f)] private float smoothness = 0.35f;

        private Volume volume;
        private Vignette vignette;
        private float currentWeight;
        private bool isActive;

        private void Awake()
        {
            EnsureVolume();
        }

        private void OnEnable()
        {
            TryBindPlayer();
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            if (volume != null && volume.profile != null)
            {
                Destroy(volume.profile);
            }
        }

        private void Update()
        {
            if (bindPlayerOnStart && PlayerCombatController.Active != null
                && target != PlayerCombatController.Active.Bullets)
            {
                Unsubscribe();
                TryBindPlayer();
                Subscribe();
                Refresh();
            }

            Tick(Time.unscaledDeltaTime);
        }

        public void SetTarget(BulletGauge nextTarget)
        {
            if (target == nextTarget)
            {
                return;
            }

            Unsubscribe();
            target = nextTarget;
            Subscribe();
            Refresh();
        }

        private void EnsureVolume()
        {
            if (volume != null)
            {
                return;
            }

            GameObject volumeObject = new("LowBulletVignetteVolume");
            volumeObject.transform.SetParent(transform, false);
            volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            volume.weight = 0f;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vignette = profile.Add<Vignette>(true);
            vignette.color.overrideState = true;
            vignette.intensity.overrideState = true;
            vignette.smoothness.overrideState = true;
            vignette.rounded.overrideState = true;
            vignette.color.value = vignetteColor;
            vignette.intensity.value = minIntensity;
            vignette.smoothness.value = smoothness;
            vignette.rounded.value = false;

            volume.profile = profile;
        }

        private void TryBindPlayer()
        {
            if (!bindPlayerOnStart || PlayerCombatController.Active == null)
            {
                return;
            }

            target = PlayerCombatController.Active.Bullets;
        }

        private void Subscribe()
        {
            if (target == null)
            {
                return;
            }

            target.Changed += HandleChanged;
        }

        private void Unsubscribe()
        {
            if (target == null)
            {
                return;
            }

            target.Changed -= HandleChanged;
        }

        private void HandleChanged(int current, int max)
        {
            isActive = current <= 0;
        }

        private void Refresh()
        {
            isActive = target != null && target.IsEmpty;
        }

        private void Tick(float deltaTime)
        {
            if (volume == null || vignette == null)
            {
                return;
            }

            float targetWeight = isActive && !GameModalState.BlocksGameplayInput ? 1f : 0f;
            float fadeSpeed = 1f / Mathf.Max(0.01f, fadeSeconds);
            currentWeight = Mathf.MoveTowards(currentWeight, targetWeight, fadeSpeed * deltaTime);
            volume.weight = currentWeight;

            if (currentWeight <= 0f)
            {
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / pulseSeconds));
            vignette.intensity.value = Mathf.Lerp(minIntensity, maxIntensity, pulse);
        }
    }
}
