using UnityEngine;

namespace Week14.Enemy
{
    public enum HackerThrownWeaponType
    {
        Bayonet,
        Gun,
        Sword,
        ThrowingWeapon
    }

    public sealed class HackerThrownWeapon : MonoBehaviour
    {
        private HackerBossAI owner;
        private Vector3 startPosition;
        private Vector3 landingPosition;
        private float travelSeconds;
        private float elapsed;
        private AnimationCurve travelSpeedCurve;
        private bool isThrown;
        private Transform equippedWeapon;
        private bool restoreEquippedWeapon;
        private Transform recallReturnAnchor;
        private Transform recallRotationTarget;
        private float recallSeconds;
        private float recallElapsed;
        private Vector3 recallStartWireAnchorPosition;
        private Vector3 recallWireAnchorLocalPosition;
        private Quaternion recallStartRotation;
        private float recallRotationDegrees;
        private bool isRecalling;
        private bool isOrbiting;
        private SpriteRenderer weaponRenderer;
        private Sprite originalSprite;
        private Sprite flyingSprite;
        private Sprite groundedSprite;

        public HackerThrownWeaponType WeaponType { get; private set; }
        public bool IsGrounded => !isThrown && !isRecalling && !isOrbiting;
        internal bool IsOwnedBy(HackerBossAI boss) => owner == boss;

        public Transform GetChildTransform(string childPath)
        {
            if (string.IsNullOrWhiteSpace(childPath))
            {
                return transform;
            }

            string normalizedPath = childPath.Trim().Trim('/');
            Transform child = transform.Find(normalizedPath);
            if (child != null)
            {
                return child;
            }

            int firstSeparatorIndex = normalizedPath.IndexOf('/');
            if (firstSeparatorIndex >= 0)
            {
                child = transform.Find(normalizedPath.Substring(firstSeparatorIndex + 1));
                if (child != null)
                {
                    return child;
                }
            }

            int lastSeparatorIndex = normalizedPath.LastIndexOf('/');
            string childName = lastSeparatorIndex >= 0
                ? normalizedPath.Substring(lastSeparatorIndex + 1)
                : normalizedPath;
            return FindChildRecursive(transform, childName);
        }

        internal void ThrowTo(
            HackerBossAI nextOwner,
            HackerThrownWeaponType nextWeaponType,
            Vector3 nextStartPosition,
            Vector3 nextLandingPosition,
            float nextTravelSeconds,
            AnimationCurve nextTravelSpeedCurve,
            Transform nextEquippedWeapon,
            Sprite nextFlyingSprite,
            Sprite nextGroundedSprite)
        {
            owner = nextOwner;
            WeaponType = nextWeaponType;
            startPosition = nextStartPosition;
            landingPosition = nextLandingPosition;
            travelSeconds = Mathf.Max(0.01f, nextTravelSeconds);
            travelSpeedCurve = nextTravelSpeedCurve;
            elapsed = 0f;
            isThrown = true;
            isRecalling = false;
            isOrbiting = false;
            equippedWeapon = nextEquippedWeapon;
            CacheWeaponRenderer();
            flyingSprite = nextFlyingSprite;
            groundedSprite = nextGroundedSprite;
            ApplyWeaponSprite(flyingSprite);
            MatchEquippedWeaponWorldScale();
            restoreEquippedWeapon = equippedWeapon != null && equippedWeapon.gameObject.activeSelf;
            if (restoreEquippedWeapon)
            {
                equippedWeapon.gameObject.SetActive(false);
            }

            transform.position = startPosition;
            ConfigurePhysicsCollisionsIgnored();
        }

        internal bool BeginRecall(
            Transform returnAnchor,
            Transform wireAnchor,
            float durationSeconds,
            float rotationDegrees)
        {
            if (!IsGrounded || returnAnchor == null || wireAnchor == null)
            {
                return false;
            }

            recallReturnAnchor = returnAnchor;
            recallRotationTarget = equippedWeapon != null ? equippedWeapon : returnAnchor;
            recallSeconds = Mathf.Max(0.01f, durationSeconds);
            recallElapsed = 0f;
            recallStartWireAnchorPosition = wireAnchor.position;
            recallWireAnchorLocalPosition = transform.InverseTransformPoint(wireAnchor.position);
            recallStartRotation = transform.rotation;
            recallRotationDegrees = rotationDegrees;
            isRecalling = true;
            return true;
        }

