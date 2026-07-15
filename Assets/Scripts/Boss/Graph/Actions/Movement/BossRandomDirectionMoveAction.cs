using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class BossRandomDirectionMoveAction : BossAction, ISerializationCallbackReceiver, IBossActionDurationProvider
    {
        private const string GroundLayerName = "Ground";

        [SerializeField, Min(0.01f)] private float distance = 2f;
        [SerializeField, Min(0.01f)] private float duration = 0.3f;
        [Tooltip("켜면 아래 각도 범위 대신, 플레이어를 바라보는 방향 기준 좌/우(90도) 중 하나를 무작위로 골라 이동합니다.")]
        [SerializeField] private bool strafeAroundPlayer;
        [Tooltip("Strafe Around Player가 꺼져 있을 때 사용하는 랜덤 방향의 각도 범위입니다. 0~360이면 전방위, 예를 들어 -60~60이면 보스의 현재 오른쪽 기준 부채꼴 범위로 제한됩니다.")]
        [SerializeField] private float minAngleDegrees;
        [SerializeField] private float maxAngleDegrees = 360f;
        [SerializeField] private AnimationCurve speedCurve;
        [SerializeField, HideInInspector] private bool speedCurveInitialized;
        [SerializeField] private bool stopWhenFinished = true;
        [Tooltip("켜면 이동 목적지가 Ground 레이어 위가 아닐 때 다른 방향을 다시 뽑습니다.")]
        [SerializeField] private bool requireGroundDestination = true;
        [SerializeField, Min(0.01f)] private float groundProbeRadius = 0.2f;
        [SerializeField, Min(1)] private int groundRetryAttempts = 8;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null || context.Boss == null || context.Boss.Body == null)
            {
                yield break;
            }

            Vector2 direction = GetDirection(context);
            float baseSpeed = distance / Mathf.Max(0.01f, duration);
            AnimationCurve curve = GetSpeedCurve();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                float t = Mathf.Clamp01(elapsed / duration);
                context.Boss.SetMovementVelocity(direction * (baseSpeed * curve.Evaluate(t)));
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            if (stopWhenFinished)
            {
                context.Stop();
            }
        }

        private Vector2 GetDirection(BossActionContext context)
        {
            Vector2 direction = GetRandomDirection(context);
            if (!requireGroundDestination)
            {
                return direction;
            }

            Vector2 origin = context.OriginPosition;
            for (int i = 0; i < groundRetryAttempts && !IsGroundPosition(origin + direction * distance); i++)
            {
                direction = GetRandomDirection(context);
            }

            return direction;
        }

        private Vector2 GetRandomDirection(BossActionContext context)
        {
            if (strafeAroundPlayer)
            {
                Vector2 toPlayer = context.GetDirectionToPlayer(context.OriginPosition);
                Vector2 side = new(-toPlayer.y, toPlayer.x);
                return UnityEngine.Random.value < 0.5f ? side : -side;
            }

            float lowAngle = Mathf.Min(minAngleDegrees, maxAngleDegrees);
            float highAngle = Mathf.Max(minAngleDegrees, maxAngleDegrees);
            return BossActionContext.AngleToDirection(UnityEngine.Random.Range(lowAngle, highAngle));
        }

        // ConductorSpawnTurretsAction의 지면 판정 방식(콜라이더 + 타일맵 둘 다 확인)과 동일한 방식이다 —
        // 맵마다 Ground를 콜라이더로 깔았는지 Tilemap으로 깔았는지가 달라서 둘 다 확인해야 한다.
        private bool IsGroundPosition(Vector2 position)
        {
            int groundLayer = LayerMask.NameToLayer(GroundLayerName);
            if (groundLayer < 0)
            {
                return true;
            }

            int groundMask = 1 << groundLayer;
            if (Physics2D.OverlapCircle(position, groundProbeRadius, groundMask) != null)
            {
                return true;
            }

            Tilemap[] tilemaps = UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            for (int i = 0; i < tilemaps.Length; i++)
            {
                Tilemap tilemap = tilemaps[i];
                if (tilemap == null || !tilemap.isActiveAndEnabled || tilemap.gameObject.layer != groundLayer)
                {
                    continue;
                }

                if (tilemap.HasTile(tilemap.WorldToCell(position)))
                {
                    return true;
                }
            }

            return false;
        }

        public void OnBeforeSerialize()
        {
            EnsureSpeedCurve();
        }

        public void OnAfterDeserialize()
        {
            EnsureSpeedCurve();
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, duration);
            return seconds > 0f;
        }

        private AnimationCurve GetSpeedCurve()
        {
            EnsureSpeedCurve();
            return speedCurve;
        }

        private void EnsureSpeedCurve()
        {
            if (speedCurveInitialized && speedCurve != null && speedCurve.length > 0)
            {
                return;
            }

            speedCurve = BossMoveSpeedCurve.CreateConstant();
            speedCurveInitialized = true;
        }
    }
}
