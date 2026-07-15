using UnityEngine;

namespace Week14.Enemy
{
    public enum HackerThrownWeaponType
    {
        Bayonet,
        Gun,
        Sword
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
            Transform nextEquippedWeapon)
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
                return;
            }

            elapsed += EnemyTimeScale.DeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / travelSeconds);
            float progress = GetTravelProgress(normalizedTime);
            transform.position = Vector3.Lerp(startPosition, landingPosition, progress);
            if (normalizedTime >= 1f)
            {
                isThrown = false;
                owner?.RegisterGroundedWeapon(this);
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

        private void MatchEquippedWeaponWorldScale()
        {
            if (equippedWeapon == null || transform.parent != null)
            {
                return;
            }

            // 투척 프리팹은 월드 루트에 생성되므로, 손에 장착된 무기의 실제 표시 크기를 그대로 사용한다.
            transform.localScale = equippedWeapon.lossyScale;
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
