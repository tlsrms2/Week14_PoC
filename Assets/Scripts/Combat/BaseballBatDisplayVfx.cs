using UnityEngine;

namespace Week14.Combat
{
    public sealed class BaseballBatDisplayVfx : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;

        public void Initialize(Sprite sprite, int sortingOrder, Vector3 scale, Color color)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.color = color;
            transform.localScale = scale;
        }

        public void SetSprite(Sprite sprite)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = sprite;
            }
        }

        public void SetScale(Vector3 scale)
        {
            transform.localScale = scale;
        }

        public void SetColor(Color color)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
            }
        }

        // orbitRotationDegrees: 와인드업/스윙 등 "플레이어를 중심으로 실제로 도는" 게임플레이 회전값입니다.
        // 위치와 회전 둘 다에 반영되어야 궤도를 그리며 도는 것처럼 보입니다.
        // spriteRotationOffsetDegrees: 스프라이트 아트 방향 보정용이라 위치에는 영향을 주면 안 됩니다.
        public void SetPose(Vector3 origin, Vector2 direction, float offsetDistance, float orbitRotationDegrees, float spriteRotationOffsetDegrees)
        {
            Vector2 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            float baseAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
            float orbitAngle = baseAngle + orbitRotationDegrees;
            float orbitAngleRad = orbitAngle * Mathf.Deg2Rad;
            Vector2 orbitDirection = new Vector2(Mathf.Cos(orbitAngleRad), Mathf.Sin(orbitAngleRad));

            origin.z = 0f;
            transform.position = origin + (Vector3)(orbitDirection * offsetDistance);
            transform.rotation = Quaternion.Euler(0f, 0f, orbitAngle + spriteRotationOffsetDegrees);
        }
    }
}
