using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Week14.UI
{
    public sealed class TitleLightPulseController : MonoBehaviour
    {
        [Serializable]
        private sealed class LightGroup
        {
            [Tooltip("인스펙터에서 구분하기 위한 그룹 이름입니다.")]
            [SerializeField] private string groupName = string.Empty;
            [Tooltip("같은 타이밍에 함께 점등할 Light2D들입니다.")]
            [SerializeField] private Light2D[] lights = Array.Empty<Light2D>();

            public string GroupName => groupName;
            public Light2D[] Lights => lights;

            public bool HasAnyLight()
            {
                if (lights == null)
                {
                    return false;
                }

                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        [Header("Light 그룹")]
        [Tooltip("순서와 관계없이 무작위로 선택될 Light 그룹들입니다. 한 번에 하나의 그룹만 점등됩니다.")]
        [SerializeField] private LightGroup[] lightGroups = Array.Empty<LightGroup>();
        [Tooltip("바로 직전에 점등했던 그룹을 다음 선택 후보에서 제외할지 여부입니다.")]
        [SerializeField] private bool avoidImmediateRepeat = true;

        [Header("Intensity")]
        [Tooltip("점등하지 않는 상태의 기본 Intensity입니다.")]
        [SerializeField, Min(0f)] private float baseIntensity = 1f;
        [Tooltip("점등 피크에 곱할 배율입니다. 1이면 각 Light의 Radius Outer 값을 그대로 사용합니다.")]
        [SerializeField, Min(0f)] private float peakIntensityMultiplier = 1f;
        [Tooltip("활성화될 때 그룹에 포함된 모든 Light를 기본 Intensity로 초기화할지 여부입니다.")]
        [SerializeField] private bool resetGroupedLightsOnEnable = true;
        [Tooltip("비활성화될 때 그룹에 포함된 모든 Light를 기본 Intensity로 되돌릴지 여부입니다.")]
        [SerializeField] private bool resetGroupedLightsOnDisable = true;

        [Header("타이밍")]
        [Tooltip("활성화 후 첫 점등이 시작되기 전 대기하는 랜덤 시간 범위입니다.")]
        [SerializeField] private Vector2 initialDelaySecondsRange = new Vector2(0.35f, 0.9f);
        [Tooltip("기본 Intensity에서 피크 Intensity까지 빠르게 올라가는 시간입니다.")]
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.16f;
        [Tooltip("피크까지 올라가는 형태입니다. X는 진행률, Y는 보간 비율입니다.")]
        [SerializeField] private AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("선택된 그룹이 피크 Intensity를 유지하는 랜덤 시간 범위입니다.")]
        [SerializeField] private Vector2 peakHoldSecondsRange = new Vector2(2.4f, 3.8f);
        [Tooltip("피크 유지 후 기본 Intensity까지 서서히 감소하는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float fadeOutSeconds = 1.1f;
        [Tooltip("감소 형태입니다. X는 진행률, Y는 피크 유지 비율이며 기본값은 부드럽게 1에서 0으로 내려갑니다.")]
        [SerializeField] private AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [Tooltip("일시정지나 Time.timeScale의 영향을 받지 않고 계속 진행할지 여부입니다.")]
        [SerializeField] private bool useUnscaledTime = true;
        [Tooltip("활성화되면 자동으로 점등 루프를 시작할지 여부입니다.")]
        [SerializeField] private bool playOnEnable = true;

        private Coroutine pulseRoutine;
        private int previousGroupIndex = -1;

        private void OnEnable()
        {
            if (resetGroupedLightsOnEnable)
            {
                SetAllGroupedLightsToBase();
            }

            if (playOnEnable)
            {
                StartPulsing();
            }
        }

        private void OnDisable()
        {
            StopPulsing();

            if (resetGroupedLightsOnDisable)
            {
                SetAllGroupedLightsToBase();
            }
        }

        public void StartPulsing()
        {
            if (pulseRoutine != null)
            {
                return;
            }

            pulseRoutine = StartCoroutine(PulseRoutine());
        }

        public void StopPulsing()
        {
            if (pulseRoutine == null)
            {
                return;
            }

            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
        }

        private IEnumerator PulseRoutine()
        {
            float initialDelay = GetRandomRange(initialDelaySecondsRange);
            if (initialDelay > 0f)
            {
                yield return WaitForSeconds(initialDelay);
            }

            while (true)
            {
                int groupIndex = PickNextGroupIndex();
                if (groupIndex < 0)
                {
                    yield return null;
                    continue;
                }

                LightGroup group = lightGroups[groupIndex];
                previousGroupIndex = groupIndex;

                yield return FadeGroupToPeak(group);
                float holdSeconds = GetRandomRange(peakHoldSecondsRange);
                yield return WaitForSeconds(holdSeconds);
                yield return FadeGroupToBase(group);
            }
        }

        private IEnumerator FadeGroupToPeak(LightGroup group)
        {
            Light2D[] lights = group.Lights;
            float[] startIntensities = new float[lights.Length];
            float[] peakIntensities = new float[lights.Length];

            for (int i = 0; i < lights.Length; i++)
            {
                startIntensities[i] = lights[i] != null ? lights[i].intensity : baseIntensity;
                peakIntensities[i] = lights[i] != null ? GetPeakIntensity(lights[i]) : baseIntensity;
            }

            if (fadeInSeconds <= 0f)
            {
                SetGroupToPeak(group);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < fadeInSeconds)
            {
                elapsed += GetDeltaTime();
                float ratio = Mathf.Clamp01(elapsed / fadeInSeconds);
                float peakWeight = fadeInCurve != null ? Mathf.Clamp01(fadeInCurve.Evaluate(ratio)) : ratio;

                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null)
                    {
                        lights[i].intensity = Mathf.Lerp(startIntensities[i], peakIntensities[i], peakWeight);
                    }
                }

                yield return null;
            }

            SetGroupToPeak(group);
        }

        private IEnumerator FadeGroupToBase(LightGroup group)
        {
            Light2D[] lights = group.Lights;
            float[] startIntensities = new float[lights.Length];

            for (int i = 0; i < lights.Length; i++)
            {
                startIntensities[i] = lights[i] != null ? lights[i].intensity : baseIntensity;
            }

            float elapsed = 0f;
            while (elapsed < fadeOutSeconds)
            {
                elapsed += GetDeltaTime();
                float ratio = Mathf.Clamp01(elapsed / fadeOutSeconds);
                float peakWeight = fadeOutCurve != null ? Mathf.Clamp01(fadeOutCurve.Evaluate(ratio)) : 1f - ratio;

                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null)
                    {
                        lights[i].intensity = Mathf.Lerp(baseIntensity, startIntensities[i], peakWeight);
                    }
                }

                yield return null;
            }

            SetGroupToBase(group);
        }

        private int PickNextGroupIndex()
        {
            int validCount = CountValidGroups();
            if (validCount == 0)
            {
                return -1;
            }

            bool canAvoidPrevious = avoidImmediateRepeat && validCount > 1 && IsValidGroup(previousGroupIndex);
            int choice = UnityEngine.Random.Range(0, canAvoidPrevious ? validCount - 1 : validCount);

            for (int i = 0; i < lightGroups.Length; i++)
            {
                if (!IsValidGroup(i) || (canAvoidPrevious && i == previousGroupIndex))
                {
                    continue;
                }

                if (choice == 0)
                {
                    return i;
                }

                choice--;
            }

            return -1;
        }

        private int CountValidGroups()
        {
            if (lightGroups == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < lightGroups.Length; i++)
            {
                if (IsValidGroup(i))
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsValidGroup(int index)
        {
            return lightGroups != null
                && index >= 0
                && index < lightGroups.Length
                && lightGroups[index] != null
                && lightGroups[index].HasAnyLight();
        }

        private void SetAllGroupedLightsToBase()
        {
            if (lightGroups == null)
            {
                return;
            }

            for (int i = 0; i < lightGroups.Length; i++)
            {
                if (lightGroups[i] != null)
                {
                    SetGroupToBase(lightGroups[i]);
                }
            }
        }

        private void SetGroupToPeak(LightGroup group)
        {
            Light2D[] lights = group.Lights;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                {
                    lights[i].intensity = GetPeakIntensity(lights[i]);
                }
            }
        }

        private void SetGroupToBase(LightGroup group)
        {
            Light2D[] lights = group.Lights;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                {
                    lights[i].intensity = baseIntensity;
                }
            }
        }

        private float GetPeakIntensity(Light2D light)
        {
            return Mathf.Max(0f, light.pointLightOuterRadius * peakIntensityMultiplier);
        }

        private float GetDeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }

        private object WaitForSeconds(float seconds)
        {
            float clampedSeconds = Mathf.Max(0f, seconds);
            return useUnscaledTime ? new WaitForSecondsRealtime(clampedSeconds) : new WaitForSeconds(clampedSeconds);
        }

        private static float GetRandomRange(Vector2 range)
        {
            return UnityEngine.Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
        }
    }
}
