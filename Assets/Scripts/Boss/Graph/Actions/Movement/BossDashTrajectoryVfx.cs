using UnityEngine;

namespace Week14.Enemy
{
    internal sealed class BossDashTrajectoryVfx : MonoBehaviour
    {
        private Transform fillTransform;
        private float length;
        private float width;

        internal static BossDashTrajectoryVfx Spawn(
            Sprite sprite,
            float length,
            float width,
            Color backgroundColor,
            Color fillColor,
            int sortingOrder)
        {
            GameObject go = new("BossDashTrajectoryVfx");
            BossDashTrajectoryVfx vfx = go.AddComponent<BossDashTrajectoryVfx>();
            vfx.Setup(sprite, length, width, backgroundColor, fillColor, sortingOrder);
            return vfx;
        }

        private void Setup(
            Sprite sprite,
            float length,
            float width,
            Color backgroundColor,
            Color fillColor,
            int sortingOrder)
        {
            this.length = length;
            this.width = width;

            GameObject bgGo = new("Background");
            bgGo.transform.SetParent(transform, false);
            bgGo.transform.localScale = new Vector3(width, length, 1f);
            SpriteRenderer bgRenderer = bgGo.AddComponent<SpriteRenderer>();
            bgRenderer.sprite = sprite;
            bgRenderer.color = backgroundColor;
            bgRenderer.sortingOrder = sortingOrder;

            GameObject fillGo = new("Fill");
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.localScale = new Vector3(0f, length, 1f);
            SpriteRenderer fillRenderer = fillGo.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = sprite;
            fillRenderer.color = fillColor;
            fillRenderer.sortingOrder = sortingOrder + 1;
            fillTransform = fillGo.transform;
        }

        // progress: 0~1, 1이면 fill이 background를 꽉 채움
        internal void UpdateVfx(Vector3 bossPosition, Vector2 direction, float progress)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 dir = direction.normalized;
            Vector2 center = (Vector2)bossPosition + dir * (length * 0.5f);
            transform.position = new Vector3(center.x, center.y, bossPosition.z);

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            if (fillTransform != null)
            {
                fillTransform.localScale = new Vector3(width * Mathf.Clamp01(progress), length, 1f);
            }
        }
    }
}