        internal bool BeginOrbit()
        {
            if (!IsGrounded)
            {
                return false;
            }

            isOrbiting = true;
            return true;
        }

        internal void SetOrbitPose(Vector3 position, Quaternion rotation)
        {
            if (!isOrbiting)
            {
                return;
            }

            transform.SetPositionAndRotation(position, rotation);
        }

        internal void EndOrbit()
        {
            isOrbiting = false;
        }

        private void Update()
        {
            if (isRecalling)
            {
                TickRecall();
                return;
            }

            if (!isThrown)
            {
                if (IsGrounded)
                {
                    FacePlayer();
                }

                return;
            }

            elapsed += EnemyTimeScale.DeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / travelSeconds);
            float progress = GetTravelProgress(normalizedTime);
            transform.position = Vector3.Lerp(startPosition, landingPosition, progress);
            if (normalizedTime >= 1f)
            {
                transform.position = landingPosition;
                isThrown = false;
                ApplyWeaponSprite(groundedSprite);
                owner?.RegisterGroundedWeapon(this);
                FacePlayer();
            }
        }

        internal void FacePlayer()
        {
            if (owner?.Player == null)
            {
                return;
            }

            Vector2 direction = (Vector2)owner.Player.position - (Vector2)transform.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.up = direction.normalized;
            }
        }

        private void OnDestroy()
        {
            RestoreEquippedWeapon();
            owner?.UnregisterGroundedWeapon(this);
        }

        internal void DespawnAndRestoreEquippedWeapon()
        {
            RestoreEquippedWeapon();
            Destroy(gameObject);
        }

        private void TickRecall()
        {
            if (recallReturnAnchor == null || recallRotationTarget == null)
            {
                isRecalling = false;
                return;
            }

            recallElapsed += EnemyTimeScale.DeltaTime;
            float progress = Mathf.Clamp01(recallElapsed / recallSeconds);
            Quaternion targetRotation = recallRotationTarget.rotation;
            transform.rotation = Quaternion.Slerp(recallStartRotation, targetRotation, progress)
                * Quaternion.Euler(0f, 0f, recallRotationDegrees * progress);
            Vector3 targetAnchorPosition = recallReturnAnchor.position;
            Vector3 wireAnchorPosition = Vector3.Lerp(recallStartWireAnchorPosition, targetAnchorPosition, progress);
            transform.position = wireAnchorPosition - transform.TransformVector(recallWireAnchorLocalPosition);
            if (progress < 1f)
            {
                return;
            }

            transform.rotation = targetRotation;
            transform.position = targetAnchorPosition - transform.TransformVector(recallWireAnchorLocalPosition);
            isRecalling = false;
            owner?.UnregisterGroundedWeapon(this);
            RestoreEquippedWeapon();
            Destroy(gameObject);
        }

        private void RestoreEquippedWeapon()
        {
            if (restoreEquippedWeapon && equippedWeapon != null)
            {
                equippedWeapon.gameObject.SetActive(true);
            }

            restoreEquippedWeapon = false;
        }

        private void CacheWeaponRenderer()
        {
            if (weaponRenderer == null)
            {
                weaponRenderer = FindPrimarySpriteRenderer(transform);
            }

            if (weaponRenderer != null && originalSprite == null)
            {
                originalSprite = weaponRenderer.sprite;
            }
        }

        private void ApplyWeaponSprite(Sprite sprite)
        {
            CacheWeaponRenderer();
            if (weaponRenderer != null)
            {
                weaponRenderer.sprite = sprite != null ? sprite : originalSprite;
            }
        }

        private void MatchEquippedWeaponWorldScale()
        {
            if (equippedWeapon == null || transform.parent != null)
            {
                return;
            }

            // 투척 프리팹은 월드 루트에 생성되므로, 손에 장착된 무기의 실제 표시 크기를 그대로 사용한다.
            SpriteRenderer equippedRenderer = FindPrimarySpriteRenderer(equippedWeapon);
            SpriteRenderer thrownRenderer = FindMatchingSpriteRenderer(equippedWeapon, equippedRenderer);
            if (equippedRenderer == null || thrownRenderer == null)
            {
                transform.localScale = equippedWeapon.lossyScale;
                return;
            }

            Vector3 targetScale = equippedRenderer.transform.lossyScale;
            Vector3 currentScale = thrownRenderer.transform.lossyScale;
            transform.localScale = Vector3.Scale(
                transform.localScale,
                new Vector3(
                    GetScaleRatio(targetScale.x, currentScale.x),
                    GetScaleRatio(targetScale.y, currentScale.y),
                    GetScaleRatio(targetScale.z, currentScale.z)));
        }

        private SpriteRenderer FindMatchingSpriteRenderer(
            Transform equippedRoot,
            SpriteRenderer equippedRenderer)
        {
            if (equippedRenderer == null)
            {
                return null;
            }

            string relativePath = GetRelativePath(equippedRoot, equippedRenderer.transform);
            Transform matchingTransform = string.IsNullOrEmpty(relativePath)
                ? transform
                : transform.Find(relativePath);
            SpriteRenderer matchingRenderer = matchingTransform != null
                ? matchingTransform.GetComponent<SpriteRenderer>()
                : null;
            return matchingRenderer ?? FindPrimarySpriteRenderer(transform);
        }

        private static SpriteRenderer FindPrimarySpriteRenderer(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            SpriteRenderer bestRenderer = null;
            float bestArea = float.NegativeInfinity;
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || renderer.sprite == null)
                {
                    continue;
                }

                Vector3 size = renderer.sprite.bounds.size;
                float area = size.x * size.y;
                if (area > bestArea)
                {
                    bestArea = area;
                    bestRenderer = renderer;
                }
            }

            return bestRenderer;
        }

        private static string GetRelativePath(Transform root, Transform child)
        {
            if (root == null
                || child == null
                || (child != root && !child.IsChildOf(root)))
            {
                return null;
            }

            if (child == root)
            {
                return string.Empty;
            }

            System.Collections.Generic.List<string> segments = new();
            Transform current = child;
            while (current != null && current != root)
            {
                segments.Add(current.name);
                current = current.parent;
            }

            segments.Reverse();
            return string.Join("/", segments);
        }

        private static float GetScaleRatio(float targetScale, float currentScale)
        {
            return Mathf.Abs(currentScale) > 0.0001f
                ? targetScale / currentScale
                : 1f;
        }

        private void ConfigurePhysicsCollisionsIgnored()
        {
            Rigidbody2D[] bodies = GetComponentsInChildren<Rigidbody2D>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody2D body = bodies[i];
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.bodyType = RigidbodyType2D.Kinematic;
            }

            Collider2D[] weaponColliders = GetComponentsInChildren<Collider2D>(true);
            IgnoreCollisionsWith(weaponColliders, owner != null ? owner.transform : null);
            IgnoreCollisionsWith(weaponColliders, owner != null ? owner.Player : null);
        }

        private static void IgnoreCollisionsWith(Collider2D[] weaponColliders, Transform target)
        {
            if (target == null)
            {
                return;
            }

            Collider2D[] targetColliders = target.GetComponentsInChildren<Collider2D>(true);
            for (int weaponIndex = 0; weaponIndex < weaponColliders.Length; weaponIndex++)
            {
                Collider2D weaponCollider = weaponColliders[weaponIndex];
                if (weaponCollider == null)
                {
                    continue;
                }

                for (int targetIndex = 0; targetIndex < targetColliders.Length; targetIndex++)
                {
                    Collider2D targetCollider = targetColliders[targetIndex];
                    if (targetCollider != null)
                    {
                        Physics2D.IgnoreCollision(weaponCollider, targetCollider, true);
                    }
                }
            }
        }

        private float GetTravelProgress(float normalizedTime)
        {
            if (travelSpeedCurve == null || travelSpeedCurve.length == 0)
            {
                return normalizedTime;
            }

            float fullArea = IntegrateSpeedCurve(1f);
            if (fullArea <= 0.0001f)
            {
                return normalizedTime;
            }

            return Mathf.Clamp01(IntegrateSpeedCurve(normalizedTime) / fullArea);
        }

        private float IntegrateSpeedCurve(float normalizedEndTime)
        {
            const int segments = 24;
            float endTime = Mathf.Clamp01(normalizedEndTime);
            if (endTime <= 0f)
            {
                return 0f;
            }

            float previousSpeed = EvaluateTravelSpeed(0f);
            float area = 0f;
            for (int i = 1; i <= segments; i++)
            {
                float time = endTime * i / segments;
                float speed = EvaluateTravelSpeed(time);
                area += (previousSpeed + speed) * 0.5f * (endTime / segments);
                previousSpeed = speed;
            }

            return area;
        }

        private float EvaluateTravelSpeed(float normalizedTime)
        {
            return Mathf.Max(0f, travelSpeedCurve.Evaluate(normalizedTime));
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
