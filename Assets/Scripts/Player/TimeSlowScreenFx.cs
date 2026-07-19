using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Week14.Combat
{
    // 화면 전체에 색 필터를 씌우는 효과입니다. PlayerOverlayCameraTag가 만들어 둔
    // 오버레이 카메라는 이 Volume의 영향을 받지 않으므로, 플레이어(와 잔상)는 제외한 채
    // 나머지 화면에만 필터가 적용됩니다.
    //
    // Volume/driver는 DontDestroyOnLoad라서 씬이 바뀌어도 죽지 않고 남아있다.
    // 씬 전환 도중(페이드인/유지 중)에 새 씬이 로드되면 weight가 0으로 안 돌아온 채
    // 그대로 남아 새 씬에서도 화면이 계속 물들어 보이므로, 씬이 로드될 때마다 강제로 초기화한다.
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

        // 플레이어/보스 사망 연출처럼 화면이 항상 원래 색으로 보여야 하는 컷씬 직전에 호출해서
        // 진행 중이던 틴트를 즉시 걷어낸다.
        public static void CancelImmediate()
        {
            if (driver == null)
            {
                return;
            }

            if (fadeRoutine != null)
            {
                driver.StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            colorAdjustments.colorFilter.value = Color.white;
            volume.weight = 0f;
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

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (fadeRoutine != null)
            {
                driver.StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            colorAdjustments.colorFilter.value = Color.white;
            volume.weight = 0f;
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
