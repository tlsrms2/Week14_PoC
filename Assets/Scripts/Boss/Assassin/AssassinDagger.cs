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

        private AssassinBossAI owner;
        private Transform bossTransform;
        private Collider2D hitCollider;
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

        internal IEnumerator FlyToBoss(Transform target, float speed, int playerDamage)
        {
            isRecalling = true;
            hasHitPlayer = false;
            pendingPlayerDamage = playerDamage;
            if (playerDamage > 0 && hitCollider != null)
            {
                hitCollider.enabled = true;
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
