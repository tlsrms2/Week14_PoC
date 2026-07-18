using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public enum SpawnEffectFollowMode
    {
        StayInWorld,
        FollowSpawnPoint
    }

    [Serializable]
    public sealed class BossActionPrefabEffectSettings
    {
        [SerializeField] private bool enabled;
        [SerializeField] private GameObject effectPrefab;
        [SerializeField, BossGraphBossChildPath] private string spawnPointPath;
        [SerializeField] private Vector2 positionOffset;
        [SerializeField, Min(0.01f)] private float scale = 1f;
        [SerializeField] private SpawnEffectFollowMode followMode = SpawnEffectFollowMode.FollowSpawnPoint;
        [SerializeField] private bool followRotation = true;

        internal GameObject Play(BossActionContext context)
        {
            return Play(
                context,
                enabled,
                effectPrefab,
                spawnPointPath,
                positionOffset,
                scale,
                followMode,
                followRotation);
        }

        internal GameObject PlayAtWorldYRotation(
            BossActionContext context,
            string spawnPointPathOverride,
            float worldYRotationDegrees)
        {
            return Play(
                context,
                enabled,
                effectPrefab,
                string.IsNullOrWhiteSpace(spawnPointPathOverride)
                    ? spawnPointPath
                    : spawnPointPathOverride,
                positionOffset,
                scale,
                followMode,
                followRotation: false,
                worldYRotationDegrees: worldYRotationDegrees,
                useSpawnPointRotation: false);
        }

        internal static GameObject Play(
            BossActionContext context,
            bool enabled,
            GameObject effectPrefab,
            string spawnPointPath,
            Vector2 positionOffset,
            float scale,
            SpawnEffectFollowMode followMode,
            bool followRotation,
            float worldYRotationDegrees = 0f,
            bool useSpawnPointRotation = true)
        {
            if (!enabled || context == null || effectPrefab == null)
            {
                return null;
            }

            Transform spawnPoint = context.GetBossChildTransform(spawnPointPath);
            if (spawnPoint == null)
            {
                return null;
            }

            Vector3 position = spawnPoint.TransformPoint(
                new Vector3(positionOffset.x, positionOffset.y, 0f));
            Transform followTarget = followMode == SpawnEffectFollowMode.FollowSpawnPoint
                ? spawnPoint
                : null;

            Quaternion rotation = Quaternion.AngleAxis(worldYRotationDegrees, Vector3.up);
            if (useSpawnPointRotation)
            {
                rotation *= spawnPoint.rotation;
            }

            return ProjectileVfx.PlayPrefab(
                effectPrefab,
                position,
                rotation,
                followTarget,
                Mathf.Max(0.01f, scale),
                followRotation);
        }
    }

    [Serializable]
    public sealed class SpawnEffectPrefabAction : BossAction, IBossActionDurationProvider
    {
        [Tooltip("생성할 일회성 이펙트 프리팹입니다.")]
        [SerializeField] private GameObject effectPrefab;
        [Tooltip("Boss Graph Editor의 실제 보스 하이어러키에서 생성 위치를 드래그해 지정합니다.")]
        [SerializeField, BossGraphBossChildPath] private string spawnPointPath;
        [Tooltip("Spawn Point의 로컬 좌표를 기준으로 적용할 X/Y 위치 오프셋입니다.")]
        [SerializeField] private Vector2 positionOffset;
        [Tooltip("액션 시작 후 이펙트를 생성하기까지 대기할 시간입니다.")]
        [SerializeField, Min(0f)] private float delaySeconds;
        [Tooltip("프리팹 원본 스케일에 곱할 배율입니다.")]
        [SerializeField, Min(0.01f)] private float scale = 1f;
        [Tooltip("Stay In World는 생성 좌표에 유지하고, Follow Spawn Point는 지정한 Transform을 따라갑니다.")]
        [SerializeField] private SpawnEffectFollowMode followMode = SpawnEffectFollowMode.FollowSpawnPoint;
        [Tooltip("Follow Spawn Point일 때 지정한 Transform의 회전도 따라갑니다.")]
        [SerializeField] private bool followRotation = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null || effectPrefab == null)
            {
                yield break;
            }

            if (delaySeconds > 0f)
            {
                yield return context.WaitSeconds(delaySeconds);
            }

            Transform originTransform = context.GetBossChildTransform(spawnPointPath);
            if (originTransform == null)
            {
                yield break;
            }

            Vector3 position = originTransform.TransformPoint(
                new Vector3(positionOffset.x, positionOffset.y, 0f));
            Vector2 direction = ResolveDirection(originTransform);
            Transform followTarget = followMode == SpawnEffectFollowMode.FollowSpawnPoint
                ? originTransform
                : null;

            ProjectileVfx.PlayPrefab(
                effectPrefab,
                position,
                direction,
                followTarget,
                Mathf.Max(0.01f, scale),
                followRotation);
        }

        public bool TryGetDurationSeconds(out float durationSeconds)
        {
            durationSeconds = Mathf.Max(0f, delaySeconds);
            return durationSeconds > 0f;
        }

        private static Vector2 ResolveDirection(Transform originTransform)
        {
            if (originTransform != null && originTransform.right.sqrMagnitude > 0.0001f)
            {
                return originTransform.right;
            }

            return Vector2.right;
        }
    }
}
