using UnityEngine;
using Week14.Input;

namespace Week14.Combat
{
    public sealed class SniperChargeIndicator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] renderers;
        [SerializeField, Min(0.01f)] private float maxScale = 1f;
        [SerializeField, Min(0.01f)] private float minScale = 0.2f;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color maxChargeColor = new Color(1f, 0.85f, 0f, 1f);
        [SerializeField, Min(0f)] private float colorTransitionSpeed = 8f;

        private float chargeRatio;

        private void Awake()
        {
            gameObject.SetActive(false);
        }

        public void SetChargeRatio(float ratio)
        {
            chargeRatio = ratio;
            bool active = ratio > 0f;
            if (gameObject.activeSelf != active)
            {
                gameObject.SetActive(active);
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
            float scale = Mathf.Lerp(maxScale, minScale, Mathf.Clamp01(chargeRatio));
            transform.localScale = Vector3.one * scale;

            Color targetColor = chargeRatio >= 1f ? maxChargeColor : normalColor;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].color = Color.Lerp(renderers[i].color, targetColor, colorTransitionSpeed * Time.deltaTime);
            }
        }
    }
}
