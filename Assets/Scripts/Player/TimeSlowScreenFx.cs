using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Week14.Combat
{
    // 화면 전체에 색 필터를 씌우는 효과입니다. PlayerOverlayCameraTag가 만들어 둔
    // 오버레이 카메라는 이 Volume의 영향을 받지 않으므로, 플레이어(와 잔상)는 제외한 채
    // 나머지 화면에만 필터가 적용됩니다.
    public static class TimeSlowScreenFx
    {
        private static Volume volume;
        private static ColorAdjustments colorAdjustments;
        private static TimeSlowScreenFxDriver driver;
        private static Coroutine fadeRoutine;

        public static void Show(float durationSeconds, float fadeInSeconds, float fadeOutSeconds, Color tintColor)
        {
            EnsureSetup();

            colorAdjustments.colorFilter.value = Color.Lerp(Color.white, new Color(tintColor.r, tintColor.g, tintColor.b, 1f), tintColor.a);

            if (fadeRoutine != null)
            {
                driver.StopCoroutine(fadeRoutine);
            }

            fadeRoutine = driver.StartCoroutine(FadeRoutine(durationSeconds, fadeInSeconds, fadeOutSeconds));
        }

        private static void EnsureSetup()
        {
            if (driver != null)
            {
                return;
            }

            GameObject driverObject = new GameObject("TimeSlowScreenFxDriver") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(driverObject);
            driver = driverObject.AddComponent<TimeSlowScreenFxDriver>();

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            colorAdjustments = profile.Add<ColorAdjustments>(true);

            volume = driverObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            volume.weight = 0f;
            volume.profile = profile;
        }

        private static IEnumerator FadeRoutine(float durationSeconds, float fadeInSeconds, float fadeOutSeconds)
        {
            float holdSeconds = Mathf.Max(0f, durationSeconds - fadeInSeconds - fadeOutSeconds);

            yield return Fade(volume.weight, 1f, fadeInSeconds);

            float elapsed = 0f;
            while (elapsed < holdSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            yield return Fade(volume.weight, 0f, fadeOutSeconds);
            fadeRoutine = null;
        }

        private static IEnumerator Fade(float from, float to, float seconds)
        {
            if (seconds <= 0f)
            {
                volume.weight = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                volume.weight = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }

            volume.weight = to;
        }

        private sealed class TimeSlowScreenFxDriver : MonoBehaviour
        {
        }
    }
}
