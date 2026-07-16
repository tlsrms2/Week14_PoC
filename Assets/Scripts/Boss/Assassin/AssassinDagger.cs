using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [AddComponentMenu("Week14/Boss/Assassin Dagger")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class AssassinDagger : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float turnDegreesPerSecond = 720f;
        [SerializeField, Min(0.01f)] private float arrivalDistance = 0.2f;
        [Tooltip("회수 비행 중 궤적의 두께 계산에 쓰이는 반지름입니다. 일반 투사체의 Radius와 같은 역할입니다.")]
        [SerializeField, Min(0.01f)] private float trailRadius = 0.15f;
        [Tooltip("궤적이 남는 시간(초)입니다. 일반 투사체의 Trail Seconds와 같습니다.")]
        [SerializeField, Min(0.025f)] private float trailSeconds = 0.12f;
        [Tooltip("궤적 두께 배율입니다. 일반 투사체의 Trail Width Multiplier와 같습니다.")]
        [SerializeField, Min(0.1f)] private float trailWidthMultiplier = 1f;

        private AssassinBossAI owner;
        private Transform bossTransform;
        private Collider2D hitCollider;
        private TrailRenderer trail;
        private bool isRecalling;
        private bool hasHitPlayer;
        private int pendingPlayerDamage;

        private void Awake()
        {
            hitCollider = GetComponent<Collider2D>();
            hitCollider.isTrigger = true;
            // 바닥에 놓여있는 동안에는 플레이어와 그냥 스쳐도 맞지 않도록 꺼두고,
            // 회수 비행 중(FlyToBoss)에만 실제 콜라이더 판정으로 플레이어를 때린다.
            hitCollider.enabled = false;

            // 일반 투사체(EnemyProjectile)와 동일한 방식 — TrailRenderer를 프리팹에 미리 붙일 필요 없이
            // 코드가 자동으로 추가/설정한다. color 인자는 ProjectileVfx.EnsureTrail 내부에서 쓰이지 않고
            // 항상 고정된 주황색 트레일 색을 쓰므로(일반 투사체와 동일), 아무 값이나 넘겨도 무방하다.
            ProjectileVfx.ApplyVisibility(gameObject, Color.white, trailRadius, trailSeconds, trailWidthMultiplier);
            trail = GetComponent<TrailRenderer>();
            if (trail != null)
            {
                trail.emitting = false;
            }
        }

        internal void Initialize(AssassinBossAI nextOwner, Transform nextBossTransform)
        {
            owner = nextOwner;
            bossTransform = nextBossTransform;
        }

        private void Update()
        {
            if (isRecalling || bossTransform == null)
            {
                return;
            }

            FaceBoss();
        }

        private void FaceBoss()
        {
            Vector2 direction = bossTransform.position - transform.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float currentAngle = transform.eulerAngles.z;
            float maxDelta = turnDegreesPerSecond * EnemyTimeScale.DeltaTime;
            float nextAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, maxDelta);
            transform.rotation = Quaternion.Euler(0f, 0f, nextAngle);
        }

        internal IEnumerator FlyToBoss(Transform target, float speed, int playerDamage, bool showTrail)
        {
            isRecalling = true;
            hasHitPlayer = false;
            pendingPlayerDamage = playerDamage;
            if (playerDamage > 0 && hitCollider != null)
            {
                hitCollider.enabled = true;
            }

            if (trail != null)
            {
                trail.Clear();
                trail.emitting = showTrail;
            }

            while (target != null && Vector2.Distance(transform.position, target.position) > arrivalDistance)
            {
                Vector2 direction = ((Vector2)target.position - (Vector2)transform.position).normalized;
                transform.position += (Vector3)(direction * speed * EnemyTimeScale.DeltaTime);
                yield return null;
            }

            Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (hasHitPlayer || pendingPlayerDamage <= 0)
            {
                return;
            }

            PlayerCombatController player = other.GetComponentInParent<PlayerCombatController>();
            if (player == null)
            {
                return;
            }

            Vector2 hitDirection = (Vector2)(player.transform.position - transform.position);
            if (player.ReceiveAttack(pendingPlayerDamage, transform.position, hitDirection))
            {
                hasHitPlayer = true;
            }
        }

        private void OnDestroy()
        {
            owner?.UnregisterDagger(this);
        }
    }
}
