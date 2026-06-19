using System.Collections.Generic;
using UnityEngine;

namespace Week14.UI
{
    public sealed class AttackTimingOutline : MonoBehaviour
    {
        public enum OutlineShape
        {
            Circle,
            Square
        }

        private const string RootName = "AttackTimingOutline";
        private const string BulletRootName = "AttackBullets";
        private const int MaxPointCount = 73;
        private const int MaxBulletsPerRow = 10;

        [SerializeField, Min(0.1f)] private float fallbackRadius = 0.85f;
        [SerializeField, Min(0f)] private float radiusPadding = 0.02f;
        [SerializeField, Min(0.001f)] private float width = 0.045f;
        [SerializeField, Min(0.001f)] private float bulletWidth = 0.067f;
        [SerializeField, Min(0.01f)] private float bulletLength = 0.2f;
        [SerializeField, Min(0.01f)] private float bulletGap = 0.13f;
        [SerializeField, Min(0.01f)] private float bulletRowGap = 0.18f;
        [SerializeField, Min(0f)] private float bulletYOffset = 0.26f;
        [SerializeField] private Color color = new(1f, 0.82f, 0.18f, 0.95f);
        [SerializeField] private Color spentBulletColor = new(1f, 1f, 1f, 0.22f);
        [SerializeField] private int sortingOrder = 8;
        [SerializeField] private OutlineShape outlineShape = OutlineShape.Circle;
        [SerializeField] private Transform targetRoot;

        private Transform root;
        private Transform bulletRoot;
        private LineRenderer line;
        private readonly List<LineRenderer> bulletLines = new();
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

            if (bulletRoot != null)
            {
                bulletRoot.SetParent(targetRoot, false);
            }
        }

        public void SetOutlineShape(OutlineShape shape)
        {
            outlineShape = shape;
        }

        public void Show(float remainingSeconds, float durationSeconds)
        {
            Show(remainingSeconds, durationSeconds, 0, 0);
        }

        public void Show(float remainingSeconds, float durationSeconds, int loadedBulletCount, int totalBulletCount)
        {
            if (durationSeconds <= 0f || remainingSeconds <= 0f)
            {
                if (totalBulletCount > 0)
                {
                    ShowBullets(loadedBulletCount, totalBulletCount);
                }
                else
                {
                    Hide();
                }

                return;
            }

            EnsureLine();
            UpdateRootTransform();

            float ratio = Mathf.Clamp01(remainingSeconds / durationSeconds);
            DrawOutline(line, ratio);
            DrawBullets(loadedBulletCount, totalBulletCount);
        }

        public void ShowBullets(int loadedBulletCount, int totalBulletCount)
        {
            if (totalBulletCount <= 0)
            {
                Hide();
                return;
            }

            EnsureLine();
            UpdateRootTransform();
            SetLineVisible(line, false);
            DrawBullets(loadedBulletCount, totalBulletCount);
        }

