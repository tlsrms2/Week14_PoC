using System;
using UnityEngine;

namespace Week14.Enemy
{
    public static class EnemyTimeScale
    {
        private static float currentMultiplier = 1f;
        private static float expiresAt = -1f;
        private static EnemyTimeScaleDriver driver;

        // 배율이 자연 만료(지속시간 종료)되는 순간 한 번 호출됩니다. 스킬 쿨타임처럼
        // "효과가 실제로 끝난 시점"이 필요한 곳에서 구독해서 사용합니다.
        public static event Action Expired;

        public static float Current
        {
            get
            {
                CheckExpiry();
                return currentMultiplier;
            }
        }

        public static float DeltaTime => Time.deltaTime * Current;
        public static float FixedDeltaTime => Time.fixedDeltaTime * Current;

        // Time.time 기반 절대 마감 시각(destroyAt 등)에 매 프레임 더해주면, 느려진 만큼 마감 시각이
        // 뒤로 밀려나 "이동은 느려졌는데 사라지는 시점은 그대로"인 괴리를 없앨 수 있다.
        public static float DeltaTimeDebt => Time.deltaTime * (1f - Current);

        public static void SetTemporary(float multiplier, float durationSeconds)
        {
            currentMultiplier = Mathf.Max(0f, multiplier);
            expiresAt = durationSeconds > 0f ? Time.time + durationSeconds : -1f;
            EnsureDriver();
        }

        private static void CheckExpiry()
        {
            if (expiresAt >= 0f && Time.time >= expiresAt)
            {
                currentMultiplier = 1f;
                expiresAt = -1f;
                Expired?.Invoke();
            }
        }

        // Current를 아무도 폴링하지 않는 상황(적이 없는 방 등)에서도 만료 이벤트가 정확한
        // 시점에 발생하도록, 전용 드라이버가 매 프레임 만료 여부를 직접 확인합니다.
        private static void EnsureDriver()
        {
            if (driver != null)
            {
                return;
            }

            GameObject go = new GameObject("EnemyTimeScaleDriver") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            driver = go.AddComponent<EnemyTimeScaleDriver>();
        }

        private sealed class EnemyTimeScaleDriver : MonoBehaviour
        {
            private void Update()
            {
                CheckExpiry();
            }
        }
    }
}
