using UnityEngine;

namespace Week14.Combat
{
    public sealed class SniperChargeTickEffect : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private float startScale;
        private float lifetimeSeconds;
        private float elapsed;

        public static SniperChargeTickEffect Spawn(
            Sprite sprite,
            Transform parent,
            Color color,
            float startScale,
            float lifetimeSeconds,
            int sortingOrder)
        {
            if (sprite == null || lifetimeSeconds <= 0f)
            {
                return null;
            }

            GameObject instance = new("SniperChargeTickEffect");
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            SpriteRenderer spriteRenderer = instance.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = sortingOrder;

            SniperChargeTickEffect effect = instance.AddComponent<SniperChargeTickEffect>();
            effect.spriteRenderer = spriteRenderer;
            effect.startScale = Mathf.Max(0.01f, startScale);
            effect.lifetimeSeconds = lifetimeSeconds;
            instance.transform.localScale = Vector3.one * effect.startScale;
            return effect;
        }

        public void Cancel()
        {
            if (this != null)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifetimeSeconds);
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, 0f, t);

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
