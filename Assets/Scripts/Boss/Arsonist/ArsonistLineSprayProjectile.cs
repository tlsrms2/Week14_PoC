using UnityEngine;

namespace Week14.Enemy
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public abstract class ArsonistLineSprayProjectile : MonoBehaviour
    {
        private const string WallLayerName = "Wall";

        [SerializeField] private ArsonistBossAI owner;
        [SerializeField, Min(0f)] private float speed = 7f;
        [SerializeField, Min(0.05f)] private float lifetime = 0.55f;
        [SerializeField, Min(0.05f)] private float patchRadius = 0.35f;
        [SerializeField, Min(0.05f)] private float patchDuration = 4f;
        [SerializeField, Min(0.05f)] private float paintSpacing = 0.28f;
        [SerializeField] private bool destroyOnWall = true;

        private Rigidbody2D body;
        private Vector2 direction = Vector2.right;
        private Vector2 previousPosition;
        private float destroyAt;
        private float distanceSinceLastPaint;
        private bool launched;
        private bool paintedInitial;

        protected ArsonistBossAI Owner => owner;
        protected float PatchRadius => patchRadius;
        protected float PatchDuration => patchDuration;

        public void Launch(ArsonistBossAI nextOwner, Vector2 launchDirection)
        {
            owner = nextOwner != null ? nextOwner : ResolveOwner();
            direction = launchDirection.sqrMagnitude > 0.0001f ? launchDirection.normalized : transform.right;
            BeginFlight();
        }

        protected abstract void PlaceHazard(Vector3 position, float radius, float duration);

        protected virtual void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        protected virtual void Start()
        {
            if (!launched)
            {
                Launch(ResolveOwner(), transform.right);
            }
        }

        private void Update()
        {
            if (!launched)
            {
                return;
            }

            if (body != null)
            {
                body.linearVelocity = direction * Mathf.Max(0f, speed) * EnemyTimeScale.Current;
            }

            destroyAt += EnemyTimeScale.DeltaTimeDebt;

            Vector2 currentPosition = transform.position;
            PaintSegment(previousPosition, currentPosition);
            previousPosition = currentPosition;

            if (Time.time >= destroyAt)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (destroyOnWall && IsWallCollider(other))
            {
                Destroy(gameObject);
            }
        }

        private void BeginFlight()
        {
            launched = true;
            previousPosition = transform.position;
            destroyAt = Time.time + Mathf.Max(0.05f, lifetime);
            distanceSinceLastPaint = 0f;
            paintedInitial = false;

            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (body != null)
            {
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.linearVelocity = direction * Mathf.Max(0f, speed) * EnemyTimeScale.Current;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            PaintSegment(previousPosition, previousPosition);
        }

        private ArsonistBossAI ResolveOwner()
        {
            if (owner != null)
            {
                return owner;
            }

            owner = GetComponentInParent<ArsonistBossAI>();
            if (owner == null)
            {
                owner = FindFirstObjectByType<ArsonistBossAI>();
            }

            return owner;
        }

        private void PaintSegment(Vector2 from, Vector2 to)
        {
            if (!paintedInitial)
            {
                PlaceHazard(from, patchRadius, patchDuration);
                paintedInitial = true;
            }

            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
            {
                return;
            }

            Vector2 normal = delta / distance;
            float spacing = Mathf.Max(0.05f, paintSpacing);
            float remaining = distance;
            Vector2 cursor = from;

            while (distanceSinceLastPaint + remaining >= spacing)
            {
                float step = spacing - distanceSinceLastPaint;
                cursor += normal * step;
                PlaceHazard(cursor, patchRadius, patchDuration);
                remaining -= step;
                distanceSinceLastPaint = 0f;
            }

            distanceSinceLastPaint += remaining;
        }

        private static bool IsWallCollider(Collider2D collider)
        {
            int wallLayer = LayerMask.NameToLayer(WallLayerName);
            return wallLayer >= 0
                && collider != null
                && collider.gameObject.layer == wallLayer;
        }
    }
}
