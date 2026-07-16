using UnityEngine;

namespace Week14.Enemy
{
    // 보스 위치를 중심으로 커지는 원형 범위 인디케이터. BossDashTrajectoryVfx와 같은 스타일(Spawn 정적
    // 팩토리 + UpdateVfx)로 만들었다. 배경(전체 범위) 스프라이트 위에 채움 스프라이트가 0->1로 커지며
    // 겹쳐지는 방식으로, ParryTimedBomb.cs가 인라인으로 하던 것과 동일한 시각 효과를 낸다.
    internal sealed class BossAreaIndicatorVfx : MonoBehaviour
    {
        private Transform fillTransform;
        private float fullScale = 1f;

        internal static BossAreaIndicatorVfx Spawn(
            Sprite sprite,
            float radius,
            Color backgroundColor,
            Color fillColor,
            int sortingOrder)
        {
            GameObject go = new("BossAreaIndicatorVfx");
            BossAreaIndicatorVfx vfx = go.AddComponent<BossAreaIndicatorVfx>();
            vfx.Setup(sprite, radius, backgroundColor, fillColor, sortingOrder);
            return vfx;
        }

        private void Setup(Sprite sprite, float radius, Color backgroundColor, Color fillColor, int sortingOrder)
        {
            fullScale = sprite != null
                ? (radius * 2f) / Mathf.Max(0.01f, sprite.bounds.size.x)
                : 1f;

            GameObject bgGo = new("Background");
            bgGo.transform.SetParent(transform, false);
            bgGo.transform.localScale = Vector3.one * fullScale;
            SpriteRenderer bgRenderer = bgGo.AddComponent<SpriteRenderer>();
            bgRenderer.sprite = sprite;
            bgRenderer.color = backgroundColor;
            BossSorting.Apply(bgRenderer);
            bgRenderer.sortingOrder = sortingOrder;

            GameObject fillGo = new("Fill");
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.localScale = Vector3.zero;
            SpriteRenderer fillRenderer = fillGo.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = sprite;
            fillRenderer.color = fillColor;
            BossSorting.Apply(fillRenderer);
            fillRenderer.sortingOrder = sortingOrder + 1;
            fillTransform = fillGo.transform;
        }

        // progress: 0~1, 1이면 채움이 배경(=범위 반지름)을 꽉 채운다.
        internal void UpdateVfx(Vector3 center, float progress)
        {
            transform.position = center;
            if (fillTransform != null)
            {
                fillTransform.localScale = Vector3.one * (fullScale * Mathf.Clamp01(progress));
            }
        }
    }
}
