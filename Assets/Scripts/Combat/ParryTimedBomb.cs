using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    [RequireComponent(typeof(EnemyProjectile))]
    public sealed class ParryTimedBomb : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("폭발 판정 반경입니다.")] private float explosionRadius = 2f;
        [SerializeField, Min(0), Tooltip("폭발 시 플레이어에게 감소시킬 탄환 수입니다.")] private int explosionDamage = 1;
        [SerializeField, Tooltip("폭발 이펙트 색상입니다.")] private Color explosionColor = new(1f, 0.6f, 0.2f, 1f);
        [SerializeField, Tooltip("폭발 범위를 표시할 원형 스프라이트입니다.")] private Sprite rangeSprite;
        [SerializeField, Tooltip("배경(전체 범위) 색상입니다.")] private Color rangeBackgroundColor = new(1f, 0.2f, 0.1f, 0.25f);
        [SerializeField, Tooltip("중앙에서 차오르는 채움 색상입니다.")] private Color rangeFillColor = new(1f, 0.4f, 0.15f, 0.55f);
        [SerializeField] private int rangeSortingOrder = 15;

        private EnemyProjectile projectile;
        private SpriteRenderer rangeBackground;
        private SpriteRenderer rangeFill;
        private float rangeFullScale = 1f;

        private void Awake()
        {
            projectile = GetComponent<EnemyProjectile>();
            projectile.Launched += HandleParryTimeout;
            SetupRangeIndicator();
        }

        private void OnDestroy()
        {
            if (projectile != null)
            {
                projectile.Launched -= HandleParryTimeout;
            }
        }

        private void Update()
        {
            if (projectile == null || !projectile.IsCharging || rangeBackground == null || rangeFill == null)
            {
                return;
            }

            Vector3 center = ResolveExplosionCenter();
            rangeBackground.transform.position = center;
            rangeFill.transform.position = center;
            rangeFill.transform.localScale = Vector3.one * (rangeFullScale * projectile.ChargeProgress01);
        }

        private void SetupRangeIndicator()
        {
            if (rangeSprite == null)
            {
                return;
            }

            rangeFullScale = (explosionRadius * 2f) / Mathf.Max(0.01f, rangeSprite.bounds.size.x);

            rangeBackground = CreateRangeSpriteChild("ExplosionRangeBackground", rangeBackgroundColor, rangeSortingOrder);
            rangeBackground.transform.localScale = Vector3.one * rangeFullScale;

            rangeFill = CreateRangeSpriteChild("ExplosionRangeFill", rangeFillColor, rangeSortingOrder + 1);
            rangeFill.transform.localScale = Vector3.zero;
        }

        private SpriteRenderer CreateRangeSpriteChild(string childName, Color color, int sortingOrder)
        {
            GameObject child = new(childName);
            child.transform.SetParent(transform, false);
            SpriteRenderer spriteRenderer = child.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = rangeSprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = sortingOrder;
            return spriteRenderer;
        }

        private Vector3 ResolveExplosionCenter()
        {
            return projectile.OwnerBoss != null
                ? projectile.OwnerBoss.transform.position
                : transform.position;
        }

        private void HandleParryTimeout(EnemyProjectile _)
        {
            Vector3 explosionCenter = ResolveExplosionCenter();

            ProjectileVfx.PlayHogExplosion(explosionCenter, explosionColor, Mathf.Max(1f, explosionRadius));

            if (explosionDamage > 0)
            {
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

            projectile.DestroyFromOwner();
        }
    }
}
