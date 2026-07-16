using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [AddComponentMenu("Week14/Boss/Assassin Dagger")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class AssassinDagger : MonoBehaviour
    {
        // 단검 스프라이트 원본은 위쪽(+Y)을 향해 그려져 있는데, FaceBoss/회수 비행은
        // "0도 = +X(오른쪽)"을 기준으로 각도를 계산한다. 그 차이를 보정하는 값.
        private const float SpriteForwardOffsetDegrees = -220f;

        private const int MaxPathDashCount = 64;
        private const float PathDashLength = 0.2f;
        private const float PathDashGap = 0.14f;
        private static Material pathIndicatorMaterial;

        [SerializeField, Min(1f)] private float turnDegreesPerSecond = 720f;
        [SerializeField, Min(0.01f)] private float arrivalDistance = 0.2f;
        [Tooltip("회수 비행 중 궤적의 두께 계산에 쓰이는 반지름입니다. 일반 투사체의 Radius와 같은 역할입니다.")]
        [SerializeField, Min(0.01f)] private float trailRadius = 0.15f;
        [Tooltip("궤적이 남는 시간(초)입니다. 일반 투사체의 Trail Seconds와 같습니다.")]
        [SerializeField, Min(0.025f)] private float trailSeconds = 0.12f;
        [Tooltip("궤적 두께 배율입니다. 일반 투사체의 Trail Width Multiplier와 같습니다.")]
        [SerializeField, Min(0.1f)] private float trailWidthMultiplier = 1f;
        [Tooltip("플레이어를 노리는 회수 비행(패링 실패) 중 표시할 예상 경로 점선 색상입니다.")]
        [SerializeField] private Color pathIndicatorColor = new(1f, 0.3f, 0.15f, 0.6f);
        [Tooltip("예상 경로 점선의 두께입니다.")]
        [SerializeField, Min(0.005f)] private float pathIndicatorWidth = 0.05f;

        private AssassinBossAI owner;
        private Transform bossTransform;
        private Collider2D hitCollider;
        private TrailRenderer trail;
        private bool isRecalling;
        private bool hasHitPlayer;
        private int pendingPlayerDamage;
        private readonly List<LineRenderer> pathIndicatorDashes = new();
        private Transform pathIndicatorRoot;

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

            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + SpriteForwardOffsetDegrees;
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

                if (showTrail)
                {
                    UpdatePathIndicator(transform.position, target.position);
                }

                yield return null;
            }

            HidePathIndicator();
            Destroy(gameObject);
        }

        // 패링 실패(플레이어를 노리는) 회수 비행 중에만 호출된다. 일반 투사체의 점선 예상경로와
        // 같은 방식(LineRenderer 여러 개를 잘라 붙여 점선처럼 보이게)으로 보스까지의 남은 경로를 매 프레임 다시 그린다.
        private void UpdatePathIndicator(Vector2 start, Vector2 target)
        {
            Vector2 delta = target - start;
            float length = delta.magnitude;
            if (length <= 0.05f)
            {
                HidePathIndicator();
                return;
            }

            Vector2 direction = delta / length;
            int dashCount = Mathf.Min(MaxPathDashCount, Mathf.CeilToInt(length / (PathDashLength + PathDashGap)));

            for (int i = 0; i < dashCount; i++)
            {
                float segmentStart = i * (PathDashLength + PathDashGap);
                float segmentEnd = Mathf.Min(segmentStart + PathDashLength, length);
                LineRenderer dash = EnsurePathDash(i);
                if (dash == null)
                {
                    continue;
                }

                dash.enabled = true;
                dash.startColor = pathIndicatorColor;
                dash.endColor = pathIndicatorColor;
                dash.startWidth = pathIndicatorWidth;
                dash.endWidth = pathIndicatorWidth;
                dash.SetPosition(0, start + direction * segmentStart);
                dash.SetPosition(1, start + direction * segmentEnd);
            }

            for (int i = dashCount; i < pathIndicatorDashes.Count; i++)
            {
                SetPathDashVisible(i, false);
            }
        }

        private void HidePathIndicator()
        {
            for (int i = 0; i < pathIndicatorDashes.Count; i++)
            {
                SetPathDashVisible(i, false);
            }
        }

        private void SetPathDashVisible(int index, bool visible)
        {
            if (index < 0 || index >= pathIndicatorDashes.Count || pathIndicatorDashes[index] == null)
            {
                return;
            }

            pathIndicatorDashes[index].enabled = visible;
        }

        private LineRenderer EnsurePathDash(int index)
        {
            EnsurePathIndicatorRoot();
            if (pathIndicatorRoot == null)
            {
                return null;
            }

            while (pathIndicatorDashes.Count <= index)
            {
                GameObject dashObject = new($"PathIndicator_{pathIndicatorDashes.Count:00}");
                dashObject.transform.SetParent(pathIndicatorRoot, false);
                LineRenderer dash = dashObject.AddComponent<LineRenderer>();
                dash.useWorldSpace = true;
                dash.loop = false;
                dash.positionCount = 2;
                dash.numCornerVertices = 0;
                dash.numCapVertices = 1;
                dash.sortingOrder = 17;
                dash.material = GetPathIndicatorMaterial();
                pathIndicatorDashes.Add(dash);
            }

            return pathIndicatorDashes[index];
        }

        private void EnsurePathIndicatorRoot()
        {
            if (pathIndicatorRoot != null)
            {
                return;
            }

            GameObject rootObject = new("PathIndicator");
            rootObject.transform.SetParent(transform, false);
            rootObject.transform.localPosition = Vector3.zero;
            pathIndicatorRoot = rootObject.transform;
        }

        private static Material GetPathIndicatorMaterial()
        {
            if (pathIndicatorMaterial != null)
            {
                return pathIndicatorMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            pathIndicatorMaterial = shader != null ? new Material(shader) : null;
            return pathIndicatorMaterial;
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
