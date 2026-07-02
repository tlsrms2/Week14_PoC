using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    // Shared by two dash-synced bullet patterns:
    //  - PerpendicularWall: bullets line up across the dash direction, then fire parallel to the dash.
    //  - ParallelLane: bullets line up along the dash direction offset from its centerline,
    //    then fire outward, closest-to-boss pair first.
    public enum BossGraphDashFormationPattern
    {
        PerpendicularWall,
        ParallelLane
    }

    public enum BossGraphDashFormationFireOrder
    {
        Simultaneous,
        SequentialByIndex
    }

    [Serializable]
    public sealed class FireDashFormationAction : BossAction, ISerializationCallbackReceiver
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();

        [SerializeField] private BossGraphDashFormationPattern pattern = BossGraphDashFormationPattern.PerpendicularWall;
        [SerializeField, Min(1)] private int bulletCount = 6;
        [SerializeField, Min(0f)] private float spacing = 0.6f;
        [Tooltip("PerpendicularWall: 중앙(대시 경로)을 비우고 첫 탄이 놓일 좌우 간격. ParallelLane: 중앙선에서 좌/우 레인까지의 고정 거리.")]
        [SerializeField, Min(0f)] private float centerOffset = 1.2f;
        [Tooltip("페어링된 BossDashAction과 같은 방향이 나오도록 설정하세요. AtPlayer는 BossDashAction의 기본 방향 계산과 동일합니다.")]
        [SerializeField] private BossGraphProjectileAimSpec dashAim = new();

        [SerializeField, Min(0f)] private float alignDuration = 0.15f;
        [SerializeField] private AnimationCurve alignEase;
        [SerializeField, HideInInspector] private bool alignEaseInitialized;
        [SerializeField, Min(0f)] private float holdSeconds = 0.3f;

        [SerializeField] private BossGraphDashFormationFireOrder fireOrder = BossGraphDashFormationFireOrder.Simultaneous;
        [SerializeField, Min(0f)] private float fireInterval = 0.05f;

        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public void OnBeforeSerialize() => EnsureAlignEase();
        public void OnAfterDeserialize() => EnsureAlignEase();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null || bulletCount <= 0)
            {
                yield break;
            }

            EnsureAlignEase();
            Vector3 originPosition = context.OriginPosition;
            BossGraphProjectileAimSpec aimSpec = dashAim ?? new BossGraphProjectileAimSpec();
            Vector2 dashDirection = aimSpec.GetDirection(context, originPosition);
            Vector2 perpendicular = new(-dashDirection.y, dashDirection.x);

            for (int i = 0; i < bulletCount; i++)
            {
                Vector3 targetPosition = GetFormationTargetPosition(originPosition, dashDirection, perpendicular, i);
                Vector2 launchDirection = GetLaunchDirection(dashDirection, perpendicular, i);
                float chargeSeconds = alignDuration + GetBulletHoldSeconds(i);

                EnemyProjectile spawned = context.FireProjectile(
                    projectile,
                    originPosition,
                    launchDirection,
                    0f,
                    false,
                    false,
                    chargeSeconds,
                    -1f,
                    false,
                    projectileName);

                if (spawned == null)
                {
                    continue;
                }

                Transform anchor = FormationAlignAnchor.Create(
                    originPosition,
                    targetPosition,
                    alignDuration,
                    alignEase,
                    chargeSeconds + 0.5f);
                spawned.ConfigureChargeAnchor(anchor);
                spawned.ConfigureChargeMotion(0f, false, false);

                context.PlaySfx(fireSfxId);
                context.PlaySfxOnLaunch(spawned, launchSfxId);
                context.PlayOriginBurst(effects, originPosition);
                context.PlayMuzzleFlashIfEnabled(effects, targetPosition, launchDirection);
            }

            context.PlayCameraShakeIfEnabled(effects, dashDirection);
        }

        private float GetBulletHoldSeconds(int index)
        {
            if (fireOrder == BossGraphDashFormationFireOrder.Simultaneous)
            {
                return holdSeconds;
            }

            int groupIndex = index / 2;
            return holdSeconds + fireInterval * groupIndex;
        }

        private Vector3 GetFormationTargetPosition(Vector3 origin, Vector2 dashDirection, Vector2 perpendicular, int index)
        {
            int laneIndex = index / 2;
            float side = index % 2 == 0 ? -1f : 1f;

            if (pattern == BossGraphDashFormationPattern.PerpendicularWall)
            {
                float offset = centerOffset + spacing * laneIndex;
                return origin + (Vector3)(perpendicular * offset * side);
            }

            float alongDash = spacing * (laneIndex + 1);
            return origin + (Vector3)(dashDirection * alongDash) + (Vector3)(perpendicular * centerOffset * side);
        }

        private Vector2 GetLaunchDirection(Vector2 dashDirection, Vector2 perpendicular, int index)
        {
            if (pattern == BossGraphDashFormationPattern.PerpendicularWall)
            {
                return dashDirection;
            }

            float side = index % 2 == 0 ? -1f : 1f;
            return perpendicular * side;
        }

        private void EnsureAlignEase()
        {
            if (alignEaseInitialized && alignEase != null && alignEase.length > 0)
            {
                return;
            }

            alignEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            alignEaseInitialized = true;
        }
    }
}
