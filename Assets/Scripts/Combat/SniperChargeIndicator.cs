using UnityEngine;
using Week14.Input;

namespace Week14.Combat
{
    public sealed class SniperChargeIndicator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] renderers;
        [SerializeField, Min(0.01f)] private float maxScale = 1f;
        [SerializeField, Min(0.01f)] private float minScale = 0.2f;
        [Tooltip("차지 시작 시점의 탄환 수로 나누어 탄환 1개 소모당 회전할 각도(Z축)를 계산합니다.")]
        [SerializeField] private float totalRotationDegrees = -90f;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color maxChargeColor = new Color(1f, 0.85f, 0f, 1f);
        [SerializeField, Min(0f)] private float colorTransitionSpeed = 8f;
        [Tooltip("스케일이 목표값으로 근접하는 속도입니다.")]
        [SerializeField, Min(0f)] private float scaleTransitionSpeed = 15f;
        [Tooltip("회전이 목표값으로 근접하는 속도입니다.")]
        [SerializeField, Min(0f)] private float rotationTransitionSpeed = 15f;

        private int totalBulletCount;
        private int consumedBulletCount;
        private float scaleStep;
        private float rotationStep;
        private float currentScale;
        private float currentRotationZ;

        private void Awake()
        {
            gameObject.SetActive(false);
        }

        public void BeginCharge(int currentBulletCount)
        {
            totalBulletCount = Mathf.Max(0, currentBulletCount);
            consumedBulletCount = 0;
            currentScale = maxScale;
            currentRotationZ = 0f;

            if (totalBulletCount > 0)
            {
                scaleStep = (maxScale - minScale) / totalBulletCount;
                rotationStep = totalRotationDegrees / totalBulletCount;
            }
            else
            {
                scaleStep = 0f;
                rotationStep = 0f;
            }

            bool active = totalBulletCount > 0;
            if (gameObject.activeSelf != active)
            {
                gameObject.SetActive(active);
            }
        }

        public void ConsumeBullet()
        {
            if (totalBulletCount <= 0) return;
            consumedBulletCount = Mathf.Min(consumedBulletCount + 1, totalBulletCount);
        }

        public void EndCharge()
        {
            totalBulletCount = 0;
            consumedBulletCount = 0;
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            PlayerCombatController player = PlayerCombatController.Active;
            if (player == null || !player.IsReticleVisible)
            {
                gameObject.SetActive(false);
                return;
            }

            FollowCursor();
            ApplyVisual();
        }

        private void FollowCursor()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 worldPos = cam.ScreenToWorldPoint(GameInput.MouseScreenPosition);
            worldPos.z = transform.position.z;
            transform.position = worldPos;
        }

        private void ApplyVisual()
        {
            float targetScale = totalBulletCount > 0
                ? Mathf.Max(minScale, maxScale - scaleStep * consumedBulletCount)
                : maxScale;
            currentScale = Mathf.Lerp(currentScale, targetScale, scaleTransitionSpeed * Time.deltaTime);
            transform.localScale = Vector3.one * currentScale;

            float targetRotationZ = rotationStep * consumedBulletCount;
            currentRotationZ = Mathf.LerpAngle(currentRotationZ, targetRotationZ, rotationTransitionSpeed * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0f, 0f, currentRotationZ);

            bool fullyCharged = totalBulletCount > 0 && consumedBulletCount >= totalBulletCount;
            Color targetColor = fullyCharged ? maxChargeColor : normalColor;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].color = Color.Lerp(renderers[i].color, targetColor, colorTransitionSpeed * Time.deltaTime);
            }
        }
    }
}