        public void Hide()
        {
            SetLineVisible(line, false);
            SetBulletsVisible(false);
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

                if (bulletRoot != null && bulletRoot.parent != targetRoot)
                {
                    bulletRoot.SetParent(targetRoot, false);
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
            EnsureBulletRoot();
        }

        private void DrawOutline(LineRenderer renderer, float ratio)
        {
            if (outlineShape == OutlineShape.Square)
            {
                DrawSquareOutline(renderer, ratio);
                return;
            }

            DrawArc(renderer, ratio, 90f, 360f, color, MaxPointCount, ratio >= 0.995f);
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
            renderer.numCornerVertices = 2;
            renderer.numCapVertices = 2;
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

        private void DrawSquareOutline(LineRenderer renderer, float ratio)
        {
            if (renderer == null || ratio <= 0f)
            {
                SetLineVisible(renderer, false);
                return;
            }

            Vector2 halfExtents = GetOutlineHalfExtents();
            Vector2[] corners =
            {
                new(halfExtents.x, halfExtents.y),
                new(halfExtents.x, -halfExtents.y),
                new(-halfExtents.x, -halfExtents.y),
                new(-halfExtents.x, halfExtents.y),
                new(halfExtents.x, halfExtents.y)
            };

            float[] segmentLengths =
            {
                Vector2.Distance(corners[0], corners[1]),
                Vector2.Distance(corners[1], corners[2]),
                Vector2.Distance(corners[2], corners[3]),
                Vector2.Distance(corners[3], corners[4])
            };
            float perimeter = segmentLengths[0] + segmentLengths[1] + segmentLengths[2] + segmentLengths[3];
            float targetLength = perimeter * Mathf.Clamp01(ratio);
            int pointCount = Mathf.Max(2, Mathf.CeilToInt(MaxPointCount * ratio));

            renderer.enabled = true;
            renderer.loop = ratio >= 0.995f;
            renderer.positionCount = renderer.loop ? 4 : pointCount;
            renderer.numCornerVertices = 0;
            renderer.numCapVertices = 0;
            renderer.startWidth = width;
            renderer.endWidth = width;
            renderer.startColor = color;
            renderer.endColor = color;
            renderer.sortingOrder = sortingOrder;

            if (renderer.loop)
            {
                for (int i = 0; i < 4; i++)
                {
                    renderer.SetPosition(i, corners[i]);
                }

                return;
            }

            for (int i = 0; i < pointCount; i++)
            {
                float t = pointCount <= 1 ? 0f : (float)i / (pointCount - 1);
                renderer.SetPosition(i, GetSquareOutlinePoint(corners, segmentLengths, targetLength * t));
            }
        }

        private static Vector2 GetSquareOutlinePoint(Vector2[] corners, float[] segmentLengths, float distance)
        {
            float remaining = Mathf.Max(0f, distance);
            for (int i = 0; i < segmentLengths.Length; i++)
            {
                if (remaining <= segmentLengths[i] || i == segmentLengths.Length - 1)
                {
                    float t = segmentLengths[i] <= 0f ? 0f : Mathf.Clamp01(remaining / segmentLengths[i]);
                    return Vector2.Lerp(corners[i], corners[i + 1], t);
                }

                remaining -= segmentLengths[i];
            }

            return corners[corners.Length - 1];
        }

        private void DrawBullets(int loadedBulletCount, int totalBulletCount)
        {
            if (totalBulletCount <= 0)
            {
                SetBulletsVisible(false);
                return;
            }

            EnsureBulletRoot();

            int loadedCount = Mathf.Clamp(loadedBulletCount, 0, totalBulletCount);
            float firstRowY = GetBulletAnchorY() + bulletYOffset;
            float rowSpacing = Mathf.Max(bulletRowGap, bulletLength + bulletWidth * 1.35f);

            for (int i = 0; i < totalBulletCount; i++)
            {
                int row = i / MaxBulletsPerRow;
                int column = i % MaxBulletsPerRow;
                int rowStartIndex = row * MaxBulletsPerRow;
                int rowBulletCount = Mathf.Min(MaxBulletsPerRow, totalBulletCount - rowStartIndex);
                float startX = -((rowBulletCount - 1) * bulletGap) * 0.5f;
                float x = startX + column * bulletGap;
                float y = firstRowY + row * rowSpacing;

                LineRenderer bullet = GetBulletLine(i);
                bullet.enabled = true;
                bullet.positionCount = 2;
                bullet.loop = false;
                bullet.useWorldSpace = false;
                bullet.numCapVertices = 4;
                bullet.numCornerVertices = 2;
                bullet.material = GetMaterial();
                bullet.startWidth = bulletWidth;
                bullet.endWidth = bulletWidth;
                bullet.startColor = i < loadedCount ? color : spentBulletColor;
                bullet.endColor = bullet.startColor;
                bullet.sortingOrder = sortingOrder + 1;

                bullet.SetPosition(0, new Vector3(x, y - bulletLength * 0.5f, 0f));
                bullet.SetPosition(1, new Vector3(x, y + bulletLength * 0.5f, 0f));
            }

            for (int i = totalBulletCount; i < bulletLines.Count; i++)
            {
                SetLineVisible(bulletLines[i], false);
            }
        }

        private void SetBulletsVisible(bool visible)
        {
            for (int i = 0; i < bulletLines.Count; i++)
            {
                SetLineVisible(bulletLines[i], visible);
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

            if (bulletRoot != null)
            {
                bulletRoot.localPosition = root.localPosition;
                bulletRoot.localRotation = Quaternion.identity;
                bulletRoot.localScale = Vector3.one;
            }
        }

        private void EnsureBulletRoot()
        {
            if (targetRoot == null)
            {
                targetRoot = transform;
            }

            if (bulletRoot != null)
            {
                return;
            }

            bulletRoot = targetRoot.Find(BulletRootName);
            if (bulletRoot == null)
            {
                GameObject bulletRootObject = new GameObject(BulletRootName);
                bulletRootObject.transform.SetParent(targetRoot, false);
                bulletRoot = bulletRootObject.transform;
            }
        }

        private LineRenderer GetBulletLine(int index)
        {
            EnsureBulletRoot();

            while (bulletLines.Count <= index)
            {
                GameObject bulletObject = new GameObject($"Bullet_{bulletLines.Count}");
                bulletObject.transform.SetParent(bulletRoot, false);
                LineRenderer bullet = bulletObject.AddComponent<LineRenderer>();
                bulletLines.Add(bullet);
            }

            return bulletLines[index];
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

        private Vector2 GetOutlineHalfExtents()
        {
            SpriteRenderer renderer = targetRoot != null ? targetRoot.GetComponent<SpriteRenderer>() : null;
            if (renderer == null || renderer.sprite == null)
            {
                return Vector2.one * fallbackRadius;
            }

            Bounds bounds = renderer.sprite.bounds;
            return new Vector2(
                Mathf.Max(0.05f, bounds.extents.x + radiusPadding),
                Mathf.Max(0.05f, bounds.extents.y + radiusPadding));
        }

        private float GetBulletAnchorY()
        {
            return outlineShape == OutlineShape.Square ? GetOutlineHalfExtents().y : GetOutlineRadius();
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
