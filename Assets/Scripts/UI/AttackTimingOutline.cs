using UnityEngine;

namespace Week14.UI
{
    public sealed class AttackTimingOutline : MonoBehaviour
    {
        private const string RootName = "AttackTimingOutline";
        private const int MaxPointCount = 73;

        [SerializeField, Min(0.1f)] private float fallbackRadius = 0.85f;
        [SerializeField, Min(0f)] private float radiusPadding = 0.02f;
        [SerializeField, Min(0.001f)] private float width = 0.045f;
        [SerializeField] private Color color = new(1f, 0.82f, 0.18f, 0.95f);
        [SerializeField] private int sortingOrder = 8;
        [SerializeField] private Transform targetRoot;

        private Transform root;
        private LineRenderer line;
        private static Material material;

        public void SetTarget(Transform target)
        {
            Transform nextTarget = target != null ? target : transform;
            if (targetRoot == nextTarget)
            {
                return;
            }

            targetRoot = nextTarget;
            if (root != null)
            {
                root.SetParent(targetRoot, false);
            }
        }

        public void Show(float remainingSeconds, float durationSeconds)
        {
            if (durationSeconds <= 0f || remainingSeconds <= 0f)
            {
                Hide();
                return;
            }

            EnsureLine();
            UpdateRootTransform();

            float ratio = Mathf.Clamp01(remainingSeconds / durationSeconds);
            DrawArc(line, ratio, 90f, 360f, color, MaxPointCount, ratio >= 0.995f);
        }

        public void Hide()
        {
            SetLineVisible(line, false);
        }

        private void EnsureLine()
        {
            if (targetRoot == null)
            {
                targetRoot = transform;
            }

            if (line != null)
            {
                if (root != null && root.parent != targetRoot)
                {
                    root.SetParent(targetRoot, false);
                }

                return;
            }

            root = targetRoot.Find(RootName);
            if (root == null)
            {
                GameObject rootObject = new GameObject(RootName);
                rootObject.transform.SetParent(targetRoot, false);
                root = rootObject.transform;
            }

            line = root.GetComponent<LineRenderer>();
            if (line == null)
            {
                line = root.gameObject.AddComponent<LineRenderer>();
            }

            line.useWorldSpace = false;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.material = GetMaterial();
        }

        private void DrawArc(
            LineRenderer renderer,
            float ratio,
            float startDegrees,
            float sweepDegrees,
            Color lineColor,
            int maxPointCount,
            bool loop)
        {
            if (renderer == null || ratio <= 0f)
            {
                SetLineVisible(renderer, false);
                return;
            }

            float outlineRadius = GetOutlineRadius();
            float arcDegrees = sweepDegrees * Mathf.Clamp01(ratio);
            int pointCount = Mathf.Max(2, Mathf.CeilToInt(maxPointCount * ratio));

            renderer.enabled = true;
            renderer.loop = loop;
            renderer.positionCount = loop ? maxPointCount : pointCount;
            renderer.startWidth = width;
            renderer.endWidth = width;
            renderer.startColor = lineColor;
            renderer.endColor = lineColor;
            renderer.sortingOrder = sortingOrder;

            int count = renderer.positionCount;
            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0f : (float)i / (count - 1);
                float angle = (startDegrees - arcDegrees * t) * Mathf.Deg2Rad;
                renderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * outlineRadius, Mathf.Sin(angle) * outlineRadius, 0f));
            }
        }

        private static void SetLineVisible(LineRenderer renderer, bool visible)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }

        private void UpdateRootTransform()
        {
            if (root == null)
            {
                return;
            }

            root.localPosition = GetSpriteCenter();
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
        }

        private float GetOutlineRadius()
        {
            SpriteRenderer renderer = targetRoot != null ? targetRoot.GetComponent<SpriteRenderer>() : null;
            if (renderer == null || renderer.sprite == null)
            {
                return fallbackRadius;
            }

            Bounds bounds = renderer.sprite.bounds;
            return Mathf.Max(bounds.extents.x, bounds.extents.y) + radiusPadding;
        }

        private Vector3 GetSpriteCenter()
        {
            SpriteRenderer renderer = targetRoot != null ? targetRoot.GetComponent<SpriteRenderer>() : null;
            if (renderer == null || renderer.sprite == null)
            {
                return Vector3.zero;
            }

            return renderer.sprite.bounds.center;
        }

        private static Material GetMaterial()
        {
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Sprites/Default");
            material = shader != null ? new Material(shader) : null;
            return material;
        }
    }
}
