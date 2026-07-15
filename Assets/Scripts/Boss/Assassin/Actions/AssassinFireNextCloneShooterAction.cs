using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class AssassinFireNextCloneShooterAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [Tooltip("발사한 것이 분신이었다면, 그 분신이 사라지는 데 걸리는 페이드 시간입니다. 보스 자신이 쐈다면 무시됩니다.")]
        [SerializeField, Min(0f)] private float despawnFadeSeconds = 0.15f;
        [Tooltip("한 번에 발사하는 총알 개수입니다. 1이면 기존과 동일하게 한 발만 발사합니다. 홀수면 가운데 총알이 플레이어와 정확히 일직선입니다.")]
        [FormerlySerializedAs("lineBulletCount")]
        [SerializeField, Min(1)] private int arcBulletCount = 1;
        [Tooltip("총알들이 부채꼴로 퍼지는 전체 각도(도)입니다. 발사 지점(보스 또는 분신) 한 점에서 이 각도만큼 호를 그리며 퍼져 나갑니다.")]
        [SerializeField, Range(0f, 360f)] private float arcDegrees = 40f;
        [Tooltip("왼쪽 총알부터 오른쪽 총알까지 순서대로 발사할 때, 한 발씩 사이에 두는 시간(초)입니다. 0이면 전부 동시에 발사합니다.")]
        [FormerlySerializedAs("lineFireInterval")]
        [SerializeField, Min(0f)] private float arcFireInterval = 0.05f;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is not AssassinBossAI assassin
                || !assassin.TryDequeueCloneShooter(out Vector3 origin, out AssassinClone clone))
            {
                yield break;
            }

            // 중심 각도는 최초 한 번만 플레이어 쪽으로 계산한다. 총알들은 같은 지점(origin)에서
            // 이 중심 각도를 기준으로 arcDegrees만큼 부채꼴로 각도를 벌려 발사된다 —
            // 홀수 개일 때는 가운데 총알이 정확히 이 중심 각도(=플레이어 방향)와 일치한다.
            Vector2 centerDirection = context.GetDirectionToPlayer(origin);
            float centerAngle = Mathf.Atan2(centerDirection.y, centerDirection.x) * Mathf.Rad2Deg;

            // 대기열에서는 이미 빠졌지만 아직 총알을 다 쏘지 않았으므로, 자동 색상 갱신(대기열 맨 앞 감시)이
            // 다음 차례를 앞당겨 색칠하지 않도록 붙잡아 둔다. 다 쏘고 나면 EndShooterAttack에서 색을 되돌리고
            // 자동 갱신을 다시 풀어준다.
            assassin.BeginShooterAttack();

            int count = Mathf.Max(1, arcBulletCount);
            float angleStep = count > 1 ? arcDegrees / (count - 1) : 0f;
            float leftmostAngle = centerAngle + arcDegrees * 0.5f;

            for (int i = 0; i < count; i++)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    i--;
                    continue;
                }

                Vector2 shotDirection = BossActionContext.AngleToDirection(leftmostAngle - angleStep * i);

                EnemyProjectile firedProjectile = context.FireProjectile(
                    projectile,
                    origin,
                    shotDirection,
                    0f,
                    projectileName: projectileName);

                if (firedProjectile != null)
                {
                    context.PlaySfx(fireSfxId);
                    context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
                    context.PlayOriginBurst(effects, origin);
                    context.PlayMuzzleFlashIfEnabled(effects, origin, shotDirection);
                    context.PlayCameraShakeIfEnabled(effects, shotDirection);
                }

                if (i < count - 1 && arcFireInterval > 0f)
                {
                    yield return context.WaitSeconds(arcFireInterval);
                }
            }

            assassin.EndShooterAttack();

            if (clone != null)
            {
                clone.PlayDespawn(despawnFadeSeconds);
            }
        }
    }
}
