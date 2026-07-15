using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    // 보스 위치를 중심으로 원형 인디케이터를 띄우고, Windup Seconds가 지나면 그 범위 안 플레이어에게
    // 광역 데미지를 준다. 투사체(탄) 없이 순수하게 보스 자신을 중심으로 하는 공격이다.
    //
    // 패링으로 억제 가능한 패턴으로 쓰려면(예: Muscle 보스의 기존 폭탄 패턴 대체):
    // Fire Parry Suppression Bait와 P 포트로 병렬 연결하고, 이 액션의 Windup Seconds를 그 액션의
    // Bait Duration Seconds와 같게 맞추세요. 그러면 패링 가능 시간 내내 인디케이터가 보이다가,
    // 패링 성공 시 패턴 전체(이 액션 포함)가 취소되고, 패링 실패 시 정확히 인디케이터가 다 찼을 때
    // 데미지가 발동합니다.
    [Serializable]
    public sealed class BossAreaDamageAction : BossAction
    {
        [Header("범위 피해")]
        [Tooltip("피해를 주는 범위 반지름입니다.")]
        [SerializeField, Min(0.1f)] private float explosionRadius = 2f;
        [Tooltip("범위 안 플레이어에게 줄 피해량(감소시킬 탄환 수)입니다.")]
        [SerializeField, Min(0)] private int explosionDamage = 1;
        [Tooltip("폭발 이펙트 색상입니다.")]
        [SerializeField] private Color explosionColor = new(1f, 0.6f, 0.2f, 1f);

        [Header("범위 인디케이터")]
        [Tooltip("인디케이터가 차오르는 시간(초)입니다. 이 시간이 지나면 데미지가 발동합니다.")]
        [SerializeField, Min(0.1f)] private float windupSeconds = 1.5f;
        [Tooltip("범위를 표시할 원형 스프라이트입니다. 비워두면 인디케이터를 표시하지 않습니다.")]
        [SerializeField] private Sprite rangeSprite;
        [Tooltip("배경(전체 범위) 색상입니다.")]
        [SerializeField] private Color rangeBackgroundColor = new(1f, 0.2f, 0.1f, 0.25f);
        [Tooltip("중앙에서 차오르는 채움 색상입니다.")]
        [SerializeField] private Color rangeFillColor = new(1f, 0.4f, 0.15f, 0.55f);
        [SerializeField] private int rangeSortingOrder = 15;

        [Header("애니메이션")]
        [Tooltip("인디케이터가 다 차서 실제로 내려찍는(폭발하는) 모션을 시작할 때 켤 Animator Bool 이름입니다. 비워두면 호출하지 않습니다.")]
        [SerializeField] private string slamBoolName = "isSlam";
        [Tooltip("폭발 전 대기할 Animation Event 이름입니다. 내려찍는 애니메이션의 충돌 프레임에서 발생시켜야 폭발과 모션이 맞아떨어집니다. 비워두면 대기 없이 즉시 터집니다.")]
        [SerializeField] private string impactEventId = "SlamImpact";
        [SerializeField, Min(0f)] private float impactEventTimeoutSeconds = 2f;

        [Header("사운드")]
        [SerializeField, BossGraphSfxId] private string windupSfxId;
        [SerializeField, BossGraphSfxId] private string explosionSfxId;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss == null)
            {
                yield break;
            }

            BossAreaIndicatorVfx indicator = rangeSprite != null
                ? BossAreaIndicatorVfx.Spawn(rangeSprite, explosionRadius, rangeBackgroundColor, rangeFillColor, rangeSortingOrder)
                : null;

            if (indicator != null)
            {
                context.RegisterTransientVisual(indicator.gameObject);
            }

            context.PlaySfx(windupSfxId);

            float elapsed = 0f;
            while (elapsed < windupSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                indicator?.UpdateVfx(context.Boss.transform.position, elapsed / windupSeconds);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            context.SetAnimationBool(slamBoolName, true);

            if (!string.IsNullOrWhiteSpace(impactEventId))
            {
                yield return context.WaitForAnimationEvent(impactEventId, impactEventTimeoutSeconds);
            }

            Vector3 explosionCenter = context.Boss.transform.position;

            if (indicator != null)
            {
                context.UnregisterTransientVisual(indicator.gameObject);
                UnityEngine.Object.Destroy(indicator.gameObject);
            }

            context.PlaySfx(explosionSfxId);
            ProjectileVfx.PlayHogExplosion(explosionCenter, explosionColor, Mathf.Max(1f, explosionRadius));

            if (explosionDamage <= 0)
            {
                yield break;
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(explosionCenter, explosionRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                PlayerCombatController player = hits[i].GetComponentInParent<PlayerCombatController>();
                if (player == null)
                {
                    continue;
                }

                Vector2 hitDirection = (Vector2)(player.transform.position - explosionCenter);
                player.ReceiveAttack(explosionDamage, explosionCenter, hitDirection);
                break;
            }
        }
    }
}
