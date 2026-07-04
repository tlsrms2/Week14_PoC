using UnityEngine;

namespace Week14.Enemy
{
    public static class EnemyTimeScale
    {
        private static float currentMultiplier = 1f;
        private static float expiresAt = -1f;

        public static float Current
        {
            get
            {
                if (expiresAt >= 0f && Time.time >= expiresAt)
                {
                    currentMultiplier = 1f;
                    expiresAt = -1f;
                }

                return currentMultiplier;
            }
        }

        public static float DeltaTime => Time.deltaTime * Current;

        // Time.time 기반 절대 마감 시각(destroyAt 등)에 매 프레임 더해주면, 느려진 만큼 마감 시각이
        // 뒤로 밀려나 "이동은 느려졌는데 사라지는 시점은 그대로"인 괴리를 없앨 수 있다.
        public static float DeltaTimeDebt => Time.deltaTime * (1f - Current);

        public static void SetTemporary(float multiplier, float durationSeconds)
        {
            currentMultiplier = Mathf.Max(0f, multiplier);
            expiresAt = durationSeconds > 0f ? Time.time + durationSeconds : -1f;
        }
    }
}
